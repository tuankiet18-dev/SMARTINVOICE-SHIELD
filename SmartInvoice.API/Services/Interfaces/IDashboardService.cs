using SmartInvoice.API.DTOs.Dashboard;

namespace SmartInvoice.API.Services.Interfaces;

public interface IDashboardService
{
    Task<DashboardStatsDto> GetDashboardStatsAsync(Guid companyId, string userRole, Guid userId, string period = "30d");
}
