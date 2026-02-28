using System;
using System.Collections.Generic;

namespace SmartInvoice.API.DTOs.User
{
    public class UserProfileDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? EmployeeId { get; set; }
        public Guid CompanyId { get; set; }
        public string Role { get; set; } = string.Empty;
        public List<string>? Permissions { get; set; }
        public bool IsActive { get; set; }
    }
}
