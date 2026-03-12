using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartInvoice.API.DTOs.SQS;
using SmartInvoice.API.Services.Interfaces;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SmartInvoice.API.Services.Implementations
{
    /// <summary>
    /// Implementation of SQS message publisher for VietQR validation requests.
    /// Serializes validation messages to JSON and sends them to the configured SQS queue.
    /// 
    /// Queue Configuration:
    /// The queue URL is read from AWS Systems Manager Parameter Store:
    /// Parameter: /SmartInvoice/dev/AWS_SQS_URL
    /// Example: https://sqs.ap-southeast-1.amazonaws.com/212208750923/smartinvoice-vietqr-queue
    /// </summary>
    public class SqsMessagePublisher : ISqsMessagePublisher
    {
        private readonly IAmazonSQS _sqsClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SqsMessagePublisher> _logger;

        public SqsMessagePublisher(
            IAmazonSQS sqsClient,
            IConfiguration configuration,
            ILogger<SqsMessagePublisher> logger)
        {
            _sqsClient = sqsClient;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Publishes a VietQR validation request message to SQS.
        /// The message is serialized to JSON with snake_case property names for consistency.
        /// </summary>
        public async Task<string?> PublishVietQrValidationAsync(
            VietQrValidationMessage message,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Get queue URL from AWS Systems Manager Parameter Store (/SmartInvoice/dev/AWS_SQS_URL)
                var queueUrl = _configuration["AWS_SQS_URL"]
                               ?? throw new InvalidOperationException("AWS_SQS_URL parameter not configured in AWS Systems Manager Parameter Store");

                // Serialize message to JSON
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                };
                string messageBody = JsonSerializer.Serialize(message, jsonOptions);

                // Create SQS SendMessage request
                var sendMessageRequest = new SendMessageRequest
                {
                    QueueUrl = queueUrl,
                    MessageBody = messageBody,
                    // Optional: Set message attributes for filtering/routing
                    MessageAttributes = new Dictionary<string, MessageAttributeValue>
                    {
                        {
                            "InvoiceId",
                            new MessageAttributeValue
                            {
                                StringValue = message.InvoiceId.ToString(),
                                DataType = "String"
                            }
                        },
                        {
                            "TaxCode",
                            new MessageAttributeValue
                            {
                                StringValue = message.TaxCode,
                                DataType = "String"
                            }
                        }
                    }
                };

                // Add correlation ID as message deduplication ID if provided (for FIFO queues)
                if (!string.IsNullOrEmpty(message.CorrelationId))
                {
                    sendMessageRequest.MessageDeduplicationId = message.CorrelationId;
                    sendMessageRequest.MessageGroupId = "vietqr-validation"; // FIFO queue group
                }

                // Send message to SQS
                var response = await _sqsClient.SendMessageAsync(sendMessageRequest, cancellationToken);

                _logger.LogInformation(
                    "Published VietQR validation message to SQS. InvoiceId={InvoiceId}, TaxCode={TaxCode}, MessageId={MessageId}",
                    message.InvoiceId,
                    message.TaxCode,
                    response.MessageId);

                return response.MessageId;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("PublishVietQrValidationAsync was canceled for InvoiceId={InvoiceId}", message.InvoiceId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error publishing VietQR validation message to SQS for InvoiceId={InvoiceId}, TaxCode={TaxCode}",
                    message.InvoiceId,
                    message.TaxCode);
                throw;
            }
        }
    }
}
