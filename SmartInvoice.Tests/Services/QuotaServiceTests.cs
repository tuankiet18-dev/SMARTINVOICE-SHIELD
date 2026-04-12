using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartInvoice.API.Data;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Services.Implementations;
using SmartInvoice.Tests.Helpers;

namespace SmartInvoice.Tests.Services;

/// <summary>
/// Unit Tests cho QuotaService.
/// 
/// QuotaService dùng AppDbContext trực tiếp (không qua IUnitOfWork)
/// → Dùng EF Core InMemory Provider thông qua TestAppDbContext.
/// 
/// TestAppDbContext: Override OnModelCreating để Ignore AuditChange.Changes property
/// (kiểu List&lt;AuditChange&gt; có OldValue/NewValue: object — InMemory provider không cho phép).
/// 
/// Mỗi test dùng DB riêng (unique name) để tránh xung đột dữ liệu.
/// </summary>
public class QuotaServiceTests
{
    // ─────────────────────────────────────────
    //  Factory helper: Tạo QuotaService với DB freshly seeded
    // ─────────────────────────────────────────

    private static async Task<(TestAppDbContext db, QuotaService service)> CreateServiceWithCompany(
        Company company)
    {
        var db = InMemoryDbHelper.CreateInMemoryDbContext() as TestAppDbContext
                 ?? throw new InvalidOperationException("Expected TestAppDbContext");

        // Tạo SubscriptionPackage dummy đúng constraints
        var package = new SubscriptionPackage
        {
            PackageId   = company.SubscriptionPackageId,
            PackageCode = $"PKG_{Guid.NewGuid():N}",
            PackageName = "Test Package",
            MaxUsers    = 999,
            MaxInvoicesPerMonth = 99999,
            StorageQuotaGB      = 100,
            IsActive    = true,
            HasAdvancedWorkflow = false,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow
        };

        db.SubscriptionPackages.Add(package);
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        return (db, new QuotaService(db));
    }

    // ═══════════════════════════════════════════════════════════════
    //  ValidateAndConsumeInvoiceQuotaAsync
    //  Logic: Tiêu thụ quota hóa đơn trong tháng
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Category", "Quota")]
    public async Task ValidateAndConsumeInvoiceQuotaAsync_WhenCompanyNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange: DB rỗng — không có company nào
        var db      = InMemoryDbHelper.CreateInMemoryDbContext();
        var service = new QuotaService(db);

