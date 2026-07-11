using System.ComponentModel.DataAnnotations;

namespace Portal.Web.Models;

public class BulkPurchaseRowDto
{
    [Required]
    public DateOnly InvoiceDate { get; set; }

    [MaxLength(100)]
    public string? InvoiceNumber { get; set; }

    [Required]
    public int SupplierId { get; set; }

    [Required]
    public int ExpenseCategoryId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = null!;

    [Required]
    public decimal AmountExcludingVat { get; set; }

    public decimal VatAmount { get; set; }

    public int PurchaseOriginTypeId { get; set; } = 1;

    public int PurchaseTypeId { get; set; } = 3;

    [MaxLength(100)]
    public string? Country { get; set; }

    public int? VatSubmissionPeriodId { get; set; }
}
