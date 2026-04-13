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
                .IgnoreQueryFilters()
                .Include(i => i.Company)
                .Include(i => i.DocumentType)
                .Include(i => i.CheckResults) // Replaced ValidationLayers and RiskCheckResults
                .Include(i => i.AuditLogs)
                    .ThenInclude(a => a.User)
                .Include(i => i.OriginalFile)
                .Include(i => i.Workflow.Submitter)
                .Include(i => i.Workflow.Approver)
                .Include(i => i.Workflow.Rejector)
                .FirstOrDefaultAsync(i => i.InvoiceId == id);
        }

        public async Task<bool> ExistsByDetailsAsync(string sellerTaxCode, string serialNumber, string invoiceNumber, Guid companyId)
        {
            return await _context.Invoices.AnyAsync(i =>
                i.CompanyId == companyId &&
                i.Seller.TaxCode == sellerTaxCode &&
                i.SerialNumber == serialNumber &&
                i.InvoiceNumber == invoiceNumber &&
                !i.IsDeleted);
        }

        public async Task<Invoice?> GetExistingInvoiceAsync(string sellerTaxCode, string serialNumber, string invoiceNumber, Guid companyId)
        {
            string cleanNum = invoiceNumber?.TrimStart('0') ?? "";
            if (string.IsNullOrEmpty(cleanNum)) cleanNum = "0";
            string cleanNumPadded = cleanNum.PadLeft(8, '0');

            string cleanSymbol = serialNumber?.Trim() ?? "";
            if (cleanSymbol.Length == 7 && char.IsDigit(cleanSymbol[0]))
            {
                cleanSymbol = cleanSymbol.Substring(1);
            }
            string fullSymbol1 = "1" + cleanSymbol;
            string fullSymbol2 = "2" + cleanSymbol;

            return await _context.Invoices
                .Where(i =>
                    i.CompanyId == companyId &&
                    i.Seller.TaxCode == sellerTaxCode &&
                    (i.InvoiceNumber == cleanNum || i.InvoiceNumber == cleanNumPadded || i.InvoiceNumber == invoiceNumber) &&
                    (i.SerialNumber == cleanSymbol || i.SerialNumber == fullSymbol1 || i.SerialNumber == fullSymbol2 || i.SerialNumber == serialNumber) &&
                    !i.IsDeleted)
                .OrderByDescending(i => i.Version)
                .FirstOrDefaultAsync();
        }

        public async Task<Invoice?> GetByIdIncludeDeletedAsync(Guid id)
        {
            return await _context.Invoices
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(i => i.InvoiceId == id);
        }

        public async Task<bool> ExistsByNumberAsync(string invoiceNumber, Guid companyId)
        {
            return await _context.Invoices.AnyAsync(i => i.InvoiceNumber == invoiceNumber && i.CompanyId == companyId);
        }

        public async Task<(IEnumerable<Invoice> Items, int TotalCount)> GetPagedInvoicesAsync(DTOs.Invoice.GetInvoicesQueryDto request, Guid companyId, Guid userId, string userRole)
        {
            var query = _context.Invoices.AsQueryable();

            // 1. Tenant Isolation: MUST be restricted to CompanyId and NOT show replaced versions
            query = query.Where(i => i.CompanyId == companyId && !i.IsDeleted && !i.IsReplaced);

            // 2. Role-Based Access Control (RBAC)
            if (userRole == "Accountant")
            {
                // Member can only see their own uploaded invoices
                query = query.Where(i => i.Workflow.UploadedBy == userId);
            }
            // CompanyAdmin and SuperAdmin can see all invoices in the company

            // 3. Apply Filters
            if (!string.IsNullOrEmpty(request.Keyword))
            {
                var keyword = request.Keyword.ToLower();
                query = query.Where(i =>
                    (i.InvoiceNumber != null && i.InvoiceNumber.ToLower().Contains(keyword)) ||
                    (i.SerialNumber != null && i.SerialNumber.ToLower().Contains(keyword)) ||
                    (i.Seller.Name != null && i.Seller.Name.ToLower().Contains(keyword))
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
                .Include(i => i.Workflow.Uploader)
                .OrderByDescending(i => i.InvoiceDate)
                .Skip((request.Page - 1) * request.Size)
                .Take(request.Size)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<Invoice?> GetTrashInvoiceWithDetailsAsync(Guid id)
        {
            return await _context.Invoices
                .IgnoreQueryFilters()
                .Include(i => i.Workflow)
                .Include(i => i.OriginalFile)
                .Include(i => i.VisualFile)
                .FirstOrDefaultAsync(i => i.InvoiceId == id && i.IsDeleted);
        }

        public async Task<(IEnumerable<Invoice> Items, int TotalCount)> GetPagedTrashInvoicesAsync(DTOs.Invoice.GetInvoicesQueryDto request, Guid companyId, Guid userId, string userRole)
        {
            var query = _context.Invoices.IgnoreQueryFilters().AsQueryable();

            query = query.Where(i => i.CompanyId == companyId && i.IsDeleted);

            if (userRole == "Accountant")
            {
                query = query.Where(i => i.Workflow.UploadedBy == userId);
            }

            if (!string.IsNullOrEmpty(request.Keyword))
            {
                var keyword = request.Keyword.ToLower();
                query = query.Where(i =>
                    (i.InvoiceNumber != null && i.InvoiceNumber.ToLower().Contains(keyword)) ||
                    (i.Seller.Name != null && i.Seller.Name.ToLower().Contains(keyword)));
            }

            var totalCount = await query.CountAsync();

            query = query.OrderByDescending(i => i.CreatedAt);

            var items = await query
                .Skip((request.Page - 1) * request.Size)
                .Take(request.Size)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}