using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInvoice.API.Entities;

[Table("PaymentTransactions")]
public class PaymentTransaction
{
    [Key]
    public Guid TransactionId { get; set; }

    // --- Relations ---
    public Guid CompanyId { get; set; }
    [ForeignKey(nameof(CompanyId))]
    public Company? Company { get; set; }

    public Guid? PackageId { get; set; }
    [ForeignKey(nameof(PackageId))]
    public SubscriptionPackage? Package { get; set; }

    // --- Payment Type ---
    [MaxLength(20)]
    public string PaymentType { get; set; } = "Subscription"; // Subscription, Addon

    // --- Payment Info ---
    [Required]
    [MaxLength(20)]
    public string BillingCycle { get; set; } = "Monthly"; // Monthly, SemiAnnual, Annual

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "VND";

    // --- VNPay Info ---
    [MaxLength(100)]
    public string? VnpTxnRef { get; set; } // Mã giao dịch bên mình

    [MaxLength(100)]
    public string? VnpTransactionNo { get; set; } // Mã giao dịch VNPay trả về

    [MaxLength(20)]
    public string? VnpResponseCode { get; set; } // 00 = success

    [MaxLength(50)]
    public string? VnpBankCode { get; set; }

    [MaxLength(20)]
    public string? VnpCardType { get; set; }

    [MaxLength(50)]
    public string? VnpPayDate { get; set; }

    // --- Status ---
    [MaxLength(20)]
    public string Status { get; set; } = "Pending"; // Pending, Success, Failed, Cancelled

    public string? FailReason { get; set; }

    // --- Metadata ---
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
