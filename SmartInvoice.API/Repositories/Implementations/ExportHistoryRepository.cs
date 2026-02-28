using SmartInvoice.API.Entities;
using SmartInvoice.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using SmartInvoice.API.Data;
namespace SmartInvoice.API.Repositories.Implementations
{
    public class ExportHistoryRepository : BaseRepository<ExportHistory>, IExportHistoryRepository
    {
        public ExportHistoryRepository(AppDbContext context) : base(context)
        {
        }
    }
}
