using System.ComponentModel.DataAnnotations;

namespace SmartInvoice.API.DTOs.Invoice;

public class SubmitInvoiceDto
{
    /// <summary>
    /// Ghi chú khi submit (tùy chọn).
    /// </summary>
    [MaxLength(500)]
    public string? Comment { get; set; }
}

public class ApproveInvoiceDto
{
    /// <summary>
    /// Ghi chú khi duyệt (tùy chọn).
    /// </summary>
    [MaxLength(500)]
    public string? Comment { get; set; }
}

public class RejectInvoiceDto
{
    /// <summary>
    /// Lý do từ chối (bắt buộc).
    /// </summary>
    [Required(ErrorMessage = "Lý do từ chối là bắt buộc")]
    [MaxLength(1000)]
    public string Reason { get; set; } = null!;

    /// <summary>
    /// Ghi chú bổ sung (tùy chọn).
    /// </summary>
    [MaxLength(500)]
    public string? Comment { get; set; }
}
