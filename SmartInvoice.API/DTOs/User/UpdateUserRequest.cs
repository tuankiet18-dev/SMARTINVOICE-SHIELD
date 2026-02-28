using System.ComponentModel.DataAnnotations;

namespace SmartInvoice.API.DTOs.User
{
    public class UpdateUserRequest
    {
        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? EmployeeId { get; set; }
    }
}
