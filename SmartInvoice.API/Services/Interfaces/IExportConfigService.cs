using SmartInvoice.API.DTOs.Export;

namespace SmartInvoice.API.Services.Interfaces;

public interface IExportConfigService
{
    Task<ExportConfigDto?> GetExportConfigAsync(Guid companyId);
    Task<ExportConfigDto> UpdateExportConfigAsync(Guid companyId, UpdateExportConfigDto dto);
}
