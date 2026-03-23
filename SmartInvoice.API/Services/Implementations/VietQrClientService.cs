using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartInvoice.API.DTOs.Invoice;
using SmartInvoice.API.Services.Interfaces;

namespace SmartInvoice.API.Services.Implementations
{
    /// <summary>
    /// Implementation of VietQR Tax Code validation service with double-check locking pattern
    /// to prevent thundering herd and concurrent API calls for the same Tax Code.
    /// 
    /// Concurrency Control Strategy:
    /// - Uses ConcurrentDictionary<taxCode, SemaphoreSlim> for per-Tax-Code synchronization
    /// - Double-check locking: check cache → acquire lock → check cache again → call API
    /// - Prevents N concurrent requests from calling the API N times (serializes to 1 call)
    /// 
    /// Caching Strategy:
    /// - SUCCESS: 7 days (stable business data)
    /// - RATELIMIT (429): 10 minutes (API recovery time)
    /// - ERROR (other): 5-30 minutes (transient errors may resolve)
    /// 
    /// Resilience (handled by Polly policies configured in DI):
    /// - Timeout: 5 seconds total
    /// - Retry: 3 attempts with exponential backoff for 429 and 5xx
    /// - Circuit Breaker: Break after 5 consecutive failures, stay broken 1 minute
    /// </summary>
    public class VietQrClientService : IVietQrClientService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly ILogger<VietQrClientService> _logger;

        // Per-Tax-Code semaphore for double-check locking pattern
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _taxCodeLocks =
            new ConcurrentDictionary<string, SemaphoreSlim>();

        private const int VietQrTimeoutSeconds = 5;

