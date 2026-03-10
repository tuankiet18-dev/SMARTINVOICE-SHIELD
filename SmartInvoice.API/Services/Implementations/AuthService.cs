using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using SmartInvoice.API.DTOs.Auth;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Repositories.Interfaces;
using SmartInvoice.API.Services.Interfaces;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Runtime;
using SmartInvoice.API.Enums; // For AmazonServiceException if needed, but specific exceptions are in Model
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace SmartInvoice.API.Services.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAmazonCognitoIdentityProvider _cognitoClient;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly string _clientId;
        private readonly string _userPoolId;
        private readonly string _clientSecret;

        public AuthService(
            IUnitOfWork unitOfWork,
            IAmazonCognitoIdentityProvider cognitoClient,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            IMemoryCache cache)
        {
            _unitOfWork = unitOfWork;
            _cognitoClient = cognitoClient;
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _clientId = configuration["COGNITO_CLIENT_ID"] ?? "";
            _userPoolId = configuration["COGNITO_USER_POOL_ID"] ?? "";
            _clientSecret = configuration["COGNITO_CLIENT_SECRET"] ?? "";
        }

        private string CalculateSecretHash(string username)
        {
            var data = username + _clientId;
            var key = Encoding.UTF8.GetBytes(_clientSecret);
            var message = Encoding.UTF8.GetBytes(data);

            using (var hmac = new System.Security.Cryptography.HMACSHA256(key))
            {
                var hash = hmac.ComputeHash(message);
                return Convert.ToBase64String(hash);
            }
        }

        public async Task<CheckTaxCodeResponse> CheckTaxCodeAsync(CheckTaxCodeRequest request)
        {
            // 1. Check in local DB
            var existingCompany = await _unitOfWork.Companies.GetByTaxCodeAsync(request.TaxCode);
            if (existingCompany != null)
            {
                return new CheckTaxCodeResponse
                {
                    IsValid = true,
                    IsRegistered = true,
                    CompanyName = existingCompany.CompanyName,
                    ErrorMessage = "Company already registered."
                };
            }

            // 2. Check Cache
            string cacheKey = $"VietQR_TaxCode_{request.TaxCode}";
            if (_cache.TryGetValue(cacheKey, out CheckTaxCodeResponse cachedResponse))
            {
                return cachedResponse;
            }

            // 3. External Validation via VietQR API
            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"https://api.vietqr.io/v2/business/{request.TaxCode}";
                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("code", out var codeElement) && codeElement.GetString() == "00")
                    {
                        var data = root.GetProperty("data");
                        var companyName = data.GetProperty("name").GetString();
                        var address = data.GetProperty("address").GetString();

                        var validResponse = new CheckTaxCodeResponse
                        {
                            IsValid = true,
                            IsRegistered = false,
                            CompanyName = companyName,
                            Address = address
                        };

                        // Cache successful response for 7 days
                        var cacheEntryOptions = new MemoryCacheEntryOptions()
                            .SetAbsoluteExpiration(TimeSpan.FromDays(7));
                        _cache.Set(cacheKey, validResponse, cacheEntryOptions);

                        return validResponse;
                    }
                    else
                    {
                        var desc = root.TryGetProperty("desc", out var descElement) ? descElement.GetString() : "Unknown error";
                        return new CheckTaxCodeResponse
                        {
                            IsValid = false,
                            IsRegistered = false,
                            ErrorMessage = $"Mã số thuế không hợp lệ hoặc không tồn tại: {desc}"
                        };
                    }
                }
                else
                {
                    return new CheckTaxCodeResponse
                    {
                        IsValid = false,
                        IsRegistered = false,
                        ErrorMessage = $"Không thể xác thực mã số thuế. Status: {response.StatusCode}"
                    };
                }
            }
            catch (Exception ex)
            {
                return new CheckTaxCodeResponse
                {
                    IsValid = false,
                    IsRegistered = false,
                    ErrorMessage = $"Lỗi khi xác thực mã số thuế: {ex.Message}"
                };
            }
        }

        public async Task RegisterCompanyAsync(RegisterCompanyRequest request)
        {
            // Normalize Email
            var normalizedEmail = request.AdminEmail.ToLower().Trim();

            // 1. Validation
            var existingUser = await _unitOfWork.Users.GetByEmailAsync(normalizedEmail);
            if (existingUser != null) throw new Exception("Email already exists.");

            var taxCodeCheck = await CheckTaxCodeAsync(new CheckTaxCodeRequest { TaxCode = request.TaxCode });
            if (!taxCodeCheck.IsValid || taxCodeCheck.IsRegistered)
            {
                throw new Exception(taxCodeCheck.ErrorMessage ?? "Mã số thuế không hợp lệ hoặc đã được đăng ký.");
            }

            // 2. Transaction
            await _unitOfWork.BeginTransactionAsync();
            bool cognitoCreated = false;

            try
            {
                // Create Company using validated data from VietQR
                var company = new Company
                {
                    CompanyId = Guid.NewGuid(),
                    CompanyName = taxCodeCheck.CompanyName ?? request.CompanyName ?? "Default Company Name", // Fallback to avoid nullability issues
                    TaxCode = request.TaxCode,
                    Address = string.IsNullOrWhiteSpace(taxCodeCheck.Address) ? request.Address : taxCodeCheck.Address, // Use API address if available
                    Email = request.AdminEmail, // Using Admin Email since Company Email was removed
                    PhoneNumber = request.AdminPhone, // Using Admin Phone
                    BusinessType = request.BusinessType, // Using mapped payload data
                    LegalRepresentative = request.AdminFullName, // Using Admin Name as representative
                    SubscriptionTier = SubscriptionTier.Free.ToString(),
                    IsActive = true
                };
                await _unitOfWork.Companies.AddAsync(company);
                await _unitOfWork.CompleteAsync();

                // 3. Register in Cognito
                var secretHash = CalculateSecretHash(normalizedEmail);
                var signUpRequest = new SignUpRequest
                {
                    ClientId = _clientId,
                    SecretHash = secretHash,
                    Username = normalizedEmail,
                    Password = request.Password,
                    UserAttributes = new List<AttributeType>
                    {
                        new AttributeType { Name = "email", Value = normalizedEmail },
                        new AttributeType { Name = "name", Value = request.AdminFullName },
                        new AttributeType { Name = "custom:company_id", Value = company.CompanyId.ToString() },
                        new AttributeType { Name = "custom:role", Value = UserRole.CompanyAdmin.ToString() }
                    }
                };

                var signUpResponse = await _cognitoClient.SignUpAsync(signUpRequest);
                var cognitoSub = signUpResponse.UserSub;
                cognitoCreated = true;

                // 4. Create Local User
                var user = new User
                {
                    Email = normalizedEmail,
                    CognitoSub = cognitoSub,
                    FullName = request.AdminFullName,
                    CompanyId = company.CompanyId,
                    Role = UserRole.CompanyAdmin.ToString(),
                    Permissions = new List<string>
                    {
                        SmartInvoice.API.Constants.Permissions.UserView,
                        SmartInvoice.API.Constants.Permissions.UserManage,
                        SmartInvoice.API.Constants.Permissions.InvoiceView,
                        SmartInvoice.API.Constants.Permissions.InvoiceUpload,
                        SmartInvoice.API.Constants.Permissions.InvoiceEdit,
                        SmartInvoice.API.Constants.Permissions.InvoiceApprove,
                        SmartInvoice.API.Constants.Permissions.InvoiceReject,
                        SmartInvoice.API.Constants.Permissions.InvoiceOverrideRisk,
                        SmartInvoice.API.Constants.Permissions.ReportExport
                    },
                    IsActive = false, // Not active until verified
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _unitOfWork.Users.AddAsync(user);
                await _unitOfWork.CompleteAsync();

                await _unitOfWork.CommitTransactionAsync();
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();

                if (cognitoCreated)
                {
                    try
                    {
                        var deleteRequest = new AdminDeleteUserRequest
                        {
                            UserPoolId = _userPoolId,
                            Username = normalizedEmail
                        };
                        await _cognitoClient.AdminDeleteUserAsync(deleteRequest);
                    }
                    catch { /* Ignore rollback failure */ }
                }

                if (ex is UsernameExistsException)
                    throw new Exception("Email already registered in Cognito.");

                throw;
            }
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            try
            {
                var normalizedEmail = request.Email.ToLower().Trim();

                // 1. Authenticate with Cognito
                var secretHash = CalculateSecretHash(normalizedEmail);
                var authRequest = new InitiateAuthRequest
                {
                    ClientId = _clientId,
                    AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,
                    AuthParameters = new Dictionary<string, string>
                    {
                        { "USERNAME", normalizedEmail },
                        { "PASSWORD", request.Password },
                        { "SECRET_HASH", secretHash }
                    }
                };

                var authResponse = await _cognitoClient.InitiateAuthAsync(authRequest);
                if (authResponse.ChallengeName == ChallengeNameType.NEW_PASSWORD_REQUIRED)
                {
                    return new LoginResponse
                    {
                        ChallengeName = authResponse.ChallengeName.ToString(),
                        Session = authResponse.Session
                    };
                }

                var result = authResponse.AuthenticationResult;

                var user = await _unitOfWork.Users.GetByEmailAsync(normalizedEmail);
                if (user == null)
                {
                    throw new Exception($"Local user record not found for email: {normalizedEmail}");
                }

                if (!user.IsActive)
                {
                    throw new Exception("Account is inactive.");
                }

                var company = await _unitOfWork.Companies.GetByIdAsync(user.CompanyId);
                if (company != null && !company.IsActive)
                {
                    throw new Exception("Company account is locked.");
                }

                user.LastLoginAt = DateTime.UtcNow;
                await _unitOfWork.CompleteAsync();

                return new LoginResponse
                {
                    AccessToken = result.AccessToken,
                    IdToken = result.IdToken,
                    RefreshToken = result.RefreshToken,
                    Expiration = DateTime.UtcNow.AddSeconds(result.ExpiresIn ?? 3600),
                    User = new SmartInvoice.API.DTOs.User.UserProfileDto
                    {
                        Id = user.Id,
                        Email = user.Email,
                        FullName = user.FullName,
                        EmployeeId = user.EmployeeId,
                        CompanyId = user.CompanyId,
                        CompanyName = company?.CompanyName,
                        Role = user.Role,
                        Permissions = user.Permissions,
                        IsActive = user.IsActive
                    }
                };
            }
            catch (NotAuthorizedException)
            {
                throw new Exception("Invalid email or password.");
            }
            catch (UserNotConfirmedException)
            {
                throw new Exception("Account not verified. Please check your email.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Login failed: {ex.Message}");
            }
        }

        public async Task<LoginResponse> RespondToNewPasswordRequiredAsync(RespondToNewPasswordRequest request)
        {
            try
            {
                var normalizedEmail = request.Email.ToLower().Trim();
                var secretHash = CalculateSecretHash(normalizedEmail);

                var challengeRequest = new RespondToAuthChallengeRequest
                {
                    ClientId = _clientId,
                    ChallengeName = ChallengeNameType.NEW_PASSWORD_REQUIRED,
                    Session = request.Session,
                    ChallengeResponses = new Dictionary<string, string>
                    {
                        { "USERNAME", normalizedEmail },
                        { "NEW_PASSWORD", request.NewPassword },
                        { "SECRET_HASH", secretHash }
                    }
                };

                var authResponse = await _cognitoClient.RespondToAuthChallengeAsync(challengeRequest);
                var result = authResponse.AuthenticationResult;

                var user = await _unitOfWork.Users.GetByEmailAsync(normalizedEmail);
                if (user == null)
                    throw new Exception("Local user record not found.");

                user.LastLoginAt = DateTime.UtcNow;
                await _unitOfWork.CompleteAsync();

                var company = await _unitOfWork.Companies.GetByIdAsync(user.CompanyId);

                return new LoginResponse
                {
                    AccessToken = result.AccessToken,
                    IdToken = result.IdToken,
                    RefreshToken = result.RefreshToken,
                    Expiration = DateTime.UtcNow.AddSeconds(result.ExpiresIn ?? 3600),
                    User = new SmartInvoice.API.DTOs.User.UserProfileDto
                    {
                        Id = user.Id,
                        Email = user.Email,
                        FullName = user.FullName,
                        EmployeeId = user.EmployeeId,
                        CompanyId = user.CompanyId,
                        CompanyName = company?.CompanyName,
                        Role = user.Role,
                        Permissions = user.Permissions,
                        IsActive = user.IsActive
                    }
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to respond to new password requirement: {ex.Message}");
            }
        }

        public async Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request)
        {
            try
            {
                var normalizedEmail = request.Email.ToLower().Trim();
                var secretHash = CalculateSecretHash(normalizedEmail);

                var authRequest = new InitiateAuthRequest
                {
                    ClientId = _clientId,
                    AuthFlow = AuthFlowType.REFRESH_TOKEN_AUTH,
                    AuthParameters = new Dictionary<string, string>
                    {
                        { "REFRESH_TOKEN", request.RefreshToken },
                        { "SECRET_HASH", secretHash }
                    }
                };

                var authResponse = await _cognitoClient.InitiateAuthAsync(authRequest);
                var result = authResponse.AuthenticationResult;

                var user = await _unitOfWork.Users.GetByEmailAsync(normalizedEmail);
                if (user == null)
                    throw new Exception("Local user record not found.");

                var company = await _unitOfWork.Companies.GetByIdAsync(user.CompanyId);

                return new LoginResponse
                {
                    AccessToken = result.AccessToken,
                    IdToken = result.IdToken,
                    RefreshToken = request.RefreshToken, // Usually keep the old one unless Cognito issues a new one
                    Expiration = DateTime.UtcNow.AddSeconds(result.ExpiresIn ?? 3600),
                    User = new SmartInvoice.API.DTOs.User.UserProfileDto
                    {
                        Id = user.Id,
                        Email = user.Email,
                        FullName = user.FullName,
                        EmployeeId = user.EmployeeId,
                        CompanyId = user.CompanyId,
                        CompanyName = company?.CompanyName,
                        Role = user.Role,
                        Permissions = user.Permissions,
                        IsActive = user.IsActive
                    }
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Refresh token failed: {ex.Message}");
            }
        }

        public async Task VerifyEmailAsync(VerifyEmailRequest request)
        {
            try
            {
                var normalizedEmail = request.Email.ToLower().Trim();
                var secretHash = CalculateSecretHash(normalizedEmail);
                var confirmRequest = new ConfirmSignUpRequest
                {
                    ClientId = _clientId,
                    SecretHash = secretHash,
                    Username = normalizedEmail,
                    ConfirmationCode = request.Token
                };

                await _cognitoClient.ConfirmSignUpAsync(confirmRequest);

                // Update local status
                var user = await _unitOfWork.Users.GetByEmailAsync(normalizedEmail);
                if (user != null)
                {
                    user.IsActive = true;
                    // await _unitOfWork.Users.UpdateAsync(user); // If needed, but tracking might handle it
                    await _unitOfWork.CompleteAsync();
                }
            }
            catch (CodeMismatchException)
            {
                throw new Exception("Invalid verification code.");
            }
            catch (ExpiredCodeException)
            {
                throw new Exception("Verification code expired.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Verification failed: {ex.Message}");
            }
        }

        public async Task SeedSuperAdminAsync(SeedSuperAdminRequest request)
        {
            var normalizedEmail = request.Email.ToLower().Trim();

            // 1. Check if user exists locally
            var existingUser = await _unitOfWork.Users.GetByEmailAsync(normalizedEmail);
            if (existingUser != null) throw new Exception("Admin email already exists locally.");

            await _unitOfWork.BeginTransactionAsync();
            bool cognitoCreated = false;

            try
            {
                // 2. Create a dummy "System Administration" company
                var companyId = Guid.NewGuid();
                var company = new Company
                {
                    CompanyId = companyId,
                    CompanyName = "System Administration",
                    TaxCode = "SUPERADMIN_SYS",
                    Address = "System Managed",
                    Email = normalizedEmail,
                    PhoneNumber = "0000000000",
                    SubscriptionTier = SubscriptionTier.Enterprise.ToString(),
                    IsActive = true
                };
                await _unitOfWork.Companies.AddAsync(company);
                await _unitOfWork.CompleteAsync();

                // 3. Register in Cognito
                var secretHash = CalculateSecretHash(normalizedEmail);
                var signUpRequest = new SignUpRequest
                {
                    ClientId = _clientId,
                    SecretHash = secretHash,
                    Username = normalizedEmail,
                    Password = request.Password,
                    UserAttributes = new List<AttributeType>
                    {
                        new AttributeType { Name = "email", Value = normalizedEmail },
                        new AttributeType { Name = "name", Value = request.FullName },
                        new AttributeType { Name = "custom:company_id", Value = companyId.ToString() },
                        new AttributeType { Name = "custom:role", Value = UserRole.SuperAdmin.ToString() }
                    }
                };

                var signUpResponse = await _cognitoClient.SignUpAsync(signUpRequest);
                var cognitoSub = signUpResponse.UserSub;
                cognitoCreated = true;

                // 4. Auto-Confirm User in Cognito (Requires Admin Confirm SignUp)
                var confirmRequest = new AdminConfirmSignUpRequest
                {
                    UserPoolId = _userPoolId,
                    Username = normalizedEmail
                };
                await _cognitoClient.AdminConfirmSignUpAsync(confirmRequest);

                // 5. Create Local User with SuperAdmin Role
                var user = new User
                {
                    Email = normalizedEmail,
                    CognitoSub = cognitoSub,
                    FullName = request.FullName,
                    CompanyId = companyId,
                    Role = UserRole.SuperAdmin.ToString(),
                    Permissions = new List<string> { "*" }, // SuperAdmin gets everything conventionally or a specific wildcard
                    IsActive = true, // Force active
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _unitOfWork.Users.AddAsync(user);
                await _unitOfWork.CompleteAsync();

                await _unitOfWork.CommitTransactionAsync();
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();

                if (cognitoCreated)
                {
                    try
                    {
                        var deleteRequest = new AdminDeleteUserRequest
                        {
                            UserPoolId = _userPoolId,
                            Username = normalizedEmail
                        };
                        await _cognitoClient.AdminDeleteUserAsync(deleteRequest);
                    }
                    catch { /* Ignore rollback failure */ }
                }

                if (ex is UsernameExistsException)
                    throw new Exception("Email already registered in Cognito.");

                throw;
            }
        }
    }
}