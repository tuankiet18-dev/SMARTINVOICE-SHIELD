using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartInvoice.API.Entities;

namespace SmartInvoice.API.Services.Interfaces
{
    public interface IFileStorageService
    {
        Task<FileStorage> GetByIdAsync(Guid id);
        Task<IEnumerable<FileStorage>> GetAllAsync();
        Task<FileStorage> CreateAsync(FileStorage entity);
        Task UpdateAsync(FileStorage entity);
        Task DeleteAsync(Guid id);
    }
}
