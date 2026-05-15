using System.ComponentModel.DataAnnotations;
using Portal.Infrastructure.Entities;

namespace Portal.Web.Models;

public class QuotationCreateViewModel
{
    [Required(ErrorMessage = "Customer is required")]
    public int CustomerId { get; set; }

    public DateOnly? ValidUntil { get; set; }

    [MaxLength(4000)]
    public string? Notes { get; set; }

    public List<Customer> Customers { get; set; } = new();
}
