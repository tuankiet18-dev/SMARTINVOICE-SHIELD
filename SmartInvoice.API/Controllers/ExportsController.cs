using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInvoice.API.DTOs.Export;
using SmartInvoice.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace SmartInvoice.API.Controllers;

[ApiController]
[Route("api/exports")]
[Authorize]
public class ExportsController : ControllerBase
{
    private readonly IExportService _exportService;

    public ExportsController(IExportService exportService)
    {
        _exportService = exportService;
    }

    /// <summary>
    /// Tạo báo cáo xuất hóa đơn (MISA / STANDARD)
    /// </summary>
    [HttpPost("generate")]
    [Authorize(Policy = Constants.Permissions.ReportExport)]
    public async Task<IActionResult> GenerateExport([FromBody] GenerateExportRequestDto request)
    {
        var companyId = GetCurrentCompanyId();
        var userId = GetCurrentUserId();
        var userRole = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value
            ?? User.FindFirst("role")?.Value 
            ?? "Accountant";

        if (companyId == null || userId == null)
            return Unauthorized(new { Message = "Không xác định được người dùng hoặc công ty" });

        try
        {
            var result = await _exportService.GenerateExportAsync(companyId.Value, userId.Value, userRole, request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetExportHistory(
        [FromServices] SmartInvoice.API.Data.AppDbContext dbContext, 
        [FromServices] IAwsS3Service s3Service)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized(new { Message = "Không xác định được công ty" });

        var histories = await dbContext.ExportHistories
            .Where(h => h.CompanyId == companyId && !h.IsDeleted)
            .OrderByDescending(h => h.ExportedAt)
            .Take(50) // Lấy 10 lần xuất gần nhất
            .ToListAsync();

        var result = histories.Select(h => new {
            h.ExportId,
            h.FileName,
            fileType = h.FileType,
            h.TotalRecords,
            h.Status,
            // Sinh lại link S3 nếu file đã hoàn tất
            DownloadUrl = !string.IsNullOrEmpty(h.S3Key) ? s3Service.GeneratePreSignedUrl(h.S3Key, 15) : null
        });

        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Constants.Permissions.ReportExport)]
    public async Task<IActionResult> SoftDeleteExport(Guid id)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized(new { Message = "Không xác định được công ty" });
        
        var success = await _exportService.SoftDeleteExportAsync(id, companyId.Value);
        if (!success) return NotFound(new { Message = "Không tìm thấy file export" });
        return Ok(new { Message = "Đã chuyển file vào thùng rác." });
    }

    [HttpGet("trash")]
    [Authorize(Policy = Constants.Permissions.ReportExport)]
    public async Task<IActionResult> GetTrashExports()
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized(new { Message = "Không xác định được công ty" });
        var result = await _exportService.GetTrashExportsAsync(companyId.Value);
        return Ok(result);
    }

    [HttpPost("{id:guid}/restore")]
    [Authorize(Policy = Constants.Permissions.ReportExport)]
    public async Task<IActionResult> RestoreExport(Guid id)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized(new { Message = "Không xác định được công ty" });
        var success = await _exportService.RestoreExportAsync(id, companyId.Value);
        if (!success) return NotFound(new { Message = "Không tìm thấy file trong thùng rác" });
        return Ok(new { Message = "Phục hồi thành công." });
    }

    [HttpDelete("{id:guid}/hard")]
    [Authorize(Policy = Constants.Permissions.ReportExport)]
    public async Task<IActionResult> HardDeleteExport(Guid id)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized(new { Message = "Không xác định được công ty" });
        var success = await _exportService.HardDeleteExportAsync(id, companyId.Value);
        if (!success) return NotFound(new { Message = "Không tìm thấy file" });
        return Ok(new { Message = "Xóa vĩnh viễn thành công. Đã hoàn trả dung lượng." });
    }

    private Guid? GetCurrentCompanyId()
    {
        var companyIdClaim = User.Claims.FirstOrDefault(c => c.Type == "CompanyId")?.Value;
        return Guid.TryParse(companyIdClaim, out var companyId) ? companyId : null;
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}


