using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using SmartInvoice.API.DTOs.User;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Enums;
using SmartInvoice.API.Repositories.Interfaces;
using SmartInvoice.API.Services.Interfaces;

namespace SmartInvoice.API.Services.Implementations
{
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAmazonCognitoIdentityProvider _cognitoClient;
        private readonly string _userPoolId;
        private readonly IQuotaService _quotaService;

        public UserService(
            IUnitOfWork unitOfWork,
            IAmazonCognitoIdentityProvider cognitoClient,
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            IQuotaService quotaService
        )
        {
            _unitOfWork = unitOfWork;
            _cognitoClient = cognitoClient;
            _userPoolId = configuration["COGNITO_USER_POOL_ID"] ?? "";
            _quotaService = quotaService;
        }

        public async Task<User?> GetUserByIdAsync(Guid id)
        {
            return await _unitOfWork.Users.GetByIdAsync(id);
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _unitOfWork.Users.GetByEmailAsync(email);
        }

        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            return await _unitOfWork.Users.GetAllAsync();
        }

        public async Task<IEnumerable<User>> GetUsersByCompanyIdAsync(Guid companyId, bool includeDeleted = false)
        {
            return await _unitOfWork.Users.GetByCompanyIdAsync(companyId, includeDeleted);
        }

        public async Task<User> CreateUserAsync(User user)
        {
            // TODO: Hash password here
            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.CompleteAsync();
            return user;
        }

        public async Task UpdateUserAsync(User user)
        {
            _unitOfWork.Users.Update(user);
            await _unitOfWork.CompleteAsync();
        }

        public async Task DeleteUserAsync(Guid id)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(id);
            if (user != null)
            {
                _unitOfWork.Users.Remove(user);
                await _unitOfWork.CompleteAsync();
            }
        }

        // ==========================================
        // Company Admin User Management
        // ==========================================

        public async Task<User> CreateCompanyMemberAsync(CreateCompanyMemberDto dto, Guid companyId)
        {
            // Kiểm tra giới hạn user của công ty
            await _quotaService.ValidateUserQuotaAsync(companyId);

            var validCompanyRoles = new[]
            {
                UserRole.CompanyAdmin.ToString(),
                UserRole.ChiefAccountant.ToString(),
                UserRole.Accountant.ToString(),
                UserRole.Viewer.ToString(),
            };

            if (string.IsNullOrEmpty(dto.Role) || !validCompanyRoles.Contains(dto.Role))
            {
                dto.Role = UserRole.Accountant.ToString();
            }

            List<string> finalPermissions =
                dto.Permissions != null && dto.Permissions.Any()
                    ? dto.Permissions.ToList()
                    : GetDefaultPermissionsForRole(dto.Role);

            var normalizedEmail = dto.Email.ToLower().Trim();

            var existingUser = await _unitOfWork.Users.GetByEmailAsync(normalizedEmail);
            if (existingUser != null)
            {
                if (!existingUser.IsDeleted)
                    throw new Exception("Email already exists and is active.");
                if (existingUser.CompanyId != companyId)
                    throw new Exception("Email already registered with another company.");
            }

            await _unitOfWork.BeginTransactionAsync();
            bool cognitoCreated = false;
            try
            {
                // 1. Create in Cognito
                var adminCreateUserRequest = new AdminCreateUserRequest
                {
                    UserPoolId = _userPoolId,
                    Username = normalizedEmail,
                    UserAttributes = new List<AttributeType>
                    {
                        new AttributeType { Name = "email", Value = normalizedEmail },
                        new AttributeType { Name = "name", Value = dto.FullName },
                        new AttributeType
                        {
                            Name = "custom:company_id",
                            Value = companyId.ToString(),
                        },
                        new AttributeType { Name = "custom:role", Value = dto.Role },
                    },
                    DesiredDeliveryMediums = new List<string> { "EMAIL" },
                };

                var cognitoResponse = await _cognitoClient.AdminCreateUserAsync(
                    adminCreateUserRequest
                );
                var cognitoSub =
                    cognitoResponse.User.Attributes.Find(a => a.Name == "sub")?.Value
                    ?? throw new Exception("Failed to get Cognito User Sub");
                cognitoCreated = true;

                User userToReturn;

                if (existingUser != null)
                {
                    // Restore soft-deleted user
                    existingUser.IsDeleted = false;
                    existingUser.DeletedAt = null;
                    existingUser.CognitoSub = cognitoSub;
                    existingUser.FullName = dto.FullName;
                    existingUser.EmployeeId = dto.EmployeeId;
                    existingUser.Role = dto.Role;
                    existingUser.Permissions = finalPermissions;
                    existingUser.IsActive = true;
                    existingUser.UpdatedAt = DateTime.UtcNow;

                    _unitOfWork.Users.Update(existingUser);
                    userToReturn = existingUser;
                }
                else
                {
                    // Create new user in DB
                    var newUser = new User
                    {
                        Id = Guid.NewGuid(),
                        Email = normalizedEmail,
                        CognitoSub = cognitoSub,
                        CompanyId = companyId,
                        FullName = dto.FullName,
                        EmployeeId = dto.EmployeeId,
                        Role = dto.Role,
                        Permissions = finalPermissions,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                    };
                    await _unitOfWork.Users.AddAsync(newUser);
                    userToReturn = newUser;
                }

                await _unitOfWork.CompleteAsync();

                await _unitOfWork.CommitTransactionAsync();

                // Cập nhật tăng số user sử dụng trong gói
                await _quotaService.IncreaseUserCountAsync(companyId);

                return userToReturn;
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
                            Username = normalizedEmail,
                        };
                        await _cognitoClient.AdminDeleteUserAsync(deleteRequest);
                    }
                    catch
                    { /* Ignore rollback failure */
                    }
                }

                throw new Exception($"Failed to create member: {ex.Message}");
            }
        }

        public async Task UpdateCompanyMemberAsync(
            Guid userId,
            UpdateCompanyMemberDto dto,
            Guid companyId
        )
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null || user.CompanyId != companyId)
                throw new Exception("User not found or you don't have permission.");

            bool wasActive = user.IsActive;

            user.FullName = dto.FullName;
            user.EmployeeId = dto.EmployeeId;
            user.Role = dto.Role;
            user.Permissions = dto.Permissions ?? new List<string>();
            user.IsActive = dto.IsActive;
            user.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Users.Update(user);

            if (wasActive && !dto.IsActive)
            {
                await _quotaService.DecreaseUserCountAsync(companyId);
            }
            else if (!wasActive && dto.IsActive)
            {
                await _quotaService.IncreaseUserCountAsync(companyId);
            }

            // Also update Cognito if needed
            if (user.IsActive == false)
            {
                // Disable user
                var disableReq = new AdminDisableUserRequest
                {
                    UserPoolId = _userPoolId,
                    Username = user.Email,
                };
                await _cognitoClient.AdminDisableUserAsync(disableReq);
            }
            else
            {
                var enableReq = new AdminEnableUserRequest
                {
                    UserPoolId = _userPoolId,
                    Username = user.Email,
                };
                await _cognitoClient.AdminEnableUserAsync(enableReq);
            }

            // Sync attributes
            var updateAttrReq = new AdminUpdateUserAttributesRequest
            {
                UserPoolId = _userPoolId,
                Username = user.Email,
                UserAttributes = new List<AttributeType>
                {
                    new AttributeType { Name = "name", Value = dto.FullName },
                    new AttributeType { Name = "custom:role", Value = dto.Role },
                },
            };
            await _cognitoClient.AdminUpdateUserAttributesAsync(updateAttrReq);

            await _unitOfWork.CompleteAsync();
        }

        public async Task DeleteCompanyMemberAsync(Guid userId, Guid companyId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null || user.CompanyId != companyId)
                throw new Exception("User not found or you don't have permission.");

            try
            {
                var delReq = new AdminDeleteUserRequest { UserPoolId = _userPoolId, Username = user.Email };
                await _cognitoClient.AdminDeleteUserAsync(delReq);
            }
            catch (UserNotFoundException) { }

            bool wasActive = user.IsActive; 
            
            user.IsActive = false; 

            _unitOfWork.Users.Remove(user);
            await _unitOfWork.CompleteAsync();

            if (wasActive) 
            {
                await _quotaService.DecreaseUserCountAsync(companyId);
            }
        }

        private List<string> GetDefaultPermissionsForRole(string role)
        {
            return role switch
            {
                "CompanyAdmin" => new List<string>
                {
                    Constants.Permissions.CompanyView,
                    Constants.Permissions.CompanyManage,
                    Constants.Permissions.UserView,
                    Constants.Permissions.UserManage,
                    Constants.Permissions.InvoiceView,
                    Constants.Permissions.InvoiceUpload,
                    Constants.Permissions.InvoiceEdit,
                    Constants.Permissions.InvoiceApprove,
                    Constants.Permissions.InvoiceReject,
                    Constants.Permissions.InvoiceOverrideRisk,
                    Constants.Permissions.ReportExport,
                },
                "ChiefAccountant" => new List<string>
                {
                    Constants.Permissions.InvoiceView,
                    Constants.Permissions.InvoiceUpload,
                    Constants.Permissions.InvoiceEdit,
                    Constants.Permissions.InvoiceApprove,
                    Constants.Permissions.InvoiceReject,
                    Constants.Permissions.InvoiceOverrideRisk,
                    Constants.Permissions.ReportExport,
                    Constants.Permissions.UserView,
                },
                "Accountant" => new List<string>
                {
                    Constants.Permissions.InvoiceView,
                    Constants.Permissions.InvoiceUpload,
                    Constants.Permissions.InvoiceEdit,
                    Constants.Permissions.ReportExport,
                },
                "Viewer" => new List<string> { Constants.Permissions.InvoiceView },
                _ => new List<string>(), // Mặc định không có quyền gì nếu role rác
            };
        }
    }
}
