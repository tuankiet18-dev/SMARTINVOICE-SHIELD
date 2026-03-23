using System.ComponentModel.DataAnnotations;

namespace SmartInvoice.API.DTOs.Export;

public class UpdateExportConfigDto
{
    [MaxLength(20)]
    public string? DefaultDebitAccount { get; set; }

    [MaxLength(20)]
    public string? DefaultCreditAccount { get; set; }

    [MaxLength(20)]
    public string? DefaultTaxAccount { get; set; }

    [MaxLength(50)]
    public string? DefaultWarehouse { get; set; }
}
