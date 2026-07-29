namespace Portal.Infrastructure.Entities;

/// <summary>
/// Represents a per-business filing instance with due date and status workflow.
/// Schema: [compliance].BusinessApplication
/// </summary>
public class BusinessApplication
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int ApplicationTypeId { get; set; }

    public DateTime DueDate { get; set; }

    public string Status { get; set; } = "Pending";

    public string? ReferenceNumber { get; set; }

    public string? Notes { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
