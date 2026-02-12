using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Repositories.Interfaces;
using SmartInvoice.API.Services.Interfaces;

namespace SmartInvoice.API.Services.Implementations
{
    public class InvoiceService : IInvoiceService
    {
        private readonly IUnitOfWork _unitOfWork;

        public InvoiceService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Invoice?> GetInvoiceByIdAsync(Guid id)
        {
            return await _unitOfWork.Invoices.GetInvoiceWithDetailsAsync(id);
        }

        public async Task<IEnumerable<Invoice>> GetAllInvoicesAsync()
        {
            return await _unitOfWork.Invoices.GetAllAsync();
        }

        public async Task<Invoice> CreateInvoiceAsync(Invoice invoice)
        {
            await _unitOfWork.Invoices.AddAsync(invoice);
            await _unitOfWork.CompleteAsync();
            return invoice;
        }

        public async Task UpdateInvoiceAsync(Invoice invoice)
        {
            _unitOfWork.Invoices.Update(invoice);
            await _unitOfWork.CompleteAsync();
        }

        public async Task DeleteInvoiceAsync(Guid id)
        {
            var invoice = await _unitOfWork.Invoices.GetByIdAsync(id);
            if (invoice != null)
            {
                _unitOfWork.Invoices.Remove(invoice);
                await _unitOfWork.CompleteAsync();
            }
        }
        
        public async Task<bool> ValidateInvoiceAsync(Guid id)
        {
            // Placeholder for 3-Layer Validation Logic
            return true; 
        }
    }
}
