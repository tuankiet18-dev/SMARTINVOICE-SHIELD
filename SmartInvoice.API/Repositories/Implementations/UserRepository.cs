using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartInvoice.API.Data;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Repositories.Interfaces;

namespace SmartInvoice.API.Repositories.Implementations
{
    public class UserRepository : BaseRepository<User>, IUserRepository
    {
        public UserRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            return await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
        }

        public async Task<bool> ExistsByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            return await _context.Users.IgnoreQueryFilters().AnyAsync(u => u.Email.ToLower() == email.ToLower());
        }

        public async Task<System.Collections.Generic.IEnumerable<User>> GetByCompanyIdAsync(System.Guid companyId)
        {
            return await _context.Users.Where(u => u.CompanyId == companyId).ToListAsync();
        }
    }
}
