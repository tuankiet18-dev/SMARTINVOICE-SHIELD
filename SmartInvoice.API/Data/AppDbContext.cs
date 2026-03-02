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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // =================================================================================
        // 2. RELATIONSHIPS & CONSTRAINTS
        // =================================================================================

        // Companies
        modelBuilder.Entity<Company>()
            .HasIndex(c => c.TaxCode)
            .IsUnique();

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
    }
}
