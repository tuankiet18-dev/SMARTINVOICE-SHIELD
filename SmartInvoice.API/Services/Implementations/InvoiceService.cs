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

        // ════════════════════════════════════════════
        //  QUERY
        // ════════════════════════════════════════════

        public async Task<InvoiceDetailDto?> GetInvoiceDetailAsync(Guid invoiceId, Guid companyId, Guid userId, string userRole)
        {
            var invoice = await _unitOfWork.Invoices.GetInvoiceWithDetailsAsync(invoiceId);
            if (invoice == null) return null;

            // Multi-tenant check
            if (invoice.CompanyId != companyId) return null;

            // RBAC: Member chỉ xem hóa đơn do mình upload
            if (userRole == "Member" && invoice.UploadedBy != userId) return null;

            return MapToDetailDto(invoice);
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

        public async Task UpdateInvoiceAsync(Guid id, UpdateInvoiceDto request, Guid userId, string userEmail, string userRole, string? ipAddress)
        {
            var existingInvoice = await _unitOfWork.Invoices.GetByIdAsync(id);
            if (existingInvoice == null)
                throw new KeyNotFoundException($"Không tìm thấy hóa đơn với ID: {id}");

            // Chỉ cho phép edit khi status = Draft hoặc Rejected
            if (existingInvoice.Status != "Draft" && existingInvoice.Status != "Rejected")
                throw new InvalidOperationException($"Chỉ được chỉnh sửa hóa đơn ở trạng thái Nháp hoặc Từ chối. Trạng thái hiện tại: {existingInvoice.Status}");

            // Track changes for audit
            var changes = new List<AuditChange>();
            void TrackChange(string field, object? oldVal, object? newVal)
            {
                if (oldVal?.ToString() != newVal?.ToString())
                    changes.Add(new AuditChange { Field = field, OldValue = oldVal, NewValue = newVal, ChangeType = "UPDATE" });
            }

            if (request.InvoiceNumber != null)
            {
                TrackChange("InvoiceNumber", existingInvoice.InvoiceNumber, request.InvoiceNumber);
                existingInvoice.InvoiceNumber = request.InvoiceNumber;
            }
            if (request.SerialNumber != null)
            {
                TrackChange("SerialNumber", existingInvoice.SerialNumber, request.SerialNumber);
                existingInvoice.SerialNumber = request.SerialNumber;
            }
            TrackChange("InvoiceDate", existingInvoice.InvoiceDate, request.InvoiceDate);
            existingInvoice.InvoiceDate = request.InvoiceDate;

            if (request.TotalAmount > 0)
            {
                TrackChange("TotalAmount", existingInvoice.TotalAmount, request.TotalAmount);
                existingInvoice.TotalAmount = request.TotalAmount;
            }
            if (request.Status != null)
            {
                TrackChange("Status", existingInvoice.Status, request.Status);
                existingInvoice.Status = request.Status;
            }
            TrackChange("Notes", existingInvoice.Notes, request.Notes);
            existingInvoice.Notes = request.Notes;

            // Nếu hóa đơn đã bị Rejected thì sửa xong trả về Draft
            if (existingInvoice.Status == "Rejected")
            {
                existingInvoice.Status = "Draft";
                existingInvoice.RejectedBy = null;
                existingInvoice.RejectedAt = null;
                existingInvoice.RejectionReason = null;
            }

            existingInvoice.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Invoices.Update(existingInvoice);

            // Audit log
            if (changes.Any())
            {
                await _unitOfWork.InvoiceAuditLogs.AddAsync(new InvoiceAuditLog
                {
                    AuditId = Guid.NewGuid(),
                    InvoiceId = id,
                    UserId = userId,
                    UserEmail = userEmail,
                    UserRole = userRole,
                    Action = "EDIT",
                    Changes = changes,
                    IpAddress = ipAddress,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _unitOfWork.CompleteAsync();
        }

        public async Task<bool> DeleteInvoiceAsync(Guid id, Guid companyId, Guid userId, string userRole)
        {
            var invoice = await _unitOfWork.Invoices.GetByIdAsync(id);
            if (invoice == null) return false;

            // Multi-tenant check
            if (invoice.CompanyId != companyId) return false;

            // Chỉ cho phép xóa hóa đơn Draft
            if (invoice.Status != "Draft")
                throw new InvalidOperationException("Chỉ được xóa hóa đơn ở trạng thái Nháp.");

            // Soft delete
            invoice.IsDeleted = true;
            invoice.DeletedAt = DateTime.UtcNow;
            _unitOfWork.Invoices.Update(invoice);
            await _unitOfWork.CompleteAsync();
            return true;
        }

        public async Task<bool> ValidateInvoiceAsync(Guid id)
        {
            return true;
        }

        public async Task<PagedResult<InvoiceDto>> GetInvoicesAsync(GetInvoicesQueryDto query, Guid companyId, Guid userId, string userRole)
        {
            var result = await _unitOfWork.Invoices.GetPagedInvoicesAsync(query, companyId, userId, userRole);

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
                UploadedByName = i.Uploader?.FullName ?? "Unknown"
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
                throw new KeyNotFoundException($"Không tìm thấy hóa đơn ID: {invoiceId}");

            var logs = await _unitOfWork.InvoiceAuditLogs.GetByInvoiceIdAsync(invoiceId);

            return logs.Select(log => new InvoiceAuditLogDto
            {
                AuditId = log.AuditId,
                UserEmail = log.UserEmail,
                UserRole = log.UserRole,
                IpAddress = log.IpAddress,
                Action = log.Action,
                CreatedAt = log.CreatedAt,
                Changes = log.Changes,
                Reason = log.Reason,
                Comment = log.Comment
            }).ToList();
        }

        // ════════════════════════════════════════════
        //  WORKFLOW: Submit → Pending
        // ════════════════════════════════════════════

        public async Task SubmitInvoiceAsync(Guid invoiceId, Guid companyId, Guid userId, string userEmail, string userRole, string? comment, string? ipAddress)
        {
            var invoice = await _unitOfWork.Invoices.GetByIdAsync(invoiceId);
            if (invoice == null)
                throw new KeyNotFoundException("Không tìm thấy hóa đơn.");
            if (invoice.CompanyId != companyId)
                throw new UnauthorizedAccessException("Không có quyền truy cập hóa đơn này.");
            if (invoice.Status != "Draft")
                throw new InvalidOperationException($"Chỉ có thể gửi duyệt hóa đơn ở trạng thái Nháp. Trạng thái hiện tại: {invoice.Status}");

            var oldStatus = invoice.Status;
            invoice.Status = "Pending";
            invoice.SubmittedBy = userId;
            invoice.SubmittedAt = DateTime.UtcNow;
            invoice.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Invoices.Update(invoice);

            await _unitOfWork.InvoiceAuditLogs.AddAsync(new InvoiceAuditLog
            {
                AuditId = Guid.NewGuid(),
                InvoiceId = invoiceId,
                UserId = userId,
                UserEmail = userEmail,
                UserRole = userRole,
                Action = "SUBMIT",
                Changes = new List<AuditChange>
                {
                    new() { Field = "Status", OldValue = oldStatus, NewValue = "Pending", ChangeType = "UPDATE" }
                },
                Comment = comment,
                IpAddress = ipAddress,
                CreatedAt = DateTime.UtcNow
            });

            await _unitOfWork.CompleteAsync();
        }

        // ════════════════════════════════════════════
        //  WORKFLOW: Approve → Approved
        // ════════════════════════════════════════════

        public async Task ApproveInvoiceAsync(Guid invoiceId, Guid companyId, Guid userId, string userEmail, string userRole, string? comment, string? ipAddress)
        {
            if (userRole != "CompanyAdmin" && userRole != "SuperAdmin")
                throw new UnauthorizedAccessException("Chỉ Admin mới có quyền duyệt hóa đơn.");

            var invoice = await _unitOfWork.Invoices.GetByIdAsync(invoiceId);
            if (invoice == null)
                throw new KeyNotFoundException("Không tìm thấy hóa đơn.");
            if (invoice.CompanyId != companyId)
                throw new UnauthorizedAccessException("Không có quyền truy cập hóa đơn này.");
            if (invoice.Status != "Pending")
                throw new InvalidOperationException($"Chỉ có thể duyệt hóa đơn ở trạng thái Chờ duyệt. Trạng thái hiện tại: {invoice.Status}");

            var oldStatus = invoice.Status;
            invoice.Status = "Approved";
            invoice.ApprovedBy = userId;
            invoice.ApprovedAt = DateTime.UtcNow;
            invoice.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Invoices.Update(invoice);

            await _unitOfWork.InvoiceAuditLogs.AddAsync(new InvoiceAuditLog
            {
                AuditId = Guid.NewGuid(),
                InvoiceId = invoiceId,
                UserId = userId,
                UserEmail = userEmail,
                UserRole = userRole,
                Action = "APPROVE",
                Changes = new List<AuditChange>
                {
                    new() { Field = "Status", OldValue = oldStatus, NewValue = "Approved", ChangeType = "UPDATE" }
                },
                Comment = comment,
                IpAddress = ipAddress,
                CreatedAt = DateTime.UtcNow
            });

            await _unitOfWork.CompleteAsync();
        }

        // ════════════════════════════════════════════
        //  WORKFLOW: Reject → Rejected
        // ════════════════════════════════════════════

        public async Task RejectInvoiceAsync(Guid invoiceId, Guid companyId, Guid userId, string userEmail, string userRole, string reason, string? comment, string? ipAddress)
        {
            if (userRole != "CompanyAdmin" && userRole != "SuperAdmin")
                throw new UnauthorizedAccessException("Chỉ Admin mới có quyền từ chối hóa đơn.");

            var invoice = await _unitOfWork.Invoices.GetByIdAsync(invoiceId);
            if (invoice == null)
                throw new KeyNotFoundException("Không tìm thấy hóa đơn.");
            if (invoice.CompanyId != companyId)
                throw new UnauthorizedAccessException("Không có quyền truy cập hóa đơn này.");
            if (invoice.Status != "Pending")
                throw new InvalidOperationException($"Chỉ có thể từ chối hóa đơn ở trạng thái Chờ duyệt. Trạng thái hiện tại: {invoice.Status}");

            var oldStatus = invoice.Status;
            invoice.Status = "Rejected";
            invoice.RejectedBy = userId;
            invoice.RejectedAt = DateTime.UtcNow;
            invoice.RejectionReason = reason;
            invoice.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Invoices.Update(invoice);

            await _unitOfWork.InvoiceAuditLogs.AddAsync(new InvoiceAuditLog
            {
                AuditId = Guid.NewGuid(),
                InvoiceId = invoiceId,
                UserId = userId,
                UserEmail = userEmail,
                UserRole = userRole,
                Action = "REJECT",
                Reason = reason,
                Changes = new List<AuditChange>
                {
                    new() { Field = "Status", OldValue = oldStatus, NewValue = "Rejected", ChangeType = "UPDATE" }
                },
                Comment = comment,
                IpAddress = ipAddress,
                CreatedAt = DateTime.UtcNow
            });

            await _unitOfWork.CompleteAsync();
        }

        // ════════════════════════════════════════════
        //  MAPPING HELPER
        // ════════════════════════════════════════════

        private static InvoiceDetailDto MapToDetailDto(Invoice i)
        {
            return new InvoiceDetailDto
            {
                InvoiceId = i.InvoiceId,
                InvoiceNumber = i.InvoiceNumber,
                SerialNumber = i.SerialNumber,
                FormNumber = i.FormNumber,
                InvoiceDate = i.InvoiceDate,
                Status = i.Status,
                RiskLevel = i.RiskLevel,
                ProcessingMethod = i.ProcessingMethod,
                InvoiceCurrency = i.InvoiceCurrency,
                ExchangeRate = i.ExchangeRate,
                MCCQT = i.MCCQT,

                SellerName = i.SellerName,
                SellerTaxCode = i.SellerTaxCode,
                SellerAddress = i.SellerAddress,
                SellerBankAccount = i.SellerBankAccount,
                SellerBankName = i.SellerBankName,

                BuyerName = i.BuyerName,
                BuyerTaxCode = i.BuyerTaxCode,
                BuyerAddress = i.BuyerAddress,

                TotalAmountBeforeTax = i.TotalAmountBeforeTax,
                TotalTaxAmount = i.TotalTaxAmount,
                TotalAmount = i.TotalAmount,
                TotalAmountInWords = i.TotalAmountInWords,

                PaymentMethod = i.PaymentMethod,
                Notes = i.Notes,

                UploadedByName = i.Uploader?.FullName ?? "N/A",
                CreatedAt = i.CreatedAt,
                SubmittedByName = i.Submitter?.FullName,
                SubmittedAt = i.SubmittedAt,
                ApprovedByName = i.Approver?.FullName,
                ApprovedAt = i.ApprovedAt,
                RejectedByName = i.Rejector?.FullName,
                RejectedAt = i.RejectedAt,
                RejectionReason = i.RejectionReason,

                RiskReasons = i.RiskReasons,

                LineItems = i.InvoiceLineItems?.OrderBy(l => l.LineNumber).Select(l => new LineItemDto
                {
                    LineNumber = l.LineNumber,
                    ItemName = l.ItemName,
                    Unit = l.Unit,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    TotalAmount = l.TotalAmount,
                    VatRate = l.VatRate,
                    VatAmount = l.VatAmount
                }).ToList() ?? new(),

                ValidationLayers = i.ValidationLayers?.OrderBy(v => v.LayerOrder).Select(v => new ValidationLayerDto
                {
                    LayerName = v.LayerName,
                    LayerOrder = v.LayerOrder,
                    IsValid = v.IsValid,
                    ValidationStatus = v.ValidationStatus,
                    ErrorDetails = v.ErrorDetails,
                    CheckedAt = v.CheckedAt
                }).ToList() ?? new(),

                RiskChecks = i.RiskCheckResults?.Select(r => new RiskCheckDto
                {
                    CheckType = r.CheckType,
                    CheckStatus = r.CheckStatus,
                    RiskLevel = r.RiskLevel,
                    ErrorMessage = r.ErrorMessage,
                    Suggestion = r.Suggestion,
                    CheckDetails = r.CheckDetails,
                    CheckedAt = r.CheckedAt
                }).ToList() ?? new(),

                AuditLogs = i.AuditLogs?.OrderByDescending(a => a.CreatedAt).Select(a => new InvoiceAuditLogDto
                {
                    AuditId = a.AuditId,
                    UserEmail = a.UserEmail,
                    UserRole = a.UserRole,
                    UserFullName = a.User != null ? a.User.FullName : null,
                    IpAddress = a.IpAddress,
                    Action = a.Action,
                    CreatedAt = a.CreatedAt,
                    Changes = a.Changes,
                    Reason = a.Reason,
                    Comment = a.Comment
                }).ToList() ?? new()
            };
        }
        public async Task<ValidationResultDto> ProcessInvoiceXmlAsync(string s3Key, string userId, string companyId)
        {
            if (string.IsNullOrEmpty(s3Key))
            {
                throw new ArgumentException("S3Key is required.");
            }

            string? tempFilePath = null;
            var UserId = Guid.Parse(userId);
            var CompanyId = Guid.Parse(companyId);
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
                var logicResult = await _invoiceProcessor.ValidateBusinessLogicAsync(xmlDoc, CompanyId);

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

                // --- KIỂM TRA LỖI NGHIÊM TRỌNG: Không lưu DB nếu là trùng lặp hoặc lỗi quyền sở hữu ---
                var hasFatalError = finalResult.Errors.Any(e =>
                    e.Contains("[RỦI RO TRÙNG LẶP]") ||
                    e.Contains("[LỖI QUYỀN SỞ HỮU]"));

                if (hasFatalError)
                {
                    return finalResult; // Trả kết quả ngay, KHÔNG lưu vào Database
                }

                // --- LƯU VÀO DATABASE ---

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

                    Status = isInvoiceValid
                        ? (finalResult.Warnings.Any() ? "Draft" : "Draft")
                        : "Rejected",
                    RiskLevel = isInvoiceValid
                        ? (finalResult.Warnings.Any() ? "Yellow" : "Green")
                        : "Red",
                    Notes = isInvoiceValid
                        ? (finalResult.Warnings.Any() ? "Hóa đơn có cảnh báo, cần xem xét" : null)
                        : "Hóa đơn có lỗi, cần kiểm tra lại",

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

                string GetLayerStatus(bool isValid, List<string>? warnings) =>
                    !isValid ? "Fail" : (warnings != null && warnings.Any()) ? "Warning" : "Pass";

                invoice.ValidationLayers.Add(new ValidationLayer
                {
                    LayerId = Guid.NewGuid(),
                    InvoiceId = invoiceId,
                    LayerName = "Structure",
                    LayerOrder = 1,
                    IsValid = structResult.IsValid,
                    ValidationStatus = GetLayerStatus(structResult.IsValid, structResult.Warnings),
                    ErrorDetails = GetErrorStr(structResult.Errors)
                });

                invoice.ValidationLayers.Add(new ValidationLayer
                {
                    LayerId = Guid.NewGuid(),
                    InvoiceId = invoiceId,
                    LayerName = "Signature",
                    LayerOrder = 2,
                    IsValid = sigResult.IsValid,
                    ValidationStatus = GetLayerStatus(sigResult.IsValid, sigResult.Warnings),
                    ErrorDetails = GetErrorStr(sigResult.Errors)
                });

                invoice.ValidationLayers.Add(new ValidationLayer
                {
                    LayerId = Guid.NewGuid(),
                    InvoiceId = invoiceId,
                    LayerName = "BusinessLogic",
                    LayerOrder = 3,
                    IsValid = logicResult.IsValid,
                    ValidationStatus = GetLayerStatus(logicResult.IsValid, logicResult.Warnings),
                    ErrorDetails = GetErrorStr(logicResult.Errors)
                });

                // 5. Tạo RiskCheckResult
                var checkStatus = isInvoiceValid
                    ? (finalResult.Warnings.Any() ? "WARNING" : "PASS")
                    : "FAIL";
                invoice.RiskCheckResults.Add(new RiskCheckResult
                {
                    CheckId = Guid.NewGuid(),
                    InvoiceId = invoiceId,
                    CheckType = "AUTO_UPLOAD_VALIDATION",
                    CheckStatus = checkStatus,
                    RiskLevel = invoice.RiskLevel,
                    CheckDetails = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        Errors = finalResult.Errors,
                        Warnings = finalResult.Warnings
                    })
                });

                // 6. Tạo Audit Log ghi nhận hành động Upload
                var uploadUser = await _unitOfWork.Users.GetByIdAsync(UserId);
                invoice.AuditLogs.Add(new InvoiceAuditLog
                {
                    AuditId = Guid.NewGuid(),
                    InvoiceId = invoiceId,
                    UserId = UserId,
                    UserEmail = uploadUser?.Email,
                    UserRole = uploadUser?.Role,
                    Action = "UPLOAD",
                    Changes = new List<AuditChange>
                    {
                        new() { Field = "Status", OldValue = null, NewValue = isInvoiceValid ? "Draft" : "Rejected", ChangeType = "INSERT" },
                        new() { Field = "RiskLevel", OldValue = null, NewValue = invoice.RiskLevel, ChangeType = "INSERT" }
                    },
                    Comment = isInvoiceValid ? "Tải lên hóa đơn hợp lệ." : "Tải lên hóa đơn không hợp lệ."
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
