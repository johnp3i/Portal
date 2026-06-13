using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Orchestrates proposal section management including creation, deletion with line reassignment,
/// reordering, line movement between sections, and field updates.
/// </summary>
public class ProposalSectionService : IProposalSectionService
{
    private readonly ProposalSectionRepository _sectionRepository;
    private readonly PortalDbContext _dbContext;

    public ProposalSectionService(
        ProposalSectionRepository sectionRepository,
        PortalDbContext dbContext)
    {
        _sectionRepository = sectionRepository;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Returns all proposal sections for a given quotation, ordered by SortOrder.
    /// </summary>
    public async Task<List<ProposalSection>> GetByQuotationIdAsync(int quotationId)
    {
        try
        {
            return await _sectionRepository.GetByQuotationIdAsync(quotationId);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Creates a new proposal section with the next available SortOrder.
    /// Validates that the name is non-empty/whitespace.
    /// </summary>
    public async Task AddSectionAsync(int quotationId, string name, string? description, string columnConfiguration = "OneTime", string sectionType = "LineItems", bool isEmphasized = false, string? accentColor = null, string? label = null, bool isTotalsTableShown = false, bool isHalfWidth = false)
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

            var existingSections = await _sectionRepository.GetByQuotationIdAsync(quotationId);
            var nextSortOrder = existingSections.Count > 0
                ? existingSections.Max(s => s.SortOrder) + 1
                : 1;

            var section = new ProposalSection
            {
                QuotationId = quotationId,
                Name = name.Trim(),
                SortOrder = nextSortOrder,
                ColumnConfiguration = columnConfiguration ?? "OneTime",
                Description = description,
                Notes = null,
                SectionType = sectionType,
                IsEmphasized = isEmphasized,
                AccentColor = accentColor,
                Label = label,
                IsTotalsTableShown = isTotalsTableShown,
                IsHalfWidth = isHalfWidth
            };

            await _sectionRepository.InsertAsync(section);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Deletes a proposal section and reassigns all its lines to the Default section (ProposalSectionId = NULL).
    /// Sets ProposalSectionId = NULL on all QuotationLines in the section before deleting.
    /// </summary>
    public async Task RemoveSectionAsync(int sectionId, int quotationId)
    {
        try
        {
            const string reassignQuery = @"
                UPDATE [quotation].[QuotationLine]
                SET [ProposalSectionId] = NULL
                WHERE [ProposalSectionId] = @SectionId";

            await _dbContext.Database.ExecuteSqlRawAsync(reassignQuery,
                new SqlParameter("@SectionId", sectionId));

            await _sectionRepository.DeleteAsync(sectionId);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Bulk updates SortOrder for all sections in a quotation based on the provided ordered list of section IDs.
    /// Each section's SortOrder is set to its position (1-based) in the list.
    /// </summary>
    public async Task ReorderSectionsAsync(int quotationId, List<int> orderedSectionIds)
    {
        try
        {
            for (int i = 0; i < orderedSectionIds.Count; i++)
            {
                const string updateQuery = @"
                    UPDATE [quotation].[ProposalSection]
                    SET [SortOrder] = @SortOrder
                    WHERE [Id] = @Id AND [QuotationId] = @QuotationId";

                await _dbContext.Database.ExecuteSqlRawAsync(updateQuery,
                    new SqlParameter("@SortOrder", i + 1),
                    new SqlParameter("@Id", orderedSectionIds[i]),
                    new SqlParameter("@QuotationId", quotationId));
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Moves a quotation line to a different section, or to the Default section when targetSectionId is NULL.
    /// </summary>
    public async Task MoveLineToSectionAsync(int lineId, int? targetSectionId)
    {
        try
        {
            const string updateQuery = @"
                UPDATE [quotation].[QuotationLine]
                SET [ProposalSectionId] = @TargetSectionId
                WHERE [Id] = @LineId";

            await _dbContext.Database.ExecuteSqlRawAsync(updateQuery,
                new SqlParameter("@TargetSectionId", targetSectionId ?? (object)DBNull.Value),
                new SqlParameter("@LineId", lineId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Reorders quotation lines by updating their SortOrder based on the provided ordered list of line IDs.
    /// Each line's SortOrder is set to its position (1-based) in the list.
    /// </summary>
    public async Task ReorderLinesAsync(List<int> orderedLineIds)
    {
        try
        {
            for (int i = 0; i < orderedLineIds.Count; i++)
            {
                const string updateQuery = @"
                    UPDATE [quotation].[QuotationLine]
                    SET [SortOrder] = @SortOrder
                    WHERE [quotation].[QuotationLine].[Id] = @Id";

                await _dbContext.Database.ExecuteSqlRawAsync(updateQuery,
                    new SqlParameter("@SortOrder", i + 1),
                    new SqlParameter("@Id", orderedLineIds[i]));
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Updates the Name, Description, and Notes fields of an existing proposal section.
    /// Validates that the name is non-empty/whitespace.
    /// </summary>
    public async Task UpdateSectionAsync(int sectionId, string name, string? description, string? notes, string? columnConfiguration = null, string? sectionType = null, bool? isEmphasized = null, string? accentColor = null, string? label = null, bool? isTotalsTableShown = null, bool? isHalfWidth = null)
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
                throw new InvalidOperationException("Proposal section not found.");
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

            // AccentColor can be explicitly set to null to clear it
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

            if (isHalfWidth.HasValue)
            {
                section.IsHalfWidth = isHalfWidth.Value;
            }

            await _sectionRepository.UpdateAsync(section);
        }
        catch (Exception)
        {
            throw;
        }
    }
}
