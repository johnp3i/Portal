using Portal.Infrastructure.Entities;

namespace Portal.Web.Models;

public class QuotationDetailViewModel
{
    public Quotation Quotation { get; set; } = null!;
    public List<QuotationLine> Lines { get; set; } = new();
    public string CustomerName { get; set; } = null!;
    public string StatusName { get; set; } = null!;
    public bool IsExpired { get; set; }
    public List<int> AvailableTransitions { get; set; } = new();
}
