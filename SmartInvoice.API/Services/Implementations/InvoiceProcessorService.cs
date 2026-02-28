using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Linq;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartInvoice.API.DTOs.Invoice;
using SmartInvoice.API.Entities.JsonModels;
using SmartInvoice.API.Repositories.Interfaces;
using SmartInvoice.API.Services.Interfaces;

namespace SmartInvoice.API.Services.Implementations
{
    public class InvoiceProcessorService : IInvoiceProcessorService
    {
        private readonly ILogger<InvoiceProcessorService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _xsdPath;
        private readonly IUnitOfWork _unitOfWork;

        public InvoiceProcessorService(
            ILogger<InvoiceProcessorService> logger,
            IHttpClientFactory httpClientFactory,
            IHostEnvironment env,
            IUnitOfWork unitOfWork)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _xsdPath = Path.Combine(env.ContentRootPath, "Resources", "InvoiceSchema.xsd");
            _unitOfWork = unitOfWork;
        }

        public ValidationResultDto ValidateStructure(string xmlPath)
        {
            var result = new ValidationResultDto();

            try
            {
                var settings = new XmlReaderSettings
                {
                    ValidationType = ValidationType.Schema
                };
                settings.Schemas.Add(null, _xsdPath);

                settings.ValidationEventHandler += (sender, args) =>
                {
                    result.AddError($"Lỗi cấu trúc XML (Dòng {args.Exception.LineNumber}: {args.Message})");
                };

                using (var reader = XmlReader.Create(xmlPath, settings))
                {
                    while (reader.Read()) { }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi đọc file XML tại ValidateStructure");
                result.AddError($"Lỗi hệ thống khi đọc file XML: {ex.Message}");
            }

            return result;
        }

        public ValidationResultDto VerifyDigitalSignature(string xmlPath)
        {
            var result = new ValidationResultDto();

            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.PreserveWhitespace = true;
                xmlDoc.Load(xmlPath);

                bool isCashRegister = false;
                XmlNode khhDonNode = xmlDoc.SelectSingleNode("//*[local-name()='KHHDon']");
                if (khhDonNode != null && !string.IsNullOrEmpty(khhDonNode.InnerText) && khhDonNode.InnerText.Length >= 4 && char.ToUpper(khhDonNode.InnerText[3]) == 'M')
                {
                    isCashRegister = true;
                }

                XmlNodeList nodeList = xmlDoc.GetElementsByTagName("Signature");
                if (nodeList.Count == 0)
                {
                    if (isCashRegister)
                    {
                        result.AddWarning("Hóa đơn khởi tạo từ máy tính tiền không có chữ ký số (Hợp lệ theo quy định).");
                        return result;
                    }

                    result.AddError("Không tìm thấy Chữ ký số trong file.");
                    return result;
                }

                SignedXml signedXml = new SignedXml(xmlDoc);
                signedXml.LoadXml((XmlElement)nodeList[0]);

                bool isValidSignature = signedXml.CheckSignature();

                if (isValidSignature)
                {
                    if (signedXml.KeyInfo != null)
                    {
                        foreach (KeyInfoClause clause in signedXml.KeyInfo)
                        {
                            if (clause is KeyInfoX509Data x509Data)
                            {
                                if (x509Data.Certificates.Count > 0)
                                {
                                    X509Certificate2 cert = (X509Certificate2)x509Data.Certificates[0];
                                    result.SignerSubject = cert.Subject;

                                    XmlNode nLapNode = xmlDoc.SelectSingleNode("//*[local-name()='NLap']");
                                    if (nLapNode != null && DateTime.TryParse(nLapNode.InnerText, out DateTime invoiceDate))
                                    {
                                        if (invoiceDate < cert.NotBefore || invoiceDate > cert.NotAfter)
                                        {
                                            result.AddError($"[RỦI RO CHỮ KÝ] Chữ ký số không có hiệu lực tại thời điểm lập hóa đơn ({invoiceDate:dd/MM/yyyy}). Hiệu lực chứng thư: {cert.NotBefore:dd/MM/yyyy} - {cert.NotAfter:dd/MM/yyyy}.");
                                        }
                                    }

                                    break;
                                }
                            }
                        }
                    }
                }
                else
                {
                    result.AddError("Chữ ký số KHÔNG HỢP LỆ. Hóa đơn có thể đã bị sửa đổi.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kiểm tra chữ ký");
                result.AddError($"Lỗi khi kiểm tra chữ ký: {ex.Message}");
            }

            return result;
        }

        public InvoiceExtractedData ExtractData(string xmlPath)
        {
            var extractedData = new InvoiceExtractedData
            {
                LineItems = new System.Collections.Generic.List<SmartInvoice.API.Entities.JsonModels.InvoiceLineItem>()
            };

            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(xmlPath);

                // Try capturing additional general invoice details
                extractedData.PaymentTerms = GetNodeValue(xmlDoc, "HTTT");

                extractedData.InvoiceTemplateCode = GetNodeValue(xmlDoc, "KHMSHDon");
                extractedData.InvoiceSymbol = GetNodeValue(xmlDoc, "KHHDon");
                extractedData.InvoiceNumber = GetNodeValue(xmlDoc, "SHDon");

                XmlNode nBan = xmlDoc.SelectSingleNode("//*[local-name()='NBan']");
                extractedData.SellerName = nBan?.SelectSingleNode(".//*[local-name()='Ten']")?.InnerText;
                extractedData.SellerTaxCode = nBan?.SelectSingleNode(".//*[local-name()='MST']")?.InnerText;

                string nLapStr = GetNodeValue(xmlDoc, "NLap");
                if (DateTime.TryParse(nLapStr, out DateTime dtLap))
                {
                    extractedData.InvoiceDate = dtLap;
                }

                string sTotalPreTax = GetNodeValue(xmlDoc, "TgTCThue");
                string sTotalTax = GetNodeValue(xmlDoc, "TgTThue");
                string sTotalAmount = GetNodeValue(xmlDoc, "TgTTTBSo");

                decimal.TryParse(sTotalPreTax, out decimal totalPreTax);
                decimal.TryParse(sTotalTax, out decimal totalTax);
                decimal.TryParse(sTotalAmount, out decimal totalAmountInfo);

                extractedData.TotalPreTax = totalPreTax;
                extractedData.TotalTaxAmount = totalTax;
                extractedData.TotalAmount = totalAmountInfo;

                // Process Line Items
                XmlNodeList items = xmlDoc.SelectNodes("//*[local-name()='HHDVu']");
                int stt = 1;
                foreach (XmlNode item in items)
                {
                    string name = item.SelectSingleNode(".//*[local-name()='Ten']")?.InnerText;
                    string unit = item.SelectSingleNode(".//*[local-name()='DVTinh']")?.InnerText;
                    string sQty = item.SelectSingleNode(".//*[local-name()='SLuong']")?.InnerText;
                    string sPrice = item.SelectSingleNode(".//*[local-name()='DGia']")?.InnerText;
                    string sTotal = item.SelectSingleNode(".//*[local-name()='ThTien']")?.InnerText;
                    string sTaxRate = item.SelectSingleNode(".//*[local-name()='TSuat']")?.InnerText;
                    string sTaxAmount = item.SelectSingleNode(".//*[local-name()='TienThue']")?.InnerText;

                    decimal.TryParse(sQty, out decimal qty);
                    decimal.TryParse(sPrice, out decimal price);
                    decimal.TryParse(sTotal, out decimal total);
                    decimal.TryParse(sTaxAmount, out decimal taxAmount);

                    int vatRate = 0;
                    if (!string.IsNullOrEmpty(sTaxRate))
                    {
                        if (sTaxRate.Contains("%"))
                        {
                            int.TryParse(sTaxRate.Replace("%", ""), out vatRate);
                        }
                    }

                    if (!string.IsNullOrEmpty(name))
                    {
                        extractedData.LineItems.Add(new SmartInvoice.API.Entities.JsonModels.InvoiceLineItem
                        {
                            Stt = stt++,
                            ProductName = name,
                            Unit = unit,
                            Quantity = qty,
                            UnitPrice = price,
                            TotalAmount = total,
                            VatRate = vatRate,
                            VatAmount = taxAmount
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trích xuất dữ liệu XML");
            }

            return extractedData;
        }

        public async Task<ValidationResultDto> ValidateBusinessLogicAsync(string xmlPath)
        {
            var result = new ValidationResultDto();

            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(xmlPath);

                string GetVal(string tag) => GetNodeValue(xmlDoc, tag);

                string pBan = GetVal("PBan");
                string thDon = GetVal("THDon");
                string khmshDon = GetVal("KHMSHDon");
                string mauSo = GetVal("MauSo");
                string khhDon = GetVal("KHHDon");
                string shDon = GetVal("SHDon");
                string nLap = GetVal("NLap");
                string dvtTe = GetVal("DVTTe");
                string tGia = GetVal("TGia");
                string mccqt = GetVal("MCCQT");
                string ngayKy = GetVal("NgayKy");
                string tchDon = GetVal("TCHDon");

                bool isDataValid = true;

                // A. Phiên bản (PBan)
                isDataValid &= CheckMandatory(pBan, "Phiên bản (PBan)", result);
                if (!string.IsNullOrEmpty(pBan) && pBan != "2.0.1" && pBan != "2.0.0" && pBan != "2.1.0")
                {
                    result.AddError($"[LỖI CẤU TRÚC] Phiên bản XML '{pBan}' không được hỗ trợ. Bắt buộc phải là '2.0.1' (hoặc '2.0.0').");
                    isDataValid = false;
                }

                // B. Cấu trúc chuỗi định danh (KHMSHDon, KHHDon, SHDon)
                isDataValid &= CheckMandatory(khmshDon, "Ký hiệu mẫu số (KHMSHDon)", result);
                if (!string.IsNullOrEmpty(khmshDon) && (khmshDon.Length != 1 || !"123456".Contains(khmshDon)))
                {
                    result.AddError($"[LỖI CẤU TRÚC] Ký hiệu mẫu số '{khmshDon}' sai định dạng. Bắt buộc phải có chiều dài chính xác là 1 ký tự và phải thuộc tập hợp [1, 2, 3, 4, 5, 6].");
                    isDataValid = false;
                }

                isDataValid &= CheckMandatory(khhDon, "Ký hiệu hóa đơn (KHHDon)", result);
                if (!string.IsNullOrEmpty(khhDon) && khhDon.Length != 6)
                {
                    result.AddError($"[LỖI CẤU TRÚC] Ký hiệu hóa đơn '{khhDon}' sai định dạng. Bắt buộc phải có chiều dài chính xác là 6 ký tự.");
                    isDataValid = false;
                }

                isDataValid &= CheckMandatory(shDon, "Số hóa đơn (SHDon)", result);
                if (!string.IsNullOrEmpty(shDon) && shDon.Length > 8)
                {
                    result.AddError($"[LỖI CẤU TRÚC] Số hóa đơn '{shDon}' sai định dạng. Bắt buộc có chiều dài tối đa là 8 ký tự.");
                    isDataValid = false;
                }

                // C. Tiền tệ (DVTTe) và Tỷ giá (TGia)
                isDataValid &= CheckMandatory(dvtTe, "Đơn vị tiền tệ (DVTTe)", result);
                if (!string.IsNullOrEmpty(dvtTe) && dvtTe.ToUpper() != "VND")
                {
                    if (string.IsNullOrEmpty(tGia))
                    {
                        result.AddError($"[LỖI CẤU TRÚC] Tiền tệ là '{dvtTe}', bắt buộc phải có hệ số Tỷ giá (TGia).");
                        isDataValid = false;
                    }
                    else if (!decimal.TryParse(tGia, out _))
                    {
                        result.AddError($"[LỖI DỮ LIỆU] Tỷ giá '{tGia}' phải là kiểu số thập phân hợp lệ.");
                        isDataValid = false;
                    }
                }

                // D. Tính chất hóa đơn (TCHDon) & Thông tin liên quan
                if (!string.IsNullOrEmpty(tchDon) && (tchDon == "1" || tchDon == "2"))
                {
                    XmlNodeList tthdLQuanNodes = xmlDoc.SelectNodes("//*[local-name()='TTHDLQuan']");
                    if (tthdLQuanNodes == null || tthdLQuanNodes.Count == 0)
                    {
                        string tchDonName = tchDon == "1" ? "Thay thế" : "Điều chỉnh";
                        result.AddError($"[LỖI NGHIỆP VỤ] Hóa đơn mang tính chất '{tchDonName}' (TCHDon = {tchDon}), bắt buộc phải có nhánh Thông tin hóa đơn liên quan (TTHDLQuan).");
                        isDataValid = false;
                    }
                }

                if (!string.IsNullOrEmpty(nLap) && !string.IsNullOrEmpty(ngayKy))
                {
                    if (DateTime.TryParse(nLap, out DateTime dtLap) && DateTime.TryParse(ngayKy, out DateTime dtKy))
                    {
                        if (Math.Abs((dtKy - dtLap).TotalDays) > 1)
                        {
                            result.AddWarning($"[RỦI RO THỜI GIAN] Ngày lập ({dtLap:dd/MM/yyyy}) và Ngày ký ({dtKy:dd/MM/yyyy}) chênh lệch quá 1 ngày.");
                        }
                    }
                }

                bool isVatInvoice = true;
                bool isCashRegister = false;

                if (!string.IsNullOrEmpty(khhDon) && khhDon.Length >= 4 && char.ToUpper(khhDon[3]) == 'M')
                {
                    isCashRegister = true;
                    result.AddWarning("Thông tin: Hóa đơn MÁY TÍNH TIỀN (Ký hiệu 'M'). Hệ thống tự động áp dụng luật kiểm tra của Máy tính tiền (Có kiểm tra Mã CQT hợp lệ).");
                }

                if (khmshDon?.StartsWith("2") == true || mauSo == "2")
                {
                    isVatInvoice = false;
                    result.AddWarning("PHÁT HIỆN: Hóa đơn Bán hàng (02GTTT). Chế độ kiểm tra: KHÔNG THUẾ.");
                }

                XmlNode nBan = xmlDoc.SelectSingleNode("//*[local-name()='NBan']");
                string sellerName = nBan?.SelectSingleNode(".//*[local-name()='Ten']")?.InnerText;
                string sellerTax = nBan?.SelectSingleNode(".//*[local-name()='MST']")?.InnerText;
                string sellerAddr = nBan?.SelectSingleNode(".//*[local-name()='DChi']")?.InnerText;

                string totalAmountStr = GetVal("TgTTTBSo");
                string totalAmountWords = GetVal("TgTTTBChu");

                // bool isDataValid = true;

                if (isCashRegister)
                {
                    if (string.IsNullOrEmpty(mccqt))
                    {
                        result.AddError("[LỖI PHÁP LÝ] Hóa đơn Máy tính tiền bắt buộc phải có Mã của Cơ quan thuế (MCCQT)!");
                        isDataValid = false;
                    }
                }

                isDataValid &= CheckMandatory(nLap, "Ngày lập (NLap)", result);

                isDataValid &= CheckMandatory(sellerTax, "MST Người Bán", result);
                isDataValid &= CheckMandatory(totalAmountStr, "Tổng tiền bằng số", result);

                string actualSignerSubject = GetSignerSubjectInternal(xmlDoc);

                if (!string.IsNullOrEmpty(actualSignerSubject))
                {
                    if (!string.IsNullOrEmpty(sellerTax) && !actualSignerSubject.Contains(sellerTax))
                    {
                        result.AddError($"[CẢNH BÁO AN NINH - SPOOFING DETECTED] MST trên hóa đơn: {sellerTax}, Người ký thực sự: {actualSignerSubject}. Hóa đơn bị ký bởi đơn vị KHÁC.");
                        return result;
                    }
                }

                if (!isDataValid) return result;

                // CHECK D & E: Duplicate & Blacklist
                if (!string.IsNullOrEmpty(sellerTax) && !string.IsNullOrEmpty(khhDon) && !string.IsNullOrEmpty(shDon))
                {
                    bool isDuplicate = await _unitOfWork.Invoices.ExistsByDetailsAsync(sellerTax, khhDon, shDon);

                    if (isDuplicate)
                    {
                        result.AddError($"[RỦI RO TRÙNG LẶP] Hóa đơn số {shDon}, ký hiệu {khhDon} của MST {sellerTax} đã tồn tại trong hệ thống.");
                    }
                }

                if (!string.IsNullOrEmpty(sellerTax))
                {
                    var blacklisted = await _unitOfWork.LocalBlacklists.GetByTaxCodeAsync(sellerTax);
                    if (blacklisted != null)
                    {
                        result.AddError($"[RỦI RO DANH SÁCH ĐEN] Mã số thuế người bán '{sellerTax}' thuộc danh sách đen nội bộ! Lý do: {blacklisted.Reason}");
                    }
                }

                decimal totalAmount = 0;
                CheckDecimal(totalAmountStr, "TgTTTBSo", out totalAmount, result);

                XmlNodeList items = xmlDoc.SelectNodes("//*[local-name()='HHDVu']");
                decimal totalLineItems = 0;
                bool hasRisk = false;

                if (items.Count == 0)
                {
                    result.AddError("[LỖI CẤU TRÚC] Không có dòng hàng hóa (HHDVu) nào!");
                }

                foreach (XmlNode item in items)
                {
                    string name = item.SelectSingleNode(".//*[local-name()='Ten']")?.InnerText ?? "Hàng hóa";
                    string sQty = item.SelectSingleNode(".//*[local-name()='SLuong']")?.InnerText ?? "0";
                    string sPrice = item.SelectSingleNode(".//*[local-name()='DGia']")?.InnerText ?? "0";
                    string sTotal = item.SelectSingleNode(".//*[local-name()='ThTien']")?.InnerText ?? "0";
                    string tChat = item.SelectSingleNode(".//*[local-name()='TChat']")?.InnerText;
                    string tSuat = item.SelectSingleNode(".//*[local-name()='TSuat']")?.InnerText;

                    if (!string.IsNullOrEmpty(tSuat))
                    {
                        var validRates = new[] { "0%", "5%", "8%", "10%", "KCT", "KKKNT" };
                        if (!validRates.Contains(tSuat))
                        {
                            result.AddError($"[RỦI RO THUẾ SUẤT] Thuế suất '{tSuat}' không hợp lệ hoặc bất thường ở hàng hóa: {name}");
                            hasRisk = true;
                        }
                    }

                    if (string.IsNullOrEmpty(tChat))
                    {
                        result.AddError($"[LỖI CẤU TRÚC] Thiếu trường 'Tính chất' (TChat) cho hàng hóa: {name}");
                        hasRisk = true;
                    }

                    decimal qty = 0, price = 0, totalClaimed = 0;
                    CheckDecimal(sQty, "SLuong", out qty, result);
                    CheckDecimal(sPrice, "DGia", out price, result);
                    CheckDecimal(sTotal, "ThTien", out totalClaimed, result);

                    totalLineItems += totalClaimed;

                    if (tChat == "1" && Math.Abs((qty * price) - totalClaimed) > 10m)
                    {
                        result.AddWarning($"[CẢNH BÁO RỦI RO] Sai lệch tính toán: {name}");
                    }
                }

                string sTotalPreTax = GetVal("TgTCThue") ?? "0";
                string sTotalTax = GetVal("TgTThue") ?? "0";

                decimal totalPreTax = 0, totalTax = 0;
                CheckDecimal(sTotalPreTax, "TgTCThue", out totalPreTax, result);
                CheckDecimal(sTotalTax, "TgTThue", out totalTax, result);

                if (isVatInvoice)
                {
                    if (Math.Abs((totalPreTax + totalTax) - totalAmount) > 10m)
                    {
                        result.AddError($"[LỖI LOGIC] Tổng thanh toán KHÔNG KHỚP (Tiền hàng + Thuế)");
                        hasRisk = true;
                    }
                }
                else
                {
                    decimal comparisonBase = (totalPreTax == 0 && Math.Abs(totalPreTax - totalAmount) > 10m) ? totalLineItems : totalPreTax;
                    if (Math.Abs(comparisonBase - totalAmount) > 10m)
                    {
                        result.AddError($"[LỖI LOGIC] Hóa đơn bán hàng: Tổng tiền không khớp.");
                        hasRisk = true;
                    }
                }

                if (!string.IsNullOrEmpty(sellerTax))
                {
                    if (!IsValidTaxCode(sellerTax))
                    {
                        result.AddError($"[LỖI MST] Mã số thuế '{sellerTax}' không đúng định dạng!");
                    }
                    else
                    {
                        await CheckTaxCodeExistAsync(sellerTax, result);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trong quá trình ValidateBusinessLogicAsync");
                result.AddError($"Lỗi hệ thống: {ex.Message}");
            }

            return result;
        }

        // ================= HELPER FUNCTIONS =================

        private string GetNodeValue(XmlDocument doc, string localName)
        {
            XmlNode node = doc.SelectSingleNode($"//*[local-name()='{localName}']");
            return node?.InnerText;
        }

        private bool CheckMandatory(string value, string fieldName, ValidationResultDto result)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                result.AddError($"[LỖI CẤU TRÚC] Thiếu trường bắt buộc: {fieldName}");
                return false;
            }
            return true;
        }

        private bool CheckDecimal(string value, string fieldName, out decimal decimalResult, ValidationResultDto result)
        {
            if (!decimal.TryParse(value, out decimalResult))
            {
                result.AddError($"[LỖI DỮ LIỆU] Trường '{fieldName}' phải là số! Giá trị hiện tại: '{value}'");
                return false;
            }
            return true;
        }

        private bool IsValidTaxCode(string taxCode)
        {
            if (string.IsNullOrEmpty(taxCode)) return false;

            Match match10or13 = Regex.Match(taxCode, @"^(\d{10})(-\d{3})?$");
            if (match10or13.Success)
            {
                string mst10 = match10or13.Groups[1].Value;
                return ValidateMstChecksum(mst10);
            }

            if (Regex.IsMatch(taxCode, @"^\d{12}$"))
            {
                return true;
            }

            return false;
        }

        private bool ValidateMstChecksum(string mst10)
        {
            if (mst10.Length != 10) return false;

            int[] weights = { 31, 29, 23, 19, 17, 13, 7, 5, 3 };
            long sum = 0;

            for (int i = 0; i < 9; i++)
            {
                if (!char.IsDigit(mst10[i])) return false;
                sum += (mst10[i] - '0') * weights[i];
            }

            long remainder = sum % 11;
            long checkDigit = 10 - remainder;

            if (checkDigit == 10) return false;

            int actualDigit = mst10[9] - '0';
            return checkDigit == actualDigit;
        }

        private async Task CheckTaxCodeExistAsync(string taxCode, ValidationResultDto validationResult)
        {
            if (!IsValidTaxCode(taxCode)) return;

            try
            {
                string url = $"https://api.vietqr.io/v2/business/{taxCode}";
                var client = _httpClientFactory.CreateClient();
                HttpResponseMessage response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        JsonElement root = doc.RootElement;
                        string code = root.GetProperty("code").GetString();

                        if (code == "00")
                        {
                            // Doanh nghiệp tồn tại
                        }
                        else
                        {
                            validationResult.AddWarning($"[API WARNING] Không tìm thấy thông tin doanh nghiệp trên VietQR! (Code: {code})");
                        }
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    validationResult.AddWarning($"[Hệ thống] Máy chủ tra cứu MST (VietQR) đang quá tải, tạm thời bỏ qua bước xác thực chéo doanh nghiệp.");
                }
                else
                {
                    validationResult.AddWarning($"[API ERROR] Lỗi kết nối VietQR API: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gọi API VietQR");
                validationResult.AddWarning($"[API ERROR] Exception kết nối VietQR: {ex.Message}");
            }
        }

        private string GetSignerSubjectInternal(XmlDocument xmlDoc)
        {
            try
            {
                XmlNodeList nodeList = xmlDoc.GetElementsByTagName("Signature");
                if (nodeList.Count > 0)
                {
                    SignedXml signedXml = new SignedXml(xmlDoc);
                    signedXml.LoadXml((XmlElement)nodeList[0]);

                    if (signedXml.KeyInfo != null)
                    {
                        foreach (KeyInfoClause clause in signedXml.KeyInfo)
                        {
                            if (clause is KeyInfoX509Data x509Data)
                            {
                                if (x509Data.Certificates.Count > 0)
                                {
                                    X509Certificate2 cert = (X509Certificate2)x509Data.Certificates[0];
                                    return cert.Subject;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
