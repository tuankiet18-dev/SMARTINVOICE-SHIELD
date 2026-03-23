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
            _logger?.LogInformation("═══════════════════════════════════════════════════════════════");
            _logger?.LogInformation("🧠 [OCR_API] START ExtractInvoiceDataAsync");
            _logger?.LogInformation("   └─ FileUrl: {FileUrl}", fileUrl);
            _logger?.LogInformation("   └─ FileId: {FileId}", fileId);
            _logger?.LogInformation("   └─ InvoiceId: {InvoiceId}", invoiceId ?? Guid.Empty);
            _logger?.LogInformation("═══════════════════════════════════════════════════════════════");

            var requestPayload = new { file_url = fileUrl };
            aiLog.RequestPayload = JsonSerializer.Serialize(requestPayload);

            _logger?.LogInformation("[OCR_API] 📤 Sending request to OCR API endpoint: /api/v1/extract");
            _logger?.LogInformation("   └─ Payload: {Payload}", aiLog.RequestPayload);

            // Assuming the internal API endpoint is loaded via HttpClient BaseAddress
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await _httpClient.PostAsJsonAsync("/api/v1/extract", requestPayload);
            sw.Stop();

            var responseContent = await response.Content.ReadAsStringAsync();
            aiLog.ResponsePayload = responseContent;

            _logger?.LogInformation("[OCR_API] 📥 Received response (Status: {StatusCode}, Duration: {DurationMs}ms)", response.StatusCode, sw.ElapsedMilliseconds);

            if (response.IsSuccessStatusCode)
            {
                _logger?.LogInformation("[OCR_API] ✅ OCR extraction succeeded");
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var extractedData = JsonSerializer.Deserialize<InvoiceExtractedData>(responseContent, options);

                _logger?.LogInformation("   └─ InvoiceNumber: {InvoiceNumber}", extractedData?.InvoiceNumber ?? "N/A");
                _logger?.LogInformation("   └─ SellerTaxCode: {SellerTaxCode}", extractedData?.SellerTaxCode ?? "N/A");
                _logger?.LogInformation("   └─ Total Amount: {Amount}", extractedData?.TotalAmount ?? 0);
                
                aiLog.Status = "SUCCESS";

                // _context.AIProcessingLogs.Add(aiLog);
                // await _context.SaveChangesAsync();

                _logger?.LogInformation("[OCR_API] ✅ ExtractInvoiceDataAsync completed successfully");
                _logger?.LogInformation("═══════════════════════════════════════════════════════════════\n");
                return extractedData;
            }
            else
            {
                _logger?.LogError("[OCR_API] ❌ Internal OCR API failed with status {StatusCode}", response.StatusCode);
                _logger?.LogError("   └─ Response: {Error}", responseContent.Substring(0, Math.Min(500, responseContent.Length)));
                
                aiLog.Status = "FAILED";
                aiLog.ErrorMessage = $"HTTP {response.StatusCode}: {responseContent}";

                // _context.AIProcessingLogs.Add(aiLog);
                // await _context.SaveChangesAsync();

                _logger?.LogInformation("═══════════════════════════════════════════════════════════════\n");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[OCR_API] ❌ Exception while calling Internal OCR API");
            _logger?.LogError("   └─ ExceptionType: {ExceptionType}", ex.GetType().Name);
            _logger?.LogError("   └─ Message: {Message}", ex.Message);
            
            aiLog.Status = "FAILED";
            aiLog.ErrorMessage = ex.Message;

            // _context.AIProcessingLogs.Add(aiLog);
            // await _context.SaveChangesAsync();

            _logger?.LogInformation("═══════════════════════════════════════════════════════════════\n");
            return null;
        }
    }
}
