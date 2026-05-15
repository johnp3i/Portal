using Portal.Infrastructure.Entities;

namespace Portal.Web.Models;

public class CustomerListViewModel
{
    public List<Customer> Customers { get; set; } = new();
    public string? SearchTerm { get; set; }
    public bool? IsActiveFilter { get; set; }
}
