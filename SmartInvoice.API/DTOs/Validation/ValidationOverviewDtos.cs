namespace SmartInvoice.API.DTOs.Validation;

/// <summary>
/// Query parameters for the validation overview endpoint.
/// </summary>
public class ValidationOverviewQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>Search by invoice number or seller name/tax code.</summary>
    public string? Keyword { get; set; }

    /// <summary>Filter by layer with issues: layer1, layer2, layer3.</summary>
    public string? LayerIssue { get; set; }

    /// <summary>Filter by validation status: Pass, Warning, Fail.</summary>
    public string? ValidationStatus { get; set; }
}

/// <summary>
/// Combined response: summary statistics + paged list of invoice validation results.
/// </summary>
public class ValidationOverviewDto
{
    // ── Summary statistics ──────────────────────────────
    public int TotalValidated { get; set; }
    public int PassCount { get; set; }
    public int WarningCount { get; set; }
    public int FailCount { get; set; }
    public double PassRate { get; set; }

    // ── Per-layer pass counts ───────────────────────────
    public int Layer1PassCount { get; set; }
    public int Layer2PassCount { get; set; }
    public int Layer3PassCount { get; set; }

    // ── Risk distribution ───────────────────────────────
    public int GreenCount { get; set; }
    public int YellowCount { get; set; }
    public int OrangeCount { get; set; }
    public int RedCount { get; set; }

    // ── Paged invoice validation summaries ──────────────
    public List<InvoiceValidationSummaryDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

/// <summary>
/// Per-invoice validation summary for the overview table.
/// </summary>
public class InvoiceValidationSummaryDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string? SellerName { get; set; }
    public string? SellerTaxCode { get; set; }
    public string? RiskLevel { get; set; }
    public int IssueCount { get; set; }
    public DateTime? ValidatedAt { get; set; }

    /// <summary>Layer 1 (Structure) status: Pass / Warning / Fail / null (not run).</summary>
    public string? Layer1Status { get; set; }

    /// <summary>Layer 2 (Digital Signature) status.</summary>
    public string? Layer2Status { get; set; }

    /// <summary>Layer 3 (Business Logic) status.</summary>
    public string? Layer3Status { get; set; }

    /// <summary>Overall validation status derived from layers: Pass / Warning / Fail.</summary>
    public string OverallStatus { get; set; } = "Pass";
}
