using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartInvoice.API.Data;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Entities.JsonModels;

namespace SmartInvoice.Tests.Helpers;

/// <summary>
/// DbContext kế thừa AppDbContext, override OnModelCreating để loại bỏ
/// các entity/property không tương thích với InMemory provider.
/// </summary>
public class TestAppDbContext : AppDbContext
{
    public TestAppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // KHÔNG gọi base.OnModelCreating(modelBuilder) để tránh EF scan các entity/property lỗi map

        modelBuilder.Entity<Company>().HasKey(c => c.CompanyId);
        modelBuilder.Entity<SubscriptionPackage>().HasKey(p => p.PackageId);

        // Danh sách tất cả các entity cần skip để tránh lỗi mapping 'object' (AuditChange) hoặc JSONB
        modelBuilder.Ignore<InvoiceAuditLog>();
        modelBuilder.Ignore<AuditChange>();
        modelBuilder.Ignore<Invoice>();
        modelBuilder.Ignore<User>();
        modelBuilder.Ignore<FileStorage>();
        modelBuilder.Ignore<InvoiceCheckResult>();
        modelBuilder.Ignore<Notification>();
        modelBuilder.Ignore<ExportHistory>();
        modelBuilder.Ignore<ExportConfig>();
        modelBuilder.Ignore<AIProcessingLog>();
        modelBuilder.Ignore<SystemConfiguration>();
        modelBuilder.Ignore<PaymentTransaction>();
        modelBuilder.Ignore<LocalBlacklistedCompany>();
        modelBuilder.Ignore<DocumentType>();
    }
}

/// <summary>
/// Tạo DbContext với InMemory database mỗi lần gọi.
/// </summary>
public static class InMemoryDbHelper
{
    public static AppDbContext CreateInMemoryDbContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString())
            .EnableServiceProviderCaching(false)
            .Options;

        return new TestAppDbContext(options);
    }
}
