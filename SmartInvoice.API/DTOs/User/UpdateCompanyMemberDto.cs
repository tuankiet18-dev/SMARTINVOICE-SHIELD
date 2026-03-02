using System.ComponentModel.DataAnnotations;

namespace SmartInvoice.API.DTOs.User
{
    public class UpdateCompanyMemberDto
    {
        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = null!;

        [MaxLength(50)]
        public string? EmployeeId { get; set; }

        [Required]
        public string Role { get; set; } = null!;

        public List<string>? Permissions { get; set; }

        public bool IsActive { get; set; }
    }
}
