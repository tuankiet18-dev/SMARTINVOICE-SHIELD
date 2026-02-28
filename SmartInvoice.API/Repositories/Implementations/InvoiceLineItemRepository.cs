using SmartInvoice.API.Entities;
using SmartInvoice.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using SmartInvoice.API.Data;
namespace SmartInvoice.API.Repositories.Implementations
{
    public class InvoiceLineItemRepository : BaseRepository<InvoiceLineItem>, IInvoiceLineItemRepository
    {
        public InvoiceLineItemRepository(AppDbContext context) : base(context)
        {
        }
    }
}
