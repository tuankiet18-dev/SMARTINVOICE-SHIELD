namespace SmartInvoice.API.DTOs.Export;

public class ExportConfigDto
{
    public Guid ConfigId { get; set; }
    public Guid CompanyId { get; set; }
    public string? DefaultDebitAccount { get; set; }
    public string? DefaultCreditAccount { get; set; }
    public string? DefaultTaxAccount { get; set; }
    public string? DefaultWarehouse { get; set; }
}
