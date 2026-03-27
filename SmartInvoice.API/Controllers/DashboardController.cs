using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInvoice.API.Constants;
using SmartInvoice.API.Services.Interfaces;
using System.Security.Claims;

namespace SmartInvoice.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(IDashboardService dashboardService, ILogger<DashboardController> logger)
    {
        _dashboardService = dashboardService;
        _logger = logger;
    }

    /// <summary>
    /// Get dashboard statistics for the current company.
    /// </summary>
    [HttpGet("stats")]
    [Authorize(Policy = Permissions.InvoiceView)]
    public async Task<IActionResult> GetStats([FromQuery] string period = "30d", [FromQuery] string chartPeriod = "6m")
    {
        try
        {
            var allowedPeriods = new[] { "7d", "30d", "90d", "6m", "1y", "all" };
            if (!allowedPeriods.Contains(period))
                period = "30d";
                
            // Validate chartPeriod
            var allowedChartPeriods = new[] { "3m", "6m", "12m" };
            if (!allowedChartPeriods.Contains(chartPeriod))
                chartPeriod = "6m";

            var companyIdClaim = User.FindFirst("CompanyId")?.Value;
            var userIdClaim = User.FindFirst("UserId")?.Value;
            var userRole = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? "Member";

            if (string.IsNullOrEmpty(companyIdClaim) || !Guid.TryParse(companyIdClaim, out var companyId) ||
                string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "User identity or company information is missing in token." });
            }

            // Đừng quên cập nhật cả Interface IDashboardService.cs của bạn thêm tham số chartPeriod nữa nhé!
            var stats = await _dashboardService.GetDashboardStatsAsync(companyId, userRole, userId, period, chartPeriod);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching dashboard stats");
            return StatusCode(500, new { message = "An error occurred while fetching dashboard statistics." });
        }
    }
}
