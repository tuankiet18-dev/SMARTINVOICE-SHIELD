using System;

namespace SmartInvoice.API.DTOs.Settings
{
    public class UserSettingsDto
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? EmployeeId { get; set; }
        public bool ReceiveEmailNotifications { get; set; }
        public bool ReceiveInAppNotifications { get; set; }
    }
}
