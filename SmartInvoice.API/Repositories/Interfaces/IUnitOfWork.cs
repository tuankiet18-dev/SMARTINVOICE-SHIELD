using System;
using System.Threading.Tasks;

namespace SmartInvoice.API.Repositories.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IInvoiceRepository Invoices { get; }
        IInvoiceAuditLogRepository InvoiceAuditLogs { get; }
        IInvoiceCheckResultRepository InvoiceCheckResults { get; }
        IUserRepository Users { get; }
        ICompanyRepository Companies { get; }
        ILocalBlacklistRepository LocalBlacklists { get; }
        IAIProcessingLogRepository AIProcessingLogs { get; }
        IDocumentTypeRepository DocumentTypes { get; }
        IExportHistoryRepository ExportHistories { get; }
        IExportConfigRepository ExportConfigs { get; }
        IFileStorageRepository FileStorages { get; }
        INotificationRepository Notifications { get; }
        ISystemConfigurationRepository SystemConfigurations { get; }
        Task<int> CompleteAsync();
        Task<int> ExecuteSqlAsync(string sql);
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}
