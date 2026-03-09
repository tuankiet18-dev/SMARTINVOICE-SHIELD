using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace SmartInvoice.API.DTOs.Invoice;

public class SubmitInvoiceDto
{
    /// <summary>
    /// Ghi chú khi submit (tùy chọn).
    /// </summary>
    [MaxLength(500)]
    public string? Comment { get; set; }
}

public class SubmitBatchDto
{
    /// <summary>
    /// Danh sách ID hóa đơn cần gửi duyệt hàng loạt.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "Cần ít nhất 1 hóa đơn để gửi duyệt.")]
    public List<Guid> InvoiceIds { get; set; } = new();

    /// <summary>
    /// Ghi chú chung cho cả batch (tùy chọn).
    /// </summary>
    [MaxLength(500)]
    public string? Comment { get; set; }
}

public class BatchSubmitResultDto
{
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public List<BatchSubmitItemResult> Results { get; set; } = new();
}

public class BatchSubmitItemResult
{
    public Guid InvoiceId { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
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
