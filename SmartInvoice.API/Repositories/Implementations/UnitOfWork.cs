using System;
using System.Threading.Tasks;
using SmartInvoice.API.Data;
using SmartInvoice.API.Repositories.Interfaces;

namespace SmartInvoice.API.Repositories.Implementations
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;

        public IInvoiceRepository Invoices { get; private set; }
        public IUserRepository Users { get; private set; }
        public ICompanyRepository Companies { get; private set; }
        public ILocalBlacklistRepository LocalBlacklists { get; private set; }
        public IAIProcessingLogRepository AIProcessingLogs { get; private set; }
        public IDocumentTypeRepository DocumentTypes { get; private set; }
        public IExportHistoryRepository ExportHistories { get; private set; }
        public IFileStorageRepository FileStorages { get; private set; }
        public IInvoiceAuditLogRepository InvoiceAuditLogs { get; private set; }
        public IInvoiceLineItemRepository InvoiceLineItems { get; private set; }
        public INotificationRepository Notifications { get; private set; }
        public IRiskCheckResultRepository RiskCheckResults { get; private set; }
        public ISystemConfigurationRepository SystemConfigurations { get; private set; }
        public IValidationLayerRepository ValidationLayers { get; private set; }

        public UnitOfWork(AppDbContext context)
        {
            _context = context;
            Invoices = new InvoiceRepository(_context);
            Users = new UserRepository(_context);
            Companies = new CompanyRepository(_context);
            LocalBlacklists = new LocalBlacklistRepository(_context);
            AIProcessingLogs = new AIProcessingLogRepository(_context);
            DocumentTypes = new DocumentTypeRepository(_context);
            ExportHistories = new ExportHistoryRepository(_context);
            FileStorages = new FileStorageRepository(_context);
            InvoiceAuditLogs = new InvoiceAuditLogRepository(_context);
            InvoiceLineItems = new InvoiceLineItemRepository(_context);
            Notifications = new NotificationRepository(_context);
            RiskCheckResults = new RiskCheckResultRepository(_context);
            SystemConfigurations = new SystemConfigurationRepository(_context);
            ValidationLayers = new ValidationLayerRepository(_context);
        }

        private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? _currentTransaction;

        public async Task<int> CompleteAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public async Task BeginTransactionAsync()
        {
            if (_currentTransaction != null) return;
            _currentTransaction = await _context.Database.BeginTransactionAsync();
        }

        public async Task CommitTransactionAsync()
        {
            try
            {
                if (_currentTransaction != null)
                {
                    await _currentTransaction.CommitAsync();
                }
            }
            catch
            {
                await RollbackTransactionAsync();
                throw;
            }
            finally
            {
                if (_currentTransaction != null)
                {
                    await _currentTransaction.DisposeAsync();
                    _currentTransaction = null;
                }
            }
        }

        public async Task RollbackTransactionAsync()
        {
            try
            {
                if (_currentTransaction != null)
                {
                    await _currentTransaction.RollbackAsync();
                }
            }
            finally
            {
                if (_currentTransaction != null)
                {
                    await _currentTransaction.DisposeAsync();
                    _currentTransaction = null;
                }
            }
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
