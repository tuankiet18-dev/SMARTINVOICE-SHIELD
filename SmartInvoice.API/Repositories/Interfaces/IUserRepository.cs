using System.Threading.Tasks;
using SmartInvoice.API.Entities;

namespace SmartInvoice.API.Repositories.Interfaces
{
    public interface IUserRepository : IGenericRepository<User>
    {
        Task<User?> GetByEmailAsync(string email);
        Task<bool> ExistsByEmailAsync(string email);
        Task<IEnumerable<User>> GetByCompanyIdAsync(System.Guid companyId);
    }
}