        public VietQrClientService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IMemoryCache cache,
            ILogger<VietQrClientService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _cache = cache;
            _logger = logger;
        }

        /// <summary>
        /// Validates a Vietnamese Tax Code with concurrency control and multi-level caching.
        /// Implements double-check locking to serialize concurrent requests for the same Tax Code.
        /// </summary>
        public async Task ValidateTaxCodeAsync(string taxCode, string? sellerName, ValidationResultDto validationResult, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(taxCode) || !IsValidTaxCode(taxCode))
                return;

            // Cache key variants for different result types
            string cacheKeySuccess = $"VietQR_TaxCode_{taxCode}_SUCCESS";
            string cacheKeyError = $"VietQR_TaxCode_{taxCode}_ERROR";
            string cacheKeyRateLimit = $"VietQR_TaxCode_{taxCode}_RATELIMIT";

            // ==== FIRST CHECK: Try cache hit (no lock needed) ====
            if (_cache.TryGetValue(cacheKeySuccess, out string? cachedData) && !string.IsNullOrEmpty(cachedData))
            {
                ProcessCachedSuccessResponse(cachedData, sellerName, validationResult);
                return;
            }

            if (_cache.TryGetValue(cacheKeyRateLimit, out bool isRateLimited) && isRateLimited)
            {
                validationResult.AddWarning($"[Hệ thống] Máy chủ tra cứu MST (VietQR) đang quá tải, tạm thời bỏ qua bước xác thực chéo doanh nghiệp.");
                return;
            }

            if (_cache.TryGetValue(cacheKeyError, out string? errorMessage) && !string.IsNullOrEmpty(errorMessage))
            {
                validationResult.AddWarning(errorMessage);
                return;
            }

            // ==== DOUBLE-CHECK LOCKING PATTERN ====
            // Get or create a semaphore for this specific Tax Code
            SemaphoreSlim semaphore = _taxCodeLocks.GetOrAdd(taxCode, _ => new SemaphoreSlim(1, 1));

            // Wait to acquire the lock (serialize concurrent requests for same Tax Code)
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                // ==== SECOND CHECK: Recheck cache after acquiring lock ====
                // Another thread may have filled the cache while we were waiting
                if (_cache.TryGetValue(cacheKeySuccess, out string? cachedData2) && !string.IsNullOrEmpty(cachedData2))
                {
                    ProcessCachedSuccessResponse(cachedData2, sellerName, validationResult);
                    return;
                }

                if (_cache.TryGetValue(cacheKeyRateLimit, out bool isRateLimited2) && isRateLimited2)
                {
                    validationResult.AddWarning($"[Hệ thống] Máy chủ tra cứu MST (VietQR) đang quá tải, tạm thời bỏ qua bước xác thực chéo doanh nghiệp.");
                    return;
                }

                if (_cache.TryGetValue(cacheKeyError, out string? errorMessage2) && !string.IsNullOrEmpty(errorMessage2))
                {
                    validationResult.AddWarning(errorMessage2);
                    return;
                }

                // ==== ACTUAL API CALL (only this thread reaches here for this Tax Code) ====
                await CallVietQrApiAsync(taxCode, sellerName, validationResult, cacheKeySuccess, cacheKeyError, cacheKeyRateLimit, cancellationToken);
            }
            finally
            {
                // Release the lock so other waiting threads can proceed
                semaphore.Release();
            }
        }

        /// <summary>
        /// Makes the actual VietQR API call (protected by Polly resilience policies).
        /// Only called after acquiring the semaphore lock.
        /// </summary>
        private async Task CallVietQrApiAsync(
            string taxCode,
            string? sellerName,
            ValidationResultDto validationResult,
            string cacheKeySuccess,
            string cacheKeyError,
            string cacheKeyRateLimit,
            CancellationToken cancellationToken)
        {
            try
            {
                var vietQrBaseUrl = Environment.GetEnvironmentVariable("VIETQR_API_URL")
                                    ?? _configuration["ExternalApis:VietQR"]
                                    ?? "https://api.vietqr.io/v2/business";

                string url = $"{vietQrBaseUrl.TrimEnd('/')}/{taxCode}";

                // Use the named HttpClient with Polly policies registered in DI
                var client = _httpClientFactory.CreateClient("VietQR");
                HttpResponseMessage response = await client.GetAsync(url, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync(cancellationToken);
                    await ProcessSuccessResponseAsync(json, taxCode, sellerName, validationResult, cacheKeySuccess, cacheKeyError, cancellationToken);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    // Cache rate limit for 10 minutes to prevent hammering the API
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(10));
                    _cache.Set(cacheKeyRateLimit, true, cacheOptions);
                    validationResult.AddWarning($"[Hệ thống] Máy chủ tra cứu MST (VietQR) đang quá tải, tạm thời bỏ qua bước xác thực chéo doanh nghiệp.");
                }
                else
                {
                    string warningMsg = $"[API ERROR] Lỗi kết nối VietQR API: {response.StatusCode}";
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
                    _cache.Set(cacheKeyError, warningMsg, cacheOptions);
                    validationResult.AddWarning(warningMsg);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("VietQR API request timed out or was canceled for TaxCode {TaxCode}", taxCode);
                string warningMsg = $"[API WARNING] Quá thời gian kết nối hoặc bị hủy khi gọi VietQR API.";
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
                _cache.Set(cacheKeyError, warningMsg, cacheOptions);
                validationResult.AddWarning(warningMsg);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request exception khi gọi VietQR API");
                string warningMsg = $"[API ERROR] Lỗi kết nối mạng: {ex.Message}";
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
                _cache.Set(cacheKeyError, warningMsg, cacheOptions);
                validationResult.AddWarning(warningMsg);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không mong muốn khi gọi VietQR API cho Tax Code {TaxCode}", taxCode);
                string warningMsg = $"[API ERROR] Lỗi xử lý: {ex.Message}";
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
                _cache.Set(cacheKeyError, warningMsg, cacheOptions);
                validationResult.AddWarning(warningMsg);
            }
        }

        /// <summary>
        /// Processes a successful VietQR API response, validates seller name, and caches the result.
        /// </summary>
        private Task ProcessSuccessResponseAsync(
            string json,
            string taxCode,
            string? sellerName,
            ValidationResultDto validationResult,
            string cacheKeySuccess,
            string cacheKeyError,
            CancellationToken cancellationToken)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    JsonElement root = doc.RootElement;
                    var code = root.TryGetProperty("code", out var codeElement) ? codeElement.GetString() : null;

                    if (code == "00")
                    {
                        var data = root.GetProperty("data");
                        var status = data.TryGetProperty("status", out var statusElement) ? statusElement.GetString() : null;

                        if (status != null && status.Contains("đang hoạt động", StringComparison.OrdinalIgnoreCase))
                        {
                            // Cache successful response for 7 days
                            var cacheOptions = new MemoryCacheEntryOptions()
                                .SetAbsoluteExpiration(TimeSpan.FromDays(7));
                            _cache.Set(cacheKeySuccess, json, cacheOptions);

                            // Validate seller name if provided
                            if (!string.IsNullOrWhiteSpace(sellerName))
                            {
                                ValidateSellerNameSimilarity(data, sellerName, validationResult);
                            }
                        }
                        else
                        {
                            string warningMsg = $"[API WARNING] Mã số thuế {taxCode} không ở trạng thái đang hoạt động (Trạng thái: {status})";
                            var cacheOptions = new MemoryCacheEntryOptions()
                                .SetAbsoluteExpiration(TimeSpan.FromHours(1));
                            _cache.Set(cacheKeyError, warningMsg, cacheOptions);
                            validationResult.AddWarning(warningMsg);
                        }
                    }
                    else
                    {
                        string warningMsg = $"[API WARNING] Không tìm thấy thông tin doanh nghiệp trên VietQR! (Code: {code})";
                        var cacheOptions = new MemoryCacheEntryOptions()
                            .SetAbsoluteExpiration(TimeSpan.FromMinutes(30));
                        _cache.Set(cacheKeyError, warningMsg, cacheOptions);
                        validationResult.AddWarning(warningMsg);
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Lỗi parse JSON response từ VietQR API");
                string warningMsg = $"[API ERROR] Lỗi parse dữ liệu API: {ex.Message}";
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
                _cache.Set(cacheKeyError, warningMsg, cacheOptions);
                validationResult.AddWarning(warningMsg);
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Validates that the seller name matches the registered name from VietQR.
        /// Uses Levenshtein distance to detect name variations.
        /// </summary>
        private void ValidateSellerNameSimilarity(JsonElement data, string expectedSellerName, ValidationResultDto validationResult)
        {
            var registeredName = data.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            var shortName = data.TryGetProperty("shortTerm", out var shortNameElement) ? shortNameElement.GetString() : null;

            if (!string.IsNullOrWhiteSpace(registeredName))
            {
                double similarityVal = CalculateSimilarity(expectedSellerName.ToLower(), registeredName.ToLower());
                double shortSimVal = !string.IsNullOrWhiteSpace(shortName)
                    ? CalculateSimilarity(expectedSellerName.ToLower(), shortName.ToLower())
                    : 0;

                if (similarityVal < 0.6 && shortSimVal < 0.6)
                {
                    validationResult.AddWarning($"[RỦI RO MST] Tên người bán trên hóa đơn (\"{expectedSellerName}\") khác biệt lớn so với tên đăng ký tại CQT (\"{registeredName}\").");
                }
            }
        }

        /// <summary>
        /// Processes a cached successful response.
        /// Validates seller name again from cached data without making an API call.
        /// </summary>
        private void ProcessCachedSuccessResponse(string cachedJson, string? sellerName, ValidationResultDto validationResult)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(cachedJson))
                {
                    JsonElement root = doc.RootElement;
                    if (root.TryGetProperty("data", out var data) && !string.IsNullOrWhiteSpace(sellerName))
                    {
                        ValidateSellerNameSimilarity(data, sellerName, validationResult);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xử lý cached VietQR response");
            }
        }

        // ================== TAX CODE VALIDATION HELPERS ==================

        /// <summary>
        /// Validates the format of a Vietnamese Tax Code (10 or 13 digits, or 12 digits special format).
        /// For 10-digit codes, validates the check digit using modulo-11 algorithm.
        /// </summary>
        private bool IsValidTaxCode(string taxCode)
        {
            if (string.IsNullOrEmpty(taxCode))
                return false;

            // Format: 10 digits, optionally followed by -XXX
            Match match10or13 = Regex.Match(taxCode, @"^(\d{10})(-\d{3})?$");
            if (match10or13.Success)
            {
                string mst10 = match10or13.Groups[1].Value;
                return ValidateMstChecksum(mst10);
            }

            // Format: 12 digits (special format)
            if (Regex.IsMatch(taxCode, @"^\d{12}$"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Validates the check digit of a 10-digit MST using modulo-11 algorithm.
        /// Vietnamese MST uses weighted sum with specific weights, then validates against check digit.
        /// </summary>
        private bool ValidateMstChecksum(string mst10)
        {
            if (mst10.Length != 10)
                return false;

            int[] weights = { 31, 29, 23, 19, 17, 13, 7, 5, 3 };
            long sum = 0;

            for (int i = 0; i < 9; i++)
            {
                if (!char.IsDigit(mst10[i]))
                    return false;
                sum += (mst10[i] - '0') * weights[i];
            }

            long remainder = sum % 11;
            long checkDigit = 10 - remainder;

            if (checkDigit == 10)
                return false;

            int actualDigit = mst10[9] - '0';
            return checkDigit == actualDigit;
        }

        // ================== STRING SIMILARITY HELPERS ==================

        /// <summary>
        /// Calculates similarity between two strings using Levenshtein distance.
        /// Returns 0.0 (completely different) to 1.0 (identical).
        /// </summary>
        private double CalculateSimilarity(string source, string target)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
                return 0;
            if (source == target)
                return 1.0;

            int stepsToSame = ComputeLevenshteinDistance(source, target);
            return 1.0 - ((double)stepsToSame / (double)Math.Max(source.Length, target.Length));
        }

        /// <summary>
        /// Computes the Levenshtein distance (edit distance) between two strings.
        /// Uses dynamic programming to find the minimum number of edits needed.
        /// </summary>
        private int ComputeLevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            if (n == 0) return m;
            if (m == 0) return n;

            for (int i = 0; i <= n; d[i, 0] = i++) { }
            for (int j = 0; j <= m; d[0, j] = j++) { }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[n, m];
        }
    }
}
