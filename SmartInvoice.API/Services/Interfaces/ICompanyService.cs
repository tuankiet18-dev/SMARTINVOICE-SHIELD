using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartInvoice.API.Entities;

namespace SmartInvoice.API.Services.Interfaces
{
    public interface ICompanyService
    {
        Task<Company> GetByIdAsync(Guid id);
        Task<IEnumerable<Company>> GetAllAsync();
        Task<Company> CreateAsync(Company entity);
        Task UpdateAsync(Company entity);
        Task DeleteAsync(Guid id);
    }
}
