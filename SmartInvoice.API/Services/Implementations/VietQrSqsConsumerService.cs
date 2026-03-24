using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartInvoice.API.DTOs.Invoice;
using SmartInvoice.API.DTOs.SQS;
using SmartInvoice.API.Services.Interfaces;
using SmartInvoice.API.Repositories.Interfaces;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SmartInvoice.API.Services.Implementations
{
    /// <summary>
    /// Background service that continuously polls AWS SQS for VietQR validation messages.
    /// 
    /// Architecture:
    /// - Runs as a hosted service in the main application lifecycle
    /// - Uses long polling (20 second wait) to minimize API calls and costs
    /// - Deserializes messages and processes tax code validation asynchronously
    /// - Updates invoice records in the database with validation results
    /// - Deletes processed messages from the queue
    /// 
    /// Error Handling:
    /// - Failed messages are left in the queue (will be retried after visibility timeout)
    /// - Exceptions are logged but don't stop the service
    /// - Service gracefully shuts down on application stop
    /// 
    /// DI Scope Usage:
    /// - Creates a new IServiceScope for each message (database context isolation)
    /// - Resolves fresh VietQrClientService and UnitOfWork instances
    /// - Prevents entity state tracking issues across message boundaries
    /// </summary>
    public class VietQrSqsConsumerService : BackgroundService
    {
        private readonly IAmazonSQS _sqsClient;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<VietQrSqsConsumerService> _logger;

        private string? _queueUrl;
        private const int WaitTimeSeconds = 20; // Long polling timeout (max 20 seconds)
        private const int MaxNumberOfMessages = 10; // Batch size per poll

        public VietQrSqsConsumerService(
            IAmazonSQS sqsClient,
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<VietQrSqsConsumerService> logger)
        {
            _sqsClient = sqsClient;
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Main execution loop that continuously polls SQS and processes messages.
        /// Called when the application starts.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("VietQrSqsConsumerService is starting...");

            try
            {
                _queueUrl = _configuration["AWS_SQS_URL"];
                if (string.IsNullOrEmpty(_queueUrl))
                {
                    _logger.LogWarning("⚠️ AWS_SQS_URL not configured in SSM Parameter Store — VietQrSqsConsumerService is DISABLED.");
                    return;
                }

                _logger.LogInformation("VietQR SQS Consumer configured. Queue URL: {QueueUrl}", _queueUrl);

                // Main polling loop
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await PollAndProcessMessagesAsync(stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("VietQR SQS Consumer polling canceled");
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in VietQR SQS Consumer polling loop. Will retry in 5 seconds...");
                        // Wait before retrying to avoid tight error loop
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Fatal error in VietQrSqsConsumerService. Service will stop.");
                throw;
            }
            finally
            {
                _logger.LogInformation("VietQrSqsConsumerService has stopped");
            }
        }

        /// <summary>
        /// Polls the SQS queue for messages and processes them.
        /// Uses long polling to wait up to 20 seconds for messages.
        /// </summary>
        private async Task PollAndProcessMessagesAsync(CancellationToken cancellationToken)
        {
            var receiveMessageRequest = new ReceiveMessageRequest
            {
                QueueUrl = _queueUrl,
                MaxNumberOfMessages = MaxNumberOfMessages,
                WaitTimeSeconds = WaitTimeSeconds,
                MessageAttributeNames = new List<string> { "All" }
            };

            var response = await _sqsClient.ReceiveMessageAsync(receiveMessageRequest, cancellationToken);

            if (response.Messages == null || response.Messages.Count == 0)
            {
                _logger.LogDebug("No messages received from SQS queue (long poll timeout)");
                return;
            }

            _logger.LogInformation("Received {MessageCount} messages from SQS queue", response.Messages.Count);

            // Process each message
            foreach (var message in response.Messages)
            {
                try
                {
                    await ProcessMessageAsync(message, cancellationToken);
                    
                    // Delete message from queue after successful processing
                    await DeleteMessageAsync(message, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error processing SQS message {MessageId}. Message will be retried after visibility timeout.",
                        message.MessageId);
                    // Don't delete failed messages; they'll be retried
                }
            }
        }

        /// <summary>
        /// Processes a single VietQR validation message.
        /// Deserializes the message, fetches the invoice, calls VietQR API, and updates the database.
        /// </summary>
        private async Task ProcessMessageAsync(Message message, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processing SQS message {MessageId}", message.MessageId);

            // Deserialize the message body
            if (string.IsNullOrEmpty(message.Body))
            {
                throw new InvalidOperationException("SQS message body is empty");
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            VietQrValidationMessage? validationMessage = null;
            try
            {
                validationMessage = JsonSerializer.Deserialize<VietQrValidationMessage>(message.Body, jsonOptions);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Failed to deserialize SQS message: {ex.Message}", ex);
            }

            if (validationMessage == null)
            {
                throw new InvalidOperationException("Deserialized VietQR validation message is null");
            }

            _logger.LogInformation(
                "Deserialized VietQR validation message. InvoiceId={InvoiceId}, TaxCode={TaxCode}",
                validationMessage.InvoiceId,
                validationMessage.TaxCode);

            // Create a new DI scope for this message processing
            using (var scope = _scopeFactory.CreateScope())
            {
                var vietQrService = scope.ServiceProvider.GetRequiredService<IVietQrClientService>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                // Fetch the invoice from the database
                var invoice = await unitOfWork.Invoices.GetByIdAsync(validationMessage.InvoiceId);
                if (invoice == null)
                {
                    _logger.LogWarning(
                        "Invoice not found for VietQR validation. InvoiceId={InvoiceId}",
                        validationMessage.InvoiceId);
                    return;
                }

                _logger.LogInformation(
                    "Fetched invoice from database. InvoiceId={InvoiceId}, Status={Status}",
                    invoice.InvoiceId,
                    invoice.Status);

                // Create a ValidationResultDto to collect validation results
                var validationResult = new ValidationResultDto();

                // Call VietQR service to validate the tax code
                _logger.LogInformation(
                    "Calling VietQrClientService.ValidateTaxCodeAsync for TaxCode={TaxCode}",
                    validationMessage.TaxCode);

                await vietQrService.ValidateTaxCodeAsync(
                    validationMessage.TaxCode,
                    validationMessage.SellerName,
                    validationResult,
                    cancellationToken);

                _logger.LogInformation(
                    "VietQR validation completed. Errors={ErrorCount}, Warnings={WarningCount}",
                    validationResult.ErrorDetails?.Count ?? 0,
                    validationResult.WarningDetails?.Count ?? 0);

                // Update invoice with validation results
                UpdateInvoiceWithValidationResults(invoice, validationResult);

                // Save changes to the database
                try
                {
                    await unitOfWork.CompleteAsync();
                    _logger.LogInformation(
                        "Invoice updated successfully with VietQR validation results. InvoiceId={InvoiceId}",
                        invoice.InvoiceId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error saving invoice updates to database. InvoiceId={InvoiceId}",
                        invoice.InvoiceId);
                    throw;
                }
            }
        }

        /// <summary>
        /// Updates the invoice record with validation results from VietQR.
        /// Appends warnings to the Notes field and updates RiskLevel if needed.
        /// </summary>
        private void UpdateInvoiceWithValidationResults(
            SmartInvoice.API.Entities.Invoice invoice,
            ValidationResultDto validationResult)
        {
            var warningMessages = new List<string>();

            // Collect error messages
            if (validationResult.ErrorDetails != null && validationResult.ErrorDetails.Count > 0)
            {
                foreach (var error in validationResult.ErrorDetails)
                {
                    warningMessages.Add($"[ERROR] {error.ErrorMessage}");
                }
            }

            // Collect warning messages
            if (validationResult.WarningDetails != null && validationResult.WarningDetails.Count > 0)
            {
                foreach (var warning in validationResult.WarningDetails)
                {
                    warningMessages.Add($"[WARNING] {warning.ErrorMessage}");
                }
            }

            // Update invoice Notes with validation messages
            if (warningMessages.Count > 0)
            {
                string newNotes = string.Join(Environment.NewLine, warningMessages);
                
                if (string.IsNullOrEmpty(invoice.Notes))
                {
                    invoice.Notes = newNotes;
                }
                else
                {
                    invoice.Notes += Environment.NewLine + "--- VietQR Validation Results ---" + Environment.NewLine + newNotes;
                }

                // Escalate risk level if validation errors occurred
                if (validationResult.ErrorDetails != null && validationResult.ErrorDetails.Count > 0)
                {
                    // Escalate to at least Yellow if there are errors
                    if (invoice.RiskLevel == "Green")
                    {
                        invoice.RiskLevel = "Yellow";
                    }
                }
                else if (validationResult.WarningDetails != null && validationResult.WarningDetails.Count > 0)
                {
                    // Keep as is if only warnings (no escalation)
                }
            }

            // Mark invoice as updated
            invoice.UpdatedAt = DateTime.UtcNow;

            _logger.LogDebug(
                "Invoice updated with {MessageCount} validation messages. NewRiskLevel={RiskLevel}",
                warningMessages.Count,
                invoice.RiskLevel);
        }

        /// <summary>
        /// Deletes a processed message from the SQS queue.
        /// </summary>
        private async Task DeleteMessageAsync(Message message, CancellationToken cancellationToken)
        {
            try
            {
                var deleteRequest = new DeleteMessageRequest
                {
                    QueueUrl = _queueUrl,
                    ReceiptHandle = message.ReceiptHandle
                };

                await _sqsClient.DeleteMessageAsync(deleteRequest, cancellationToken);

                _logger.LogDebug("Deleted message {MessageId} from SQS queue", message.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error deleting message {MessageId} from SQS queue",
                    message.MessageId);
                throw;
            }
        }
    }
}
