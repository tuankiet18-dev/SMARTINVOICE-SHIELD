namespace SmartInvoice.API.DTOs.Payment;

public class CreatePaymentRequest
{
    public Guid PackageId { get; set; }
    public string BillingCycle { get; set; } = "Monthly"; // Monthly, SemiAnnual, Annual
}

public class CreatePaymentResponse
{
    public string PaymentUrl { get; set; } = null!;
    public string TransactionId { get; set; } = null!;
}

public class PaymentResultDto
{
    public string TransactionId { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? PackageName { get; set; }
    public string? BillingCycle { get; set; }
    public decimal Amount { get; set; }
    public string? VnpTransactionNo { get; set; }
    public string? BankCode { get; set; }
    public string? PayDate { get; set; }
    public string? Message { get; set; }
}

public class SubscriptionPackageDto
{
    public Guid PackageId { get; set; }
    public string PackageCode { get; set; } = null!;
    public string PackageName { get; set; } = null!;
    public string? Description { get; set; }
    public int PackageLevel { get; set; }
    public decimal PricePerMonth { get; set; }
    public decimal PricePerSixMonths { get; set; }
    public decimal PricePerYear { get; set; }
    public int MaxUsers { get; set; }
    public int MaxInvoicesPerMonth { get; set; }
    public int StorageQuotaGB { get; set; }
    public bool HasAiProcessing { get; set; }
    public bool HasAdvancedWorkflow { get; set; }
    public bool HasRiskWarning { get; set; }
    public bool HasAuditLog { get; set; }
    public bool HasErpIntegration { get; set; }
    public bool IsActive { get; set; }
}

public class CurrentSubscriptionDto
{
    public string? PackageCode { get; set; }
    public string? PackageName { get; set; }
    public int PackageLevel { get; set; }
    public string SubscriptionTier { get; set; } = "Free";
    public string BillingCycle { get; set; } = "Monthly";
    public DateTime? SubscriptionStartDate { get; set; }
    public DateTime? SubscriptionExpiredAt { get; set; }
    public int MaxUsers { get; set; }
    public int MaxInvoicesPerMonth { get; set; }
    public int StorageQuotaGB { get; set; }
    // Quota Usage
    public int UsedInvoicesThisMonth { get; set; }
    public int ExtraInvoicesBalance { get; set; }
    // Feature Flags
    public bool HasAiProcessing { get; set; }
    public bool HasAdvancedWorkflow { get; set; }
    public bool HasRiskWarning { get; set; }
    public bool HasAuditLog { get; set; }
    public bool HasErpIntegration { get; set; }
}

public class PaymentHistoryDto
{
    public Guid TransactionId { get; set; }
    public string? PackageName { get; set; }
    public string BillingCycle { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Status { get; set; } = null!;
    public string? VnpTransactionNo { get; set; }
    public string PaymentType { get; set; } = "Subscription";
    public DateTime CreatedAt { get; set; }
}

public class AddonInfoDto
{
    public string AddonCode { get; set; } = null!;
    public string AddonName { get; set; } = null!;
    public string Description { get; set; } = null!;
    public int InvoiceCount { get; set; }
    public decimal Price { get; set; }
}

public class CreateAddonPaymentRequest
{
    public string AddonCode { get; set; } = "ADDON_50_INVOICES";
}
