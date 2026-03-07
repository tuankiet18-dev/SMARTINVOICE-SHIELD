using System.ComponentModel.DataAnnotations;

namespace SmartInvoice.API.DTOs.Blacklist;

/// <summary>
/// DTO để thêm công ty vào blacklist
/// </summary>
public class CreateBlacklistDto
{
    [Required(ErrorMessage = "Mã số thuế là bắt buộc")]
    [MaxLength(14, ErrorMessage = "Mã số thuế không được vượt quá 14 ký tự")]
    public string TaxCode { get; set; } = null!;

    [MaxLength(200, ErrorMessage = "Tên công ty không được vượt quá 200 ký tự")]
    public string? CompanyName { get; set; }

    public string? Reason { get; set; }
}
