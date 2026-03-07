namespace SmartInvoice.API.DTOs.AuditLog;

public class AuditLogQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public string? Action { get; set; } // UPLOAD, EDIT, SUBMIT, APPROVE, REJECT
    public string? DateFrom { get; set; }
    public string? DateTo { get; set; }
}

public class SystemAuditLogDto
{
    public Guid AuditId { get; set; }
    public Guid InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }

    public string? UserEmail { get; set; }
    public string? UserRole { get; set; }
    public string? UserFullName { get; set; }

    public string Action { get; set; } = null!;
    public string? Reason { get; set; }
    public string? Comment { get; set; }
    public string? IpAddress { get; set; }

    public List<SmartInvoice.API.Entities.JsonModels.AuditChange>? Changes { get; set; }

    public DateTime CreatedAt { get; set; }
}
