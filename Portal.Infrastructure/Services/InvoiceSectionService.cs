using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Orchestrates invoice section management including creation, deletion with line reassignment,
/// reordering, line movement between sections, and field updates.
/// </summary>
public class InvoiceSectionService : IInvoiceSectionService
{
    private readonly InvoiceSectionRepository _sectionRepository;
    private readonly InvoiceLineRepository _lineRepository;

    public InvoiceSectionService(
        InvoiceSectionRepository sectionRepository,
        InvoiceLineRepository lineRepository)
    {
        _sectionRepository = sectionRepository;
        _lineRepository = lineRepository;
    }

    /// <summary>
    /// Returns all invoice sections for a given invoice, ordered by SortOrder.
    /// </summary>
    public async Task<List<InvoiceSection>> GetByInvoiceIdAsync(int invoiceId)
    {
        try
        {
            return await _sectionRepository.GetByInvoiceIdAsync(invoiceId);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Creates a new invoice section with the next available SortOrder.
    /// Validates that the name is non-empty/whitespace and SectionType is valid.
    /// </summary>
    public async Task AddSectionAsync(int invoiceId, string name, string? description,
        string columnConfiguration = "OneTime", string sectionType = "LineItems",
        bool isEmphasized = false, string? accentColor = null, string? label = null,
        bool isTotalsTableShown = false)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Section name cannot be empty or whitespace.", nameof(name));
            }

            if (sectionType != "LineItems" && sectionType != "Narrative")
            {
                throw new ArgumentException("SectionType must be either 'LineItems' or 'Narrative'.", nameof(sectionType));
            }

            var existingSections = await _sectionRepository.GetByInvoiceIdAsync(invoiceId);
            var nextSortOrder = existingSections.Count > 0
                ? existingSections.Max(s => s.SortOrder) + 1
                : 1;

            var section = new InvoiceSection
            {
                InvoiceId = invoiceId,
                Name = name.Trim(),
                SortOrder = nextSortOrder,
                ColumnConfiguration = columnConfiguration ?? "OneTime",
                Description = description,
                Notes = null,
                SectionType = sectionType,
                IsEmphasized = isEmphasized,
                AccentColor = accentColor,
                Label = label,
                IsTotalsTableShown = isTotalsTableShown
            };

            await _sectionRepository.InsertAsync(section);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Deletes an invoice section and reassigns all its lines to the Default section (InvoiceSectionId = NULL).
    /// Sets InvoiceSectionId = NULL on all InvoiceLines in the section before deleting.
    /// </summary>
    public async Task RemoveSectionAsync(int sectionId, int invoiceId)
    {
        try
        {
            var lines = await _lineRepository.GetByInvoiceIdAsync(invoiceId);

            foreach (var line in lines.Where(l => l.InvoiceSectionId == sectionId))
            {
                await _lineRepository.UpdateSectionIdAsync(line.Id, null);
            }

            await _sectionRepository.DeleteAsync(sectionId);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Bulk updates SortOrder for all sections in an invoice based on the provided ordered list of section IDs.
    /// Each section's SortOrder is set to its position (1-based) in the list.
    /// </summary>
    public async Task ReorderSectionsAsync(int invoiceId, List<int> orderedSectionIds)
    {
        try
        {
            var updates = orderedSectionIds
                .Select((id, index) => (Id: id, SortOrder: index + 1))
                .ToList();

            await _sectionRepository.UpdateSortOrdersAsync(updates);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Moves an invoice line to a different section, or to the Default section when targetSectionId is NULL.
    /// </summary>
    public async Task MoveLineToSectionAsync(int lineId, int? targetSectionId)
    {
        try
        {
            await _lineRepository.UpdateSectionIdAsync(lineId, targetSectionId);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Reorders invoice lines by updating their SortOrder based on the provided ordered list of line IDs.
    /// Each line's SortOrder is set to its position (1-based) in the list.
    /// </summary>
    public async Task ReorderLinesAsync(List<int> orderedLineIds)
    {
        try
        {
            var updates = orderedLineIds
                .Select((id, index) => (Id: id, SortOrder: index + 1))
                .ToList();

            await _lineRepository.UpdateSortOrdersAsync(updates);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Updates the Name, Description, Notes, and optional fields of an existing invoice section.
    /// Validates that the name is non-empty/whitespace.
    /// </summary>
    public async Task UpdateSectionAsync(int sectionId, string name, string? description, string? notes,
        string? columnConfiguration = null, string? sectionType = null,
        bool? isEmphasized = null, string? accentColor = null, string? label = null,
        bool? isTotalsTableShown = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Section name cannot be empty or whitespace.", nameof(name));
            }

            if (sectionType != null && sectionType != "LineItems" && sectionType != "Narrative")
            {
                throw new ArgumentException("SectionType must be either 'LineItems' or 'Narrative'.", nameof(sectionType));
            }

            var section = await _sectionRepository.GetByIdAsync(sectionId);

            if (section == null)
            {
                throw new InvalidOperationException("Invoice section not found.");
            }

            section.Name = name.Trim();
            section.Description = description;
            section.Notes = notes;

            if (!string.IsNullOrWhiteSpace(columnConfiguration))
            {
                section.ColumnConfiguration = columnConfiguration;
            }

            if (sectionType != null)
            {
                section.SectionType = sectionType;
            }

            if (isEmphasized.HasValue)
            {
                section.IsEmphasized = isEmphasized.Value;
            }

            if (accentColor != null || isEmphasized.HasValue)
            {
                section.AccentColor = accentColor;
            }

            if (label != null)
            {
                section.Label = string.IsNullOrWhiteSpace(label) ? null : label;
            }

            if (isTotalsTableShown.HasValue)
            {
                section.IsTotalsTableShown = isTotalsTableShown.Value;
            }

            await _sectionRepository.UpdateAsync(section);
        }
        catch (Exception)
        {
            throw;
        }
    }
}
