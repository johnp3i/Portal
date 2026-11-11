namespace Portal.Infrastructure.Models;

/// <summary>
/// Filter criteria for the product catalog listing. All values are optional; a null/default
/// value means "no constraint" for that dimension. Values are whitelisted/parameterised before
/// reaching SQL.
/// </summary>
public class ProductListFilter
{
    /// <summary>Free-text match against product code or description.</summary>
    public string? Search { get; set; }

    /// <summary>
    /// Active-state filter. One of: "active" (default), "inactive", "all".
    /// </summary>
    public string Status { get; set; } = "active";

    /// <summary>Restrict to a single supplier (by Id).</summary>
    public int? SupplierId { get; set; }

    /// <summary>Restrict to a single product type (Services = 1, Goods = 2).</summary>
    public int? ProductTypeId { get; set; }

    /// <summary>Lower bound (inclusive) on CreatedAtUtc, as a calendar date.</summary>
    public DateOnly? CreatedFrom { get; set; }

    /// <summary>Upper bound (inclusive) on CreatedAtUtc, as a calendar date.</summary>
    public DateOnly? CreatedTo { get; set; }

    /// <summary>
    /// Sort key. One of: "code" (default), "created_desc", "created_asc",
    /// "price_desc", "price_asc", "lastused_desc".
    /// </summary>
    public string Sort { get; set; } = "code";
}
