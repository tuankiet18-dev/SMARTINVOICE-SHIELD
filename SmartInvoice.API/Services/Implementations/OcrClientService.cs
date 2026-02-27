using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Entities.JsonModels;
using SmartInvoice.API.Services.Interfaces;

namespace SmartInvoice.API.Services.Implementations;

public class OcrClientService : IOcrClientService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OcrClientService> _logger;
    // Replace this with actual DbContext or appropriate repository to save AI logs
    // private readonly AppDbContext _context;

    public OcrClientService(HttpClient httpClient, ILogger<OcrClientService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        // _context = context;
    }

    public async Task<InvoiceExtractedData?> ExtractInvoiceDataAsync(string fileUrl, Guid fileId, Guid? invoiceId)
    {
        var aiLog = new AIProcessingLog
        {
            LogId = Guid.NewGuid(),
            FileId = fileId,
            InvoiceId = invoiceId,
            AIService = "INTERNAL_OCR_API",
            ProcessedAt = DateTime.UtcNow
        };

        try
        {
            var requestPayload = new { file_url = fileUrl };
            aiLog.RequestPayload = JsonSerializer.Serialize(requestPayload);

            // Assuming the internal API endpoint is loaded via HttpClient BaseAddress
            var response = await _httpClient.PostAsJsonAsync("/api/v1/extract", requestPayload);

            var responseContent = await response.Content.ReadAsStringAsync();
            aiLog.ResponsePayload = responseContent;

            if (response.IsSuccessStatusCode)
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var extractedData = JsonSerializer.Deserialize<InvoiceExtractedData>(responseContent, options);

                aiLog.Status = "SUCCESS";

                // _context.AIProcessingLogs.Add(aiLog);
                // await _context.SaveChangesAsync();

                return extractedData;
            }
            else
            {
                _logger.LogError("Internal OCR API failed with status {StatusCode}: {Error}", response.StatusCode, responseContent);
                aiLog.Status = "FAILED";
                aiLog.ErrorMessage = $"HTTP {response.StatusCode}: {responseContent}";

                // _context.AIProcessingLogs.Add(aiLog);
                // await _context.SaveChangesAsync();

                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while calling Internal OCR API");
            aiLog.Status = "FAILED";
            aiLog.ErrorMessage = ex.Message;

            // _context.AIProcessingLogs.Add(aiLog);
            // await _context.SaveChangesAsync();

            return null;
        }
    }
}
