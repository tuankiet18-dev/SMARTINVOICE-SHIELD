using SmartInvoice.API.DTOs.SQS;
using System.Threading;
using System.Threading.Tasks;

namespace SmartInvoice.API.Services.Interfaces
{
    /// <summary>
    /// Service for publishing messages to AWS SQS queues.
    /// Responsible for serializing and sending messages to SQS for asynchronous processing.
    /// </summary>
    public interface ISqsMessagePublisher
    {
        /// <summary>
        /// Publishes a VietQR tax code validation message to SQS.
        /// The message will be picked up by the VietQrSqsConsumerService background worker.
        /// </summary>
        /// <param name="message">The validation message containing invoice ID, tax code, and seller name</param>
        /// <param name="cancellationToken">Cancellation token for the async operation</param>
        /// <returns>The SQS message ID if successful; null or exception if failed</returns>
        Task<string?> PublishVietQrValidationAsync(VietQrValidationMessage message, CancellationToken cancellationToken = default);
    }
}
