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
using Microsoft.Extensions.Configuration;
using SmartInvoice.API.Entities.JsonModels;
using SmartInvoice.API.DTOs.Invoice;
using SmartInvoice.API.Services.Interfaces;
using SmartInvoice.API.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace SmartInvoice.API.Services.Implementations
{
    public class InvoiceProcessorService : IInvoiceProcessorService
    {
        private readonly ILogger<InvoiceProcessorService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _xsdPath;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;

        public InvoiceProcessorService(
            ILogger<InvoiceProcessorService> logger,
            IHttpClientFactory httpClientFactory,
            IHostEnvironment env,
            IUnitOfWork unitOfWork,
            IConfiguration configuration,
            IMemoryCache cache)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _xsdPath = Path.Combine(env.ContentRootPath, "Resources", "InvoiceSchema.xsd");
            _unitOfWork = unitOfWork;
            _configuration = configuration;
            _cache = cache;
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
                    result.AddError("ERR_XML_STRUCT", $"Lỗi cấu trúc XML (Dòng {args.Exception.LineNumber}: {args.Message})", "Vui lòng xem lại hóa đơn có đúng cấu trúc dữ liệu XML quy định của TCT không.");
                };

                using (var reader = XmlReader.Create(xmlPath, settings))
                {
                    while (reader.Read()) { }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi đọc file XML tại ValidateStructure");
                result.AddError("ERR_XML_SYS", $"Lỗi hệ thống khi đọc file XML: {ex.Message}", "Kiểm tra định dạng file tải lên có đúng chuẩn bảng mã XML.");
            }

            return result;
        }

        public ValidationResultDto VerifyDigitalSignature(XmlDocument xmlDoc)
        {
            var result = new ValidationResultDto();

            try
            {

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
                        result.AddWarning("WARN_SIG_CASH_REG", "Hóa đơn khởi tạo từ máy tính tiền không có chữ ký số (Hợp lệ theo quy định).", "Đối với hóa đơn máy tính tiền, không cần chữ ký số.");
                        return result;
                    }

                    result.AddError("ERR_SIG_MISSING", "Không tìm thấy Chữ ký số trong file.", "Hóa đơn điện tử thông thường bắt buộc phải có chữ ký số. Yêu cầu bên bán cung cấp tệp có chữ ký.");
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
                                            result.AddError("ERR_SIG_EXPIRED", $"Chữ ký số chưa có hiệu lực hoặc đã hết hạn tại thời điểm lập hóa đơn ({invoiceDate:dd/MM/yyyy}). Thời hạn chứng thư thực tế: {cert.NotBefore:dd/MM/yyyy} đến {cert.NotAfter:dd/MM/yyyy}.", "Yêu cầu bên bán xuất lại hóa đơn với chữ ký số còn hạn.");
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
                    result.AddError("ERR_SIG_INVALID", "Chữ ký số không hợp lệ hoặc dữ liệu hóa đơn đã bị thay đổi sau khi ký.", "Hóa đơn đã bị chỉnh sửa trái phép hoặc chữ ký không đáng tin cậy. Vui lòng kiểm tra lại nguyên bản của file.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kiểm tra chữ ký");
                result.AddError("ERR_SIG_SYS", $"Lỗi hệ thống khi kiểm tra chữ ký: {ex.Message}", "Lỗi hệ thống trong quá trình giải mã chữ ký điện tử.");
            }

            return result;
        }

        public InvoiceExtractedData ExtractData(XmlDocument xmlDoc)
        {
            var extractedData = new InvoiceExtractedData
            {
                LineItems = new System.Collections.Generic.List<SmartInvoice.API.Entities.JsonModels.InvoiceLineItem>()
            };

            try
            {

                // Try capturing additional general invoice details
                extractedData.PaymentTerms = GetNodeValue(xmlDoc, "HTTToan") ?? GetNodeValue(xmlDoc, "HTTT");

                extractedData.InvoiceTemplateCode = GetNodeValue(xmlDoc, "KHMSHDon");
                extractedData.InvoiceSymbol = GetNodeValue(xmlDoc, "KHHDon");
                extractedData.InvoiceNumber = GetNodeValue(xmlDoc, "SHDon");

                extractedData.InvoiceCurrency = GetNodeValue(xmlDoc, "DVTTe");
                string sExchangeRate = GetNodeValue(xmlDoc, "TGia");
                if (decimal.TryParse(sExchangeRate, out decimal exchangeRate))
                {
                    extractedData.ExchangeRate = exchangeRate;
                }

                XmlNode nBan = xmlDoc.SelectSingleNode("//*[local-name()='NBan']");
                extractedData.SellerName = nBan?.SelectSingleNode(".//*[local-name()='Ten']")?.InnerText;
                extractedData.SellerTaxCode = nBan?.SelectSingleNode(".//*[local-name()='MST']")?.InnerText;
                extractedData.SellerAddress = nBan?.SelectSingleNode(".//*[local-name()='DChi']")?.InnerText;
                extractedData.SellerPhone = nBan?.SelectSingleNode(".//*[local-name()='SDThoai']")?.InnerText;
                extractedData.SellerEmail = nBan?.SelectSingleNode(".//*[local-name()='DCTDTu']")?.InnerText;
                extractedData.SellerBankAccount = nBan?.SelectSingleNode(".//*[local-name()='STKhoan']")?.InnerText;
                extractedData.SellerBankName = nBan?.SelectSingleNode(".//*[local-name()='TNHang']")?.InnerText;

                XmlNode nMua = xmlDoc.SelectSingleNode("//*[local-name()='NMua']");
                extractedData.BuyerName = nMua?.SelectSingleNode(".//*[local-name()='Ten']")?.InnerText;
                extractedData.BuyerTaxCode = nMua?.SelectSingleNode(".//*[local-name()='MST']")?.InnerText;
                extractedData.BuyerAddress = nMua?.SelectSingleNode(".//*[local-name()='DChi']")?.InnerText;
                extractedData.BuyerPhone = nMua?.SelectSingleNode(".//*[local-name()='SDThoai']")?.InnerText;
                extractedData.BuyerEmail = nMua?.SelectSingleNode(".//*[local-name()='DCTDTu']")?.InnerText;
                extractedData.BuyerContactPerson = nMua?.SelectSingleNode(".//*[local-name()='HVTNMHang']")?.InnerText;

                // Fallback: Nếu không có thẻ Ten (Tên đơn vị/Người mua), lấy HVTNMHang (Họ tên người mua hàng) làm BuyerName
                if (string.IsNullOrWhiteSpace(extractedData.BuyerName) && !string.IsNullOrWhiteSpace(extractedData.BuyerContactPerson))
                {
                    extractedData.BuyerName = extractedData.BuyerContactPerson;
                }

                extractedData.TotalAmountInWords = GetNodeValue(xmlDoc, "TgTTTBChu");

                // Mã cơ quan thuế cấp
                extractedData.MCCQT = GetNodeValue(xmlDoc, "MCCQT");

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
                    string name = item.SelectSingleNode(".//*[local-name()='THHDVu']")?.InnerText;
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

        public async Task<ValidationResultDto> ValidateBusinessLogicAsync(XmlDocument xmlDoc, Guid? companyId = null)
        {
            var result = new ValidationResultDto();

            try
            {

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
                    result.AddError("ERR_LOGIC_VERSION", $"Phiên bản XML của hóa đơn ({pBan}) chưa được hỗ trợ. Hệ thống hiện chỉ nhận định dạng 2.0.0, 2.0.1 hoặc 2.1.0.", "Kiểm tra phiên bản XML truyền vào.");
                    isDataValid = false;
                }

                // B. Cấu trúc chuỗi định danh (KHMSHDon, KHHDon, SHDon)
                isDataValid &= CheckMandatory(khmshDon, "Ký hiệu mẫu số (KHMSHDon)", result);
                if (!string.IsNullOrEmpty(khmshDon) && (khmshDon.Length != 1 || !"123456".Contains(khmshDon)))
                {
                    result.AddError("ERR_LOGIC_INV_TYPE", $"Ký hiệu mẫu số '{khmshDon}' chưa chính xác (cần 1 ký tự từ 1 đến 6).", "Kiểm tra lại ký hiệu mẫu số hóa đơn.");
                    isDataValid = false;
                }

                isDataValid &= CheckMandatory(khhDon, "Ký hiệu hóa đơn (KHHDon)", result);
                if (!string.IsNullOrEmpty(khhDon) && khhDon.Length != 6)
                {
                    result.AddError("ERR_LOGIC_INV_SYMBOL", $"[LỖI CẤU TRÚC] Ký hiệu hóa đơn '{khhDon}' sai định dạng. Bắt buộc phải có chiều dài chính xác là 6 ký tự.", "Kiểm tra lại ký hiệu hóa đơn.");
                    isDataValid = false;
                }

                isDataValid &= CheckMandatory(shDon, "Số hóa đơn (SHDon)", result);
                if (!string.IsNullOrEmpty(shDon) && shDon.Length > 8)
                {
                    result.AddError("ERR_LOGIC_INV_NUM", $"[LỖI CẤU TRÚC] Số hóa đơn '{shDon}' sai định dạng. Bắt buộc có chiều dài tối đa là 8 ký tự.", "Kiểm tra lại số hóa đơn.");
                    isDataValid = false;
                }

                // C. Tiền tệ (DVTTe) và Tỷ giá (TGia)
                // Theo QĐ 1450/QĐ-TCT, nhiều nhà cung cấp MTT không gửi DVTTe, ngầm định là VND.
                string currencyUnit = string.IsNullOrEmpty(dvtTe) ? "VND" : dvtTe.ToUpper();
                if (currencyUnit != "VND")
                {
                    if (string.IsNullOrEmpty(tGia))
                    {
                        result.AddError("ERR_LOGIC_CURRENCY", $"[LỖI CẤU TRÚC] Tiền tệ là '{currencyUnit}', bắt buộc phải có hệ số Tỷ giá (TGia).", "Bổ sung tỷ giá ngoại tệ.");
                        isDataValid = false;
                    }
                    else if (!decimal.TryParse(tGia, out _))
                    {
                        result.AddError("ERR_LOGIC_EX_RATE", $"[LỖI DỮ LIỆU] Tỷ giá '{tGia}' phải là kiểu số thập phân hợp lệ.", "Kiểm tra lại tỷ giá.");
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
                        result.AddError("ERR_LOGIC_REL", $"[LỖI NGHIỆP VỤ] Hóa đơn mang tính chất '{tchDonName}' (TCHDon = {tchDon}), bắt buộc phải có nhánh Thông tin hóa đơn liên quan (TTHDLQuan).", "Bổ sung thông tin hóa đơn bản gốc.");
                        isDataValid = false;
                    }
                }

                if (!string.IsNullOrEmpty(nLap))
                {
                    if (DateTime.TryParse(nLap, out DateTime dtLap))
                    {
                        if (dtLap > DateTime.Now)
                        {
                            result.AddWarning("WARN_LOGIC_DATE_FUTURE", $"[RỦI RO THỜI GIAN] Ngày lập ({dtLap:dd/MM/yyyy}) là ngày ở tương lai.", "Kiểm tra lại hệ thống ngày giờ.");
                        }

                        if (!string.IsNullOrEmpty(ngayKy) && DateTime.TryParse(ngayKy, out DateTime dtKy))
                        {
                            if (Math.Abs((dtKy - dtLap).TotalDays) > 1)
                            {
                                result.AddWarning("WARN_LOGIC_DATE_DISC", $"[RỦI RO THỜI GIAN] Ngày lập ({dtLap:dd/MM/yyyy}) và Ngày ký ({dtKy:dd/MM/yyyy}) chênh lệch quá 1 ngày.", "Xác minh lại thời gian lập/ký.");
                            }
                        }
                    }
                }

                bool isVatInvoice = true;
                bool isCashRegister = false;

                if (!string.IsNullOrEmpty(khhDon) && khhDon.Length >= 4 && char.ToUpper(khhDon[3]) == 'M')
                {
                    isCashRegister = true;
                }

                if (khmshDon?.StartsWith("2") == true || mauSo == "2")
                {
                    isVatInvoice = false;
                }

                XmlNode nBan = xmlDoc.SelectSingleNode("//*[local-name()='NBan']");
                string sellerName = nBan?.SelectSingleNode(".//*[local-name()='Ten']")?.InnerText;
                string sellerTax = nBan?.SelectSingleNode(".//*[local-name()='MST']")?.InnerText;
                string sellerAddr = nBan?.SelectSingleNode(".//*[local-name()='DChi']")?.InnerText;

                // Trích xuất MST Người Mua từ XML
                XmlNode nMua = xmlDoc.SelectSingleNode("//*[local-name()='NMua']");
                string buyerTax = nMua?.SelectSingleNode(".//*[local-name()='MST']")?.InnerText;

                string totalAmountStr = GetVal("TgTTTBSo");
                string totalAmountWords = GetVal("TgTTTBChu");

                // bool isDataValid = true;

                if (isCashRegister)
                {
                    if (string.IsNullOrEmpty(mccqt))
                    {
                        result.AddError("ERR_LOGIC_MCCQT", "Hóa đơn khởi tạo từ máy tính tiền bắt buộc phải có Mã của Cơ quan thuế (MCCQT).", "Vui lòng bổ sung MCCQT từ Cơ quan Thuế.");
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
                        result.AddError("ERR_LOGIC_SIGNER_MISMATCH", $"Chữ ký số không hợp lệ: MST người bán trên hóa đơn ({sellerTax}) không khớp với người ký thực tế ({actualSignerSubject}).", "Kiểm tra lại chữ ký số có thuộc về đúng MST người bán hay không.");
                        return result;
                    }
                }

                if (!isDataValid) return result;

                // CHECK C: KIỂM TRA QUYỀN SỞ HỮU HÓA ĐƠN ĐẦU VÀO
                // TH1: CÓ MST Người mua → Phải khớp với MST công ty → Nếu sai → Block
                // TH2: KHÔNG CÓ MST Người mua (BigC, xăng dầu...) → Cho qua, nhưng gắn cảnh báo để Admin duyệt
                if (companyId.HasValue)
                {
                    var company = await _unitOfWork.Companies.GetByIdAsync(companyId.Value);
                    if (company != null)
                    {
                        if (!string.IsNullOrEmpty(buyerTax))
                        {
                            // TH1: Có MST Người Mua → bắt buộc phải khớp
                            if (!string.Equals(buyerTax.Trim(), company.TaxCode?.Trim(), StringComparison.OrdinalIgnoreCase))
                            {
                                result.AddError("ERR_LOGIC_OWNER", $"Mã số thuế người mua trên hóa đơn ({buyerTax}) không khớp với công ty hiện tại.", "Vui lòng chỉ tải lên hóa đơn thuộc quyền sở hữu của công ty bạn.");
                                return result;
                            }
                        }
                        else
                        {
                            // TH2: Không có MST Người Mua → cảnh báo, cần Admin duyệt kỹ
                            result.AddWarning("WARN_LOGIC_NO_BUYER_TAX", "Hóa đơn không có thông tin Mã số thuế người mua.", "Cần trình bày thêm lý do giải trình khi gửi duyệt nội bộ.");
                        }
                    }
                }

                // CHECK D & E: Duplicate & Blacklist
                if (!string.IsNullOrEmpty(sellerTax) && !string.IsNullOrEmpty(khhDon) && !string.IsNullOrEmpty(shDon))
                {
                    if (companyId.HasValue)
                    {
                        var existingInvoice = await _unitOfWork.Invoices.GetExistingInvoiceAsync(sellerTax, khhDon, shDon, companyId.Value);

                        if (existingInvoice != null)
                        {
                            if (existingInvoice.Status == "Rejected")
                            {
                                // Flag as replacement, no error
                                result.IsReplacement = true;
                                result.ReplacedInvoiceId = existingInvoice.InvoiceId;
                                result.NewVersion = existingInvoice.Version + 1;
                            }
                            else
                            {
                                result.AddError("ERR_LOGIC_DUP", $"Hóa đơn số {shDon} (Ký hiệu: {khhDon}) của MST {sellerTax} đã tồn tại trong hệ thống.", "Vui lòng kiểm tra lại, hóa đơn này đã được tải lên trước đó.");
                                return result;
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(sellerTax))
                {
                    var blacklisted = await _unitOfWork.LocalBlacklists.GetByTaxCodeAsync(sellerTax);
                    if (blacklisted != null)
                    {
                        result.AddError("ERR_LOGIC_BLACKLIST", $"[RỦI RO DANH SÁCH ĐEN] Mã số thuế người bán '{sellerTax}' thuộc danh sách đen nội bộ! Lý do: {blacklisted.Reason}", "Cảnh báo giao dịch với công ty này theo quy định nội bộ.");
                    }
                }

                decimal totalAmount = 0;
                CheckDecimal(totalAmountStr, "TgTTTBSo", out totalAmount, result);

                XmlNodeList items = xmlDoc.SelectNodes("//*[local-name()='HHDVu']");
                decimal totalLineItems = 0;
                bool hasRisk = false;

                if (items.Count == 0)
                {
                    result.AddError("ERR_LOGIC_NO_ITEMS", "[LỖI CẤU TRÚC] Không có dòng hàng hóa (HHDVu) nào!", "Kiểm tra lại nội dung hóa đơn, không tìm thấy hàng hóa dịch vụ.");
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
                            result.AddError("ERR_LOGIC_TAX_RATE", $"[RỦI RO THUẾ SUẤT] Thuế suất '{tSuat}' không hợp lệ hoặc bất thường ở hàng hóa: {name}", "Xác minh lại mức thuế suất hiện hành.");
                            hasRisk = true;
                        }
                    }

                    if (string.IsNullOrEmpty(tChat))
                    {
                        result.AddError("ERR_LOGIC_NO_PROPERTY", $"[LỖI CẤU TRÚC] Thiếu trường 'Tính chất' (TChat) cho hàng hóa: {name}", "Bổ sung tính chất cho hàng hóa (Hàng hóa, khuyến mại, chiết khấu...).");
                        hasRisk = true;
                    }

                    decimal qty = 0, price = 0, totalClaimed = 0;
                    CheckDecimal(sQty, "SLuong", out qty, result);
                    CheckDecimal(sPrice, "DGia", out price, result);
                    CheckDecimal(sTotal, "ThTien", out totalClaimed, result);

                    totalLineItems += totalClaimed;

                    if (tChat == "1" && Math.Abs((qty * price) - totalClaimed) > 10m)
                    {
                        result.AddWarning("WARN_LOGIC_CALC_DEV", $"[CẢNH BÁO RỦI RO] Sai lệch tính toán: {name} (Lệch Thành tiền > 10đ)", "Kiểm tra lại đơn giá * số lượng có khớp với thành tiền không.");
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
                        result.AddError("ERR_LOGIC_TOTAL_MISMATCH", $"[LỖI LOGIC] Tổng thanh toán KHÔNG KHỚP (Tiền hàng + Thuế)", "Cộng dồn tiền hàng và thuế không khớp tổng thanh toán, kiểm tra lại hóa đơn.");
                        hasRisk = true;
                    }
                }
                else
                {
                    decimal comparisonBase = (totalPreTax == 0 && Math.Abs(totalPreTax - totalAmount) > 10m) ? totalLineItems : totalPreTax;
                    if (Math.Abs(comparisonBase - totalAmount) > 10m)
                    {
                        result.AddError("ERR_LOGIC_SALES_TOTAL_MISMATCH", $"[LỖI LOGIC] Hóa đơn bán hàng: Tổng tiền không khớp.", "Cộng dồn chi tiết dòng hàng hóa không khớp với tổng tiền hóa đơn.");
                        hasRisk = true;
                    }
                }

                if (!string.IsNullOrEmpty(sellerTax))
                {
                    if (!IsValidTaxCode(sellerTax))
                    {
                        result.AddError("ERR_LOGIC_TAX_FORMAT", $"[LỖI MST] Mã số thuế '{sellerTax}' không đúng định dạng!", "Vui lòng kiểm tra lại MST người bán.");
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
                result.AddError("ERR_LOGIC_SYS", $"Lỗi hệ thống: {ex.Message}", "Lỗi không xác định khi kiểm tra logic kế toán.");
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
                result.AddError("ERR_XML_MISSING_FIELD", $"[LỖI CẤU TRÚC] Thiếu trường bắt buộc: {fieldName}", $"Vui lòng bổ sung: {fieldName}");
                return false;
            }
            return true;
        }

        private bool CheckDecimal(string value, string fieldName, out decimal decimalResult, ValidationResultDto result)
        {
            if (!decimal.TryParse(value, out decimalResult))
            {
                result.AddError("ERR_DATA_NOT_NUMBER", $"[LỖI DỮ LIỆU] Trường '{fieldName}' phải là số! Giá trị hiện tại: '{value}'", "Xem lại định dạng dữ liệu (bắt buộc dạng số thập phân).");
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

            string cacheKey = $"VietQR_TaxCode_{taxCode}";
            if (_cache.TryGetValue(cacheKey, out bool isValidated) && isValidated)
            {
                // Already validated and cached successfully
                return;
            }

            try
            {
                var vietQrBaseUrl = Environment.GetEnvironmentVariable("VIETQR_API_URL")
                                    ?? _configuration["ExternalApis:VietQR"]
                                    ?? "https://api.vietqr.io/v2/business";

                string url = $"{vietQrBaseUrl.TrimEnd('/')}/{taxCode}";
                var client = _httpClientFactory.CreateClient();
                HttpResponseMessage response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        JsonElement root = doc.RootElement;
                        var code = root.TryGetProperty("code", out var codeElement) ? codeElement.GetString() : null;

                        if (code == "00")
                        {
                            // Doanh nghiệp tồn tại -> Lưu cache 7 ngày
                            var cacheEntryOptions = new MemoryCacheEntryOptions()
                                .SetAbsoluteExpiration(TimeSpan.FromDays(7));
                            _cache.Set(cacheKey, true, cacheEntryOptions);
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

        private string? GetSignerSubjectInternal(XmlDocument xmlDoc)
        {
            try
            {
                XmlNodeList? nodeList = xmlDoc.GetElementsByTagName("Signature");
                if (nodeList != null && nodeList.Count > 0)
                {
                    SignedXml signedXml = new SignedXml(xmlDoc);
                    signedXml.LoadXml((XmlElement)nodeList[0]!);

                    if (signedXml.KeyInfo != null)
                    {
                        foreach (KeyInfoClause clause in signedXml.KeyInfo)
                        {
                            if (clause is KeyInfoX509Data x509Data)
                            {
                                if (x509Data.Certificates.Count > 0)
                                {
                                    X509Certificate2? cert = (X509Certificate2?)x509Data.Certificates[0];
                                    return cert?.Subject;
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
