using System.ComponentModel.DataAnnotations;

namespace SmartInvoice.API.DTOs.Export;

public class GenerateExportRequestDto
{
    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Filter theo trạng thái hóa đơn (Draft, Pending, Approved, Rejected, Archived). Null = tất cả.
    /// </summary>
    public string? InvoiceStatus { get; set; }

    /// <summary>
    /// MISA hoặc STANDARD
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string ExportType { get; set; } = "MISA";
}
