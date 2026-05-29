using Portal.Infrastructure.Entities;

namespace Portal.Web.Models;

/// <summary>
/// Display model for quotation lines in the edit view.
/// Wraps the QuotationLine entity with derived display properties.
/// </summary>
public class QuotationLineDisplayViewModel
{
    public QuotationLine Line { get; set; } = null!;

    /// <summary>
    /// Derived from the linked product's ProductTypeId at read-time.
    /// Null when the line has no ProductCode or the product has no ProductTypeId.
    /// </summary>
    public string? ProductTypeName { get; set; }
}
