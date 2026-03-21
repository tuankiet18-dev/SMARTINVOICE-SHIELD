using Amazon.SQS.Model;

namespace SmartInvoice.API.Services.Interfaces;

/// <summary>
/// Generic SQS service for sending, receiving, and deleting messages.
/// Used by OcrWorkerService for the OCR job queue.
/// </summary>
public interface ISqsService
{
    /// <summary>
    /// Serializes a message to JSON and sends it to the specified SQS queue.
    /// </summary>
    Task<string?> SendMessageAsync<T>(T message, string queueUrl, CancellationToken ct = default);

    /// <summary>
    /// Long-polls the SQS queue and returns up to <paramref name="maxMessages"/> messages.
    /// </summary>
    Task<List<Message>> ReceiveMessagesAsync(string queueUrl, int maxMessages = 10, int waitSeconds = 20, CancellationToken ct = default);

    /// <summary>
    /// Deletes a processed message from the SQS queue.
    /// </summary>
    Task DeleteMessageAsync(string queueUrl, string receiptHandle, CancellationToken ct = default);
}
