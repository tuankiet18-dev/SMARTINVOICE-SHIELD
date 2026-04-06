using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SmartInvoice.API.Data;
using SmartInvoice.API.DTOs;
using SmartInvoice.API.DTOs.Invoice;
using SmartInvoice.API.DTOs.SQS;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Entities.JsonModels;
using SmartInvoice.API.Services;
using SmartInvoice.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using SmartInvoice.API.Enums;

namespace SmartInvoice.API.Controller
{
    [ApiController]
    [Route("api/invoices")]
    [Authorize]
    public class InvoicesController : ControllerBase
    {
        private readonly StorageService _storageService;
        private readonly IInvoiceProcessorService _invoiceProcessor;
        private readonly IInvoiceService _invoiceService;
        private readonly IQuotaService _quotaService;
        private readonly ISystemConfigProvider _configProvider;
        private readonly IAwsS3Service _s3Service;
        private readonly ISqsService _sqsService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<InvoicesController> _logger;

        [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
        public InvoicesController(
            StorageService storageService,
            IInvoiceProcessorService invoiceProcessor,
            IInvoiceService invoiceService,
            IQuotaService quotaService,
            ISystemConfigProvider configProvider,
            IAwsS3Service s3Service,
            ISqsService sqsService,
            IConfiguration configuration,
            ILogger<InvoicesController> logger)
        {
            _storageService = storageService;
            _invoiceProcessor = invoiceProcessor;
            _invoiceService = invoiceService;
            _quotaService = quotaService;
            _configProvider = configProvider;
            _s3Service = s3Service;
            _sqsService = sqsService;
            _configuration = configuration;
            _logger = logger;
        }

        // ================================================
        //  HELPER: Extract user claims
        // ================================================

        private (Guid UserId, Guid CompanyId, string UserRole, string UserEmail) GetUserInfo()
        {
            var userIdStr = User.FindFirst("UserId")?.Value;
            var companyIdStr = User.FindFirst("CompanyId")?.Value;
            var userRole = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? "Member";
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value ?? "unknown";

            if (string.IsNullOrEmpty(userIdStr) || string.IsNullOrEmpty(companyIdStr))
                throw new UnauthorizedAccessException("User identity or company information is missing in token.");

            return (Guid.Parse(userIdStr), Guid.Parse(companyIdStr), userRole, userEmail);
        }

        private string? GetClientIp() =>
            HttpContext.Connection.RemoteIpAddress?.ToString();

        // ================================================
        //  UPLOAD & PROCESS
        // ================================================

        [HttpPost("generate-upload-url")]
        [Authorize(Policy = Constants.Permissions.InvoiceUpload)]
        public async Task<IActionResult> GetUploadUrl([FromBody] UploadRequestDto request)
        {
            var maxFileSizeMb = await _configProvider.GetIntAsync("MAX_UPLOAD_SIZE_MB", 10);
            if (request.FileSize > (long)maxFileSizeMb * 1024 * 1024)
            {
                return BadRequest(new { Message = $"Dung lượng file vượt quá giới hạn cho phép ({maxFileSizeMb} MB)." });
            }

            var result = _storageService.GeneratePresignedUrl(request.FileName, request.ContentType);
            return Ok(new { UploadUrl = result.Url, S3Key = result.Key });
        }

        [HttpPost("process-xml")]
        [Authorize(Policy = Constants.Permissions.InvoiceUpload)]
        public async Task<IActionResult> ProcessXml([FromBody] ProcessXmlRequestDto request)
        {
            if (string.IsNullOrEmpty(request.S3Key))
                return BadRequest(new { Message = "S3Key is required." });

            try
            {
                var (userId, companyId, _, _) = GetUserInfo();

                // Quota check: lazy reset + consume
                await _quotaService.ValidateAndConsumeInvoiceQuotaAsync(companyId);

                var finalResult = await _invoiceService.ProcessInvoiceXmlAsync(request.S3Key, userId.ToString(), companyId.ToString());
                return Ok(finalResult);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { Message = "Thông tin định danh người dùng hoặc công ty không hợp lệ." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Lỗi hệ thống nội bộ." });
            }
        }

        [HttpPost("process-ocr")]
        [Authorize(Policy = Constants.Permissions.InvoiceUpload)]
        public async Task<IActionResult> ProcessOcr([FromBody] ProcessOcrRequestDto request)
        {
            if (request.OcrResult == null)
                return BadRequest(new { Message = "OCR data is required." });

            try
            {
                var (userId, companyId, _, _) = GetUserInfo();
                var finalResult = await _invoiceService.ProcessInvoiceOcrAsync(request, userId.ToString(), companyId.ToString());
                return Ok(finalResult);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { Message = "Thông tin định danh người dùng hoặc công ty không hợp lệ." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "Lỗi hệ thống nội bộ." });
            }
        }

        // ════════════════════════════════════════════
        //  UPLOAD IMAGE → S3 → SQS (Async OCR Pipeline)
        // ════════════════════════════════════════════

        [HttpGet("debug-config")]
        [AllowAnonymous]
        public async Task<IActionResult> DebugConfig([FromServices] SmartInvoice.API.Repositories.Interfaces.IUnitOfWork unitOfWork)
        {
            var sqsUrl = _configuration["AWS_SQS_OCR_URL"];
            var ocrEndpoint = _configuration["OCR_API_ENDPOINT"];
            
            var allInvoices = await unitOfWork.Invoices.GetAllAsync();
            var recentInvoices = allInvoices
                .OrderByDescending(i => i.CreatedAt)
                .Take(5)
                .Select(i => new { 
                    i.InvoiceId, 
                    i.Status, 
                    CreatedAt = i.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"), 
                    OriginalFileName = i.RawData != null ? i.RawData.ObjectKey : "Unknown",
                    Notes = i.Notes
                })
                .ToList();

            return Ok(new
            {
                HasSqsUrl = !string.IsNullOrEmpty(sqsUrl),
                SqsUrl = sqsUrl,
                OcrEndpoint = ocrEndpoint,
                RecentInvoices = recentInvoices
            });
        }

        [HttpPost("upload-image")]
        [Authorize(Policy = Constants.Permissions.InvoiceUpload)]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            // ── Validate file ──
            if (file == null || file.Length == 0)
                return BadRequest(new { Message = "Vui lòng chọn file ảnh hóa đơn." });

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf", ".tiff", ".tif" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
                return BadRequest(new { Message = $"File không hợp lệ. Chỉ chấp nhận: {string.Join(", ", allowedExtensions)}" });

            var maxFileSizeMb = await _configProvider.GetIntAsync("MAX_UPLOAD_SIZE_MB", 10);
            if (file.Length > (long)maxFileSizeMb * 1024 * 1024)
                return BadRequest(new { Message = $"Dung lượng file vượt quá giới hạn cho phép ({maxFileSizeMb} MB)." });

            try
            {
                var (userId, companyId, _, _) = GetUserInfo();

                // Quota check
                await _quotaService.ValidateAndConsumeInvoiceQuotaAsync(companyId);

                // ── 1. Upload to S3 ──
                string s3Key;
                using (var stream = file.OpenReadStream())
                {
                    s3Key = await _s3Service.UploadInvoiceImageAsync(stream, file.FileName, file.ContentType, companyId);
                }

                // ── 2. Create draft Invoice (Status: Processing) ──
                var invoiceId = Guid.NewGuid();
                var invoice = new Invoice
                {
                    InvoiceId = invoiceId,
                    CompanyId = companyId,
                    DocumentTypeId = 1, // Default GTGT; will be updated by OCR worker
                    ProcessingMethod = "API",
                    InvoiceNumber = "PROCESSING",
                    Status = "Processing",
                    RiskLevel = "Yellow",
                    Notes = "Đang xử lý OCR...",
                    VisualFileId = null,
                    RawData = new InvoiceRawData { ObjectKey = s3Key, OcrJobId = s3Key },
                    Workflow = new InvoiceWorkflow { UploadedBy = userId },
                    InvoiceDate = DateTime.UtcNow,
                    InvoiceCurrency = "VND",
                    ExchangeRate = 1,
                    CreatedAt = DateTime.UtcNow
                };

                // Use InvoiceService's UnitOfWork indirectly — inject via a direct DB call
                await _invoiceService.CreateDraftInvoiceAsync(invoice);

                // ── 3. Publish OCR job to SQS ──
                var ocrQueueUrl = _configuration["AWS_SQS_OCR_URL"];
                _logger.LogInformation(">>> [UPLOAD_API] Sending OCR message to Queue: {QueueUrl}", ocrQueueUrl);
                if (!string.IsNullOrEmpty(ocrQueueUrl))
                {
                    await _sqsService.SendMessageAsync(new OcrJobMessage
                    {
                        InvoiceId = invoiceId,
                        S3Key = s3Key,
                        CompanyId = companyId,
                        UserId = userId,
                        FileName = file.FileName
                    }, ocrQueueUrl);
                }

                // ── 4. Return 202 Accepted ──
                return Accepted(new
                {
                    InvoiceId = invoiceId,
                    S3Key = s3Key,
                    Status = "Processing",
                    Message = "Hóa đơn đã được tải lên thành công và đang được xử lý OCR."
                });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { Message = "Thông tin định danh người dùng hoặc công ty không hợp lệ." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "Lỗi hệ thống nội bộ." });
            }
        }

