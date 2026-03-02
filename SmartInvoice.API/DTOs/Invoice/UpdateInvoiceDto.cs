namespace SmartInvoice.API.DTOs.Invoice
{
    public class UpdateInvoiceDto
    {
        public string? InvoiceNumber { get; set; }
        public string? SerialNumber { get; set; }
        public DateTime InvoiceDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string? Status { get; set; }
        public string? Notes { get; set; }
    }
}