using System.Globalization;
using ClosedXML.Excel;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;

namespace Portal.Web.Services;

/// <summary>
/// Produces the Customer Purchases .xlsx (sheets "Product mix" and "Pending invoices").
/// Data comes from <see cref="ICustomerPurchasesService.GetDashboardAsync"/>, which applies the tenant check.
/// </summary>
public class CustomerPurchasesExcelService : ICustomerPurchasesExcelService
{
    private const string HeaderGrey = "#EEF4F8";
    private const string MoneyFormat = "#,##0.00";
    private const string PercentFormat = "0.0%";
    private const string DateFormat = "yyyy-mm-dd";

    private readonly ICustomerPurchasesService _purchasesService;
    private readonly ILogger<CustomerPurchasesExcelService> _logger;

    public CustomerPurchasesExcelService(
        ICustomerPurchasesService purchasesService,
        ILogger<CustomerPurchasesExcelService> logger)
    {
        _purchasesService = purchasesService;
        _logger = logger;
    }

    public async Task<(byte[] Content, string FileName)?> GenerateAsync(int customerId, PurchasesQuery query, CancellationToken ct)
    {
        try
        {
            var dto = await _purchasesService.GetDashboardAsync(customerId, query);
            if (dto is null)
            {
                return null;
            }

            ct.ThrowIfCancellationRequested();

            var generatedAtUtc = DateTime.UtcNow;
            var periodText = BuildPeriodText(dto.Period);
            var currencyText = dto.SelectedCurrency ?? "-";

            using var workbook = new XLWorkbook();

            WriteProductMix(workbook.Worksheets.Add("Product mix"), dto, periodText, currencyText, generatedAtUtc);
            WritePendingInvoices(workbook.Worksheets.Add("Pending invoices"), dto, periodText, currencyText, generatedAtUtc);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            return (stream.ToArray(), ExportFileName.Build(dto.CustomerName, generatedAtUtc));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate purchases Excel export for customer {CustomerId}", customerId);
            throw;
        }
    }

    private static string BuildPeriodText(PeriodDto period)
    {
        var label = period.Key switch
        {
            PurchasesQuery.PeriodLast12Months => "Last 12 months",
            PurchasesQuery.PeriodThisYear => "This year",
            PurchasesQuery.PeriodLastYear => "Last year",
            PurchasesQuery.PeriodAll => "All time",
            PurchasesQuery.PeriodCustom => "Custom",
            _ => period.Key
        };

        if (period.From.HasValue && period.To.HasValue)
        {
            return $"{label} ({period.From.Value:yyyy-MM-dd} to {period.To.Value:yyyy-MM-dd})";
        }

        return label;
    }

    /// <summary>Writes the 4 context lines and returns the next free row (after one blank row).</summary>
    private static int WriteContext(IXLWorksheet sheet, PurchasesDashboardDto dto, string periodText, string currencyText, DateTime generatedAtUtc)
    {
        sheet.Style.Font.FontName = "Calibri";
        sheet.Style.Font.FontSize = 11;

        var row = 1;
        sheet.Cell(row, 1).Value = "Customer";
        sheet.Cell(row, 2).Value = dto.CustomerName;
        row++;
        sheet.Cell(row, 1).Value = "Period";
        sheet.Cell(row, 2).Value = periodText;
        row++;
        sheet.Cell(row, 1).Value = "Currency";
        sheet.Cell(row, 2).Value = currencyText;
        row++;
        sheet.Cell(row, 1).Value = "Generated (UTC)";
        sheet.Cell(row, 2).Value = generatedAtUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
        sheet.Range(1, 1, row, 1).Style.Font.Bold = true;
        sheet.Range(1, 2, row, 2).Style.NumberFormat.Format = "@";

        return row + 2;
    }

    private static void WriteHeader(IXLWorksheet sheet, int row, string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(row, i + 1).Value = headers[i];
        }

