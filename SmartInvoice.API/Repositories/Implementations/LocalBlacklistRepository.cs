using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartInvoice.API.Data;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Repositories.Interfaces;

namespace SmartInvoice.API.Repositories.Implementations
{
    public class LocalBlacklistRepository : BaseRepository<LocalBlacklistedCompany>, ILocalBlacklistRepository
    {
        public LocalBlacklistRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<LocalBlacklistedCompany?> GetByTaxCodeAsync(string taxCode)
        {
            return await _context.LocalBlacklists.FirstOrDefaultAsync(b => b.TaxCode == taxCode);
        }
    }
}
