using SmartInvoice.API.DTOs.Export;

namespace SmartInvoice.API.Services.Interfaces;

public interface IExportService
{
    Task<ExportResultDto> GenerateExportAsync(Guid companyId, Guid userId, GenerateExportRequestDto request);
    Task<bool> SoftDeleteExportAsync(Guid exportId, Guid companyId);
    Task<IEnumerable<object>> GetTrashExportsAsync(Guid companyId);
    Task<bool> RestoreExportAsync(Guid exportId, Guid companyId);
    Task<bool> HardDeleteExportAsync(Guid exportId, Guid companyId);
}

