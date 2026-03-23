using System;
using System.ComponentModel.DataAnnotations;

namespace SmartInvoice.API.DTOs.SystemConfig
{
    public class SubscriptionPackageDto
    {
        public Guid PackageId { get; set; }
        public string PackageCode { get; set; } = null!;
        public string PackageName { get; set; } = null!;
        public string? Description { get; set; }
        public decimal PricePerMonth { get; set; }
        public decimal PricePerSixMonths { get; set; }
        public decimal PricePerYear { get; set; }
        public int MaxUsers { get; set; }
        public int MaxInvoicesPerMonth { get; set; }
        public int StorageQuotaGB { get; set; }
        public int PackageLevel { get; set; }
        public bool HasAiProcessing { get; set; }
        public bool HasAdvancedWorkflow { get; set; }
        public bool HasRiskWarning { get; set; }
        public bool HasAuditLog { get; set; }
        public bool HasErpIntegration { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateSubscriptionPackageDto
    {
        [Required]
        [MaxLength(20)]
        public string PackageCode { get; set; } = null!;
        
        [Required]
        [MaxLength(100)]
        public string PackageName { get; set; } = null!;
        
        [MaxLength(500)]
        public string? Description { get; set; }
        
        public decimal PricePerMonth { get; set; }
        public decimal PricePerSixMonths { get; set; }
        public decimal PricePerYear { get; set; }
        
        public int MaxUsers { get; set; }
        public int MaxInvoicesPerMonth { get; set; }
        public int StorageQuotaGB { get; set; }
        public int PackageLevel { get; set; }
        
        public bool HasAiProcessing { get; set; }
        public bool HasAdvancedWorkflow { get; set; }
        public bool HasRiskWarning { get; set; }
        public bool HasAuditLog { get; set; }
        public bool HasErpIntegration { get; set; }
    }

    public class UpdateSubscriptionPackageDto : CreateSubscriptionPackageDto
    {
        public bool IsActive { get; set; }
    }
}
