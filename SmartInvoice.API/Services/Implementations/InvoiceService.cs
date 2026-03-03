using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SmartInvoice.API.DTOs;
using Microsoft.Extensions.Configuration;
using SmartInvoice.API.DTOs.Invoice;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Entities.JsonModels;
using SmartInvoice.API.Repositories.Interfaces;
using SmartInvoice.API.Services.Interfaces;

namespace SmartInvoice.API.Services.Implementations
{
    public class InvoiceService : IInvoiceService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly StorageService _storageService;
        private readonly IInvoiceProcessorService _invoiceProcessor;
        private readonly IConfiguration _configuration;

        public InvoiceService(IUnitOfWork unitOfWork, StorageService storageService, IInvoiceProcessorService invoiceProcessor, IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _storageService = storageService;
            _invoiceProcessor = invoiceProcessor;
            _configuration = configuration;
        }

        public async Task<Invoice?> GetInvoiceByIdAsync(Guid id)
        {
            return await _unitOfWork.Invoices.GetInvoiceWithDetailsAsync(id);
        }

        public async Task<IEnumerable<Invoice>> GetAllInvoicesAsync()
        {
            return await _unitOfWork.Invoices.GetAllAsync();
        }

        public async Task<Invoice> CreateInvoiceAsync(Invoice invoice)
        {
            await _unitOfWork.Invoices.AddAsync(invoice);
            await _unitOfWork.CompleteAsync();
            return invoice;
        }

        public async Task UpdateInvoiceAsync(Guid id, UpdateInvoiceDto request)
        {
            var existingInvoice = await _unitOfWork.Invoices.GetByIdAsync(id);

            if (existingInvoice == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy hóa đơn với ID: {id}");
            }

            existingInvoice.InvoiceNumber = request.InvoiceNumber ?? existingInvoice.InvoiceNumber;
            existingInvoice.SerialNumber = request.SerialNumber ?? existingInvoice.SerialNumber;
            existingInvoice.InvoiceDate = request.InvoiceDate;
            existingInvoice.TotalAmount = request.TotalAmount;
            existingInvoice.Status = request.Status ?? existingInvoice.Status;
            existingInvoice.Notes = request.Notes;

            if (request.TotalAmount > 0) existingInvoice.TotalAmount = request.TotalAmount;

            existingInvoice.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Invoices.Update(existingInvoice);
            await _unitOfWork.CompleteAsync();
        }

        public async Task<bool> DeleteInvoiceAsync(Guid id)
        {
            var invoice = await _unitOfWork.Invoices.GetByIdAsync(id);
            if (invoice == null)
            {
                return false;
            }

            _unitOfWork.Invoices.Remove(invoice);
            await _unitOfWork.CompleteAsync();
            return true;
        }

        public async Task<bool> ValidateInvoiceAsync(Guid id)
        {
            // Placeholder for 3-Layer Validation Logic
            return true;
        }

        public async Task<PagedResult<InvoiceDto>> GetInvoicesAsync(DTOs.Invoice.GetInvoicesQueryDto query, Guid companyId, Guid userId, string userRole)
        {
            var result = await _unitOfWork.Invoices.GetPagedInvoicesAsync(query, companyId, userId, userRole);

            // Map từ Entity sang DTO thủ công (Hoặc dùng AutoMapper sau này)
            var dtos = result.Items.Select(i => new InvoiceDto
            {
                InvoiceId = i.InvoiceId,
                InvoiceNumber = i.InvoiceNumber,
                SerialNumber = i.SerialNumber,
                InvoiceDate = i.InvoiceDate,
                CreatedAt = i.CreatedAt,
                SellerName = i.SellerName,
                SellerTaxCode = i.SellerTaxCode,
                TotalAmount = i.TotalAmount,
                InvoiceCurrency = i.InvoiceCurrency,
                Status = i.Status,
                RiskLevel = i.RiskLevel,
                ProcessingMethod = i.ProcessingMethod,
                UploadedByName = i.Uploader?.FullName ?? "Unknown" // Lấy tên từ bảng User đã Include
            }).ToList();

            return new PagedResult<InvoiceDto>
            {
                Items = dtos,
                TotalCount = result.TotalCount,
                PageIndex = query.Page,
                PageSize = query.Size
            };
        }

