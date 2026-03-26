using System.ComponentModel.DataAnnotations;

namespace SmartInvoice.API.DTOs.Export;

public class GenerateExportRequestDto
{
    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Filter theo trạng thái hóa đơn (Draft, Pending, Approved, Rejected, Archived). Null = tất cả.
    /// </summary>
    public string? InvoiceStatus { get; set; }

    /// <summary>
    /// Danh sách ID hóa đơn được chọn cố định để xuất (nếu có thì sẽ bỏ qua StartDate/EndDate).
    /// </summary>
    public List<Guid>? InvoiceIds { get; set; }

    /// <summary>
    /// MISA hoặc STANDARD
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string ExportType { get; set; } = "MISA";
}
