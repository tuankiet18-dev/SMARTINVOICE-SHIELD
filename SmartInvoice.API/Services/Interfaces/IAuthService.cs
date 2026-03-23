using System.Threading.Tasks;
using SmartInvoice.API.DTOs.Auth;

namespace SmartInvoice.API.Services.Interfaces
{
    public interface IAuthService
    {
        Task<CheckTaxCodeResponse> CheckTaxCodeAsync(CheckTaxCodeRequest request);
        Task RegisterCompanyAsync(RegisterCompanyRequest request);
        Task<LoginResponse> LoginAsync(LoginRequest request);
        Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request);
        Task VerifyEmailAsync(VerifyEmailRequest request);
        Task ResendVerificationEmailAsync(ResendVerificationRequest request);        Task ForgotPasswordAsync(SmartInvoice.API.DTOs.Auth.ForgotPasswordRequest request);
        Task ConfirmForgotPasswordAsync(SmartInvoice.API.DTOs.Auth.ConfirmForgotPasswordRequest request);        Task<LoginResponse> RespondToNewPasswordRequiredAsync(RespondToNewPasswordRequest request);
        Task SeedSuperAdminAsync(SeedSuperAdminRequest request);
    }
}
