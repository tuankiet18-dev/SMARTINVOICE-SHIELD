namespace SmartInvoice.API.DTOs.Dashboard;

public class DashboardStatsDto
{
    // Active period
    public string Period { get; set; } = "30d";

    // KPI Cards
    public int TotalInvoices { get; set; }
    public int GreenInvoices { get; set; }
    public int YellowOrangeInvoices { get; set; }
    public int RedInvoices { get; set; }

    // Period-over-period change (% vs previous 30 days)
    public decimal TotalChange { get; set; }
    public decimal GreenChange { get; set; }
    public decimal YellowOrangeChange { get; set; }
    public decimal RedChange { get; set; }

    // Risk Distribution
    public List<RiskDistributionItem> RiskDistribution { get; set; } = new();

    // Status Distribution (pie chart)
    public List<StatusDistributionItem> StatusDistribution { get; set; } = new();

    // Monthly Trends (last 7 months — bar chart + line chart)
    public List<MonthlyTrendItem> MonthlyTrends { get; set; } = new();

    // Risk Trends (last 7 months — area chart)
    public List<RiskTrendItem> RiskTrends { get; set; } = new();

    // Recent Invoices (top 5)
    public List<RecentInvoiceItem> RecentInvoices { get; set; } = new();

    // Summary amounts
    public decimal TotalAmount { get; set; }
    public decimal ApprovedAmount { get; set; }
    public decimal PendingAmount { get; set; }
}

public class RiskDistributionItem
{
    public string Label { get; set; } = string.Empty;
    public decimal Percent { get; set; }
    public string Color { get; set; } = string.Empty;
}

public class StatusDistributionItem
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public string Color { get; set; } = string.Empty;
}

public class MonthlyTrendItem
{
    public string Month { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
    public int Pending { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalTaxAmount { get; set; }
}

public class RiskTrendItem
{
    public string Month { get; set; } = string.Empty;
    public decimal Green { get; set; }
    public decimal Yellow { get; set; }
    public decimal Red { get; set; }
}

public class RecentInvoiceItem
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public string Seller { get; set; } = string.Empty;
    public string Amount { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Risk { get; set; } = string.Empty;
}
