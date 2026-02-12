using System.Threading.Tasks;
using SmartInvoice.API.DTOs.Auth;

namespace SmartInvoice.API.Services.Interfaces
{
    public interface IAuthService
    {
        Task<CheckTaxCodeResponse> CheckTaxCodeAsync(CheckTaxCodeRequest request);
        Task RegisterCompanyAsync(RegisterCompanyRequest request);
        Task<LoginResponse> LoginAsync(LoginRequest request);
        Task VerifyEmailAsync(VerifyEmailRequest request);
    }
}
