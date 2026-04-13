using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartInvoice.API.Constants;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Data;
using SmartInvoice.API.DTOs.Validation;

namespace SmartInvoice.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ValidationController : ControllerBase
{
    private readonly AppDbContext _db;

    public ValidationController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// GET /api/validation/overview
    /// Returns summary statistics + paged list of invoice validation results.
    /// Invoices with same InvoiceNumber + SellerTaxCode are grouped; only the latest version
    /// appears as the parent row, with older versions nested in Children.
    /// </summary>
    [HttpGet("overview")]
    [Authorize(Policy = Permissions.InvoiceView)]
    public async Task<ActionResult<ValidationOverviewDto>> GetOverview([FromQuery] ValidationOverviewQueryDto query)
    {
        // ── Tenant scoping ──────────────────────────────
        var companyIdClaim = User.FindFirst("CompanyId")?.Value;
        var userIdClaim = User.FindFirst("UserId")?.Value;
        var userRole = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value ?? "Accountant";

        if (string.IsNullOrEmpty(companyIdClaim) || !Guid.TryParse(companyIdClaim, out var companyId) ||
            string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Forbid();

        // ── Base query: invoices that have at least one validation layer ──
        var invoicesQuery = _db.Invoices
            .Where(i => i.CompanyId == companyId && !i.IsDeleted)
            .Where(i => i.CheckResults.Any(c => c.Category != "AUTO_UPLOAD_VALIDATION"));

        // RBAC: Member only sees their own uploaded invoices
        if (userRole == "Accountant")
        {
            invoicesQuery = invoicesQuery.Where(i => i.Workflow.UploadedBy == userId);
        }

        // ── Project all validated invoices to DTOs (in-memory) ──────
        var allItems = await invoicesQuery
            .OrderByDescending(i => i.UpdatedAt)
            .Select(i => new InvoiceValidationSummaryDto
            {
                InvoiceId = i.InvoiceId,
                InvoiceNumber = i.InvoiceNumber ?? "",
                SellerName = i.Seller.Name,
                SellerTaxCode = i.Seller.TaxCode,
                RiskLevel = i.RiskLevel,
                Version = i.Version,
                IssueCount = i.CheckResults.Count(c => c.Status == ValidationStatuses.Fail || c.Status == ValidationStatuses.Warning),
                ValidatedAt = i.CheckResults
                    .Where(v => v.Category != "AUTO_UPLOAD_VALIDATION")
                    .OrderByDescending(v => v.CheckedAt)
                    .Select(v => (DateTime?)v.CheckedAt)
                    .FirstOrDefault(),
                Layer1Status = i.CheckResults
                    .Where(v => v.CheckOrder == 1 && v.Category != "AUTO_UPLOAD_VALIDATION")
                    .Select(v => v.Status)
                    .FirstOrDefault(),
                Layer2Status = i.CheckResults
                    .Where(v => v.CheckOrder == 2 && v.Category != "AUTO_UPLOAD_VALIDATION")
                    .Select(v => v.Status)
                    .FirstOrDefault(),
                Layer3Status = i.CheckResults
                    .Where(v => v.CheckOrder == 3 && v.Category != "AUTO_UPLOAD_VALIDATION")
                    .Select(v => v.Status)
                    .FirstOrDefault(),
            })
            .ToListAsync();

        // Derive OverallStatus in-memory
        foreach (var item in allItems)
        {
            var statuses = new[] { item.Layer1Status, item.Layer2Status, item.Layer3Status };
            if (statuses.Any(s => s == "Fail")) item.OverallStatus = "Fail";
            else if (statuses.Any(s => s == "Warning")) item.OverallStatus = "Warning";
            else item.OverallStatus = "Pass";
        }

        // ── Group by InvoiceNumber + SellerTaxCode ──────
        var grouped = allItems
            .GroupBy(i => new { i.InvoiceNumber, i.SellerTaxCode })
            .Select(g =>
            {
                var ordered = g.OrderByDescending(x => x.Version).ToList();
                var latest = ordered.First();
                latest.IsLatest = true;

                var olderVersions = ordered.Skip(1).ToList();
                foreach (var old in olderVersions) old.IsLatest = false;

                latest.Children = olderVersions.Count > 0 ? olderVersions : null;
                return latest;
            })
            .ToList();

        int totalValidationRuns = allItems.Count;
        int totalUniqueInvoices = grouped.Count;

        // ── Summary statistics (computed from latest versions only) ──────
        int totalValidated = grouped.Count;
        int passCount = 0, warningCount = 0, failCount = 0;
        int layer1Pass = 0, layer2Pass = 0, layer3Pass = 0;
        int greenCount = 0, yellowCount = 0, orangeCount = 0, redCount = 0;

        foreach (var inv in grouped)
        {
            // Overall status
            if (inv.OverallStatus == "Fail") failCount++;
            else if (inv.OverallStatus == "Warning") warningCount++;
            else passCount++;

            // Per-layer pass counts
            if (inv.Layer1Status != null && inv.Layer1Status != "Skipped" && inv.Layer1Status != ValidationStatuses.Fail) layer1Pass++;
            if (inv.Layer2Status != null && inv.Layer2Status != "Skipped" && inv.Layer2Status != ValidationStatuses.Fail) layer2Pass++;
            if (inv.Layer3Status != null && inv.Layer3Status != "Skipped" && inv.Layer3Status != ValidationStatuses.Fail) layer3Pass++;

            // Risk distribution
            switch (inv.RiskLevel)
            {
                case "Green": greenCount++; break;
                case "Yellow": yellowCount++; break;
                case "Orange": orangeCount++; break;
                case "Red": redCount++; break;
            }
        }

        // ── Filtered + paged list (applied on grouped/parent items) ────
        var filteredGroups = grouped.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim().ToLower();
            filteredGroups = filteredGroups.Where(i =>
                (!string.IsNullOrEmpty(i.InvoiceNumber) && i.InvoiceNumber.ToLower().Contains(kw)) ||
                (i.SellerName != null && i.SellerName.ToLower().Contains(kw)) ||
                (i.SellerTaxCode != null && i.SellerTaxCode.ToLower().Contains(kw))
            );
        }

        if (!string.IsNullOrWhiteSpace(query.LayerIssue))
        {
            var layer = query.LayerIssue.ToLower();
            filteredGroups = filteredGroups.Where(i =>
            {
                var status = layer == "layer1" ? i.Layer1Status :
                             layer == "layer2" ? i.Layer2Status :
                             layer == "layer3" ? i.Layer3Status : null;
                return status == ValidationStatuses.Fail || status == ValidationStatuses.Warning;
            });
        }

        if (!string.IsNullOrWhiteSpace(query.ValidationStatus))
        {
            filteredGroups = filteredGroups.Where(i => i.OverallStatus == query.ValidationStatus);
        }

        var filteredList = filteredGroups.ToList();
        int totalCount = filteredList.Count;
        int totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);

        var items = filteredList
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        return Ok(new ValidationOverviewDto
        {
            TotalValidated = totalValidated,
            TotalUniqueInvoices = totalUniqueInvoices,
            TotalValidationRuns = totalValidationRuns,
            PassCount = passCount,
            WarningCount = warningCount,
            FailCount = failCount,
            PassRate = totalValidated > 0 ? Math.Round(passCount * 100.0 / totalValidated, 1) : 0,
            Layer1PassCount = layer1Pass,
            Layer2PassCount = layer2Pass,
            Layer3PassCount = layer3Pass,
            GreenCount = greenCount,
            YellowCount = yellowCount,
            OrangeCount = orangeCount,
            RedCount = redCount,
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.Page,
            PageSize = query.PageSize,
            TotalPages = totalPages,
        });
    }
}
