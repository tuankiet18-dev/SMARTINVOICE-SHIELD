namespace SmartInvoice.API.DTOs.Invoice
{
    public class UpdateInvoiceDto
    {
        public string? InvoiceNumber { get; set; }
        public string? SerialNumber { get; set; }
        public string? FormNumber { get; set; }
        public DateTime InvoiceDate { get; set; }
        
        public decimal TotalAmount { get; set; }
        public decimal? TotalAmountBeforeTax { get; set; }
        public decimal? TotalTaxAmount { get; set; }

        public string? Status { get; set; }
        public string? Notes { get; set; }

        // Seller
        public string? SellerName { get; set; }
        public string? SellerTaxCode { get; set; }
        public string? SellerAddress { get; set; }

        // Buyer
        public string? BuyerName { get; set; }
        public string? BuyerTaxCode { get; set; }
        public string? BuyerAddress { get; set; }

        // Line Items
        public List<LineItemDto>? LineItems { get; set; }
    }
}