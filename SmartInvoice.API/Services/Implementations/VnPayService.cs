using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SmartInvoice.API.Data;
using SmartInvoice.API.DTOs.Payment;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Services.Interfaces;
using System.Globalization;

namespace SmartInvoice.API.Services.Implementations;

public class VnPayService : IVnPayService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    // Hardcoded Add-on definitions
    private static readonly Dictionary<
        string,
        (string Name, string Description, int InvoiceCount, decimal Price)
    > Addons = new()
    {
        ["ADDON_50_INVOICES"] = (
            "Gói thêm 50 Hóa đơn",
            "+50 Hóa đơn — Sử dụng không thời hạn",
            50,
            50000m
        ),
    };

    public VnPayService(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<List<SubscriptionPackageDto>> GetPackagesAsync()
    {
        return await _context
            .SubscriptionPackages.Where(p => p.IsActive)
            .OrderBy(p => p.PricePerMonth)
            .Select(p => new SubscriptionPackageDto
            {
                PackageId = p.PackageId,
                PackageCode = p.PackageCode,
                PackageName = p.PackageName,
                Description = p.Description,
                PackageLevel = p.PackageLevel,
                PricePerMonth = p.PricePerMonth,
                PricePerSixMonths = p.PricePerSixMonths,
                PricePerYear = p.PricePerYear,
                MaxUsers = p.MaxUsers,
                MaxInvoicesPerMonth = p.MaxInvoicesPerMonth,
                StorageQuotaGB = p.StorageQuotaGB,
                HasAiProcessing = p.HasAiProcessing,
                HasAdvancedWorkflow = p.HasAdvancedWorkflow,
                HasRiskWarning = p.HasRiskWarning,
                HasAuditLog = p.HasAuditLog,
                HasErpIntegration = p.HasErpIntegration,
                IsActive = p.IsActive,
            })
            .ToListAsync();
    }

    public async Task<CurrentSubscriptionDto> GetCurrentSubscriptionAsync(Guid companyId)
    {
        var company =
            await _context
                .Companies.Include(c => c.SubscriptionPackage)
                .FirstOrDefaultAsync(c => c.CompanyId == companyId)
            ?? throw new KeyNotFoundException("Company not found.");

        // Lazy fallback to Free package if expired
        if (
            company.SubscriptionExpiredAt.HasValue
            && DateTime.UtcNow > company.SubscriptionExpiredAt.Value
        )
        {
            var freePackage = await _context.SubscriptionPackages.FirstOrDefaultAsync(p =>
                p.PackageCode == "FREE"
            );
            if (freePackage != null && company.SubscriptionPackageId != freePackage.PackageId)
            {
                company.SubscriptionPackageId = freePackage.PackageId;
                company.SubscriptionPackage = freePackage;
                company.SubscriptionTier = freePackage.PackageCode;
                company.BillingCycle = "Monthly";
                company.SubscriptionStartDate = null;
                company.SubscriptionExpiredAt = null;
                company.MaxUsers = freePackage.MaxUsers;
                company.MaxInvoicesPerMonth = freePackage.MaxInvoicesPerMonth;
                company.StorageQuotaGB = freePackage.StorageQuotaGB;
                company.UpdatedAt = DateTime.UtcNow;
                company.UsedInvoicesThisMonth = 0;
                company.CurrentCycleStart = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        // Lazy reset for accurate read
        if (DateTime.UtcNow >= company.CurrentCycleStart.AddMonths(1))
        {
            company.UsedInvoicesThisMonth = 0;
            company.CurrentCycleStart = DateTime.UtcNow;
            company.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return new CurrentSubscriptionDto
        {
            PackageCode = company.SubscriptionPackage?.PackageCode,
            PackageName = company.SubscriptionPackage?.PackageName,
            PackageLevel = company.SubscriptionPackage?.PackageLevel ?? 0,
            SubscriptionTier = company.SubscriptionTier,
            BillingCycle = company.BillingCycle,
            SubscriptionStartDate = company.SubscriptionStartDate,
            SubscriptionExpiredAt = company.SubscriptionExpiredAt,
            MaxUsers = company.MaxUsers,
            MaxInvoicesPerMonth = company.MaxInvoicesPerMonth,
            StorageQuotaGB = company.StorageQuotaGB,
            UsedInvoicesThisMonth = company.UsedInvoicesThisMonth,
            ExtraInvoicesBalance = company.ExtraInvoicesBalance,
            HasAiProcessing = company.SubscriptionPackage?.HasAiProcessing ?? false,
            HasAdvancedWorkflow = company.SubscriptionPackage?.HasAdvancedWorkflow ?? false,
            HasRiskWarning = company.SubscriptionPackage?.HasRiskWarning ?? false,
            HasAuditLog = company.SubscriptionPackage?.HasAuditLog ?? false,
            HasErpIntegration = company.SubscriptionPackage?.HasErpIntegration ?? false,
        };
    }

    public async Task<CreatePaymentResponse> CreatePaymentAsync(
        CreatePaymentRequest request,
        Guid companyId,
        Guid userId,
        string ipAddress
    )
    {
        var package_ =
            await _context.SubscriptionPackages.FindAsync(request.PackageId)
            ?? throw new KeyNotFoundException("Package not found.");

        // Block downgrade: check if company's current package level >= requested package level
        var company = await _context
            .Companies.Include(c => c.SubscriptionPackage)
            .FirstOrDefaultAsync(c => c.CompanyId == companyId);

        if (
            company?.SubscriptionPackage != null
            && company.SubscriptionExpiredAt > DateTime.UtcNow
            && company.SubscriptionPackage.PackageLevel >= package_.PackageLevel
        )
        {
            throw new InvalidOperationException(
                "Không thể hạ cấp hoặc mua lại gói cùng cấp khi gói hiện tại vẫn còn hiệu lực."
            );
        }

        // Calculate amount based on billing cycle
        var amount = request.BillingCycle switch
        {
            "SemiAnnual" => package_.PricePerSixMonths,
            "Annual" => package_.PricePerYear,
            _ => package_.PricePerMonth,
        };

        if (amount <= 0)
            throw new InvalidOperationException("Gói miễn phí không cần thanh toán.");

        // Create transaction record
        var transaction = new PaymentTransaction
        {
            TransactionId = Guid.NewGuid(),
            CompanyId = companyId,
            PackageId = request.PackageId,
            BillingCycle = request.BillingCycle,
            Amount = amount,
            Currency = "VND",
            VnpTxnRef = DateTime.UtcNow.Ticks.ToString(),
            Status = "Pending",
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _context.PaymentTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        // Build VNPay URL
        var paymentUrl = BuildVnPayUrl(transaction, package_.PackageName, ipAddress);

        return new CreatePaymentResponse
        {
            PaymentUrl = paymentUrl,
            TransactionId = transaction.TransactionId.ToString(),
        };
    }

    public async Task<PaymentResultDto> ProcessVnPayReturnAsync(
        Dictionary<string, string> vnpayData
    )
    {
        // Validate checksum
        var vnpSecureHash = vnpayData.GetValueOrDefault("vnp_SecureHash") ?? "";
        var hashSecret = Environment.GetEnvironmentVariable("VNPAY_HASH_SECRET")
            ?? _configuration["VnPay:HashSecret"]
            ?? throw new InvalidOperationException("VnPay HashSecret not configured.");

        // Remove hash fields for validation
        var dataToHash = vnpayData
            .Where(kv =>
                kv.Key.StartsWith("vnp_")
                && kv.Key != "vnp_SecureHash"
                && kv.Key != "vnp_SecureHashType"
                && !string.IsNullOrEmpty(kv.Value)
            )
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}={VnPayUrlEncode(kv.Value)}")
            .ToList();

        var rawData = string.Join("&", dataToHash);
        var computedHash = HmacSHA512(hashSecret, rawData);

        if (!computedHash.Equals(vnpSecureHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid VNPay signature.");

        var txnRef = vnpayData.GetValueOrDefault("vnp_TxnRef") ?? "";
        var responseCode = vnpayData.GetValueOrDefault("vnp_ResponseCode") ?? "";
        var transactionNo = vnpayData.GetValueOrDefault("vnp_TransactionNo") ?? "";
        var bankCode = vnpayData.GetValueOrDefault("vnp_BankCode") ?? "";
        var cardType = vnpayData.GetValueOrDefault("vnp_CardType") ?? "";
        var payDate = vnpayData.GetValueOrDefault("vnp_PayDate") ?? "";

        var transaction =
            await _context
                .PaymentTransactions.Include(t => t.Package)
                .FirstOrDefaultAsync(t => t.VnpTxnRef == txnRef)
            ?? throw new KeyNotFoundException("Transaction not found.");

        transaction.VnpResponseCode = responseCode;
        transaction.VnpTransactionNo = transactionNo;
        transaction.VnpBankCode = bankCode;
        transaction.VnpCardType = cardType;
        transaction.VnpPayDate = payDate;
        transaction.UpdatedAt = DateTime.UtcNow;

        if (responseCode == "00") // Success
        {
            transaction.Status = "Success";

            if (transaction.PaymentType == "Addon")
            {
                // Add-on: cộng dồn ExtraInvoicesBalance
                var addonCompany = await _context.Companies.FindAsync(transaction.CompanyId);
                if (addonCompany != null)
                {
                    // Parse addon code from OrderInfo: "Addon|ADDON_50_INVOICES"
                    var addonCode = transaction.BillingCycle; // We stored addon code in BillingCycle field
                    if (Addons.TryGetValue(addonCode, out var addon))
                    {
                        addonCompany.ExtraInvoicesBalance += addon.InvoiceCount;
                        addonCompany.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }
            else
            {
                // Subscription upgrade: update company + reset quota
                var company = await _context.Companies.FindAsync(transaction.CompanyId);
                if (company != null && transaction.PackageId.HasValue)
                {
                    company.SubscriptionPackageId = transaction.PackageId.Value;
                    company.SubscriptionTier =
                        transaction.Package?.PackageCode ?? company.SubscriptionTier;
                    company.BillingCycle = transaction.BillingCycle;
                    company.SubscriptionStartDate = DateTime.UtcNow;
                    company.SubscriptionExpiredAt = transaction.BillingCycle switch
                    {
                        "SemiAnnual" => DateTime.UtcNow.AddMonths(6),
                        "Annual" => DateTime.UtcNow.AddYears(1),
                        _ => DateTime.UtcNow.AddMonths(1),
                    };
                    company.MaxUsers = transaction.Package?.MaxUsers ?? company.MaxUsers;
                    company.MaxInvoicesPerMonth =
                        transaction.Package?.MaxInvoicesPerMonth ?? company.MaxInvoicesPerMonth;
                    company.StorageQuotaGB =
                        transaction.Package?.StorageQuotaGB ?? company.StorageQuotaGB;

                    // Reset quota on upgrade
                    company.UsedInvoicesThisMonth = 0;
                    company.CurrentCycleStart = DateTime.UtcNow;

                    company.UpdatedAt = DateTime.UtcNow;
                }
            }
        }
        else
        {
            transaction.Status = "Failed";
            transaction.FailReason = $"VNPay response code: {responseCode}";
        }

        await _context.SaveChangesAsync();

        return new PaymentResultDto
        {
            TransactionId = transaction.TransactionId.ToString(),
            Status = transaction.Status,
            PackageName = transaction.Package?.PackageName,
            BillingCycle = transaction.BillingCycle,
            Amount = transaction.Amount,
            VnpTransactionNo = transactionNo,
            BankCode = bankCode,
            PayDate = payDate,
            Message =
                responseCode == "00"
                    ? "Thanh toán thành công! Gói dịch vụ đã được kích hoạt."
                    : $"Thanh toán thất bại. Mã lỗi: {responseCode}",
        };
    }

    public async Task<List<PaymentHistoryDto>> GetPaymentHistoryAsync(Guid companyId)
    {
        return await _context
            .PaymentTransactions.Where(t => t.CompanyId == companyId)
            .OrderByDescending(t => t.CreatedAt)
            .Include(t => t.Package)
            .Select(t => new PaymentHistoryDto
            {
                TransactionId = t.TransactionId,
                PackageName =
                    t.Package != null
                        ? t.Package.PackageName
                        : (t.PaymentType == "Addon" ? "Add-on Hóa đơn" : null),
                BillingCycle = t.BillingCycle,
                Amount = t.Amount,
                Status = t.Status,
                VnpTransactionNo = t.VnpTransactionNo,
                PaymentType = t.PaymentType,
                CreatedAt = t.CreatedAt,
            })
            .ToListAsync();
    }

    public List<AddonInfoDto> GetAvailableAddons()
    {
        return Addons
            .Select(a => new AddonInfoDto
            {
                AddonCode = a.Key,
                AddonName = a.Value.Name,
                Description = a.Value.Description,
                InvoiceCount = a.Value.InvoiceCount,
                Price = a.Value.Price,
            })
            .ToList();
    }

    public async Task<CreatePaymentResponse> CreateAddonPaymentAsync(
        CreateAddonPaymentRequest request,
        Guid companyId,
        Guid userId,
        string ipAddress
    )
    {
        if (!Addons.TryGetValue(request.AddonCode, out var addon))
            throw new KeyNotFoundException("Add-on không tồn tại.");

        var transaction = new PaymentTransaction
        {
            TransactionId = Guid.NewGuid(),
            CompanyId = companyId,
            PackageId = null,
            PaymentType = "Addon",
            BillingCycle = request.AddonCode, // Store addon code for IPN lookup
            Amount = addon.Price,
            Currency = "VND",
            VnpTxnRef = DateTime.UtcNow.Ticks.ToString(),
            Status = "Pending",
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _context.PaymentTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        var paymentUrl = BuildVnPayUrl(transaction, addon.Name, ipAddress);

        return new CreatePaymentResponse
        {
            PaymentUrl = paymentUrl,
            TransactionId = transaction.TransactionId.ToString(),
        };
    }

    // ═══════════════════════════════════════════════
    //  PRIVATE HELPERS
    // ═══════════════════════════════════════════════

    private static string VnPayUrlEncode(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        var result = new StringBuilder();
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        foreach (byte b in bytes)
        {
            char c = (char)b;
            // Giữ nguyên các ký tự này y hệt chuẩn Java
            if (
                (c >= 'a' && c <= 'z')
                || (c >= 'A' && c <= 'Z')
                || (c >= '0' && c <= '9')
                || c == '.'
                || c == '-'
                || c == '*'
                || c == '_'
            )
            {
                result.Append(c);
            }
            else if (c == ' ')
            {
                result.Append('+'); // Bắt buộc khoảng trắng phải là dấu +
            }
            else
            {
                result.Append("%" + b.ToString("X2")); // Các dấu ngoặc () sẽ thành %28, %29 ở đây
            }
        }
        return result.ToString();
    }

    private string BuildVnPayUrl(
        PaymentTransaction transaction,
        string packageName,
        string ipAddress
    )
    {
        var vnpUrl = Environment.GetEnvironmentVariable("VNPAY_URL")
            ?? _configuration["VnPay:Url"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
        var tmnCode = Environment.GetEnvironmentVariable("VNPAY_TMN_CODE")
            ?? _configuration["VnPay:TmnCode"]
            ?? throw new InvalidOperationException("VnPay TmnCode not configured.");
        var hashSecret = Environment.GetEnvironmentVariable("VNPAY_HASH_SECRET")
            ?? _configuration["VnPay:HashSecret"]
            ?? throw new InvalidOperationException("VnPay HashSecret not configured.");
        var returnUrl = Environment.GetEnvironmentVariable("VNPAY_RETURN_URL")
            ?? _configuration["VnPay:ReturnUrl"] ?? "http://localhost:3000/app/payment/result";

        // VNPay expects amount in VND * 100
        var amountInVnpFormat = ((long)(transaction.Amount * 100)).ToString();

        var rawOrderInfo = $"Thanh toan {packageName} - {transaction.BillingCycle}";
        var safeOrderInfo = GenerateSafeOrderInfo(rawOrderInfo);

        var vnpParams = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            { "vnp_Version", "2.1.0" },
            { "vnp_Command", "pay" },
            { "vnp_TmnCode", tmnCode },
            { "vnp_Amount", amountInVnpFormat },
            { "vnp_CurrCode", "VND" },
            { "vnp_TxnRef", transaction.VnpTxnRef! },
            { "vnp_OrderInfo", safeOrderInfo },
            { "vnp_OrderType", "other" },
            { "vnp_Locale", "vn" },
            { "vnp_ReturnUrl", returnUrl },
            { "vnp_IpAddr", ipAddress ?? "127.0.0.1" },
            { "vnp_CreateDate", DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss") },
        };

        // Build query string
        var queryString = string.Join(
            "&",
            vnpParams.Select(kv => $"{kv.Key}={VnPayUrlEncode(kv.Value)}")
        );

        // Create secure hash
        var secureHash = HmacSHA512(hashSecret, queryString);

        var paymentUrl = $"{vnpUrl}?{queryString}&vnp_SecureHash={secureHash}";

        // Bắt đầu "console.log" kiểu C# nè:
        Console.WriteLine("\n================== LINK VNPAY CỦA BẠN ĐÂY ==================");
        Console.WriteLine(paymentUrl);
        Console.WriteLine("==============================================================\n");

        return paymentUrl;
    }

    private static string HmacSHA512(string key, string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        using var hmac = new HMACSHA512(keyBytes);
        var hash = hmac.ComputeHash(dataBytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }

    private static string GenerateSafeOrderInfo(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        
        // Loại bỏ dấu tiếng Việt
        var normalizedString = input.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();
        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                stringBuilder.Append(c);
        }
        var noDiacritics = stringBuilder.ToString().Normalize(NormalizationForm.FormC).Replace("đ", "d").Replace("Đ", "D");

        // Chỉ giữ lại chữ cái, số và khoảng trắng (Tiêu diệt dấu ngoặc và ký tự lạ)
        var cleanStr = new StringBuilder();
        foreach (var c in noDiacritics)
        {
            if (char.IsLetterOrDigit(c) || c == ' ')
                cleanStr.Append(c);
        }
        return cleanStr.ToString().Trim();
    }
}
