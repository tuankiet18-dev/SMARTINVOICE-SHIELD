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
                Task<PagedResult<InvoiceDto>> GetInvoicesAsync(DTOs.Invoice.GetInvoicesQueryDto query, Guid companyId, Guid userId, string userRole);
                Task<IEnumerable<InvoiceAuditLogDto>> GetAuditLogsAsync(Guid invoiceId);
                Task<ValidationResultDto> ProcessInvoiceXmlAsync(string s3Key, string userId, string companyId);
                Task SubmitInvoiceAsync(Guid id, Guid userId);
                Task ApproveInvoiceAsync(Guid id, Guid userId);
                Task RejectInvoiceAsync(Guid id, Guid userId, string reason);
        }
}
