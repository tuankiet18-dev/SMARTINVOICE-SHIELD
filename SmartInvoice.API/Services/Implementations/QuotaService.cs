using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartInvoice.API.Data;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Services.Interfaces;

namespace SmartInvoice.API.Services.Implementations;

public class QuotaService : IQuotaService
{
    private readonly AppDbContext _context;

    public QuotaService(AppDbContext context)
    {
        _context = context;
    }

    private async Task HandlePlanDowngradeAndLimitsAsync(Company company)
    {
        // 1. Kiểm tra hết hạn: Nếu có ngày hết hạn và đã qua ngày hết hạn
        if (company.SubscriptionExpiredAt.HasValue && DateTime.UtcNow > company.SubscriptionExpiredAt.Value)
        {
            // Trả về gói Free cứng của hệ thống
            var freePackage = await _context.SubscriptionPackages.FirstOrDefaultAsync(p => p.PackageCode == "FREE");
            if (freePackage != null && company.SubscriptionPackageId != freePackage.PackageId)
            {
                company.SubscriptionPackageId = freePackage.PackageId;
                company.SubscriptionPackage = freePackage;
                company.SubscriptionTier = freePackage.PackageCode;
                company.BillingCycle = "Monthly";
                company.SubscriptionStartDate = null;
                company.SubscriptionExpiredAt = null; // Gói Free thì không có ngày hết hạn
                company.MaxUsers = freePackage.MaxUsers;
                company.MaxInvoicesPerMonth = freePackage.MaxInvoicesPerMonth;
                company.StorageQuotaGB = freePackage.StorageQuotaGB;
                
                // Reset lại chu kỳ và số bill của tháng
                company.UsedInvoicesThisMonth = 0;
                company.CurrentCycleStart = DateTime.UtcNow;

                _context.Companies.Update(company);
                await _context.SaveChangesAsync();
            }
        }

        // 2. Chặn tương tác nếu số user hiện tại đang lớn hơn mức cho phép của gói hiện hành
        // Điều này áp dụng ngay khi công ty rớt xuống gói Free (ví dụ 5 user > 1 user allowed)
        if (company.CurrentActiveUsers > company.MaxUsers)
        {
            throw new InvalidOperationException(
                $"Giới thiệu gói cước của bạn đã thay đổi. Số lượng user hiện tại ({company.CurrentActiveUsers}) vượt quá giới hạn ({company.MaxUsers}) của gói. Vui lòng vô hiệu hóa/xóa bớt user dư thừa hoặc nâng cấp gói để tiếp tục sử dụng dịch vụ."
            );
        }
    }

    public async Task ValidateAndConsumeInvoiceQuotaAsync(Guid companyId)
    {
        var company = await _context.Companies.FirstOrDefaultAsync(c => c.CompanyId == companyId)
            ?? throw new KeyNotFoundException("Không tìm thấy thông tin công ty.");

        await HandlePlanDowngradeAndLimitsAsync(company);

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

    public async Task ValidateUserQuotaAsync(Guid companyId)
    {
        var company = await _context.Companies.FirstOrDefaultAsync(c => c.CompanyId == companyId)
            ?? throw new KeyNotFoundException("Không tìm thấy thông tin công ty.");

        await HandlePlanDowngradeAndLimitsAsync(company);

        if (company.CurrentActiveUsers >= company.MaxUsers)
        {
            throw new InvalidOperationException($"Không thể tạo thêm người dùng. Giới hạn gói hiện tại là {company.MaxUsers} người dùng.");
        }
    }

    public async Task IncreaseUserCountAsync(Guid companyId)
    {
        var company = await _context.Companies.FirstOrDefaultAsync(c => c.CompanyId == companyId)
            ?? throw new KeyNotFoundException("Không tìm thấy thông tin công ty.");

        company.CurrentActiveUsers++;
        company.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task DecreaseUserCountAsync(Guid companyId)
    {
        var company = await _context.Companies.FirstOrDefaultAsync(c => c.CompanyId == companyId)
            ?? throw new KeyNotFoundException("Không tìm thấy thông tin công ty.");

        if (company.CurrentActiveUsers > 0)
        {
            company.CurrentActiveUsers--;
            company.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task ValidateStorageQuotaAsync(Guid companyId, long fileSizeInBytes)
    {
        var company = await _context.Companies.FirstOrDefaultAsync(c => c.CompanyId == companyId)
            ?? throw new KeyNotFoundException("Không tìm thấy thông tin công ty.");

        await HandlePlanDowngradeAndLimitsAsync(company);

        long quotaInBytes = company.StorageQuotaGB * 1000L * 1000L * 1000L; // Tính chuẩn hệ thập phân (1GB = 1 tỷ bytes)
        if (company.UsedStorageBytes + fileSizeInBytes > quotaInBytes)
        {
            throw new InvalidOperationException($"Lỗi thao tác: Tổng dung lượng lưu trữ đang sử dụng đã vượt quá giới hạn ({company.StorageQuotaGB}GB). Vui lòng nâng cấp gói đăng ký để tải lên hoặc trích xuất thêm dữ liệu.");
        }
    }

    public async Task ConsumeStorageQuotaAsync(Guid companyId, long fileSizeInBytes)
    {
        var company = await _context.Companies.FirstOrDefaultAsync(c => c.CompanyId == companyId)
            ?? throw new KeyNotFoundException("Không tìm thấy thông tin công ty.");

        company.UsedStorageBytes += fileSizeInBytes;
        company.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task ReleaseStorageQuotaAsync(Guid companyId, long fileSizeInBytes)
    {
        var company = await _context.Companies.FirstOrDefaultAsync(c => c.CompanyId == companyId)
            ?? throw new KeyNotFoundException("Company not found.");

        if (company.UsedStorageBytes >= fileSizeInBytes)
        {
            company.UsedStorageBytes -= fileSizeInBytes;
        }
        else
        {
            company.UsedStorageBytes = 0; // Prevent negative sizes
        }

        company.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
}