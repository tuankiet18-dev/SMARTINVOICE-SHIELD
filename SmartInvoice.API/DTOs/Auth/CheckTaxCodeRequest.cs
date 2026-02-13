using System.ComponentModel.DataAnnotations;

namespace SmartInvoice.API.DTOs.Auth
{
    public class CheckTaxCodeRequest
    {
        [Required]
        // [RegularExpression(@"^[0-9]{10}(-[0-9]{3})?$", ErrorMessage = "Invalid Tax Code format")]
        public string TaxCode { get; set; } = string.Empty;
    }

    public class CheckTaxCodeResponse
    {
        public bool IsValid { get; set; }
        public bool IsRegistered { get; set; }
        public string? CompanyName { get; set; }
        public string? Address { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
