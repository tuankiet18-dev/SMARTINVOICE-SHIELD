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
using SmartInvoice.API.Data;
using Microsoft.EntityFrameworkCore;

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
        private readonly INotificationService _notificationService;
        private readonly IQuotaService _quotaService;
        private readonly AppDbContext _context;
        private readonly ISystemConfigProvider _configProvider;

        public InvoiceService(AppDbContext context, IUnitOfWork unitOfWork, StorageService storageService, IInvoiceProcessorService invoiceProcessor, IConfiguration configuration, ILogger<InvoiceService> logger, ISqsMessagePublisher sqsPublisher, INotificationService notificationService, IQuotaService quotaService, ISystemConfigProvider configProvider)
        {
            _context = context;
            _unitOfWork = unitOfWork;
            _storageService = storageService;
            _invoiceProcessor = invoiceProcessor;
            _configuration = configuration;
            _logger = logger;
            _sqsPublisher = sqsPublisher;
            _notificationService = notificationService;
            _quotaService = quotaService;
            _configProvider = configProvider;
        }

        // ════════════════════════════════════════════
        //  QUERY
        // ════════════════════════════════════════════

        public async Task<InvoiceDetailDto?> GetInvoiceDetailAsync(Guid invoiceId, Guid companyId, Guid userId, string userRole)
        {
            var invoice = await _unitOfWork.Invoices.GetInvoiceWithDetailsAsync(invoiceId);
            bool wasMerged = false;

            // If the invoice was soft-deleted because it was merged into another invoice
            if (invoice != null && invoice.IsDeleted && invoice.Status == "Draft" && !string.IsNullOrEmpty(invoice.Notes) && invoice.Notes.StartsWith("MERGED_INTO:"))
            {
                var targetIdStr = invoice.Notes.Substring("MERGED_INTO:".Length);
                if (Guid.TryParse(targetIdStr, out var targetId))
                {
                    var targetInvoice = await _unitOfWork.Invoices.GetInvoiceWithDetailsAsync(targetId);
                    if (targetInvoice != null && !targetInvoice.IsDeleted)  
                    {
                        invoice = targetInvoice;
                        wasMerged = true;
                        _logger?.LogInformation("GetInvoiceDetailAsync redirected deleted draft {DraftId} to merged target {TargetId}", invoiceId, targetId);
                    }
                }
            }

            if (invoice == null || invoice.IsDeleted) return null;

            // Multi-tenant check
            if (invoice.CompanyId != companyId) return null;

            // RBAC: Member chỉ xem hóa đơn do mình upload
            if (userRole == "Accountant" && invoice.Workflow.UploadedBy != userId) return null;

            var dto = MapToDetailDto(invoice);
            
            // To prevent frontend polling timeout after merge, we fake "Success" status just for this redirected request
            if (wasMerged && dto.Status == "Draft") 
            {
                dto.Status = "Success";
            }
            
            return dto;
        }

        public async Task<string?> GetVisualFileUrlAsync(Guid invoiceId, Guid companyId)
        {
            var invoice = await _unitOfWork.Invoices.GetInvoiceWithDetailsAsync(invoiceId);
            if (invoice == null || invoice.CompanyId != companyId) return null;

            // 1. Nếu EF Core đã Include sẵn
            if (invoice.VisualFile != null)
            {
                return _storageService.GenerateDownloadUrl(invoice.VisualFile.S3Key);
            }

            // 2. Nếu EF quên Include, ta dùng VisualFileId để query trực tiếp
            if (invoice.VisualFileId.HasValue)
            {
                var file = await _unitOfWork.FileStorages.GetByIdAsync(invoice.VisualFileId.Value);
                if (file != null) return _storageService.GenerateDownloadUrl(file.S3Key);
            }

            // 3. Cứu cánh cuối cùng: Lấy S3Key từ RawData lúc vừa upload
            // FIX: Chỉ áp dụng cho luồng OCR (API), vì luồng XML thì ObjectKey là tệp văn bản.
            if (invoice.ProcessingMethod == "API" && !string.IsNullOrEmpty(invoice.RawData?.ObjectKey))
            {
                return _storageService.GenerateDownloadUrl(invoice.RawData.ObjectKey);
            }

            return null;
        }

        public async Task<List<InvoiceVersionDto>> GetInvoiceVersionsAsync(Guid invoiceId, Guid companyId)
        {
            var targetInvoice = await _unitOfWork.Invoices.GetInvoiceWithDetailsAsync(invoiceId);
            if (targetInvoice == null || targetInvoice.CompanyId != companyId)
            {
                return new List<InvoiceVersionDto>();
            }

            var sellerTaxCode = targetInvoice.Seller?.TaxCode;
            var invoiceNumber = targetInvoice.InvoiceNumber;
            var serialNumber = targetInvoice.SerialNumber;

            if (string.IsNullOrEmpty(sellerTaxCode) || string.IsNullOrEmpty(invoiceNumber))
            {
                // If it's a draft without those details, it shouldn't have versions
                return new List<InvoiceVersionDto>
                {
                    new InvoiceVersionDto
                    {
                        InvoiceId = targetInvoice.InvoiceId,
                        Version = targetInvoice.Version,
                        Status = targetInvoice.Status,
                        RiskLevel = targetInvoice.RiskLevel,
                        CreatedAt = targetInvoice.CreatedAt
                    }
                };
            }

            var versions = await _unitOfWork.Invoices.FindAsync(i =>
                i.CompanyId == companyId &&
                i.Seller.TaxCode == sellerTaxCode &&
                i.InvoiceNumber == invoiceNumber &&
                i.SerialNumber == serialNumber &&
                !i.IsDeleted
            );

            return versions
                .OrderByDescending(v => v.Version)
                .Select(v => new InvoiceVersionDto
                {
                    InvoiceId = v.InvoiceId,
                    Version = v.Version,
                    Status = v.Status,
                    RiskLevel = v.RiskLevel,
                    CreatedAt = v.CreatedAt
                })
                .ToList();
        }

        public async Task<IEnumerable<Invoice>> GetAllInvoicesAsync()
        {
            return await _unitOfWork.Invoices.GetAllAsync();
        }

        public async Task<InvoiceStatsDto> GetInvoiceStatsAsync(DateTime startDate, DateTime endDate, string? statusFilter, Guid companyId, Guid userId, string userRole)
        {
            // Lấy AppDbContext từ IUnitOfWork (nếu project bạn đang dùng _dbContext trực tiếp thì gọi _dbContext.Invoices)
            // Tui giả định bạn có thể query Invoices như sau:
            var query = await _unitOfWork.Invoices.GetAllAsync(); // Tạm lấy list nếu repo bạn không có GetQueryable
            var filteredQuery = query.Where(i => i.CompanyId == companyId
                                              && i.InvoiceDate >= startDate
                                              && i.InvoiceDate <= endDate
                                              && !i.IsDeleted
                                              && !i.IsReplaced);

            if (userRole == "Accountant")
            {
                filteredQuery = filteredQuery.Where(i => i.Workflow?.UploadedBy == userId);
            }

            var finalQuery = filteredQuery.AsQueryable();

            if (!string.IsNullOrEmpty(statusFilter))
            {
                finalQuery = finalQuery.Where(i => i.Status == statusFilter);
            }

            var list = finalQuery.ToList();

            var totalCount = list.Count;
            // LOGIC MỚI: Hợp lệ nếu Status là Approved HOẶC RiskLevel là Green
            var validCount = list.Count(i => i.Status == "Approved" || i.RiskLevel == "Green");

            // LOGIC MỚI: Cần xem xét nếu CHƯA Approved VÀ nằm ở vùng rủi ro
            var needReviewCount = list.Count(i => i.Status != "Approved" && (i.RiskLevel == "Yellow" || i.RiskLevel == "Orange"));

            var approvedCount = list.Count(i => i.Status == "Approved");

            return new InvoiceStatsDto
            {
                TotalCount = totalCount,
                TotalAmount = list.Sum(i => i.TotalAmount),
                TotalTaxAmount = list.Sum(i => i.TotalTaxAmount ?? 0),
                ValidCount = validCount,
                NeedReviewCount = needReviewCount,
                ApprovedCount = approvedCount
            };
        }

        public async Task<Invoice> CreateInvoiceAsync(Invoice invoice)
        {
            await _unitOfWork.Invoices.AddAsync(invoice);
            await _unitOfWork.CompleteAsync();
            return invoice;
        }

        public async Task UpdateInvoiceAsync(Guid id, UpdateInvoiceDto request, Guid userId, string userEmail, string userRole, string? ipAddress)
        {
            var existingInvoice = await _unitOfWork.Invoices.GetInvoiceWithDetailsAsync(id);
            if (existingInvoice == null)
                throw new KeyNotFoundException("Không tìm thấy hóa đơn yêu cầu.");

            // Chỉ cho phép edit khi status = Draft hoặc Rejected hoặc Processing (nếu cần review ngay)
            if (existingInvoice.Status != "Draft" && existingInvoice.Status != "Rejected" && existingInvoice.Status != "Processing" && existingInvoice.Status != "Success")
            {
                // Cho phép sửa Success nếu chưa Submit
                if (existingInvoice.Status != "Success")
                    throw new InvalidOperationException($"Không thể chỉnh sửa hóa đơn khi đang ở trạng thái {existingInvoice.Status}.");
            }

            // Track changes for audit
            var changes = new List<AuditChange>();
            void TrackChange(string field, object? oldVal, object? newVal)
            {
                if (oldVal?.ToString() != newVal?.ToString())
                    changes.Add(new AuditChange { Field = field, OldValue = oldVal, NewValue = newVal, ChangeType = "UPDATE" });
            }

            // --- Basic Fields ---
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
            if (request.FormNumber != null)
            {
                TrackChange("FormNumber", existingInvoice.FormNumber, request.FormNumber);
                existingInvoice.FormNumber = request.FormNumber;
            }

            TrackChange("InvoiceDate", existingInvoice.InvoiceDate, request.InvoiceDate);
            existingInvoice.InvoiceDate = request.InvoiceDate;

            if (request.TotalAmount > 0)
            {
                TrackChange("TotalAmount", existingInvoice.TotalAmount, request.TotalAmount);
                existingInvoice.TotalAmount = request.TotalAmount;
            }
            if (request.TotalAmountBeforeTax.HasValue)
            {
                TrackChange("TotalAmountBeforeTax", existingInvoice.TotalAmountBeforeTax, request.TotalAmountBeforeTax);
                existingInvoice.TotalAmountBeforeTax = request.TotalAmountBeforeTax;
            }
            if (request.TotalTaxAmount.HasValue)
            {
                TrackChange("TotalTaxAmount", existingInvoice.TotalTaxAmount, request.TotalTaxAmount);
                existingInvoice.TotalTaxAmount = request.TotalTaxAmount;
            }

            if (request.Status != null)
            {
                TrackChange("Status", existingInvoice.Status, request.Status);
                existingInvoice.Status = request.Status;
            }
            TrackChange("Notes", existingInvoice.Notes, request.Notes);
            existingInvoice.Notes = request.Notes;

            // --- Seller ---
            if (request.SellerName != null)
            {
                TrackChange("SellerName", existingInvoice.Seller.Name, request.SellerName);
                existingInvoice.Seller.Name = request.SellerName;
            }
            if (request.SellerTaxCode != null)
            {
                TrackChange("SellerTaxCode", existingInvoice.Seller.TaxCode, request.SellerTaxCode);
                existingInvoice.Seller.TaxCode = request.SellerTaxCode;
            }
            if (request.SellerAddress != null)
            {
                TrackChange("SellerAddress", existingInvoice.Seller.Address, request.SellerAddress);
                existingInvoice.Seller.Address = request.SellerAddress;
            }

            // --- Buyer ---
            if (request.BuyerName != null)
            {
                TrackChange("BuyerName", existingInvoice.Buyer.Name, request.BuyerName);
                existingInvoice.Buyer.Name = request.BuyerName;
            }
            if (request.BuyerTaxCode != null)
            {
                TrackChange("BuyerTaxCode", existingInvoice.Buyer.TaxCode, request.BuyerTaxCode);
                existingInvoice.Buyer.TaxCode = request.BuyerTaxCode;
            }
            if (request.BuyerAddress != null)
            {
                TrackChange("BuyerAddress", existingInvoice.Buyer.Address, request.BuyerAddress);
                existingInvoice.Buyer.Address = request.BuyerAddress;
            }

            // --- Line Items (ExtractedData) ---
            if (request.LineItems != null)
            {
                if (existingInvoice.ExtractedData == null) existingInvoice.ExtractedData = new InvoiceExtractedData();

                // For simplicity, we replace the entire collection. 
                // In a production app, we might want more granular diffing for Audit Logs.
                var oldItemsJson = System.Text.Json.JsonSerializer.Serialize(existingInvoice.ExtractedData.LineItems);
                var newItemsJson = System.Text.Json.JsonSerializer.Serialize(request.LineItems);

                if (oldItemsJson != newItemsJson)
                {
                    TrackChange("LineItems", "Modified", "Modified");
                    existingInvoice.ExtractedData.LineItems = request.LineItems.Select(l => new InvoiceLineItem
                    {
                        Stt = l.LineNumber,
                        ProductName = l.ItemName ?? "",
                        Unit = l.Unit ?? "",
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice,
                        TotalAmount = l.TotalAmount,
                        VatRate = l.VatRate,
                        VatAmount = l.VatAmount
                    }).ToList();
                }
            }

            // Nếu hóa đơn đã bị Rejected thì sửa xong trả về trạng thái Success (nếu đã bóc tách xong) hoặc Draft
            if (existingInvoice.Status == nameof(InvoiceStatus.Rejected))
            {
                existingInvoice.Status = "Success"; // Or Draft
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
                    InvoiceNumber = existingInvoice.InvoiceNumber,
                    CompanyId = existingInvoice.CompanyId,
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

        public async Task<bool> DeleteInvoiceAsync(Guid id, Guid companyId, Guid userId, string userEmail, string userRole, string? ipAddress)
        {
            var invoice = await _unitOfWork.Invoices.GetByIdAsync(id);
            if (invoice == null) return false;

            // Multi-tenant check
            if (invoice.CompanyId != companyId) return false;

            // Cho phép xóa đối với mọi trạng thái để giảm dung lượng
            invoice.IsDeleted = true;
            invoice.DeletedAt = DateTime.UtcNow;
            
            _unitOfWork.Invoices.Update(invoice);

            // Audit log
            await _unitOfWork.InvoiceAuditLogs.AddAsync(new InvoiceAuditLog
            {
                AuditId = Guid.NewGuid(),
                InvoiceId = id,
                InvoiceNumber = invoice.InvoiceNumber,
                CompanyId = companyId,
                UserId = userId,
                UserEmail = userEmail,
                UserRole = userRole,
                Action = "TRASH",
                Comment = "Đã chuyển hóa đơn vào thùng rác.",
                IpAddress = ipAddress,
                CreatedAt = DateTime.UtcNow
            });

            await _unitOfWork.CompleteAsync();
            return true;
        }

        public async Task<PagedResult<InvoiceDto>> GetTrashInvoicesAsync(GetInvoicesQueryDto query, Guid companyId, Guid userId, string userRole)
        {
            var (items, totalCount) = await _unitOfWork.Invoices.GetPagedTrashInvoicesAsync(query, companyId, userId, userRole);

            var dtos = items.Select(i => new InvoiceDto
            {
                InvoiceId = i.InvoiceId,
                InvoiceNumber = i.InvoiceNumber,
                SerialNumber = i.SerialNumber,
                InvoiceDate = i.InvoiceDate,
                CreatedAt = i.CreatedAt,
                SellerName = i.Seller?.Name,
                SellerTaxCode = i.Seller?.TaxCode,
                TotalAmount = i.TotalAmount,
                InvoiceCurrency = i.InvoiceCurrency,
                Status = i.Status,
                RiskLevel = i.RiskLevel,
                ProcessingMethod = i.ProcessingMethod,
                UploadedByName = i.Workflow?.Uploader?.FullName ?? "Unknown"
            }).ToList();

            return new PagedResult<InvoiceDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                PageIndex = query.Page,
                PageSize = query.Size
            };
        }

        public async Task<bool> RestoreInvoiceAsync(Guid id, Guid companyId, Guid userId, string userEmail, string userRole, string? ipAddress)
        {
            var invoice = await _unitOfWork.Invoices.GetTrashInvoiceWithDetailsAsync(id);
            if (invoice == null || invoice.CompanyId != companyId) return false;
            
            if (userRole == "Accountant" && invoice.Workflow?.UploadedBy != userId) return false;

            invoice.IsDeleted = false;
            invoice.DeletedAt = null;
            _unitOfWork.Invoices.Update(invoice);

            // Audit log
            await _unitOfWork.InvoiceAuditLogs.AddAsync(new InvoiceAuditLog
            {
                AuditId = Guid.NewGuid(),
                InvoiceId = id,
                InvoiceNumber = invoice.InvoiceNumber,
                CompanyId = companyId,
                UserId = userId,
                UserEmail = userEmail,
                UserRole = userRole,
                Action = "RESTORE",
                Comment = "Đã khôi phục hóa đơn từ thùng rác.",
                IpAddress = ipAddress,
                CreatedAt = DateTime.UtcNow
            });

            await _unitOfWork.CompleteAsync();
            return true;
        }

        public async Task<bool> HardDeleteInvoiceAsync(Guid id, Guid companyId, Guid userId, string userEmail, string userRole, string? ipAddress)
        {
            var invoice = await _unitOfWork.Invoices.GetTrashInvoiceWithDetailsAsync(id);
            if (invoice == null || invoice.CompanyId != companyId) return false;
            
            if (userRole == "Accountant" && invoice.Workflow?.UploadedBy != userId) return false;

            // Audit log (Ghi TRƯỚC KHI XÓA InvoiceId để DB kịp map, sau đó DB sẽ set null)
            await _unitOfWork.InvoiceAuditLogs.AddAsync(new InvoiceAuditLog
            {
                AuditId = Guid.NewGuid(),
                InvoiceId = id,
                InvoiceNumber = invoice.InvoiceNumber,
                CompanyId = companyId,
                UserId = userId,
                UserEmail = userEmail,
                UserRole = userRole,
                Action = "HARD_DELETE",
                Comment = $"Xóa vĩnh viễn hóa đơn Số: {invoice.InvoiceNumber}, Ký hiệu: {invoice.SerialNumber}. Đã giải phóng tệp tin.",
                IpAddress = ipAddress,
                CreatedAt = DateTime.UtcNow
            });

            long totalDeletedSize = 0;

            var filesToDelete = new List<FileStorage?>();
            if (invoice.OriginalFile != null) filesToDelete.Add(invoice.OriginalFile);
            if (invoice.VisualFile != null && invoice.VisualFileId != invoice.OriginalFileId) filesToDelete.Add(invoice.VisualFile);

            foreach (var file in filesToDelete)
            {
                if (file != null && !string.IsNullOrEmpty(file.S3Key))
                {
                    await _storageService.DeleteFileAsync(file.S3Key);
                    totalDeletedSize += file.FileSize;
                    _unitOfWork.FileStorages.Remove(file);
                }
            }

            if (totalDeletedSize > 0)
            {
                await _quotaService.ReleaseStorageQuotaAsync(companyId, totalDeletedSize);
            }

            // Also hard delete from DB
            _unitOfWork.Invoices.Remove(invoice);
            await _unitOfWork.CompleteAsync();
            return true;
        }

        public async Task<int> EmptyTrashAsync(Guid companyId, Guid userId, string userEmail, string userRole, string? ipAddress)
        {
            // Lấy tất cả invoice đã xóa mềm của company (kể cả Draft)
            var allTrash = await _context.Invoices
                .IgnoreQueryFilters()
                .Include(i => i.OriginalFile)
                .Include(i => i.VisualFile)
                .Where(i => i.CompanyId == companyId && i.IsDeleted)
                .ToListAsync();

            // Member chỉ xóa hóa đơn của mình
            if (userRole == "Accountant")
                allTrash = allTrash.Where(i => i.Workflow.UploadedBy == userId).ToList();

            if (allTrash.Count == 0) return 0;

            long totalReleasedSize = 0;
            var processedFileIds = new HashSet<Guid>();

            foreach (var invoice in allTrash)
            {
                // Xóa OriginalFile (XML) trên S3
                if (invoice.OriginalFile != null && !processedFileIds.Contains(invoice.OriginalFile.FileId))
                {
                    processedFileIds.Add(invoice.OriginalFile.FileId);
                    if (!string.IsNullOrEmpty(invoice.OriginalFile.S3Key))
                    {
                        try { await _storageService.DeleteFileAsync(invoice.OriginalFile.S3Key); } catch { /* best-effort */ }
                        totalReleasedSize += invoice.OriginalFile.FileSize;
                    }
                    _unitOfWork.FileStorages.Remove(invoice.OriginalFile);
                }

                // Xóa VisualFile (PDF/Ảnh) trên S3 — chỉ nếu khác OriginalFile
                if (invoice.VisualFile != null
                    && invoice.VisualFileId != invoice.OriginalFileId
                    && !processedFileIds.Contains(invoice.VisualFile.FileId))
                {
                    processedFileIds.Add(invoice.VisualFile.FileId);
                    if (!string.IsNullOrEmpty(invoice.VisualFile.S3Key))
                    {
                        try { await _storageService.DeleteFileAsync(invoice.VisualFile.S3Key); } catch { /* best-effort */ }
                        totalReleasedSize += invoice.VisualFile.FileSize;
                    }
                    _unitOfWork.FileStorages.Remove(invoice.VisualFile);
                }

                // Xóa file từ RawData (OCR upload chưa có FileStorage record riêng)
                if (invoice.OriginalFileId == null && invoice.VisualFileId == null
                    && !string.IsNullOrEmpty(invoice.RawData?.ObjectKey))
                {
                    try { await _storageService.DeleteFileAsync(invoice.RawData.ObjectKey); } catch { /* best-effort */ }
                }

                _unitOfWork.Invoices.Remove(invoice);

                // Ghi nhận Audit Log cho từng hóa đơn bị xóa vĩnh viễn
                await _unitOfWork.InvoiceAuditLogs.AddAsync(new InvoiceAuditLog
                {
                    AuditId = Guid.NewGuid(),
                    InvoiceId = invoice.InvoiceId,
                    InvoiceNumber = invoice.InvoiceNumber,
                    CompanyId = companyId,
                    UserId = userId,
                    UserEmail = userEmail,
                    UserRole = userRole,
                    Action = "HARD_DELETE",
                    Comment = $"Dọn dẹp thùng rác: Xóa vĩnh viễn hóa đơn Số: {invoice.InvoiceNumber}, Ký hiệu: {invoice.SerialNumber}.",
                    IpAddress = ipAddress,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (totalReleasedSize > 0)
                await _quotaService.ReleaseStorageQuotaAsync(companyId, totalReleasedSize);

            await _unitOfWork.CompleteAsync();
            return allTrash.Count;
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
                UploadedByName = i.Workflow.Uploader?.FullName ?? "Unknown",
                CurrentApprovalStep = i.Workflow != null ? i.Workflow.CurrentApprovalStep : 1
            }).ToList();

            return new PagedResult<InvoiceDto>
            {
                Items = dtos,
                TotalCount = result.TotalCount,
                PageIndex = query.Page,
                PageSize = query.Size
            };
        }

        public async Task<IEnumerable<InvoiceAuditLogDto>> GetAuditLogsAsync(Guid invoiceId, Guid companyId, Guid userId, string userRole)
        {
            var invoiceExists = await _unitOfWork.Invoices.GetInvoiceWithDetailsAsync(invoiceId);
            if (invoiceExists == null || invoiceExists.CompanyId != companyId)
                throw new KeyNotFoundException("Không tìm thấy hóa đơn yêu cầu.");

            // RBAC Member/Accountant chỉ được xem log hóa đơn do mình tạo
            if (userRole == "Accountant" && invoiceExists.Workflow?.UploadedBy != userId)
                throw new UnauthorizedAccessException("Bạn không có quyền xem lịch sử hóa đơn này.");

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
                throw new InvalidOperationException($"Chỉ có thể gửi duyệt hóa đơn ở trạng thái Nháp. Trạng thái hiện tại: {invoice.Status}.");

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
                InvoiceNumber = invoice.InvoiceNumber,
                CompanyId = companyId,
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

            await _notificationService.SendNotificationToCompanyAdminsAsync(
                companyId: companyId,
                type: "Approval",
                title: "Hóa đơn mới chờ duyệt",
                message: $"Hóa đơn số {invoice.InvoiceNumber} đang chờ được phê duyệt.",
                relatedInvoiceId: invoiceId,
                priority: "Normal"
            );

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
                        throw new InvalidOperationException($"Hóa đơn không ở trạng thái Nháp. Trạng thái hiện tại: {invoice.Status}.");

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
                        CompanyId = invoice.CompanyId,
                        InvoiceNumber = invoice.InvoiceNumber,
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

            if (batchResult.SuccessCount > 0)
            {
                await _notificationService.SendNotificationToCompanyAdminsAsync(
                    companyId: companyId,
                    type: "Approval",
                    title: "Hóa đơn mới chờ duyệt",
                    message: $"{batchResult.SuccessCount} hóa đơn đang chờ được phê duyệt.",
                    priority: "Normal"
                );
            }

            return batchResult;
        }

        // ════════════════════════════════════════════
        //  WORKFLOW: Approve → Approved
        // ════════════════════════════════════════════

        public async Task ApproveInvoiceAsync(Guid invoiceId, Guid companyId, Guid userId, string userEmail, string userRole, string? comment, string? ipAddress)
        {
            _logger?.LogInformation("ApproveInvoiceAsync called for {InvoiceId} by user {UserId}", invoiceId, userId);

            // Ràng buộc chung: Kế toán viên (Accountant) KHÔNG CÓ QUYỀN PHÊ DUYỆT BẤT KỲ CẤP ĐỘ NÀO, chỉ được gửi duyệt
            if (userRole == "Accountant")
            {
                throw new InvalidOperationException("Kế toán viên (Accountant) không có quyền phê duyệt hóa đơn. Vui lòng sử dụng tính năng 'Gửi duyệt'.");
            }

            var invoice = await _unitOfWork.Invoices.GetByIdAsync(invoiceId);
            if (invoice == null)
                throw new KeyNotFoundException("Không tìm thấy hóa đơn.");
            if (invoice.CompanyId != companyId)
                throw new UnauthorizedAccessException("Không có quyền truy cập hóa đơn này.");
            if (invoice.Status != "Pending")
                throw new InvalidOperationException($"Chỉ có thể duyệt hóa đơn ở trạng thái Chờ duyệt. Trạng thái hiện tại: {invoice.Status}.");

            var company = await _context.Companies.Include(c => c.SubscriptionPackage).FirstOrDefaultAsync(c => c.CompanyId == companyId);
            bool isPremiumTier = company != null && company.SubscriptionPackage != null && company.SubscriptionPackage.HasAdvancedWorkflow;
            bool needsTwoStep = isPremiumTier && company.RequireTwoStepApproval && invoice.TotalAmount >= (company.TwoStepApprovalThreshold ?? 0);

            var oldStatus = invoice.Status;
            var auditAction = "APPROVE";
            var auditMsg = comment ?? "Đã duyệt hóa đơn.";

            if (needsTwoStep)
            {
                if (invoice.Workflow.CurrentApprovalStep == 1)
                {
                    // --- LẦN DUYỆT 1 ---
                    invoice.Workflow.Level1ApprovedBy = userId;
                    invoice.Workflow.Level1ApprovedAt = DateTime.UtcNow;
                    invoice.Workflow.CurrentApprovalStep = 2; 
                    // Status vẫn giữ là Pending để đợi người thứ 2 duyệt
                    
                    auditAction = "APPROVE_LEVEL_1";
                    auditMsg = "Đã duyệt Cấp 1. Đang chờ phê duyệt Cấp 2.";
                }
                else if (invoice.Workflow.CurrentApprovalStep == 2)
                {
                    // --- LẦN DUYỆT 2 ---
                    
                    // Ràng buộc 1: KIỂM TRA CHÉO (Bắt buộc 2 người khác nhau, vô hiệu hóa tự biên tự diễn)
                    if (invoice.Workflow.Level1ApprovedBy == userId)
                    {
                        throw new InvalidOperationException("Bạn đã duyệt Cấp 1 rồi. Hóa đơn này cần một người khác để phê duyệt Cấp 2 (Cross-check).");
                    }

                    // Ràng buộc 2: KIỂM SOÁT CẤP BẬC (Chỉ có Sếp mới được chốt sổ)
                    if (userRole != "ChiefAccountant" && userRole != "CompanyAdmin" && userRole != "SuperAdmin")
                    {
                        throw new InvalidOperationException("Chỉ có Kế toán trưởng hoặc Giám đốc mới có quyền phê duyệt hóa đơn Cấp 2.");
                    }

                    // Qua được 2 ải bảo mật -> Chốt duyệt
                    invoice.Workflow.Level2ApprovedBy = userId;
                    invoice.Workflow.Level2ApprovedAt = DateTime.UtcNow;
                    
                    // Chính thức Approved
                    invoice.Status = "Approved";
                    invoice.Workflow.ApprovedBy = userId;
                    invoice.Workflow.ApprovedAt = DateTime.UtcNow;

                    auditAction = "APPROVE_LEVEL_2";
                    auditMsg = "Đã duyệt Cấp 2. Hoàn tất phê duyệt.";
                }
            }
            else
            {
                // --- QUY TRÌNH 1 CẤP (Công ty gói Cơ bản hoặc hóa đơn dưới hạn mức) ---
                invoice.Status = "Approved";
                invoice.Workflow.ApprovedBy = userId;
                invoice.Workflow.ApprovedAt = DateTime.UtcNow;
            }

            invoice.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Invoices.Update(invoice);

            await _unitOfWork.InvoiceAuditLogs.AddAsync(new InvoiceAuditLog
            {
                AuditId = Guid.NewGuid(),
                InvoiceId = invoiceId,
                CompanyId = invoice.CompanyId,
                InvoiceNumber = invoice.InvoiceNumber,
                UserId = userId,
                UserEmail = userEmail,
                UserRole = userRole,
                Action = auditAction,
                Changes = new List<AuditChange>
                {
                    new() { Field = "Status", OldValue = oldStatus, NewValue = invoice.Status, ChangeType = "UPDATE" }
                },
                Comment = auditMsg,
                IpAddress = ipAddress,
                CreatedAt = DateTime.UtcNow
            });

            await _unitOfWork.CompleteAsync();

            await _notificationService.SendNotificationAsync(
                userId: invoice.Workflow.UploadedBy,
                type: "Approval",
                title: "Hóa đơn đã được duyệt",
                message: $"Hóa đơn số {invoice.InvoiceNumber} đã được phê duyệt.",
                relatedInvoiceId: invoiceId,
                priority: "Normal"
            );

            _logger?.LogInformation("Approved invoice {InvoiceId}", invoiceId);
        }

        // ════════════════════════════════════════════
        //  WORKFLOW: Reject → Rejected
        // ════════════════════════════════════════════

        public async Task RejectInvoiceAsync(Guid invoiceId, Guid companyId, Guid userId, string userEmail, string userRole, string reason, string? comment, string? ipAddress)
        {
            if (userRole != "ChiefAccountant" && userRole != "CompanyAdmin" && userRole != "SuperAdmin")
                throw new UnauthorizedAccessException("Chỉ Kế toán trưởng và Admin mới có quyền từ chối hóa đơn.");

            _logger?.LogInformation("RejectInvoiceAsync called for {InvoiceId} by user {UserId} with reason {Reason}", invoiceId, userId, reason);

            var invoice = await _unitOfWork.Invoices.GetByIdAsync(invoiceId);
            if (invoice == null)
                throw new KeyNotFoundException("Không tìm thấy hóa đơn.");
            if (invoice.CompanyId != companyId)
                throw new UnauthorizedAccessException("Không có quyền truy cập hóa đơn này.");
            if (invoice.Status != "Pending")
                throw new InvalidOperationException($"Chỉ có thể từ chối hóa đơn ở trạng thái Chờ duyệt. Trạng thái hiện tại: {invoice.Status}.");

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
                CompanyId = invoice.CompanyId,
                InvoiceNumber = invoice.InvoiceNumber,
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

            await _notificationService.SendNotificationAsync(
                userId: invoice.Workflow.UploadedBy,
                type: "Approval",
                title: "Hóa đơn bị từ chối",
                message: $"Hóa đơn số {invoice.InvoiceNumber} đã bị từ chối với lý do: {reason}",
                relatedInvoiceId: invoiceId,
                priority: "High"
            );

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

                Version = i.Version,
                IsReplaced = i.IsReplaced,
                ReplacedBy = i.ReplacedBy,

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
                    ErrorCode = v.ErrorCode,
                    ErrorMessage = v.ErrorMessage,
                    Suggestion = v.Suggestion,
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
                throw new ArgumentException("Thiếu mã tệp (S3Key) để thực hiện xử lý.");
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

                    try 
                    {
                        await _storageService.DeleteFileAsync(s3Key);
                        _logger?.LogInformation("Deleted orphaned S3 file due to fatal error: {S3Key}", s3Key);
                    }
                    catch (Exception ex) 
                    {
                        _logger?.LogError(ex, "Failed to delete orphaned S3 file: {S3Key}", s3Key);
                    }

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
                long fileSize = fileInfo.Exists ? fileInfo.Length : 0;
                await _quotaService.ValidateStorageQuotaAsync(CompanyId, fileSize);

                var fileStorage = new FileStorage
                {
                    FileId = Guid.NewGuid(),
                    CompanyId = CompanyId,
                    UploadedBy = UserId,
                    OriginalFileName = s3Key.Split('/').Last(),
                    FileExtension = ".xml",
                    FileSize = fileSize,
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
                    existingInvoice.Notes = isInvoiceValid ? (finalResult.WarningDetails.Any() ? "Hóa đơn hợp lệ nhưng có cảnh báo, vui lòng kiểm tra lại." : "Hóa đơn hợp lệ (Dữ liệu từ XML).") : "Hóa đơn không hợp lệ, vui lòng kiểm tra các lỗi chi tiết.";
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
                        CheckId = Guid.NewGuid(),
                        InvoiceId = existingInvoice.InvoiceId,
                        Category = "STRUCTURE",
                        CheckName = "Structure",
                        CheckOrder = 1,
                        IsValid = structResult.IsValid,
                        Status = GetLayerStatus(structResult.IsValid, structResult.WarningDetails),
                        ErrorCode = structErrInfoM?.ErrorCode,
                        ErrorMessage = structErrInfoM?.ErrorMessage,
                        Suggestion = structErrInfoM?.Suggestion,
                        ErrorDetails = GetErrorStr(structResult.ErrorDetails),
                        DurationMs = (int)swStruct.ElapsedMilliseconds
                    });

                    var sigErrInfoM = (sigResult.ErrorDetails ?? System.Linq.Enumerable.Empty<ValidationErrorDetail>()).FirstOrDefault() ?? (sigResult.WarningDetails ?? System.Linq.Enumerable.Empty<ValidationErrorDetail>()).FirstOrDefault();
                    newCheckResults.Add(new InvoiceCheckResult
                    {
                        CheckId = Guid.NewGuid(),
                        InvoiceId = existingInvoice.InvoiceId,
                        Category = "SIGNATURE",
                        CheckName = "Signature",
                        CheckOrder = 2,
                        IsValid = sigResult.IsValid,
                        Status = GetLayerStatus(sigResult.IsValid, sigResult.WarningDetails),
                        ErrorCode = sigErrInfoM?.ErrorCode,
                        ErrorMessage = sigErrInfoM?.ErrorMessage,
                        Suggestion = sigErrInfoM?.Suggestion,
                        ErrorDetails = GetErrorStr(sigResult.ErrorDetails),
                        AdditionalData = sigResult.SignerSubject != null ? System.Text.Json.JsonSerializer.Serialize(new { SignerSubject = sigResult.SignerSubject }) : null,
                        DurationMs = (int)swSig.ElapsedMilliseconds
                    });

                    var logicErrInfoM = (logicResult.ErrorDetails ?? System.Linq.Enumerable.Empty<ValidationErrorDetail>()).FirstOrDefault() ?? (logicResult.WarningDetails ?? System.Linq.Enumerable.Empty<ValidationErrorDetail>()).FirstOrDefault();
                    newCheckResults.Add(new InvoiceCheckResult
                    {
                        CheckId = Guid.NewGuid(),
                        InvoiceId = existingInvoice.InvoiceId,
                        Category = "BUSINESS_LOGIC",
                        CheckName = "BusinessLogic",
                        CheckOrder = 3,
                        IsValid = logicResult.IsValid,
                        Status = GetLayerStatus(logicResult.IsValid, logicResult.WarningDetails),
                        ErrorCode = logicErrInfoM?.ErrorCode,
                        ErrorMessage = logicErrInfoM?.ErrorMessage,
                        Suggestion = logicErrInfoM?.Suggestion,
                        ErrorDetails = GetErrorStr(logicResult.ErrorDetails),
                        DurationMs = (int)swLogic.ElapsedMilliseconds
                    });

                    var checkStatusM = isInvoiceValid ? (finalResult.WarningDetails.Any() ? "WARNING" : "PASS") : "FAIL";
                    var priorityErrorM = finalResult.ErrorDetails.FirstOrDefault();
                    var priorityWarningM = finalResult.WarningDetails.FirstOrDefault();
                    newCheckResults.Add(new InvoiceCheckResult
                    {
                        CheckId = Guid.NewGuid(),
                        InvoiceId = existingInvoice.InvoiceId,
                        Category = "AUTO_UPLOAD_VALIDATION",
                        CheckName = "AUTO_UPLOAD_VALIDATION",
                        CheckOrder = 4,
                        IsValid = isInvoiceValid,
                        Status = checkStatusM,
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
                        InvoiceNumber = existingInvoice.InvoiceNumber,
                        CompanyId = CompanyId,
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

                    await _quotaService.ConsumeStorageQuotaAsync(CompanyId, fileSize);

                    _logger?.LogInformation("Merge complete for target invoice {InvoiceId}", existingInvoice.InvoiceId);
                    finalResult.InvoiceId = existingInvoice.InvoiceId;
                    return finalResult;
                }

                // ============================================================
                // NORMAL FLOW: CREATE A NEW INVOICE RECORD
                // ============================================================
                var invoiceId = Guid.NewGuid();

                string initialStatus = isInvoiceValid ? (finalResult.WarningDetails.Any() ? "Draft" : "Draft") : "Rejected";
                string initialRiskLevel = isInvoiceValid ? (finalResult.WarningDetails.Any() ? "Yellow" : "Green") : "Red";

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

                    Status = initialStatus,
                    RiskLevel = initialRiskLevel,
                    Notes = isInvoiceValid
                        ? (finalResult.WarningDetails.Any() ? "Hóa đơn hợp lệ nhưng có cảnh báo, vui lòng kiểm tra lại." : "Hóa đơn hợp lệ.")
                        : "Hóa đơn không hợp lệ, vui lòng kiểm tra các lỗi chi tiết.",

                    Version = finalResult.NewVersion,

                    Workflow = new InvoiceWorkflow
                    {
                        UploadedBy = UserId,
                        ApprovedBy = null,
                        ApprovedAt = null
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
                var uploadUser = await _unitOfWork.Users.GetByIdAsync(Guid.Parse(userId));
                invoice.AuditLogs.Add(new InvoiceAuditLog
                {
                    AuditId = Guid.NewGuid(),
                    InvoiceId = invoiceId,
                    InvoiceNumber = invoice.InvoiceNumber,
                    CompanyId = Guid.Parse(companyId),
                    UserId = Guid.Parse(userId),
                    UserEmail = uploadUser?.Email,
                    UserRole = uploadUser?.Role,
                    Action = "UPLOAD",
                    Changes = new List<AuditChange>
                    {
                        new() { Field = "Status", OldValue = null, NewValue = initialStatus, ChangeType = "INSERT" },
                        new() { Field = "RiskLevel", OldValue = null, NewValue = invoice.RiskLevel, ChangeType = "INSERT" }
                    },
                    Comment = isInvoiceValid ? "Đã tải lên và xác thực hóa đơn thành công." : "Tải lên hoàn tất nhưng hóa đơn không hợp lệ (Dữ liệu lỗi từ XML)."
                });

                // Lưu tất cả vào Database
                await _unitOfWork.FileStorages.AddAsync(fileStorage);
                await _unitOfWork.Invoices.AddAsync(invoice);
                await _unitOfWork.CompleteAsync();

                await _quotaService.ConsumeStorageQuotaAsync(CompanyId, fileSize);

                _logger?.LogInformation("Created new invoice {InvoiceId} from S3Key={S3Key}, RiskLevel={RiskLevel}", invoiceId, s3Key, invoice.RiskLevel);

                // Publish VietQR validation message to SQS for asynchronous processing
                if (await _configProvider.GetBoolAsync("ENABLE_VIETQR_VALIDATION", true))
                {
                    try
                    {
                        var sqsMessage = new SmartInvoice.API.DTOs.SQS.VietQrValidationMessage
                        {
                            InvoiceId = invoice.InvoiceId,
                            TaxCode = invoice.Seller?.TaxCode ?? "N/A",
                            SellerName = invoice.Seller?.Name ?? "N/A"
                        };

                        await _sqsPublisher.PublishVietQrValidationAsync(sqsMessage, CancellationToken.None);
                        _logger?.LogInformation("Invoice {InvoiceId} saved and VietQR validation message published to SQS successfully.", invoice.InvoiceId);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Invoice {InvoiceId} saved successfully, but failed to publish VietQR validation message to SQS.", invoice.InvoiceId);
                    }
                }
                else
                {
                    _logger?.LogInformation("VietQR validation is disabled via configuration. Skipping SQS publish for Invoice {InvoiceId}.", invoice.InvoiceId);
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

        /// <summary>
        /// Creates a draft invoice with Status="Processing" for the async OCR pipeline.
        /// Called by InvoicesController.UploadImage before publishing SQS message.
        /// </summary>
        public async Task CreateDraftInvoiceAsync(Invoice invoice)
        {
            await _unitOfWork.Invoices.AddAsync(invoice);
            await _unitOfWork.CompleteAsync();
            _logger?.LogInformation("Created draft invoice {InvoiceId} with Status=Processing", invoice.InvoiceId);
        }

        public async Task<ValidationResultDto> ProcessInvoiceOcrAsync(ProcessOcrRequestDto request, string userId, string companyId)
        {
            if (request.OcrResult == null)
                throw new ArgumentException("Thiếu dữ liệu OCR để thực hiện xử lý.");

            var UserId = Guid.Parse(userId);
            var CompanyId = Guid.Parse(companyId);
            var overallStopwatch = Stopwatch.StartNew();

            _logger?.LogInformation("═══════════════════════════════════════════════════════════════");
            _logger?.LogInformation("🎬 [OCR] START ProcessInvoiceOcrAsync");
            _logger?.LogInformation("   └─ S3Key: {S3Key}", request.S3Key);
            _logger?.LogInformation("   └─ CompanyId: {CompanyId}", CompanyId);
            _logger?.LogInformation("   └─ UserId: {UserId}", UserId);
            _logger?.LogInformation("   └─ Bucket: {Bucket}", request.BucketName);
            _logger?.LogInformation("═══════════════════════════════════════════════════════════════");

            // Step 1: Validate Business Logic
            _logger?.LogInformation("[OCR STEP 1/5] 🔍 Validating OCR business logic...");
            var swLogic = Stopwatch.StartNew();
            var logicResult = await _invoiceProcessor.ValidateOcrBusinessLogicAsync(request.OcrResult, CompanyId);
            swLogic.Stop();
            _logger?.LogInformation("[OCR STEP 1/5] ✅ Business logic validation completed");
            _logger?.LogInformation("   └─ IsValid: {IsValid}", logicResult.IsValid);
            _logger?.LogInformation("   └─ Errors: {ErrorCount}", logicResult.ErrorDetails?.Count ?? 0);
            _logger?.LogInformation("   └─ Warnings: {WarningCount}", logicResult.WarningDetails?.Count ?? 0);
            _logger?.LogInformation("   └─ MergeMode: {MergeMode}", logicResult.MergeMode);
            _logger?.LogInformation("   └─ Duration: {DurationMs}ms", swLogic.ElapsedMilliseconds);

            if (logicResult.ErrorDetails?.Any() == true)
            {
                _logger?.LogWarning("[OCR STEP 1/5] ⚠️  Errors found:");
                foreach (var err in logicResult.ErrorDetails.Take(3))
                {
                    _logger?.LogWarning("      • {ErrorCode}: {ErrorMessage}", err.ErrorCode, err.ErrorMessage);
                }
            }

            var finalResult = new ValidationResultDto();
            finalResult.ErrorDetails.AddRange(logicResult.ErrorDetails ?? new List<ValidationErrorDetail>());
            finalResult.WarningDetails.AddRange(logicResult.WarningDetails ?? new List<ValidationErrorDetail>());

            finalResult.IsReplacement = logicResult.IsReplacement;
            finalResult.ReplacedInvoiceId = logicResult.ReplacedInvoiceId;
            finalResult.NewVersion = logicResult.NewVersion;
            finalResult.MergeMode = logicResult.MergeMode;
            finalResult.MergeTargetInvoiceId = logicResult.MergeTargetInvoiceId;

            // Step 2: Extract OCR Data
            _logger?.LogInformation("[OCR STEP 2/5] 🧠 Extracting invoice data from OCR result...");
            var swExtract = Stopwatch.StartNew();
            finalResult.ExtractedData = _invoiceProcessor.ExtractOcrData(request.OcrResult);
            swExtract.Stop();
            _logger?.LogInformation("[OCR STEP 2/5] ✅ Data extraction completed ({DurationMs}ms)", swExtract.ElapsedMilliseconds);
            _logger?.LogInformation("   └─ InvoiceNumber: {InvoiceNumber}", finalResult.ExtractedData?.InvoiceNumber ?? "N/A");
            _logger?.LogInformation("   └─ SellerTaxCode: {SellerTaxCode}", finalResult.ExtractedData?.SellerTaxCode ?? "N/A");
            _logger?.LogInformation("   └─ BuyerTaxCode: {BuyerTaxCode}", finalResult.ExtractedData?.BuyerTaxCode ?? "N/A");
            _logger?.LogInformation("   └─ TotalAmount: {TotalAmount}", finalResult.ExtractedData?.TotalAmount ?? 0);
            // ConfidenceScore property does not exist on OcrInvoiceResult, so this log line is removed.

            // Step 3: Check for fatal errors
            _logger?.LogInformation("[OCR STEP 3/5] 🛑 Checking for fatal errors...");
            var fatalErrorCodes = new[] { ErrorCodes.LogicDuplicate, ErrorCodes.LogicDuplicateRejected, ErrorCodes.LogicOwner };
            var hasFatalError = finalResult.ErrorDetails.Any(e =>
                !string.IsNullOrEmpty(e.ErrorCode) && fatalErrorCodes.Contains(e.ErrorCode));

            if (hasFatalError)
            {
                _logger?.LogWarning("[OCR STEP 3/5] ❌ FATAL ERROR - Aborting OCR processing");
                var fatalErr = finalResult.ErrorDetails.First(e => !string.IsNullOrEmpty(e.ErrorCode) && fatalErrorCodes.Contains(e.ErrorCode));
                _logger?.LogWarning("   └─ FatalError: {ErrorCode} - {ErrorMessage}", fatalErr.ErrorCode, fatalErr.ErrorMessage);

                if (!string.IsNullOrEmpty(request.S3Key))
                {
                    try 
                    {
                        await _storageService.DeleteFileAsync(request.S3Key);
                        _logger?.LogInformation("Deleted orphaned OCR S3 file due to fatal error: {S3Key}", request.S3Key);
                    }
                    catch (Exception ex) 
                    {
                        _logger?.LogError(ex, "Failed to delete orphaned OCR S3 file: {S3Key}", request.S3Key);
                    }
                }

                overallStopwatch.Stop();
                _logger?.LogWarning("[OCR] ❌ ProcessInvoiceOcrAsync FAILED - Total duration: {TotalMs}ms", overallStopwatch.ElapsedMilliseconds);
                return finalResult;
            }
            _logger?.LogInformation("[OCR STEP 3/5] ✅ No fatal errors detected - proceeding with save");

            // --- Tạo FileStorage cho file OCR (Visual File) ---
            _logger?.LogInformation("[OCR STEP 4/5] 💾 Creating FileStorage record...");
            Guid? visualFileId = null;
            long newFileSizeToConsume = 0;

            if (!string.IsNullOrEmpty(request.S3Key))
            {
                _logger?.LogInformation("   └─ Looking up file by S3Key: {S3Key}", request.S3Key);
                // Check if a FileStorage with this S3Key already exists (unique constraint)
                var existingFile = await _unitOfWork.FileStorages.FindByS3KeyAsync(request.S3Key);
                if (existingFile != null)
                {
                    visualFileId = existingFile.FileId;
                    _logger?.LogInformation("   └─ ℹ️  File already exists in storage: {FileId}", visualFileId);
                }
                else
                {
                    var bucketName = !string.IsNullOrEmpty(request.BucketName) ? request.BucketName :
                                     (Environment.GetEnvironmentVariable("AWS_BUCKET_NAME") ?? _configuration["AWS:BucketName"] ?? "smartinvoice-storage-team-dat");
                    _logger?.LogInformation("   └─ 📦 Creating new FileStorage record");
                    _logger?.LogInformation("      • BucketName: {BucketName}", bucketName);
                    _logger?.LogInformation("      • S3Region: {Region}", _configuration["AWS:Region"] ?? "ap-southeast-1");

                    long s3FileSize = await _storageService.GetFileSizeAsync(request.S3Key);
                    newFileSizeToConsume = s3FileSize;
                    await _quotaService.ValidateStorageQuotaAsync(CompanyId, s3FileSize);

                    var fileStorage = new FileStorage
                    {
                        FileId = Guid.NewGuid(),
                        CompanyId = CompanyId,
                        UploadedBy = UserId,
                        OriginalFileName = request.S3Key.Split('/').Last(),
                        FileExtension = ".jpg",
                        FileSize = s3FileSize,
                        MimeType = "image/jpeg",
                        S3BucketName = bucketName,
                        S3Key = request.S3Key,
                        IsProcessed = true,
                        ProcessedAt = DateTime.UtcNow
                    };
                    visualFileId = fileStorage.FileId;
                    await _unitOfWork.FileStorages.AddAsync(fileStorage);
                    
                    _logger?.LogInformation("      ✅ FileStorage created: {FileId}", visualFileId);
                }
            }

            // ============================================================
            // CASE 3B: INVOICE DOSSIER — OCR ATTACHES TO EXISTING XML RECORD
            // ============================================================
            if (finalResult.MergeMode == DossierMergeMode.OcrAttachesToXml && finalResult.MergeTargetInvoiceId.HasValue)
            {
                _logger?.LogInformation("[OCR STEP 5/5] 🔗 MERGE MODE: Attaching OCR to existing XML record");
                _logger?.LogInformation("   └─ MergeTargetInvoiceId: {TargetInvoiceId}", finalResult.MergeTargetInvoiceId);

                var existingInvoice = await _unitOfWork.Invoices.GetByIdAsync(finalResult.MergeTargetInvoiceId.Value);
                if (existingInvoice == null)
                {
                    _logger?.LogError("   ❌ Target invoice not found in database");
                    finalResult.AddError("ERR_MERGE_FAILED", "Không tìm thấy hóa đơn XML gốc để đính kèm bản thể hiện.");
                    return finalResult;
                }

                _logger?.LogInformation("   ✅ Target invoice found");
                _logger?.LogInformation("      • Current Status: {Status}", existingInvoice.Status);
                _logger?.LogInformation("      • Current RiskLevel: {RiskLevel}", existingInvoice.RiskLevel);
                _logger?.LogInformation("      • OriginalFileId: {OriginalFileId}", existingInvoice.OriginalFileId ?? null);

                // Only attach the visual file — DO NOT override any data
                existingInvoice.VisualFileId = visualFileId;
                existingInvoice.UpdatedAt = DateTime.UtcNow;

                _logger?.LogInformation("   📝 Updating VisualFileId to: {VisualFileId}", visualFileId);

                // Audit Log — add directly to AuditLogs table (not through navigation property)
                var mergeUser = await _unitOfWork.Users.GetByIdAsync(UserId);
                var auditLog = new InvoiceAuditLog
                {
                    AuditId = Guid.NewGuid(),
                    InvoiceId = existingInvoice.InvoiceId,
                    CompanyId = existingInvoice.CompanyId,
                    InvoiceNumber = existingInvoice.InvoiceNumber,
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
                if (newFileSizeToConsume > 0)
                {
                    await _quotaService.ConsumeStorageQuotaAsync(CompanyId, newFileSizeToConsume);
                }

                finalResult.InvoiceId = existingInvoice.InvoiceId;
                // Clear any non-critical errors/warnings from OCR validation since we didn't use any of that data
                finalResult.ErrorDetails.Clear();
                finalResult.WarningDetails.Clear();

                overallStopwatch.Stop();
                _logger?.LogInformation("[OCR] ✅ MERGE COMPLETED Successfully");
                _logger?.LogInformation("   └─ Result: InvoiceId={InvoiceId}", existingInvoice.InvoiceId);
                _logger?.LogInformation("   └─ Total duration: {TotalMs}ms", overallStopwatch.ElapsedMilliseconds);
                _logger?.LogInformation("═══════════════════════════════════════════════════════════════\n");
                return finalResult;
            }

            // ============================================================
            // NORMAL FLOW: CREATE NEW OCR-ONLY INVOICE (Yellow Risk)
            // ============================================================
            _logger?.LogInformation("[OCR STEP 5/5] 💾 Creating new invoice record (normal flow)");

            var docTypes = await _unitOfWork.DocumentTypes.GetAllAsync();
            var docTypeId = 1;

            var typeStr = request.OcrResult?.Invoice?.Type?.Value?.ToUpper();
            if (typeStr != null && typeStr.Contains("BÁN HÀNG"))
            {
                var saleType = docTypes.FirstOrDefault(d => d.TypeCode == "SALE" || d.FormTemplate == "02GTTT");
                docTypeId = saleType?.DocumentTypeId ?? 2;
                _logger?.LogInformation("   └─ DocumentType: SALE (ID: {TypeId})", docTypeId);
            }
            else
            {
                var gtgtType = docTypes.FirstOrDefault(d => d.TypeCode == "GTGT" || d.FormTemplate == "01GTKT");
                docTypeId = gtgtType?.DocumentTypeId ?? 1;
                _logger?.LogInformation("   └─ DocumentType: GTGT (ID: {TypeId})", docTypeId);
            }

            var invoiceId = Guid.NewGuid();
            var isInvoiceValid = finalResult.IsValid;
            var riskLevel = !isInvoiceValid ? "Red" : "Yellow";

            _logger?.LogInformation("   ✅ New InvoiceId generated: {InvoiceId}", invoiceId);
            _logger?.LogInformation("   └─ IsValid: {IsValid}", isInvoiceValid);
            _logger?.LogInformation("   └─ RiskLevel: {RiskLevel}", riskLevel);

            // OCR-only: VisualFileId gets the image, OriginalFileId stays null (no XML yet)
            // Always add WARN_MISSING_XML_EVIDENCE for OCR-only uploads
            if (isInvoiceValid)
            {
                finalResult.AddWarning("WARN_MISSING_XML_EVIDENCE",
                    "Hóa đơn được trích xuất từ ảnh/PDF bằng AI. Để đảm bảo 100% tính pháp lý khi khai thuế, bạn cần bổ sung file XML gốc.",
                    "Tải lên file XML gốc của hóa đơn này để hệ thống xác thực chữ ký số và cập nhật dữ liệu chính xác.");
                _logger?.LogInformation("   ⚠️  Added warning: WARN_MISSING_XML_EVIDENCE");
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
                RawData = new InvoiceRawData
                {
                    ObjectKey = request.S3Key,
                    OcrJobId = request.S3Key
                },
                ExtractedData = finalResult.ExtractedData,
                Status = hasFatalError ? nameof(InvoiceStatus.Rejected) : nameof(InvoiceStatus.Draft),
                // OCR-only: Force Yellow risk even if math is correct (missing XML evidence)
                RiskLevel = !isInvoiceValid ? "Red" : "Yellow",
                Notes = !isInvoiceValid ? "Hóa đơn có lỗi trích xuất, vui lòng kiểm tra lại."
                        : "Dữ liệu được trích xuất từ OCR, bạn nên bổ sung file XML để đảm bảo tính pháp lý.",
                Version = finalResult.NewVersion,
                Workflow = new InvoiceWorkflow { UploadedBy = UserId },
                CreatedAt = DateTime.UtcNow
            };

            _logger?.LogInformation("   📋 Invoice properties populated:");
            _logger?.LogInformation("      • InvoiceNumber: {Number}", invoice.InvoiceNumber);
            _logger?.LogInformation("      • SerialNumber: {Serial}", invoice.SerialNumber);
            _logger?.LogInformation("      • SellerTax: {SellerTax}", invoice.Seller?.TaxCode ?? "N/A");
            _logger?.LogInformation("      • BuyerTax: {BuyerTax}", invoice.Buyer?.TaxCode ?? "N/A");
            _logger?.LogInformation("      • Amount: {Amount}", invoice.TotalAmount);
            _logger?.LogInformation("      • Status: {Status}", invoice.Status);
            _logger?.LogInformation("      • RiskLevel: {RiskLevel}", invoice.RiskLevel);

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

            // Add Structure and Signature checks for OCR
            invoice.CheckResults.Add(new InvoiceCheckResult
            {
                CheckId = Guid.NewGuid(),
                InvoiceId = invoiceId,
                Category = "STRUCTURE",
                CheckName = "Structure",
                CheckOrder = 1,
                IsValid = true,
                Status = "Pass",
                ErrorMessage = "Thông tin bóc tách từ hình ảnh/PDF (OCR)",
                DurationMs = 0
            });

            invoice.CheckResults.Add(new InvoiceCheckResult
            {
                CheckId = Guid.NewGuid(),
                InvoiceId = invoiceId,
                Category = "SIGNATURE",
                CheckName = "Signature",
                CheckOrder = 2,
                IsValid = true,
                Status = "Warning",
                ErrorCode = "WARN_MISSING_XML_EVIDENCE",
                ErrorMessage = "Thiếu file XML gốc để xác thực chữ ký số.",
                Suggestion = "Vui lòng tải lên file XML để đảm bảo tính pháp lý cao nhất.",
                DurationMs = 0
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

            var uploadUser = await _unitOfWork.Users.GetByIdAsync(Guid.Parse(userId));
            invoice.AuditLogs.Add(new InvoiceAuditLog
            {
                AuditId = Guid.NewGuid(),
                InvoiceId = invoiceId,
                InvoiceNumber = invoice.InvoiceNumber,
                CompanyId = Guid.Parse(companyId),
                UserId = Guid.Parse(userId),
                UserEmail = uploadUser?.Email,
                UserRole = uploadUser?.Role,
                Action = "UPLOAD_OCR",
                Changes = new List<AuditChange>
                {
                    new() { Field = "Status", OldValue = null, NewValue = isInvoiceValid ? "Draft" : "Rejected", ChangeType = "INSERT" },
                    new() { Field = "RiskLevel", OldValue = null, NewValue = invoice.RiskLevel, ChangeType = "INSERT" }
                },
                Comment = isInvoiceValid ? "Dữ liệu OCR đã được trích xuất thành công. Vui lòng bổ sung XML nếu có." : "Trích xuất OCR hoàn tất nhưng dữ liệu không hợp lệ."
            });

            _logger?.LogInformation("   💾 Saving invoice to database...");
            await _unitOfWork.Invoices.AddAsync(invoice);
            await _unitOfWork.CompleteAsync();
            if (newFileSizeToConsume > 0)
            {
                await _quotaService.ConsumeStorageQuotaAsync(CompanyId, newFileSizeToConsume);
            }

            // Publish SQS message for VietQR validation if Seller TaxCode is available
            if (!string.IsNullOrEmpty(invoice.Seller?.TaxCode) && await _configProvider.GetBoolAsync("ENABLE_VIETQR_VALIDATION", true))
            {
                try
                {
                    var sqsMessage = new SmartInvoice.API.DTOs.SQS.VietQrValidationMessage
                    {
                        InvoiceId = invoice.InvoiceId,
                        TaxCode = invoice.Seller.TaxCode,
                        SellerName = invoice.Seller.Name
                    };
                    await _sqsPublisher.PublishVietQrValidationAsync(sqsMessage, CancellationToken.None);
                    _logger?.LogInformation("Invoice {InvoiceId} saved and VietQR validation message published to SQS successfully.", invoice.InvoiceId);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Invoice {InvoiceId} saved successfully, but failed to publish VietQR validation message to SQS.", invoice.InvoiceId);
                }
            }
            else if (!string.IsNullOrEmpty(invoice.Seller?.TaxCode))
            {
                _logger?.LogInformation("VietQR validation is disabled via configuration. Skipping SQS publish for Invoice {InvoiceId}.", invoice.InvoiceId);
            }

            finalResult.InvoiceId = invoiceId;
            overallStopwatch.Stop();
            _logger?.LogInformation("[OCR] ✅ ProcessInvoiceOcrAsync COMPLETED Successfully");
            _logger?.LogInformation("   └─ InvoiceId: {InvoiceId}", invoiceId);
            _logger?.LogInformation("   └─ Status: {Status}", invoice.Status);
            _logger?.LogInformation("   └─ RiskLevel: {RiskLevel}", invoice.RiskLevel);
            _logger?.LogInformation("   └─ Total Processing Time: {TotalMs}ms", overallStopwatch.ElapsedMilliseconds);
            _logger?.LogInformation("═══════════════════════════════════════════════════════════════\n");
            return finalResult;
        }
    }
}
