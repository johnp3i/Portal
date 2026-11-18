namespace Portal.Infrastructure.Models;

/// <summary>
/// Per-invoice financial facts read from the database. The only persisted-status information
/// carried is <see cref="IsWrittenOff"/>; overdue and pending are always derived by the calculator.
/// </summary>
public sealed record InvoiceFinancialRow(
    int Id,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    string CurrencyCode,
    decimal TotalAmount,
    decimal TotalPaid,
    DateTime? LastPaymentDateUtc,
    decimal TotalCredited,
    bool IsWrittenOff);

/// <summary>
/// One non-adjustment invoice line of a counted invoice.
/// </summary>
public sealed record ProductLineRow(
    int InvoiceId,
    int LineId,
    DateOnly InvoiceDate,
    string? ProductCode,
    string Description,
    decimal Quantity,
    decimal LineTotal,
    decimal? CostPrice);

/// <summary>
/// Count and total of counted (Issued and Applied) credit notes in the period.
/// </summary>
public sealed record CreditNoteSummaryRow(int Count, decimal Total);

/// <summary>
/// Catalogue list price of an active product.
/// </summary>
public sealed record CatalogPriceRow(string ProductCode, decimal DefaultSellingPrice);

/// <summary>
/// Everything the dashboard needs, read in one batched database call. Lines cover every currency;
/// the service keeps only the lines of the selected currency's invoices.
/// </summary>
public sealed record PurchasesDataSet(
    List<InvoiceFinancialRow> Invoices,
    List<ProductLineRow> Lines,
    IReadOnlyDictionary<string, CreditNoteSummaryRow> CreditByCurrency,
    List<CatalogPriceRow> Catalogue,
    string? PrimaryCurrency);
