using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SmartInvoice.API.Entities.JsonModels;

namespace SmartInvoice.API.Entities;

[Table("Invoices")]
public class Invoice
{
    public Invoice()
    {
        CheckResults = new List<InvoiceCheckResult>();
        AuditLogs = new List<InvoiceAuditLog>();
    }

    [Key]
    public Guid InvoiceId { get; set; }

    // --- Relations ---
    public Guid CompanyId { get; set; }

    [ForeignKey(nameof(CompanyId))]
    public Company? Company { get; set; }

    public int DocumentTypeId { get; set; }

    [ForeignKey(nameof(DocumentTypeId))]
    public DocumentType? DocumentType { get; set; }

    public Guid? OriginalFileId { get; set; }

    [ForeignKey(nameof(OriginalFileId))]
    public FileStorage? OriginalFile { get; set; }

    public Guid? VisualFileId { get; set; }

    [ForeignKey(nameof(VisualFileId))]
    public FileStorage? VisualFile { get; set; }

    // --- Processing Method ---
    [MaxLength(10)]
    public string ProcessingMethod { get; set; } = "XML"; // XML, OCR, MANUAL

    // --- CORE FIELDS (NĐ 123/2020) ---
    [MaxLength(20)]
    public string? FormNumber { get; set; } // 01GTKT

    [MaxLength(50)]
    public string? SerialNumber { get; set; } // C24T

    [Required]
    [MaxLength(50)]
    public string InvoiceNumber { get; set; } = null!;

    public DateTime InvoiceDate { get; set; }

    [MaxLength(3)]
    public string InvoiceCurrency { get; set; } = "VND";

    [Column(TypeName = "decimal(18,6)")]
    public decimal ExchangeRate { get; set; } = 1;

    // --- Seller Info ---
    public SellerInfo Seller { get; set; } = new();

    // --- Buyer Info ---
    public BuyerInfo Buyer { get; set; } = new();

    // --- Amounts ---
    [Column(TypeName = "decimal(18,2)")]
    public decimal? TotalAmountBeforeTax { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? TotalTaxAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    public string? TotalAmountInWords { get; set; }

    // --- Additional Info ---
    [MaxLength(100)]
    public string? PaymentMethod { get; set; }

    [MaxLength(50)]
    public string? MCCQT { get; set; } // Mã cơ quan thuế

    public string? Notes { get; set; }

    // --- FLEXIBLE DATA (JSONB) ---
    [Column(TypeName = "jsonb")]
    public InvoiceRawData? RawData { get; set; }

    [Column(TypeName = "jsonb")]
    public InvoiceExtractedData? ExtractedData { get; set; }

    // --- WORKFLOW & RISK ---
    [MaxLength(20)]
    public string Status { get; set; } = "Draft"; // Draft, Pending, Approved, Rejected, Archived

    [MaxLength(20)]
    public string RiskLevel { get; set; } = "Green"; // Green, Yellow, Orange, Red

    // --- OCR METADATA ---
    public float? OcrConfidenceScore { get; set; }

    // --- VERSION CONTROL ---
    public bool IsReplaced { get; set; } = false;
    public Guid? ReplacedBy { get; set; }

    [ForeignKey(nameof(ReplacedBy))]
    public Invoice? ReplacementInvoice { get; set; }
    public int Version { get; set; } = 1;

    // --- USER TRACKING & WORKFLOW ---
    public InvoiceWorkflow Workflow { get; set; } = new();

    // --- Metadata ---
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // --- Navigation Properties ---
    public virtual ICollection<InvoiceCheckResult> CheckResults { get; set; }
    public virtual ICollection<InvoiceAuditLog> AuditLogs { get; set; }
}

[Owned]
public class SellerInfo
{
    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(14)]
    public string? TaxCode { get; set; }
    public string? Address { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? BankAccount { get; set; }

    [MaxLength(200)]
    public string? BankName { get; set; }
}

[Owned]
public class BuyerInfo
{
    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(14)]
    public string? TaxCode { get; set; }
    public string? Address { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(100)]
    public string? ContactPerson { get; set; }
}

[Owned]
public class InvoiceWorkflow
{
    public Guid UploadedBy { get; set; }

    [ForeignKey(nameof(UploadedBy))]
    public User? Uploader { get; set; }

    public Guid? SubmittedBy { get; set; }

    [ForeignKey(nameof(SubmittedBy))]
    public User? Submitter { get; set; }
    public DateTime? SubmittedAt { get; set; }

    public Guid? Level1ApprovedBy { get; set; }

    [ForeignKey(nameof(Level1ApprovedBy))]
    public User? Level1Approver { get; set; }
    public DateTime? Level1ApprovedAt { get; set; }
    public Guid? Level2ApprovedBy { get; set; }

    [ForeignKey(nameof(Level2ApprovedBy))]
    public User? Level2Approver { get; set; }
    public DateTime? Level2ApprovedAt { get; set; }
    public int CurrentApprovalStep { get; set; } = 1;
    public Guid? ApprovedBy { get; set; }

    [ForeignKey(nameof(ApprovedBy))]
    public User? Approver { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public Guid? RejectedBy { get; set; }

    [ForeignKey(nameof(RejectedBy))]
    public User? Rejector { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
}
