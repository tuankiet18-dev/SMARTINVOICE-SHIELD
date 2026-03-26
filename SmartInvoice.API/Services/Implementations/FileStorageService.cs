using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Repositories.Interfaces;
using SmartInvoice.API.Services.Interfaces;

namespace SmartInvoice.API.Services.Implementations
{
    public class FileStorageService : IFileStorageService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IQuotaService _quotaService;

        public FileStorageService(IUnitOfWork unitOfWork, IQuotaService quotaService)
        {
            _unitOfWork = unitOfWork;
            _quotaService = quotaService;
        }

        public async Task<FileStorage> GetByIdAsync(Guid id)
        {
            return await _unitOfWork.FileStorages.GetByIdAsync(id);
        }

        public async Task<IEnumerable<FileStorage>> GetAllAsync()
        {
            return await _unitOfWork.FileStorages.GetAllAsync();
        }

        public async Task<FileStorage> CreateAsync(FileStorage entity)
        {
            // Kiểm tra dung lượng còn trống không trước khi lưu
            await _quotaService.ValidateStorageQuotaAsync(entity.CompanyId, entity.FileSize);

            await _unitOfWork.FileStorages.AddAsync(entity);
            await _unitOfWork.CompleteAsync();

            // Cập nhật mức sử dụng dung lượng
            await _quotaService.ConsumeStorageQuotaAsync(entity.CompanyId, entity.FileSize);

            return entity;
        }

        public async Task UpdateAsync(FileStorage entity)
        {
            _unitOfWork.FileStorages.Update(entity);
            await _unitOfWork.CompleteAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.FileStorages.GetByIdAsync(id);
            if (entity != null)
            {
                _unitOfWork.FileStorages.Remove(entity);
                await _unitOfWork.CompleteAsync();
                // Giải phóng dung lượng khi file bị xóa
                await _quotaService.ReleaseStorageQuotaAsync(entity.CompanyId, entity.FileSize);            }
        }
    }
}
