using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInvoice.API.Entities;

[Table("AIProcessingLogs")]
public class AIProcessingLog
{
    [Key]
    public Guid LogId { get; set; }

    public Guid FileId { get; set; }
    [ForeignKey(nameof(FileId))]
    public FileStorage? FileStorage { get; set; }

    public Guid? InvoiceId { get; set; }
    [ForeignKey(nameof(InvoiceId))]
    public Invoice? Invoice { get; set; }

    // --- AI Service Info ---
    [Required]
    [MaxLength(50)]
    public string AIService { get; set; } = null!; // TEXTRACT

    [MaxLength(100)]
    public string? AIModel { get; set; } // AnalyzeExpense

    [MaxLength(50)]
    public string AIRegion { get; set; } = "ap-southeast-1";

    // --- Payloads (JSONB) ---
    [Column(TypeName = "jsonb")]
    public string? RequestPayload { get; set; }

    [Column(TypeName = "jsonb")]
    public string? ResponsePayload { get; set; }

    // --- Result ---
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "SUCCESS"; // SUCCESS, FAILED

    public string? ErrorMessage { get; set; }
    [MaxLength(50)]
    public string? ErrorCode { get; set; }

    // --- Metrics ---
    [Column(TypeName = "decimal(5,2)")]
    public decimal? ConfidenceScore { get; set; }

    public int? ProcessingTimeMs { get; set; }
    public int? TokensUsed { get; set; }

    [Column(TypeName = "jsonb")]
    public string? ProcessedData { get; set; }

    [Column(TypeName = "decimal(10,4)")]
    public decimal? EstimatedCostUSD { get; set; }

    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
