using Bogus;
using Microsoft.EntityFrameworkCore;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Data;

namespace SmartInvoice.API.Data.Seeding;

public static class DashboardDataSeeder
{
    public static async Task SeedInvoicesAsync(AppDbContext context, int amountToSeed = 1000)
    {
        // 1. Fetch required linked entities to avoid FK constraint errors
        var companies = await context.Companies.ToListAsync();
        var users = await context.Users.ToListAsync();
        
        // Let's assume you might not have DocumentTypes, fallback to handle it:
        // Adjust if your db has DocumentTypes mapped
        var documentTypesIds = await context.Set<DocumentType>().Select(d => d.DocumentTypeId).ToListAsync();

        if (companies.Count == 0 || users.Count == 0)
        {
            throw new Exception("Please ensure you have at least 1 Company and 1 User in your database before seeding Invoices.");
        }

        // Set Vietnamese locale for realistic names and addresses
        Randomizer.Seed = new Random(8675309);
        var faker = new Faker("vi");

        var statuses = new[] { "Draft", "Pending", "Approved", "Rejected", "Archived" };
        var riskLevels = new[] { "Green", "Yellow", "Orange", "Red" };

        // 2. Setup Bogus generation rules for Owned Entities
        var sellerFaker = new Faker<SellerInfo>("vi")
            .RuleFor(s => s.Name, f => f.Company.CompanyName())
            .RuleFor(s => s.TaxCode, f => f.Random.Replace("##########")) // 10 digit tax code
            .RuleFor(s => s.Address, f => f.Address.FullAddress())
            .RuleFor(s => s.Phone, f => f.Phone.PhoneNumber("09########"))
            .RuleFor(s => s.Email, f => f.Internet.Email())
            .RuleFor(s => s.BankAccount, f => f.Finance.Account())
            .RuleFor(s => s.BankName, f => f.Finance.AccountName());

        var buyerFaker = new Faker<BuyerInfo>("vi")
            .RuleFor(b => b.Name, f => f.Company.CompanyName())
            .RuleFor(b => b.TaxCode, f => f.Random.Replace("##########"))
            .RuleFor(b => b.Address, f => f.Address.FullAddress())
            .RuleFor(b => b.Phone, f => f.Phone.PhoneNumber("09########"))
            .RuleFor(b => b.Email, f => f.Internet.Email())
            .RuleFor(b => b.ContactPerson, f => f.Name.FullName());

        // 3. Setup Bogus rule for the Main Invoice Entity
        var invoiceFaker = new Faker<Invoice>("vi")
            .RuleFor(i => i.InvoiceId, f => Guid.NewGuid())
            .RuleFor(i => i.CompanyId, f => f.PickRandom(companies).CompanyId)
            // Use existing DocumentTypeId or a dummy int if none found
            .RuleFor(i => i.DocumentTypeId, f => documentTypesIds.Any() ? f.PickRandom(documentTypesIds) : 1)
            .RuleFor(i => i.ProcessingMethod, f => f.PickRandom("XML", "OCR", "MANUAL"))
            .RuleFor(i => i.FormNumber, f => f.PickRandom("01GTKT", "02GTTT"))
            .RuleFor(i => i.SerialNumber, f => f.Random.Replace("C##T"))
            .RuleFor(i => i.InvoiceNumber, f => f.Random.Replace("#######"))
            
            // Distribute invoice dates over the last 180 days for metrics
            .RuleFor(i => i.InvoiceDate, f => f.Date.Recent(180).ToUniversalTime())
            
            .RuleFor(i => i.InvoiceCurrency, f => "VND")
            .RuleFor(i => i.ExchangeRate, f => 1m)
            
            .RuleFor(i => i.Seller, f => sellerFaker.Generate())
            .RuleFor(i => i.Buyer, f => buyerFaker.Generate())
            
            // Tax Calculation logic
            .RuleFor(i => i.TotalAmountBeforeTax, f => Math.Round(f.Random.Decimal(1000000m, 50000000m), 2))
            .RuleFor(i => i.TotalTaxAmount, (f, i) => Math.Round(i.TotalAmountBeforeTax.Value * 0.1m, 2)) // 10% VAT
            .RuleFor(i => i.TotalAmount, (f, i) => i.TotalAmountBeforeTax.Value + i.TotalTaxAmount.Value)
            // Can be generated but left simple for demo
            .RuleFor(i => i.TotalAmountInWords, f => "Số tiền viết bằng chữ demo")
            
            .RuleFor(i => i.PaymentMethod, f => f.PickRandom("TM/CK", "TM", "CK"))
            // Setup a weighted random for Status (more Approved)
            .RuleFor(i => i.Status, f => f.PickRandomParam("Draft", "Pending", "Approved", "Approved", "Approved", "Rejected", "Archived"))
            // Setup a weighted random for Risk Level (Removed Orange)
            .RuleFor(i => i.RiskLevel, f => f.PickRandomParam("Green", "Green", "Green", "Green", "Green", "Yellow", "Yellow", "Red"))
            .RuleFor(i => i.OcrConfidenceScore, f => f.Random.Float(0.6f, 0.99f))
            
            // Workflow linkage
            .RuleFor(i => i.Workflow, (f, i) => new InvoiceWorkflow
            {
                UploadedBy = f.PickRandom(users).Id,
                CurrentApprovalStep = i.Status == "Approved" ? 3 : 1
            })
            .RuleFor(i => i.CreatedAt, (f, i) => i.InvoiceDate.ToUniversalTime())
            .RuleFor(i => i.UpdatedAt, (f, i) => i.InvoiceDate.AddDays(f.Random.Int(0, 5)).ToUniversalTime());

        // 4. Generate Invoices
        var mockInvoices = invoiceFaker.Generate(amountToSeed);

        // 5. Generate CheckResults (Rà soát rủi ro) and AuditLogs (Lịch sử)
        foreach (var invoice in mockInvoices)
        {
            var uploader = users.First(u => u.Id == invoice.Workflow.UploadedBy);

            // Mock 1: Lịch sử thao tác ban đầu (Upload)
            invoice.AuditLogs.Add(new InvoiceAuditLog
            {
                AuditId = Guid.NewGuid(),
                InvoiceId = invoice.InvoiceId,
                UserId = uploader.Id,
                UserEmail = uploader.Email,
                UserRole = "User",
                Action = "UPLOAD",
                Comment = "System generated mock data (Upload)",
                CreatedAt = invoice.CreatedAt.ToUniversalTime()
            });

            // Nếu hoá đơn đã Approve thì thêm Log duyệt
            if (invoice.Status == "Approved")
            {
                invoice.AuditLogs.Add(new InvoiceAuditLog
                {
                    AuditId = Guid.NewGuid(),
                    InvoiceId = invoice.InvoiceId,
                    UserId = uploader.Id,
                    UserEmail = uploader.Email,
                    UserRole = "Manager",
                    Action = "APPROVE",
                    Comment = "System generated mock data (Approve)",
                    CreatedAt = invoice.UpdatedAt.ToUniversalTime()
                });
            }

            // Mock 2: Kết quả kiểm tra rủi ro (CheckResults)
            // Sẽ tạo ra 2-3 kết quả kiểu "Chữ ký", "Mã số thuế"
            invoice.CheckResults.Add(new InvoiceCheckResult
            {
                CheckId = Guid.NewGuid(),
                InvoiceId = invoice.InvoiceId,
                Category = "Risk",
                CheckName = "Mã số thuế người mua",
                CheckOrder = 1,
                IsValid = invoice.RiskLevel != "Red",
                Status = invoice.RiskLevel == "Red" ? "Fail" : "Pass",
                CheckedAt = invoice.CreatedAt.AddMinutes(5).ToUniversalTime(),
                Suggestion = invoice.RiskLevel == "Red" ? "Kiểm tra lại MST" : null
            });

            invoice.CheckResults.Add(new InvoiceCheckResult
            {
                CheckId = Guid.NewGuid(),
                InvoiceId = invoice.InvoiceId,
                Category = "Signature",
                CheckName = "Tính toàn vẹn chữ ký số",
                CheckOrder = 2,
                IsValid = invoice.RiskLevel == "Green",
                Status = invoice.RiskLevel == "Green" ? "Pass" : (invoice.RiskLevel == "Yellow" ? "Warning" : "Fail"),
                CheckedAt = invoice.CreatedAt.AddMinutes(6).ToUniversalTime()
            });
        }

        await context.Invoices.AddRangeAsync(mockInvoices);
        await context.SaveChangesAsync();
    }
}