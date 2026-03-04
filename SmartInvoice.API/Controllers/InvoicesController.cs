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
using SmartInvoice.API.Entities;
using SmartInvoice.API.Entities.JsonModels;
using SmartInvoice.API.Services;
using SmartInvoice.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using SmartInvoice.API.Enums;
using SmartInvoice.API.DTOs.Invoice;

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

        [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
        public InvoicesController(StorageService storageService, IInvoiceProcessorService invoiceProcessor, IInvoiceService invoiceService)
        {
            _storageService = storageService;
            _invoiceProcessor = invoiceProcessor;
            _invoiceService = invoiceService;
        }

        // API 1: Lấy link upload (Frontend gọi cái này trước)
        [HttpPost("generate-upload-url")]
        [Authorize(Policy = Constants.Permissions.InvoiceUpload)]
        public IActionResult GetUploadUrl([FromBody] UploadRequestDto request)
        {
            var result = _storageService.GeneratePresignedUrl(request.FileName, request.ContentType);
            // Trả về URL để FE dùng PUT upload file, kèm S3Key để FE gửi lại sau khi upload xong
            return Ok(new { UploadUrl = result.Url, S3Key = result.Key });
        }

        [HttpPost("process-xml")]
        [Authorize(Policy = Constants.Permissions.InvoiceUpload)]
        public async Task<IActionResult> ProcessXml([FromBody] ProcessXmlRequestDto request)
        {
            if (string.IsNullOrEmpty(request.S3Key))
            {
                return BadRequest(new { Message = "S3Key is required." });
            }

            try
            {
                var userIdStr = User.FindFirst("UserId")?.Value;
                var companyIdStr = User.FindFirst("CompanyId")?.Value;

                if (string.IsNullOrEmpty(userIdStr) || string.IsNullOrEmpty(companyIdStr))
                {
                    return Unauthorized(new { Message = "User identity or company information is missing in token." });
                }

                var finalResult = await _invoiceService.ProcessInvoiceXmlAsync(request.S3Key, userIdStr, companyIdStr);

                // Always return Ok (200) since the processing was technically completed and recorded
                return Ok(finalResult);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Internal server error: {ex.Message}" });
            }
        }

        // API HỖ TRỢ TEST NHANH TRÊN SWAGGER (Bỏ qua S3)
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
                {
                    await file.CopyToAsync(stream);
                }

                var structResult = _invoiceProcessor.ValidateStructure(tempFilePath);
                if (!structResult.IsValid) return BadRequest(structResult);

                var xmlDoc = new System.Xml.XmlDocument();
                xmlDoc.PreserveWhitespace = true;
                xmlDoc.Load(tempFilePath);

                var sigResult = _invoiceProcessor.VerifyDigitalSignature(xmlDoc);
                if (!sigResult.IsValid) return BadRequest(sigResult);

                var logicResult = await _invoiceProcessor.ValidateBusinessLogicAsync(xmlDoc);
                logicResult.SignerSubject = sigResult.SignerSubject;

                // Move data extraction before BadRequest to ensure details are returned
                logicResult.ExtractedData = _invoiceProcessor.ExtractData(xmlDoc);

                // Always return Ok (200) for successful simulation
                return Ok(logicResult);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Lỗi: {ex.Message}" });
            }
            finally
            {
                if (System.IO.File.Exists(tempFilePath))
                    System.IO.File.Delete(tempFilePath);
            }
        }

        // API 2: Tạo hóa đơn (Gọi sau khi upload xong)
        [HttpPost]
        [Authorize(Policy = Constants.Permissions.InvoiceUpload)]
        public async Task<IActionResult> CreateInvoice()
        {
            // Tạm thời hard-code để test, sau này sẽ lấy từ Body gửi lên
            var invoice = new Invoice
            {
                InvoiceId = Guid.NewGuid(),
                InvoiceNumber = "INV-TEST-01",
                Status = "Pending",
                CompanyId = Guid.NewGuid(), // Fake ID: Will need a real company in DB to save
                UploadedBy = Guid.Empty // Placeholder: Will be replaced by actual user ID from context
            };

            await _invoiceService.CreateInvoiceAsync(invoice);

            return Ok(invoice);
        }

        // API 3: Lấy danh sách hóa đơn (Frontend gọi để hiển thị lên bảng)
        // [HttpGet]
        // public IActionResult GetInvoices()
        // {
        //     // Tạm thời trả về mock data theo chuẩn cấu trúc của DB nếu thực tế chưa có CSDL (để phục vụ giao diện FE trước)
        //     var mockInvoices = Enumerable.Range(1, 20).Select(i =>
        //     {
        //         var risks = new[] { "Green", "Green", "Green", "Yellow", "Orange", "Red", "Green", "Yellow" };
        //         var statuses = new[] { "Approved", "Pending", "Draft", "Approved", "Rejected", "Approved", "Pending", "Approved" };
        //         var types = new[] { "01GTKT", "02GTTT", "01GTKT", "01GTKT", "02GTTT" };
        //         var sellers = new[]
        //         {
        //             "Công ty TNHH Thương mại ABC",
        //             "Công ty CP Công nghệ XYZ",
        //             "DN Tư nhân Phát Đạt",
        //             "Công ty TNHH SX Minh Tâm",
        //             "Công ty CP Vận tải An Bình"
        //         };

        //         return new
        //         {
        //             key = i.ToString(),
        //             invoiceNo = $"INV-2026-{(1284 - i).ToString("D6")}",
        //             type = types[i % types.Length],
        //             seller = sellers[i % sellers.Length],
        //             mst = $"0{new Random().Next(100000000, 999999999)}",
        //             amount = $"{(new Random().Next(1, 90) * 1000000).ToString("N0")} ₫",
        //             date = $"{(12 - (i / 3)).ToString("D2")}/02/2026",
        //             status = statuses[i % statuses.Length],
        //             risk = risks[i % risks.Length],
        //             method = i % 4 == 0 ? "OCR" : "XML"
        //         };
        //     }).ToList();

        //     return Ok(mockInvoices);
        // }

        [HttpGet]
        [Authorize(Policy = Constants.Permissions.InvoiceView)]
        public async Task<IActionResult> GetInvoices([FromQuery] GetInvoicesQueryDto query)
        {
            try
            {
                var userIdStr = User.FindFirst("UserId")?.Value;
                var companyIdStr = User.FindFirst("CompanyId")?.Value;
                var userRole = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value;

                if (string.IsNullOrEmpty(userIdStr) || string.IsNullOrEmpty(companyIdStr))
                {
                    return Unauthorized(new { Message = "User identity or company information is missing in token." });
                }

                Guid userId = Guid.Parse(userIdStr);
                Guid companyId = Guid.Parse(companyIdStr);

                var result = await _invoiceService.GetInvoicesAsync(query, companyId, userId, userRole ?? "Member");
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetInvoiceById(Guid id)
        {
            try
            {
                var invoice = await _invoiceService.GetInvoiceByIdAsync(id);

                if (invoice == null)
                {
                    return NotFound(new { Message = $"Không tìm thấy hóa đơn với ID: {id}" });
                }

                return Ok(invoice);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Lỗi server nội bộ", Error = ex.Message });
            }
        }

        [HttpPost("upload")]
        public IActionResult UploadInvoice(IFormFile file)
        {
            return Problem("Chức năng đang phát triển", statusCode: 501);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateInvoice(Guid id, [FromBody] UpdateInvoiceDto request)
        {
            try
            {
                await _invoiceService.UpdateInvoiceAsync(id, request);

                return Ok(new { Message = "Cập nhật thành công" });
            }
            catch (KeyNotFoundException) // Bắt cái lỗi mình ném ra ở Service
            {
                return NotFound(new { Message = "Không tìm thấy hóa đơn" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteInvoice(Guid id)
        {
            try
            {
                var isDeleted = await _invoiceService.DeleteInvoiceAsync(id);

                if (!isDeleted)
                {
                    return NotFound(new { Message = $"Không tìm thấy hóa đơn với ID: {id}" });
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Lỗi khi xóa hóa đơn", Error = ex.Message });
            }
        }

        [HttpPost("{id}/validate")]
        public IActionResult ValidateInvoice(Guid id)
        {
            return Problem("Chức năng đang phát triển", statusCode: 501);
        }

        [HttpPost("{id}/submit")]
        public IActionResult SubmitInvoice(Guid id)
        {
            return Problem("Chức năng đang phát triển", statusCode: 501);
        }

        [HttpPost("{id}/approve")]
        public IActionResult ApproveInvoice(Guid id)
        {
            return Problem("Chức năng đang phát triển", statusCode: 501);
        }

        [HttpPost("{id}/reject")]
        public IActionResult RejectInvoice(Guid id, [FromBody] object reason) // object tạm, sau này thay bằng DTO
        {
            return Problem("Chức năng đang phát triển", statusCode: 501);
        }

        [HttpGet("{id}/audit-logs")]
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
    }
}