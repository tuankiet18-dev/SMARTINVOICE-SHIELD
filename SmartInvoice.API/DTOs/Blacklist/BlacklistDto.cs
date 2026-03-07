namespace SmartInvoice.API.DTOs.Blacklist;

/// <summary>
/// DTO trả về thông tin công ty trong blacklist
/// </summary>
public class BlacklistDto
{
    public Guid BlacklistId { get; set; }
    public string TaxCode { get; set; } = null!;
    public string? CompanyName { get; set; }
    public string? Reason { get; set; }
    public bool IsActive { get; set; }
    public Guid? AddedBy { get; set; }
    public DateTime AddedDate { get; set; }
    public DateTime? RemovedDate { get; set; }
}
