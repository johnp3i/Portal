using System.ComponentModel.DataAnnotations;

namespace Portal.Web.Models;

public class QuotationLineFormViewModel
{
    [Required(ErrorMessage = "Description is required")]
    [MaxLength(500)]
    public string Description { get; set; } = null!;

    [Required]
    [Range(0.0001, double.MaxValue, ErrorMessage = "Quantity must be greater than zero")]
    public decimal Quantity { get; set; }

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "Unit price must be zero or greater")]
    public decimal UnitPrice { get; set; }

    [Required]
    [Range(0, 100, ErrorMessage = "VAT rate must be between 0 and 100")]
    public decimal VatRate { get; set; }

    [Range(0, 100, ErrorMessage = "Discount must be between 0 and 100")]
    public decimal Discount { get; set; }

    public string DiscountType { get; set; } = "Percentage";

    [Range(0, double.MaxValue, ErrorMessage = "Cost price must be zero or greater")]
    public decimal? CostPrice { get; set; }

    [MaxLength(2048)]
    [Url(ErrorMessage = "Reference URL must be a valid URL")]
    public string? ReferenceUrl { get; set; }

    [MaxLength(1000)]
    public string? Subtitle { get; set; }

    [MaxLength(50)]
    public string? ProductCode { get; set; }
}
