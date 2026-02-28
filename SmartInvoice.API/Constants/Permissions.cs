namespace SmartInvoice.API.Constants;

public static class Permissions
{
    // System Permissions
    public const string SystemView = "system:view";
    public const string SystemManage = "system:manage";

    // Company Permissions
    public const string CompanyView = "company:view";
    public const string CompanyManage = "company:manage";

    // Blacklist Permissions
    public const string BlacklistView = "blacklist:view";
    public const string BlacklistManage = "blacklist:manage";

    // User Permissions
    public const string UserView = "user:view";
    public const string UserManage = "user:manage";

    // Invoice Permissions
    public const string InvoiceView = "invoice:view";
    public const string InvoiceUpload = "invoice:upload";
    public const string InvoiceEdit = "invoice:edit";
    public const string InvoiceApprove = "invoice:approve";
    public const string InvoiceReject = "invoice:reject";
    public const string InvoiceOverrideRisk = "invoice:override_risk";

    // Report Permissions
    public const string ReportExport = "report:export";
}
