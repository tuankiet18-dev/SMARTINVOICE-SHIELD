using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInvoice.API.Entities;

[Table("ValidationLayers")]
public class ValidationLayer
{
    [Key]
    public Guid LayerId { get; set; }

    // --- Relations ---
    public Guid InvoiceId { get; set; }
    [ForeignKey(nameof(InvoiceId))]
    public Invoice? Invoice { get; set; }

    // --- Layer Info ---
    [Required]
    [MaxLength(50)]
    public string LayerName { get; set; } = null!; // Structure, Signature, BusinessLogic

    public int LayerOrder { get; set; } // 1, 2, 3

    // --- Result ---
    public bool IsValid { get; set; }

    [Required]
    [MaxLength(20)]
    public string ValidationStatus { get; set; } = "Skipped"; // Pass, Warning, Fail, Skipped

    // --- Error Details ---
    [MaxLength(50)]
    public string? ErrorCode { get; set; }
    
    public string? ErrorMessage { get; set; }

    [Column(TypeName = "jsonb")]
    public string? ErrorDetails { get; set; }

    // --- Specific Layer Data (JSONB) ---
    [Column(TypeName = "jsonb")]
    public string? LayerData { get; set; } 

    // --- Performance ---
    public int? ValidationDurationMs { get; set; }

    // --- Metadata ---
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(100)]
    public string CheckedBy { get; set; } = "System";
}
