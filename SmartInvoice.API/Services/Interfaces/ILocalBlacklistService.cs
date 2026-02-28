using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartInvoice.API.Entities;

namespace SmartInvoice.API.Services.Interfaces
{
    public interface ILocalBlacklistService
    {
        Task<LocalBlacklistedCompany> GetByIdAsync(Guid id);
        Task<IEnumerable<LocalBlacklistedCompany>> GetAllAsync();
        Task<LocalBlacklistedCompany> CreateAsync(LocalBlacklistedCompany entity);
        Task UpdateAsync(LocalBlacklistedCompany entity);
        Task DeleteAsync(Guid id);
    }
}
