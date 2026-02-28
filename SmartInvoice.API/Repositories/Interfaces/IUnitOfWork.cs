using System;
using System.Threading.Tasks;

namespace SmartInvoice.API.Repositories.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IInvoiceRepository Invoices { get; }
        IUserRepository Users { get; }
        ICompanyRepository Companies { get; }
        ILocalBlacklistRepository LocalBlacklists { get; }
        IAIProcessingLogRepository AIProcessingLogs { get; }
        IDocumentTypeRepository DocumentTypes { get; }
        IExportHistoryRepository ExportHistories { get; }
        IFileStorageRepository FileStorages { get; }
        IInvoiceAuditLogRepository InvoiceAuditLogs { get; }
        IInvoiceLineItemRepository InvoiceLineItems { get; }
        INotificationRepository Notifications { get; }
        IRiskCheckResultRepository RiskCheckResults { get; }
        ISystemConfigurationRepository SystemConfigurations { get; }
        IValidationLayerRepository ValidationLayers { get; }
        Task<int> CompleteAsync();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}
