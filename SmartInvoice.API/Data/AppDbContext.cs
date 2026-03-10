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
    public DbSet<SmartInvoice.API.Entities.InvoiceCheckResult> InvoiceCheckResults { get; set; } // REPLACED
    public DbSet<LocalBlacklistedCompany> LocalBlacklists { get; set; }
    public DbSet<InvoiceAuditLog> InvoiceAuditLogs { get; set; }
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

        modelBuilder.Entity<Invoice>(b =>
        {
            b.OwnsOne(i => i.Seller, s =>
            {
                s.Property(p => p.Name).HasColumnName("SellerName");
                s.Property(p => p.TaxCode).HasColumnName("SellerTaxCode");
                s.Property(p => p.Address).HasColumnName("SellerAddress");
                s.Property(p => p.BankAccount).HasColumnName("SellerBankAccount");
                s.Property(p => p.BankName).HasColumnName("SellerBankName");
                s.Property(p => p.Phone).HasColumnName("SellerPhone");
                s.Property(p => p.Email).HasColumnName("SellerEmail");
            });

            b.OwnsOne(i => i.Buyer, buyer =>
            {
                buyer.Property(p => p.Name).HasColumnName("BuyerName");
                buyer.Property(p => p.TaxCode).HasColumnName("BuyerTaxCode");
                buyer.Property(p => p.Address).HasColumnName("BuyerAddress");
                buyer.Property(p => p.Phone).HasColumnName("BuyerPhone");
                buyer.Property(p => p.Email).HasColumnName("BuyerEmail");
                buyer.Property(p => p.ContactPerson).HasColumnName("BuyerContactPerson");
            });

            b.OwnsOne(i => i.Workflow, w =>
            {
                w.Property(p => p.UploadedBy).HasColumnName("UploadedBy");
                w.Property(p => p.SubmittedBy).HasColumnName("SubmittedBy");
                w.Property(p => p.SubmittedAt).HasColumnName("SubmittedAt");
                w.Property(p => p.ApprovedBy).HasColumnName("ApprovedBy");
                w.Property(p => p.ApprovedAt).HasColumnName("ApprovedAt");
                w.Property(p => p.RejectedBy).HasColumnName("RejectedBy");
                w.Property(p => p.RejectedAt).HasColumnName("RejectedAt");
                w.Property(p => p.RejectionReason).HasColumnName("RejectionReason");
            });
        });

        // LocalBlacklistedCompany
        modelBuilder.Entity<LocalBlacklistedCompany>()
            .HasIndex(b => b.TaxCode)
            .IsUnique();

        // InvoiceCheckResults
        modelBuilder.Entity<InvoiceCheckResult>()
            .HasOne(vl => vl.Invoice)
            .WithMany(i => i.CheckResults)
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
        // Covering Index for Dashboard Filtering and Sorting
        modelBuilder.Entity<Invoice>()
            .HasIndex(i => new { i.CompanyId, i.Status, i.RiskLevel, i.InvoiceDate });

        // General chronological search per company
        modelBuilder.Entity<Invoice>()
            .HasIndex(i => new { i.CompanyId, i.CreatedAt });

        // Indexes for Seller search and Unique Invoice Enforcement will be handled manually in the migration
        // due to EF Core limitations with composite indexes on Owned Entities.

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
    }
}
