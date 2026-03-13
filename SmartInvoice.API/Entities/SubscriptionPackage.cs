using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInvoice.API.Entities;

[Table("SubscriptionPackages")]
public class SubscriptionPackage
{
    [Key]
    public Guid PackageId { get; set; }

    [Required]
    [MaxLength(20)]
    public string PackageCode { get; set; } = null!; // FREE, STARTER, PRO, ENTERPRISE

    [Required]
    [MaxLength(100)]
    public string PackageName { get; set; } = null!;

    [MaxLength(500)]
    public string? Description { get; set; }

    // --- Pricing (VND) ---
    [Column(TypeName = "decimal(18,2)")]
    public decimal PricePerMonth { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PricePerSixMonths { get; set; } // Giảm giá ~1 tháng so với mua lẻ

    [Column(TypeName = "decimal(18,2)")]
    public decimal PricePerYear { get; set; } // Giảm giá ~2 tháng so với mua lẻ

    // --- Quotas ---
    public int MaxUsers { get; set; }
    public int MaxInvoicesPerMonth { get; set; }
    public int StorageQuotaGB { get; set; }

    // --- Package Level (for upgrade/downgrade comparison) ---
    public int PackageLevel { get; set; } // Free=1, Starter=2, Pro=3, Enterprise=4

    // --- Feature Flags ---
    public bool HasAiProcessing { get; set; }
    public bool HasAdvancedWorkflow { get; set; }
    public bool HasRiskWarning { get; set; }
    public bool HasAuditLog { get; set; }
    public bool HasErpIntegration { get; set; }

    // --- Status ---
    public bool IsActive { get; set; } = true;

    // --- Metadata ---
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
