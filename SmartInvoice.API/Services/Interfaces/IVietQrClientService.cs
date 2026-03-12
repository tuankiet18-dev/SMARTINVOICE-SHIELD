using System.Threading;
using System.Threading.Tasks;
using SmartInvoice.API.DTOs.Invoice;

namespace SmartInvoice.API.Services.Interfaces
{
    /// <summary>
    /// Service for validating Vietnamese Tax Codes (MST) via the VietQR API.
    /// Implements caching and concurrency control to prevent thundering herd issues.
    /// </summary>
    public interface IVietQrClientService
    {
        /// <summary>
        /// Validates a Vietnamese Tax Code asynchronously with caching and concurrency control.
        /// 
        /// This method implements double-check locking to ensure only one thread calls the VietQR API
        /// for the same Tax Code, while other concurrent requests wait and fetch the cached result.
        /// </summary>
        /// <param name="taxCode">The Vietnamese Tax Code (MST) to validate</param>
        /// <param name="sellerName">Optional seller name for cross-verification</param>
        /// <param name="validationResult">The DTO to populate with warnings or errors</param>
        /// <param name="cancellationToken">Cancellation token for the async operation</param>
        /// <returns>A completed task. Results are populated in validationResult parameter.</returns>
        Task ValidateTaxCodeAsync(string taxCode, string? sellerName, ValidationResultDto validationResult, CancellationToken cancellationToken = default);
    }
}
