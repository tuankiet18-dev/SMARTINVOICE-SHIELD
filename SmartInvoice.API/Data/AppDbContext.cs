using Microsoft.EntityFrameworkCore;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Entities.JsonModels;

namespace SmartInvoice.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<ISoftDelete>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = DateTime.UtcNow;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }

    public DbSet<Company> Companies { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<DocumentType> DocumentTypes { get; set; }
    public DbSet<FileStorage> FileStorages { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<SmartInvoice.API.Entities.InvoiceLineItem> InvoiceLineItems { get; set; } // NEW
    public DbSet<LocalBlacklistedCompany> LocalBlacklists { get; set; } // NEW
    public DbSet<ValidationLayer> ValidationLayers { get; set; }
    public DbSet<InvoiceAuditLog> InvoiceAuditLogs { get; set; }
    public DbSet<RiskCheckResult> RiskCheckResults { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<ExportHistory> ExportHistories { get; set; }
    public DbSet<AIProcessingLog> AIProcessingLogs { get; set; }
    public DbSet<SystemConfiguration> SystemConfigurations { get; set; }
    public DbSet<SubscriptionPackage> SubscriptionPackages { get; set; }
    public DbSet<PaymentTransaction> PaymentTransactions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // =================================================================================
        // 2. RELATIONSHIPS & CONSTRAINTS
        // =================================================================================

        // SubscriptionPackages
        modelBuilder.Entity<SubscriptionPackage>()
            .HasIndex(sp => sp.PackageCode)
            .IsUnique();

        // PaymentTransactions
        modelBuilder.Entity<PaymentTransaction>()
            .HasOne(pt => pt.Company)
            .WithMany()
            .HasForeignKey(pt => pt.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PaymentTransaction>()
            .HasOne(pt => pt.Package)
            .WithMany()
            .HasForeignKey(pt => pt.PackageId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PaymentTransaction>()
            .HasIndex(pt => pt.VnpTxnRef)
            .IsUnique();

        modelBuilder.Entity<PaymentTransaction>()
            .HasIndex(pt => new { pt.CompanyId, pt.CreatedAt });

        // Companies
        modelBuilder.Entity<Company>()
            .HasIndex(c => c.TaxCode)
            .IsUnique();

        modelBuilder.Entity<Company>()
            .HasOne(c => c.SubscriptionPackage)
            .WithMany()
            .HasForeignKey(c => c.SubscriptionPackageId)
            .OnDelete(DeleteBehavior.SetNull);

        // Users
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasOne(u => u.Company)
            .WithMany()
            .HasForeignKey(u => u.CompanyId)
            .OnDelete(DeleteBehavior.Cascade); // Delete company -> delete users

        // Invoices
        modelBuilder.Entity<Invoice>()
            .HasOne(i => i.Company)
            .WithMany()
            .HasForeignKey(i => i.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Invoice>()
            .HasOne(i => i.OriginalFile)
            .WithMany()
            .HasForeignKey(i => i.OriginalFileId)
            .OnDelete(DeleteBehavior.Restrict); // Don't delete invoice if file is deleted (keep record)

        // InvoiceLineItems (NEW)
        modelBuilder.Entity<SmartInvoice.API.Entities.InvoiceLineItem>()
            .HasOne(li => li.Invoice)
            .WithMany(i => i.InvoiceLineItems)
            .HasForeignKey(li => li.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade); // Delete invoice -> delete line items

        // LocalBlacklistedCompany (NEW)
        modelBuilder.Entity<LocalBlacklistedCompany>()
            .HasIndex(b => b.TaxCode)
            .IsUnique();

        // ValidationLayers
        modelBuilder.Entity<ValidationLayer>()
            .HasOne(vl => vl.Invoice)
            .WithMany(i => i.ValidationLayers)
            .HasForeignKey(vl => vl.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // AuditLogs
        modelBuilder.Entity<InvoiceAuditLog>()
            .HasOne(al => al.Invoice)
            .WithMany(i => i.AuditLogs)
            .HasForeignKey(al => al.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // =================================================================================
        // 3. INDEXES
        // =================================================================================

        // Invoices
        modelBuilder.Entity<Invoice>()
            .HasIndex(i => new { i.CompanyId, i.InvoiceDate }); // Descending typically handled by DB, EF default is ASC

        modelBuilder.Entity<Invoice>()
            .HasIndex(i => new { i.CompanyId, i.Status });

        modelBuilder.Entity<Invoice>()
            .HasIndex(i => new { i.CompanyId, i.CreatedAt });

        modelBuilder.Entity<Invoice>()
            .HasIndex(i => new { i.CompanyId, i.RiskLevel });

        modelBuilder.Entity<Invoice>()
            .HasIndex(i => i.InvoiceNumber);

        modelBuilder.Entity<Invoice>()
            .HasIndex(i => i.SellerTaxCode);

        // FileStorages
        modelBuilder.Entity<FileStorage>()
            .HasIndex(f => f.S3Key)
            .IsUnique();

        // =================================================================================
        // 4. CHECK CONSTRAINTS (PostgreSQL)
        // =================================================================================

        // Note: EF Core supports .HasCheckConstraint for SQL generation
        modelBuilder.Entity<Company>()
            .ToTable(t => t.HasCheckConstraint("CHK_Companies_SubscriptionDates",
                "\"SubscriptionExpiredAt\" IS NULL OR \"SubscriptionExpiredAt\" > \"SubscriptionStartDate\""));

        modelBuilder.Entity<Invoice>()
            .ToTable(t => t.HasCheckConstraint("CHK_Invoices_Amounts",
                "\"TotalAmount\" >= 0"));

        // Global Query Filters (Soft Delete)
        modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
        modelBuilder.Entity<Company>().HasQueryFilter(c => !c.IsDeleted);
        modelBuilder.Entity<Invoice>().HasQueryFilter(i => !i.IsDeleted);
        modelBuilder.Entity<FileStorage>().HasQueryFilter(f => !f.IsDeleted);

        // Global Query Filter for LocalBlacklist
        modelBuilder.Entity<LocalBlacklistedCompany>().HasQueryFilter(b => b.IsActive);

        // =================================================================================
        // 5. VALUE CONVERTERS
        // =================================================================================

        // Fix for Npgsql Jsonb List<string> mapping issue
        modelBuilder.Entity<User>()
            .Property(u => u.Permissions)
            .HasColumnType("jsonb")
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>(),
                new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
                    (c1, c2) => System.Text.Json.JsonSerializer.Serialize(c1, (System.Text.Json.JsonSerializerOptions?)null) == System.Text.Json.JsonSerializer.Serialize(c2, (System.Text.Json.JsonSerializerOptions?)null),
                    c => c == null ? 0 : System.Text.Json.JsonSerializer.Serialize(c, (System.Text.Json.JsonSerializerOptions?)null).GetHashCode(),
                    c => System.Text.Json.JsonSerializer.Deserialize<List<string>>(System.Text.Json.JsonSerializer.Serialize(c, (System.Text.Json.JsonSerializerOptions?)null), (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>()
                )
            );

        // Npgsql native JSON serialization handles mapping POCOs to jsonb 
        // as long as NpgsqlDataSourceBuilder.EnableDynamicJson() is called.

        // =================================================================================
        // 6. SEED DATA
        // =================================================================================

        modelBuilder.Entity<SubscriptionPackage>().HasData(
            new SubscriptionPackage
            {
                PackageId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                PackageCode = "FREE",
                PackageName = "Gói Dùng Thử (Free)",
                PackageLevel = 1,
                Description = "Trải nghiệm sức mạnh xử lý hóa đơn bằng AI dành cho cá nhân hoặc doanh nghiệp mới thành lập.",
                PricePerMonth = 0m,
                PricePerSixMonths = 0m,
                PricePerYear = 0m,
                MaxUsers = 1,
                MaxInvoicesPerMonth = 30,
                StorageQuotaGB = 1,
                HasAiProcessing = true,
                HasAdvancedWorkflow = false,
                HasRiskWarning = false,
                HasAuditLog = false,
                HasErpIntegration = false,
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new SubscriptionPackage
            {
                PackageId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                PackageCode = "STARTER",
                PackageName = "Gói Khởi Nghiệp (Starter)",
                PackageLevel = 2,
                Description = "Giải pháp tối ưu cho doanh nghiệp siêu nhỏ, đáp ứng nhu cầu xử lý hóa đơn tự động cơ bản.",
                PricePerMonth = 199000m,
                PricePerSixMonths = 995000m,
                PricePerYear = 1990000m,
                MaxUsers = 5,
                MaxInvoicesPerMonth = 200,
                StorageQuotaGB = 5,
                HasAiProcessing = true,
                HasAdvancedWorkflow = false,
                HasRiskWarning = false,
                HasAuditLog = false,
                HasErpIntegration = false,
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new SubscriptionPackage
            {
                PackageId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                PackageCode = "PRO",
                PackageName = "Gói Chuyên Nghiệp (Professional)",
                PackageLevel = 3,
                Description = "Quản trị rủi ro toàn diện và tự động hóa quy trình phê duyệt cho doanh nghiệp vừa và nhỏ (SME).",
                PricePerMonth = 599000m,
                PricePerSixMonths = 2995000m,
                PricePerYear = 5990000m,
                MaxUsers = 15,
                MaxInvoicesPerMonth = 1000,
                StorageQuotaGB = 20,
                HasAiProcessing = true,
                HasAdvancedWorkflow = true,
                HasRiskWarning = true,
                HasAuditLog = true,
                HasErpIntegration = false,
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new SubscriptionPackage
            {
                PackageId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                PackageCode = "ENTERPRISE",
                PackageName = "Gói Doanh Nghiệp (Enterprise)",
                PackageLevel = 4,
                Description = "Giải pháp tùy biến chuyên sâu, tích hợp API trực tiếp vào hệ thống ERP của tập đoàn.",
                PricePerMonth = 1999000m,
                PricePerSixMonths = 9995000m,
                PricePerYear = 19990000m,
                MaxUsers = 999,
                MaxInvoicesPerMonth = 99999,
                StorageQuotaGB = 100,
                HasAiProcessing = true,
                HasAdvancedWorkflow = true,
                HasRiskWarning = true,
                HasAuditLog = true,
                HasErpIntegration = true,
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
