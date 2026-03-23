using SmartInvoice.API.Entities;

namespace SmartInvoice.API.Repositories.Interfaces;

public interface IExportConfigRepository : IGenericRepository<ExportConfig>
{
    Task<ExportConfig?> GetByCompanyIdAsync(Guid companyId);
}
