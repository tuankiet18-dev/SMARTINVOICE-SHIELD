using SmartInvoice.API.DTOs.Dashboard;

namespace SmartInvoice.API.Services.Interfaces;

public interface IDashboardService
{
    Task<DashboardStatsDto> GetDashboardStatsAsync(Guid companyId, string period = "30d");
}
