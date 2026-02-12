using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInvoice.API.Entities;

[Table("SystemConfigurations")]
public class SystemConfiguration
{
    [Key]
    public int ConfigId { get; set; }

    [Required]
    [MaxLength(100)]
    public string ConfigKey { get; set; } = null!;

    [Required]
    public string ConfigValue { get; set; } = null!;

    [MaxLength(20)]
    public string ConfigType { get; set; } = "String"; // String, Integer, Boolean, JSON, Secret

    // --- Metadata ---
    [MaxLength(50)]
    public string? Category { get; set; } // AWS, Validation
    
    public string? Description { get; set; }
    public string? DefaultValue { get; set; }
    
    public bool IsEncrypted { get; set; } = false;
    public bool IsReadOnly { get; set; } = false;
    public bool RequiresRestart { get; set; } = false;

    // --- Tracking ---
    public Guid? UpdatedBy { get; set; }
    [ForeignKey(nameof(UpdatedBy))]
    public User? Updater { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
