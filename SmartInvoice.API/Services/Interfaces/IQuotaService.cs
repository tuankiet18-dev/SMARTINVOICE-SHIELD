namespace SmartInvoice.API.Services.Interfaces;

public interface IQuotaService
{
    Task ValidateAndConsumeInvoiceQuotaAsync(Guid companyId);
}
