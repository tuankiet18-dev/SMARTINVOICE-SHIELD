using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartInvoice.API.DTOs;
using SmartInvoice.API.DTOs.Invoice;
using SmartInvoice.API.Entities;

namespace SmartInvoice.API.Services.Interfaces
{
    public interface IInvoiceService
    {
        Task<Invoice?> GetInvoiceByIdAsync(Guid id);
        Task<IEnumerable<Invoice>> GetAllInvoicesAsync();
        Task<Invoice> CreateInvoiceAsync(Invoice invoice);
        Task UpdateInvoiceAsync(Guid id, UpdateInvoiceDto request);
        Task<bool> DeleteInvoiceAsync(Guid id);
        Task<bool> ValidateInvoiceAsync(Guid id); 
        Task<PagedResult<InvoiceDto>> GetInvoicesAsync(int pageIndex, int pageSize);
        Task<IEnumerable<InvoiceAuditLogDto>> GetAuditLogsAsync(Guid invoiceId);
    }
}
