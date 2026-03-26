namespace SmartInvoice.API.DTOs.Invoice;

public class InvoiceStatsDto
{
    public decimal TotalAmount { get; set; }
    public decimal TotalTaxAmount { get; set; }
    public int ValidCount { get; set; }
    public int NeedReviewCount { get; set; }
    public int TotalCount { get; set; }
    public int ApprovedCount { get; set; }
}