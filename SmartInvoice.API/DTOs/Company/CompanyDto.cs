namespace SmartInvoice.API.DTOs.Company;

public class CompanyDto
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = null!;
    public string TaxCode { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? Website { get; set; }
    public string? LegalRepresentative { get; set; }
    public string? BusinessType { get; set; }
    public string? BusinessLicense { get; set; }
    public Guid SubscriptionPackageId { get; set; }
    public string SubscriptionTier { get; set; } = null!;
    public bool RequireTwoStepApproval { get; set; }
    public decimal? TwoStepApprovalThreshold { get; set; }
    public string BillingCycle { get; set; } = null!;
    public DateTime? SubscriptionStartDate { get; set; }
    public DateTime? SubscriptionExpiredAt { get; set; }
    public int MaxUsers { get; set; }
    public int MaxInvoicesPerMonth { get; set; }
    public int StorageQuotaGB { get; set; }
    public int UsedInvoicesThisMonth { get; set; }
    public long UsedStorageBytes { get; set; }
    public int CurrentActiveUsers { get; set; }
    public int ExtraInvoicesBalance { get; set; }
    public bool IsActive { get; set; }
}

public class CreateCompanyDto
{
    public string CompanyName { get; set; } = null!;
    public string TaxCode { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? Website { get; set; }
    public string? LegalRepresentative { get; set; }
    public string? BusinessType { get; set; }
    public string? BusinessLicense { get; set; }
    public Guid SubscriptionPackageId { get; set; }
}

public class UpdateCompanyDto
{
    public string CompanyName { get; set; } = null!;
    public string TaxCode { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? Website { get; set; }
    public string? LegalRepresentative { get; set; }
    public string? BusinessType { get; set; }
    public string? BusinessLicense { get; set; }
    public Guid SubscriptionPackageId { get; set; }
    public bool IsActive { get; set; }
}
