using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SmartInvoice.API.Data;
using Microsoft.Extensions.DependencyInjection;

public class ProgramTest {
    public static void Main() {
        var contextOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=datisekai.net,1433;Initial Catalog=SmartInvoice_Local;Persist Security Info=False;User ID=SA;Password=123123@SA;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=True;Connection Timeout=30;")
            .Options;
        using var context = new AppDbContext(contextOptions);
        var invoices = context.Invoices.OrderByDescending(i => i.CreatedAt).Take(20).ToList();
        foreach (var i in invoices) {
            Console.WriteLine($@"ID: {i.InvoiceId}, No: {i.InvoiceNumber}, OriginalFileId: {i.OriginalFileId}, VisualFileId: {i.VisualFileId}, Method: {i.ProcessingMethod}, Status: {i.Status}"); 
        }
    }
}
