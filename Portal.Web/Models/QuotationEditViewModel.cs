using System.ComponentModel.DataAnnotations;
using Portal.Infrastructure.Entities;

namespace Portal.Web.Models;

public class QuotationEditViewModel
{
    public int Id { get; set; }
    public string Reference { get; set; } = null!;

    [Required(ErrorMessage = "Customer is required")]
    public int CustomerId { get; set; }

    public DateOnly? ValidUntil { get; set; }

    [MaxLength(4000)]
    public string? Notes { get; set; }

    public int? QuotationContactId { get; set; }

    public bool IsGrandTotalShown { get; set; } = true;

    public List<QuotationLine> Lines { get; set; } = new();
    public List<QuotationLineDisplayViewModel> DisplayLines { get; set; } = new();
    public List<ProposalSection> Sections { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public List<Customer> Customers { get; set; } = new();
    public List<QuotationContact> Contacts { get; set; } = new();
}
