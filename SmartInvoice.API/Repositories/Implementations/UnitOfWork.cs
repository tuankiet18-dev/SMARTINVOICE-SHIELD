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

        public UnitOfWork(AppDbContext context)
        {
            _context = context;
            Invoices = new InvoiceRepository(_context);
            Users = new UserRepository(_context);
            Companies = new CompanyRepository(_context);
            LocalBlacklists = new LocalBlacklistRepository(_context);
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
