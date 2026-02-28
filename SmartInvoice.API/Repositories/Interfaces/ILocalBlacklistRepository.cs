using System.Threading.Tasks;
using SmartInvoice.API.Entities;

namespace SmartInvoice.API.Repositories.Interfaces
{
    public interface ILocalBlacklistRepository : IGenericRepository<LocalBlacklistedCompany>
    {
        Task<LocalBlacklistedCompany?> GetByTaxCodeAsync(string taxCode);
    }
}
