using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInvoice.API.Entities;

[Table("LocalBlacklist")]
public class LocalBlacklistedCompany
{
    [Key]
    public Guid BlacklistId { get; set; }

    [Required]
    [MaxLength(14)]
    public string TaxCode { get; set; } = null!;

    [MaxLength(200)]
    public string? CompanyName { get; set; }

    public string? Reason { get; set; }

    public bool IsActive { get; set; } = true;

    // --- Audit ---
    public Guid? AddedBy { get; set; } // User ID
    public DateTime AddedDate { get; set; } = DateTime.UtcNow;
    public DateTime? RemovedDate { get; set; }
}
