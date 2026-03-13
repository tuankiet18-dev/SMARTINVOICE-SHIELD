using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInvoice.API.Entities;

[Table("ExportConfigs")]
public class ExportConfig
{
    [Key]
    public Guid ConfigId { get; set; }

    public Guid CompanyId { get; set; }
    [ForeignKey(nameof(CompanyId))]
    public Company? Company { get; set; }

    [MaxLength(20)]
    public string? DefaultDebitAccount { get; set; } // TK Nợ

    [MaxLength(20)]
    public string? DefaultCreditAccount { get; set; } // TK Có

    [MaxLength(20)]
    public string? DefaultTaxAccount { get; set; } // TK Thuế

    [MaxLength(50)]
    public string? DefaultWarehouse { get; set; } // Mã kho

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
