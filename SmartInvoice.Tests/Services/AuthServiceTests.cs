using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Moq;
using SmartInvoice.API.DTOs.Auth;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Repositories.Interfaces;
using SmartInvoice.API.Services.Implementations;
using SmartInvoice.API.Services.Interfaces;
using SmartInvoice.Tests.Helpers;
using Amazon.CognitoIdentityProvider;

namespace SmartInvoice.Tests.Services;

/// <summary>
/// Unit Tests cho AuthService — tầng xác thực và quản lý người dùng.
/// 
/// Các dependency được mock:
///   - IUnitOfWork (repositories)
///   - IAmazonCognitoIdentityProvider (AWS Cognito)
///   - IHttpClientFactory (gọi VietQR API bên ngoài)
///   - IMemoryCache (cache kết quả validation)
/// </summary>
public class AuthServiceTests
{
    private readonly Mock<IUnitOfWork>                          _mockUow;
    private readonly Mock<ILocalBlacklistRepository>            _mockBlacklistRepo;
    private readonly Mock<ICompanyRepository>                   _mockCompanyRepo;
    private readonly Mock<IUserRepository>                      _mockUserRepo;
    private readonly Mock<IAmazonCognitoIdentityProvider>       _mockCognito;
    private readonly Mock<IHttpClientFactory>                   _mockHttpFactory;
    private readonly IMemoryCache                               _memoryCache;
    private readonly Mock<IQuotaService>                        _mockQuota;
    private readonly AuthService                                _sut;

    public AuthServiceTests()
    {
        _mockUow           = new Mock<IUnitOfWork>();
        _mockBlacklistRepo = new Mock<ILocalBlacklistRepository>();
        _mockCompanyRepo   = new Mock<ICompanyRepository>();
        _mockUserRepo      = new Mock<IUserRepository>();
        _mockCognito       = new Mock<IAmazonCognitoIdentityProvider>();
        _mockHttpFactory   = new Mock<IHttpClientFactory>();
        _mockQuota         = new Mock<IQuotaService>();

        // Dùng MemoryCache thật (không cần mock)
        _memoryCache = new MemoryCache(new MemoryCacheOptions());

        // Wire repos vào UoW
        _mockUow.Setup(u => u.LocalBlacklists).Returns(_mockBlacklistRepo.Object);
        _mockUow.Setup(u => u.Companies).Returns(_mockCompanyRepo.Object);
        _mockUow.Setup(u => u.Users).Returns(_mockUserRepo.Object);
        _mockUow.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

        // Tạo AuthService với config tối thiểu (Cognito keys trống — sẽ override trong test cụ thể)
        var config = BuildInMemoryConfiguration(new Dictionary<string, string?>
        {
            ["COGNITO_CLIENT_ID"]     = "test-client-id",
            ["COGNITO_USER_POOL_ID"]  = "us-east-1_TestPool",
            ["COGNITO_CLIENT_SECRET"] = "test-secret-key-long-enough-for-hmac"
        });

        _sut = new AuthService(
            unitOfWork:        _mockUow.Object,
            context:           null!,
            cognitoClient:     _mockCognito.Object,
            configuration:     config,
            httpClientFactory: _mockHttpFactory.Object,
            cache:             _memoryCache,
            quotaService:      _mockQuota.Object
        );
    }

    // ═══════════════════════════════════════════════════════════════
    //  CheckTaxCodeAsync — Kiểm tra Mã Số Thuế
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Category", "Auth")]
    public async Task CheckTaxCodeAsync_WhenTaxCodeIsBlacklisted_ReturnsInvalidResponse()
    {
        // Arrange: MST này đang bị blacklist (IsActive = true)
        var blacklistEntry = InvoiceTestFactory.CreateBlacklistEntry(
            taxCode:  "BAD0000001",
            isActive: true,
            reason:   "Gian lận thuế VAT");

        _mockBlacklistRepo.Setup(r => r.GetByTaxCodeAsync("BAD0000001"))
            .ReturnsAsync(blacklistEntry);

        // Act
        var result = await _sut.CheckTaxCodeAsync(new CheckTaxCodeRequest
        {
            TaxCode = "BAD0000001"
        });

        // Assert
        result.IsValid.Should().BeFalse("MST bị blacklist phải trả về IsValid=false");
        result.IsRegistered.Should().BeFalse();
        result.ErrorMessage.Should().Contain("từ chối phục vụ",
            "Message phải giải thích lý do bị từ chối");
        result.ErrorMessage.Should().Contain("Gian lận thuế VAT",
            "Message phải nêu lý do cụ thể từ blacklist record");
    }

