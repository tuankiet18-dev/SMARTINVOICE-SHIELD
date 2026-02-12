using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartInvoice.API.Entities;

namespace SmartInvoice.API.Services.Interfaces
{
    public interface IInvoiceService
    {
        Task<Invoice?> GetInvoiceByIdAsync(Guid id);
        Task<IEnumerable<Invoice>> GetAllInvoicesAsync();
        Task<Invoice> CreateInvoiceAsync(Invoice invoice);
        Task UpdateInvoiceAsync(Invoice invoice);
        Task DeleteInvoiceAsync(Guid id);
        Task<bool> ValidateInvoiceAsync(Guid id); 
    }
}
