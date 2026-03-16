using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInvoice.API.Entities;

[Table("Companies")]
public class Company : ISoftDelete
{
    [Key]
    public Guid CompanyId { get; set; }

    // --- Basic Info ---
    [Required]
    [MaxLength(200)]
    public string CompanyName { get; set; } = null!;

    [Required]
    [MaxLength(14)]
    public string TaxCode { get; set; } = null!; // Unique Index needed

    [Required]
    [MaxLength(100)]
    public string Email { get; set; } = null!;

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    public string? Address { get; set; }

    [MaxLength(200)]
    public string? Website { get; set; }

    // --- Business Info ---
    [MaxLength(100)]
    public string? LegalRepresentative { get; set; }

    [MaxLength(50)]
    public string? BusinessType { get; set; } // TNHH, CP...

    [MaxLength(50)]
    public string? BusinessLicense { get; set; }

    // --- Subscription Info ---
    public Guid? SubscriptionPackageId { get; set; }
    [ForeignKey(nameof(SubscriptionPackageId))]
    public SubscriptionPackage? SubscriptionPackage { get; set; }

    [MaxLength(50)]
    public string SubscriptionTier { get; set; } = "Free"; // Free, Starter, Professional, Enterprise

    [MaxLength(20)]
    public string BillingCycle { get; set; } = "Monthly"; // Monthly, SemiAnnual, Annual

    public DateTime? SubscriptionStartDate { get; set; }
    public DateTime? SubscriptionExpiredAt { get; set; }

    public int MaxUsers { get; set; } = 5;
    public int MaxInvoicesPerMonth { get; set; } = 100;
    public int StorageQuotaGB { get; set; } = 5;

    // --- Quota Tracking (Lazy Evaluation) ---
    public int UsedInvoicesThisMonth { get; set; } = 0;
    public int ExtraInvoicesBalance { get; set; } = 0;
    public DateTime CurrentCycleStart { get; set; } = DateTime.UtcNow;

    // --- Configurations ---
    public bool IsAutoApproveEnabled { get; set; } = false;
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal AutoApproveThreshold { get; set; } = 0;

    // --- Status ---
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;

    // --- Metadata ---
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
}
