namespace SmartInvoice.API.DTOs.SQS;

/// <summary>
/// SQS message payload for the OCR processing queue.
/// Published by the upload endpoint, consumed by OcrWorkerService.
/// </summary>
public class OcrJobMessage
{
    /// <summary>ID of the draft invoice record in the database.</summary>
    public Guid InvoiceId { get; set; }

    /// <summary>S3 object key for the uploaded invoice image.</summary>
    public string S3Key { get; set; } = string.Empty;

    /// <summary>Company that owns this invoice.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>User who uploaded the invoice.</summary>
    public Guid UserId { get; set; }

    /// <summary>Original file name for content-type detection.</summary>
    public string FileName { get; set; } = string.Empty;
}
