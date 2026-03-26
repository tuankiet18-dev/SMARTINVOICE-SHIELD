namespace SmartInvoice.API.DTOs.Auth
{
    public class ConfirmForgotPasswordRequest
    {
        public string Email { get; set; } = string.Empty;
        public string ConfirmationCode { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}