namespace SmartInvoice.API.DTOs.Export;

public class ExportResultDto
{
    public Guid ExportId { get; set; }
    public string FileName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public int TotalRecords { get; set; }
    public string? DownloadUrl { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
