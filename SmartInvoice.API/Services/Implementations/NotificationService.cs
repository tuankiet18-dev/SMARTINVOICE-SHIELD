using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Repositories.Interfaces;
using SmartInvoice.API.Services.Interfaces;

namespace SmartInvoice.API.Services.Implementations
{
    public class NotificationService : INotificationService
    {
        private readonly IUnitOfWork _unitOfWork;

        public NotificationService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Notification> GetByIdAsync(Guid id)
        {
            return await _unitOfWork.Notifications.GetByIdAsync(id);
        }

        public async Task<IEnumerable<Notification>> GetAllAsync()
        {
            return await _unitOfWork.Notifications.GetAllAsync();
        }

        public async Task<Notification> CreateAsync(Notification entity)
        {
            await _unitOfWork.Notifications.AddAsync(entity);
            await _unitOfWork.CompleteAsync();
            return entity;
        }

        public async Task UpdateAsync(Notification entity)
        {
            _unitOfWork.Notifications.Update(entity);
            await _unitOfWork.CompleteAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.Notifications.GetByIdAsync(id);
            if (entity != null)
            {
                _unitOfWork.Notifications.Remove(entity);
                await _unitOfWork.CompleteAsync();
            }
        }

        public async Task SendNotificationAsync(Guid userId, string type, string title, string message, string? actionUrl = null, string? actionText = null, Guid? relatedInvoiceId = null, string priority = "Normal")
        {
            var notification = new Notification
            {
                UserId = userId,
                Type = type,
                Title = title,
                Message = message,
                ActionUrl = actionUrl,
                ActionText = actionText,
                RelatedInvoiceId = relatedInvoiceId,
                Priority = priority,
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Notifications.AddAsync(notification);
            await _unitOfWork.CompleteAsync();
        }

        public async Task SendNotificationToCompanyAdminsAsync(Guid companyId, string type, string title, string message, string? actionUrl = null, string? actionText = null, Guid? relatedInvoiceId = null, string priority = "Normal")
        {
            var admins = await _unitOfWork.Users.GetByCompanyIdAsync(companyId);
            var companyAdmins = admins.Where(u => u.Role == SmartInvoice.API.Enums.UserRole.CompanyAdmin.ToString());

            foreach (var admin in companyAdmins)
            {
                if (admin.ReceiveInAppNotifications)
                {
                    await SendNotificationAsync(admin.Id, type, title, message, actionUrl, actionText, relatedInvoiceId, priority);
                }
            }
        }

        public async Task<IEnumerable<Notification>> GetUserNotificationsAsync(Guid userId, bool unreadOnly = false, int pageIndex = 1, int pageSize = 20)
        {
            return await ((INotificationRepository)_unitOfWork.Notifications).GetByUserIdAsync(userId, unreadOnly, pageIndex, pageSize);
        }

        public async Task<int> GetUnreadCountAsync(Guid userId)
        {
            return await ((INotificationRepository)_unitOfWork.Notifications).GetUnreadCountAsync(userId);
        }

        public async Task MarkAsReadAsync(Guid notificationId, Guid userId)
        {
            var notification = await _unitOfWork.Notifications.GetByIdAsync(notificationId);
            if (notification != null && notification.UserId == userId && !notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
                _unitOfWork.Notifications.Update(notification);
                await _unitOfWork.CompleteAsync();
            }
        }

        public async Task MarkAllAsReadAsync(Guid userId)
        {
            var notifications = await ((INotificationRepository)_unitOfWork.Notifications).GetByUserIdAsync(userId, true, 1, 1000);
            foreach (var notification in notifications)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
                _unitOfWork.Notifications.Update(notification);
            }
            if (notifications.Any())
            {
                await _unitOfWork.CompleteAsync();
            }
        }
    }
}
