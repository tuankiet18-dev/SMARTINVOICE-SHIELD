using SmartInvoice.API.Entities;
namespace SmartInvoice.API.Repositories.Interfaces
{
    public interface IFileStorageRepository : IGenericRepository<FileStorage>
    {
        Task<FileStorage?> FindByS3KeyAsync(string s3Key);
    }
}
