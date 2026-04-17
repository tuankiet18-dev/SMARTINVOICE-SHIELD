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
        var companies = await context.Companies
            .Where(c => c.CompanyName != "System Administration")
            .ToListAsync();
            
        var validCompanyIds = companies.Select(c => c.CompanyId).ToList();
        var users = await context.Users
            .Where(u => validCompanyIds.Contains(u.CompanyId))
            .ToListAsync();
        
        // Let's assume you might not have DocumentTypes, fallback to handle it:
        // Adjust if your db has DocumentTypes mapped
        var documentTypesIds = await context.Set<DocumentType>().Select(d => d.DocumentTypeId).ToListAsync();

        if (companies.Count == 0 || users.Count == 0)
        {
            throw new Exception("Please ensure you have at least 1 Company (excluding System Administration) and 1 User in your database before seeding Invoices.");
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
            // Gắn cờ DEMO vào số hóa đơn để dễ phân biệt bằng mắt
            .RuleFor(i => i.InvoiceNumber, f => "DEMO-" + f.Random.Replace("#######"))
            
            // Lùi ngày về quá khứ (từ 1 đến 180 ngày trước), không có ngày hôm nay.
            // Để chừa trọn "Hôm nay" (Today) hoàn toàn trống cho bạn test Core Flow!
            .RuleFor(i => i.InvoiceDate, f => DateTime.UtcNow.AddDays(-f.Random.Int(1, 180)))
            
            .RuleFor(i => i.InvoiceCurrency, f => "VND")
            .RuleFor(i => i.ExchangeRate, f => 1m)
            
            .RuleFor(i => i.Seller, f => sellerFaker.Generate())
            .RuleFor(i => i.Buyer, f => buyerFaker.Generate())
            
            // Tax Calculation logic
            .RuleFor(i => i.TotalAmountBeforeTax, f => Math.Round(f.Random.Decimal(100000m, 50000000m), 2))
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
            
            // Generate Detailed ExtractedData including Line Items
            .RuleFor(i => i.ExtractedData, (f, i) => 
            {
                var numItems = f.Random.Int(1, 5); // 1 to 5 items per invoice
                var lineItems = new List<SmartInvoice.API.Entities.JsonModels.InvoiceLineItem>();
                
                decimal calculatedTotalAmountBeforeTax = 0;
                
                for (int j = 1; j <= numItems; j++)
                {
                    var quantity = f.Random.Int(1, 50);
                    var unitPrice = Math.Round(f.Random.Decimal(10000m, 1000000m), 2);
                    var total = quantity * unitPrice;
                    var vatAmount = Math.Round(total * 0.1m, 2);
                    
                    lineItems.Add(new SmartInvoice.API.Entities.JsonModels.InvoiceLineItem
                    {
                        Stt = j,
                        ProductName = f.Commerce.ProductName(),
                        Unit = f.PickRandom("Cái", "Chiếc", "Hộp", "Kg", "Lít", "Bộ"),
                        Quantity = quantity,
                        UnitPrice = unitPrice,
                        TotalAmount = total,
                        VatRate = 10,
                        VatAmount = vatAmount
                    });
                    
                    calculatedTotalAmountBeforeTax += total;
                }
                
                // Override outer Invoice totals to match the line items sum to maintain consistency!
                i.TotalAmountBeforeTax = calculatedTotalAmountBeforeTax;
                i.TotalTaxAmount = Math.Round(calculatedTotalAmountBeforeTax * 0.1m, 2);
                i.TotalAmount = i.TotalAmountBeforeTax.Value + i.TotalTaxAmount.Value;

                return new SmartInvoice.API.Entities.JsonModels.InvoiceExtractedData
                {
                    SellerName = i.Seller.Name,
                    SellerTaxCode = i.Seller.TaxCode,
                    SellerAddress = i.Seller.Address,
                    BuyerName = i.Buyer.Name,
                    BuyerTaxCode = i.Buyer.TaxCode,
                    BuyerAddress = i.Buyer.Address,
                    InvoiceNumber = i.InvoiceNumber,
                    InvoiceDate = i.InvoiceDate,
                    TotalPreTax = i.TotalAmountBeforeTax.Value,
                    TotalTaxAmount = i.TotalTaxAmount.Value,
                    TotalAmount = i.TotalAmount,
                    LineItems = lineItems,
                    InvoiceCurrency = "VND"
                };
            })
            
            // Workflow linkage
            .RuleFor(i => i.Workflow, (f, i) => 
            {
                var companyUsers = users.Where(u => u.CompanyId == i.CompanyId).ToList();
                var uploaderId = companyUsers.Any() ? f.PickRandom(companyUsers).Id : f.PickRandom(users).Id;
                
                return new InvoiceWorkflow
                {
                    UploadedBy = uploaderId,
                    CurrentApprovalStep = i.Status == "Approved" ? 3 : 1
                };
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
                CompanyId = invoice.CompanyId,
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
                    CompanyId = invoice.CompanyId,
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