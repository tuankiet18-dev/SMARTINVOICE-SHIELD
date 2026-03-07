using System.ComponentModel.DataAnnotations;

namespace SmartInvoice.API.DTOs.Blacklist;

/// <summary>
/// DTO để cập nhật thông tin công ty trong blacklist
/// </summary>
public class UpdateBlacklistDto
{
    [MaxLength(200, ErrorMessage = "Tên công ty không được vượt quá 200 ký tự")]
    public string? CompanyName { get; set; }

    public string? Reason { get; set; }

    public bool? IsActive { get; set; }
}
