using System.ComponentModel.DataAnnotations;

namespace Portal.Web.Models;

public class LineItemCatalogEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Description is required")]
    [MaxLength(500)]
    public string Description { get; set; } = null!;

    [Required(ErrorMessage = "Unit price is required")]
    [Range(0, double.MaxValue, ErrorMessage = "Unit price must be a positive value")]
    [Display(Name = "Unit Price")]
    public decimal UnitPrice { get; set; }

    [Required(ErrorMessage = "VAT rate is required")]
    [Range(0, 100, ErrorMessage = "VAT rate must be between 0 and 100")]
    [Display(Name = "VAT Rate (%)")]
    public decimal VatRate { get; set; }

    [MaxLength(2048)]
    [Display(Name = "Reference URL")]
    [Url(ErrorMessage = "Please enter a valid URL")]
    public string? ReferenceUrl { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Discount must be a positive value")]
    public decimal Discount { get; set; }

    [Required(ErrorMessage = "Discount type is required")]
    [Display(Name = "Discount Type")]
    public string DiscountType { get; set; } = "Percentage";
}
