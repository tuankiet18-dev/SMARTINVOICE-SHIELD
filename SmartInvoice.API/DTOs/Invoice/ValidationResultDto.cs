using System.Collections.Generic;
using System.Linq;

namespace SmartInvoice.API.DTOs.Invoice
{
    /// <summary>
    /// Defines how a new upload interacts with an existing Invoice record (Invoice Dossier model).
    /// </summary>
    public enum DossierMergeMode
    {
        /// <summary>No merge — this is a brand new invoice.</summary>
        None,
        /// <summary>Case 3A: An XML is uploaded for an existing OCR-only record. Override data with XML (Source of Truth).</summary>
        XmlOverridesOcr,
        /// <summary>Case 3B: An OCR (Visual) file is uploaded for an existing XML record. Only attach the visual file, do NOT override data.</summary>
        OcrAttachesToXml
    }

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

        // Auto-approval flag — set when invoice was automatically approved per company configuration
        public bool IsAutoApproved { get; set; } = false;

        // This holds the actual data parsed from the invoice (LineItems, etc.)
        public SmartInvoice.API.Entities.JsonModels.InvoiceExtractedData? ExtractedData { get; set; }

        // --- Invoice Dossier Merge ---
        /// <summary>Indicates the merge mode for this upload (None, XmlOverridesOcr, OcrAttachesToXml).</summary>
        public DossierMergeMode MergeMode { get; set; } = DossierMergeMode.None;
        /// <summary>The existing Invoice ID to merge into (only set when MergeMode != None).</summary>
        public System.Guid? MergeTargetInvoiceId { get; set; }

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
