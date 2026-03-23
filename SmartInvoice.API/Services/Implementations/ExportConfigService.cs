using SmartInvoice.API.DTOs.Export;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Repositories.Interfaces;
using SmartInvoice.API.Services.Interfaces;

namespace SmartInvoice.API.Services.Implementations;

public class ExportConfigService : IExportConfigService
{
    private readonly IUnitOfWork _unitOfWork;

    public ExportConfigService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ExportConfigDto?> GetExportConfigAsync(Guid companyId)
    {
        var config = await _unitOfWork.ExportConfigs.GetByCompanyIdAsync(companyId);
        if (config == null) return null;

        return MapToDto(config);
    }

    public async Task<ExportConfigDto> UpdateExportConfigAsync(Guid companyId, UpdateExportConfigDto dto)
    {
        var config = await _unitOfWork.ExportConfigs.GetByCompanyIdAsync(companyId);

        if (config == null)
        {
            // Auto-create config nếu chưa tồn tại
            config = new ExportConfig
            {
                ConfigId = Guid.NewGuid(),
                CompanyId = companyId,
                DefaultDebitAccount = dto.DefaultDebitAccount,
                DefaultCreditAccount = dto.DefaultCreditAccount,
                DefaultTaxAccount = dto.DefaultTaxAccount,
                DefaultWarehouse = dto.DefaultWarehouse,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _unitOfWork.ExportConfigs.AddAsync(config);
        }
        else
        {
            config.DefaultDebitAccount = dto.DefaultDebitAccount;
            config.DefaultCreditAccount = dto.DefaultCreditAccount;
            config.DefaultTaxAccount = dto.DefaultTaxAccount;
            config.DefaultWarehouse = dto.DefaultWarehouse;
            config.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.ExportConfigs.Update(config);
        }

        await _unitOfWork.CompleteAsync();
        return MapToDto(config);
    }

    private static ExportConfigDto MapToDto(ExportConfig config)
    {
        return new ExportConfigDto
        {
            ConfigId = config.ConfigId,
            CompanyId = config.CompanyId,
            DefaultDebitAccount = config.DefaultDebitAccount,
            DefaultCreditAccount = config.DefaultCreditAccount,
            DefaultTaxAccount = config.DefaultTaxAccount,
            DefaultWarehouse = config.DefaultWarehouse
        };
    }
}
