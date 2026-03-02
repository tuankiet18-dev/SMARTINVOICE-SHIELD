using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartInvoice.API.Entities;

namespace SmartInvoice.API.Services.Interfaces
{
    public interface INotificationService
    {
        Task<Notification> GetByIdAsync(Guid id);
        Task<IEnumerable<Notification>> GetAllAsync();
        Task<Notification> CreateAsync(Notification entity);
        Task UpdateAsync(Notification entity);
        Task DeleteAsync(Guid id);
    }
}
