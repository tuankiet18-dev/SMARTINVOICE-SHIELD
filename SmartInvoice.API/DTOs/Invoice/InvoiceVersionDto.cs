namespace SmartInvoice.API.DTOs.Invoice;

public class InvoiceVersionDto
{
    public Guid InvoiceId { get; set; }
    public int Version { get; set; }
    public string Status { get; set; } = null!;
    public string RiskLevel { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
