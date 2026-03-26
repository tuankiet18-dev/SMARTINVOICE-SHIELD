namespace SmartInvoice.API.DTOs.Invoice
{
    public class InvoiceDto
    {
        public Guid InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = null!;
        public string? SerialNumber { get; set; } // Ký hiệu
        public DateTime InvoiceDate { get; set; }
        public DateTime CreatedAt { get; set; }

        // Thông tin người bán (Hiển thị tóm tắt)
        public string? SellerName { get; set; }
        public string? SellerTaxCode { get; set; }

        // Tiền nong (Quan trọng)
        public decimal TotalAmount { get; set; }
        public string InvoiceCurrency { get; set; } = "VND";

        // Trạng thái & Rủi ro (Để tô màu trên FE)
        public string Status { get; set; } = null!; // Draft, Pending...
        public int CurrentApprovalStep { get; set; }
        public string RiskLevel { get; set; } = null!; // Green, Red...

        // Ai làm?
        public string? UploadedByName { get; set; } // Map từ User entity

        // Phương thức xử lý
        public string ProcessingMethod { get; set; } = null!; // XML, OCR
    }
}