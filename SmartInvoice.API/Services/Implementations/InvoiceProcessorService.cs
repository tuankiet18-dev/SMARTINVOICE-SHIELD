using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Linq;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using SmartInvoice.API.Entities.JsonModels;
using SmartInvoice.API.Constants;
using SmartInvoice.API.Enums;
using SmartInvoice.API.DTOs.Invoice;
using SmartInvoice.API.Services.Interfaces;
using SmartInvoice.API.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace SmartInvoice.API.Services.Implementations
{
    public class InvoiceProcessorService : IInvoiceProcessorService
    {
        private static readonly string[] SupportedXmlVersions = { "2.0.0", "2.0.1", "2.1.0" };
        private static readonly string[] ValidTaxRates = { "0%", "5%", "8%", "10%", "KCT", "KKKNT" };
        private const decimal CurrencyTolerance = 10m;

        private readonly ILogger<InvoiceProcessorService> _logger;
        private readonly string _xsdPath;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;

        public InvoiceProcessorService(
            ILogger<InvoiceProcessorService> logger,
            IHostEnvironment env,
            IUnitOfWork unitOfWork,
            IConfiguration configuration)
        {
            _logger = logger;
            _xsdPath = Path.Combine(env.ContentRootPath, "Resources", "InvoiceSchema.xsd");
            _unitOfWork = unitOfWork;
            _configuration = configuration;
        }

        public ValidationResultDto ValidateStructure(string xmlPath)
        {
            var result = new ValidationResultDto();

            try
            {
                var settings = new XmlReaderSettings
                {
                    ValidationType = ValidationType.Schema,
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null
                };
                settings.Schemas.Add(null, _xsdPath);

                settings.ValidationEventHandler += (sender, args) =>
                {
                    result.AddError(ErrorCodes.XmlStruct, $"Lỗi cấu trúc XML (Dòng {args.Exception.LineNumber}: {args.Message})", "Vui lòng xem lại hóa đơn có đúng cấu trúc dữ liệu XML quy định của TCT không.");
                };

                using (var reader = XmlReader.Create(xmlPath, settings))
                {
                    while (reader.Read()) { }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi đọc file XML tại ValidateStructure");
                result.AddError(ErrorCodes.XmlSys, $"Lỗi hệ thống khi đọc file XML: {ex.Message}", "Kiểm tra định dạng file tải lên có đúng chuẩn bảng mã XML.");
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
                if (
                    khhDonNode != null
                    && !string.IsNullOrEmpty(khhDonNode.InnerText)
                    && khhDonNode.InnerText.Length >= 4
                    && char.ToUpper(khhDonNode.InnerText[3]) == 'M'
                )
                {
                    isCashRegister = true;
                }

                XmlNodeList nodeList = xmlDoc.GetElementsByTagName("Signature");
                if (nodeList.Count == 0)
                {
                    if (isCashRegister)
                    {
                        result.AddWarning(
                            "WARN_SIG_CASH_REG",
                            "Hóa đơn khởi tạo từ máy tính tiền không có chữ ký số (Hợp lệ theo quy định).",
                            "Đối với hóa đơn máy tính tiền, không cần chữ ký số."
                        );
                        return result;
                    }

                    result.AddError(ErrorCodes.SigMissing, "Không tìm thấy Chữ ký số trong file.", "Hóa đơn điện tử thông thường bắt buộc phải có chữ ký số. Yêu cầu bên bán cung cấp tệp có chữ ký.");
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
                                    X509Certificate2 cert = (X509Certificate2)
                                        x509Data.Certificates[0];
                                    result.SignerSubject = cert.Subject;

                                    XmlNode nLapNode = xmlDoc.SelectSingleNode(
                                        "//*[local-name()='NLap']"
                                    );
                                    if (
                                        nLapNode != null
                                        && DateTime.TryParse(
                                            nLapNode.InnerText,
                                            out DateTime invoiceDate
                                        )
                                    )
                                    {
                                        if (
                                            invoiceDate < cert.NotBefore
                                            || invoiceDate > cert.NotAfter
                                        )
                                        {
                                            result.AddError(ErrorCodes.SigExpired, $"Chữ ký số chưa có hiệu lực hoặc đã hết hạn tại thời điểm lập hóa đơn ({invoiceDate:dd/MM/yyyy}). Thời hạn chứng thư thực tế: {cert.NotBefore:dd/MM/yyyy} đến {cert.NotAfter:dd/MM/yyyy}.", "Yêu cầu bên bán xuất lại hóa đơn với chữ ký số còn hạn.");
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
                    result.AddError(ErrorCodes.SigInvalid, "Chữ ký số không hợp lệ hoặc dữ liệu hóa đơn đã bị thay đổi sau khi ký.", "Hóa đơn đã bị chỉnh sửa trái phép hoặc chữ ký không đáng tin cậy. Vui lòng kiểm tra lại nguyên bản của file.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kiểm tra chữ ký");
                result.AddError(ErrorCodes.SigSys, $"Lỗi hệ thống khi kiểm tra chữ ký: {ex.Message}", "Lỗi hệ thống trong quá trình giải mã chữ ký điện tử.");
            }

            return result;
        }

        public InvoiceExtractedData ExtractData(XmlDocument xmlDoc, ValidationResultDto validationResult)
        {
            var extractedData = new InvoiceExtractedData
            {
                LineItems =
                    new System.Collections.Generic.List<SmartInvoice.API.Entities.JsonModels.InvoiceLineItem>(),
            };

            try
            {
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
                extractedData.SellerName = nBan
                    ?.SelectSingleNode(".//*[local-name()='Ten']")
                    ?.InnerText;
                extractedData.SellerTaxCode = nBan
                    ?.SelectSingleNode(".//*[local-name()='MST']")
                    ?.InnerText;
                extractedData.SellerAddress = nBan
                    ?.SelectSingleNode(".//*[local-name()='DChi']")
                    ?.InnerText;
                extractedData.SellerPhone = nBan
                    ?.SelectSingleNode(".//*[local-name()='SDThoai']")
                    ?.InnerText;
                extractedData.SellerEmail = nBan
                    ?.SelectSingleNode(".//*[local-name()='DCTDTu']")
                    ?.InnerText;
                extractedData.SellerBankAccount = nBan
                    ?.SelectSingleNode(".//*[local-name()='STKhoan']")
                    ?.InnerText;
                extractedData.SellerBankName = nBan
                    ?.SelectSingleNode(".//*[local-name()='TNHang']")
                    ?.InnerText;

                XmlNode nMua = xmlDoc.SelectSingleNode("//*[local-name()='NMua']");
                extractedData.BuyerName = nMua
                    ?.SelectSingleNode(".//*[local-name()='Ten']")
                    ?.InnerText;
                extractedData.BuyerTaxCode = nMua
                    ?.SelectSingleNode(".//*[local-name()='MST']")
                    ?.InnerText;
                extractedData.BuyerAddress = nMua
                    ?.SelectSingleNode(".//*[local-name()='DChi']")
                    ?.InnerText;
                extractedData.BuyerPhone = nMua
                    ?.SelectSingleNode(".//*[local-name()='SDThoai']")
                    ?.InnerText;
                extractedData.BuyerEmail = nMua
                    ?.SelectSingleNode(".//*[local-name()='DCTDTu']")
                    ?.InnerText;
                extractedData.BuyerContactPerson = nMua
                    ?.SelectSingleNode(".//*[local-name()='HVTNMHang']")
                    ?.InnerText;

                if (string.IsNullOrWhiteSpace(extractedData.BuyerName) && !string.IsNullOrWhiteSpace(extractedData.BuyerContactPerson))
                {
                    extractedData.BuyerName = extractedData.BuyerContactPerson;
                }

                extractedData.TotalAmountInWords = GetNodeValue(xmlDoc, "TgTTTBChu");
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

                XmlNodeList items = xmlDoc.SelectNodes("//*[local-name()='HHDVu']");
                int stt = 1;
                foreach (XmlNode item in items)
                {
                    string name = item.SelectSingleNode(".//*[local-name()='THHDVu']")?.InnerText;
                    string unit = item.SelectSingleNode(".//*[local-name()='DVTinh']")?.InnerText;
                    string sQty = item.SelectSingleNode(".//*[local-name()='SLuong']")?.InnerText;
                    string sPrice = item.SelectSingleNode(".//*[local-name()='DGia']")?.InnerText;
                    string sTotal = item.SelectSingleNode(".//*[local-name()='ThTien']")?.InnerText;
                    string sTaxRate = item.SelectSingleNode(
                        ".//*[local-name()='TSuat']"
                    )?.InnerText;
                    string sTaxAmountStr = item.SelectSingleNode(
                        ".//*[local-name()='TTin'][*[local-name()='TTruong']='Tiền thuế']/*[local-name()='DLieu']"
                    )?.InnerText;

                    decimal taxAmount = 0;
                    if (!string.IsNullOrEmpty(sTaxAmountStr))
                    {
                        decimal.TryParse(sTaxAmountStr, out taxAmount);
                    }
                    decimal.TryParse(sQty, out decimal qty);
                    decimal.TryParse(sPrice, out decimal price);
                    decimal.TryParse(sTotal, out decimal total);

                    int vatRate = 0;
                    if (!string.IsNullOrEmpty(sTaxRate) && sTaxRate.Contains("%"))
                    {
                        int.TryParse(sTaxRate.Replace("%", ""), out vatRate);
                    }

                    if (!string.IsNullOrEmpty(name))
                    {
                        extractedData.LineItems.Add(
                            new SmartInvoice.API.Entities.JsonModels.InvoiceLineItem
                            {
                                Stt = stt++,
                                ProductName = name,
                                Unit = unit,
                                Quantity = qty,
                                UnitPrice = price,
                                TotalAmount = total,
                                VatRate = vatRate,
                                VatAmount = taxAmount,
                            }
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trích xuất dữ liệu XML");
                validationResult.AddError(ErrorCodes.ExtractData, $"Lỗi khi bóc tách dữ liệu: {ex.Message}", "Cấu trúc XML không đúng quy chuẩn.");
            }

            return extractedData;
        }

        public async Task<ValidationResultDto> ValidateBusinessLogicAsync(XmlDocument xmlDoc, Guid? companyId = null, CancellationToken cancellationToken = default)
        {
            var result = new ValidationResultDto();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                string GetVal(string tag) => GetNodeValue(xmlDoc, tag);
                string pBan = GetVal("PBan");
                string khmshDon = GetVal("KHMSHDon");
                string khhDon = GetVal("KHHDon");
                string shDon = GetVal("SHDon");
                string nLap = GetVal("NLap");
                string dvtTe = GetVal("DVTTe");
                string tGia = GetVal("TGia");
                string tchDon = GetVal("TCHDon");
                string mccqt = GetVal("MCCQT");
                string ngayKy = GetVal("NgayKy");
                string mauSo = GetVal("MauSo");

                XmlNode nBan = xmlDoc.SelectSingleNode("//*[local-name()='NBan']");
                string sellerTax = nBan?.SelectSingleNode(".//*[local-name()='MST']")?.InnerText;

                XmlNode nMua = xmlDoc.SelectSingleNode("//*[local-name()='NMua']");
                string buyerTax = nMua?.SelectSingleNode(".//*[local-name()='MST']")?.InnerText;

                string totalAmountStr = GetVal("TgTTTBSo");

                bool isCashRegister = !string.IsNullOrEmpty(khhDon) && khhDon.Length >= 4 && char.ToUpper(khhDon[3]) == 'M';
                bool isVatInvoice = !(khmshDon?.StartsWith("2") == true || mauSo == "2");

                bool isFormatValid = ValidateFormatRules(xmlDoc, result, pBan, khmshDon, khhDon, shDon, dvtTe, tGia, tchDon, nLap, ngayKy, mccqt, isCashRegister, sellerTax, totalAmountStr);

                if (ValidateSignerSubjectMismatch(xmlDoc, sellerTax, result))
                {
                    return result;
                }

                if (!isFormatValid) return result;

                bool dbValid = await ValidateDatabaseConstraintsAsync(companyId, sellerTax, buyerTax, GetVal("NMua/Ten"), khhDon, shDon, result, cancellationToken);
                if (!dbValid) return result;

                ValidateFinancialMath(xmlDoc, result, isVatInvoice, totalAmountStr, GetVal("TgTCThue") ?? "0", GetVal("TgTThue") ?? "0");

                if (!string.IsNullOrEmpty(sellerTax))
                {
                    if (!IsValidTaxCode(sellerTax))
                    {
                        result.AddError(ErrorCodes.LogicTaxFormat, $"[LỖI MST] Mã số thuế '{sellerTax}' không đúng định dạng!", "Vui lòng kiểm tra lại MST người bán.");
                    }
                    else
                    {
                        // Note: VietQR validation is now performed asynchronously via SQS
                        // The invoice will be updated with validation results by the background worker
                        _logger.LogInformation("VietQR validation will be performed asynchronously for TaxCode={TaxCode}", sellerTax);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("ValidateBusinessLogicAsync was canceled.");
                result.AddError(ErrorCodes.Cancelled, "Hành động bị hủy bới người dùng hoặc hệ thống.", "Vui lòng thử lại sau.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trong quá trình ValidateBusinessLogicAsync");
                result.AddError(ErrorCodes.LogicSystem, $"Lỗi hệ thống: {ex.Message}", "Lỗi không xác định khi kiểm tra logic kế toán.");
            }

            return result;
        }

        // ================= HELPER FUNCTIONS =================

        private bool ValidateFormatRules(XmlDocument xmlDoc, ValidationResultDto result, string pBan, string khmshDon, string khhDon, string shDon, string dvtTe, string tGia, string tchDon, string nLap, string ngayKy, string mccqt, bool isCashRegister, string sellerTax, string totalAmountStr)
        {
            bool isDataValid = true;

            isDataValid &= CheckMandatory(pBan, "Phiên bản (PBan)", result);
            if (!string.IsNullOrEmpty(pBan) && !SupportedXmlVersions.Contains(pBan))
            {
                result.AddError(ErrorCodes.LogicVersion, $"Phiên bản XML của hóa đơn ({pBan}) chưa được hỗ trợ. Hệ thống hiện chỉ nhận định dạng {string.Join(", ", SupportedXmlVersions)}.", "Kiểm tra phiên bản XML truyền vào.");
                isDataValid = false;
            }

            isDataValid &= CheckMandatory(khmshDon, "Ký hiệu mẫu số (KHMSHDon)", result);
            if (!string.IsNullOrEmpty(khmshDon) && (khmshDon.Length != 1 || !"123456".Contains(khmshDon)))
            {
                result.AddError(ErrorCodes.LogicInvType, $"Ký hiệu mẫu số '{khmshDon}' chưa chính xác (cần 1 ký tự từ 1 đến 6).", "Kiểm tra lại ký hiệu mẫu số hóa đơn.");
                isDataValid = false;
            }

            isDataValid &= CheckMandatory(khhDon, "Ký hiệu hóa đơn (KHHDon)", result);
            if (!string.IsNullOrEmpty(khhDon) && khhDon.Length != 6)
            {
                result.AddError(ErrorCodes.LogicInvSymbol, $"[LỖI CẤU TRÚC] Ký hiệu hóa đơn '{khhDon}' sai định dạng. Bắt buộc phải có chiều dài chính xác là 6 ký tự.", "Kiểm tra lại ký hiệu hóa đơn.");
                isDataValid = false;
            }

            isDataValid &= CheckMandatory(shDon, "Số hóa đơn (SHDon)", result);
            if (!string.IsNullOrEmpty(shDon) && shDon.Length > 8)
            {
                result.AddError(ErrorCodes.LogicInvNum, $"[LỖI CẤU TRÚC] Số hóa đơn '{shDon}' sai định dạng. Bắt buộc có chiều dài tối đa là 8 ký tự.", "Kiểm tra lại số hóa đơn.");
                isDataValid = false;
            }

            string currencyUnit = string.IsNullOrEmpty(dvtTe) ? "VND" : dvtTe.ToUpper();
            if (currencyUnit != "VND")
            {
                if (string.IsNullOrEmpty(tGia))
                {
                    result.AddError(ErrorCodes.LogicCurrency, $"[LỖI CẤU TRÚC] Tiền tệ là '{currencyUnit}', bắt buộc phải có hệ số Tỷ giá (TGia).", "Bổ sung tỷ giá ngoại tệ.");
                    isDataValid = false;
                }
                else if (!decimal.TryParse(tGia, out _))
                {
                    result.AddError(ErrorCodes.LogicExRate, $"[LỖI DỮ LIỆU] Tỷ giá '{tGia}' phải là kiểu số thập phân hợp lệ.", "Kiểm tra lại tỷ giá.");
                    isDataValid = false;
                }
            }

            if (!string.IsNullOrEmpty(tchDon) && (tchDon == "1" || tchDon == "2"))
            {
                XmlNodeList tthdLQuanNodes = xmlDoc.SelectNodes("//*[local-name()='TTHDLQuan']");
                if (tthdLQuanNodes == null || tthdLQuanNodes.Count == 0)
                {
                    string tchDonName = tchDon == "1" ? "Thay thế" : "Điều chỉnh";
                    result.AddError(ErrorCodes.LogicRecordType, $"[LỖI NGHIỆP VỤ] Hóa đơn mang tính chất '{tchDonName}' (TCHDon = {tchDon}), bắt buộc phải có nhánh Thông tin hóa đơn liên quan (TTHDLQuan).", "Bổ sung thông tin hóa đơn bản gốc.");
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

            if (isCashRegister)
            {
                if (string.IsNullOrEmpty(mccqt))
                {
                    result.AddError(ErrorCodes.LogicMccqt, "Hóa đơn khởi tạo từ máy tính tiền bắt buộc phải có Mã của Cơ quan thuế (MCCQT).", "Vui lòng bổ sung MCCQT từ Cơ quan Thuế.");
                    isDataValid = false;
                }
            }

            isDataValid &= CheckMandatory(nLap, "Ngày lập (NLap)", result);
            isDataValid &= CheckMandatory(sellerTax, "MST Người Bán", result);
            isDataValid &= CheckMandatory(totalAmountStr, "Tổng tiền bằng số", result);

            return isDataValid;
        }

        private bool ValidateSignerSubjectMismatch(XmlDocument xmlDoc, string sellerTax, ValidationResultDto result)
        {
            string actualSignerSubject = GetSignerSubjectInternal(xmlDoc);

            if (!string.IsNullOrEmpty(actualSignerSubject))
            {
                if (!string.IsNullOrEmpty(sellerTax) && !actualSignerSubject.Contains(sellerTax))
                {
                    result.AddError(ErrorCodes.LogicSignerMismatch, $"Chữ ký số không hợp lệ: MST người bán trên hóa đơn ({sellerTax}) không khớp với người ký thực tế ({actualSignerSubject}).", "Kiểm tra lại chữ ký số có thuộc về đúng MST người bán hay không.");
                    return true;
                }
            }
            return false;
        }

        private async Task<bool> ValidateDatabaseConstraintsAsync(Guid? companyId, string sellerTax, string buyerTax, string? buyerName, string khhDon, string shDon, ValidationResultDto result, CancellationToken cancellationToken, string processingMethod = "XML")
        {
            if (!companyId.HasValue) return true;

            var company = await _unitOfWork.Companies.GetByIdAsync(companyId.Value);
            if (company != null)
            {
                if (!string.IsNullOrEmpty(buyerTax))
                {
                    if (!string.Equals(buyerTax.Trim(), company.TaxCode?.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        result.AddError(ErrorCodes.LogicOwner, $"Mã số thuế người mua trên hóa đơn ({buyerTax}) không khớp với công ty hiện tại.", "Vui lòng chỉ tải lên hóa đơn thuộc quyền sở hữu của công ty bạn.");
                        return false;
                    }
                    
                    if (!string.IsNullOrWhiteSpace(buyerName) && !string.IsNullOrWhiteSpace(company.CompanyName))
                    {
                        double similarity = CalculateSimilarity(buyerName.ToLower(), company.CompanyName.ToLower());
                        if (similarity < 0.6)
                        {
                            result.AddWarning($"[RỦI RO TÊN NGƯỜI MUA] Tên người mua trên hóa đơn (\"{buyerName}\") có khác biệt so với tên công ty của bạn đăng ký trên hệ thống (\"{company.CompanyName}\").");
                        }
                    }
                }
                else
                {
                    result.AddWarning("WARN_LOGIC_NO_BUYER_TAX", "Hóa đơn không có thông tin Mã số thuế người mua.", "Cần trình bày thêm lý do giải trình khi gửi duyệt nội bộ.");
                }
            }

            if (!string.IsNullOrEmpty(sellerTax) && !string.IsNullOrEmpty(khhDon) && !string.IsNullOrEmpty(shDon))
            {
                var existingInvoice = await _unitOfWork.Invoices.GetExistingInvoiceAsync(sellerTax, khhDon, shDon, companyId.Value);

                if (existingInvoice != null)
                {
                    // --- INVOICE DOSSIER MERGE LOGIC ---
                    // Case 3A: Uploading XML, existing record was from OCR (has VisualFileId but no OriginalFileId)
                    if (processingMethod == "XML" && existingInvoice.OriginalFileId == null && existingInvoice.ProcessingMethod == "API")
                    {
                        result.MergeMode = DTOs.Invoice.DossierMergeMode.XmlOverridesOcr;
                        result.MergeTargetInvoiceId = existingInvoice.InvoiceId;
                        // Not a fatal error — allow the upload to proceed with merge
                        return true;
                    }

                    // Case 3B: Uploading OCR, existing record was from XML (has OriginalFileId)
                    if (processingMethod == "API" && existingInvoice.OriginalFileId != null)
                    {
                        result.MergeMode = DTOs.Invoice.DossierMergeMode.OcrAttachesToXml;
                        result.MergeTargetInvoiceId = existingInvoice.InvoiceId;
                        // Not a fatal error — allow the upload to proceed (will only attach visual file)
                        return true;
                    }

                    // --- ORIGINAL DUPLICATE LOGIC (true duplicate) ---
                    if (existingInvoice.Status == nameof(InvoiceStatus.Rejected))
                    {
                        if (existingInvoice.Workflow != null && existingInvoice.Workflow.RejectedBy.HasValue)
                        {
                            result.AddError(ErrorCodes.LogicDuplicateRejected, $"Hóa đơn số {shDon} đã bị Quản trị viên từ chối trước đó. Không thể tải lên lại bản sao này.", "Hóa đơn đã bị từ chối nghiệp vụ. Vui lòng liên hệ nhà cung cấp phát hành hóa đơn mới hoặc điều chỉnh.");
                            return false;
                        }

                        result.IsReplacement = true;
                        result.ReplacedInvoiceId = existingInvoice.InvoiceId;
                        result.NewVersion = existingInvoice.Version + 1;
                    }
                    else
                    {
                        result.AddError(ErrorCodes.LogicDuplicate, $"Hóa đơn số {shDon} (Ký hiệu: {khhDon}) của MST {sellerTax} đã tồn tại trong hệ thống.", "Vui lòng kiểm tra lại, hóa đơn này đã được tải lên trước đó.");
                        return false;
                    }
                }
            }

            if (!string.IsNullOrEmpty(sellerTax))
            {
                var blacklisted = await _unitOfWork.LocalBlacklists.GetByTaxCodeAsync(sellerTax);
                if (blacklisted != null)
                {
                    result.AddError(ErrorCodes.LogicBlacklist, $"[RỦI RO DANH SÁCH ĐEN] Mã số thuế người bán '{sellerTax}' thuộc danh sách đen nội bộ! Lý do: {blacklisted.Reason}", "Cảnh báo giao dịch với công ty này theo quy định nội bộ.");
                }
            }

            return true;
        }

        private void ValidateFinancialMath(XmlDocument xmlDoc, ValidationResultDto result, bool isVatInvoice, string totalAmountStr, string sTotalPreTax, string sTotalTax)
        {
            decimal totalAmount = 0;
            CheckDecimal(totalAmountStr, "TgTTTBSo", out totalAmount, result);

            XmlNodeList items = xmlDoc.SelectNodes("//*[local-name()='HHDVu']");
            decimal totalLineItems = 0;

            if (items.Count == 0)
            {
                result.AddError(ErrorCodes.LogicNoItems, "[LỖI CẤU TRÚC] Không có dòng hàng hóa (HHDVu) nào!", "Kiểm tra lại nội dung hóa đơn, không tìm thấy hàng hóa dịch vụ.");
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
                    if (!ValidTaxRates.Contains(tSuat))
                    {
                        result.AddError(ErrorCodes.LogicTaxRate, $"[RỦI RO THUẾ SUẤT] Thuế suất '{tSuat}' không hợp lệ hoặc bất thường ở hàng hóa: {name}", "Xác minh lại mức thuế suất hiện hành.");
                    }
                }

                if (string.IsNullOrEmpty(tChat))
                {
                    result.AddError(ErrorCodes.LogicNoProperty, $"[LỖI CẤU TRÚC] Thiếu trường 'Tính chất' (TChat) cho hàng hóa: {name}", "Bổ sung tính chất cho hàng hóa (Hàng hóa, khuyến mại, chiết khấu...).");
                }

                decimal qty = 0, price = 0, totalClaimed = 0;
                CheckDecimal(sQty, "SLuong", out qty, result);
                CheckDecimal(sPrice, "DGia", out price, result);
                CheckDecimal(sTotal, "ThTien", out totalClaimed, result);

                totalLineItems += totalClaimed;

                if (tChat == "1" && Math.Abs((qty * price) - totalClaimed) > CurrencyTolerance)
                {
                    result.AddWarning("WARN_LOGIC_CALC_DEV", $"[CẢNH BÁO RỦI RO] Sai lệch tính toán: {name} (Lệch Thành tiền > {CurrencyTolerance}đ)", "Kiểm tra lại đơn giá * số lượng có khớp với thành tiền không.");
                }
            }

            decimal totalPreTax = 0, totalTax = 0;
            CheckDecimal(sTotalPreTax, "TgTCThue", out totalPreTax, result);
            CheckDecimal(sTotalTax, "TgTThue", out totalTax, result);

            if (isVatInvoice)
            {
                if (Math.Abs((totalPreTax + totalTax) - totalAmount) > CurrencyTolerance)
                {
                    result.AddError(ErrorCodes.LogicTotalMismatch, $"[LỖI LOGIC] Tổng thanh toán KHÔNG KHỚP (Tiền hàng + Thuế)", "Cộng dồn tiền hàng và thuế không khớp tổng thanh toán, kiểm tra lại hóa đơn.");
                }
            }
            else
            {
                decimal comparisonBase = (totalPreTax == 0 && Math.Abs(totalPreTax - totalAmount) > CurrencyTolerance) ? totalLineItems : totalPreTax;
                if (Math.Abs(comparisonBase - totalAmount) > CurrencyTolerance)
                {
                    result.AddError(ErrorCodes.LogicSalesTotalMismatch, $"[LỖI LOGIC] Hóa đơn bán hàng: Tổng tiền không khớp.", "Cộng dồn chi tiết dòng hàng hóa không khớp với tổng tiền hóa đơn.");
                }
            }
        }

        private string GetNodeValue(XmlDocument doc, string localName)
        {
            XmlNode node = doc.SelectSingleNode($"//*[local-name()='{localName}']");
            return node?.InnerText;
        }

        private bool CheckMandatory(string value, string fieldName, ValidationResultDto result)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                result.AddError(ErrorCodes.XmlMissingField, $"[LỖI CẤU TRÚC] Thiếu trường bắt buộc: {fieldName}", $"Vui lòng bổ sung: {fieldName}");
                return false;
            }
            return true;
        }

        private bool CheckDecimal(
            string value,
            string fieldName,
            out decimal decimalResult,
            ValidationResultDto result
        )
        {
            if (!decimal.TryParse(value, out decimalResult))
            {
                result.AddError(ErrorCodes.DataNotNumber, $"[LỖI DỮ LIỆU] Trường '{fieldName}' phải là số! Giá trị hiện tại: '{value}'", "Xem lại định dạng dữ liệu (bắt buộc dạng số thập phân).");
                return false;
            }
            return true;
        }

        private bool IsValidTaxCode(string taxCode)
        {
            if (string.IsNullOrEmpty(taxCode))
                return false;

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
            if (mst10.Length != 10)
                return false;

            int[] weights = { 31, 29, 23, 19, 17, 13, 7, 5, 3 };
            long sum = 0;

            for (int i = 0; i < 9; i++)
            {
                if (!char.IsDigit(mst10[i]))
                    return false;
                sum += (mst10[i] - '0') * weights[i];
            }

            long remainder = sum % 11;
            long checkDigit = 10 - remainder;

            if (checkDigit == 10)
                return false;

            int actualDigit = mst10[9] - '0';
            return checkDigit == actualDigit;
        }

        private double CalculateSimilarity(string source, string target)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target)) return 0;
            if (source == target) return 1.0;

            int stepsToSame = ComputeLevenshteinDistance(source, target);
            return (1.0 - ((double)stepsToSame / (double)Math.Max(source.Length, target.Length)));
        }

        private int ComputeLevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            if (n == 0) return m;
            if (m == 0) return n;

            for (int i = 0; i <= n; d[i, 0] = i++) { }
            for (int j = 0; j <= m; d[0, j] = j++) { }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
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
                                    X509Certificate2? cert = (X509Certificate2?)
                                        x509Data.Certificates[0];
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

        public InvoiceExtractedData ExtractOcrData(OcrInvoiceResult ocrData)
        {
            var extractedData = new InvoiceExtractedData
            {
                LineItems = new System.Collections.Generic.List<SmartInvoice.API.Entities.JsonModels.InvoiceLineItem>()
            };

            if (ocrData == null) return extractedData;

            if (ocrData.Invoice != null)
            {
                extractedData.InvoiceCurrency = ocrData.Invoice.Currency?.Value;
                if (DateTime.TryParse(ocrData.Invoice.Date?.Value, out DateTime invDate))
                    extractedData.InvoiceDate = invDate;
                extractedData.InvoiceNumber = ocrData.Invoice.Number?.Value;
                extractedData.PaymentTerms = ocrData.Invoice.PaymentMethod?.Value;
                extractedData.InvoiceSymbol = ocrData.Invoice.Symbol?.Value;
                extractedData.TotalAmount = ocrData.Invoice.TotalAmount?.Value ?? 0;
                extractedData.TotalPreTax = ocrData.Invoice.Subtotal?.Value ?? 0;
                extractedData.TotalTaxAmount = ocrData.Invoice.VatAmount?.Value ?? 0;
            }

            if (ocrData.Seller != null)
            {
                extractedData.SellerAddress = ocrData.Seller.Address?.Value;
                extractedData.SellerBankAccount = ocrData.Seller.BankAccount?.Value;
                extractedData.SellerBankName = ocrData.Seller.BankName?.Value;
                extractedData.SellerName = ocrData.Seller.Name?.Value;
                extractedData.SellerPhone = ocrData.Seller.Phone?.Value;
                extractedData.SellerTaxCode = ocrData.Seller.TaxCode?.Value;
                if (string.IsNullOrEmpty(extractedData.SellerTaxCode) && !string.IsNullOrEmpty(ocrData.Seller.TaxAuthorityCode?.Value))
                {
                     // Fallback check if tax authority code might hold something useful
                }
            }

            if (ocrData.Buyer != null)
            {
                extractedData.BuyerAddress = ocrData.Buyer.Address?.Value;
                extractedData.BuyerName = ocrData.Buyer.Name?.Value;
                extractedData.BuyerTaxCode = ocrData.Buyer.TaxCode?.Value;
                if (!string.IsNullOrEmpty(ocrData.Buyer.FullName?.Value))
                {
                    extractedData.BuyerContactPerson = ocrData.Buyer.FullName.Value;
                }
            }

            if (ocrData.Items != null && ocrData.Items.Count > 0)
            {
                int stt = 1;
                foreach (var item in ocrData.Items)
                {
                    int vatRate = 0;
                    if (!string.IsNullOrWhiteSpace(item.VatRate?.Value) && item.VatRate.Value.Contains("%"))
                        int.TryParse(item.VatRate.Value.Replace("%", ""), out vatRate);

                    extractedData.LineItems.Add(new SmartInvoice.API.Entities.JsonModels.InvoiceLineItem
                    {
                        Stt = stt++,
                        ProductName = item.Name?.Value,
                        Unit = item.Unit?.Value,
                        Quantity = item.Quantity?.Value ?? 0,
                        UnitPrice = item.UnitPrice?.Value ?? 0,
                        TotalAmount = item.Total?.Value ?? 0,
                        VatRate = vatRate,
                        VatAmount = item.LineTax?.Value ?? 0
                    });
                }
            }

            return extractedData;
        }

        public async Task<ValidationResultDto> ValidateOcrBusinessLogicAsync(OcrInvoiceResult ocrData, Guid? companyId = null, CancellationToken cancellationToken = default)
        {
            var result = new ValidationResultDto();

            if (ocrData == null)
            {
                result.AddError(ErrorCodes.OcrEmpty, "Không có dữ liệu OCR", "Kiểm tra lại kết quả trích xuất AI.");
                return result;
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                string? sellerTax = ocrData.Seller?.TaxCode?.Value;
                string? buyerTax = ocrData.Buyer?.TaxCode?.Value;
                string? khhDon = ocrData.Invoice?.Symbol?.Value;
                string? shDon = ocrData.Invoice?.Number?.Value;

                if (string.IsNullOrWhiteSpace(sellerTax))
                    result.AddError(ErrorCodes.XmlMissingField, "[LỖI DỮ LIỆU OCR] Thiếu trường bắt buộc: MST Người Bán", "OCR không đọc được MST người bán.");
                
                if (ocrData.Invoice?.TotalAmount == null || ocrData.Invoice.TotalAmount.Value == 0)
                    result.AddError(ErrorCodes.XmlMissingField, "[LỖI DỮ LIỆU OCR] Thiếu trường bắt buộc: Tổng tiền", "OCR không đọc được Tổng tiền.");
                
                if (string.IsNullOrWhiteSpace(ocrData.Invoice?.Date?.Value))
                    result.AddError(ErrorCodes.XmlMissingField, "[LỖI DỮ LIỆU OCR] Thiếu trường bắt buộc: Ngày lập", "OCR không đọc được Ngày lập hóa đơn.");

                bool dbValid = await ValidateDatabaseConstraintsAsync(companyId, sellerTax ?? "", buyerTax ?? "", ocrData.Buyer?.Name?.Value, khhDon ?? "", shDon ?? "", result, cancellationToken, processingMethod: "API");
                if (!dbValid) return result;

                decimal totalAmount = ocrData.Invoice?.TotalAmount?.Value ?? 0;
                decimal totalPreTax = ocrData.Invoice?.Subtotal?.Value ?? 0;
                decimal totalTax = ocrData.Invoice?.VatAmount?.Value ?? 0;
                
                if (totalAmount > 0 && Math.Abs((totalPreTax + totalTax) - totalAmount) > CurrencyTolerance)
                {
                    result.AddError(ErrorCodes.LogicTotalMismatch, $"[LỖI LOGIC OCR] Tổng thanh toán KHÔNG KHỚP (Tiền hàng + Thuế)", "Cộng dồn tiền hàng và thuế không khớp tổng thanh toán, kiểm tra lại dữ liệu OCR.");
                }

                if (ocrData.Items != null && ocrData.Items.Any())
                {
                    decimal totalLineItemsPreTax = 0;
                    decimal totalLineItemsTax = 0;

                    foreach (var item in ocrData.Items)
                    {
                        totalLineItemsPreTax += item.Total?.Value ?? 0;
                        totalLineItemsTax += item.LineTax?.Value ?? 0;
                    }

                    if (totalPreTax > 0 && Math.Abs(totalLineItemsPreTax - totalPreTax) > CurrencyTolerance)
                    {
                        result.AddError(ErrorCodes.LogicSalesTotalMismatch, $"[LỖI LOGIC OCR] Tổng tiền hàng từ chi tiết ({totalLineItemsPreTax:N0}) không khớp tổng tiền chưa thuế hóa đơn ({totalPreTax:N0}).", "Chi tiết các dòng hàng bị thiếu hoặc sai lệch giá trị thành tiền.");
                    }
                    else if (totalPreTax == 0 && totalAmount > 0 && Math.Abs(totalLineItemsPreTax + totalLineItemsTax - totalAmount) > CurrencyTolerance)
                    {
                        result.AddError(ErrorCodes.LogicSalesTotalMismatch, $"[LỖI LOGIC OCR] Tổng chi tiết dòng hàng hóa không khớp với tổng tiền hóa đơn.", "Cộng dồn chi tiết dòng hàng hóa không khớp với tổng thanh toán.");
                    }

                    if (totalTax > 0 && totalLineItemsTax > 0 && Math.Abs(totalLineItemsTax - totalTax) > CurrencyTolerance)
                    {
                        result.AddWarning("WARN_LOGIC_TAX_MISMATCH", $"[CẢNH BÁO OCR] Tổng tiền thuế từ dòng hàng hóa ({totalLineItemsTax:N0}) không khớp tổng thuế hóa đơn ({totalTax:N0}).", "Kiểm tra lại dữ liệu tiền thuế từng dòng.");
                    }
                }

                if (!string.IsNullOrEmpty(sellerTax))
                {
                    if (!IsValidTaxCode(sellerTax))
                    {
                        result.AddError(ErrorCodes.LogicTaxFormat, $"[LỖI MST OCR] Mã số thuế '{sellerTax}' không đúng định dạng!", "API VietQR không thể kiểm tra.");
                    }
                    else
                    {
                        // Note: VietQR validation is now performed asynchronously via SQS
                        // The invoice will be updated with validation results by the background worker
                        _logger.LogInformation("VietQR validation will be performed asynchronously for TaxCode={TaxCode}", sellerTax);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                result.AddError(ErrorCodes.Cancelled, "Hành động bị hủy bới người dùng hoặc hệ thống.", "Vui lòng thử lại sau.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi ValidateOcrBusinessLogicAsync");
                result.AddError(ErrorCodes.LogicSystem, $"Lỗi hệ thống OCR Validate: {ex.Message}", "Lỗi.");
            }

            return result;
        }
    }
}
