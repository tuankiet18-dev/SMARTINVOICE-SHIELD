using System.ComponentModel.DataAnnotations;

namespace SmartInvoice.API.DTOs.User
{
    public class CreateCompanyMemberDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = null!;

        [Required(ErrorMessage = "Mã nhân viên là bắt buộc")]
        [MaxLength(50)]
        public string EmployeeId { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string Role { get; set; } = "Member";

        public List<string>? Permissions { get; set; }
    }
}
