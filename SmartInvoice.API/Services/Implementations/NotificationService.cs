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
    }
}
