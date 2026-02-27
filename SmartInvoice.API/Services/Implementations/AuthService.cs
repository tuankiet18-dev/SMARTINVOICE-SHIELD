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
using DotNetEnv;
using Amazon.Runtime;
using SmartInvoice.API.Enums; // For AmazonServiceException if needed, but specific exceptions are in Model

namespace SmartInvoice.API.Services.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService; // Optional if Cognito sends emails
        private readonly IAmazonCognitoIdentityProvider _cognitoClient;
        private readonly string _clientId;
        private readonly string _userPoolId;
        private readonly string _clientSecret;

        public AuthService(
            IUnitOfWork unitOfWork,
            IEmailService emailService,
            IAmazonCognitoIdentityProvider cognitoClient)
        {
            _unitOfWork = unitOfWork;
            _emailService = emailService;
            _cognitoClient = cognitoClient;
            _clientId = Env.GetString("COGNITO_CLIENT_ID");
            _userPoolId = Env.GetString("COGNITO_USER_POOL_ID");
            _clientSecret = Env.GetString("COGNITO_CLIENT_SECRET");
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

            // 2. Mock External Validtion (VietQR/Tax Authority)
            // In real app, call external API here.

            return new CheckTaxCodeResponse
            {
                IsValid = true,
                IsRegistered = false,
                CompanyName = "Unknown Company (Mock)", // Normally fetched from API
                Address = "Unknown Address (Mock)"
            };
        }

        public async Task RegisterCompanyAsync(RegisterCompanyRequest request)
        {
            // Normalize Email
            var normalizedEmail = request.AdminEmail.ToLower().Trim();

            // 1. Validation
            var existingUser = await _unitOfWork.Users.GetByEmailAsync(normalizedEmail);
            if (existingUser != null) throw new Exception("Email already exists.");

            var existingCompany = await _unitOfWork.Companies.GetByTaxCodeAsync(request.TaxCode);
            if (existingCompany != null) throw new Exception("Company TaxCode already registered.");

            // 2. Transaction
            await _unitOfWork.BeginTransactionAsync();

            try
            {
                // Create Company
                var company = new Company
                {
                    CompanyId = Guid.NewGuid(),
                    CompanyName = request.CompanyName,
                    TaxCode = request.TaxCode,
                    Address = request.Address,
                    Email = request.CompanyEmail,
                    PhoneNumber = request.PhoneNumber, // [NEW]
                    BusinessType = request.BusinessType, // [NEW]
                    LegalRepresentative = request.LegalRepresentative, // [NEW]
                    SubscriptionTier = request.SubscriptionTier ?? SubscriptionTier.Free.ToString(),
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
            catch (UsernameExistsException)
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw new Exception("Email already registered in Cognito.");
            }
            catch (Exception)
            {
                await _unitOfWork.RollbackTransactionAsync();
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
                var result = authResponse.AuthenticationResult;

                // 2. Mock check local DB if needed (e.g. for Company status)
                // We don't necessarily NEED to fetch the user if we trust the token, 
                // but front-end might want user details.
                var user = await _unitOfWork.Users.GetByEmailAsync(normalizedEmail);
                if (user == null)
                {
                    // User exists in Cognito but not in local DB? Edge case.
                    // Maybe auto-create or throw. for now throw.
                    throw new Exception($"Local user record not found for email: {normalizedEmail}");
                }

                if (!user.IsActive)
                {
                    // Check if Cognito says confirmed? 
                    // user.IsActive is local soft delete/ban.
                    throw new Exception("Account is inactive.");
                }

                // Check Company
                var company = await _unitOfWork.Companies.GetByIdAsync(user.CompanyId);
                if (company != null && !company.IsActive)
                {
                    throw new Exception("Company account is locked.");
                }

                // Update local stats
                user.LastLoginAt = DateTime.UtcNow;
                await _unitOfWork.CompleteAsync();

                return new LoginResponse
                {
                    AccessToken = result.AccessToken,
                    IdToken = result.IdToken,
                    RefreshToken = result.RefreshToken, // Cognito Refresh Token
                    Expiration = DateTime.UtcNow.AddSeconds(result.ExpiresIn ?? 3600),
                    User = new UserDto
                    {
                        UserId = user.Id,
                        Email = user.Email,
                        FullName = user.FullName,
                        Role = user.Role,
                        CompanyId = user.CompanyId
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

                return new LoginResponse
                {
                    AccessToken = result.AccessToken,
                    IdToken = result.IdToken,
                    RefreshToken = request.RefreshToken, // Usually keep the old one unless Cognito issues a new one
                    Expiration = DateTime.UtcNow.AddSeconds(result.ExpiresIn ?? 3600),
                    User = new UserDto
                    {
                        UserId = user.Id,
                        Email = user.Email,
                        FullName = user.FullName,
                        Role = user.Role,
                        CompanyId = user.CompanyId
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
    }
}