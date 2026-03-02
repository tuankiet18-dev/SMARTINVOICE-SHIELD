using SmartInvoice.API.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartInvoice.API.Repositories.Interfaces
{
    public interface IInvoiceAuditLogRepository : IGenericRepository<InvoiceAuditLog>
    {
        Task<IEnumerable<InvoiceAuditLog>> GetByInvoiceIdAsync(Guid invoiceId);
    }
}