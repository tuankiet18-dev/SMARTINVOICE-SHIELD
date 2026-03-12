using SmartInvoice.API.Entities.JsonModels;

namespace SmartInvoice.API.DTOs.Invoice;

/// <summary>
/// DTO trả về chi tiết hóa đơn – chỉ hiển thị các cột cần thiết.
/// </summary>
public class InvoiceDetailDto
{
    // ─── Header ───
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public string? SerialNumber { get; set; }
    public string? FormNumber { get; set; }
    public DateTime InvoiceDate { get; set; }
    public string Status { get; set; } = null!;
    public string RiskLevel { get; set; } = null!;
    public string ProcessingMethod { get; set; } = null!;
    public string InvoiceCurrency { get; set; } = "VND";
    public decimal ExchangeRate { get; set; }
    public string? MCCQT { get; set; }

    // ─── Invoice Dossier ───
    /// <summary>True if the invoice has an XML original file (OriginalFileId is set).</summary>
    public bool HasOriginalFile { get; set; }
    /// <summary>True if the invoice has a visual PDF/Image file (VisualFileId is set).</summary>
    public bool HasVisualFile { get; set; }

    // ─── Seller ───
    public string? SellerName { get; set; }
    public string? SellerTaxCode { get; set; }
    public string? SellerAddress { get; set; }
    public string? SellerBankAccount { get; set; }
    public string? SellerBankName { get; set; }

    // ─── Buyer ───
    public string? BuyerName { get; set; }
    public string? BuyerTaxCode { get; set; }
    public string? BuyerAddress { get; set; }

    // ─── Amounts ───
    public decimal? TotalAmountBeforeTax { get; set; }
    public decimal? TotalTaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? TotalAmountInWords { get; set; }

    // ─── Payment ───
    public string? PaymentMethod { get; set; }
    public string? Notes { get; set; }

    // ─── Workflow ───
    public string? UploadedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? SubmittedByName { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectedByName { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }

    // ─── Risk Reasons ───
    public List<RiskReason>? RiskReasons { get; set; }

    // ─── Related Data ───
    public List<LineItemDto> LineItems { get; set; } = new();
    public List<ValidationLayerDto> ValidationLayers { get; set; } = new();
    public List<RiskCheckDto> RiskChecks { get; set; } = new();
    public List<InvoiceAuditLogDto> AuditLogs { get; set; } = new();
}

public class LineItemDto
{
    public int LineNumber { get; set; }
    public string? ItemName { get; set; }
    public string? Unit { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public int VatRate { get; set; }
    public decimal VatAmount { get; set; }
}

public class ValidationLayerDto
{
    public string LayerName { get; set; } = null!;
    public int LayerOrder { get; set; }
    public bool IsValid { get; set; }
    public string ValidationStatus { get; set; } = null!;
    public string? ErrorDetails { get; set; }
    public DateTime CheckedAt { get; set; }
}

public class RiskCheckDto
{
    public string CheckType { get; set; } = null!;
    public string CheckStatus { get; set; } = null!;
    public string RiskLevel { get; set; } = null!;
    public string? ErrorMessage { get; set; }
    public string? Suggestion { get; set; }
    public string? CheckDetails { get; set; }
    public DateTime CheckedAt { get; set; }
}
