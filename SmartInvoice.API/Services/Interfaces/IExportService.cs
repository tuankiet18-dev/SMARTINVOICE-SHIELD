using SmartInvoice.API.DTOs.Export;

namespace SmartInvoice.API.Services.Interfaces;

public interface IExportService
{
    Task<ExportResultDto> GenerateExportAsync(Guid companyId, Guid userId, GenerateExportRequestDto request);
}
