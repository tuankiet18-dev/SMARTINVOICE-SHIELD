using Microsoft.EntityFrameworkCore;
using SmartInvoice.API.Data;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Repositories.Implementations;
using SmartInvoice.API.Repositories.Interfaces;

public class InvoiceAuditLogRepository : BaseRepository<InvoiceAuditLog>, IInvoiceAuditLogRepository
{
    public InvoiceAuditLogRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<InvoiceAuditLog>> GetByInvoiceIdAsync(Guid invoiceId)
    {
        return await _context.Set<InvoiceAuditLog>()
            .Where(log => log.InvoiceId == invoiceId)
            .OrderByDescending(log => log.CreatedAt)
            .ToListAsync();
    }
}
