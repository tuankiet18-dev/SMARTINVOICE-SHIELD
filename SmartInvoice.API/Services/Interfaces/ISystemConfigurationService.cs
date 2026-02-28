using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartInvoice.API.Entities;

namespace SmartInvoice.API.Services.Interfaces
{
    public interface ISystemConfigurationService
    {
        Task<SystemConfiguration> GetByIdAsync(Guid id);
        Task<IEnumerable<SystemConfiguration>> GetAllAsync();
        Task<SystemConfiguration> CreateAsync(SystemConfiguration entity);
        Task UpdateAsync(SystemConfiguration entity);
        Task DeleteAsync(Guid id);
    }
}