    [Fact]
    [Trait("Category", "Auth")]
    public async Task CheckTaxCodeAsync_WhenTaxCodeBlacklistIsInactive_ProceedsToDbCheck()
    {
        // Arrange: MST trong blacklist nhưng IsActive = false (đã xóa khỏi blacklist)
        var inactiveEntry = InvoiceTestFactory.CreateBlacklistEntry(
            taxCode:  "OLD0000001",
            isActive: false); // Không còn hiệu lực

        _mockBlacklistRepo.Setup(r => r.GetByTaxCodeAsync("OLD0000001"))
            .ReturnsAsync(inactiveEntry);

        // Company này đã được đăng ký trên hệ thống
        var company = new Company
        {
            CompanyId   = Guid.NewGuid(),
            TaxCode     = "OLD0000001",
            CompanyName = "Công ty Đã Phục Hồi",
            Email       = "contact@restored.com"
        };
        _mockCompanyRepo.Setup(r => r.GetByTaxCodeAsync("OLD0000001"))
            .ReturnsAsync(company);

        // Act
        var result = await _sut.CheckTaxCodeAsync(new CheckTaxCodeRequest
        {
            TaxCode = "OLD0000001"
        });

        // Assert: Phải check DB và trả về IsRegistered=true
        result.IsValid.Should().BeTrue("MST không còn trong blacklist active");
        result.IsRegistered.Should().BeTrue("MST đã đăng ký trên hệ thống");
        result.CompanyName.Should().Be("Công ty Đã Phục Hồi");
    }

    [Fact]
    [Trait("Category", "Auth")]
    public async Task CheckTaxCodeAsync_WhenTaxCodeAlreadyRegisteredInDb_ReturnsRegisteredResponse()
    {
        // Arrange: MST hợp lệ, đã được registered trong DB (công ty đã có tài khoản)
        var taxCode = "1234567890";
        _mockBlacklistRepo.Setup(r => r.GetByTaxCodeAsync(taxCode))
            .ReturnsAsync((LocalBlacklistedCompany?)null); // Không bị blacklist

        var existingCompany = new Company
        {
            CompanyId   = Guid.NewGuid(),
            TaxCode     = taxCode,
            CompanyName = "Công ty Đã Đăng Ký",
            Email       = "exist@company.com"
        };
        _mockCompanyRepo.Setup(r => r.GetByTaxCodeAsync(taxCode))
            .ReturnsAsync(existingCompany);

        // Act
        var result = await _sut.CheckTaxCodeAsync(new CheckTaxCodeRequest
        {
            TaxCode = taxCode
        });

        // Assert: Trả về IsRegistered=true (không cần gọi VietQR API)
        result.IsValid.Should().BeTrue();
        result.IsRegistered.Should().BeTrue("MST đã đăng ký phải trả về IsRegistered=true");
        result.CompanyName.Should().Be("Công ty Đã Đăng Ký");
    }

