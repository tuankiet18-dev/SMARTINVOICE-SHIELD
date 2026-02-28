using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Repositories.Interfaces;
using SmartInvoice.API.Services.Interfaces;

namespace SmartInvoice.API.Services.Implementations
{
    public class LocalBlacklistService : ILocalBlacklistService
    {
        private readonly IUnitOfWork _unitOfWork;

        public LocalBlacklistService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<LocalBlacklistedCompany> GetByIdAsync(Guid id)
        {
            return await _unitOfWork.LocalBlacklists.GetByIdAsync(id);
        }

        public async Task<IEnumerable<LocalBlacklistedCompany>> GetAllAsync()
        {
            return await _unitOfWork.LocalBlacklists.GetAllAsync();
        }

        public async Task<LocalBlacklistedCompany> CreateAsync(LocalBlacklistedCompany entity)
        {
            await _unitOfWork.LocalBlacklists.AddAsync(entity);
            await _unitOfWork.CompleteAsync();
            return entity;
        }

        public async Task UpdateAsync(LocalBlacklistedCompany entity)
        {
            _unitOfWork.LocalBlacklists.Update(entity);
            await _unitOfWork.CompleteAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.LocalBlacklists.GetByIdAsync(id);
            if (entity != null)
            {
                _unitOfWork.LocalBlacklists.Remove(entity);
                await _unitOfWork.CompleteAsync();
            }
        }
    }
}