        [HttpPost("test-process-local")]
        [Authorize(Policy = Constants.Permissions.InvoiceUpload)]
        public async Task<IActionResult> TestProcessLocal(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Vui lòng chọn 1 file XML để test.");

            var tempFilePath = Path.GetTempFileName();
            try
            {
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                    await file.CopyToAsync(stream);

                var structResult = _invoiceProcessor.ValidateStructure(tempFilePath);
                if (!structResult.IsValid) return BadRequest(structResult);

                var xmlDoc = new System.Xml.XmlDocument();
                xmlDoc.PreserveWhitespace = true;
                xmlDoc.Load(tempFilePath);

                var sigResult = _invoiceProcessor.VerifyDigitalSignature(xmlDoc);
                if (!sigResult.IsValid) return BadRequest(sigResult);

                var logicResult = await _invoiceProcessor.ValidateBusinessLogicAsync(xmlDoc);
                logicResult.SignerSubject = sigResult.SignerSubject;
                logicResult.ExtractedData = _invoiceProcessor.ExtractData(xmlDoc, logicResult);

                return Ok(logicResult);
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "Lỗi hệ thống nội bộ." });
            }
            finally
            {
                if (System.IO.File.Exists(tempFilePath))
                    System.IO.File.Delete(tempFilePath);
            }
        }

        // ================================================
        //  LIST & DETAIL
        // ================================================

        [HttpGet]
        [Authorize(Policy = Constants.Permissions.InvoiceView)]
        public async Task<IActionResult> GetInvoices([FromQuery] GetInvoicesQueryDto query)
        {
            try
            {
                var (userId, companyId, userRole, _) = GetUserInfo();
                var result = await _invoiceService.GetInvoicesAsync(query, companyId, userId, userRole);
                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { Message = "Thông tin định danh người dùng hoặc công ty không hợp lệ." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "Lỗi hệ thống nội bộ khi lấy danh sách hóa đơn." });
            }
        }

        [HttpGet("trash")]
        [Authorize(Policy = Constants.Permissions.InvoiceView)]
        public async Task<IActionResult> GetTrashInvoices([FromQuery] GetInvoicesQueryDto query)
        {
            try
            {
                var (userId, companyId, userRole, _) = GetUserInfo();
                var result = await _invoiceService.GetTrashInvoicesAsync(query, companyId, userId, userRole);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "Lỗi hệ thống nội bộ khi lấy danh sách hóa đơn trong thùng rác." });
            }
        }

        [HttpPost("{id:guid}/restore")]
        [Authorize(Policy = Constants.Permissions.InvoiceEdit)]
        public async Task<IActionResult> RestoreInvoice(Guid id)
        {
            try
            {
                var (userId, companyId, userRole, _) = GetUserInfo();
                var success = await _invoiceService.RestoreInvoiceAsync(id, companyId, userId, userRole);
                if (!success) return NotFound(new { Message = "Không tìm thấy hóa đơn trong thùng rác hoặc không có quyền." });
                return Ok(new { Message = "Phục hồi thành công." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "Lỗi hệ thống nội bộ khi phục hồi hóa đơn." });
            }
        }

        [HttpDelete("{id:guid}/hard")]
        [Authorize(Policy = Constants.Permissions.InvoiceEdit)]
        public async Task<IActionResult> HardDeleteInvoice(Guid id)
        {
            try
            {
                var (userId, companyId, userRole, _) = GetUserInfo();
                var success = await _invoiceService.HardDeleteInvoiceAsync(id, companyId, userId, userRole);
                if (!success) return NotFound(new { Message = "Không tìm thấy hóa đơn trong thùng rác hoặc không có quyền." });
                return Ok(new { Message = "Xóa vĩnh viễn thành công. Đã hoàn trả dung lượng." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "Lỗi hệ thống nội bộ khi thực hiện xóa vĩnh viễn." });
            }
        }

        [HttpDelete("trash/empty")]
        [Authorize(Policy = Constants.Permissions.InvoiceEdit)]
        public async Task<IActionResult> EmptyTrash()
        {
            try
            {
                var (userId, companyId, userRole, _) = GetUserInfo();
                var deletedCount = await _invoiceService.EmptyTrashAsync(companyId, userId, userRole);
                return Ok(new { Message = $"Đã xóa vĩnh viễn {deletedCount} hóa đơn. Dung lượng đã được hoàn trả.", DeletedCount = deletedCount });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Lỗi hệ thống nội bộ khi dọn thùng rác.", Error = ex.Message });
            }
        }

        [HttpGet("{id:guid}")]
        [Authorize(Policy = Constants.Permissions.InvoiceView)]
        public async Task<IActionResult> GetInvoiceById(Guid id)
        {
            try
            {
                var (userId, companyId, userRole, _) = GetUserInfo();
                var detail = await _invoiceService.GetInvoiceDetailAsync(id, companyId, userId, userRole);

                if (detail == null)
                    return NotFound(new { Message = $"Không tìm thấy hóa đơn với ID: {id}" });

                return Ok(detail);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { Message = "Thông tin định danh người dùng hoặc công ty không hợp lệ." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "Lỗi hệ thống nội bộ khi lấy chi tiết hóa đơn." });
            }
        }

        [HttpGet("{id}/visual")]
        [Authorize(Policy = Constants.Permissions.InvoiceView)]
        public async Task<IActionResult> GetInvoiceVisual(Guid id)
        {
            try
            {
                var (_, companyId, _, _) = GetUserInfo();
                var url = await _invoiceService.GetVisualFileUrlAsync(id, companyId);
                
                if (string.IsNullOrEmpty(url))
                    return NotFound(new { Message = "Không tìm thấy file ảnh/PDF cho hóa đơn này." });

                return Ok(new { Url = url });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { Message = "Thông tin định danh người dùng hoặc công ty không hợp lệ." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "Lỗi hệ thống nội bộ khi lấy đường dẫn file." });
            }
        }

        // ════════════════════════════════════════════
        //  CRUD
        // ================================================

        [HttpGet("{id}/versions")]
        [Authorize(Policy = Constants.Permissions.InvoiceView)]
        public async Task<IActionResult> GetInvoiceVersions(Guid id)
        {
            try
            {
                var (userId, companyId, _, _) = GetUserInfo();
                var versions = await _invoiceService.GetInvoiceVersionsAsync(id, companyId);
                return Ok(versions);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { Message = "Thông tin định danh người dùng hoặc công ty không hợp lệ." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "Lỗi hệ thống nội bộ khi lấy lịch sử phiên bản." });
            }
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = Constants.Permissions.InvoiceEdit)]
        public async Task<IActionResult> UpdateInvoice(Guid id, [FromBody] UpdateInvoiceDto request)
        {
            try
            {
                var (userId, _, userRole, userEmail) = GetUserInfo();
                await _invoiceService.UpdateInvoiceAsync(id, request, userId, userEmail, userRole, GetClientIp());
                return Ok(new { Message = "Cập nhật hóa đơn thành công." });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { Message = "Không tìm thấy hóa đơn" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Policy = Constants.Permissions.InvoiceEdit)]
        public async Task<IActionResult> DeleteInvoice(Guid id)
        {
            try
            {
                var (userId, companyId, userRole, _) = GetUserInfo();
                var isDeleted = await _invoiceService.DeleteInvoiceAsync(id, companyId, userId, userRole);

                if (!isDeleted)
                    return NotFound(new { Message = $"Không tìm thấy hóa đơn với ID: {id}" });

                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Lỗi khi xóa hóa đơn", Error = ex.Message });
            }
        }

        // ================================================
        //  WORKFLOW
        // ================================================

        [HttpPost("{id:guid}/submit")]
        [Authorize(Policy = Constants.Permissions.InvoiceUpload)]
        public async Task<IActionResult> SubmitInvoice(Guid id, [FromBody] SubmitInvoiceDto? request)
        {
            try
            {
                var (userId, companyId, userRole, userEmail) = GetUserInfo();
                await _invoiceService.SubmitInvoiceAsync(id, companyId, userId, userEmail, userRole, request?.Comment, GetClientIp());
                return Ok(new { Message = "Hóa đơn đã được gửi duyệt thành công." });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { Message = "Không tìm thấy hóa đơn." });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        [HttpPost("submit-batch")]
        [Authorize(Policy = Constants.Permissions.InvoiceUpload)]
        public async Task<IActionResult> SubmitBatch([FromBody] SubmitBatchDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var (userId, companyId, userRole, userEmail) = GetUserInfo();
                var result = await _invoiceService.SubmitBatchAsync(request.InvoiceIds, companyId, userId, userEmail, userRole, request.Comment, GetClientIp());
                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { Message = "User identity or company information is missing in token." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        [HttpPost("{id:guid}/approve")]
        [Authorize(Policy = Constants.Permissions.InvoiceApprove)]
        public async Task<IActionResult> ApproveInvoice(Guid id, [FromBody] ApproveInvoiceDto? request)
        {
            try
            {
                var (userId, companyId, userRole, userEmail) = GetUserInfo();
                await _invoiceService.ApproveInvoiceAsync(id, companyId, userId, userEmail, userRole, request?.Comment, GetClientIp());
                return Ok(new { Message = "Hóa đơn đã được duyệt thành công." });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { Message = "Không tìm thấy hóa đơn." });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        [HttpPost("{id:guid}/reject")]
        [Authorize(Policy = Constants.Permissions.InvoiceReject)]
        public async Task<IActionResult> RejectInvoice(Guid id, [FromBody] RejectInvoiceDto request)
        {
            try
            {
                var (userId, companyId, userRole, userEmail) = GetUserInfo();
                await _invoiceService.RejectInvoiceAsync(id, companyId, userId, userEmail, userRole, request.Reason, request.Comment, GetClientIp());
                return Ok(new { Message = "Hóa đơn đã bị từ chối." });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { Message = "Không tìm thấy hóa đơn." });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        // ================================================
        //  AUDIT LOG
        // ================================================

        [HttpGet("{id:guid}/audit-logs")]
        [Authorize(Policy = Constants.Permissions.InvoiceView)]
        public async Task<IActionResult> GetAuditLogs(Guid id)
        {
            try
            {
                var logs = await _invoiceService.GetAuditLogsAsync(id);
                return Ok(logs);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { Message = "Không tìm thấy hóa đơn." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        [HttpGet("stats")]
        [Authorize(Policy = Constants.Permissions.InvoiceView)]
        public async Task<IActionResult> GetInvoiceStats([FromQuery] DateTime startDate, [FromQuery] DateTime endDate, [FromQuery] string? status)
        {
            try
            {
                var (_, companyId, _, _) = GetUserInfo();
                var stats = await _invoiceService.GetInvoiceStatsAsync(startDate, endDate, status, companyId);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }
    }
}
