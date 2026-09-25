namespace Portal.Infrastructure.Models.Sales;

// ============================================================================
// Prospect Excel import DTOs
// The workbook's "30 Prospects" sheet has 19 columns (A1:S). Total /20 and Priority
// are IGNORED on import and recomputed from the four score dimensions. Other sheets
// (Campaign Summary, Weekly Call Tracker) are ignored.
// ============================================================================

/// <summary>One parsed prospect row from the workbook, with validation + duplicate state.</summary>
public class ProspectImportRow
{
    /// <summary>Excel row number (1-based) for error messages.</summary>
    public int RowNumber { get; set; }

    /// <summary>Workbook "ID" column (e.g. "A01"). Used for idempotent re-import.</summary>
    public string? ImportRowId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Segment { get; set; }
    public string? Location { get; set; }
    public string? BusinessType { get; set; }
    public string? PublicContactRole { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? PublicEvidence { get; set; }
    public string? WhyFit { get; set; }
    public string? RecommendedFirstContact { get; set; }
    public string? ResearchSourceUrl { get; set; }

    public byte IcpFitScore { get; set; }
    public byte PainProbabilityScore { get; set; }
    public byte AccessibilityScore { get; set; }
    public byte LearningValueScore { get; set; }

    /// <summary>Recomputed total (0-20), never read from the workbook.</summary>
    public int Total { get; set; }
    /// <summary>Recomputed priority band (A/B/Hold).</summary>
    public string Priority { get; set; } = string.Empty;

    /// <summary>True when this row matches an existing prospect (safe re-import will update it).</summary>
    public bool IsDuplicate { get; set; }
    public int? DuplicateOfId { get; set; }

    /// <summary>Row-level validation errors. A row with errors is skipped on confirm.</summary>
    public List<string> Errors { get; set; } = new();

    public bool IsValid => Errors.Count == 0;
}

/// <summary>Preview returned before any DB write. Carried back into ConfirmImportAsync.</summary>
public class ProspectImportPreview
{
    public string FileName { get; set; } = string.Empty;
    public int ProspectCampaignId { get; set; }
    public string CampaignName { get; set; } = string.Empty;

    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public int NewRows { get; set; }
    public int DuplicateRows { get; set; }

    public List<ProspectImportRow> Rows { get; set; } = new();

    /// <summary>File-level errors (wrong sheet, empty, too large). When set, nothing can be imported.</summary>
    public List<string> FileErrors { get; set; } = new();
}

/// <summary>Outcome of a confirmed import.</summary>
public class ProspectImportResult
{
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedInvalidCount { get; set; }
}
