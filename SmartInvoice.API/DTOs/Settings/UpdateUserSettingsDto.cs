namespace SmartInvoice.API.DTOs.Settings
{
    public class UpdateUserSettingsDto
    {
        public string FullName { get; set; } = string.Empty;
        public string EmployeeId { get; set; } = string.Empty;
        public bool ReceiveEmailNotifications { get; set; }
        public bool ReceiveInAppNotifications { get; set; }
    }
}
