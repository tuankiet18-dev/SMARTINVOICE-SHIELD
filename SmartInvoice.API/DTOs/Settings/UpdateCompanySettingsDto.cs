namespace SmartInvoice.API.DTOs.Settings
{
    public class UpdateCompanySettingsDto
    {
        public bool IsAutoApproveEnabled { get; set; }
        public decimal AutoApproveThreshold { get; set; }
        public bool RequireTwoStepApproval { get; set; }
        public decimal TwoStepApprovalThreshold { get; set; }
    }
}

