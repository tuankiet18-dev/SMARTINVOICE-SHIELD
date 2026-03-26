using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;

namespace SmartInvoice.API.DTOs.User
{
    public class UserProfileDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string EmployeeId { get; set; } = null!;
        public Guid CompanyId { get; set; }
        public string? CompanyName { get; set; }
        public string Role { get; set; } = null!;
        public List<string>? Permissions { get; set; }
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }
}
