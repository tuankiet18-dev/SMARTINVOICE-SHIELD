using Microsoft.EntityFrameworkCore;
using SmartInvoice.API.Data;
using SmartInvoice.API.Services.Interfaces;

namespace SmartInvoice.API.Services.Implementations;

public class QuotaService : IQuotaService
{
    private readonly AppDbContext _context;

    public QuotaService(AppDbContext context)
    {
        _context = context;
    }

    public async Task ValidateAndConsumeInvoiceQuotaAsync(Guid companyId)
    {
        var company = await _context.Companies.FirstOrDefaultAsync(c => c.CompanyId == companyId)
            ?? throw new KeyNotFoundException("Company not found.");

        // Bước 1: Lazy Reset — nếu đã qua 1 tháng kể từ CurrentCycleStart thì reset
        if (DateTime.UtcNow >= company.CurrentCycleStart.AddMonths(1))
        {
            company.UsedInvoicesThisMonth = 0;
            company.CurrentCycleStart = DateTime.UtcNow;
        }

        // Bước 2: Tiêu thụ quota gói tháng
        if (company.UsedInvoicesThisMonth < company.MaxInvoicesPerMonth)
        {
            company.UsedInvoicesThisMonth++;
            company.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return;
        }

        // Bước 3: Tiêu thụ gói Add-on
        if (company.ExtraInvoicesBalance > 0)
        {
            company.ExtraInvoicesBalance--;
            company.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return;
        }

        // Bước 4: Hết tất cả quota
        throw new InvalidOperationException(
            "Đã hết giới hạn hóa đơn trong tháng. Vui lòng mua thêm Add-on hoặc Nâng cấp gói.");
    }
}
