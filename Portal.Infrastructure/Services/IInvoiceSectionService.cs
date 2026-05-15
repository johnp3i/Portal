using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Orchestrates invoice section management including creation, deletion with line reassignment,
/// reordering, line movement between sections, and field updates.
/// </summary>
public interface IInvoiceSectionService
{
    /// <summary>
    /// Returns all invoice sections for a given invoice, ordered by SortOrder.
    /// </summary>
    Task<List<InvoiceSection>> GetByInvoiceIdAsync(int invoiceId);

    /// <summary>
    /// Creates a new invoice section with the next available SortOrder.
    /// </summary>
    Task AddSectionAsync(int invoiceId, string name, string? description,
        string columnConfiguration = "OneTime", string sectionType = "LineItems",
        bool isEmphasized = false, string? accentColor = null, string? label = null,
        bool isTotalsTableShown = false);

    /// <summary>
    /// Deletes an invoice section and reassigns all its lines to the Default section (InvoiceSectionId = NULL).
    /// </summary>
    Task RemoveSectionAsync(int sectionId, int invoiceId);

    /// <summary>
    /// Bulk updates SortOrder for all sections in an invoice based on the provided ordered list of section IDs.
    /// </summary>
    Task ReorderSectionsAsync(int invoiceId, List<int> orderedSectionIds);

    /// <summary>
    /// Moves an invoice line to a different section, or to the Default section when targetSectionId is NULL.
    /// </summary>
    Task MoveLineToSectionAsync(int lineId, int? targetSectionId);

    /// <summary>
    /// Reorders invoice lines by updating their SortOrder based on the provided ordered list of line IDs.
    /// </summary>
    Task ReorderLinesAsync(List<int> orderedLineIds);

    /// <summary>
    /// Updates the Name, Description, Notes, and ColumnConfiguration fields of an existing invoice section.
    /// </summary>
    Task UpdateSectionAsync(int sectionId, string name, string? description, string? notes,
        string? columnConfiguration = null, string? sectionType = null,
        bool? isEmphasized = null, string? accentColor = null, string? label = null,
        bool? isTotalsTableShown = null);
}
