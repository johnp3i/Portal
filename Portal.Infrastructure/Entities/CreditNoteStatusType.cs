namespace Portal.Infrastructure.Entities;

/// <summary>
/// Reference table defining the lifecycle states of a Credit Note.
/// Schema: [credit].CreditNoteStatusType
/// Seed values: Draft (1), Issued (2), Applied (3), Voided (4)
/// </summary>
public class CreditNoteStatusType
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    // Navigation properties
    public ICollection<CreditNote> CreditNotes { get; set; } = new List<CreditNote>();
}
