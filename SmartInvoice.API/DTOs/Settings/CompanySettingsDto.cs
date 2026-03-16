using System;

namespace SmartInvoice.API.DTOs.Settings
{
    public class CompanySettingsDto
    {
        public Guid CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string TaxCode { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }

        public bool IsAutoApproveEnabled { get; set; }
        public decimal AutoApproveThreshold { get; set; }
    }
}
