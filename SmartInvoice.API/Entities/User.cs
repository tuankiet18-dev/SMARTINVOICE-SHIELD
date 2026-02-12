using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
using SmartInvoice.API.Enums;

namespace SmartInvoice.API.Entities;

[Table("Users")]
public class User
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Email { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string CognitoSub { get; set; } = null!; // Link to AWS Cognito User

    // --- Multi-tenant ---
    public Guid CompanyId { get; set; }
    [ForeignKey(nameof(CompanyId))]
    public Company? Company { get; set; }

    // --- Personal Info ---
    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = null!;

    [MaxLength(50)]
    public string? EmployeeId { get; set; }

    // --- Authorization ---
    [MaxLength(50)]
    public string Role { get; set; } = UserRole.Member.ToString(); // Re-adding Role as string since we removed AspNetRoles

    [Column(TypeName = "jsonb")]
    public List<string>? Permissions { get; set; } = new();

    // --- Activity & Audit ---
    public bool IsActive { get; set; } = true;

    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginUserAgent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
