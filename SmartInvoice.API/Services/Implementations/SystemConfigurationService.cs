using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Repositories.Interfaces;
using SmartInvoice.API.Services.Interfaces;

namespace SmartInvoice.API.Services.Implementations
{
    public class SystemConfigurationService : ISystemConfigurationService
    {
        private readonly IUnitOfWork _unitOfWork;

        public SystemConfigurationService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<SystemConfiguration> GetByIdAsync(Guid id)
        {
            return await _unitOfWork.SystemConfigurations.GetByIdAsync(id);
        }

        public async Task<IEnumerable<SystemConfiguration>> GetAllAsync()
        {
            return await _unitOfWork.SystemConfigurations.GetAllAsync();
        }

        public async Task<SystemConfiguration> CreateAsync(SystemConfiguration entity)
        {
            await _unitOfWork.SystemConfigurations.AddAsync(entity);
            await _unitOfWork.CompleteAsync();
            return entity;
        }

        public async Task UpdateAsync(SystemConfiguration entity)
        {
            _unitOfWork.SystemConfigurations.Update(entity);
            await _unitOfWork.CompleteAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.SystemConfigurations.GetByIdAsync(id);
            if (entity != null)
            {
                _unitOfWork.SystemConfigurations.Remove(entity);
                await _unitOfWork.CompleteAsync();
            }
        }
    }
}
