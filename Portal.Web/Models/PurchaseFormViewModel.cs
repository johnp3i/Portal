using System.ComponentModel.DataAnnotations;
using Portal.Infrastructure.Entities;

namespace Portal.Web.Models;

public class PurchaseFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Supplier is required")]
    public int SupplierId { get; set; }

    [Required(ErrorMessage = "Expense category is required")]
    public int ExpenseCategoryId { get; set; }

    [Required(ErrorMessage = "Purchase origin type is required")]
    public int PurchaseOriginTypeId { get; set; } = 1;

    [Required(ErrorMessage = "Purchase type is required")]
    public int PurchaseTypeId { get; set; } = 3;

    [MaxLength(100)]
    public string? InvoiceNumber { get; set; }

    [Required(ErrorMessage = "Invoice date is required")]
    public DateOnly InvoiceDate { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Amount excluding VAT is required")]
    public decimal AmountExcludingVat { get; set; }

    public decimal VatAmount { get; set; }

    [MaxLength(100)]
    public string? Country { get; set; }

    public string? Notes { get; set; }

    // Dropdown lists
    public List<Supplier> Suppliers { get; set; } = new();
    public List<ExpenseCategory> ExpenseCategories { get; set; } = new();
    public List<PurchaseOriginType> OriginTypes { get; set; } = new();
    public List<PurchaseType> PurchaseTypes { get; set; } = new();
    public List<ExpenseType> ExpenseTypes { get; set; } = new();
}
