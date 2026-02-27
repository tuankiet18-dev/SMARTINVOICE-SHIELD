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
        Task<int> CompleteAsync();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}
