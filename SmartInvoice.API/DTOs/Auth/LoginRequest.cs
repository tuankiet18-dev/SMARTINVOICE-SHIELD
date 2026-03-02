using System.ComponentModel.DataAnnotations;

namespace SmartInvoice.API.DTOs.Auth
{
    public class LoginRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string IdToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime Expiration { get; set; }
        public SmartInvoice.API.DTOs.User.UserProfileDto? User { get; set; }

        public string? ChallengeName { get; set; } // NEW_PASSWORD_REQUIRED
        public string? Session { get; set; } // Required to respond to challenge
    }

    public class RespondToNewPasswordRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        public string Session { get; set; } = string.Empty;
    }

    public class RefreshTokenRequest
    {
        public string RefreshToken { get; set; } = string.Empty; // Optional, set by controller

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
