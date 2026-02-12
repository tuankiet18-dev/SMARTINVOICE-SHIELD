using Microsoft.EntityFrameworkCore;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Entities.JsonModels;

namespace SmartInvoice.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Company> Companies { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<DocumentType> DocumentTypes { get; set; }
    public DbSet<FileStorage> FileStorages { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<ValidationLayer> ValidationLayers { get; set; }
    public DbSet<InvoiceAuditLog> InvoiceAuditLogs { get; set; }
    public DbSet<RiskCheckResult> RiskCheckResults { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<ExportHistory> ExportHistories { get; set; }
    public DbSet<AIProcessingLog> AIProcessingLogs { get; set; } // Renamed from AIProcessingLog
    public DbSet<SystemConfiguration> SystemConfigurations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // =================================================================================
        // 1. JSONB CONFIGURATION
        // =================================================================================

        /*
        // User
        // User Permissions - Handled by [Column(TypeName="jsonb")] in Entity
        // modelBuilder.Entity<User>().OwnsMany(u => u.Permissions, builder => builder.ToJson());

        // DocumentType
        modelBuilder.Entity<DocumentType>().OwnsOne(d => d.ValidationRules, builder => builder.ToJson());
        modelBuilder.Entity<DocumentType>().OwnsOne(d => d.ProcessingConfig, builder => builder.ToJson());

        // Invoice
        modelBuilder.Entity<Invoice>().OwnsOne(i => i.RawData, builder => builder.ToJson());
        modelBuilder.Entity<Invoice>().OwnsOne(i => i.ExtractedData, builder => 
        { 
            builder.ToJson(); 
            // Explicitly map nested collection inside JSONB
            builder.OwnsMany(e => e.LineItems);
        });
        modelBuilder.Entity<Invoice>().OwnsOne(i => i.ValidationResult, builder => 
        { 
            builder.ToJson();
            builder.OwnsMany(v => v.Errors); // RiskReason list
            builder.OwnsMany(v => v.Warnings);
        });
        modelBuilder.Entity<Invoice>().OwnsMany(i => i.RiskReasons, builder => builder.ToJson());

        // ValidationLayer - Manually mapped mainly because strict schema is complex here
        // But let's try to map what we can if useful. For now, keep as string/jsonb for flexibility
        // unless we have specific classes for each layer type.

        // InvoiceAuditLog
        modelBuilder.Entity<InvoiceAuditLog>().OwnsMany(l => l.Changes, builder => builder.ToJson());
        */

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

        // Global Query Filter for User (Soft Delete)
        modelBuilder.Entity<User>().HasQueryFilter(u => u.IsActive);

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