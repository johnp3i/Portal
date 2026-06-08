namespace Portal.Web.Configuration;

/// <summary>
/// Platform-level invoicing configuration loaded from appsettings "Invoice" section.
/// Used for invoice number generation, PDF rendering, and CSV export/import.
/// </summary>
public class InvoiceSettings
{
    public const string SectionName = "Invoice";

    /// <summary>
    /// Company name displayed on invoices (e.g. "3 Inventors Ltd").
    /// </summary>
    public string CompanyName { get; set; } = null!;

    /// <summary>
    /// Company address displayed on invoices (e.g. "Nicosia, Cyprus").
    /// </summary>
    public string CompanyAddress { get; set; } = null!;

    /// <summary>
    /// ISO 3166-1 alpha-2 country code (e.g. "CY").
    /// </summary>
    public string CompanyCountryCode { get; set; } = null!;

    /// <summary>
    /// Company VAT registration number (e.g. "10439718W").
    /// </summary>
    public string CompanyVatNumber { get; set; } = null!;

    /// <summary>
    /// Email address for invoice-related correspondence.
    /// </summary>
    public string CompanyEmail { get; set; } = null!;

    /// <summary>
    /// Platform code prefix used in invoice number generation.
    /// Pattern: {PlatformCode}-INV-{yyyy}-{NNNN}
    /// Example: BILI-INV-2026-0001
    /// </summary>
    public string PlatformCode { get; set; } = null!;
}
