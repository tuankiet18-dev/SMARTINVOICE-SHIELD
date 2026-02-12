using System.ComponentModel.DataAnnotations;

namespace SmartInvoice.API.DTOs.Auth
{
    public class RegisterCompanyRequest
    {
        // Company Info
        [Required]
        public string TaxCode { get; set; } = string.Empty;

        [Required]
        public string CompanyName { get; set; } = string.Empty;

        [Required]
        public string Address { get; set; } = string.Empty;

        [EmailAddress]
        public string? CompanyEmail { get; set; }

        public string? SubscriptionTier { get; set; } = "Free";

        // Admin Info
        [Required]
        public string AdminFullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string AdminEmail { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;
    }
}
