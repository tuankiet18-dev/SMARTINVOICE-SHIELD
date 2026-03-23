using Microsoft.EntityFrameworkCore;
using SmartInvoice.API.Data;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Repositories.Interfaces;

namespace SmartInvoice.API.Repositories.Implementations;

public class ExportConfigRepository : BaseRepository<ExportConfig>, IExportConfigRepository
{
    public ExportConfigRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<ExportConfig?> GetByCompanyIdAsync(Guid companyId)
    {
        return await _dbSet.FirstOrDefaultAsync(ec => ec.CompanyId == companyId);
    }
}
