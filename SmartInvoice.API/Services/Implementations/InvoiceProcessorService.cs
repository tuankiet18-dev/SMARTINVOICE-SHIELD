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

        private readonly ILogger<InvoiceProcessorService> _logger;
        private readonly string _xsdPath;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;
        private readonly ISystemConfigProvider _configProvider;

        public InvoiceProcessorService(
            ILogger<InvoiceProcessorService> logger,
            IHostEnvironment env,
            IUnitOfWork unitOfWork,
            IConfiguration configuration,
            ISystemConfigProvider configProvider)
        {
            _logger = logger;
            _xsdPath = Path.Combine(env.ContentRootPath, "Resources", "InvoiceSchema.xsd");
            _unitOfWork = unitOfWork;
            _configuration = configuration;
            _configProvider = configProvider;
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
                    result.AddError(ErrorCodes.XmlStruct, $"Tệp định dạng chưa đúng chuẩn (Dòng {args.Exception.LineNumber})", "Hóa đơn không đúng định dạng XML theo quy định của Tổng cục Thuế.");
                };

                using (var reader = XmlReader.Create(xmlPath, settings))
                {
                    while (reader.Read()) { }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi đọc file XML tại ValidateStructure");
                result.AddError(ErrorCodes.XmlSys, "Không thể đọc nội dung hóa đơn", "Tệp hóa đơn tải lên có thể bị lỗi hoặc không tuân thủ mẫu chuẩn của cơ quan Thuế.");
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
                result.AddError(ErrorCodes.SigSys, "Không thể xác thực thông tin chữ ký điện tử", "Vui lòng tải lại tệp. Nếu lỗi vẫn tiếp diễn, tệp này có thể bị hỏng ở phần định dạng.");
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
                    string sTaxAmountStr = item.SelectSingleNode(".//*[local-name()='ThueGTGT']")?.InnerText
                        ?? item.SelectSingleNode(".//*[local-name()='TTin'][*[local-name()='TTruong']='Tiền thuế' or *[local-name()='TTruong']='VATAmount']/*[local-name()='DLieu']")?.InnerText;

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
                validationResult.AddError(ErrorCodes.ExtractData, "Quá trình đọc dữ liệu thất bại", "Cấu trúc hóa đơn tải lên có thể không tuân theo đúng mẫu chuẩn.");
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

                // 1. Kiểm tra cấu trúc format
                ValidateFormatRules(xmlDoc, result, pBan, khmshDon, khhDon, shDon, dvtTe, tGia, tchDon, nLap, ngayKy, mccqt, isCashRegister, sellerTax, totalAmountStr);

                // 2. Kiểm tra chữ ký
                ValidateSignerSubjectMismatch(xmlDoc, sellerTax, result);

                // 3. Kiểm tra DB: Quyền sở hữu, Trùng lặp
                await ValidateDatabaseConstraintsAsync(companyId, sellerTax, buyerTax, GetVal("NMua/Ten"), khhDon, shDon, result, cancellationToken);

                // 4. LUÔN LUÔN chạy kiểm tra toán học
                await ValidateFinancialMath(xmlDoc, result, isVatInvoice, totalAmountStr, GetVal("TgTCThue") ?? "0", GetVal("TgTThue") ?? "0");

                if (!string.IsNullOrEmpty(sellerTax))
                {
                    if (!IsValidTaxCode(sellerTax))
                    {
                        result.AddError(ErrorCodes.LogicTaxFormat, $"Mã số thuế '{sellerTax}' không đúng định dạng!", "Vui lòng kiểm tra lại MST người bán.");
                    }
                    else
                    {
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
                result.AddError(ErrorCodes.LogicSystem, "Đã xảy ra sự cố không xác định khi kiểm tra dữ liệu", "Vui lòng thử lại sau hoặc liên hệ bộ phận hỗ trợ.");
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
                result.AddError(ErrorCodes.LogicInvSymbol, $"Ký hiệu hóa đơn '{khhDon}' không hợp lệ. Phải có chính xác 6 ký tự.", "Vui lòng tra cứu lại ký hiệu hóa đơn trên bản thể hiện.");
                isDataValid = false;
            }

            isDataValid &= CheckMandatory(shDon, "Số hóa đơn (SHDon)", result);
            if (!string.IsNullOrEmpty(shDon) && shDon.Length > 8)
            {
                result.AddError(ErrorCodes.LogicInvNum, $"Số hóa đơn '{shDon}' vượt quá độ dài quy định. Ký tự tối đa cho phép là 8.", "Vui lòng kiểm tra lại số hóa đơn.");
                isDataValid = false;
            }

            string currencyUnit = string.IsNullOrEmpty(dvtTe) ? "VND" : dvtTe.ToUpper();
            if (currencyUnit != "VND")
            {
                if (string.IsNullOrEmpty(tGia))
                {
                    result.AddError(ErrorCodes.LogicCurrency, $"Đơn vị tiền tệ là '{currencyUnit}' bắt buộc phải cung cấp tỷ giá quy đổi.", "Bổ sung tỷ giá ngoại tệ vào dữ liệu hóa đơn.");
                    isDataValid = false;
                }
                else if (!decimal.TryParse(tGia, out _))
                {
                    result.AddError(ErrorCodes.LogicExRate, $"Tỷ giá '{tGia}' phải là con số hợp lệ.", "Vui lòng xem lại trị giá của tỷ giá.");
                    isDataValid = false;
                }
            }

            if (!string.IsNullOrEmpty(tchDon) && (tchDon == "1" || tchDon == "2"))
            {
                XmlNodeList tthdLQuanNodes = xmlDoc.SelectNodes("//*[local-name()='TTHDLQuan']");
                if (tthdLQuanNodes == null || tthdLQuanNodes.Count == 0)
                {
                    string tchDonName = tchDon == "1" ? "Thay thế" : "Điều chỉnh";
                    result.AddError(ErrorCodes.LogicRecordType, $"Hóa đơn mang tính chất '{tchDonName}' bắt buộc phải chứa thông tin hóa đơn bị thay đổi/điều chỉnh.", "Bạn cần cung cấp thông tin liên kết with hóa đơn gốc.");
                    isDataValid = false;
                }
            }

            if (!string.IsNullOrEmpty(nLap))
            {
                if (DateTime.TryParse(nLap, out DateTime dtLap))
                {
                    if (dtLap > DateTime.Now)
                    {
                        result.AddWarning("WARN_LOGIC_DATE_FUTURE", $"Ngày lập ({dtLap:dd/MM/yyyy}) là một ngày trong tương lai.", "Kiểm tra lại thời gian trên thiết bị máy tính của nhà cung cấp, hoặc thời gian nhận thông điệp.");
                    }

                    if (!string.IsNullOrEmpty(ngayKy) && DateTime.TryParse(ngayKy, out DateTime dtKy))
                    {
                        if (Math.Abs((dtKy - dtLap).TotalDays) > 1)
                        {
                            result.AddWarning("WARN_LOGIC_DATE_DISC", $"Ngày lập ({dtLap:dd/MM/yyyy}) và Ngày ký ({dtKy:dd/MM/yyyy}) có sự chênh lệch.", "Lưu ý xác minh rủi ro nghiệp vụ này khi thực hiện thanh toán.");
                        }
                    }
                }
            }

            if (isCashRegister)
            {
                if (string.IsNullOrEmpty(mccqt))
                {
                    result.AddError(ErrorCodes.LogicMccqt, "Hóa đơn máy tính tiền cần có Mã của Cơ quan thuế (MCCQT).", "Yêu cầu bên bán cung cấp hóa đơn có chứa mã từ Cơ quan Thuế.");
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
                        result.AddError(ErrorCodes.LogicOwner, $"Mã số thuế người mua ({buyerTax}) không khớp với công ty của bạn.", "Vui lòng kiểm tra lại để đảm bảo hóa đơn này xuất đúng tên công ty bạn.");
                    }

                    if (!string.IsNullOrWhiteSpace(buyerName) && !string.IsNullOrWhiteSpace(company.CompanyName))
                    {
                        double similarity = CalculateSimilarity(buyerName.ToLower(), company.CompanyName.ToLower());
                        if (similarity < 0.6)
                        {
                            result.AddWarning($"Tên người mua trên hóa đơn (\"{buyerName}\") không khớp hoàn toàn with tên công ty của bạn (\"{company.CompanyName}\").");
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
                    bool isMerge = false;
                    if (processingMethod == "XML" && existingInvoice.OriginalFileId == null && existingInvoice.ProcessingMethod == "API")
                    {
                        result.MergeMode = DTOs.Invoice.DossierMergeMode.XmlOverridesOcr;
                        result.MergeTargetInvoiceId = existingInvoice.InvoiceId;
                        isMerge = true;
                    }
                    else if (processingMethod == "API" && existingInvoice.OriginalFileId != null)
                    {
                        result.MergeMode = DTOs.Invoice.DossierMergeMode.OcrAttachesToXml;
                        result.MergeTargetInvoiceId = existingInvoice.InvoiceId;
                        isMerge = true;
                    }

                    if (!isMerge)
                    {
                        if (existingInvoice.Status == nameof(InvoiceStatus.Rejected))
                        {
                            if (existingInvoice.Workflow != null && existingInvoice.Workflow.RejectedBy.HasValue)
                            {
                                result.AddError(ErrorCodes.LogicDuplicateRejected, $"Hóa đơn số {shDon} này đã bị từ chối trước đó nên không thể tải lên lại.", "Vui lòng liên hệ nhà cung cấp phát hành hóa đơn mới hoặc hóa đơn điều chỉnh.");
                            }
                            else
                            {
                                result.IsReplacement = true;
                                result.ReplacedInvoiceId = existingInvoice.InvoiceId;
                                result.NewVersion = existingInvoice.Version + 1;
                            }
                        }
                        else
                        {
                            result.AddError(ErrorCodes.LogicDuplicate, $"Hóa đơn số {shDon} (Ký hiệu: {khhDon}) đã tồn tại trong hệ thống.", "Bạn không cần tải lên lại hóa đơn này.");
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(sellerTax))
            {
                var blacklisted = await _unitOfWork.LocalBlacklists.GetByTaxCodeAsync(sellerTax);
                if (blacklisted != null)
                {
                    result.AddError(ErrorCodes.LogicBlacklist, $"Mã số thuế người bán '{sellerTax}' nằm trong danh sách đen của cấu hình nội bộ! Lý do: {blacklisted.Reason}", "Lưu ý rủi ro khi giao dịch với nhà cung cấp này.");
                }
            }

            var fatalErrorCodes = new[] { ErrorCodes.LogicDuplicate, ErrorCodes.LogicDuplicateRejected, ErrorCodes.LogicOwner };
            return !result.ErrorDetails.Any(e => !string.IsNullOrEmpty(e.ErrorCode) && fatalErrorCodes.Contains(e.ErrorCode));
        }

        private void ValidateFinancialLogic(
            ValidationResultDto result,
            decimal totalAmount,
            decimal totalPreTax,
            decimal totalTax,
            decimal tolerance,
            decimal? totalLineItemsPreTax = null,
            decimal? totalLineItemsTax = null,
            bool isVatInvoice = false,
            string source = "XML")
        {
            bool shouldCheckTotalMath = isVatInvoice || totalPreTax > 0 || totalTax > 0;
            if (shouldCheckTotalMath && totalAmount > 0 && Math.Abs((totalPreTax + totalTax) - totalAmount) > tolerance)
            {
                result.AddError(
                    ErrorCodes.LogicTotalMismatch,
                    $"Tổng thanh toán không khớp với tiền hàng cộng thuế",
                    $"Chênh lệch quá dung sai cho phép ({tolerance} VNĐ), vui lòng kiểm tra lại.");
            }

            if (totalLineItemsPreTax.HasValue)
            {
                decimal linePreTax = totalLineItemsPreTax.Value;

                if (totalPreTax > 0)
                {
                    if (Math.Abs(linePreTax - totalPreTax) > tolerance)
                    {
                        result.AddError(
                            ErrorCodes.LogicSalesTotalMismatch,
                            $"Tổng tiền ở chi tiết các dòng ({linePreTax:N0} đ) chưa khớp với tổng tiền chưa thuế chung ({totalPreTax:N0} đ)",
                            "Các dòng chi tiết có thể bị lệch giá trị số tiền.");
                    }
                }
                else if (totalAmount > 0)
                {
                    decimal totalWithLinesTax = linePreTax + (totalLineItemsTax ?? 0);
                    if (Math.Abs(totalWithLinesTax - totalAmount) > tolerance)
                    {
                        result.AddError(
                            ErrorCodes.LogicSalesTotalMismatch,
                            $"Tổng các chi tiết mặt hàng hóa không khớp với tổng khoản tiền hóa đơn",
                            "Giá trị các mặt hàng bị sai lệch.");
                    }
                }
            }

            if (totalLineItemsTax.HasValue && totalTax > 0)
            {
                decimal lineTax = totalLineItemsTax.Value;
                if (Math.Abs(lineTax - totalTax) > tolerance)
                {
                    result.AddWarning(
                        source == "OCR" ? "WARN_LOGIC_TAX_MISMATCH_OCR" : "WARN_LOGIC_TAX_MISMATCH",
                        $"Tiền thuế ở chi tiết ({lineTax:N0} đ) không khớp tổng tiền thuế hóa đơn ({totalTax:N0} đ)",
                        "Kiểm tra sự lệch lạc của thuế GTGT từng mặt hàng.");
                }
            }
        }

        private async Task ValidateFinancialMath(XmlDocument xmlDoc, ValidationResultDto result, bool isVatInvoice, string totalAmountStr, string sTotalPreTax, string sTotalTax)
        {
            decimal tolerance = await _configProvider.GetDecimalAsync("CURRENCY_TOLERANCE", 10m);
            decimal totalAmount = 0;
            CheckDecimal(totalAmountStr, "TgTTTBSo", out totalAmount, result);

            XmlNodeList items = xmlDoc.SelectNodes("//*[local-name()='HHDVu']");
            decimal totalLineItems = 0;
            decimal totalLineItemsTax = 0;

            if (items.Count == 0)
            {
                result.AddError(ErrorCodes.LogicNoItems, "Không tìm thấy nội dung chi tiết danh sách hàng hóa", "Vui lòng kiểm tra lại chất lượng tệp, hoặc nội dung của hóa đơn.");
            }

            foreach (XmlNode item in items)
            {
                string name = item.SelectSingleNode(".//*[local-name()='Ten']")?.InnerText ?? item.SelectSingleNode(".//*[local-name()='THHDVu']")?.InnerText ?? "Hàng hóa";
                string sQty = item.SelectSingleNode(".//*[local-name()='SLuong']")?.InnerText ?? "0";
                string sPrice = item.SelectSingleNode(".//*[local-name()='DGia']")?.InnerText ?? "0";
                string sTotal = item.SelectSingleNode(".//*[local-name()='ThTien']")?.InnerText ?? "0";
                string tChat = item.SelectSingleNode(".//*[local-name()='TChat']")?.InnerText;
                string tSuat = item.SelectSingleNode(".//*[local-name()='TSuat']")?.InnerText;
                string sLineTax = item.SelectSingleNode(".//*[local-name()='ThueGTGT']")?.InnerText
                    ?? item.SelectSingleNode(".//*[local-name()='TTin'][*[local-name()='TTruong']='Tiền thuế' or *[local-name()='TTruong']='VATAmount']/*[local-name()='DLieu']")?.InnerText ?? "0";

                if (!string.IsNullOrEmpty(tSuat))
                {
                    if (!ValidTaxRates.Contains(tSuat))
                    {
                        result.AddError(ErrorCodes.LogicTaxRate, $"Mức thuế suất '{tSuat}' không xác định tại mặt hàng: {name}", "Vui lòng xem lại mức thuế suất hiện hành.");
                    }
                }

                if (string.IsNullOrEmpty(tChat))
                {
                    result.AddError(ErrorCodes.LogicNoProperty, $"Thiếu dữ liệu tính chất đối với hàng hóa: {name}", "Tính chất mặt hàng (hàng hóa, khuyến mại, chiết khấu...) không được để trống.");
                }

                decimal qty = 0, price = 0, totalClaimed = 0, lineTax = 0;
                CheckDecimal(sQty, "SLuong", out qty, result);
                CheckDecimal(sPrice, "DGia", out price, result);
                CheckDecimal(sTotal, "ThTien", out totalClaimed, result);
                CheckDecimal(sLineTax, "ThueGTGT", out lineTax, result);

                totalLineItems += totalClaimed;
                totalLineItemsTax += lineTax;

                if (tChat == "1" && Math.Abs((qty * price) - totalClaimed) > tolerance)
                {
                    result.AddWarning("WARN_LOGIC_CALC_DEV", $"Sai lệch số học ở hàng hóa: {name}", "Đơn giá x số lượng chưa khớp đúng với cột Thành tiền.");
                }
            }

            decimal totalPreTax = 0, totalTax = 0;
            CheckDecimal(sTotalPreTax, "TgTCThue", out totalPreTax, result);
            CheckDecimal(sTotalTax, "TgTThue", out totalTax, result);

            ValidateFinancialLogic(result, totalAmount, totalPreTax, totalTax, tolerance, totalLineItems, totalLineItemsTax, isVatInvoice, "XML");
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
                result.AddError(ErrorCodes.XmlMissingField, $"Hóa đơn bị thiếu thông tin: {fieldName}", $"Trường nội dung này là bắt buộc.");
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
                result.AddError(ErrorCodes.DataNotNumber, $"Dữ liệu '{fieldName}' bắt buộc là dạng số. Tại hóa đơn có giá trị: '{value}'", "Dữ liệu chưa đúng chuẩn định dạng.");
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
                decimal itemSum = 0;
                foreach (var item in ocrData.Items)
                {
                    int vatRate = 0;
                    if (!string.IsNullOrWhiteSpace(item.VatRate?.Value))
                    {
                        var rateVal = item.VatRate.Value.Replace("%", "").Trim();
                        int.TryParse(rateVal, out vatRate);
                    }

                    itemSum += item.Total?.Value ?? 0;

                    decimal lineTotal = item.Total?.Value ?? 0;
                    decimal lineTax = item.LineTax?.Value ?? 0;

                    // Nếu AI trả về 0 nhưng có % thuế, ta tự làm toán nội suy
                    if (lineTax == 0 && lineTotal > 0 && vatRate > 0)
                    {
                        lineTax = Math.Round(lineTotal * vatRate / 100m, 0);
                    }

                    extractedData.LineItems.Add(new SmartInvoice.API.Entities.JsonModels.InvoiceLineItem
                    {
                        Stt = stt++,
                        ProductName = item.Name?.Value,
                        Unit = item.Unit?.Value,
                        Quantity = item.Quantity?.Value ?? 0,
                        UnitPrice = item.UnitPrice?.Value ?? 0,
                        TotalAmount = item.Total?.Value ?? 0,
                        VatRate = vatRate,
                        VatAmount = lineTax
                    });
                }

                // Smart inference: if subtotal is 0 or inconsistent with item sum, trust item sum
                if (extractedData.TotalPreTax == 0 || Math.Abs(extractedData.TotalPreTax - itemSum) > 10)
                {
                    extractedData.TotalPreTax = itemSum;
                }
            }

            // Recalculate VAT from breakdown if missing
            if (ocrData.Invoice?.VatBreakdown != null && ocrData.Invoice.VatBreakdown.Count > 0)
            {
                decimal calculatedTax = 0;
                foreach (var b in ocrData.Invoice.VatBreakdown)
                {
                    decimal taxable = b.TaxableAmount?.Value ?? 0;
                    decimal vat = b.VatAmount?.Value ?? 0;

                    if (vat == 0 && taxable > 0 && !string.IsNullOrEmpty(b.Rate))
                    {
                        var rateMatch = Regex.Match(b.Rate, @"\d+");
                        if (rateMatch.Success && decimal.TryParse(rateMatch.Value, out decimal rateNum))
                        {
                            vat = Math.Round(taxable * rateNum / 100, 0);
                        }
                    }
                    calculatedTax += vat;
                }

                if (extractedData.TotalTaxAmount == 0 && calculatedTax > 0)
                {
                    extractedData.TotalTaxAmount = calculatedTax;
                }
            }

            // Ensure consistent TotalAmount
            if (extractedData.TotalAmount == 0 || Math.Abs(extractedData.TotalAmount - (extractedData.TotalPreTax + extractedData.TotalTaxAmount)) > 10)
            {
                extractedData.TotalAmount = extractedData.TotalPreTax + extractedData.TotalTaxAmount;
            }

            return extractedData;
        }

        public async Task<ValidationResultDto> ValidateOcrBusinessLogicAsync(OcrInvoiceResult ocrData, Guid? companyId = null, CancellationToken cancellationToken = default)
        {
            var result = new ValidationResultDto();
            decimal tolerance = await _configProvider.GetDecimalAsync("CURRENCY_TOLERANCE", 10m);

            if (ocrData == null)
            {
                result.AddError(ErrorCodes.OcrEmpty, "Không có dữ liệu trích xuất từ tệp", "Hệ thống AI chưa đọc được thông tin trên hóa đơn, vui lòng kiểm tra lại chất lượng hình ảnh/PDF tải lên.");
                return result;
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                string? sellerTax = ocrData.Seller?.TaxCode?.Value;
                string? buyerTax = ocrData.Buyer?.TaxCode?.Value;
                string? khhDon = ocrData.Invoice?.Symbol?.Value;
                string? shDon = ocrData.Invoice?.Number?.Value;

                // Add errors/warnings from Python validator (OCR side)
                if (ocrData.Validation != null)
                {
                    if (ocrData.Validation.Errors != null)
                    {
                        foreach (var pyErr in ocrData.Validation.Errors)
                        {
                            // Use a generic code if Python doesn't provide one
                            result.AddError("ERR_OCR_INTERNAL_VALIDATION", pyErr, "Vui lòng kiểm tra lại thực tế trên hóa đơn.");
                        }
                    }
                    if (ocrData.Validation.Warnings != null)
                    {
                        foreach (var pyWarn in ocrData.Validation.Warnings)
                        {
                            result.AddWarning(pyWarn);
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(sellerTax))
                    result.AddError("ERR_LOGIC_MISSING_FIELD", "Thiếu thông tin bắt buộc: Mã số thuế người bán", "Hệ thống không tự động nhận diện được MST người bán.");

                if (ocrData.Invoice?.TotalAmount == null || ocrData.Invoice.TotalAmount.Value == 0)
                    result.AddError("ERR_LOGIC_MISSING_FIELD", "Thiếu thông tin bắt buộc: Tổng tiền hóa đơn", "Trường thông tin tổng tiền của hóa đơn đang bị trống.");

                if (string.IsNullOrWhiteSpace(ocrData.Invoice?.Date?.Value))
                    result.AddError("ERR_LOGIC_MISSING_FIELD", "Thiếu thông tin bắt buộc: Ngày lập", "Hệ thống chưa xác định được ngày lập hóa đơn.");

                await ValidateDatabaseConstraintsAsync(companyId, sellerTax ?? "", buyerTax ?? "", ocrData.Buyer?.Name?.Value, khhDon ?? "", shDon ?? "", result, cancellationToken, processingMethod: "API");

                var extractedData = ExtractOcrData(ocrData);

                decimal totalAmount = extractedData.TotalAmount;
                decimal totalPreTax = extractedData.TotalPreTax;
                decimal totalTax = extractedData.TotalTaxAmount;

                decimal? totalLineItemsPreTax = null;
                decimal? totalLineItemsTax = null;

                // 1. TÍNH TỔNG TIỀN TỪ DỮ LIỆU ĐÃ ĐƯỢC LÀM SẠCH VÀ NỘI SUY (tránh lỗi 0đ)
                if (extractedData.LineItems != null && extractedData.LineItems.Any())
                {
                    totalLineItemsPreTax = 0;
                    totalLineItemsTax = 0;

                    foreach (var item in extractedData.LineItems)
                    {
                        totalLineItemsPreTax += item.TotalAmount;
                        totalLineItemsTax += item.VatAmount; // Đã chứa số tiền thuế được tự động tính ở hàm Extract
                    }
                }

                // 2. DUYỆT DỮ LIỆU RAW ĐỂ GIỮ LẠI CÁC CẢNH BÁO ĐỊNH DẠNG/TOÁN HỌC
                if (ocrData.Items != null && ocrData.Items.Any())
                {
                    foreach (var item in ocrData.Items)
                    {
                        string? vatRateStr = item.VatRate?.Value;
                        if (!string.IsNullOrWhiteSpace(vatRateStr))
                        {
                            string normalizedRate = vatRateStr.Trim();
                            // If it's a number (e.g. "8"), append "%" to match ValidTaxRates (e.g. "8%")
                            if (Regex.IsMatch(normalizedRate, @"^\d+$"))
                            {
                                normalizedRate += "%";
                            }

                            if (!ValidTaxRates.Contains(normalizedRate))
                            {
                                string productName = item.Name?.Value ?? "Hàng hóa";
                                result.AddError(
                                    ErrorCodes.LogicTaxRate,
                                    $"Mức thuế suất '{vatRateStr}' không đúng chuẩn tại mặt hàng: {productName}",
                                    "Vui lòng xem lại mức thuế suất hiện hành.");
                            }
                        }

                        decimal qty = item.Quantity?.Value ?? 0;
                        decimal price = item.UnitPrice?.Value ?? 0;
                        decimal total = item.Total?.Value ?? 0;

                        if (qty > 0 && price > 0 && total > 0)
                        {
                            decimal expectedTotal = qty * price;
                            if (Math.Abs(expectedTotal - total) > tolerance)
                            {
                                string productName = item.Name?.Value ?? "Hàng hóa";
                                result.AddWarning(
                                    "WARN_LOGIC_CALC_DEV_OCR",
                                    $"Sai lệch số học ở mặt hàng {productName} (Đơn giá: {(price):N0}, Số lượng: {qty}, Thành tiền: {(total):N0})",
                                    "Tính toán Đơn giá x Số lượng không khớp với giá trị Thành tiền thu được.");
                            }
                        }
                    }
                }

                bool isOcrVatInvoice = true;
                string? invoiceTypeStr = ocrData.Invoice?.Type?.Value?.ToUpper();
                string? invoiceSymbol = ocrData.Invoice?.Symbol?.Value;

                if ((invoiceTypeStr != null && invoiceTypeStr.Contains("BÁN HÀNG")) ||
                    (!string.IsNullOrEmpty(invoiceSymbol) && invoiceSymbol.StartsWith("2")))
                {
                    isOcrVatInvoice = false;
                }

                ValidateFinancialLogic(result, totalAmount, totalPreTax, totalTax, tolerance, totalLineItemsPreTax, totalLineItemsTax, isVatInvoice: isOcrVatInvoice, "OCR");

                if (!string.IsNullOrEmpty(sellerTax))
                {
                    if (!IsValidTaxCode(sellerTax))
                    {
                        result.AddError(ErrorCodes.LogicTaxFormat, $"Mã số thuế '{sellerTax}' trích xuất bị sai định dạng chuẩn!", "Cần kiểm tra MST nhà cung cấp.");
                    }
                    else
                    {
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
                result.AddError(ErrorCodes.LogicSystem, "Đã xảy ra sự cố trong quá trình kiểm tra hình ảnh/PDF", "Hệ thống gặp sự cố, xin thử lại sau.");
            }

            return result;
        }
    }
}
