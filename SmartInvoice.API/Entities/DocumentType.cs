using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartInvoice.API.Entities.JsonModels;

namespace SmartInvoice.API.Entities;

[Table("DocumentTypes")]
public class DocumentType
{
    [Key]
    public int DocumentTypeId { get; set; }

    [Required]
    [MaxLength(50)]
    public string TypeCode { get; set; } = null!; // GTGT, SALE, CASH_REGISTER

    [Required]
    [MaxLength(100)]
    public string TypeName { get; set; } = null!;

    [MaxLength(100)]
    public string? TypeNameEN { get; set; }

    public string? Description { get; set; }

    // --- Compliance Rules (NĐ 123/2020) ---
    [MaxLength(20)]
    public string? FormTemplate { get; set; } // 01GTKT, 02GTTT

    public bool RequiresXML { get; set; } = false;
    public bool RequiresDigitalSignature { get; set; } = false;
    public bool RequiresMCCQT { get; set; } = false; // Mã cơ quan thuế
    public bool RequiresVAT { get; set; } = false;

    // --- Configurations (JSONB) ---
    [Column(TypeName = "jsonb")]
    public ValidationRuleConfig? ValidationRules { get; set; }

    [Column(TypeName = "jsonb")]
    public ProcessingConfig? ProcessingConfig { get; set; }

    // --- Status ---
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
