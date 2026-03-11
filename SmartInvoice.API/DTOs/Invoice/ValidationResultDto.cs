using System.Collections.Generic;
using System.Linq;

namespace SmartInvoice.API.DTOs.Invoice
{
    public class ValidationErrorDetail
    {
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Suggestion { get; set; }
    }

    public class ValidationResultDto
    {
        public bool IsValid => !ErrorDetails.Any();

        public List<ValidationErrorDetail> ErrorDetails { get; set; } = new List<ValidationErrorDetail>();
        public List<ValidationErrorDetail> WarningDetails { get; set; } = new List<ValidationErrorDetail>();

        // For backward compatibility
        public List<string?> Errors => ErrorDetails.Select(e => e.ErrorMessage).ToList();
        public List<string?> Warnings => WarningDetails.Select(w => w.ErrorMessage).ToList();

        public string? SignerSubject { get; set; }

        // Versioning properties
        public bool IsReplacement { get; set; } = false;
        public System.Guid? ReplacedInvoiceId { get; set; }
        public int NewVersion { get; set; } = 1;

        // ID of the saved invoice in DB (null if fatal error, not saved)
        public System.Guid? InvoiceId { get; set; }

        // This holds the actual data parsed from the invoice (LineItems, etc.)
        public SmartInvoice.API.Entities.JsonModels.InvoiceExtractedData? ExtractedData { get; set; }

        public void AddError(string? errorCode, string? message, string? suggestion = null)
        {
            ErrorDetails.Add(new ValidationErrorDetail { ErrorCode = errorCode, ErrorMessage = message, Suggestion = suggestion });
        }

        public void AddError(string? message)
        {
            ErrorDetails.Add(new ValidationErrorDetail { ErrorCode = "ERR_UNKNOWN", ErrorMessage = message });
        }

        public void AddWarning(string? errorCode, string? message, string? suggestion = null)
        {
            WarningDetails.Add(new ValidationErrorDetail { ErrorCode = errorCode, ErrorMessage = message, Suggestion = suggestion });
        }

        public void AddWarning(string? message)
        {
            WarningDetails.Add(new ValidationErrorDetail { ErrorCode = "WARN_UNKNOWN", ErrorMessage = message });
        }
    }
}
