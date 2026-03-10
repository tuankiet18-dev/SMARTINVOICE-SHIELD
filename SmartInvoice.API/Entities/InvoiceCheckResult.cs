using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInvoice.API.Entities;

[Table("InvoiceCheckResults")]
public class InvoiceCheckResult
{
    [Key]
    public Guid CheckId { get; set; }

    // --- Relations ---
    public Guid InvoiceId { get; set; }
    [ForeignKey(nameof(InvoiceId))]
    public Invoice? Invoice { get; set; }

    // --- Check Info ---
    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = null!; // Structure, Signature, BusinessLogic, Risk

    [Required]
    [MaxLength(100)]
    public string CheckName { get; set; } = null!; // e.g., "Mã số thuế người mua", "Tính trọn vẹn của file"

    public int CheckOrder { get; set; } // Sequence of execution

    // --- Result ---
    public bool IsValid { get; set; } // Equivalent to old Layer.IsValid or Risk Level Green

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Skipped"; // Pass, Warning, Fail, Skipped

    // --- Error Details ---
    [MaxLength(50)]
    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public string? Suggestion { get; set; }

    [Column(TypeName = "jsonb")]
    public string? ErrorDetails { get; set; } // JSON array of ValidationErrorDetail

    // --- Specific Check Data (JSONB) ---
    [Column(TypeName = "jsonb")]
    public string? AdditionalData { get; set; }

    // --- Performance ---
    public int? DurationMs { get; set; }

    // --- Metadata ---
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(100)]
    public string CheckedBy { get; set; } = "System";
}