        // Act & Assert
        await service.Invoking(s => s.ValidateAndConsumeInvoiceQuotaAsync(Guid.NewGuid()))
            .Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Không tìm thấy thông tin công ty*");
    }

    [Fact]
    [Trait("Category", "Quota")]
    public async Task ValidateAndConsumeInvoiceQuotaAsync_WhenMonthlyQuotaExhaustedAndNoAddOn_ThrowsInvalidOperation()
    {
        // Arrange: Company đã dùng hết 100/100 invoice, không có add-on
        // Lưu ý: ExecuteSqlRawAsync trả về 0 trên InMemory → service sẽ check ExtraInvoicesBalance
        var company = InvoiceTestFactory.CreateActiveCompany(
            maxInvoices:  100,
            usedInvoices: 100); // Đã hết quota tháng

        company.ExtraInvoicesBalance = 0; // Không có add-on

        var (_, service) = await CreateServiceWithCompany(company);

        // Act & Assert: Phải ném InvalidOperationException
        await service.Invoking(s => s.ValidateAndConsumeInvoiceQuotaAsync(company.CompanyId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Đã hết giới hạn hóa đơn*");
    }

    [Fact]
    [Trait("Category", "Quota")]
    public async Task ValidateAndConsumeInvoiceQuotaAsync_WhenQuotaAvailable_DoesNotThrow()
    {
        // Arrange: Company còn quota (50/100)
        // Lưu ý InMemory: ExecuteSqlRawAsync ("UPDATE Companies SET UsedInvoicesThisMonth+1") → rowsAffected=0
        //   → Service sẽ tiếp tục kiểm tra ExtraInvoicesBalance
        //   → ExtraInvoicesBalance = 10 > 0 → Không throw
        var company = InvoiceTestFactory.CreateActiveCompany(
            maxInvoices:  100,
            usedInvoices: 50);
        company.ExtraInvoicesBalance = 10; // Add-on còn quota

        var (_, service) = await CreateServiceWithCompany(company);

        // Act & Assert: Không ném exception (add-on được tiêu thụ)
        await service.Invoking(s => s.ValidateAndConsumeInvoiceQuotaAsync(company.CompanyId))
            .Should().NotThrowAsync(
                "Company còn add-on quota nên không bị từ chối");
    }

    // ═══════════════════════════════════════════════════════════════
    //  ValidateUserQuotaAsync
    //  Logic: Kiểm tra giới hạn số lượng người dùng
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Category", "Quota")]
    public async Task ValidateUserQuotaAsync_WhenUserLimitReached_ThrowsInvalidOperation()
    {
        // Arrange: Company đang dùng đủ 5/5 user slot
        var company = InvoiceTestFactory.CreateActiveCompany(
            maxUsers:     5,
            currentUsers: 5); // Đã đầy

        var (_, service) = await CreateServiceWithCompany(company);

        // Act & Assert
        await service.Invoking(s => s.ValidateUserQuotaAsync(company.CompanyId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Không thể tạo thêm người dùng*");
    }

    [Fact]
    [Trait("Category", "Quota")]
    public async Task ValidateUserQuotaAsync_WhenUserSlotAvailable_DoesNotThrow()
    {
        // Arrange: Company còn chỗ (3/5)
        var company = InvoiceTestFactory.CreateActiveCompany(
            maxUsers:     5,
            currentUsers: 3);

        var (_, service) = await CreateServiceWithCompany(company);

        // Act & Assert
        await service.Invoking(s => s.ValidateUserQuotaAsync(company.CompanyId))
            .Should().NotThrowAsync();
    }

    // ═══════════════════════════════════════════════════════════════
    //  IncreaseUserCountAsync / DecreaseUserCountAsync
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Category", "Quota")]
    public async Task IncreaseUserCountAsync_WhenCalled_IncrementsCurrentActiveUsers()
    {
        // Arrange
        var company = InvoiceTestFactory.CreateActiveCompany(currentUsers: 2);
        var (db, service) = await CreateServiceWithCompany(company);

        // Act
        await service.IncreaseUserCountAsync(company.CompanyId);

        // Assert: Đọc lại từ DB để verify
        var updated = await db.Companies.FindAsync(company.CompanyId);
        updated!.CurrentActiveUsers.Should().Be(3,
            "IncreaseUserCount phải tăng CurrentActiveUsers thêm 1");
    }

    [Fact]
    [Trait("Category", "Quota")]
    public async Task DecreaseUserCountAsync_WhenCalled_DecrementsCurrentActiveUsers()
    {
        // Arrange
        var company = InvoiceTestFactory.CreateActiveCompany(currentUsers: 3);
        var (db, service) = await CreateServiceWithCompany(company);

        // Act
        await service.DecreaseUserCountAsync(company.CompanyId);

        // Assert
        var updated = await db.Companies.FindAsync(company.CompanyId);
        updated!.CurrentActiveUsers.Should().Be(2);
    }

    [Fact]
    [Trait("Category", "Quota")]
    public async Task DecreaseUserCountAsync_WhenCurrentUsersIsZero_DoesNotGoNegative()
    {
        // Arrange: Edge case — user count đang = 0
        var company = InvoiceTestFactory.CreateActiveCompany(currentUsers: 0);
        var (db, service) = await CreateServiceWithCompany(company);

        // Act
        await service.DecreaseUserCountAsync(company.CompanyId);

        // Assert: Không được phép âm
        var updated = await db.Companies.FindAsync(company.CompanyId);
        updated!.CurrentActiveUsers.Should().Be(0,
            "CurrentActiveUsers không được phép âm");
    }

    // ═══════════════════════════════════════════════════════════════
    //  ValidateStorageQuotaAsync
    //  Logic: Kiểm tra dung lượng lưu trữ
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Category", "Quota")]
    public async Task ValidateStorageQuotaAsync_WhenUploadWouldExceedLimit_ThrowsInvalidOperation()
    {
        // Arrange: Quota = 5GB, đã dùng = 4.9GB, file mới = 200MB → vượt
        const long quotaGB           = 5;
        const long usedBytes         = 4_900_000_000L;  // 4.9 GB
        const long newFileSizeBytes  = 200_000_000L;    // 200 MB

        var company = InvoiceTestFactory.CreateActiveCompany(
            storageQuotaGB:   quotaGB,
            usedStorageBytes: usedBytes);

        var (_, service) = await CreateServiceWithCompany(company);

        // Act & Assert
        await service.Invoking(s => s.ValidateStorageQuotaAsync(company.CompanyId, newFileSizeBytes))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*dung lượng lưu trữ*");
    }

    [Fact]
    [Trait("Category", "Quota")]
    public async Task ValidateStorageQuotaAsync_WhenStorageWithinLimit_DoesNotThrow()
    {
        // Arrange: Quota 5GB, đã dùng 1GB, file mới 100MB → OK
        const long quotaGB           = 5;
        const long usedBytes         = 1_000_000_000L;  // 1 GB
        const long newFileSizeBytes  = 100_000_000L;    // 100 MB

        var company = InvoiceTestFactory.CreateActiveCompany(
            storageQuotaGB:   quotaGB,
            usedStorageBytes: usedBytes);

        var (_, service) = await CreateServiceWithCompany(company);

        // Act & Assert
        await service.Invoking(s => s.ValidateStorageQuotaAsync(company.CompanyId, newFileSizeBytes))
            .Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Category", "Quota")]
    public async Task ValidateStorageQuotaAsync_WhenFileExactlyFillsQuota_DoesNotThrow()
    {
        // Arrange: Edge case — file vừa khít hết quota
        const long quotaGB           = 5;
        const long quotaBytes        = quotaGB * 1_000_000_000L; // 5 GB
        const long usedBytes         = 4_500_000_000L;           // 4.5 GB
        const long newFileSizeBytes  = quotaBytes - usedBytes;    // 500 MB → vừa khít

        var company = InvoiceTestFactory.CreateActiveCompany(
            storageQuotaGB:   quotaGB,
            usedStorageBytes: usedBytes);

        var (_, service) = await CreateServiceWithCompany(company);

        // Act & Assert: Vừa đủ → không lỗi
        await service.Invoking(s => s.ValidateStorageQuotaAsync(company.CompanyId, newFileSizeBytes))
            .Should().NotThrowAsync();
    }

    // ═══════════════════════════════════════════════════════════════
    //  ValidateAndConsumeInvoiceQuotaAsync — Subscription Expiry Downgrade
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Category", "Quota")]
    public async Task ValidateAndConsumeInvoiceQuotaAsync_WhenSubscriptionExpiredAndUserOverLimit_ThrowsInvalidOperation()
    {
        // Arrange: Subscription đã hết hạn, company có 5 users nhưng sẽ bị downgrade về FREE (MaxUsers=1)
        var db = InMemoryDbHelper.CreateInMemoryDbContext() as TestAppDbContext
                 ?? throw new InvalidOperationException("Expected TestAppDbContext");

        var freePackage = new SubscriptionPackage
        {
            PackageId   = Guid.NewGuid(),
            PackageCode = "FREE",
            PackageName = "Free",
            MaxUsers    = 1,    // MaxUsers = 1
            MaxInvoicesPerMonth = 30,
            StorageQuotaGB      = 1,
            IsActive            = true,
            HasAdvancedWorkflow = false,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow
        };
        db.SubscriptionPackages.Add(freePackage);

        var company = InvoiceTestFactory.CreateActiveCompany(
            maxUsers:     5,
            currentUsers: 5,
            maxInvoices:  100,
            usedInvoices: 50);
        company.SubscriptionExpiredAt  = DateTime.UtcNow.AddDays(-10); // Đã hết hạn
        company.SubscriptionPackageId  = Guid.NewGuid(); // Đang dùng gói khác (không phải FREE)
        company.ExtraInvoicesBalance   = 0;

        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var service = new QuotaService(db);

        // Act & Assert: Sau downgrade về FREE: CurrentActiveUsers(5) > MaxUsers(1) → throw
        await service.Invoking(s => s.ValidateAndConsumeInvoiceQuotaAsync(company.CompanyId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*vượt quá giới hạn*");
    }
}
