using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartInvoice.API.Constants;
using SmartInvoice.API.Data;
using SmartInvoice.API.DTOs;
using SmartInvoice.API.DTOs.AuditLog;

namespace SmartInvoice.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuditLogController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<AuditLogController> _logger;

    public AuditLogController(AppDbContext db, ILogger<AuditLogController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Get paged audit logs for the current company (system-wide, not per-invoice).
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Permissions.InvoiceView)]
    public async Task<IActionResult> GetAuditLogs([FromQuery] AuditLogQueryDto query)
    {
        try
        {
            var companyIdClaim = User.FindFirst("CompanyId")?.Value;
            var userIdClaim = User.FindFirst("UserId")?.Value;
            var userRole = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value 
                ?? User.FindFirst("role")?.Value 
                ?? "Accountant";

            if (string.IsNullOrEmpty(companyIdClaim) || !Guid.TryParse(companyIdClaim, out var companyId) ||
                string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized(new { message = "CompanyId or UserId claim is missing or invalid." });

            // Base query: Phi bình thường hóa CompanyId giúp xem log độc lập với hóa đơn
            var baseQuery = _db.InvoiceAuditLogs
                .Include(a => a.Invoice)
                .Include(a => a.User)
                .Where(a => a.CompanyId == companyId);

            // RBAC: Nếu là Accountant thì chỉ được xem log của các hóa đơn do chính họ tải lên hoặc log do họ tạo ra
            if (userRole == "Accountant")
            {
                baseQuery = baseQuery.Where(a => a.UserId == userId || (a.Invoice != null && a.Invoice.Workflow.UploadedBy == userId));
            }

            // Exclude Demo Data
            if (query.ExcludeDemoData)
            {
                // Filter out logs where InvoiceNumber or Invoice.InvoiceNumber starts with "DEMO-"
                baseQuery = baseQuery.Where(a => 
                    (a.InvoiceNumber == null || !a.InvoiceNumber.StartsWith("DEMO-")) &&
                    (a.Invoice == null || a.Invoice.InvoiceNumber == null || !a.Invoice.InvoiceNumber.StartsWith("DEMO-"))
                );
            }

            // Filter by action
            if (!string.IsNullOrEmpty(query.Action))
                baseQuery = baseQuery.Where(a => a.Action == query.Action);

            // Filter by keyword (search in invoice number, user email, reason, comment)
            if (!string.IsNullOrEmpty(query.Keyword))
            {
                var kw = query.Keyword.ToLower();
                baseQuery = baseQuery.Where(a =>
                    (a.InvoiceNumber != null && a.InvoiceNumber.ToLower().Contains(kw)) ||
                    (a.UserEmail != null && a.UserEmail.ToLower().Contains(kw)) ||
                    (a.Reason != null && a.Reason.ToLower().Contains(kw)) ||
                    (a.Comment != null && a.Comment.ToLower().Contains(kw))
                );
            }

            // Filter by date range
            if (!string.IsNullOrEmpty(query.DateFrom) && DateTime.TryParse(query.DateFrom, out var dateFrom))
                baseQuery = baseQuery.Where(a => a.CreatedAt >= dateFrom.ToUniversalTime());

            if (!string.IsNullOrEmpty(query.DateTo) && DateTime.TryParse(query.DateTo, out var dateTo))
                baseQuery = baseQuery.Where(a => a.CreatedAt <= dateTo.ToUniversalTime().AddDays(1));

            var totalCount = await baseQuery.CountAsync();

            var items = await baseQuery
                .OrderByDescending(a => a.CreatedAt)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(a => new SystemAuditLogDto
                {
                    AuditId = a.AuditId,
                    InvoiceId = a.InvoiceId,
                    InvoiceNumber = a.InvoiceNumber,
                    UserEmail = a.UserEmail,
                    UserRole = a.UserRole,
                    UserFullName = a.User != null ? a.User.FullName : null,
                    Action = a.Action,
                    Reason = a.Reason,
                    Comment = a.Comment,
                    IpAddress = a.IpAddress,
                    Changes = a.Changes,
                    CreatedAt = a.CreatedAt,
                })
                .ToListAsync();

            return Ok(new PagedResult<SystemAuditLogDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageIndex = query.Page,
                PageSize = query.PageSize,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching audit logs");
            return StatusCode(500, new { message = "An error occurred while fetching audit logs." });
        }
    }
}
