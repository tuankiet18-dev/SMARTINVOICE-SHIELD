using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartInvoice.API.DTOs;
using SmartInvoice.API.DTOs.Invoice;
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

        public async Task UpdateInvoiceAsync(Guid id, UpdateInvoiceDto request)
        {
            var existingInvoice = await _unitOfWork.Invoices.GetByIdAsync(id);

            if (existingInvoice == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy hóa đơn với ID: {id}");
            }

            existingInvoice.InvoiceNumber = request.InvoiceNumber ?? existingInvoice.InvoiceNumber;
            existingInvoice.SerialNumber = request.SerialNumber ?? existingInvoice.SerialNumber;
            existingInvoice.InvoiceDate = request.InvoiceDate;
            existingInvoice.TotalAmount = request.TotalAmount;
            existingInvoice.Status = request.Status ?? existingInvoice.Status;
            existingInvoice.Notes = request.Notes;

            if (request.TotalAmount > 0) existingInvoice.TotalAmount = request.TotalAmount;

            existingInvoice.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Invoices.Update(existingInvoice);
            await _unitOfWork.CompleteAsync();
        }

        public async Task<bool> DeleteInvoiceAsync(Guid id)
        {
            var invoice = await _unitOfWork.Invoices.GetByIdAsync(id);
            if (invoice == null)
            {
                return false;
            }

            _unitOfWork.Invoices.Remove(invoice);
            await _unitOfWork.CompleteAsync();
            return true;
        }

        public async Task<bool> ValidateInvoiceAsync(Guid id)
        {
            // Placeholder for 3-Layer Validation Logic
            return true;
        }

        public async Task<PagedResult<InvoiceDto>> GetInvoicesAsync(int pageIndex, int pageSize)
        {
            var result = await _unitOfWork.Invoices.GetPagedInvoicesAsync(pageIndex, pageSize);

            // Map từ Entity sang DTO thủ công (Hoặc dùng AutoMapper sau này)
            var dtos = result.Items.Select(i => new InvoiceDto
            {
                InvoiceId = i.InvoiceId,
                InvoiceNumber = i.InvoiceNumber,
                SerialNumber = i.SerialNumber,
                InvoiceDate = i.InvoiceDate,
                SellerName = i.SellerName,
                SellerTaxCode = i.SellerTaxCode,
                TotalAmount = i.TotalAmount,
                InvoiceCurrency = i.InvoiceCurrency,
                Status = i.Status,
                RiskLevel = i.RiskLevel,
                ProcessingMethod = i.ProcessingMethod,
                UploadedByName = i.Uploader?.FullName ?? "Unknown" // Lấy tên từ bảng User đã Include
            }).ToList();

            return new PagedResult<InvoiceDto>
            {
                Items = dtos,
                TotalCount = result.TotalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }

        public async Task<IEnumerable<InvoiceAuditLogDto>> GetAuditLogsAsync(Guid invoiceId)
        {
            var invoiceExists = await _unitOfWork.Invoices.GetByIdAsync(invoiceId);
            if (invoiceExists == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy hóa đơn ID: {invoiceId}");
            }

            var logs = await _unitOfWork.InvoiceAuditLogs.GetByInvoiceIdAsync(invoiceId);

            var dtos = logs.Select(log => new InvoiceAuditLogDto
            {
                AuditId = log.AuditId,
                UserEmail = log.UserEmail, //
                UserRole = log.UserRole,   //
                IpAddress = log.IpAddress,
                Action = log.Action,
                CreatedAt = log.CreatedAt,
                Changes = log.Changes,     //
                Reason = log.Reason,
                Comment = log.Comment
            }).ToList();

            return dtos;
        }
    }
}
