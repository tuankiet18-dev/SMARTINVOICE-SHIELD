using SmartInvoice.API.Entities.JsonModels;

namespace SmartInvoice.API.DTOs.Invoice
{
    public class InvoiceAuditLogDto
    {
        public Guid AuditId { get; set; }
        
        // Ai làm? (Lấy từ trường denormalized cho nhanh)
        public string? UserEmail { get; set; } 
        public string? UserRole { get; set; }
        public string? IpAddress { get; set; }

        // Làm gì?
        public string Action { get; set; } = null!; // EDIT, APPROVE...
        public DateTime CreatedAt { get; set; }

        public List<AuditChange>? Changes { get; set; }
        
        public string? Reason { get; set; }
        public string? Comment { get; set; }
    }
}