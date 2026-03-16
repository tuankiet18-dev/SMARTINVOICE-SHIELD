using System;
using System.ComponentModel.DataAnnotations;

namespace SmartInvoice.API.DTOs.SystemConfig
{
    public class SystemConfigDto
    {
        public int ConfigId { get; set; }
        public string ConfigKey { get; set; } = null!;
        public string ConfigValue { get; set; } = null!;
        public string ConfigType { get; set; } = null!;
        public string? Category { get; set; }
        public string? Description { get; set; }
        public string? DefaultValue { get; set; }
        public bool IsReadOnly { get; set; }
        public bool RequiresRestart { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class UpdateSystemConfigDto
    {
        [Required]
        public string ConfigValue { get; set; } = null!;
    }
}
