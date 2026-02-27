using System;
using System.Threading.Tasks;
using SmartInvoice.API.Entities;

namespace SmartInvoice.API.Repositories.Interfaces
{
    public interface IInvoiceRepository : IGenericRepository<Invoice>
    {
        Task<Invoice?> GetInvoiceWithDetailsAsync(Guid id);
        Task<bool> ExistsByNumberAsync(string invoiceNumber, Guid companyId);
        Task<bool> ExistsByDetailsAsync(string sellerTaxCode, string serialNumber, string invoiceNumber);
    }
}
