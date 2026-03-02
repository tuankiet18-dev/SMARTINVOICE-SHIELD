using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartInvoice.API.Entities;

namespace SmartInvoice.API.Services.Interfaces
{
    public interface IUserService
    {
        Task<User?> GetUserByIdAsync(Guid id);
        Task<User?> GetUserByEmailAsync(string email);
        Task<IEnumerable<User>> GetAllUsersAsync();
        Task<IEnumerable<User>> GetUsersByCompanyIdAsync(Guid companyId);
        Task<User> CreateUserAsync(User user);
        Task UpdateUserAsync(User user);
        Task DeleteUserAsync(Guid id);

        // Company Admin User Management
        Task<User> CreateCompanyMemberAsync(SmartInvoice.API.DTOs.User.CreateCompanyMemberDto dto, Guid companyId);
        Task UpdateCompanyMemberAsync(Guid userId, SmartInvoice.API.DTOs.User.UpdateCompanyMemberDto dto, Guid companyId);
        Task DeleteCompanyMemberAsync(Guid userId, Guid companyId);
    }
}
