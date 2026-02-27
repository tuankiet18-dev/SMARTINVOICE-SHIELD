using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInvoice.API.DTOs.Auth;
using SmartInvoice.API.Services.Interfaces;

namespace SmartInvoice.API.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("check-tax-code")]
        [AllowAnonymous]
        public async Task<IActionResult> CheckTaxCode([FromBody] CheckTaxCodeRequest request)
        {
            var result = await _authService.CheckTaxCodeAsync(request);
            return Ok(result);
        }

        [HttpPost("register-company")]
        [AllowAnonymous]
        public async Task<IActionResult> RegisterCompany([FromBody] RegisterCompanyRequest request)
        {
            try
            {
                await _authService.RegisterCompanyAsync(request);
                return Ok(new { Message = "Registration successful. Please check your email to verify your account." });
            }
            catch (Exception ex)
            {
                var message = ex.InnerException != null ? $"{ex.Message} -> {ex.InnerException.Message}" : ex.Message;
                return BadRequest(new { Message = message });
            }
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var response = await _authService.LoginAsync(request);
                SetRefreshTokenCookie(response.RefreshToken);
                response.RefreshToken = string.Empty; // Don't send in body
                return Ok(response);
            }
            catch (Exception ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
        }

        [HttpPost("refresh-token")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                var refreshToken = Request.Cookies["refreshToken"];
                if (string.IsNullOrEmpty(refreshToken))
                {
                    return Unauthorized(new { Message = "Refresh token is missing." });
                }

                // Since Cognito needs the token and we have the email in the request body
                var serviceRequest = new RefreshTokenRequest
                {
                    Email = request.Email,
                    RefreshToken = refreshToken
                };

                var response = await _authService.RefreshTokenAsync(serviceRequest);
                SetRefreshTokenCookie(response.RefreshToken);
                response.RefreshToken = string.Empty; // Don't send in body
                return Ok(response);
            }
            catch (Exception ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("refreshToken", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None
            });
            return Ok(new { Message = "Logged out successfully" });
        }

        [HttpPost("verify-email")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
        {
            try
            {
                await _authService.VerifyEmailAsync(request);
                return Ok(new { Message = "Email verified successfully. You can now login." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        private void SetRefreshTokenCookie(string refreshToken)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Expires = DateTime.UtcNow.AddDays(7), // Cognito default is often 30 days, adjust as needed
                Secure = true, // Required for cross-site (SameSite=None)
                SameSite = SameSiteMode.None // Allows frontend on localhost:3000 to send cookies to backend on localhost:5172
            };
            Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
        }
    }
}
