using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInvoice.API.Entities;

[Table("InvoiceLineItems")]
public class InvoiceLineItem
{
    [Key]
    public Guid LineItemId { get; set; }

    public Guid InvoiceId { get; set; }
    [ForeignKey(nameof(InvoiceId))]
    public Invoice? Invoice { get; set; }

    public int LineNumber { get; set; }

    [MaxLength(500)]
    public string? ItemName { get; set; }

    [MaxLength(50)]
    public string? Unit { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    public int VatRate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal VatAmount { get; set; }

    // --- OCR Specific Data ---
    public float? ConfidenceScore { get; set; } // Confidence score for this specific line item from OCR

    // --- Metadata ---
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
