using SmartInvoice.API.Entities;
using SmartInvoice.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using SmartInvoice.API.Data;
namespace SmartInvoice.API.Repositories.Implementations
{
    public class InvoiceAuditLogRepository : BaseRepository<InvoiceAuditLog>, IInvoiceAuditLogRepository
    {
        public InvoiceAuditLogRepository(AppDbContext context) : base(context)
        {
        }
    }
}
