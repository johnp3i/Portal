using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Web.Models;

public class QuotationListViewModel
{
    public List<QuotationListDto> Quotations { get; set; } = new();
    public PagedResult<QuotationListDto> PagedQuotations { get; set; } = new();
    public int? StatusFilter { get; set; }
    public int? CustomerFilter { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? SearchTerm { get; set; }
    public List<Customer> Customers { get; set; } = new();
    public List<QuotationStatusType> Statuses { get; set; } = new();
}
