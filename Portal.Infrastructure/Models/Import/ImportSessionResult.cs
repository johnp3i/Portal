namespace Portal.Infrastructure.Models.Import;

/// <summary>
/// Result returned to the UI after parsing a file — contains preview data.
/// </summary>
public class ImportSessionResult
{
    public int SessionId { get; set; }

    public int TotalRows { get; set; }

    public int ValidRows { get; set; }

    public int InvalidRows { get; set; }

    public int WarningRows { get; set; }

    public decimal BatchTotal { get; set; }

    public List<ValidatedRow> Rows { get; set; } = new();
}

/// <summary>
/// Result after successful import confirmation.
/// </summary>
public class ImportConfirmationResult
{
    public int ImportedCount { get; set; }

    public decimal TotalAmount { get; set; }
}
