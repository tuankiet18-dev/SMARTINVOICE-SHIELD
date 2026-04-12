using SmartInvoice.API.Entities;
using SmartInvoice.API.Entities.JsonModels;

namespace SmartInvoice.Tests.Helpers;

/// <summary>
/// Factory methods tạo các test entities với giá trị mặc định hợp lệ.
/// Giúp các test class không bị lặp code setup.
/// </summary>
public static class InvoiceTestFactory
{
    // ─────────────────────────────────────────
    //  Invoice
    // ─────────────────────────────────────────

    public static Invoice CreateDraftInvoice(
        Guid? invoiceId = null,
        Guid? companyId = null,
        Guid? uploadedBy = null,
        string invoiceNumber = "INV-001",
        decimal totalAmount = 1_000_000m)
    {
        return new Invoice
        {
            InvoiceId     = invoiceId ?? Guid.NewGuid(),
            CompanyId     = companyId ?? Guid.NewGuid(),
            InvoiceNumber = invoiceNumber,
            SerialNumber  = "C24T",
            FormNumber    = "01GTKT",
            InvoiceDate   = DateTime.UtcNow.Date,
            Status        = "Draft",
            RiskLevel     = "Green",
            ProcessingMethod = "XML",
            TotalAmount   = totalAmount,
            InvoiceCurrency = "VND",
            Seller = new SellerInfo
            {
                Name    = "Công ty TNHH ABC",
                TaxCode = "0123456789",
                Address = "123 Đường Test, Hà Nội"
            },
            Buyer = new BuyerInfo
            {
                Name    = "Công ty TNHH XYZ",
                TaxCode = "9876543210",
                Address = "456 Đường Sample, HCM"
            },
            Workflow = new InvoiceWorkflow
            {
                UploadedBy = uploadedBy ?? Guid.NewGuid()
            },
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static Invoice CreatePendingInvoice(
        Guid? invoiceId = null,
        Guid? companyId = null,
        Guid? uploadedBy = null,
        decimal totalAmount = 1_000_000m)
    {
        var inv = CreateDraftInvoice(invoiceId, companyId, uploadedBy, "INV-PENDING-001", totalAmount);
        inv.Status = "Pending";
        inv.Workflow.SubmittedBy = uploadedBy ?? Guid.NewGuid();
        inv.Workflow.SubmittedAt = DateTime.UtcNow;
        return inv;
    }

    public static Invoice CreateApprovedInvoice(
        Guid? invoiceId = null,
        Guid? companyId = null)
    {
        var inv = CreatePendingInvoice(invoiceId, companyId);
        inv.InvoiceNumber = "INV-APPROVED-001";
        inv.Status = "Approved";
        inv.Workflow.ApprovedBy = Guid.NewGuid();
        inv.Workflow.ApprovedAt = DateTime.UtcNow;
        return inv;
    }

    public static Invoice CreateDeletedInvoice(
        Guid? invoiceId = null,
        Guid? companyId = null)
    {
        var inv = CreateDraftInvoice(invoiceId, companyId);
        inv.IsDeleted = true;
        inv.DeletedAt = DateTime.UtcNow;
        return inv;
    }

    // ─────────────────────────────────────────
    //  Company
    // ─────────────────────────────────────────

    public static Company CreateActiveCompany(
        Guid? companyId = null,
        int maxInvoices = 100,
        int usedInvoices = 0,
        long storageQuotaGB = 5,
        long usedStorageBytes = 0,
        int maxUsers = 5,
        int currentUsers = 0)
    {
        return new Company
        {
            CompanyId            = companyId ?? Guid.NewGuid(),
            CompanyName          = "Test Company Ltd.",
            TaxCode              = "0123456789",
            Email                = "contact@testcompany.com",
            PhoneNumber          = "0901234567",
            Address              = "123 Test Street",
            SubscriptionTier     = "Professional",
            SubscriptionPackageId = Guid.NewGuid(),
            MaxInvoicesPerMonth  = maxInvoices,
            UsedInvoicesThisMonth = usedInvoices,
            StorageQuotaGB       = (int)storageQuotaGB,
            UsedStorageBytes     = usedStorageBytes,
            MaxUsers             = maxUsers,
            CurrentActiveUsers   = currentUsers,
            CurrentCycleStart    = DateTime.UtcNow.AddDays(-5),
            IsActive             = true,
            CreatedAt            = DateTime.UtcNow
        };
    }

    // ─────────────────────────────────────────
    //  LocalBlacklist
    // ─────────────────────────────────────────

    public static LocalBlacklistedCompany CreateBlacklistEntry(
        string taxCode = "BAD0000001",
        bool isActive = true,
        string reason = "Gian lận thuế")
    {
        return new LocalBlacklistedCompany
        {
            BlacklistId = Guid.NewGuid(),
            TaxCode     = taxCode,
            CompanyName = "Công ty Vi Phạm",
            Reason      = reason,
            IsActive    = isActive,
            AddedDate   = DateTime.UtcNow
        };
    }

    // ─────────────────────────────────────────
    //  FileStorage
    // ─────────────────────────────────────────

    public static FileStorage CreateFileStorage(
        Guid? fileId = null,
        string s3Key = "raw/test-file.pdf",
        long fileSize = 512_000)
    {
        return new FileStorage
        {
            FileId           = fileId ?? Guid.NewGuid(),
            S3Key            = s3Key,
            S3BucketName     = "smartinvoice-test-bucket",
            FileSize         = fileSize,
            OriginalFileName = "test-invoice.pdf",
            FileExtension    = ".pdf",
            MimeType         = "application/pdf",
            CompanyId        = Guid.NewGuid(),
            UploadedBy       = Guid.NewGuid(),
            CreatedAt        = DateTime.UtcNow
        };
    }
}
