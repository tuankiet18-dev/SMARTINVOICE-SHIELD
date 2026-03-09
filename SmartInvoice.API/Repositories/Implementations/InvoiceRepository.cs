using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartInvoice.API.Data;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Repositories.Interfaces;

namespace SmartInvoice.API.Repositories.Implementations
{
    public class InvoiceRepository : BaseRepository<Invoice>, IInvoiceRepository
    {
        public InvoiceRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<Invoice?> GetInvoiceWithDetailsAsync(Guid id)
        {
            return await _context.Invoices
                .Include(i => i.Company)
                .Include(i => i.DocumentType)
                .Include(i => i.ValidationLayers)
                .Include(i => i.RiskCheckResults)
                .Include(i => i.AuditLogs)
                    .ThenInclude(a => a.User)
                .Include(i => i.InvoiceLineItems)
                .Include(i => i.OriginalFile)
                .Include(i => i.Uploader)
                .Include(i => i.Submitter)
                .Include(i => i.Approver)
                .Include(i => i.Rejector)
                .FirstOrDefaultAsync(i => i.InvoiceId == id && !i.IsDeleted);
        }

        public async Task<bool> ExistsByDetailsAsync(string sellerTaxCode, string serialNumber, string invoiceNumber, Guid companyId)
        {
            return await _context.Invoices.AnyAsync(i =>
                i.CompanyId == companyId &&
                i.SellerTaxCode == sellerTaxCode &&
                i.SerialNumber == serialNumber &&
                i.InvoiceNumber == invoiceNumber &&
                !i.IsDeleted);
        }

        public async Task<Invoice?> GetExistingInvoiceAsync(string sellerTaxCode, string serialNumber, string invoiceNumber, Guid companyId)
        {
            return await _context.Invoices
                .Where(i =>
                    i.CompanyId == companyId &&
                    i.SellerTaxCode == sellerTaxCode &&
                    i.SerialNumber == serialNumber &&
                    i.InvoiceNumber == invoiceNumber &&
                    !i.IsDeleted)
                .OrderByDescending(i => i.Version)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> ExistsByNumberAsync(string invoiceNumber, Guid companyId)
        {
            return await _context.Invoices.AnyAsync(i => i.InvoiceNumber == invoiceNumber && i.CompanyId == companyId);
        }

        public async Task<(IEnumerable<Invoice> Items, int TotalCount)> GetPagedInvoicesAsync(DTOs.Invoice.GetInvoicesQueryDto request, Guid companyId, Guid userId, string userRole)
        {
            var query = _context.Invoices.AsQueryable();

            // 1. Tenant Isolation: MUST be restricted to CompanyId
            query = query.Where(i => i.CompanyId == companyId && !i.IsDeleted);

            // 2. Role-Based Access Control (RBAC)
            if (userRole == "Member")
            {
                // Member can only see their own uploaded invoices
                query = query.Where(i => i.UploadedBy == userId);
            }
            // CompanyAdmin and SuperAdmin can see all invoices in the company

            // 3. Apply Filters
            if (!string.IsNullOrEmpty(request.Keyword))
            {
                var keyword = request.Keyword.ToLower();
                query = query.Where(i =>
                    (i.InvoiceNumber != null && i.InvoiceNumber.ToLower().Contains(keyword)) ||
                    (i.SerialNumber != null && i.SerialNumber.ToLower().Contains(keyword)) ||
                    (i.SellerName != null && i.SellerName.ToLower().Contains(keyword)) ||
                    (i.SellerTaxCode != null && i.SellerTaxCode.ToLower().Contains(keyword))
                );
            }

            if (!string.IsNullOrEmpty(request.Status))
            {
                query = query.Where(i => i.Status == request.Status);
            }

            if (!string.IsNullOrEmpty(request.RiskLevel))
            {
                query = query.Where(i => i.RiskLevel == request.RiskLevel);
            }

            if (request.FromDate.HasValue)
            {
                // Set the time part to start of day for inclusive filtering
                var fromDate = request.FromDate.Value.Date;
                query = query.Where(i => i.InvoiceDate >= fromDate);
            }

            if (request.ToDate.HasValue)
            {
                // Include the entire toDate
                var toDate = request.ToDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(i => i.InvoiceDate <= toDate);
            }

            // 4. Count total records BEFORE pagination
            var totalCount = await query.CountAsync();

            // 5. Sort & Pagination
            var items = await query
                .Include(i => i.Uploader)
                .OrderByDescending(i => i.InvoiceDate)
                .Skip((request.Page - 1) * request.Size)
                .Take(request.Size)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}
