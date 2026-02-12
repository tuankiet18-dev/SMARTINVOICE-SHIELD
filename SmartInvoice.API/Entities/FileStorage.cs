using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInvoice.API.Entities;

[Table("FileStorages")]
public class FileStorage
{
    [Key]
    public Guid FileId { get; set; }

    // --- Relations ---
    public Guid CompanyId { get; set; }
    [ForeignKey(nameof(CompanyId))]
    public Company? Company { get; set; }

    public Guid UploadedBy { get; set; }
    [ForeignKey(nameof(UploadedBy))]
    public User? Uploader { get; set; }

    // --- File Info ---
    [Required]
    [MaxLength(255)]
    public string OriginalFileName { get; set; } = null!;

    [Required]
    [MaxLength(10)]
    public string FileExtension { get; set; } = null!;

    public long FileSize { get; set; } // Bytes

    [Required]
    [MaxLength(100)]
    public string MimeType { get; set; } = null!;

    [MaxLength(64)]
    public string? FileHash { get; set; } // SHA-256

    // --- Storage Info (S3) ---
    [Required]
    [MaxLength(100)]
    public string S3BucketName { get; set; } = null!;

    [Required]
    [MaxLength(500)]
    public string S3Key { get; set; } = null!;

    [MaxLength(50)]
    public string S3Region { get; set; } = "ap-southeast-1";

    [MaxLength(100)]
    public string? S3VersionId { get; set; }

    // Temporary URL
    public string? S3Url { get; set; }
    public DateTime? S3UrlExpiresAt { get; set; }

    // --- Processing Status ---
    public bool IsProcessed { get; set; } = false;
    public DateTime? ProcessedAt { get; set; }
    public string? ProcessingError { get; set; }

    // --- Lifecycle ---
    public bool ArchivedToGlacier { get; set; } = false;
    public DateTime? ArchivedAt { get; set; }
    
    public bool DeletedFromS3 { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
