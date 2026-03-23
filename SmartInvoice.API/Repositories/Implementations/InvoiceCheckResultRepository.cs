using SmartInvoice.API.Data;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Repositories.Interfaces;

namespace SmartInvoice.API.Repositories.Implementations
{
    public class InvoiceCheckResultRepository : BaseRepository<InvoiceCheckResult>, IInvoiceCheckResultRepository
    {
        public InvoiceCheckResultRepository(AppDbContext dbContext) : base(dbContext)
        {
        }
    }
}
