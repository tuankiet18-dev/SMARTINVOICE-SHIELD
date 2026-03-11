using System.ComponentModel.DataAnnotations;

namespace SmartInvoice.API.DTOs.Auth
{
    public class SeedSuperAdminRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        [MinLength(8)]
        public string Password { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = null!;
    }
}
