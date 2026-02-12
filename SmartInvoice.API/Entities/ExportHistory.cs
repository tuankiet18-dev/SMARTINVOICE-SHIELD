using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInvoice.API.Entities;

[Table("ExportHistories")]
public class ExportHistory
{
    [Key]
    public Guid ExportId { get; set; }

    public Guid CompanyId { get; set; }
    [ForeignKey(nameof(CompanyId))]
    public Company? Company { get; set; }

    public Guid ExportedBy { get; set; }
    [ForeignKey(nameof(ExportedBy))]
    public User? Exporter { get; set; }

    [Required]
    [MaxLength(20)]
    public string ExportFormat { get; set; } = null!; // EXCEL, CSV, PDF

    [Required]
    [MaxLength(50)]
    public string FileType { get; set; } = null!; // MISA, FAST, STANDARD

    [Column(TypeName = "jsonb")]
    public string? FilterCriteria { get; set; } // JSON filter params

    public int TotalRecords { get; set; }
    public long? FileSize { get; set; }

    [MaxLength(500)]
    public string? S3Key { get; set; } 

    public string? S3Url { get; set; }
    public DateTime? S3UrlExpiresAt { get; set; }

    public int DownloadCount { get; set; } = 0;
    public DateTime? LastDownloadAt { get; set; }

    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
}
