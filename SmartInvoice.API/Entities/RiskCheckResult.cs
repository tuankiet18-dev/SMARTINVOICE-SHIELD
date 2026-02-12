using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInvoice.API.Entities;

[Table("RiskCheckResults")]
public class RiskCheckResult
{
    [Key]
    public Guid CheckId { get; set; }

    // --- Invoice Relation ---
    public Guid InvoiceId { get; set; }
    [ForeignKey(nameof(InvoiceId))]
    public Invoice? Invoice { get; set; }

    // --- Check Info ---
    [Required]
    [MaxLength(50)]
    public string CheckType { get; set; } = null!; // LEGAL, VALID, REASONABLE

    [MaxLength(100)]
    public string? CheckSubType { get; set; }

    // --- Result ---
    [Required]
    [MaxLength(20)]
    public string CheckStatus { get; set; } = "PASS"; // PASS, WARNING, FAIL

    [Required]
    [MaxLength(20)]
    public string RiskLevel { get; set; } = "Green"; // Green, Yellow, Orange, Red

    // --- Error Info ---
    [MaxLength(50)]
    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public string? Suggestion { get; set; }

    [Column(TypeName = "jsonb")]
    public string? CheckDetails { get; set; } // JSON details

    // --- Performance ---
    public int? CheckDurationMs { get; set; }

    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(100)]
    public string CheckedBy { get; set; } = "System";
}