        public async Task<IEnumerable<InvoiceAuditLogDto>> GetAuditLogsAsync(Guid invoiceId)
        {
            var invoiceExists = await _unitOfWork.Invoices.GetByIdAsync(invoiceId);
            if (invoiceExists == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy hóa đơn ID: {invoiceId}");
            }

            var logs = await _unitOfWork.InvoiceAuditLogs.GetByInvoiceIdAsync(invoiceId);

            var dtos = logs.Select(log => new InvoiceAuditLogDto
            {
                AuditId = log.AuditId,
                UserEmail = log.UserEmail, //
                UserRole = log.UserRole,   //
                IpAddress = log.IpAddress,
                Action = log.Action,
                CreatedAt = log.CreatedAt,
                Changes = log.Changes,     //
                Reason = log.Reason,
                Comment = log.Comment
            }).ToList();

            return dtos;
        }
        public async Task<ValidationResultDto> ProcessInvoiceXmlAsync(string s3Key, string userId, string companyId)
        {
            if (string.IsNullOrEmpty(s3Key))
            {
                throw new ArgumentException("S3Key is required.");
            }

            string? tempFilePath = null;
            try
            {
                // 1. Tải file từ S3 về máy chủ tạm
                tempFilePath = await _storageService.DownloadToTempFileAsync(s3Key);

                // 2. Validate cấu trúc XSD
                var structResult = _invoiceProcessor.ValidateStructure(tempFilePath);

                var xmlDoc = new System.Xml.XmlDocument();
                xmlDoc.PreserveWhitespace = true;
                xmlDoc.Load(tempFilePath);

                // 3. Verify Chữ ký số
                var sigResult = _invoiceProcessor.VerifyDigitalSignature(xmlDoc);

                // 4. Validate Logic & Business (VietQR...)
                var logicResult = await _invoiceProcessor.ValidateBusinessLogicAsync(xmlDoc);

                // 5. Gộp tất cả các lỗi và cảnh báo lại thành một kết quả duy nhất
                var finalResult = new ValidationResultDto
                {
                    SignerSubject = sigResult.SignerSubject,
                    Errors = new List<string>(),
                    Warnings = new List<string>()
                };

                if (structResult.Errors != null) finalResult.Errors.AddRange(structResult.Errors);
                if (sigResult.Errors != null) finalResult.Errors.AddRange(sigResult.Errors);
                if (logicResult.Errors != null) finalResult.Errors.AddRange(logicResult.Errors);

                if (structResult.Warnings != null) finalResult.Warnings.AddRange(structResult.Warnings);
                if (sigResult.Warnings != null) finalResult.Warnings.AddRange(sigResult.Warnings);
                if (logicResult.Warnings != null) finalResult.Warnings.AddRange(logicResult.Warnings);

                // Trích xuất dữ liệu
                finalResult.ExtractedData = _invoiceProcessor.ExtractData(xmlDoc);

                // --- LƯU VÀO DATABASE ---
                var UserId = Guid.Parse(userId);
                var CompanyId = Guid.Parse(companyId);

                var docTypes = await _unitOfWork.DocumentTypes.GetAllAsync();

                // Xác định loại hóa đơn dựa trên Ký hiệu mẫu số hóa đơn (KHMSHDon)
                var templateCode = finalResult.ExtractedData?.InvoiceTemplateCode;
                int docTypeId = 1; // Default

                if (!string.IsNullOrEmpty(templateCode) && templateCode.StartsWith("1"))
                {
                    // 1 = Hóa đơn GTGT
                    var gtgtType = docTypes.FirstOrDefault(d => d.TypeCode == "GTGT" || d.FormTemplate == "01GTKT");
                    docTypeId = gtgtType?.DocumentTypeId ?? 1;
                }
                else if (!string.IsNullOrEmpty(templateCode) && templateCode.StartsWith("2"))
                {
                    // 2 = Hóa đơn Bán hàng
                    var saleType = docTypes.FirstOrDefault(d => d.TypeCode == "SALE" || d.FormTemplate == "02GTTT");
                    docTypeId = saleType?.DocumentTypeId ?? 2;
                }
                else
                {
                    // Fallback
                    var defaultDocType = docTypes.FirstOrDefault();
                    docTypeId = defaultDocType?.DocumentTypeId ?? 1;
                }

                var invoiceId = Guid.NewGuid();
                var isInvoiceValid = finalResult.IsValid;

                // Lấy bucket name từ cấu hình (ưu tiên biến môi trường AWS_BUCKET_NAME, sau đó lấy từ appsettings.json)
                var bucketName = Environment.GetEnvironmentVariable("AWS_BUCKET_NAME")
                                 ?? _configuration["AWS:BucketName"]
                                 ?? "smartinvoice-storage-team-dat";

                // 1. Tạo FileStorage
                var fileInfo = new FileInfo(tempFilePath);
                var fileStorage = new FileStorage
                {
                    FileId = Guid.NewGuid(),
                    CompanyId = CompanyId,
                    UploadedBy = UserId,
                    OriginalFileName = s3Key.Split('/').Last(),
                    FileExtension = ".xml",
                    FileSize = fileInfo.Exists ? fileInfo.Length : 0,
                    MimeType = "text/xml",
                    S3BucketName = bucketName,
                    S3Key = s3Key,
                    IsProcessed = true,
                    ProcessedAt = DateTime.UtcNow
                };

                // 2. Tạo Invoice
                var invoice = new Invoice
                {
                    InvoiceId = invoiceId,
                    CompanyId = CompanyId,
                    DocumentTypeId = docTypeId,
                    OriginalFileId = fileStorage.FileId,
                    ProcessingMethod = "XML",
                    InvoiceNumber = finalResult.ExtractedData?.InvoiceNumber ?? "UNKNOWN",
                    FormNumber = finalResult.ExtractedData?.InvoiceTemplateCode,
                    SerialNumber = finalResult.ExtractedData?.InvoiceSymbol,
                    InvoiceDate = finalResult.ExtractedData?.InvoiceDate != null
                                  ? DateTime.SpecifyKind(finalResult.ExtractedData.InvoiceDate.Value, DateTimeKind.Utc)
                                  : DateTime.UtcNow,
                    InvoiceCurrency = finalResult.ExtractedData?.InvoiceCurrency ?? "VND",
                    ExchangeRate = finalResult.ExtractedData?.ExchangeRate ?? 1,
                    SellerName = finalResult.ExtractedData?.SellerName,
                    SellerTaxCode = finalResult.ExtractedData?.SellerTaxCode,
                    SellerAddress = finalResult.ExtractedData?.SellerAddress,
                    SellerPhone = finalResult.ExtractedData?.SellerPhone,
                    SellerEmail = finalResult.ExtractedData?.SellerEmail,
                    SellerBankAccount = finalResult.ExtractedData?.SellerBankAccount,
                    SellerBankName = finalResult.ExtractedData?.SellerBankName,

                    BuyerName = finalResult.ExtractedData?.BuyerName,
                    BuyerTaxCode = finalResult.ExtractedData?.BuyerTaxCode,
                    BuyerAddress = finalResult.ExtractedData?.BuyerAddress,
                    BuyerPhone = finalResult.ExtractedData?.BuyerPhone,
                    BuyerEmail = finalResult.ExtractedData?.BuyerEmail,
                    BuyerContactPerson = finalResult.ExtractedData?.BuyerContactPerson,

                    TotalAmountBeforeTax = finalResult.ExtractedData?.TotalPreTax,
                    TotalTaxAmount = finalResult.ExtractedData?.TotalTaxAmount,
                    TotalAmount = finalResult.ExtractedData?.TotalAmount ?? 0,
                    TotalAmountInWords = finalResult.ExtractedData?.TotalAmountInWords,

                    PaymentMethod = finalResult.ExtractedData?.PaymentTerms,
                    MCCQT = finalResult.ExtractedData?.MCCQT,
                    RawData = new InvoiceRawData { ObjectKey = s3Key },
                    ExtractedData = finalResult.ExtractedData,

                    Status = isInvoiceValid ? "Draft" : "Rejected",
                    RiskLevel = isInvoiceValid ? "Green" : "Red",
                    Notes = isInvoiceValid ? null : "Hóa đơn có lỗi, cần kiểm tra lại",

                    UploadedBy = UserId,
                    CreatedAt = DateTime.UtcNow
                };

                // 3. Tạo InvoiceLineItems
                if (finalResult.ExtractedData?.LineItems != null)
                {
                    foreach (var item in finalResult.ExtractedData.LineItems)
                    {
                        var lineItem = new SmartInvoice.API.Entities.InvoiceLineItem
                        {
                            LineItemId = Guid.NewGuid(),
                            InvoiceId = invoiceId,
                            LineNumber = item.Stt,
                            ItemName = item.ProductName,
                            Unit = item.Unit,
                            Quantity = item.Quantity,
                            UnitPrice = item.UnitPrice,
                            TotalAmount = item.TotalAmount,
                            VatRate = item.VatRate,
                            VatAmount = item.VatAmount
                        };
                        invoice.InvoiceLineItems.Add(lineItem);
                    }
                }

                // 4. Tạo ValidationLayers cho 3 bước kiểm tra
                string? GetErrorStr(List<string>? errs) => errs != null && errs.Any() ? System.Text.Json.JsonSerializer.Serialize(errs) : null;

                invoice.ValidationLayers.Add(new ValidationLayer
                {
                    LayerId = Guid.NewGuid(),
                    InvoiceId = invoiceId,
                    LayerName = "Structure",
                    LayerOrder = 1,
                    IsValid = structResult.IsValid,
                    ValidationStatus = structResult.IsValid ? "Pass" : "Fail",
                    ErrorDetails = GetErrorStr(structResult.Errors)
                });

                invoice.ValidationLayers.Add(new ValidationLayer
                {
                    LayerId = Guid.NewGuid(),
                    InvoiceId = invoiceId,
                    LayerName = "Signature",
                    LayerOrder = 2,
                    IsValid = sigResult.IsValid,
                    ValidationStatus = sigResult.IsValid ? "Pass" : "Fail",
                    ErrorDetails = GetErrorStr(sigResult.Errors)
                });

                invoice.ValidationLayers.Add(new ValidationLayer
                {
                    LayerId = Guid.NewGuid(),
                    InvoiceId = invoiceId,
                    LayerName = "BusinessLogic",
                    LayerOrder = 3,
                    IsValid = logicResult.IsValid,
                    ValidationStatus = logicResult.IsValid ? "Pass" : "Fail",
                    ErrorDetails = GetErrorStr(logicResult.Errors)
                });

                // 5. Tạo RiskCheckResult
                invoice.RiskCheckResults.Add(new RiskCheckResult
                {
                    CheckId = Guid.NewGuid(),
                    InvoiceId = invoiceId,
                    CheckType = "AUTO_UPLOAD_VALIDATION",
                    CheckStatus = isInvoiceValid ? "PASS" : "FAIL",
                    RiskLevel = invoice.RiskLevel,
                    CheckDetails = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        Errors = finalResult.Errors,
                        Warnings = finalResult.Warnings
                    })
                });

                // 6. Tạo Audit Log ghi nhận hành động Upload
                invoice.AuditLogs.Add(new InvoiceAuditLog
                {
                    AuditId = Guid.NewGuid(),
                    InvoiceId = invoiceId,
                    UserId = UserId,
                    Action = "UPLOAD",
                    Comment = isInvoiceValid ? "Uploaded valid invoice to Draft." : "Uploaded invalid invoice to Rejected."
                });

                // Lưu tất cả vào Database
                await _unitOfWork.FileStorages.AddAsync(fileStorage);
                await _unitOfWork.Invoices.AddAsync(invoice);
                await _unitOfWork.CompleteAsync();

                return finalResult;
            }
            finally
            {
                // Luôn dọn dẹp file tạm trên local server (S3 vẫn giữ nguyên file gốc)
                if (tempFilePath != null && System.IO.File.Exists(tempFilePath))
                {
                    System.IO.File.Delete(tempFilePath);
                }
            }
        }
    }
}
