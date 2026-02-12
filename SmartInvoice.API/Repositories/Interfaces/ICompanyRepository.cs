using System.Threading.Tasks;
using SmartInvoice.API.Entities;

namespace SmartInvoice.API.Repositories.Interfaces
{
    public interface ICompanyRepository : IGenericRepository<Company>
    {
        Task<Company?> GetByTaxCodeAsync(string taxCode);
    }
}
