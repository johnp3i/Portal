using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Orchestrates proposal section management including creation, deletion with line reassignment,
/// reordering, line movement between sections, and field updates.
/// </summary>
public interface IProposalSectionService
{
    /// <summary>
    /// Returns all proposal sections for a given quotation, ordered by SortOrder.
    /// </summary>
    Task<List<ProposalSection>> GetByQuotationIdAsync(int quotationId);

    /// <summary>
    /// Creates a new proposal section with the next available SortOrder.
    /// </summary>
    Task AddSectionAsync(int quotationId, string name, string? description, string columnConfiguration = "OneTime", string sectionType = "LineItems", bool isEmphasized = false, string? accentColor = null, string? label = null, bool isTotalsTableShown = false, bool isHalfWidth = false);

    /// <summary>
    /// Deletes a proposal section and reassigns all its lines to the Default section (ProposalSectionId = NULL).
    /// </summary>
    Task RemoveSectionAsync(int sectionId, int quotationId);

    /// <summary>
    /// Bulk updates SortOrder for all sections in a quotation based on the provided ordered list of section IDs.
    /// </summary>
    Task ReorderSectionsAsync(int quotationId, List<int> orderedSectionIds);

    /// <summary>
    /// Moves a quotation line to a different section, or to the Default section when targetSectionId is NULL.
    /// </summary>
    Task MoveLineToSectionAsync(int lineId, int? targetSectionId);

    /// <summary>
    /// Reorders quotation lines by updating their SortOrder based on the provided ordered list of line IDs.
    /// </summary>
    Task ReorderLinesAsync(List<int> orderedLineIds);

    /// <summary>
    /// Updates the Name, Description, Notes, and ColumnConfiguration fields of an existing proposal section.
    /// </summary>
    Task UpdateSectionAsync(int sectionId, string name, string? description, string? notes, string? columnConfiguration = null, string? sectionType = null, bool? isEmphasized = null, string? accentColor = null, string? label = null, bool? isTotalsTableShown = null, bool? isHalfWidth = null);
}
