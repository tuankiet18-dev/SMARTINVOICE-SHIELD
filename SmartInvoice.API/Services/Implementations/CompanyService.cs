using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Repositories.Interfaces;
using SmartInvoice.API.Services.Interfaces;

namespace SmartInvoice.API.Services.Implementations
{
    public class CompanyService : ICompanyService
    {
        private readonly IUnitOfWork _unitOfWork;

        public CompanyService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Company> GetByIdAsync(Guid id)
        {
            return await _unitOfWork.Companies.GetByIdAsync(id);
        }

        public async Task<IEnumerable<Company>> GetAllAsync()
        {
            return await _unitOfWork.Companies.GetAllAsync();
        }

        public async Task<Company> CreateAsync(Company entity)
        {
            await _unitOfWork.Companies.AddAsync(entity);
            await _unitOfWork.CompleteAsync();
            return entity;
        }

        public async Task UpdateAsync(Company entity)
        {
            _unitOfWork.Companies.Update(entity);
            await _unitOfWork.CompleteAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.Companies.GetByIdAsync(id);
            if (entity != null)
            {
                _unitOfWork.Companies.Remove(entity);
                await _unitOfWork.CompleteAsync();
            }
        }
    }
}
