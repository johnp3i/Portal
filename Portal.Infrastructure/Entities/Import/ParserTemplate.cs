namespace Portal.Infrastructure.Entities.Import;

/// <summary>
/// A reusable, supplier-specific configuration that defines how columns in
/// an uploaded file map to purchase fields.
/// Schema: [import].ParserTemplate
/// </summary>
public class ParserTemplate
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int SupplierId { get; set; }

    public string Name { get; set; } = null!;

    public string FileFormatType { get; set; } = null!; // "CSV" or "Excel"

    public int HeaderRow { get; set; } = 1;

    public int DataStartRow { get; set; } = 2;

    public string? SheetName { get; set; }

    public string ColumnMappingsJson { get; set; } = null!;

    public bool IsManaged { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    // Navigation properties
    public Business? Business { get; set; }

    public Supplier? Supplier { get; set; }
}
