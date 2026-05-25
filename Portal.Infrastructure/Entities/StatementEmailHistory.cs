namespace Portal.Infrastructure.Entities;

/// <summary>
/// A record of a statement of account emailed to a customer, including the period covered,
/// recipient, and sender details. Append-only — no UPDATE or DELETE permitted.
/// Schema: [customer].StatementEmailHistory
/// </summary>
public class StatementEmailHistory
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int CustomerId { get; set; }

    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public string RecipientEmail { get; set; } = string.Empty;

    public string SentByUserId { get; set; } = string.Empty;

    public DateTime SentAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public Customer Customer { get; set; } = null!;
}
