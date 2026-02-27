using SmartInvoice.API.Entities;
using SmartInvoice.API.Entities.JsonModels;

namespace SmartInvoice.API.Services.Interfaces;

public interface IOcrClientService
{
    /// <summary>
    /// Sends an invoice document (via S3 URL or direct file upload) to an internal OCR API for data extraction.
    /// </summary>
    Task<InvoiceExtractedData?> ExtractInvoiceDataAsync(string fileUrl, Guid fileId, Guid? invoiceId);
}
