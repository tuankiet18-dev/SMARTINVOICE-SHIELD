using SmartInvoice.API.Entities;
using SmartInvoice.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using SmartInvoice.API.Data;
namespace SmartInvoice.API.Repositories.Implementations
{
    public class DocumentTypeRepository : BaseRepository<DocumentType>, IDocumentTypeRepository
    {
        public DocumentTypeRepository(AppDbContext context) : base(context)
        {
        }
    }
}
