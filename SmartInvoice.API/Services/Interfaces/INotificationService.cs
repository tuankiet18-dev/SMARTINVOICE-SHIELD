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

        Task SendNotificationAsync(Guid userId, string type, string title, string message, string? actionUrl = null, string? actionText = null, Guid? relatedInvoiceId = null, string priority = "Normal");
        Task SendNotificationToCompanyAdminsAsync(Guid companyId, string type, string title, string message, string? actionUrl = null, string? actionText = null, Guid? relatedInvoiceId = null, string priority = "Normal");
        Task<IEnumerable<Notification>> GetUserNotificationsAsync(Guid userId, bool unreadOnly = false, int pageIndex = 1, int pageSize = 20);
        Task<int> GetUnreadCountAsync(Guid userId);
        Task MarkAsReadAsync(Guid notificationId, Guid userId);
        Task MarkAllAsReadAsync(Guid userId);
    }
}
