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
                .FirstOrDefaultAsync(i => i.InvoiceId == id);
        }

        public async Task<bool> ExistsByNumberAsync(string invoiceNumber, Guid companyId)
        {
            return await _context.Invoices.AnyAsync(i => i.InvoiceNumber == invoiceNumber && i.CompanyId == companyId);
        }
    }
}
