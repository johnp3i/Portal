namespace Portal.Infrastructure.Constants;

public static class PortalModules
{
    public const string Customer = "customer";
    public const string Quotation = "quotation";
    public const string Invoice = "invoice";
    public const string Revenue = "revenue";
    public const string Purchase = "purchase";
    public const string Vat = "vat";
    public const string Credit = "credit";
    public const string Audit = "audit";
    public const string Products = "products";

    public static readonly string[] All = { Customer, Quotation, Invoice, Revenue, Purchase, Vat, Credit, Audit, Products };

    public static bool IsValid(string module) => All.Contains(module);
}
