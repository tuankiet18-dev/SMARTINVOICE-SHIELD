using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using SmartInvoice.API.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartInvoice.API.DTOs.Invoice;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Entities.JsonModels;
using SmartInvoice.API.Repositories.Interfaces;
using SmartInvoice.API.Services.Interfaces;
using SmartInvoice.API.Constants;
using SmartInvoice.API.Enums;

namespace SmartInvoice.API.Services.Implementations
{
    public class InvoiceService : IInvoiceService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly StorageService _storageService;
        private readonly IInvoiceProcessorService _invoiceProcessor;
        private readonly IConfiguration _configuration;
        private readonly ILogger<InvoiceService> _logger;
        private readonly ISqsMessagePublisher _sqsPublisher;

        public InvoiceService(IUnitOfWork unitOfWork, StorageService storageService, IInvoiceProcessorService invoiceProcessor, IConfiguration configuration, ILogger<InvoiceService> logger, ISqsMessagePublisher sqsPublisher)
        {
            _unitOfWork = unitOfWork;
            _storageService = storageService;
            _invoiceProcessor = invoiceProcessor;
            _configuration = configuration;
            _logger = logger;
            _sqsPublisher = sqsPublisher;
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
            if (userRole == "Member" && invoice.Workflow.UploadedBy != userId) return null;

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
            if (existingInvoice.Status == nameof(InvoiceStatus.Rejected))
            {
                existingInvoice.Status = "Draft";
                existingInvoice.Workflow.RejectedBy = null;
                existingInvoice.Workflow.RejectedAt = null;
                existingInvoice.Workflow.RejectionReason = null;
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
                SellerName = i.Seller.Name,
                SellerTaxCode = i.Seller.TaxCode,
                TotalAmount = i.TotalAmount,
                InvoiceCurrency = i.InvoiceCurrency,
                Status = i.Status,
                RiskLevel = i.RiskLevel,
                ProcessingMethod = i.ProcessingMethod,
                UploadedByName = i.Workflow.Uploader?.FullName ?? "Unknown"
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
            _logger?.LogInformation("SubmitInvoiceAsync called for {InvoiceId} by user {UserId}", invoiceId, userId);
            var invoice = await _unitOfWork.Invoices.GetByIdAsync(invoiceId);
            if (invoice == null)
                throw new KeyNotFoundException("Không tìm thấy hóa đơn.");
            if (invoice.CompanyId != companyId)
                throw new UnauthorizedAccessException("Không có quyền truy cập hóa đơn này.");
            if (invoice.Status != "Draft")
                throw new InvalidOperationException($"Chỉ có thể gửi duyệt hóa đơn ở trạng thái Nháp. Trạng thái hiện tại: {invoice.Status}");

            var oldStatus = invoice.Status;
            invoice.Status = "Pending";
            invoice.Workflow.SubmittedBy = userId;
            invoice.Workflow.SubmittedAt = DateTime.UtcNow;
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
            _logger?.LogInformation("Invoice {InvoiceId} status changed to Pending", invoiceId);
        }

        // ════════════════════════════════════════════
        //  WORKFLOW: SubmitBatch → All Pending
        // ════════════════════════════════════════════

        public async Task<BatchSubmitResultDto> SubmitBatchAsync(List<Guid> invoiceIds, Guid companyId, Guid userId, string userEmail, string userRole, string? comment, string? ipAddress)
        {
            var batchResult = new BatchSubmitResultDto();
            _logger?.LogInformation("Start SubmitBatchAsync for {Count} invoices by user {UserId}", invoiceIds.Count, userId);

            foreach (var invoiceId in invoiceIds)
            {
                try
                {
                    var invoice = await _unitOfWork.Invoices.GetByIdAsync(invoiceId);
                    if (invoice == null)
                        throw new KeyNotFoundException($"Không tìm thấy hóa đơn ID: {invoiceId}");
                    if (invoice.CompanyId != companyId)
                        throw new UnauthorizedAccessException("Không có quyền truy cập hóa đơn này.");
                    if (invoice.Status != nameof(InvoiceStatus.Draft))
                        throw new InvalidOperationException($"Hóa đơn không ở trạng thái Nháp (Status hiện tại: {invoice.Status}).");

                    var oldStatus = invoice.Status;
                    invoice.Status = "Pending";
                    invoice.Workflow.SubmittedBy = userId;
                    invoice.Workflow.SubmittedAt = DateTime.UtcNow;
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

                    _logger?.LogInformation("Submitted invoice {InvoiceId} to Pending", invoiceId);

                    batchResult.SuccessCount++;
                    batchResult.Results.Add(new BatchSubmitItemResult { InvoiceId = invoiceId, Success = true });
                }
                catch (Exception ex)
                {
                    batchResult.FailCount++;
                    batchResult.Results.Add(new BatchSubmitItemResult { InvoiceId = invoiceId, Success = false, ErrorMessage = ex.Message });
                    _logger?.LogInformation("Failed to submit invoice {InvoiceId}: {Error}", invoiceId, ex.Message);
                }
            }

            return batchResult;
        }

        // ════════════════════════════════════════════
        //  WORKFLOW: Approve → Approved
        // ════════════════════════════════════════════

        public async Task ApproveInvoiceAsync(Guid invoiceId, Guid companyId, Guid userId, string userEmail, string userRole, string? comment, string? ipAddress)
        {
            if (userRole != "CompanyAdmin" && userRole != "SuperAdmin")
                throw new UnauthorizedAccessException("Chỉ Admin mới có quyền duyệt hóa đơn.");

            _logger?.LogInformation("ApproveInvoiceAsync called for {InvoiceId} by user {UserId}", invoiceId, userId);

            var invoice = await _unitOfWork.Invoices.GetByIdAsync(invoiceId);
            if (invoice == null)
                throw new KeyNotFoundException("Không tìm thấy hóa đơn.");
            if (invoice.CompanyId != companyId)
                throw new UnauthorizedAccessException("Không có quyền truy cập hóa đơn này.");
            if (invoice.Status != "Pending")
                throw new InvalidOperationException($"Chỉ có thể duyệt hóa đơn ở trạng thái Chờ duyệt. Trạng thái hiện tại: {invoice.Status}");

            var oldStatus = invoice.Status;
            invoice.Status = "Approved";
            invoice.Workflow.ApprovedBy = userId;
            invoice.Workflow.ApprovedAt = DateTime.UtcNow;
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
            _logger?.LogInformation("Approved invoice {InvoiceId}", invoiceId);
        }

        // ════════════════════════════════════════════
        //  WORKFLOW: Reject → Rejected
        // ════════════════════════════════════════════

        public async Task RejectInvoiceAsync(Guid invoiceId, Guid companyId, Guid userId, string userEmail, string userRole, string reason, string? comment, string? ipAddress)
        {
            if (userRole != "CompanyAdmin" && userRole != "SuperAdmin")
                throw new UnauthorizedAccessException("Chỉ Admin mới có quyền từ chối hóa đơn.");

            _logger?.LogInformation("RejectInvoiceAsync called for {InvoiceId} by user {UserId} with reason {Reason}", invoiceId, userId, reason);

            var invoice = await _unitOfWork.Invoices.GetByIdAsync(invoiceId);
            if (invoice == null)
                throw new KeyNotFoundException("Không tìm thấy hóa đơn.");
            if (invoice.CompanyId != companyId)
                throw new UnauthorizedAccessException("Không có quyền truy cập hóa đơn này.");
            if (invoice.Status != "Pending")
                throw new InvalidOperationException($"Chỉ có thể từ chối hóa đơn ở trạng thái Chờ duyệt. Trạng thái hiện tại: {invoice.Status}");

            var oldStatus = invoice.Status;
            invoice.Status = "Rejected";
            invoice.Workflow.RejectedBy = userId;
            invoice.Workflow.RejectedAt = DateTime.UtcNow;
            invoice.Workflow.RejectionReason = reason;
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
            _logger?.LogInformation("Rejected invoice {InvoiceId} with reason {Reason}", invoiceId, reason);
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

                HasOriginalFile = i.OriginalFileId != null && i.OriginalFileId != Guid.Empty,
                HasVisualFile = i.VisualFileId != null,

                SellerName = i.Seller.Name,
                SellerTaxCode = i.Seller.TaxCode,
                SellerAddress = i.Seller.Address,
                SellerBankAccount = i.Seller.BankAccount,
                SellerBankName = i.Seller.BankName,

                BuyerName = i.Buyer.Name,
                BuyerTaxCode = i.Buyer.TaxCode,
                BuyerAddress = i.Buyer.Address,

                TotalAmountBeforeTax = i.TotalAmountBeforeTax,
                TotalTaxAmount = i.TotalTaxAmount,
                TotalAmount = i.TotalAmount,
                TotalAmountInWords = i.TotalAmountInWords,

                PaymentMethod = i.PaymentMethod,
                Notes = i.Notes,

                UploadedByName = i.Workflow.Uploader?.FullName ?? "N/A",
                CreatedAt = i.CreatedAt,
                SubmittedByName = i.Workflow.Submitter?.FullName,
                SubmittedAt = i.Workflow.SubmittedAt,
                ApprovedByName = i.Workflow.Approver?.FullName,
                ApprovedAt = i.Workflow.ApprovedAt,
                RejectedByName = i.Workflow.Rejector?.FullName,
                RejectedAt = i.Workflow.RejectedAt,
                RejectionReason = i.Workflow.RejectionReason,

                RiskReasons = new List<RiskReason>(), // RiskReasons is no longer stored purely on Invoice

                LineItems = i.ExtractedData?.LineItems?.Select(l => new LineItemDto
                {
                    LineNumber = l.Stt,
                    ItemName = l.ProductName,
                    Unit = l.Unit,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    TotalAmount = l.TotalAmount,
                    VatRate = l.VatRate,
                    VatAmount = l.VatAmount
                }).ToList() ?? new(),

                ValidationLayers = i.CheckResults?.Where(c => c.Category != "AUTO_UPLOAD_VALIDATION").OrderBy(v => v.CheckOrder).Select(v => new ValidationLayerDto
                {
                    LayerName = v.CheckName,
                    LayerOrder = v.CheckOrder,
                    IsValid = v.IsValid,
                    ValidationStatus = v.Status,
                    ErrorDetails = v.ErrorDetails,
                    CheckedAt = v.CheckedAt
                }).ToList() ?? new(),

                RiskChecks = i.CheckResults?.Where(c => c.Category == "AUTO_UPLOAD_VALIDATION").Select(r => new RiskCheckDto
                {
                    CheckType = r.CheckName,
                    CheckStatus = r.Status,
                    RiskLevel = i.RiskLevel,
                    ErrorMessage = r.ErrorMessage,
                    Suggestion = r.Suggestion,
                    CheckDetails = r.ErrorDetails,
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
            _logger?.LogInformation("Start ProcessInvoiceXmlAsync for S3Key={S3Key}, CompanyId={CompanyId}, UserId={UserId}", s3Key, CompanyId, UserId);
            try
            {
                // 1. Tải file từ S3 về máy chủ tạm
                tempFilePath = await _storageService.DownloadToTempFileAsync(s3Key);
                _logger?.LogInformation("Downloaded S3Key={S3Key} to temp path={TempPath}", s3Key, tempFilePath);

                // 2. Validate cấu trúc XSD
                var swStruct = Stopwatch.StartNew();
                var structResult = _invoiceProcessor.ValidateStructure(tempFilePath);
                swStruct.Stop();
                _logger?.LogInformation("Structure validation completed. IsValid={IsValid}, DurationMs={Ms}", structResult.IsValid, swStruct.ElapsedMilliseconds);

                var xmlDoc = new System.Xml.XmlDocument();
                xmlDoc.PreserveWhitespace = true;
                xmlDoc.Load(tempFilePath);

                // 3. Verify Chữ ký số
                var swSig = Stopwatch.StartNew();
                var sigResult = _invoiceProcessor.VerifyDigitalSignature(xmlDoc);
                swSig.Stop();
                _logger?.LogInformation("Signature verification completed. IsValid={IsValid}, SignerSubject={SignerSubject}, DurationMs={Ms}", sigResult.IsValid, sigResult.SignerSubject, swSig.ElapsedMilliseconds);

                // 4. Validate Logic & Business (VietQR...)
                var swLogic = Stopwatch.StartNew();
                var logicResult = await _invoiceProcessor.ValidateBusinessLogicAsync(xmlDoc, CompanyId);
                swLogic.Stop();
                _logger?.LogInformation("Business logic validation completed. IsValid={IsValid}, Errors={ErrorCount}, Warnings={WarningCount}, DurationMs={Ms}", logicResult.IsValid, logicResult.ErrorDetails?.Count ?? 0, logicResult.WarningDetails?.Count ?? 0, swLogic.ElapsedMilliseconds);

                // 5. Gộp tất cả các lỗi và cảnh báo lại thành một kết quả duy nhất
                var finalResult = new ValidationResultDto
                {
                    SignerSubject = sigResult.SignerSubject
                };

                finalResult.ErrorDetails.AddRange(structResult.ErrorDetails ?? new List<ValidationErrorDetail>());
                finalResult.ErrorDetails.AddRange(sigResult.ErrorDetails ?? new List<ValidationErrorDetail>());
                finalResult.ErrorDetails.AddRange(logicResult.ErrorDetails ?? new List<ValidationErrorDetail>());

                finalResult.WarningDetails.AddRange(structResult.WarningDetails ?? new List<ValidationErrorDetail>());
                finalResult.WarningDetails.AddRange(sigResult.WarningDetails ?? new List<ValidationErrorDetail>());
                finalResult.WarningDetails.AddRange(logicResult.WarningDetails ?? new List<ValidationErrorDetail>());

                // Sao chép thông tin versioning từ logicResult sang finalResult
                finalResult.IsReplacement = logicResult.IsReplacement;
                finalResult.ReplacedInvoiceId = logicResult.ReplacedInvoiceId;
                finalResult.NewVersion = logicResult.NewVersion;

                // Trích xuất dữ liệu
                finalResult.ExtractedData = _invoiceProcessor.ExtractData(xmlDoc, finalResult);

                // --- KIỂM TRA LỖI NGHIÊM TRỌNG: Không lưu DB nếu là trùng lặp hoặc lỗi quyền sở hữu ---
                var fatalErrorCodes = new[] { ErrorCodes.LogicDuplicate, ErrorCodes.LogicDuplicateRejected, ErrorCodes.LogicOwner };
                var hasFatalError = finalResult.ErrorDetails.Any(e =>
                    !string.IsNullOrEmpty(e.ErrorCode) && fatalErrorCodes.Contains(e.ErrorCode));

                if (hasFatalError)
                {
                    _logger?.LogInformation("Fatal validation error detected for S3Key={S3Key}; aborting save. Errors={Errors}", s3Key, System.Text.Json.JsonSerializer.Serialize(finalResult.ErrorDetails));
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

                var isInvoiceValid = finalResult.IsValid;

                // Lấy bucket name từ cấu hình (ưu tiên biến môi trường AWS_BUCKET_NAME, sau đó lấy từ appsettings.json)
                var bucketName = Environment.GetEnvironmentVariable("AWS_BUCKET_NAME")
                                 ?? _configuration["AWS:BucketName"]
                                 ?? "smartinvoice-storage-team-dat";

                _logger?.LogInformation("Preparing to persist invoice. IsValid={IsValid}, RiskLevelCandidate={RiskLevelCandidate}", isInvoiceValid, isInvoiceValid ? (finalResult.WarningDetails.Any() ? "Yellow" : "Green") : "Red");
                // 1. Tạo FileStorage cho file XML
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

                // Helper functions
                string? GetErrorStr(List<ValidationErrorDetail>? errs) => errs != null && errs.Any() ? System.Text.Json.JsonSerializer.Serialize(errs) : null;
                string GetLayerStatus(bool valid, List<ValidationErrorDetail>? warnings) =>
                    !valid ? "Fail" : (warnings != null && warnings.Any()) ? "Warning" : "Pass";

                // ============================================================
                // CASE 3A: INVOICE DOSSIER — XML OVERRIDES EXISTING OCR RECORD
                // ============================================================
                if (logicResult.MergeMode == DossierMergeMode.XmlOverridesOcr && logicResult.MergeTargetInvoiceId.HasValue)
                {
                    var existingInvoice = await _unitOfWork.Invoices.GetByIdAsync(logicResult.MergeTargetInvoiceId.Value);
                    if (existingInvoice == null)
                    {
                        finalResult.AddError("ERR_MERGE_FAILED", "Không tìm thấy hóa đơn OCR gốc để gộp dữ liệu XML.");
                        return finalResult;
                    }

                    // Keep the old OCR file as VisualFile
                    if (existingInvoice.OriginalFileId.HasValue && existingInvoice.VisualFileId == null)
                    {
                        existingInvoice.VisualFileId = existingInvoice.OriginalFileId;
                    }

                    // Set the new XML file as the OriginalFile (Source of Truth)
                    existingInvoice.OriginalFileId = fileStorage.FileId;
                    existingInvoice.ProcessingMethod = "XML";

                    // Override ALL data fields with XML-extracted data
                    existingInvoice.DocumentTypeId = docTypeId;
                    existingInvoice.InvoiceNumber = finalResult.ExtractedData?.InvoiceNumber ?? existingInvoice.InvoiceNumber;
                    existingInvoice.FormNumber = finalResult.ExtractedData?.InvoiceTemplateCode;
                    existingInvoice.SerialNumber = finalResult.ExtractedData?.InvoiceSymbol;
                    existingInvoice.InvoiceDate = finalResult.ExtractedData?.InvoiceDate != null
                                      ? DateTime.SpecifyKind(finalResult.ExtractedData.InvoiceDate.Value, DateTimeKind.Utc)
                                      : existingInvoice.InvoiceDate;
                    existingInvoice.InvoiceCurrency = finalResult.ExtractedData?.InvoiceCurrency ?? "VND";
                    existingInvoice.ExchangeRate = finalResult.ExtractedData?.ExchangeRate ?? 1;

                    existingInvoice.Seller = new SellerInfo
                    {
                        Name = finalResult.ExtractedData?.SellerName,
                        TaxCode = finalResult.ExtractedData?.SellerTaxCode,
                        Address = finalResult.ExtractedData?.SellerAddress,
                        Phone = finalResult.ExtractedData?.SellerPhone,
                        Email = finalResult.ExtractedData?.SellerEmail,
                        BankAccount = finalResult.ExtractedData?.SellerBankAccount,
                        BankName = finalResult.ExtractedData?.SellerBankName
                    };
                    existingInvoice.Buyer = new BuyerInfo
                    {
                        Name = finalResult.ExtractedData?.BuyerName,
                        TaxCode = finalResult.ExtractedData?.BuyerTaxCode,
                        Address = finalResult.ExtractedData?.BuyerAddress,
                        Phone = finalResult.ExtractedData?.BuyerPhone,
                        Email = finalResult.ExtractedData?.BuyerEmail,
                        ContactPerson = finalResult.ExtractedData?.BuyerContactPerson
                    };

                    existingInvoice.TotalAmountBeforeTax = finalResult.ExtractedData?.TotalPreTax;
                    existingInvoice.TotalTaxAmount = finalResult.ExtractedData?.TotalTaxAmount;
                    existingInvoice.TotalAmount = finalResult.ExtractedData?.TotalAmount ?? 0;
                    existingInvoice.TotalAmountInWords = finalResult.ExtractedData?.TotalAmountInWords;
                    existingInvoice.PaymentMethod = finalResult.ExtractedData?.PaymentTerms;
                    existingInvoice.MCCQT = finalResult.ExtractedData?.MCCQT;
                    existingInvoice.ExtractedData = finalResult.ExtractedData;
                    existingInvoice.RawData = new InvoiceRawData { ObjectKey = s3Key };

                    // Recalculate status & risk — XML is Source of Truth so can be Green
                    existingInvoice.Status = isInvoiceValid ? "Draft" : "Rejected";
                    existingInvoice.RiskLevel = isInvoiceValid ? (finalResult.WarningDetails.Any() ? "Yellow" : "Green") : "Red";
                    existingInvoice.Notes = isInvoiceValid ? (finalResult.WarningDetails.Any() ? "Hóa đơn có cảnh báo, cần xem xét" : null) : "Hóa đơn có lỗi, cần kiểm tra lại";
                    existingInvoice.Version += 1;
                    existingInvoice.UpdatedAt = DateTime.UtcNow;

                    // Save invoice updates + new FileStorage first
                    await _unitOfWork.FileStorages.AddAsync(fileStorage);
                    await _unitOfWork.CompleteAsync();
                    _logger?.LogInformation("Merged XML into existing invoice {InvoiceId}. Updated risk={RiskLevel}", existingInvoice.InvoiceId, existingInvoice.RiskLevel);

                    // Delete old CheckResults via raw SQL (avoid EF tracking conflicts)
                    await _unitOfWork.ExecuteSqlAsync(
                        $"DELETE FROM \"InvoiceCheckResults\" WHERE \"InvoiceId\" = '{existingInvoice.InvoiceId}'");

                    // Add new CheckResults directly
                    var newCheckResults = new List<InvoiceCheckResult>();

                    var structErrInfoM = (structResult.ErrorDetails ?? System.Linq.Enumerable.Empty<ValidationErrorDetail>()).FirstOrDefault() ?? (structResult.WarningDetails ?? System.Linq.Enumerable.Empty<ValidationErrorDetail>()).FirstOrDefault();
                    newCheckResults.Add(new InvoiceCheckResult
                    {
                        CheckId = Guid.NewGuid(), InvoiceId = existingInvoice.InvoiceId,
                        Category = "STRUCTURE", CheckName = "Structure", CheckOrder = 1,
                        IsValid = structResult.IsValid,
                        Status = GetLayerStatus(structResult.IsValid, structResult.WarningDetails),
                        ErrorCode = structErrInfoM?.ErrorCode, ErrorMessage = structErrInfoM?.ErrorMessage,
                        Suggestion = structErrInfoM?.Suggestion,
                        ErrorDetails = GetErrorStr(structResult.ErrorDetails),
                        DurationMs = (int)swStruct.ElapsedMilliseconds
                    });

                    var sigErrInfoM = (sigResult.ErrorDetails ?? System.Linq.Enumerable.Empty<ValidationErrorDetail>()).FirstOrDefault() ?? (sigResult.WarningDetails ?? System.Linq.Enumerable.Empty<ValidationErrorDetail>()).FirstOrDefault();
                    newCheckResults.Add(new InvoiceCheckResult
                    {
                        CheckId = Guid.NewGuid(), InvoiceId = existingInvoice.InvoiceId,
                        Category = "SIGNATURE", CheckName = "Signature", CheckOrder = 2,
                        IsValid = sigResult.IsValid,
                        Status = GetLayerStatus(sigResult.IsValid, sigResult.WarningDetails),
                        ErrorCode = sigErrInfoM?.ErrorCode, ErrorMessage = sigErrInfoM?.ErrorMessage,
                        Suggestion = sigErrInfoM?.Suggestion,
                        ErrorDetails = GetErrorStr(sigResult.ErrorDetails),
                        AdditionalData = sigResult.SignerSubject != null ? System.Text.Json.JsonSerializer.Serialize(new { SignerSubject = sigResult.SignerSubject }) : null,
                        DurationMs = (int)swSig.ElapsedMilliseconds
                    });

                    var logicErrInfoM = (logicResult.ErrorDetails ?? System.Linq.Enumerable.Empty<ValidationErrorDetail>()).FirstOrDefault() ?? (logicResult.WarningDetails ?? System.Linq.Enumerable.Empty<ValidationErrorDetail>()).FirstOrDefault();
                    newCheckResults.Add(new InvoiceCheckResult
                    {
                        CheckId = Guid.NewGuid(), InvoiceId = existingInvoice.InvoiceId,
                        Category = "BUSINESS_LOGIC", CheckName = "BusinessLogic", CheckOrder = 3,
                        IsValid = logicResult.IsValid,
                        Status = GetLayerStatus(logicResult.IsValid, logicResult.WarningDetails),
                        ErrorCode = logicErrInfoM?.ErrorCode, ErrorMessage = logicErrInfoM?.ErrorMessage,
                        Suggestion = logicErrInfoM?.Suggestion,
                        ErrorDetails = GetErrorStr(logicResult.ErrorDetails),
                        DurationMs = (int)swLogic.ElapsedMilliseconds
                    });

                    var checkStatusM = isInvoiceValid ? (finalResult.WarningDetails.Any() ? "WARNING" : "PASS") : "FAIL";
                    var priorityErrorM = finalResult.ErrorDetails.FirstOrDefault();
                    var priorityWarningM = finalResult.WarningDetails.FirstOrDefault();
                    newCheckResults.Add(new InvoiceCheckResult
                    {
                        CheckId = Guid.NewGuid(), InvoiceId = existingInvoice.InvoiceId,
                        Category = "AUTO_UPLOAD_VALIDATION", CheckName = "AUTO_UPLOAD_VALIDATION", CheckOrder = 4,
                        IsValid = isInvoiceValid, Status = checkStatusM,
                        ErrorCode = priorityErrorM?.ErrorCode ?? priorityWarningM?.ErrorCode,
                        ErrorMessage = priorityErrorM?.ErrorMessage ?? priorityWarningM?.ErrorMessage,
                        Suggestion = priorityErrorM?.Suggestion ?? priorityWarningM?.Suggestion,
                        DurationMs = (int)(swStruct.ElapsedMilliseconds + swSig.ElapsedMilliseconds + swLogic.ElapsedMilliseconds),
                        ErrorDetails = System.Text.Json.JsonSerializer.Serialize(new { ErrorDetails = finalResult.ErrorDetails, WarningDetails = finalResult.WarningDetails })
                    });

                    foreach (var cr in newCheckResults)
                        await _unitOfWork.InvoiceCheckResults.AddAsync(cr);

                    // Audit Log
                    var mergeUser = await _unitOfWork.Users.GetByIdAsync(UserId);
                    await _unitOfWork.InvoiceAuditLogs.AddAsync(new InvoiceAuditLog
                    {
                        AuditId = Guid.NewGuid(),
                        InvoiceId = existingInvoice.InvoiceId,
                        UserId = UserId,
                        UserEmail = mergeUser?.Email,
                        UserRole = mergeUser?.Role,
                        Action = "MERGE_XML_OVERRIDE",
                        Changes = new List<AuditChange>
                        {
                            new() { Field = "ProcessingMethod", OldValue = "API", NewValue = "XML", ChangeType = "UPDATE" },
                            new() { Field = "RiskLevel", OldValue = "Yellow", NewValue = existingInvoice.RiskLevel, ChangeType = "UPDATE" },
                            new() { Field = "OriginalFileId", OldValue = null, NewValue = fileStorage.FileId.ToString(), ChangeType = "UPDATE" }
                        },
                        Comment = "Đã đính kèm bản gốc XML. Dữ liệu đã được cập nhật theo bản gốc và xác thực chữ ký số."
                    });

                    await _unitOfWork.CompleteAsync();
                    _logger?.LogInformation("Merge complete for target invoice {InvoiceId}", existingInvoice.InvoiceId);
                    finalResult.InvoiceId = existingInvoice.InvoiceId;
                    return finalResult;
                }

                // ============================================================
                // NORMAL FLOW: CREATE A NEW INVOICE RECORD
                // ============================================================
                var invoiceId = Guid.NewGuid();

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
                    Seller = new SellerInfo
                    {
                        Name = finalResult.ExtractedData?.SellerName,
                        TaxCode = finalResult.ExtractedData?.SellerTaxCode,
                        Address = finalResult.ExtractedData?.SellerAddress,
                        Phone = finalResult.ExtractedData?.SellerPhone,
                        Email = finalResult.ExtractedData?.SellerEmail,
                        BankAccount = finalResult.ExtractedData?.SellerBankAccount,
                        BankName = finalResult.ExtractedData?.SellerBankName
                    },

                    Buyer = new BuyerInfo
                    {
                        Name = finalResult.ExtractedData?.BuyerName,
                        TaxCode = finalResult.ExtractedData?.BuyerTaxCode,
                        Address = finalResult.ExtractedData?.BuyerAddress,
                        Phone = finalResult.ExtractedData?.BuyerPhone,
                        Email = finalResult.ExtractedData?.BuyerEmail,
                        ContactPerson = finalResult.ExtractedData?.BuyerContactPerson
                    },

                    TotalAmountBeforeTax = finalResult.ExtractedData?.TotalPreTax,
                    TotalTaxAmount = finalResult.ExtractedData?.TotalTaxAmount,
                    TotalAmount = finalResult.ExtractedData?.TotalAmount ?? 0,
                    TotalAmountInWords = finalResult.ExtractedData?.TotalAmountInWords,

                    PaymentMethod = finalResult.ExtractedData?.PaymentTerms,
                    MCCQT = finalResult.ExtractedData?.MCCQT,
                    RawData = new InvoiceRawData { ObjectKey = s3Key },
                    ExtractedData = finalResult.ExtractedData,

                    Status = isInvoiceValid
                        ? (finalResult.WarningDetails.Any() ? "Draft" : "Draft")
                        : "Rejected",
                    RiskLevel = isInvoiceValid
                        ? (finalResult.WarningDetails.Any() ? "Yellow" : "Green")
                        : "Red",
                    Notes = isInvoiceValid
                        ? (finalResult.WarningDetails.Any() ? "Hóa đơn có cảnh báo, cần xem xét" : null)
                        : "Hóa đơn có lỗi, cần kiểm tra lại",

                    Version = finalResult.NewVersion,

                    Workflow = new InvoiceWorkflow
                    {
                        UploadedBy = UserId
                    },
                    CreatedAt = DateTime.UtcNow
                };

                if (finalResult.IsReplacement && finalResult.ReplacedInvoiceId.HasValue)
                {
                    invoice.ReplacedBy = finalResult.ReplacedInvoiceId;

                    var oldInvoice = await _unitOfWork.Invoices.GetByIdAsync(finalResult.ReplacedInvoiceId.Value);
                    if (oldInvoice != null)
                    {
                        oldInvoice.IsReplaced = true;
                        oldInvoice.ReplacedBy = invoiceId;
                        _unitOfWork.Invoices.Update(oldInvoice);
                    }
                }

                // 3. (Removed) InvoiceLineItems creation since it's now handled by the ExtractedData JSONB field.

                // 4. Tạo InvoiceCheckResult cho 3 bước kiểm tra (Category = "STRUCTURE", "SIGNATURE", "BUSINESS_LOGIC")
                var structErrInfo = (structResult.ErrorDetails ?? System.Linq.Enumerable.Empty<ValidationErrorDetail>()).FirstOrDefault() ?? (structResult.WarningDetails ?? System.Linq.Enumerable.Empty<ValidationErrorDetail>()).FirstOrDefault();
                invoice.CheckResults.Add(new InvoiceCheckResult
                {
                    CheckId = Guid.NewGuid(),
                    InvoiceId = invoiceId,
                    Category = "STRUCTURE",
                    CheckName = "Structure",
                    CheckOrder = 1,
                    IsValid = structResult.IsValid,
                    Status = GetLayerStatus(structResult.IsValid, structResult.WarningDetails),
                    ErrorCode = structErrInfo?.ErrorCode,
                    ErrorMessage = structErrInfo?.ErrorMessage,
                    Suggestion = structErrInfo?.Suggestion,
                    ErrorDetails = GetErrorStr(structResult.ErrorDetails),
                    DurationMs = (int)swStruct.ElapsedMilliseconds
                });

                var sigErrInfo = (sigResult.ErrorDetails ?? System.Linq.Enumerable.Empty<ValidationErrorDetail>()).FirstOrDefault() ?? (sigResult.WarningDetails ?? System.Linq.Enumerable.Empty<ValidationErrorDetail>()).FirstOrDefault();
                invoice.CheckResults.Add(new InvoiceCheckResult
                {
                    CheckId = Guid.NewGuid(),
                    InvoiceId = invoiceId,
                    Category = "SIGNATURE",
                    CheckName = "Signature",
                    CheckOrder = 2,
                    IsValid = sigResult.IsValid,
                    Status = GetLayerStatus(sigResult.IsValid, sigResult.WarningDetails),
                    ErrorCode = sigErrInfo?.ErrorCode,
                    ErrorMessage = sigErrInfo?.ErrorMessage,
                    Suggestion = sigErrInfo?.Suggestion,
                    ErrorDetails = GetErrorStr(sigResult.ErrorDetails),
                    AdditionalData = sigResult.SignerSubject != null ? System.Text.Json.JsonSerializer.Serialize(new { SignerSubject = sigResult.SignerSubject }) : null,
                    DurationMs = (int)swSig.ElapsedMilliseconds
                });

                var logicErrInfo = (logicResult.ErrorDetails ?? System.Linq.Enumerable.Empty<ValidationErrorDetail>()).FirstOrDefault() ?? (logicResult.WarningDetails ?? System.Linq.Enumerable.Empty<ValidationErrorDetail>()).FirstOrDefault();
                invoice.CheckResults.Add(new InvoiceCheckResult
                {
                    CheckId = Guid.NewGuid(),
                    InvoiceId = invoiceId,
                    Category = "BUSINESS_LOGIC",
                    CheckName = "BusinessLogic",
                    CheckOrder = 3,
                    IsValid = logicResult.IsValid,
                    Status = GetLayerStatus(logicResult.IsValid, logicResult.WarningDetails),
                    ErrorCode = logicErrInfo?.ErrorCode,
                    ErrorMessage = logicErrInfo?.ErrorMessage,
                    Suggestion = logicErrInfo?.Suggestion,
                    ErrorDetails = GetErrorStr(logicResult.ErrorDetails),
                    DurationMs = (int)swLogic.ElapsedMilliseconds
                });

                // 5. Tạo RiskCheckResult (Category = "AUTO_UPLOAD_VALIDATION")
                var checkStatus = isInvoiceValid
                    ? (finalResult.WarningDetails.Any() ? "WARNING" : "PASS")
                    : "FAIL";

                var priorityError = finalResult.ErrorDetails.FirstOrDefault();
                var priorityWarning = finalResult.WarningDetails.FirstOrDefault();
                var riskCheckErrorCode = priorityError?.ErrorCode ?? priorityWarning?.ErrorCode;
                var riskCheckErrorMessage = priorityError?.ErrorMessage ?? priorityWarning?.ErrorMessage;
                var riskCheckSuggestion = priorityError?.Suggestion ?? priorityWarning?.Suggestion;

                invoice.CheckResults.Add(new InvoiceCheckResult
                {
                    CheckId = Guid.NewGuid(),
                    InvoiceId = invoiceId,
                    Category = "AUTO_UPLOAD_VALIDATION",
                    CheckName = "AUTO_UPLOAD_VALIDATION",
                    CheckOrder = 4,
                    IsValid = isInvoiceValid,
                    Status = checkStatus,
                    ErrorCode = riskCheckErrorCode,
                    ErrorMessage = riskCheckErrorMessage,
                    Suggestion = riskCheckSuggestion,
                    DurationMs = (int)(swStruct.ElapsedMilliseconds + swSig.ElapsedMilliseconds + swLogic.ElapsedMilliseconds),
                    ErrorDetails = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        ErrorDetails = finalResult.ErrorDetails,
                        WarningDetails = finalResult.WarningDetails
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
                _logger?.LogInformation("Created new invoice {InvoiceId} from S3Key={S3Key}, RiskLevel={RiskLevel}", invoiceId, s3Key, invoice.RiskLevel);

                // Publish VietQR validation message to SQS for asynchronous processing
                try
                {
                    var sqsMessage = new SmartInvoice.API.DTOs.SQS.VietQrValidationMessage
                    {
                        InvoiceId = invoice.InvoiceId,
                        TaxCode = invoice.Seller.TaxCode,
                        SellerName = invoice.Seller.Name
                    };

                    await _sqsPublisher.PublishVietQrValidationAsync(sqsMessage, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Invoice {InvoiceId} saved successfully, but failed to publish VietQR validation message to SQS.", invoice.InvoiceId);
                }

                // Trả về invoiceId để frontend biết đây là record nào trong DB
                finalResult.InvoiceId = invoiceId;

                return finalResult;
            }
            finally
            {
                // Luôn dọn dẹp file tạm trên local server (S3 vẫn giữ nguyên file gốc)
                if (tempFilePath != null && System.IO.File.Exists(tempFilePath))
                {
                    System.IO.File.Delete(tempFilePath);
                    _logger?.LogInformation("Deleted temp file {TempPath}", tempFilePath);
                }
            }
        }

        public async Task<ValidationResultDto> ProcessInvoiceOcrAsync(ProcessOcrRequestDto request, string userId, string companyId)
        {
            if (request.OcrResult == null)
                throw new ArgumentException("OCR data is required.");

            var UserId = Guid.Parse(userId);
            var CompanyId = Guid.Parse(companyId);

            _logger?.LogInformation("Start ProcessInvoiceOcrAsync for S3Key={S3Key}, CompanyId={CompanyId}, UserId={UserId}", request.S3Key, CompanyId, UserId);

            var swLogic = Stopwatch.StartNew();
            var logicResult = await _invoiceProcessor.ValidateOcrBusinessLogicAsync(request.OcrResult, CompanyId);
            swLogic.Stop();
            _logger?.LogInformation("OCR business logic validation completed. IsValid={IsValid}, Errors={ErrorCount}, Warnings={WarningCount}, DurationMs={Ms}", logicResult.IsValid, logicResult.ErrorDetails?.Count ?? 0, logicResult.WarningDetails?.Count ?? 0, swLogic.ElapsedMilliseconds);

            var finalResult = new ValidationResultDto();
            finalResult.ErrorDetails.AddRange(logicResult.ErrorDetails ?? new List<ValidationErrorDetail>());
            finalResult.WarningDetails.AddRange(logicResult.WarningDetails ?? new List<ValidationErrorDetail>());

            finalResult.IsReplacement = logicResult.IsReplacement;
            finalResult.ReplacedInvoiceId = logicResult.ReplacedInvoiceId;
            finalResult.NewVersion = logicResult.NewVersion;
            finalResult.MergeMode = logicResult.MergeMode;
            finalResult.MergeTargetInvoiceId = logicResult.MergeTargetInvoiceId;

            finalResult.ExtractedData = _invoiceProcessor.ExtractOcrData(request.OcrResult);

            var fatalErrorCodes = new[] { ErrorCodes.LogicDuplicate, ErrorCodes.LogicDuplicateRejected, ErrorCodes.LogicOwner };
            var hasFatalError = finalResult.ErrorDetails.Any(e =>
                !string.IsNullOrEmpty(e.ErrorCode) && fatalErrorCodes.Contains(e.ErrorCode));

            if (hasFatalError)
            {
                _logger?.LogInformation("Fatal error in OCR processing for S3Key={S3Key}; aborting.", request.S3Key);
                return finalResult;
            }

            // --- Tạo FileStorage cho file OCR (Visual File) ---
            Guid? visualFileId = null;
            if (!string.IsNullOrEmpty(request.S3Key))
            {
                // Check if a FileStorage with this S3Key already exists (unique constraint)
                var existingFile = await _unitOfWork.FileStorages.FindByS3KeyAsync(request.S3Key);
                if (existingFile != null)
                {
                    visualFileId = existingFile.FileId;
                }
                else
                {
                    var bucketName = !string.IsNullOrEmpty(request.BucketName) ? request.BucketName : 
                                     (Environment.GetEnvironmentVariable("AWS_BUCKET_NAME") ?? _configuration["AWS:BucketName"] ?? "smartinvoice-storage-team-dat");
                    var fileStorage = new FileStorage
                    {
                        FileId = Guid.NewGuid(),
                        CompanyId = CompanyId,
                        UploadedBy = UserId,
                        OriginalFileName = request.S3Key.Split('/').Last(),
                        FileExtension = ".jpg",
                        FileSize = 0,
                        MimeType = "image/jpeg",
                        S3BucketName = bucketName,
                        S3Key = request.S3Key,
                        IsProcessed = true,
                        ProcessedAt = DateTime.UtcNow
                    };
                    visualFileId = fileStorage.FileId;
                    await _unitOfWork.FileStorages.AddAsync(fileStorage);
                }
            }

            // ============================================================
            // CASE 3B: INVOICE DOSSIER — OCR ATTACHES TO EXISTING XML RECORD
            // ============================================================
            if (finalResult.MergeMode == DossierMergeMode.OcrAttachesToXml && finalResult.MergeTargetInvoiceId.HasValue)
            {
                var existingInvoice = await _unitOfWork.Invoices.GetByIdAsync(finalResult.MergeTargetInvoiceId.Value);
                if (existingInvoice == null)
                {
                    finalResult.AddError("ERR_MERGE_FAILED", "Không tìm thấy hóa đơn XML gốc để đính kèm bản thể hiện.");
                    return finalResult;
                }

                // Only attach the visual file — DO NOT override any data
                existingInvoice.VisualFileId = visualFileId;
                existingInvoice.UpdatedAt = DateTime.UtcNow;

                // Audit Log — add directly to AuditLogs table (not through navigation property)
                var mergeUser = await _unitOfWork.Users.GetByIdAsync(UserId);
                var auditLog = new InvoiceAuditLog
                {
                    AuditId = Guid.NewGuid(),
                    InvoiceId = existingInvoice.InvoiceId,
                    UserId = UserId,
                    UserEmail = mergeUser?.Email,
                    UserRole = mergeUser?.Role,
                    Action = "ATTACH_VISUAL_FILE",
                    Changes = new List<AuditChange>
                    {
                        new() { Field = "VisualFileId", OldValue = null, NewValue = visualFileId?.ToString(), ChangeType = "UPDATE" }
                    },
                    Comment = "Đã đính kèm bản thể hiện PDF/Ảnh. Dữ liệu không thay đổi (giữ nguyên bản gốc XML)."
                };
                await _unitOfWork.InvoiceAuditLogs.AddAsync(auditLog);

                await _unitOfWork.CompleteAsync();

                finalResult.InvoiceId = existingInvoice.InvoiceId;
                // Clear any non-critical errors/warnings from OCR validation since we didn't use any of that data
                finalResult.ErrorDetails.Clear();
                finalResult.WarningDetails.Clear();
                return finalResult;
            }

            // ============================================================
            // NORMAL FLOW: CREATE NEW OCR-ONLY INVOICE (Yellow Risk)
            // ============================================================
            var docTypes = await _unitOfWork.DocumentTypes.GetAllAsync();
            var docTypeId = 1;
            
            var typeStr = request.OcrResult.Invoice?.Type?.Value?.ToUpper();
            if (typeStr != null && typeStr.Contains("BÁN HÀNG"))
            {
                var saleType = docTypes.FirstOrDefault(d => d.TypeCode == "SALE" || d.FormTemplate == "02GTTT");
                docTypeId = saleType?.DocumentTypeId ?? 2;
            }
            else
            {
                 var gtgtType = docTypes.FirstOrDefault(d => d.TypeCode == "GTGT" || d.FormTemplate == "01GTKT");
                 docTypeId = gtgtType?.DocumentTypeId ?? 1;
            }

            var invoiceId = Guid.NewGuid();
            var isInvoiceValid = finalResult.IsValid;

            // OCR-only: VisualFileId gets the image, OriginalFileId stays null (no XML yet)
            // Always add WARN_MISSING_XML_EVIDENCE for OCR-only uploads
            if (isInvoiceValid)
            {
                finalResult.AddWarning("WARN_MISSING_XML_EVIDENCE", 
                    "Hóa đơn được trích xuất từ ảnh/PDF bằng AI. Để đảm bảo 100% tính pháp lý khi khai thuế, bạn cần bổ sung file XML gốc.", 
                    "Tải lên file XML gốc của hóa đơn này để hệ thống xác thực chữ ký số và cập nhật dữ liệu chính xác.");
            }

            var invoice = new Invoice
            {
                InvoiceId = invoiceId,
                CompanyId = CompanyId,
                DocumentTypeId = docTypeId,
                OriginalFileId = null,           // No XML yet
                VisualFileId = visualFileId,      // OCR image/PDF goes here
                ProcessingMethod = "API",
                InvoiceNumber = finalResult.ExtractedData?.InvoiceNumber ?? "UNKNOWN",
                FormNumber = finalResult.ExtractedData?.InvoiceTemplateCode,
                SerialNumber = finalResult.ExtractedData?.InvoiceSymbol,
                InvoiceDate = finalResult.ExtractedData?.InvoiceDate != null
                                ? DateTime.SpecifyKind(finalResult.ExtractedData.InvoiceDate.Value, DateTimeKind.Utc)
                                : DateTime.UtcNow,
                InvoiceCurrency = finalResult.ExtractedData?.InvoiceCurrency ?? "VND",
                ExchangeRate = finalResult.ExtractedData?.ExchangeRate ?? 1,
                Seller = new SellerInfo
                {
                    Name = finalResult.ExtractedData?.SellerName,
                    TaxCode = finalResult.ExtractedData?.SellerTaxCode,
                    Address = finalResult.ExtractedData?.SellerAddress,
                    Phone = finalResult.ExtractedData?.SellerPhone,
                    Email = finalResult.ExtractedData?.SellerEmail,
                    BankAccount = finalResult.ExtractedData?.SellerBankAccount,
                    BankName = finalResult.ExtractedData?.SellerBankName
                },
                Buyer = new BuyerInfo
                {
                    Name = finalResult.ExtractedData?.BuyerName,
                    TaxCode = finalResult.ExtractedData?.BuyerTaxCode,
                    Address = finalResult.ExtractedData?.BuyerAddress,
                    Phone = finalResult.ExtractedData?.BuyerPhone,
                    Email = finalResult.ExtractedData?.BuyerEmail,
                    ContactPerson = finalResult.ExtractedData?.BuyerContactPerson
                },
                TotalAmountBeforeTax = finalResult.ExtractedData?.TotalPreTax,
                TotalTaxAmount = finalResult.ExtractedData?.TotalTaxAmount,
                TotalAmount = finalResult.ExtractedData?.TotalAmount ?? 0,
                TotalAmountInWords = finalResult.ExtractedData?.TotalAmountInWords,
                PaymentMethod = finalResult.ExtractedData?.PaymentTerms,
                MCCQT = finalResult.ExtractedData?.MCCQT,
                RawData = new InvoiceRawData { 
                    ObjectKey = request.S3Key,
                    OcrJobId = request.S3Key
                },
                ExtractedData = finalResult.ExtractedData,
                Status = hasFatalError ? nameof(InvoiceStatus.Rejected) : nameof(InvoiceStatus.Draft),
                // OCR-only: Force Yellow risk even if math is correct (missing XML evidence)
                RiskLevel = !isInvoiceValid ? "Red" : "Yellow",
                Notes = !isInvoiceValid ? "Hóa đơn có lỗi, cần kiểm tra lại" 
                        : "Hóa đơn từ OCR, cần bổ sung file XML gốc để xác thực pháp lý.",
                Version = finalResult.NewVersion,
                Workflow = new InvoiceWorkflow { UploadedBy = UserId },
                CreatedAt = DateTime.UtcNow
            };

            if (finalResult.IsReplacement && finalResult.ReplacedInvoiceId.HasValue)
            {
                invoice.ReplacedBy = finalResult.ReplacedInvoiceId;
                var oldInvoice = await _unitOfWork.Invoices.GetByIdAsync(finalResult.ReplacedInvoiceId.Value);
                if (oldInvoice != null)
                {
                    oldInvoice.IsReplaced = true;
                    oldInvoice.ReplacedBy = invoiceId;
                    _unitOfWork.Invoices.Update(oldInvoice);
                }
            }

            string? GetErrorStr(List<ValidationErrorDetail>? errs) => errs != null && errs.Any() ? System.Text.Json.JsonSerializer.Serialize(errs) : null;
            string GetLayerStatus(bool isValid, List<ValidationErrorDetail>? warnings) => !isValid ? "Fail" : (warnings != null && warnings.Any()) ? "Warning" : "Pass";

            var logicErrInfo = (logicResult.ErrorDetails ?? System.Linq.Enumerable.Empty<ValidationErrorDetail>()).FirstOrDefault() ?? (logicResult.WarningDetails ?? System.Linq.Enumerable.Empty<ValidationErrorDetail>()).FirstOrDefault();
            invoice.CheckResults.Add(new InvoiceCheckResult
            {
                CheckId = Guid.NewGuid(),
                InvoiceId = invoiceId,
                Category = "BUSINESS_LOGIC",
                CheckName = "BusinessLogic",
                CheckOrder = 3,
                IsValid = logicResult.IsValid,
                Status = GetLayerStatus(logicResult.IsValid, logicResult.WarningDetails),
                ErrorCode = logicErrInfo?.ErrorCode,
                ErrorMessage = logicErrInfo?.ErrorMessage,
                Suggestion = logicErrInfo?.Suggestion,
                ErrorDetails = GetErrorStr(logicResult.ErrorDetails),
                DurationMs = (int)swLogic.ElapsedMilliseconds
            });

            var checkStatus = isInvoiceValid ? (finalResult.WarningDetails.Any() ? "WARNING" : "PASS") : "FAIL";
            var priorityError = finalResult.ErrorDetails.FirstOrDefault();
            var priorityWarning = finalResult.WarningDetails.FirstOrDefault();
            
            invoice.CheckResults.Add(new InvoiceCheckResult
            {
                CheckId = Guid.NewGuid(),
                InvoiceId = invoiceId,
                Category = "AUTO_UPLOAD_VALIDATION",
                CheckName = "AUTO_UPLOAD_VALIDATION",
                CheckOrder = 4,
                IsValid = isInvoiceValid,
                Status = checkStatus,
                ErrorCode = priorityError?.ErrorCode ?? priorityWarning?.ErrorCode,
                ErrorMessage = priorityError?.ErrorMessage ?? priorityWarning?.ErrorMessage,
                Suggestion = priorityError?.Suggestion ?? priorityWarning?.Suggestion,
                DurationMs = (int)swLogic.ElapsedMilliseconds,
                ErrorDetails = System.Text.Json.JsonSerializer.Serialize(new
                {
                    ErrorDetails = finalResult.ErrorDetails,
                    WarningDetails = finalResult.WarningDetails
                })
            });

            var uploadUser = await _unitOfWork.Users.GetByIdAsync(UserId);
            invoice.AuditLogs.Add(new InvoiceAuditLog
            {
                AuditId = Guid.NewGuid(),
                InvoiceId = invoiceId,
                UserId = UserId,
                UserEmail = uploadUser?.Email,
                UserRole = uploadUser?.Role,
                Action = "UPLOAD_OCR",
                Changes = new List<AuditChange>
                {
                    new() { Field = "Status", OldValue = null, NewValue = isInvoiceValid ? "Draft" : "Rejected", ChangeType = "INSERT" },
                    new() { Field = "RiskLevel", OldValue = null, NewValue = invoice.RiskLevel, ChangeType = "INSERT" }
                },
                Comment = isInvoiceValid ? "Tải lên OCR hợp lệ. Cần bổ sung file XML gốc." : "Tải lên OCR không hợp lệ."
            });

            await _unitOfWork.Invoices.AddAsync(invoice);
            await _unitOfWork.CompleteAsync();

            finalResult.InvoiceId = invoiceId;
            return finalResult;
        }
    }
}
