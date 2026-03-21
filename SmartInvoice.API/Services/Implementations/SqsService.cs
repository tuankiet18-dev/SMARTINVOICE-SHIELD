using Amazon.SQS;
using Amazon.SQS.Model;
using SmartInvoice.API.Services.Interfaces;
using System.Text.Json;

namespace SmartInvoice.API.Services.Implementations;

/// <summary>
/// Generic SQS service implementation using IAmazonSQS.
/// Handles JSON serialization/deserialization for message bodies.
/// </summary>
public class SqsService : ISqsService
{
    private readonly IAmazonSQS _sqsClient;
    private readonly ILogger<SqsService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public SqsService(IAmazonSQS sqsClient, ILogger<SqsService> logger)
    {
        _sqsClient = sqsClient;
        _logger = logger;
    }

    public async Task<string?> SendMessageAsync<T>(T message, string queueUrl, CancellationToken ct = default)
    {
        try
        {
            var messageBody = JsonSerializer.Serialize(message, _jsonOptions);

            var request = new SendMessageRequest
            {
                QueueUrl = queueUrl,
                MessageBody = messageBody
            };

            var response = await _sqsClient.SendMessageAsync(request, ct);

            _logger.LogInformation(
                "SQS message sent. Queue={QueueUrl}, MessageId={MessageId}",
                queueUrl, response.MessageId);

            return response.MessageId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SQS message to {QueueUrl}", queueUrl);
            throw;
        }
    }

    public async Task<List<Message>> ReceiveMessagesAsync(
        string queueUrl, int maxMessages = 10, int waitSeconds = 20, CancellationToken ct = default)
    {
        var request = new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = maxMessages,
            WaitTimeSeconds = waitSeconds,
            MessageAttributeNames = new List<string> { "All" }
        };

        var response = await _sqsClient.ReceiveMessageAsync(request, ct);
        return response.Messages ?? new List<Message>();
    }

    public async Task DeleteMessageAsync(string queueUrl, string receiptHandle, CancellationToken ct = default)
    {
        try
        {
            await _sqsClient.DeleteMessageAsync(new DeleteMessageRequest
            {
                QueueUrl = queueUrl,
                ReceiptHandle = receiptHandle
            }, ct);

            _logger.LogDebug("Deleted SQS message from {QueueUrl}", queueUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete SQS message from {QueueUrl}", queueUrl);
            throw;
        }
    }
}
