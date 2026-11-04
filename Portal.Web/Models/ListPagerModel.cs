namespace Portal.Web.Models;

/// <summary>
/// View model for the shared <c>_ListPager</c> partial — a numbered list pager
/// (Previous · 1 · … · window · … · last · Next) for server-rendered list pages.
/// Build it from any <c>PagedResult&lt;T&gt;</c> and point it at the list's base path.
/// </summary>
public class ListPagerModel
{
    /// <summary>Current 1-based page.</summary>
    public int CurrentPage { get; set; }

    /// <summary>Total number of pages.</summary>
    public int TotalPages { get; set; }

    /// <summary>Total item count (for the "Showing X–Y of N" label).</summary>
    public int TotalCount { get; set; }

    /// <summary>Page size (for the "Showing X–Y" label).</summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Absolute path of the list action, e.g. <c>/Sales/Contacts</c>. The partial appends
    /// the page (and any <see cref="SearchTerm"/>) as a query string.
    /// </summary>
    public string BasePath { get; set; } = string.Empty;

    /// <summary>Optional current search term, preserved across page links.</summary>
    public string? SearchTerm { get; set; }

    /// <summary>Noun shown in the "Showing X–Y of N {ItemNoun}" label (e.g. "contacts").</summary>
    public string ItemNoun { get; set; } = "items";

    /// <summary>How many page numbers to show on each side of the current page.</summary>
    public int Window { get; set; } = 2;
}
