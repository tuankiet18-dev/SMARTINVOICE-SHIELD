using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartInvoice.API.Data;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Repositories.Interfaces;

namespace SmartInvoice.API.Repositories.Implementations
{
    public class InvoiceRepository : BaseRepository<Invoice>, IInvoiceRepository
    {
        public InvoiceRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<Invoice?> GetInvoiceWithDetailsAsync(Guid id)
        {
            return await _context.Invoices
                .Include(i => i.Company)
                .Include(i => i.DocumentType)
                .Include(i => i.ValidationLayers)
                .Include(i => i.RiskCheckResults)
                .Include(i => i.AuditLogs)
                .Include(i => i.InvoiceLineItems)
                .Include(i => i.OriginalFile)
                .FirstOrDefaultAsync(i => i.InvoiceId == id);
        }

        public async Task<bool> ExistsByDetailsAsync(string sellerTaxCode, string serialNumber, string invoiceNumber)
        {
            return await _context.Invoices.AnyAsync(i =>
                i.SellerTaxCode == sellerTaxCode &&
                i.SerialNumber == serialNumber &&
                i.InvoiceNumber == invoiceNumber);
        }

        public async Task<bool> ExistsByNumberAsync(string invoiceNumber, Guid companyId)
        {
            return await _context.Invoices.AnyAsync(i => i.InvoiceNumber == invoiceNumber && i.CompanyId == companyId);
        }

        public async Task<(IEnumerable<Invoice> Items, int TotalCount)> GetPagedInvoicesAsync(int pageIndex, int pageSize)
        {
            var query = _context.Invoices.AsQueryable();

            // 1. Đếm tổng số bản ghi (để FE biết có bao nhiêu trang)
            var totalCount = await query.CountAsync();

            // 2. Lấy dữ liệu phân trang
            // Ví dụ: Trang 2 (index 1), size 10 -> Skip(10).Take(10)
            var items = await query
                .Include(i => i.Uploader)
                .OrderByDescending(i => i.InvoiceDate) // Sắp xếp mới nhất lên đầu
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}
