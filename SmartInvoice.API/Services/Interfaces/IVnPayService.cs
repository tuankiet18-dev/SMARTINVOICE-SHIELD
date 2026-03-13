using SmartInvoice.API.DTOs.Payment;

namespace SmartInvoice.API.Services.Interfaces;

public interface IVnPayService
{
    Task<List<SubscriptionPackageDto>> GetPackagesAsync();
    Task<CurrentSubscriptionDto> GetCurrentSubscriptionAsync(Guid companyId);
    Task<CreatePaymentResponse> CreatePaymentAsync(CreatePaymentRequest request, Guid companyId, Guid userId, string ipAddress);
    Task<PaymentResultDto> ProcessVnPayReturnAsync(Dictionary<string, string> vnpayData);
    Task<List<PaymentHistoryDto>> GetPaymentHistoryAsync(Guid companyId);
    List<AddonInfoDto> GetAvailableAddons();
    Task<CreatePaymentResponse> CreateAddonPaymentAsync(CreateAddonPaymentRequest request, Guid companyId, Guid userId, string ipAddress);
}
