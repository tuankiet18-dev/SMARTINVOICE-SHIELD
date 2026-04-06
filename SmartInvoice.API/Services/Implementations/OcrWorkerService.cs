using SmartInvoice.API.Constants;
using SmartInvoice.API.DTOs.Invoice;
using SmartInvoice.API.DTOs.SQS;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Entities.JsonModels;
using SmartInvoice.API.Repositories.Interfaces;
using SmartInvoice.API.Services.Interfaces;
using System.Text.Json;

namespace SmartInvoice.API.Services.Implementations;

/// <summary>
/// Background service that polls the OCR SQS queue, downloads invoice images from S3,
/// sends them to the local Python OCR API, and updates the database with extraction results.
///
/// Architecture follows the same pattern as VietQrSqsConsumerService:
/// - Long polling (20s wait) to minimize API calls
/// - New DI scope per message to isolate EF Core contexts
/// - Failed messages stay in queue for retry; permanently bad messages get deleted
/// </summary>
public class OcrWorkerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OcrWorkerService> _logger;
    private readonly SemaphoreSlim _concurrencySemaphore = new(1, 1); // Sequential: 1 at a time to match single-worker OCR Python server
    private string? _queueUrl;
    private const int WaitTimeSeconds = 20;
    private const int MaxNumberOfMessages = 1; // Sequential: 1 message per poll to avoid GIL contention & Gemini 429

    public OcrWorkerService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OcrWorkerService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 OcrWorkerService is starting...");

        try
        {
            _logger.LogInformation(">>> [OCR_WORKER] Attempting to find SQS URL in configuration...");
            _queueUrl = _configuration["AWS_SQS_OCR_URL"];
            if (string.IsNullOrEmpty(_queueUrl))
            {
                _logger.LogWarning("❌ [OCR_WORKER] AWS_SQS_OCR_URL not configured! OcrWorkerService is DISABLED.");
                return;
            }

            _logger.LogInformation("✅ [OCR_WORKER] Configured. Queue URL: {QueueUrl}", _queueUrl);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PollAndProcessAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("OCR Worker polling canceled due to application shutdown.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in OCR Worker polling loop. Retrying in 5s...");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error in OcrWorkerService. Service will stop.");
            throw;
        }
        finally
        {
            _logger.LogInformation("OcrWorkerService has stopped.");
        }
    }

    private async Task PollAndProcessAsync(CancellationToken ct)
    {
        _logger.LogInformation(">>> [OCR_WORKER] Checking for messages in SQS URL: {QueueUrl}", _queueUrl);
        // 300s (5 min) timeout per job — worst case ~1m40s (Gemini fallback), 3x safety margin
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(300));

        using var scope = _scopeFactory.CreateScope();
        var sqsService = scope.ServiceProvider.GetRequiredService<ISqsService>();

        var messages = await sqsService.ReceiveMessagesAsync(_queueUrl!, MaxNumberOfMessages, WaitTimeSeconds, cts.Token);

        if (messages.Count == 0)
        {
            _logger.LogInformation(">>> [OCR_WORKER] No OCR jobs in queue (long poll timeout).");
            return;
        }

        _logger.LogInformation("Received {Count} OCR job(s) from SQS. Processing sequentially...", messages.Count);

        foreach (var message in messages)
        {
            _logger.LogInformation("Starting sequential processing for MessageId={MessageId}", message.MessageId);

            await _concurrencySemaphore.WaitAsync(ct);
            try
            {
                await ProcessSingleJobAsync(message.Body, cts.Token);

                // Delete message after successful processing
                await sqsService.DeleteMessageAsync(_queueUrl!, message.ReceiptHandle, ct);
                _logger.LogInformation("OCR job completed and SQS message deleted. MessageId={MessageId}", message.MessageId);
            }
            catch (JsonException ex)
            {
                // Permanently bad message — delete to avoid infinite retry
                _logger.LogError(ex, "Invalid OCR job message format. Deleting poison message {MessageId}.", message.MessageId);
                await sqsService.DeleteMessageAsync(_queueUrl!, message.ReceiptHandle, ct);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                _logger.LogWarning("OCR job {MessageId} timed out after 300s. Message will be retried.", message.MessageId);
                // Don't delete — let SQS retry after visibility timeout
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing OCR job {MessageId}. Message will be retried after visibility timeout.", message.MessageId);
                // Don't delete — let SQS retry after visibility timeout
            }
            finally
            {
                _concurrencySemaphore.Release();
            }
        }
    }

    private async Task ProcessSingleJobAsync(string messageBody, CancellationToken ct)
    {
        // ── 1. Deserialize message ──
        var job = JsonSerializer.Deserialize<OcrJobMessage>(messageBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new JsonException("Failed to deserialize OcrJobMessage");

        var overallStopwatch = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogInformation(
            "═══════════════════════════════════════════════════════════════");
        _logger.LogInformation(
            "🧾 [OCR_WORKER] START Processing InvoiceId={InvoiceId}, S3Key={S3Key}",
            job.InvoiceId, job.S3Key);
        _logger.LogInformation(
            "   └─ CompanyId: {CompanyId}, UserId: {UserId}", job.CompanyId, job.UserId);

        using var scope = _scopeFactory.CreateScope();
        var s3Service = scope.ServiceProvider.GetRequiredService<IAwsS3Service>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var invoiceProcessor = scope.ServiceProvider.GetRequiredService<IInvoiceProcessorService>();
        var storageService = scope.ServiceProvider.GetRequiredService<StorageService>();
        var sqsPublisher = scope.ServiceProvider.GetRequiredService<ISqsMessagePublisher>();
        var configProvider = scope.ServiceProvider.GetRequiredService<ISystemConfigProvider>();

        try
        {
            _logger.LogInformation("[OCR_WORKER STEP 0/7] 🛠️ Scope initialized for InvoiceId={InvoiceId}", job.InvoiceId);

            // ══════════════════════════════════════════════════
            // STEP 1/7: Download image from S3
            // ══════════════════════════════════════════════════
            _logger.LogInformation("[OCR_WORKER STEP 1/7] 📥 Downloading image from S3...");
            byte[] imageBytes;
            try
            {
                imageBytes = await s3Service.DownloadFileAsync(job.S3Key);
                _logger.LogInformation("[OCR_WORKER STEP 1/7] ✅ Downloaded {Size} bytes.", imageBytes.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OCR_WORKER STEP 1/7] ❌ Failed to download S3 file {S3Key}.", job.S3Key);
                await UpdateInvoiceStatus(unitOfWork, job.InvoiceId, "Failed", "Red",
                    $"Không thể tải file từ S3: {ex.Message}");
                throw; // Re-throw so SQS retries
            }

            // ══════════════════════════════════════════════════
            // STEP 2/7: Call Python OCR API
            // ══════════════════════════════════════════════════
            _logger.LogInformation("[OCR_WORKER STEP 2/7] 🧠 Calling OCR API...");
            OcrApiResponse? ocrResponse;
            try
            {
                ocrResponse = await CallOcrApiAsync(imageBytes, job.FileName, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OCR_WORKER STEP 2/7] ❌ OCR API call failed.");
                await UpdateInvoiceStatus(unitOfWork, job.InvoiceId, "Failed", "Red",
                    $"OCR API lỗi: {ex.Message}");
                throw;
            }

            if (ocrResponse == null || !ocrResponse.Success || ocrResponse.Data == null)
            {
                var errorMsg = ocrResponse?.Error ?? "Unknown OCR error";
                _logger.LogWarning("[OCR_WORKER STEP 2/7] ❌ OCR returned failure: {Error}", errorMsg);
                
                // User requirement: Hard-delete completely so it doesn't show as N/A in InvoiceList taking up storage.
                var draftInvoice = await unitOfWork.Invoices.GetByIdAsync(job.InvoiceId);
                if (draftInvoice != null)
                {
                    _logger.LogInformation("Hard-deleting draft invoice {InvoiceId} completely due to OCR failure: {Msg}", job.InvoiceId, errorMsg);

                    if (!string.IsNullOrEmpty(job.S3Key))
                    {
                        await storageService.DeleteFileAsync(job.S3Key);
                    }
                    if (draftInvoice.OriginalFileId.HasValue)
                    {
                        var originalFile = await unitOfWork.FileStorages.GetByIdAsync(draftInvoice.OriginalFileId.Value);
                        if (originalFile != null)
                            unitOfWork.FileStorages.Remove(originalFile);
                    }
                    
                    unitOfWork.Invoices.Remove(draftInvoice);
                    await unitOfWork.CompleteAsync();
                }
                return; // Permanent failure → message will be deleted
            }

            _logger.LogInformation("[OCR_WORKER STEP 2/7] ✅ OCR succeeded in {LatencyMs}ms.", ocrResponse.LatencyMs);
            var ocrResult = ocrResponse.Data;

            // ══════════════════════════════════════════════════
            // STEP 3/7: Validate Business Logic (duplicate, owner, etc.)
            // ══════════════════════════════════════════════════
            _logger.LogInformation("[OCR_WORKER STEP 3/7] 🔍 Validating OCR business logic...");
            var swLogic = System.Diagnostics.Stopwatch.StartNew();
            var logicResult = await invoiceProcessor.ValidateOcrBusinessLogicAsync(ocrResult, job.CompanyId, ct);
            swLogic.Stop();

            _logger.LogInformation("[OCR_WORKER STEP 3/7] ✅ Business logic validation completed ({DurationMs}ms)", swLogic.ElapsedMilliseconds);
            _logger.LogInformation("   └─ IsValid: {IsValid}, Errors: {ErrorCount}, Warnings: {WarningCount}",
                logicResult.IsValid,
                logicResult.ErrorDetails?.Count ?? 0,
                logicResult.WarningDetails?.Count ?? 0);

            // Collect all errors/warnings
            var finalErrors = new List<ValidationErrorDetail>();
            var finalWarnings = new List<ValidationErrorDetail>();
            finalErrors.AddRange(logicResult.ErrorDetails ?? new List<ValidationErrorDetail>());
            finalWarnings.AddRange(logicResult.WarningDetails ?? new List<ValidationErrorDetail>());

            // ══════════════════════════════════════════════════
            // STEP 3.5/7: MERGE — OCR attaches to existing XML record
            // (Must run BEFORE fatal error check to prevent merge targets from being treated as duplicates)
            // ══════════════════════════════════════════════════
            var isMergeMode = logicResult.MergeMode == DTOs.Invoice.DossierMergeMode.OcrAttachesToXml
                              && logicResult.MergeTargetInvoiceId.HasValue;

            if (isMergeMode)
            {
                _logger.LogInformation("[OCR_WORKER STEP 3.5/7] 🔗 MERGE MODE: Attaching OCR visual to existing XML record");
                _logger.LogInformation("   └─ MergeTargetInvoiceId: {TargetInvoiceId}", logicResult.MergeTargetInvoiceId);

                var targetInvoice = await unitOfWork.Invoices.GetByIdAsync(logicResult.MergeTargetInvoiceId.Value);
                if (targetInvoice == null)
                {
                    _logger.LogError("   ❌ Target XML invoice not found in database. Falling through to normal flow.");
                }
                else
                {
                    // Create FileStorage for the visual file
                    Guid? mergeVisualFileId = null;
                    if (!string.IsNullOrEmpty(job.S3Key))
                    {
                        var existingFile = await unitOfWork.FileStorages.FindByS3KeyAsync(job.S3Key);
                        if (existingFile != null)
                        {
                            mergeVisualFileId = existingFile.FileId;
                        }
                        else
                        {
                            var bucketName = Environment.GetEnvironmentVariable("AWS_BUCKET_NAME")
                                             ?? _configuration["AWS:BucketName"]
                                             ?? "smartinvoice-storage-team-dat";

                            var ext = Path.GetExtension(job.FileName).ToLowerInvariant();
                            var mimeType = ext switch
                            {
                                ".png" => "image/png",
                                ".jpg" or ".jpeg" => "image/jpeg",
                                ".pdf" => "application/pdf",
                                ".tiff" or ".tif" => "image/tiff",
                                _ => "application/octet-stream"
                            };

                            var fileStorage = new FileStorage
                            {
                                FileId = Guid.NewGuid(),
                                CompanyId = job.CompanyId,
                                UploadedBy = job.UserId,
                                OriginalFileName = Path.GetFileName(job.FileName),
                                FileExtension = ext,
                                FileSize = imageBytes.Length,
                                MimeType = mimeType,
                                S3BucketName = bucketName,
                                S3Key = job.S3Key,
                                IsProcessed = true,
                                ProcessedAt = DateTime.UtcNow
                            };

                            mergeVisualFileId = fileStorage.FileId;
                            await unitOfWork.FileStorages.AddAsync(fileStorage);

                            var quotaService = scope.ServiceProvider.GetRequiredService<IQuotaService>();
                            await quotaService.ConsumeStorageQuotaAsync(job.CompanyId, imageBytes.Length);
                            _logger.LogInformation("   └─ ✅ FileStorage created for merge: {FileId}. Storage quota consumed: {Size} bytes.", mergeVisualFileId, imageBytes.Length);
                        }
                    }

                    // Attach visual file to existing XML invoice
                    targetInvoice.VisualFileId = mergeVisualFileId;
                    targetInvoice.UpdatedAt = DateTime.UtcNow;

                    // Audit Log
                    var mergeUser = await unitOfWork.Users.GetByIdAsync(job.UserId);
                    await unitOfWork.InvoiceAuditLogs.AddAsync(new InvoiceAuditLog
                    {
                        AuditId = Guid.NewGuid(),
                        InvoiceId = targetInvoice.InvoiceId,
                        UserId = job.UserId,
                        UserEmail = mergeUser?.Email,
                        UserRole = mergeUser?.Role,
                        Action = "ATTACH_VISUAL_FILE",
                        Changes = new List<AuditChange>
                        {
                            new() { Field = "VisualFileId", OldValue = null, NewValue = mergeVisualFileId?.ToString(), ChangeType = "UPDATE" }
                        },
                        Comment = "Đã đính kèm bản thể hiện PDF/Ảnh (từ OCR Worker). Dữ liệu không thay đổi (giữ nguyên bản gốc XML)."
                    });

                    // HARD DELETE the draft PDF invoice — no longer needed since VisualFileId
                    // has been transferred to the XML target invoice. The FileStorage record
                    // is intentionally kept alive (not removed) because targetInvoice.VisualFileId
                    // now references it. Only the Invoice row itself is deleted to save DB space.
                    var draftInvoice = await unitOfWork.Invoices.GetByIdAsync(job.InvoiceId);
                    if (draftInvoice != null && draftInvoice.InvoiceId != targetInvoice.InvoiceId)
                    {
                        unitOfWork.Invoices.Remove(draftInvoice);
                        _logger.LogInformation("   🗑️ Hard-deleted draft PDF invoice {DraftId} (merged into XML invoice {TargetId}). FileStorage kept.",
                            job.InvoiceId, targetInvoice.InvoiceId);
                    }

                    await unitOfWork.CompleteAsync();

                    overallStopwatch.Stop();
                    _logger.LogInformation("[OCR_WORKER] ✅ MERGE COMPLETED. Visual attached to XML invoice {TargetId}. Duration: {Ms}ms",
                        targetInvoice.InvoiceId, overallStopwatch.ElapsedMilliseconds);
                    return;
                }
            }

            // Check for fatal errors (duplicate / not owner)
            // Dừng và xóa Draft Invoice nếu lỗi nghiêm trọng (giống luồng XML) để tránh lưu dữ liệu rác
            // SKIP if merge mode was intended but target not found (fallback to normal flow)
            var fatalErrorCodes = new HashSet<string> { ErrorCodes.LogicDuplicate, ErrorCodes.LogicDuplicateRejected, ErrorCodes.LogicOwner };
            var hasFatalError = finalErrors.Any(e =>
                !string.IsNullOrEmpty(e.ErrorCode) && fatalErrorCodes.Contains(e.ErrorCode));

            if (hasFatalError)
            {
                var fatalErr = finalErrors.First(e => !string.IsNullOrEmpty(e.ErrorCode) && fatalErrorCodes.Contains(e.ErrorCode!));
                _logger.LogWarning("[OCR_WORKER STEP 3/7] ⚠️ FATAL ERROR detected — soft-deleting draft invoice with error note.");
                _logger.LogWarning("   └─ {ErrorCode}: {ErrorMessage}", fatalErr.ErrorCode, fatalErr.ErrorMessage);

                var draftInvoice = await unitOfWork.Invoices.GetByIdAsync(job.InvoiceId);
                if (draftInvoice != null)
                {
                    _logger.LogInformation("Hard-deleting draft invoice {InvoiceId} completely due to fatal error: {Msg}", job.InvoiceId, fatalErr.ErrorMessage);
                    // Delete the S3 file to save storage
                    if (!string.IsNullOrEmpty(job.S3Key))
                    {
                        await storageService.DeleteFileAsync(job.S3Key);
                    }
                    if (draftInvoice.OriginalFileId.HasValue)
                    {
                        var originalFile = await unitOfWork.FileStorages.GetByIdAsync(draftInvoice.OriginalFileId.Value);
                        if (originalFile != null)
                            unitOfWork.FileStorages.Remove(originalFile);
                    }
                    unitOfWork.Invoices.Remove(draftInvoice);
                    await unitOfWork.CompleteAsync();
                }
                return;
            }

            // ══════════════════════════════════════════════════
            // STEP 4/7: Extract OCR data → InvoiceExtractedData
            // ══════════════════════════════════════════════════
            _logger.LogInformation("[OCR_WORKER STEP 4/7] 🧠 Extracting invoice data from OCR result...");
            var extractedData = invoiceProcessor.ExtractOcrData(ocrResult);

            _logger.LogInformation("[OCR_WORKER STEP 4/7] ✅ Extracted: Seller={Seller}, Buyer={Buyer}, Amount={Amount}",
                extractedData?.SellerName ?? "N/A",
                extractedData?.BuyerName ?? "N/A",
                extractedData?.TotalAmount ?? 0);

            // ══════════════════════════════════════════════════
            // STEP 5/7: Create FileStorage record for the visual file
            // ══════════════════════════════════════════════════
            _logger.LogInformation("[OCR_WORKER STEP 5/7] 💾 Creating FileStorage record...");
            Guid? visualFileId = null;
            if (!string.IsNullOrEmpty(job.S3Key))
            {
                // Check if FileStorage already exists for this S3Key
                var existingFile = await unitOfWork.FileStorages.FindByS3KeyAsync(job.S3Key);
                if (existingFile != null)
                {
                    visualFileId = existingFile.FileId;
                    _logger.LogInformation("   └─ ℹ️  FileStorage already exists: {FileId}", visualFileId);
                }
                else
                {
                    var bucketName = Environment.GetEnvironmentVariable("AWS_BUCKET_NAME")
                                     ?? _configuration["AWS:BucketName"]
                                     ?? "smartinvoice-storage-team-dat";

                    var ext = Path.GetExtension(job.FileName).ToLowerInvariant();
                    var mimeType = ext switch
                    {
                        ".png" => "image/png",
                        ".jpg" or ".jpeg" => "image/jpeg",
                        ".pdf" => "application/pdf",
                        ".tiff" or ".tif" => "image/tiff",
                        _ => "application/octet-stream"
                    };

                    var fileStorage = new FileStorage
                    {
                        FileId = Guid.NewGuid(),
                        CompanyId = job.CompanyId,
                        UploadedBy = job.UserId,
                        OriginalFileName = Path.GetFileName(job.FileName),
                        FileExtension = ext,
                        FileSize = imageBytes.Length,
                        MimeType = mimeType,
                        S3BucketName = bucketName,
                        S3Key = job.S3Key,
                        IsProcessed = true,
                        ProcessedAt = DateTime.UtcNow
                    };

                    visualFileId = fileStorage.FileId;
                    await unitOfWork.FileStorages.AddAsync(fileStorage);

                    var quotaService = scope.ServiceProvider.GetRequiredService<IQuotaService>();
                    await quotaService.ConsumeStorageQuotaAsync(job.CompanyId, imageBytes.Length);

                    _logger.LogInformation("   └─ ✅ FileStorage created: {FileId}. Storage quota consumed: {Size} bytes.", visualFileId, imageBytes.Length);
                }
            }

            // ══════════════════════════════════════════════════
            // STEP 6/7: Update Invoice in DB
            // ══════════════════════════════════════════════════
            _logger.LogInformation("[OCR_WORKER STEP 6/7] 📝 Updating invoice record...");

            var invoice = await unitOfWork.Invoices.GetByIdAsync(job.InvoiceId);
            if (invoice == null)
            {
                _logger.LogWarning("[OCR_WORKER STEP 6/7] ⚠️ Invoice {InvoiceId} not found in DB. Skipping.", job.InvoiceId);
                return;
            }

            var isCompletelyValid = !finalErrors.Any();

            // Add WARN_MISSING_XML_EVIDENCE for OCR-only uploads (only if no fatal errors)
            if (isCompletelyValid)
            {
                finalWarnings.Add(new ValidationErrorDetail
                {
                    ErrorCode = "WARN_MISSING_XML_EVIDENCE",
                    ErrorMessage = "Hóa đơn được trích xuất từ ảnh/PDF bằng AI. Để đảm bảo 100% tính pháp lý khi khai thuế, bạn cần bổ sung file XML gốc.",
                    Suggestion = "Tải lên file XML gốc của hóa đơn này để hệ thống xác thực chữ ký số và cập nhật dữ liệu chính xác."
                });
            }

            // ── Update invoice fields ──
            // Use hasFatalError for strict Rejection; isCompletelyValid for normal errors
            invoice.Status = hasFatalError ? "Rejected" : "Draft";
            invoice.RiskLevel = hasFatalError ? "Red" : "Yellow"; // Always Yellow if no XML, unless fatal
            invoice.ProcessingMethod = "API";
            invoice.ExtractedData = extractedData;
            invoice.VisualFileId = visualFileId;
            invoice.Notes = hasFatalError
                ? finalErrors.FirstOrDefault(e => !string.IsNullOrEmpty(e.ErrorCode) && fatalErrorCodes.Contains(e.ErrorCode!))?.ErrorMessage ?? "Hóa đơn có lỗi nghiêm trọng."
                : (isCompletelyValid ? "Hóa đơn từ OCR, cần bổ sung file XML gốc để xác thực pháp lý." : "Hóa đơn có lỗi, cần kiểm tra lại.");

            string? Trunc(string? val, int max) => val?.Length > max ? val[..max] : val;

            // Map key fields
            invoice.InvoiceNumber = Trunc(extractedData?.InvoiceNumber, 50) ?? "UNKNOWN";
            invoice.FormNumber = Trunc(extractedData?.InvoiceTemplateCode, 20);
            invoice.SerialNumber = Trunc(extractedData?.InvoiceSymbol, 50);
            invoice.InvoiceCurrency = Trunc(extractedData?.InvoiceCurrency, 3) ?? "VND";
            invoice.ExchangeRate = extractedData?.ExchangeRate ?? 1;
            invoice.TotalAmountBeforeTax = extractedData?.TotalPreTax;
            invoice.TotalTaxAmount = extractedData?.TotalTaxAmount;
            invoice.TotalAmount = extractedData?.TotalAmount ?? 0;
            invoice.TotalAmountInWords = extractedData?.TotalAmountInWords;
            invoice.PaymentMethod = Trunc(extractedData?.PaymentTerms, 100);
            invoice.MCCQT = Trunc(extractedData?.MCCQT, 50);

            if (extractedData?.InvoiceDate != null)
                invoice.InvoiceDate = DateTime.SpecifyKind(extractedData.InvoiceDate.Value, DateTimeKind.Utc);

            if (invoice.Seller == null) invoice.Seller = new SellerInfo();
            invoice.Seller.Name = Trunc(extractedData?.SellerName, 200);
            invoice.Seller.TaxCode = Trunc(extractedData?.SellerTaxCode, 14);
            invoice.Seller.Address = extractedData?.SellerAddress;
            invoice.Seller.Phone = Trunc(extractedData?.SellerPhone, 20);
            invoice.Seller.Email = Trunc(extractedData?.SellerEmail, 100);
            invoice.Seller.BankAccount = Trunc(extractedData?.SellerBankAccount, 50);
            invoice.Seller.BankName = Trunc(extractedData?.SellerBankName, 200);

            if (invoice.Buyer == null) invoice.Buyer = new BuyerInfo();
            invoice.Buyer.Name = Trunc(extractedData?.BuyerName, 200);
            invoice.Buyer.TaxCode = Trunc(extractedData?.BuyerTaxCode, 14);
            invoice.Buyer.Address = extractedData?.BuyerAddress;
            invoice.Buyer.Phone = Trunc(extractedData?.BuyerPhone, 20);
            invoice.Buyer.Email = Trunc(extractedData?.BuyerEmail, 100);
            invoice.Buyer.ContactPerson = Trunc(extractedData?.BuyerContactPerson, 100);

            // ── Determine document type ──
            var docTypes = await unitOfWork.DocumentTypes.GetAllAsync();
            var typeStr = ocrResult.Invoice?.Type?.Value?.ToUpper();
            if (typeStr != null && typeStr.Contains("BÁN HÀNG"))
            {
                var saleType = docTypes.FirstOrDefault(d => d.TypeCode == "SALE" || d.FormTemplate == "02GTTT");
                invoice.DocumentTypeId = saleType?.DocumentTypeId ?? 2;
            }
            else
            {
                var gtgtType = docTypes.FirstOrDefault(d => d.TypeCode == "GTGT" || d.FormTemplate == "01GTKT");
                invoice.DocumentTypeId = gtgtType?.DocumentTypeId ?? 1;
            }

            // ── Business Logic CheckResult ──
            var logicErrInfo = (logicResult.ErrorDetails ?? Enumerable.Empty<ValidationErrorDetail>()).FirstOrDefault()
                            ?? (logicResult.WarningDetails ?? Enumerable.Empty<ValidationErrorDetail>()).FirstOrDefault();

            string GetLayerStatus(bool valid, List<ValidationErrorDetail>? warnings) =>
                !valid ? "Fail" : (warnings != null && warnings.Any()) ? "Warning" : "Pass";

            await unitOfWork.InvoiceCheckResults.AddAsync(new InvoiceCheckResult
            {
                CheckId = Guid.NewGuid(),
                InvoiceId = job.InvoiceId,
                Category = "BUSINESS_LOGIC",
                CheckName = "BusinessLogic",
                CheckOrder = 3,
                IsValid = logicResult.IsValid,
                Status = GetLayerStatus(logicResult.IsValid, logicResult.WarningDetails),
                ErrorCode = logicErrInfo?.ErrorCode,
                ErrorMessage = logicErrInfo?.ErrorMessage,
                Suggestion = logicErrInfo?.Suggestion,
                DurationMs = (int)swLogic.ElapsedMilliseconds
            });

            // ── AUTO_UPLOAD_VALIDATION CheckResult ──
            var checkStatus = isCompletelyValid ? (finalWarnings.Any() ? "WARNING" : "PASS") : "FAIL";
            var priorityError = finalErrors.FirstOrDefault();
            var priorityWarning = finalWarnings.FirstOrDefault();

            await unitOfWork.InvoiceCheckResults.AddAsync(new InvoiceCheckResult
            {
                CheckId = Guid.NewGuid(),
                InvoiceId = job.InvoiceId,
                Category = "AUTO_UPLOAD_VALIDATION",
                CheckName = "AUTO_UPLOAD_VALIDATION",
                CheckOrder = 4,
                IsValid = isCompletelyValid,
                Status = checkStatus,
                ErrorCode = priorityError?.ErrorCode ?? priorityWarning?.ErrorCode,
                ErrorMessage = priorityError?.ErrorMessage ?? priorityWarning?.ErrorMessage,
                Suggestion = priorityError?.Suggestion ?? priorityWarning?.Suggestion,
                DurationMs = (int)swLogic.ElapsedMilliseconds,
                ErrorDetails = JsonSerializer.Serialize(new
                {
                    ErrorDetails = finalErrors,
                    WarningDetails = finalWarnings
                })
            });

            // ── Audit log ──
            var uploadUser = await unitOfWork.Users.GetByIdAsync(job.UserId);
            await unitOfWork.InvoiceAuditLogs.AddAsync(new InvoiceAuditLog
            {
                AuditId = Guid.NewGuid(),
                InvoiceId = job.InvoiceId,
                UserId = job.UserId,
                UserEmail = uploadUser?.Email,
                UserRole = uploadUser?.Role,
                Action = "OCR_COMPLETED",
                Changes = new List<AuditChange>
            {
                new() { Field = "Status", OldValue = "Processing", NewValue = invoice.Status, ChangeType = "UPDATE" },
                new() { Field = "RiskLevel", OldValue = null, NewValue = invoice.RiskLevel, ChangeType = "UPDATE" }
            },
                Comment = hasFatalError
                    ? "OCR worker hoàn tất nhưng hóa đơn bị từ chối do lỗi nghiệp vụ."
                    : (isCompletelyValid ? "OCR worker hoàn tất trích xuất dữ liệu từ ảnh hóa đơn." : "OCR worker hoàn tất nhưng hóa đơn có cảnh báo/lỗi.")
            });

            invoice.UpdatedAt = DateTime.UtcNow;
            unitOfWork.Invoices.Update(invoice);
            await unitOfWork.CompleteAsync();

            _logger.LogInformation("[OCR_WORKER STEP 6/7] ✅ Invoice {InvoiceId} updated → Status={Status}, RiskLevel={RiskLevel}",
                job.InvoiceId, invoice.Status, invoice.RiskLevel);

            // ══════════════════════════════════════════════════
            // STEP 7/7: Publish VietQR validation to SQS
            // ══════════════════════════════════════════════════
            if (!string.IsNullOrEmpty(invoice.Seller?.TaxCode)
                && !hasFatalError
                && await configProvider.GetBoolAsync("ENABLE_VIETQR_VALIDATION", true))
            {
                _logger.LogInformation("[OCR_WORKER STEP 7/7] 📤 Publishing VietQR validation to SQS...");
                try
                {
                    var sqsMessage = new VietQrValidationMessage
                    {
                        InvoiceId = invoice.InvoiceId,
                        TaxCode = invoice.Seller.TaxCode,
                        SellerName = invoice.Seller.Name ?? "N/A"
                    };

                    await sqsPublisher.PublishVietQrValidationAsync(sqsMessage, ct);
                    _logger.LogInformation("[OCR_WORKER STEP 7/7] ✅ VietQR message published for TaxCode={TaxCode}", invoice.Seller.TaxCode);
                }
                catch (Exception ex)
                {
                    // Don't fail the whole job — VietQR is non-critical
                    _logger.LogError(ex, "[OCR_WORKER STEP 7/7] ⚠️ Failed to publish VietQR message. Invoice saved OK.");
                }
            }
            else
            {
                _logger.LogInformation("[OCR_WORKER STEP 7/7] ⏭️ Skipped VietQR (no seller tax code, invalid, or disabled).");
            }

            overallStopwatch.Stop();
            _logger.LogInformation("[OCR_WORKER] ✅ COMPLETED. InvoiceId={InvoiceId}, Total={TotalMs}ms",
                job.InvoiceId, overallStopwatch.ElapsedMilliseconds);
            _logger.LogInformation(
                "═══════════════════════════════════════════════════════════════\n");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OCR_WORKER] ❌ UNEXPECTED FATAL ERROR during processing InvoiceId={InvoiceId}", job.InvoiceId);
            // Use a separate scope for the error update to ensure it's saved even if the current unitOfWork context is failed
            await UpdateInvoiceStatusSafe(job.InvoiceId, "Failed", "Red", $"Lỗi hệ thống (Worker): {ex.Message}");
            throw; // Let SQS retry
        }
    }

    /// <summary>
    /// Safer version of UpdateInvoiceStatus that creates its own scope to ensure the update hits the DB
    /// even if the main processing scope's context is in a failed/poisoned state.
    /// </summary>
    private async Task UpdateInvoiceStatusSafe(Guid invoiceId, string status, string riskLevel, string notes)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await UpdateInvoiceStatus(unitOfWork, invoiceId, status, riskLevel, notes);
            _logger.LogInformation("[OCR_WORKER] ✅ Emergency status update saved for {InvoiceId} -> {Status}", invoiceId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OCR_WORKER] ❌ FAILED emergency status update for {InvoiceId}", invoiceId);
        }
    }

    /// <summary>
    /// Calls the Python OCR API with multipart/form-data containing the image file.
    /// </summary>
    private async Task<OcrApiResponse?> CallOcrApiAsync(byte[] imageBytes, string fileName, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("OcrWorker");

        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(imageBytes);

        // Determine content type
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var mimeType = ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".pdf" => "application/pdf",
            ".tiff" or ".tif" => "image/tiff",
            _ => "application/octet-stream"
        };
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);
        content.Add(fileContent, "file", fileName);

        var response = await client.PostAsync("/api/v1/extract", content, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("OCR API returned {StatusCode}: {Body}",
                response.StatusCode, responseBody[..Math.Min(500, responseBody.Length)]);
            return new OcrApiResponse { Success = false, Error = $"HTTP {response.StatusCode}" };
        }

        try
        {
            return JsonSerializer.Deserialize<OcrApiResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize OCR API response. Format mismatch.");
            return new OcrApiResponse { Success = false, Error = "Lỗi đọc dữ liệu từ AI (Sai định dạng JSON)." };
        }
    }

    /// <summary>
    /// Helper to update invoice status when OCR fails.
    /// </summary>
    private static async Task UpdateInvoiceStatus(
        IUnitOfWork unitOfWork, Guid invoiceId, string status, string riskLevel, string notes)
    {
        var invoice = await unitOfWork.Invoices.GetByIdAsync(invoiceId);
        if (invoice != null)
        {
            invoice.Status = status;
            invoice.RiskLevel = riskLevel;
            invoice.Notes = notes;
            invoice.UpdatedAt = DateTime.UtcNow;
            await unitOfWork.CompleteAsync();
        }
    }
}

// ── Response DTOs for the Python OCR API ──

/// <summary>
/// Top-level response from POST /api/v1/extract
/// </summary>
public class OcrApiResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("success")]
    public bool Success { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("request_id")]
    public string? RequestId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("latency_ms")]
    public double LatencyMs { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("data")]
    public OcrInvoiceResult? Data { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("error")]
    public string? Error { get; set; }
}
