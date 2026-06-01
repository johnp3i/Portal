using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Portal.Web.Models.Stripe;

public class SetupWizardModel
{
    [Required(ErrorMessage = "Business name is required")]
    [MaxLength(200, ErrorMessage = "Business name cannot exceed 200 characters")]
    public string BusinessName { get; set; } = null!;

    [MaxLength(50, ErrorMessage = "VAT number cannot exceed 50 characters")]
    public string? VatNumber { get; set; }

    [MaxLength(200)]
    public string? AddressLine1 { get; set; }

    [MaxLength(200)]
    public string? AddressLine2 { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    [MaxLength(100)]
    public string? Country { get; set; }

    [Required(ErrorMessage = "Currency is required")]
    public string CurrencySymbol { get; set; } = "€";

    public IFormFile? Logo { get; set; }
}
