using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInvoice.API.Entities;

[Table("Notifications")]
public class Notification
{
    [Key]
    public Guid NotificationId { get; set; }

    // --- User Relation ---
    public Guid UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    // --- Info ---
    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = null!;

    [MaxLength(20)]
    public string Priority { get; set; } = "Normal"; // Low, Normal, High, Urgent

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = null!;

    [Required]
    public string Message { get; set; } = null!;

    [MaxLength(500)]
    public string? ActionUrl { get; set; }

    [MaxLength(50)]
    public string? ActionText { get; set; }

    // --- Related ---
    public Guid? RelatedInvoiceId { get; set; }
    [ForeignKey(nameof(RelatedInvoiceId))]
    public Invoice? RelatedInvoice { get; set; }
    
    public Guid? RelatedUserId { get; set; }
    [ForeignKey(nameof(RelatedUserId))]
    public User? RelatedUser { get; set; }

    // --- Status ---
    public bool IsRead { get; set; } = false;
    public bool IsArchived { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