        var range = sheet.Range(row, 1, row, headers.Length);
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml(HeaderGrey);
    }

    private static void SetMoney(IXLCell cell, decimal? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        cell.Value = value.Value;
        cell.Style.NumberFormat.Format = MoneyFormat;
    }

    private static void SetPercent(IXLCell cell, decimal? percentPoints)
    {
        if (!percentPoints.HasValue)
        {
            return;
        }

        cell.Value = percentPoints.Value / 100m;
        cell.Style.NumberFormat.Format = PercentFormat;
    }

    private static void SetDate(IXLCell cell, DateOnly date)
    {
        cell.Value = date.ToDateTime(TimeOnly.MinValue);
        cell.Style.NumberFormat.Format = DateFormat;
    }

    private static void WriteProductMix(IXLWorksheet sheet, PurchasesDashboardDto dto, string periodText, string currencyText, DateTime generatedAtUtc)
    {
        var row = WriteContext(sheet, dto, periodText, currencyText, generatedAtUtc);
        var headers = new[] { "Product", "Code", "Qty", "Revenue", "Share", "Avg price paid", "List price", "Difference %", "Last bought", "Margin" };
        WriteHeader(sheet, row, headers);
        row++;

        foreach (var product in dto.ProductMix.Rows)
        {
            sheet.Cell(row, 1).Value = product.DisplayName;
            if (!string.IsNullOrEmpty(product.Code))
            {
                sheet.Cell(row, 2).Value = product.Code;
            }

            sheet.Cell(row, 3).Value = product.Quantity;
            sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0.##";
            SetMoney(sheet.Cell(row, 4), product.Revenue);
            SetPercent(sheet.Cell(row, 5), product.SharePercent);
            SetMoney(sheet.Cell(row, 6), product.AveragePricePaid);
            SetMoney(sheet.Cell(row, 7), product.ListPrice);
            SetPercent(sheet.Cell(row, 8), product.PriceDifferencePercent);
            SetDate(sheet.Cell(row, 9), product.LastBought);
            SetPercent(sheet.Cell(row, 10), product.MarginPercent);
            row++;
        }

        sheet.Columns(1, headers.Length).AdjustToContents();
        sheet.Column(1).Width = Math.Min(sheet.Column(1).Width, 60);
        sheet.SheetView.FreezeRows(row - dto.ProductMix.Rows.Count - 1);
    }

    private static void WritePendingInvoices(IXLWorksheet sheet, PurchasesDashboardDto dto, string periodText, string currencyText, DateTime generatedAtUtc)
    {
        var row = WriteContext(sheet, dto, periodText, currencyText, generatedAtUtc);
        var headers = new[] { "Invoice", "Issued", "Due", "Total", "Paid", "Credit notes", "Outstanding", "Status" };
        WriteHeader(sheet, row, headers);
        var headerRow = row;
        row++;

        foreach (var invoice in dto.PendingInvoices)
        {
            sheet.Cell(row, 1).Value = invoice.InvoiceNumber;
            SetDate(sheet.Cell(row, 2), invoice.IssueDate);
            SetDate(sheet.Cell(row, 3), invoice.DueDate);
            SetMoney(sheet.Cell(row, 4), invoice.Total);
            SetMoney(sheet.Cell(row, 5), invoice.Paid);
            SetMoney(sheet.Cell(row, 6), invoice.CreditNotes);
            SetMoney(sheet.Cell(row, 7), invoice.Outstanding);
            sheet.Cell(row, 8).Value = invoice.StatusText;
            row++;
        }

        // Pending total row
        sheet.Cell(row, 1).Value = "Total pending";
        SetMoney(sheet.Cell(row, 7), dto.PendingTotal);
        var totalRange = sheet.Range(row, 1, row, headers.Length);
        totalRange.Style.Font.Bold = true;
        totalRange.Style.Border.TopBorder = XLBorderStyleValues.Thin;

        sheet.Columns(1, headers.Length).AdjustToContents();
        sheet.SheetView.FreezeRows(headerRow);
    }
}

/// <summary>Builds the download file name for the purchases export.</summary>
public static class ExportFileName
{
    public static string Build(string? customerName, DateTime utcDate)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string((customerName ?? "").Where(c => !invalid.Contains(c)).ToArray()).Trim();
        if (cleaned.Length == 0)
        {
            cleaned = "Customer";
        }

        return $"Purchases_{cleaned}_{utcDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.xlsx";
    }
}
