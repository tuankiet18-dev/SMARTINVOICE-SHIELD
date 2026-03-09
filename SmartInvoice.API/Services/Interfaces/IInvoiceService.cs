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
                // ─── Query ───
                Task<InvoiceDetailDto?> GetInvoiceDetailAsync(Guid invoiceId, Guid companyId, Guid userId, string userRole);
                Task<IEnumerable<Invoice>> GetAllInvoicesAsync();
                Task<PagedResult<InvoiceDto>> GetInvoicesAsync(GetInvoicesQueryDto query, Guid companyId, Guid userId, string userRole);
                Task<IEnumerable<InvoiceAuditLogDto>> GetAuditLogsAsync(Guid invoiceId);

                // ─── CRUD ───
                Task<Invoice> CreateInvoiceAsync(Invoice invoice);
                Task UpdateInvoiceAsync(Guid id, UpdateInvoiceDto request, Guid userId, string userEmail, string userRole, string? ipAddress);
                Task<bool> DeleteInvoiceAsync(Guid id, Guid companyId, Guid userId, string userRole);

                // ─── Workflow ───
                Task SubmitInvoiceAsync(Guid invoiceId, Guid companyId, Guid userId, string userEmail, string userRole, string? comment, string? ipAddress);
                Task<BatchSubmitResultDto> SubmitBatchAsync(List<Guid> invoiceIds, Guid companyId, Guid userId, string userEmail, string userRole, string? comment, string? ipAddress);
                Task ApproveInvoiceAsync(Guid invoiceId, Guid companyId, Guid userId, string userEmail, string userRole, string? comment, string? ipAddress);
                Task RejectInvoiceAsync(Guid invoiceId, Guid companyId, Guid userId, string userEmail, string userRole, string reason, string? comment, string? ipAddress);

                // ─── Processing ───
                Task<bool> ValidateInvoiceAsync(Guid id);
                Task<ValidationResultDto> ProcessInvoiceXmlAsync(string s3Key, string userId, string companyId);
        }
}
