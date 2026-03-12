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

        public async Task<FileStorage?> FindByS3KeyAsync(string s3Key)
        {
            return await _dbSet.FirstOrDefaultAsync(f => f.S3Key == s3Key);
        }
    }
}
