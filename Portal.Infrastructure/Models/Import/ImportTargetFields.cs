namespace Portal.Infrastructure.Models.Import;

/// <summary>
/// Constants for target field names used in column mappings.
/// </summary>
public static class ImportTargetFields
{
    public const string InvoiceDate = "InvoiceDate";
    public const string InvoiceNumber = "InvoiceNumber";
    public const string Description = "Description";
    public const string AmountExcludingVat = "AmountExcludingVat";
    public const string VatAmount = "VatAmount";
    public const string TotalAmount = "TotalAmount";
    public const string PurchaseOriginType = "PurchaseOriginType";
    public const string Country = "Country";
    public const string Notes = "Notes";

    public static readonly string[] Required = { InvoiceDate, AmountExcludingVat };
    public static readonly string[] RequiredAlternate = { InvoiceDate, TotalAmount };

    public static readonly string[] All =
    {
        InvoiceDate, InvoiceNumber, Description, AmountExcludingVat,
        VatAmount, TotalAmount, PurchaseOriginType, Country, Notes
    };
}
