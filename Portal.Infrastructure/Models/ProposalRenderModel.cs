namespace Portal.Infrastructure.Models;

/// <summary>
/// DTO used as the view model for rendering the proposal snapshot Razor view.
/// </summary>
public class ProposalRenderModel
{
    // Business
    public string BusinessName { get; set; } = null!;
    public string CompanyRegistrationNumber { get; set; } = null!;
    public string VatRegistrationNumber { get; set; } = null!;
    public string BusinessAddress { get; set; } = null!;
    public string BusinessEmail { get; set; } = null!;
    public string? BusinessPhone { get; set; }
    public string? BusinessMobile { get; set; }
    public string CurrencySymbol { get; set; } = "€";

    // Customer
    public string CustomerName { get; set; } = null!;
    public string? CustomerContactPerson { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerAddress { get; set; }

    // Quotation header
    public string Reference { get; set; } = null!;
    public DateOnly? ValidUntil { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public bool IsGrandTotalShown { get; set; } = true;

    // Prepared by
    public string? PreparedByName { get; set; }
    public string? PreparedByEmail { get; set; }
    public string? PreparedByPhone { get; set; }

    // Sections with lines
    public List<ProposalSectionRenderModel> Sections { get; set; } = new();

    // Logos
    public List<ProposalLogoRenderModel> HeroLogos { get; set; } = new();
    public ProposalLogoRenderModel? MetaLogo { get; set; }
}

public class ProposalSectionRenderModel
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public string ColumnConfiguration { get; set; } = null!;
    public int SortOrder { get; set; }
    public string SectionType { get; set; } = "LineItems";
    public bool IsEmphasized { get; set; }
    public string? AccentColor { get; set; }
    public string? Label { get; set; }
    public bool IsTotalsTableShown { get; set; }
    public bool IsHalfWidth { get; set; }
    public List<ProposalLineRenderModel> Lines { get; set; } = new();
}

public class ProposalLineRenderModel
{
    public string Description { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; }
    public decimal Discount { get; set; }
    public string DiscountType { get; set; } = "Percentage";
    public decimal LineTotal { get; set; }
    public int SortOrder { get; set; }
    public string? ReferenceUrl { get; set; }
    public string? Subtitle { get; set; }
}

public class ProposalLogoRenderModel
{
    public string DisplayName { get; set; } = null!;
    public string PublicUrl { get; set; } = null!;
}
