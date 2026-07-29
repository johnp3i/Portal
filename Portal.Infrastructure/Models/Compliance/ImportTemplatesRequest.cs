namespace Portal.Infrastructure.Models.Compliance;

/// <summary>
/// Request model for importing compliance filing templates into a business.
/// </summary>
public class ImportTemplatesRequest
{
    public int[] TemplateIds { get; set; } = Array.Empty<int>();
    public int Year { get; set; }
    public DateTime? OneOffDueDate { get; set; }

    /// <summary>
    /// Optional per-template due day overrides. Key = TemplateId, Value = custom due day (1-31).
    /// When provided, overrides the template's DefaultDueDay for all generated records.
    /// </summary>
    public Dictionary<int, int>? DueDayOverrides { get; set; }
}