    [Fact]
    [Trait("Category", "Auth")]
    public async Task CheckTaxCodeAsync_WhenTaxCodeInCache_ReturnsCachedResultWithoutHttpCall()
    {
        // Arrange: Pre-populate cache với kết quả của MST này
        var taxCode   = "CACHED001";
        var cacheKey  = $"VietQR_TaxCode_{taxCode}";
        var cached    = new CheckTaxCodeResponse
        {
            IsValid      = true,
            IsRegistered = false,
            CompanyName  = "Công ty Từ Cache"
        };

        // Tự seed cache
        _memoryCache.Set(cacheKey, cached, TimeSpan.FromMinutes(5));

        _mockBlacklistRepo.Setup(r => r.GetByTaxCodeAsync(taxCode))
            .ReturnsAsync((LocalBlacklistedCompany?)null);
        _mockCompanyRepo.Setup(r => r.GetByTaxCodeAsync(taxCode))
            .ReturnsAsync((Company?)null); // Chưa đăng ký trong DB

        // Act
        var result = await _sut.CheckTaxCodeAsync(new CheckTaxCodeRequest
        {
            TaxCode = taxCode
        });

        // Assert: Phải lấy từ cache, không gọi HTTP
        result.IsValid.Should().BeTrue();
        result.CompanyName.Should().Be("Công ty Từ Cache");

        // Verify: IHttpClientFactory không được gọi (cache hit)
        _mockHttpFactory.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Never,
            "Khi có cache, không được gọi HTTP client");
    }

    [Fact]
    [Trait("Category", "Auth")]
    public async Task CheckTaxCodeAsync_WhenHttpCallFails_ReturnsInvalidResponse()
    {
        // Arrange: MST không có trong DB, không có cache, HTTP call lỗi
        var taxCode = "FAIL000001";

        _mockBlacklistRepo.Setup(r => r.GetByTaxCodeAsync(taxCode))
            .ReturnsAsync((LocalBlacklistedCompany?)null);
        _mockCompanyRepo.Setup(r => r.GetByTaxCodeAsync(taxCode))
            .ReturnsAsync((Company?)null);

        // HTTP client ném exception (timeout, network error, v.v.)
        var mockHttpClient = new Mock<HttpClient>();
        _mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Throws(new HttpRequestException("Connection refused"));

        // Act
        var result = await _sut.CheckTaxCodeAsync(new CheckTaxCodeRequest
        {
            TaxCode = taxCode
        });

        // Assert: Xử lý graceful — trả về invalid thay vì throw
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Lỗi",
            "Exception phải được handle và trả về error message");
    }

    // ═══════════════════════════════════════════════════════════════
    //  LoginAsync — Đăng nhập
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Category", "Auth")]
    public async Task LoginAsync_WhenUserNotFoundInLocalDb_ThrowsException()
    {
        // Arrange: Cognito auth thành công nhưng user không có trong local DB
        var email = "ghost@test.com";

        SetupSuccessfulCognitoAuth(_mockCognito, email);

        _mockUserRepo.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync((User?)null); // User không tồn tại trong local DB

        // Act & Assert
        await _sut.Invoking(s => s.LoginAsync(new LoginRequest
            {
                Email    = email,
                Password = "Password123!"
            }))
            .Should().ThrowAsync<Exception>()
            .WithMessage("*Không tìm thấy thông tin người dùng*");
    }

    [Fact]
    [Trait("Category", "Auth")]
    public async Task LoginAsync_WhenUserIsInactive_ThrowsException()
    {
        // Arrange: Cognito OK, nhưng local user chưa được kích hoạt
        var email = "inactive@test.com";

        SetupSuccessfulCognitoAuth(_mockCognito, email);

        _mockUserRepo.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync(new User
            {
                Id       = Guid.NewGuid(),
                Email    = email,
                FullName = "Inactive User",
                IsActive = false, // Chưa kích hoạt!
                CognitoSub = "cognito-sub-123",
                CompanyId = Guid.NewGuid()
            });

        // Act & Assert
        await _sut.Invoking(s => s.LoginAsync(new LoginRequest
            {
                Email    = email,
                Password = "Password123!"
            }))
            .Should().ThrowAsync<Exception>()
            .WithMessage("*Tài khoản chưa được kích hoạt*");
    }

    [Fact]
    [Trait("Category", "Auth")]
    public async Task LoginAsync_WhenCompanyIsInactive_ThrowsException()
    {
        // Arrange: User active nhưng company đang bị khóa
        var email     = "user@locked.com";
        var companyId = Guid.NewGuid();

        SetupSuccessfulCognitoAuth(_mockCognito, email);

        _mockUserRepo.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync(new User
            {
                Id         = Guid.NewGuid(),
                Email      = email,
                FullName   = "Active User",
                IsActive   = true,
                CognitoSub = "cognito-sub-456",
                CompanyId  = companyId
            });

        _mockCompanyRepo.Setup(r => r.GetByIdAsync(companyId))
            .ReturnsAsync(new Company
            {
                CompanyId   = companyId,
                CompanyName = "Công ty Bị Khóa",
                TaxCode     = "1234567890",
                Email       = "locked@company.com",
                IsActive    = false // Công ty bị khóa!
            });

        // Act & Assert
        await _sut.Invoking(s => s.LoginAsync(new LoginRequest
            {
                Email    = email,
                Password = "Password123!"
            }))
            .Should().ThrowAsync<Exception>()
            .WithMessage("*Tài khoản công ty đang bị tạm khóa*");
    }

    // ═══════════════════════════════════════════════════════════════
    //  Private Helpers
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Setup mock Cognito client để giả lập auth thành công.
    /// Trả về các token giả hợp lệ.
    /// </summary>
    private static void SetupSuccessfulCognitoAuth(
        Mock<IAmazonCognitoIdentityProvider> mockCognito,
        string email)
    {
        var authResult = new Amazon.CognitoIdentityProvider.Model.AuthenticationResultType
        {
            AccessToken  = FakeJwt(email, "access"),
            IdToken      = FakeJwt(email, "id"),
            RefreshToken = "fake-refresh-token",
            ExpiresIn    = 3600
        };

        var authResponse = new Amazon.CognitoIdentityProvider.Model.InitiateAuthResponse
        {
            AuthenticationResult = authResult
        };

        mockCognito
            .Setup(c => c.InitiateAuthAsync(
                It.IsAny<Amazon.CognitoIdentityProvider.Model.InitiateAuthRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(authResponse);
    }

    /// <summary>
    /// Tạo JWT token giả (base64 không có chữ ký thật — chỉ dùng cho test mock).
    /// </summary>
    private static string FakeJwt(string email, string type)
    {
        var header  = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"alg\":\"RS256\"}"));
        var payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
            $"{{\"sub\":\"cognito-sub-{type}\",\"email\":\"{email}\"}}"));
        return $"{header}.{payload}.fake-signature";
    }

    /// <summary>
    /// Tạo IConfiguration từ dictionary (không cần appsettings file).
    /// </summary>
    private static IConfiguration BuildInMemoryConfiguration(
        Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
