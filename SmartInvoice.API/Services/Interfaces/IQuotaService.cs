namespace SmartInvoice.API.Services.Interfaces;

public interface IQuotaService
{
    Task ValidateAndConsumeInvoiceQuotaAsync(Guid companyId);
    
    // User Quota Management
    Task ValidateUserQuotaAsync(Guid companyId);
    Task IncreaseUserCountAsync(Guid companyId);
    Task DecreaseUserCountAsync(Guid companyId);

    // Storage Quota Management
    Task ValidateStorageQuotaAsync(Guid companyId, long fileSizeInBytes);
    Task ConsumeStorageQuotaAsync(Guid companyId, long fileSizeInBytes);
    Task ReleaseStorageQuotaAsync(Guid companyId, long fileSizeInBytes);
}
