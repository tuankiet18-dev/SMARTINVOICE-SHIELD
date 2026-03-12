namespace SmartInvoice.API.DTOs.SQS
{
    /// <summary>
    /// Message payload for publishing VietQR tax code validation requests to SQS.
    /// This message is consumed by VietQrSqsConsumerService to asynchronously validate
    /// tax codes without blocking the invoice upload flow.
    /// </summary>
    public class VietQrValidationMessage
    {
        /// <summary>
        /// The invoice ID that needs VietQR tax code validation.
        /// This is used to locate and update the invoice record in the database after validation.
        /// </summary>
        public Guid InvoiceId { get; set; }

        /// <summary>
        /// The Vietnamese tax code (MST) to validate against the VietQR API.
        /// Format: 10 digits, 10 digits + 3-digit suffix, or 12 digits.
        /// </summary>
        public string TaxCode { get; set; } = string.Empty;

        /// <summary>
        /// Optional seller name for cross-verification against VietQR registered name.
        /// Used to detect seller name mismatches (e.g., "ABC Corp" vs "ABC CORPORATION").
        /// </summary>
        public string? SellerName { get; set; }

        /// <summary>
        /// Timestamp when the message was created (for debugging and audit trails).
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Optional tracing correlation ID for distributed tracing across services.
        /// </summary>
        public string? CorrelationId { get; set; }
    }
}
