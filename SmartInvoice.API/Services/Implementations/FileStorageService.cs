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

        public FileStorageService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
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
            await _unitOfWork.FileStorages.AddAsync(entity);
            await _unitOfWork.CompleteAsync();
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
            }
        }
    }
}
