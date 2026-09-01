using System.Text;
using ClosedXML.Excel;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Builds CSV and Excel import templates for the External Platform Sales Import canonical contract.
/// </summary>
public class ImportTemplateService : IImportTemplateService
{
    private const string PlaceholderCode = "ABC";
    private const string CsvContentType = "text/csv";
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string CsvFileName = "external-sales-import-template.csv";
    private const string XlsxFileName = "external-sales-import-template.xlsx";

    // Canonical column order
    private static readonly string[] Headers =
    {
        "InvoiceNumber", "InvoiceDate", "NetAmount", "VatAmount", "TotalAmount",
        "VatRate", "CustomerName", "Description", "PaymentMethod", "Currency"
    };

    // Per-column reference for the Excel "Instructions" sheet: name, required, type, notes
    private static readonly (string Name, string Required, string Type, string Notes)[] ColumnInfo =
    {
        ("InvoiceNumber", "Required", "Text", "Format {PlatformCode}-INV-yyyy-NNNN, e.g. GRD-INV-2026-0001"),
        ("InvoiceDate", "Required", "Date", "ISO format yyyy-MM-dd"),
        ("NetAmount", "Required", "Decimal", "Amount excluding VAT. Use '.' as decimal separator. >= 0"),
        ("VatAmount", "Required", "Decimal", "VAT portion. Use '.' as decimal separator. >= 0"),
        ("TotalAmount", "Required", "Decimal", "Must equal NetAmount + VatAmount"),
        ("VatRate", "Optional", "Decimal", "VAT rate applied, e.g. 19 or 0"),
        ("CustomerName", "Optional", "Text", "Free text, informational"),
        ("Description", "Optional", "Text", "Up to 500 characters"),
        ("PaymentMethod", "Optional", "Text", "e.g. card, bank_transfer (up to 50 chars)"),
        ("Currency", "Optional", "Text", "ISO 4217, e.g. EUR (informational this phase)")
    };

    public (byte[] Content, string FileName, string ContentType) BuildCsvTemplate(string? platformCode)
    {
        var code = ResolveCode(platformCode);
        var rows = BuildExampleRows(code);

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", Headers));
        foreach (var row in rows)
            sb.AppendLine(string.Join(",", row));

        // UTF-8 without BOM
        var content = new UTF8Encoding(false).GetBytes(sb.ToString());
        return (content, CsvFileName, CsvContentType);
    }

    public (byte[] Content, string FileName, string ContentType) BuildExcelTemplate(string? platformCode)
    {
        var code = ResolveCode(platformCode);
        var rows = BuildExampleRows(code);

        using var workbook = new XLWorkbook();

        // ── Sheet 1: Sales ──
        var sheet = workbook.Worksheets.Add("Sales");

        // Header row
        for (int c = 0; c < Headers.Length; c++)
            sheet.Cell(1, c + 1).Value = Headers[c];

        var headerRange = sheet.Range(1, 1, 1, Headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#0D5EA6");
        headerRange.Style.Font.FontColor = XLColor.White;

        // Force the InvoiceNumber column (column 1) to text so codes aren't coerced
        sheet.Column(1).Style.NumberFormat.Format = "@";

        // Example rows
        for (int r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            for (int c = 0; c < row.Length; c++)
            {
                var cell = sheet.Cell(r + 2, c + 1);
                if (c == 0)
                    cell.SetValue(row[c]); // keep invoice number as text
                else
                    cell.Value = row[c];
            }
        }

        sheet.Columns().AdjustToContents();

        // ── Sheet 2: Instructions ──
        var info = workbook.Worksheets.Add("Instructions");
        info.Cell(1, 1).Value = "Column";
        info.Cell(1, 2).Value = "Required";
        info.Cell(1, 3).Value = "Type";
        info.Cell(1, 4).Value = "Notes";

        var infoHeader = info.Range(1, 1, 1, 4);
        infoHeader.Style.Font.Bold = true;
        infoHeader.Style.Fill.BackgroundColor = XLColor.FromHtml("#0D5EA6");
        infoHeader.Style.Font.FontColor = XLColor.White;

        for (int i = 0; i < ColumnInfo.Length; i++)
        {
            var (name, required, type, notes) = ColumnInfo[i];
            info.Cell(i + 2, 1).Value = name;
            info.Cell(i + 2, 2).Value = required;
            info.Cell(i + 2, 3).Value = type;
            info.Cell(i + 2, 4).Value = notes;
        }

        info.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return (stream.ToArray(), XlsxFileName, XlsxContentType);
    }

    private static string ResolveCode(string? platformCode)
    {
        if (string.IsNullOrWhiteSpace(platformCode))
            return PlaceholderCode;
        return platformCode.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Two contract-valid example rows: a standard 19% VAT sale and a zero-VAT sale.
    /// Order matches <see cref="Headers"/>.
    /// </summary>
    private static List<string[]> BuildExampleRows(string code)
    {
        return new List<string[]>
        {
            new[] { $"{code}-INV-2026-0001", "2026-08-01", "100.00", "19.00", "119.00", "19", "Acme Ltd", "Consulting services", "bank_transfer", "EUR" },
            new[] { $"{code}-INV-2026-0002", "2026-08-02", "80.00",  "0.00",  "80.00",  "0",  "Gamma NGO", "Exempt supply",       "card",          "EUR" }
        };
    }
}
