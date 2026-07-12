namespace Portal.Infrastructure.Entities.Import;

/// <summary>
/// A transient record representing one upload-parse-review-confirm cycle.
/// Holds parsed rows and user corrections until confirmation or expiry.
/// Schema: [import].ImportSession
/// </summary>
public class ImportSession
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int SupplierId { get; set; }

    public int? ParserTemplateId { get; set; }

    public string FileName { get; set; } = null!;

    public int TotalRows { get; set; }

    public int ValidRows { get; set; }

    public int InvalidRows { get; set; }

    public string RowDataJson { get; set; } = null!;

    public bool IsConfirmed { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business? Business { get; set; }

    public Supplier? Supplier { get; set; }

    public ParserTemplate? ParserTemplate { get; set; }
}
