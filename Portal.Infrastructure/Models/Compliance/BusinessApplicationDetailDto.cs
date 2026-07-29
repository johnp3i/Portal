namespace Portal.Infrastructure.Models.Compliance;

/// <summary>
/// DTO for the compliance filing detail view with allowed transitions and attachments.
/// </summary>
public class BusinessApplicationDetailDto
{
    public int Id { get; set; }
    public string ApplicationName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string DueStatus { get; set; } = string.Empty;
    public int? DaysUntilDue { get; set; }
    public string[] AllowedTransitions { get; set; } = Array.Empty<string>();
    public List<ApplicationAttachmentDto> Attachments { get; set; } = new();
}
