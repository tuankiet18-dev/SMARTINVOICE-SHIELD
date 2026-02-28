using SmartInvoice.API.Entities;
using SmartInvoice.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using SmartInvoice.API.Data;
namespace SmartInvoice.API.Repositories.Implementations
{
    public class FileStorageRepository : BaseRepository<FileStorage>, IFileStorageRepository
    {
        public FileStorageRepository(AppDbContext context) : base(context)
        {
        }
    }
}
