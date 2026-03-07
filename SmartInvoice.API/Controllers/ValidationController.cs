using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartInvoice.API.Constants;
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
    /// </summary>
    [HttpGet("overview")]
    [Authorize(Policy = Permissions.InvoiceView)]
    public async Task<ActionResult<ValidationOverviewDto>> GetOverview([FromQuery] ValidationOverviewQueryDto query)
    {
        // ── Tenant scoping ──────────────────────────────
        var companyIdClaim = User.FindFirst("CompanyId")?.Value;
        if (string.IsNullOrEmpty(companyIdClaim) || !Guid.TryParse(companyIdClaim, out var companyId))
            return Forbid();

        // ── Base query: invoices that have at least one validation layer ──
        var invoicesQuery = _db.Invoices
            .Where(i => i.CompanyId == companyId && !i.IsDeleted)
            .Where(i => i.ValidationLayers.Any());

        // ── Summary statistics (from full set, ignoring filters) ──────
        var allValidated = await invoicesQuery
            .Select(i => new
            {
                i.InvoiceId,
                i.RiskLevel,
                Layers = i.ValidationLayers
                    .OrderBy(v => v.LayerOrder)
                    .Select(v => new { v.LayerOrder, v.ValidationStatus })
                    .ToList()
            })
            .ToListAsync();

        int totalValidated = allValidated.Count;
        int passCount = 0, warningCount = 0, failCount = 0;
        int layer1Pass = 0, layer2Pass = 0, layer3Pass = 0;
        int greenCount = 0, yellowCount = 0, orangeCount = 0, redCount = 0;

        foreach (var inv in allValidated)
        {
            // Overall status derived from layers
            var statuses = inv.Layers.Select(l => l.ValidationStatus).ToList();
            if (statuses.Any(s => s == "Fail")) failCount++;
            else if (statuses.Any(s => s == "Warning")) warningCount++;
            else passCount++;

            // Per-layer pass counts (Pass or Warning = "đạt" for that layer)
            var l1 = inv.Layers.FirstOrDefault(l => l.LayerOrder == 1);
            var l2 = inv.Layers.FirstOrDefault(l => l.LayerOrder == 2);
            var l3 = inv.Layers.FirstOrDefault(l => l.LayerOrder == 3);
            if (l1 != null && l1.ValidationStatus != "Fail") layer1Pass++;
            if (l2 != null && l2.ValidationStatus != "Fail") layer2Pass++;
            if (l3 != null && l3.ValidationStatus != "Fail") layer3Pass++;

            // Risk distribution
            switch (inv.RiskLevel)
            {
                case "Green": greenCount++; break;
                case "Yellow": yellowCount++; break;
                case "Orange": orangeCount++; break;
                case "Red": redCount++; break;
            }
        }

        // ── Filtered query for paged list ────────────────
        var filteredQuery = invoicesQuery.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim().ToLower();
            filteredQuery = filteredQuery.Where(i =>
                (i.InvoiceNumber != null && i.InvoiceNumber.ToLower().Contains(kw)) ||
                (i.SellerName != null && i.SellerName.ToLower().Contains(kw)) ||
                (i.SellerTaxCode != null && i.SellerTaxCode.ToLower().Contains(kw))
            );
        }

        if (!string.IsNullOrWhiteSpace(query.RiskLevel))
        {
            filteredQuery = filteredQuery.Where(i => i.RiskLevel == query.RiskLevel);
        }

        if (!string.IsNullOrWhiteSpace(query.ValidationStatus))
        {
            var vs = query.ValidationStatus;
            if (vs == "Fail")
                filteredQuery = filteredQuery.Where(i => i.ValidationLayers.Any(v => v.ValidationStatus == "Fail"));
            else if (vs == "Warning")
                filteredQuery = filteredQuery.Where(i =>
                    !i.ValidationLayers.Any(v => v.ValidationStatus == "Fail") &&
                    i.ValidationLayers.Any(v => v.ValidationStatus == "Warning"));
            else // Pass
                filteredQuery = filteredQuery.Where(i =>
                    i.ValidationLayers.All(v => v.ValidationStatus == "Pass"));
        }

        int totalCount = await filteredQuery.CountAsync();
        int totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);

        var items = await filteredQuery
            .OrderByDescending(i => i.UpdatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(i => new InvoiceValidationSummaryDto
            {
                InvoiceId = i.InvoiceId,
                InvoiceNumber = i.InvoiceNumber ?? "",
                SellerName = i.SellerName,
                SellerTaxCode = i.SellerTaxCode,
                RiskLevel = i.RiskLevel,
                IssueCount = i.ValidationLayers.Count(v => v.ValidationStatus == "Warning" || v.ValidationStatus == "Fail")
                           + i.RiskCheckResults.Count(r => r.CheckStatus == "FAIL" || r.CheckStatus == "WARNING"),
                ValidatedAt = i.ValidationLayers
                    .OrderByDescending(v => v.CheckedAt)
                    .Select(v => (DateTime?)v.CheckedAt)
                    .FirstOrDefault(),
                Layer1Status = i.ValidationLayers
                    .Where(v => v.LayerOrder == 1)
                    .Select(v => v.ValidationStatus)
                    .FirstOrDefault(),
                Layer2Status = i.ValidationLayers
                    .Where(v => v.LayerOrder == 2)
                    .Select(v => v.ValidationStatus)
                    .FirstOrDefault(),
                Layer3Status = i.ValidationLayers
                    .Where(v => v.LayerOrder == 3)
                    .Select(v => v.ValidationStatus)
                    .FirstOrDefault(),
            })
            .ToListAsync();

        // Derive OverallStatus on client since it's not easily computed in LINQ-to-SQL
        foreach (var item in items)
        {
            var statuses = new[] { item.Layer1Status, item.Layer2Status, item.Layer3Status };
            if (statuses.Any(s => s == "Fail")) item.OverallStatus = "Fail";
            else if (statuses.Any(s => s == "Warning")) item.OverallStatus = "Warning";
            else item.OverallStatus = "Pass";
        }

        return Ok(new ValidationOverviewDto
        {
            TotalValidated = totalValidated,
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
