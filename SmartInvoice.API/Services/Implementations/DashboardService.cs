using Microsoft.EntityFrameworkCore;
using SmartInvoice.API.Data;
using SmartInvoice.API.DTOs.Dashboard;
using SmartInvoice.API.Services.Interfaces;

namespace SmartInvoice.API.Services.Implementations;

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _db;

    public DashboardService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(Guid companyId, string userRole, Guid userId, string period = "30d")
    {
        var now = DateTime.UtcNow;

        // Determine the "recent" window based on the period parameter
        int recentDays = period switch
        {
            "7d" => 7,
            "30d" => 30,
            "90d" => 90,
            "6m" => 180,
            "1y" => 365,
            "all" => 0, // 0 means no time filter
            _ => 30,
        };

        var recentCutoff = recentDays > 0 ? now.AddDays(-recentDays) : (DateTime?)null;
        var previousCutoff = recentDays > 0 ? now.AddDays(-recentDays * 2) : (DateTime?)null;
        var sevenMonthsAgo = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-6);

        // Base query — tenant-scoped, not deleted
        var invoices = _db.Invoices
            .Where(i => i.CompanyId == companyId && !i.IsDeleted);

        if (userRole == "Member")
        {
            invoices = invoices.Where(i => i.Workflow.UploadedBy == userId);
        }

        // ══════════════════════════════════════════════════════════════
        // QUERY 1: Counts grouped by RiskLevel + Status
        // → Replaces ~16 separate CountAsync calls with 1 DB round-trip
        // → If period is "all", counts all invoices; otherwise filters by recentCutoff
        // ══════════════════════════════════════════════════════════════
        var filteredInvoices = recentCutoff.HasValue
            ? invoices.Where(i => i.CreatedAt >= recentCutoff.Value)
            : invoices;

        var allCounts = await filteredInvoices
            .GroupBy(i => new { i.RiskLevel, i.Status })
            .Select(g => new { g.Key.RiskLevel, g.Key.Status, Count = g.Count() })
            .ToListAsync();

        var totalAll = allCounts.Sum(c => c.Count);
        var greenAll = allCounts.Where(c => c.RiskLevel == "Green").Sum(c => c.Count);
        var yellowCount = allCounts.Where(c => c.RiskLevel == "Yellow").Sum(c => c.Count);
        var orangeCount = allCounts.Where(c => c.RiskLevel == "Orange").Sum(c => c.Count);
        var redAll = allCounts.Where(c => c.RiskLevel == "Red").Sum(c => c.Count);
        var yellowOrangeAll = yellowCount + orangeCount;

        // ══════════════════════════════════════════════════════════════
        // QUERY 2: Period-based counts for change % calculation
        // → Compares current period vs previous period of same length
        // ══════════════════════════════════════════════════════════════
        int currentTotal = 0, prevTotal = 0;
        int greenCurrent = 0, greenPrevious = 0;
        int yellowOrangeCurrent = 0, yellowOrangePrevious = 0;
        int redCurrent = 0, redPrevious = 0;

        if (recentCutoff.HasValue && previousCutoff.HasValue)
        {
            var periodCounts = await invoices
                .Where(i => i.CreatedAt >= previousCutoff.Value)
                .GroupBy(i => new
                {
                    IsCurrent = i.CreatedAt >= recentCutoff.Value,
                    i.RiskLevel
                })
                .Select(g => new { g.Key.IsCurrent, g.Key.RiskLevel, Count = g.Count() })
                .ToListAsync();

            var currentGroup = periodCounts.Where(p => p.IsCurrent).ToList();
            var previousGroup = periodCounts.Where(p => !p.IsCurrent).ToList();

            currentTotal = currentGroup.Sum(p => p.Count);
            prevTotal = previousGroup.Sum(p => p.Count);
            greenCurrent = currentGroup.Where(p => p.RiskLevel == "Green").Sum(p => p.Count);
            greenPrevious = previousGroup.Where(p => p.RiskLevel == "Green").Sum(p => p.Count);
            yellowOrangeCurrent = currentGroup
                .Where(p => p.RiskLevel == "Yellow" || p.RiskLevel == "Orange").Sum(p => p.Count);
            yellowOrangePrevious = previousGroup
                .Where(p => p.RiskLevel == "Yellow" || p.RiskLevel == "Orange").Sum(p => p.Count);
            redCurrent = currentGroup.Where(p => p.RiskLevel == "Red").Sum(p => p.Count);
            redPrevious = previousGroup.Where(p => p.RiskLevel == "Red").Sum(p => p.Count);
        }

        // ── Risk Distribution (computed in-memory from Query 1, no extra DB call) ──
        var riskDistribution = new List<RiskDistributionItem>();
        if (totalAll > 0)
        {
            riskDistribution = new List<RiskDistributionItem>
            {
                new() { Label = "An toàn (Green)", Percent = Math.Round((decimal)greenAll / totalAll * 100, 1), Color = "#00B69B" },
                new() { Label = "Lưu ý (Yellow)", Percent = Math.Round((decimal)yellowCount / totalAll * 100, 1), Color = "#FF9500" },
                new() { Label = "Cảnh báo (Orange)", Percent = Math.Round((decimal)orangeCount / totalAll * 100, 1), Color = "#FD7E14" },
                new() { Label = "Nguy hiểm (Red)", Percent = Math.Round((decimal)redAll / totalAll * 100, 1), Color = "#FC2A46" },
            };
        }

        // ── Status Distribution (computed in-memory from Query 1, no extra DB call) ──
        var statusColors = new Dictionary<string, string>
        {
            { "Draft", "#94a3b8" },
            { "Pending", "#e6a817" },
            { "Approved", "#2d9a5c" },
            { "Rejected", "#d63031" },
            { "Archived", "#6c757d" },
        };

        var statusNames = new Dictionary<string, string>
        {
            { "Draft", "Nháp" },
            { "Pending", "Chờ duyệt" },
            { "Approved", "Đã duyệt" },
            { "Rejected", "Từ chối" },
            { "Archived", "Lưu trữ" },
        };

        var statusDistribution = allCounts
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Sum(c => c.Count) })
            .Where(s => s.Count > 0)
            .Select(s => new StatusDistributionItem
            {
                Name = statusNames.GetValueOrDefault(s.Status, s.Status),
                Value = s.Count,
                Color = statusColors.GetValueOrDefault(s.Status, "#94a3b8"),
            })
            .ToList();

        // ══════════════════════════════════════════════════════════════
        // QUERY 3: Monthly Trends + Risk Trends combined (last 7 months)
        // → Merges 2 separate GroupBy queries into 1 DB round-trip
        // ══════════════════════════════════════════════════════════════
        var monthlyData = await invoices
            .Where(i => i.CreatedAt >= sevenMonthsAgo)
            .GroupBy(i => new { i.CreatedAt.Year, i.CreatedAt.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Total = g.Count(),
                Approved = g.Count(x => x.Status == "Approved"),
                Rejected = g.Count(x => x.Status == "Rejected"),
                Pending = g.Count(x => x.Status == "Pending"),
                Green = g.Count(x => x.RiskLevel == "Green"),
                Yellow = g.Count(x => x.RiskLevel == "Yellow"),
                Orange = g.Count(x => x.RiskLevel == "Orange"),
                Red = g.Count(x => x.RiskLevel == "Red"),
            })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync();

        var monthlyTrends = new List<MonthlyTrendItem>();
        var riskTrends = new List<RiskTrendItem>();

        for (int i = -6; i <= 0; i++)
        {
            var d = now.AddMonths(i);
            var match = monthlyData.FirstOrDefault(m => m.Year == d.Year && m.Month == d.Month);

            monthlyTrends.Add(new MonthlyTrendItem
            {
                Month = $"T{d.Month}",
                Total = match?.Total ?? 0,
                Approved = match?.Approved ?? 0,
                Rejected = match?.Rejected ?? 0,
                Pending = match?.Pending ?? 0,
            });

            if (match != null && match.Total > 0)
            {
                riskTrends.Add(new RiskTrendItem
                {
                    Month = $"T{d.Month}",
                    Green = Math.Round((decimal)match.Green / match.Total * 100, 1),
                    Yellow = Math.Round((decimal)match.Yellow / match.Total * 100, 1),
                    Orange = Math.Round((decimal)match.Orange / match.Total * 100, 1),
                    Red = Math.Round((decimal)match.Red / match.Total * 100, 1),
                });
            }
            else
            {
                riskTrends.Add(new RiskTrendItem { Month = $"T{d.Month}" });
            }
        }

        // ══════════════════════════════════════════════════════════════
        // QUERY 4: Recent Invoices (top 5, newest first)
        // ══════════════════════════════════════════════════════════════
        var recentRaw = await invoices
            .OrderByDescending(i => i.CreatedAt)
            .Take(5)
            .Select(i => new RecentInvoiceItem
            {
                InvoiceId = i.InvoiceId,
                InvoiceNo = i.InvoiceNumber ?? "N/A",
                Seller = i.Seller.Name ?? "N/A",
                Amount = i.TotalAmount.ToString("N0") + " ₫",
                Date = i.InvoiceDate.ToString("dd/MM/yyyy"),
                Status = i.Status,
                Risk = i.RiskLevel,
            })
            .ToListAsync();

        // ══════════════════════════════════════════════════════════════
        // QUERY 5: Summary Amounts (single query with conditional sums)
        // → Replaces 3 separate SumAsync calls with 1 DB round-trip
        // → Guarded by totalAll > 0 to avoid SUM(NULL) → decimal crash
        // ══════════════════════════════════════════════════════════════
        decimal totalAmount = 0m, approvedAmount = 0m, pendingAmount = 0m;

        if (totalAll > 0)
        {
            var amounts = await filteredInvoices
                .GroupBy(i => 1)
                .Select(g => new
                {
                    Total = g.Sum(i => i.TotalAmount),
                    Approved = g.Sum(i => i.Status == "Approved" ? i.TotalAmount : 0m),
                    Pending = g.Sum(i => i.Status == "Pending" ? i.TotalAmount : 0m),
                })
                .FirstOrDefaultAsync();

            totalAmount = amounts?.Total ?? 0m;
            approvedAmount = amounts?.Approved ?? 0m;
            pendingAmount = amounts?.Pending ?? 0m;
        }

        return new DashboardStatsDto
        {
            Period = period,
            TotalInvoices = totalAll,
            GreenInvoices = greenAll,
            YellowOrangeInvoices = yellowOrangeAll,
            RedInvoices = redAll,

            TotalChange = CalcChange(currentTotal, prevTotal),
            GreenChange = CalcChange(greenCurrent, greenPrevious),
            YellowOrangeChange = CalcChange(yellowOrangeCurrent, yellowOrangePrevious),
            RedChange = CalcChange(redCurrent, redPrevious),

            RiskDistribution = riskDistribution,
            StatusDistribution = statusDistribution,
            MonthlyTrends = monthlyTrends,
            RiskTrends = riskTrends,
            RecentInvoices = recentRaw,

            TotalAmount = totalAmount,
            ApprovedAmount = approvedAmount,
            PendingAmount = pendingAmount,
        };
    }

    private static decimal CalcChange(int current, int previous)
    {
        if (previous == 0) return current > 0 ? 100m : 0m;
        return Math.Round((decimal)(current - previous) / previous * 100, 1);
    }
}
