using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
using SmartInvoice.API.Entities.JsonModels;

namespace SmartInvoice.API.Entities;

[Table("Invoices")]
public class Invoice
{
    [Key]
    public Guid InvoiceId { get; set; }

    // --- Relations ---
    public Guid CompanyId { get; set; }
    [ForeignKey(nameof(CompanyId))]
    public Company? Company { get; set; }

    public int DocumentTypeId { get; set; }
    [ForeignKey(nameof(DocumentTypeId))]
    public DocumentType? DocumentType { get; set; }

    public Guid OriginalFileId { get; set; }
    [ForeignKey(nameof(OriginalFileId))]
    public FileStorage? OriginalFile { get; set; }

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
    [MaxLength(200)]
    public string? SellerName { get; set; }

    [MaxLength(14)]
    public string? SellerTaxCode { get; set; }

    public string? SellerAddress { get; set; }
    [MaxLength(20)]
    public string? SellerPhone { get; set; }
    [MaxLength(100)]
    public string? SellerEmail { get; set; }
    [MaxLength(50)]
    public string? SellerBankAccount { get; set; }
    [MaxLength(200)]
    public string? SellerBankName { get; set; }

    // --- Buyer Info ---
    [MaxLength(200)]
    public string? BuyerName { get; set; }

    [MaxLength(14)]
    public string? BuyerTaxCode { get; set; }

    public string? BuyerAddress { get; set; }
    [MaxLength(20)]
    public string? BuyerPhone { get; set; }
    [MaxLength(100)]
    public string? BuyerEmail { get; set; }
    [MaxLength(100)]
    public string? BuyerContactPerson { get; set; }

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

    [Column(TypeName = "jsonb")]
    public ValidationResultModel? ValidationResult { get; set; }

    // --- WORKFLOW & RISK ---
    [MaxLength(20)]
    public string Status { get; set; } = "Draft"; // Draft, Pending, Approved, Rejected, Archived

    [MaxLength(20)]
    public string RiskLevel { get; set; } = "Green"; // Green, Yellow, Orange, Red

    [Column(TypeName = "jsonb")]
    public List<RiskReason>? RiskReasons { get; set; }

    // --- VERSION CONTROL ---
    public bool IsReplaced { get; set; } = false;
    public Guid? ReplacedBy { get; set; }
    [ForeignKey(nameof(ReplacedBy))]
    public Invoice? ReplacementInvoice { get; set; }
    public int Version { get; set; } = 1;

    // --- USER TRACKING ---
    public Guid UploadedBy { get; set; }
    [ForeignKey(nameof(UploadedBy))]
    public User? Uploader { get; set; }

    public Guid? SubmittedBy { get; set; }
    [ForeignKey(nameof(SubmittedBy))]
    public User? Submitter { get; set; }

    public Guid? ApprovedBy { get; set; }
    [ForeignKey(nameof(ApprovedBy))]
    public User? Approver { get; set; }

    public Guid? RejectedBy { get; set; }
    [ForeignKey(nameof(RejectedBy))]
    public User? Rejector { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }

    // --- Metadata ---
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    // --- Navigation Properties ---
    public virtual ICollection<ValidationLayer> ValidationLayers { get; set; } = new List<ValidationLayer>();
    public virtual ICollection<RiskCheckResult> RiskCheckResults { get; set; } = new List<RiskCheckResult>();
    public virtual ICollection<InvoiceAuditLog> AuditLogs { get; set; } = new List<InvoiceAuditLog>();
}
