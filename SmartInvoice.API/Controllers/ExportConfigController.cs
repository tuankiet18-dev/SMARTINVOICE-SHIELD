using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInvoice.API.DTOs.Export;
using SmartInvoice.API.Services.Interfaces;

namespace SmartInvoice.API.Controllers;

[ApiController]
[Route("api/export-config")]
[Authorize]
public class ExportConfigController : ControllerBase
{
    private readonly IExportConfigService _exportConfigService;

    public ExportConfigController(IExportConfigService exportConfigService)
    {
        _exportConfigService = exportConfigService;
    }

    /// <summary>
    /// Lấy cấu hình export của company hiện tại
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Constants.Permissions.ReportExport)]
    public async Task<IActionResult> GetExportConfig()
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null)
            return Unauthorized(new { Message = "Không xác định được công ty" });

        var config = await _exportConfigService.GetExportConfigAsync(companyId.Value);
        if (config == null)
            return Ok(new ExportConfigDto { CompanyId = companyId.Value });

        return Ok(config);
    }

    /// <summary>
    /// Cập nhật cấu hình export (TK Nợ, TK Có, TK Thuế, Mã kho)
    /// </summary>
    [HttpPut]
    [Authorize(Policy = Constants.Permissions.ReportExport)]
    public async Task<IActionResult> UpdateExportConfig([FromBody] UpdateExportConfigDto dto)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null)
            return Unauthorized(new { Message = "Không xác định được công ty" });

        var result = await _exportConfigService.UpdateExportConfigAsync(companyId.Value, dto);
        return Ok(result);
    }

    private Guid? GetCurrentCompanyId()
    {
        var companyIdClaim = User.Claims.FirstOrDefault(c => c.Type == "CompanyId")?.Value;
        return Guid.TryParse(companyIdClaim, out var companyId) ? companyId : null;
    }
}
