using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Services;
using Portal.Infrastructure.Repositories;

namespace Portal.Web.Services;

/// <summary>
/// Produces a single-worksheet .xlsx for one invoice. Data is gathered the same way the HTML/PDF
/// renderer gathers it (same services, same tenant scoping), and the totals mirror the PDF's
/// grand-total math so the two exports always agree:
///   gross subtotal - line discounts - invoice-level adjustment + VAT = total.
/// </summary>
public class InvoiceExcelService : IInvoiceExcelService
{
    private const string BrandBlue = "#0D5EA6";
    private const string HeaderGrey = "#EEF4F8";

    private readonly IInvoiceService _invoiceService;
    private readonly IInvoiceSectionService _invoiceSectionService;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ICustomerService _customerService;
    private readonly IBusinessService _businessService;
    private readonly BusinessPaymentDetailRepository _paymentDetailRepository;
    private readonly ILogger<InvoiceExcelService> _logger;

    public InvoiceExcelService(
        IInvoiceService invoiceService,
        IInvoiceSectionService invoiceSectionService,
        ICurrentTenantService currentTenantService,
        ICustomerService customerService,
        IBusinessService businessService,
        BusinessPaymentDetailRepository paymentDetailRepository,
        ILogger<InvoiceExcelService> logger)
    {
        _invoiceService = invoiceService;
        _invoiceSectionService = invoiceSectionService;
        _currentTenantService = currentTenantService;
        _customerService = customerService;
        _businessService = businessService;
        _paymentDetailRepository = paymentDetailRepository;
        _logger = logger;
    }

    public async Task<byte[]> GenerateAsync(int invoiceId, CancellationToken cancellationToken = default)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;

            var invoice = await _invoiceService.GetInvoiceByIdAsync(invoiceId, businessId)
                ?? throw new InvalidOperationException($"Invoice {invoiceId} not found.");

            var lines = await _invoiceService.GetInvoiceLinesAsync(invoiceId);
            var sections = await _invoiceSectionService.GetByInvoiceIdAsync(invoiceId);
            var customer = await _customerService.GetCustomerByIdAsync(invoice.CustomerId, businessId);
            var business = await _businessService.GetBusinessByIdAsync(businessId);
            var profile = await _businessService.GetBusinessProfileAsync(businessId);
            var paymentDetails = await _paymentDetailRepository.GetByBusinessIdAsync(businessId);

            cancellationToken.ThrowIfCancellationRequested();

            var currency = profile?.CurrencySymbol ?? "€";
            var money = $"{currency}#,##0.00";

            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Invoice");
            sheet.Style.Font.FontName = "Calibri";
            sheet.Style.Font.FontSize = 11;

            var row = 1;

            // ── Business header block ──
            sheet.Cell(row, 1).Value = business?.Name ?? "";
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 1).Style.Font.FontSize = 16;
            sheet.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml(BrandBlue);
            row++;

            if (profile != null)
            {
                var addressParts = new List<string> { profile.AddressLine1 };
                if (!string.IsNullOrWhiteSpace(profile.AddressLine2)) addressParts.Add(profile.AddressLine2);
                addressParts.Add($"{profile.City}, {profile.PostalCode}");
                sheet.Cell(row++, 1).Value = string.Join(", ", addressParts);

                if (!string.IsNullOrWhiteSpace(profile.VatRegistrationNumber))
                    sheet.Cell(row++, 1).Value = $"VAT/TIC: {profile.VatRegistrationNumber}";

                var contact = profile.TelephoneNumber ?? profile.MobileNumber;
                if (!string.IsNullOrWhiteSpace(contact))
                    sheet.Cell(row++, 1).Value = contact;
                if (!string.IsNullOrWhiteSpace(profile.Email))
                    sheet.Cell(row++, 1).Value = profile.Email;
            }

            row++; // spacer

            // ── Invoice title + meta ──
            sheet.Cell(row, 1).Value = "INVOICE";
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 1).Style.Font.FontSize = 20;
            sheet.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml(BrandBlue);
            sheet.Cell(row, 5).Value = invoice.InvoiceNumber;
            sheet.Cell(row, 5).Style.Font.Bold = true;
            sheet.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            row += 2;

            var statusText = invoice.InvoiceStatusTypeId == 1 ? "Draft"
                : invoice.InvoiceStatusTypeId == 2 ? "Issued" : "Cancelled";

            WriteMetaPair(sheet, ref row, "Bill To", customer?.Name ?? "Unknown");
            WriteMetaPair(sheet, ref row, "Invoice Date", invoice.InvoiceDate.ToString("dd MMM yyyy"));
            WriteMetaPair(sheet, ref row, "Due Date", invoice.DueDate.ToString("dd MMM yyyy"));
            WriteMetaPair(sheet, ref row, "Status", statusText);
            WriteMetaPair(sheet, ref row, "Currency", invoice.CurrencyCode);
            if (invoice.QuotationId.HasValue && invoice.IsQuotationReferenceShown)
                WriteMetaPair(sheet, ref row, "Quotation Ref", $"QUO-{invoice.BusinessId}-{invoice.QuotationId.Value:D5}");

            row++; // spacer

            // Only priced lines appear in the item tables; the adjustment line (a document-level
            // discount) is surfaced in the totals block, exactly like the PDF.
            var normalLines = lines.Where(l => !l.IsAdjustmentLine).ToList();
            var adjustmentLine = lines.FirstOrDefault(l => l.IsAdjustmentLine);

            // ── Line items, grouped by section (unsectioned lines first under "General") ──
            var orderedSections = sections.OrderBy(s => s.SortOrder)
                .Where(s => s.SectionType != "Narrative")
                .ToList();

            var unsectioned = normalLines.Where(l => l.InvoiceSectionId == null)
                .OrderBy(l => l.SortOrder).ToList();
            if (unsectioned.Any())
                WriteSection(sheet, ref row, "General", unsectioned, currency, money);

            foreach (var section in orderedSections)
            {
                var secLines = normalLines.Where(l => l.InvoiceSectionId == section.Id)
                    .OrderBy(l => l.SortOrder).ToList();
                if (secLines.Any())
                {
                    var title = string.IsNullOrWhiteSpace(section.Label)
                        ? section.Name
                        : $"{section.Name} — {section.Label}";
                    WriteSection(sheet, ref row, title, secLines, currency, money);
                }
            }

            // ── Grand totals (mirror the PDF math) ──
            if (invoice.IsGrandTotalShown)
            {
                var grossSubtotal = normalLines.Sum(l => l.Quantity * l.UnitPrice);
                var netSubtotal = normalLines.Sum(l => l.LineTotal);
                var lineDiscounts = grossSubtotal - netSubtotal;
                var invoiceDiscount = adjustmentLine != null ? Math.Abs(adjustmentLine.LineTotal) : 0m;
                var vat = Math.Round(normalLines.Sum(l => l.LineTotal * l.VatRate / 100m), 2);

                row++;
                var totalsLabelCol = 4;
                var totalsValueCol = 5;

                WriteTotalRow(sheet, ref row, totalsLabelCol, totalsValueCol, "Subtotal", grossSubtotal, money, bold: false);
                if (lineDiscounts > 0)
                    WriteTotalRow(sheet, ref row, totalsLabelCol, totalsValueCol, "Line Discounts", -lineDiscounts, money, bold: false, green: true);
                if (adjustmentLine != null)
                    WriteTotalRow(sheet, ref row, totalsLabelCol, totalsValueCol, adjustmentLine.Description, -invoiceDiscount, money, bold: false, green: true);
                WriteTotalRow(sheet, ref row, totalsLabelCol, totalsValueCol, "Tax", vat, money, bold: false);
                WriteTotalRow(sheet, ref row, totalsLabelCol, totalsValueCol, "Total", invoice.TotalAmount, money, bold: true);
            }

            // ── Notes ──
            if (!string.IsNullOrWhiteSpace(invoice.Notes))
            {
                row += 2;
                sheet.Cell(row, 1).Value = "Notes";
                sheet.Cell(row, 1).Style.Font.Bold = true;
                row++;
                sheet.Cell(row, 1).Value = invoice.Notes;
                sheet.Range(row, 1, row, 5).Merge().Style.Alignment.WrapText = true;
                row++;
            }

            // ── Signature section ──
            // We intentionally render the signature *structure* (labels + a blank area for a wet
            // signature + the receiving party), not the digital signature image: embedding a
            // floating image in a worksheet cell is unreliable and sizes poorly. The section mirrors
            // the PDF's "Issued by" / "Received by" layout.
            {
                row += 2;
                sheet.Cell(row, 1).Value = "Signature";
                sheet.Cell(row, 1).Style.Font.Bold = true;
                sheet.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml(BrandBlue);
                row++;

                // Labels
                sheet.Cell(row, 1).Value = "Issued by";
                sheet.Cell(row, 1).Style.Font.Italic = true;
                sheet.Cell(row, 1).Style.Font.FontColor = XLColor.Gray;
                sheet.Cell(row, 4).Value = "Received by";
                sheet.Cell(row, 4).Style.Font.Italic = true;
                sheet.Cell(row, 4).Style.Font.FontColor = XLColor.Gray;
                row++;

                // Vertical space for a handwritten signature
                row += 2;

                // Signature lines (bottom border) under each column pair
                var issuedLine = sheet.Range(row, 1, row, 2);
                issuedLine.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                var receivedLine = sheet.Range(row, 4, row, 5);
                receivedLine.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                row++;

                // Names beneath the lines: business issues, customer receives
                sheet.Cell(row, 1).Value = business?.Name ?? "";
                sheet.Cell(row, 1).Style.Font.Bold = true;
                sheet.Cell(row, 4).Value = customer?.Name ?? "";
                sheet.Cell(row, 4).Style.Font.Bold = true;
                row++;
            }

            // ── Payment details ──
            var activePayments = paymentDetails.Where(p => p.IsActive).OrderBy(p => p.SortOrder).ToList();
            if (activePayments.Any())
            {
                row += 2;
                sheet.Cell(row, 1).Value = "Payment Details";
                sheet.Cell(row, 1).Style.Font.Bold = true;
                sheet.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml(BrandBlue);
                row++;

                // The IBAN is long and must always print in full, so it gets its own merged span
                // (columns B:C) with wrap text — this both decouples it from the narrow Qty column
                // (so Qty no longer stretches to the IBAN width) and guarantees the full IBAN is
                // visible on screen and on paper (it wraps rather than clipping if ever too long).
                //   Bank = A | IBAN = B:C (merged) | SWIFT/BIC = D | Payee = E
                sheet.Cell(row, 1).Value = "Bank";
                sheet.Cell(row, 2).Value = "IBAN";
                sheet.Cell(row, 4).Value = "SWIFT/BIC";
                sheet.Cell(row, 5).Value = "Payee";
                sheet.Range(row, 1, row, 5).Style.Font.Bold = true;
                sheet.Range(row, 1, row, 5).Style.Fill.BackgroundColor = XLColor.FromHtml(HeaderGrey);
                sheet.Range(row, 2, row, 3).Merge();
                row++;

                foreach (var pd in activePayments)
                {
                    sheet.Cell(row, 1).Value = pd.BankName;

                    var ibanCell = sheet.Cell(row, 2);
                    ibanCell.SetValue(pd.Iban);
                    ibanCell.Style.NumberFormat.Format = "@"; // keep IBAN as text (no number coercion)
                    sheet.Range(row, 2, row, 3).Merge();      // give the IBAN a dedicated wide span
                    ibanCell.Style.Alignment.WrapText = true; // never clip — wrap if it overflows

                    sheet.Cell(row, 4).Value = pd.SwiftBic ?? "";
                    sheet.Cell(row, 5).Value = pd.PayeeName;
                    row++;
                }
            }

            // Auto-fit first (merged cells like the IBAN span are ignored by AdjustToContents, so
            // the long IBAN no longer dictates the Qty column width), then pin deterministic widths
            // so the layout is stable regardless of content:
            //   A Description (wide) | B Qty (narrow) | C Unit Price | D VAT% | E Discount | F Total
            // B and C together form the IBAN span in the payment rows, so their combined width must
            // comfortably hold a full IBAN (~34 chars incl. spaces).
            sheet.Columns(1, 6).AdjustToContents();
            if (sheet.Column(1).Width < 36) sheet.Column(1).Width = 36; // Description
            sheet.Column(2).Width = 10;  // Qty  (short values — safe to fix)
            sheet.Column(3).Width = 24;  // Unit Price; with B(10) gives IBAN span ~34 chars
            sheet.Column(4).Width = 12;  // VAT %
            sheet.Column(5).Width = 14;  // Discount / Payee
            sheet.Column(6).Width = 14;  // Total

            cancellationToken.ThrowIfCancellationRequested();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate Excel export for invoice {InvoiceId}", invoiceId);
            throw;
        }
    }

    private static void WriteMetaPair(IXLWorksheet sheet, ref int row, string label, string value)
    {
        sheet.Cell(row, 1).Value = label;
        sheet.Cell(row, 1).Style.Font.FontColor = XLColor.Gray;
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 2).Value = value;
        row++;
    }

    private static void WriteSection(
        IXLWorksheet sheet, ref int row, string title,
        List<InvoiceLine> secLines, string currency, string money)
    {
        // Section title
        sheet.Cell(row, 1).Value = title;
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml(BrandBlue);
        row++;

        // Column header
        var headers = new[] { "Description", "Qty", "Unit Price", "VAT %", "Discount", "Total" };
        for (int c = 0; c < headers.Length; c++)
            sheet.Cell(row, c + 1).Value = headers[c];
        var headerRange = sheet.Range(row, 1, row, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml(HeaderGrey);
        row++;

        var firstDataRow = row;
        foreach (var line in secLines)
        {
            var description = string.IsNullOrWhiteSpace(line.Subtitle)
                ? line.Description
                : $"{line.Description} ({line.Subtitle})";

            sheet.Cell(row, 1).Value = description;
            sheet.Cell(row, 2).Value = line.Quantity;
            sheet.Cell(row, 3).Value = line.UnitPrice;
            sheet.Cell(row, 3).Style.NumberFormat.Format = money;
            sheet.Cell(row, 4).Value = line.VatRate / 100m;
            sheet.Cell(row, 4).Style.NumberFormat.Format = "0.00%";
            sheet.Cell(row, 5).Value = line.Discount > 0
                ? (line.DiscountType == "Percentage" ? $"{line.Discount:0.##}%" : $"{currency}{line.Discount:0.00}")
                : "—";
            sheet.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            sheet.Cell(row, 6).Value = line.LineTotal;
            sheet.Cell(row, 6).Style.NumberFormat.Format = money;
            row++;
        }

        // Section totals
        var secSubtotal = secLines.Sum(l => l.LineTotal);
        var secVat = secLines.Sum(l => l.LineTotal * l.VatRate / 100m);
        var secTotal = secSubtotal + secVat;

        sheet.Cell(row, 5).Value = "Section Subtotal";
        sheet.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        sheet.Cell(row, 6).Value = secSubtotal;
        sheet.Cell(row, 6).Style.NumberFormat.Format = money;
        row++;
        sheet.Cell(row, 5).Value = "Section VAT";
        sheet.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        sheet.Cell(row, 6).Value = secVat;
        sheet.Cell(row, 6).Style.NumberFormat.Format = money;
        row++;
        sheet.Cell(row, 5).Value = "Section Total";
        sheet.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        sheet.Cell(row, 5).Style.Font.Bold = true;
        sheet.Cell(row, 6).Value = secTotal;
        sheet.Cell(row, 6).Style.NumberFormat.Format = money;
        sheet.Cell(row, 6).Style.Font.Bold = true;
        row++;

        // Thin border under the item rows for readability
        sheet.Range(firstDataRow, 1, row - 1, 6).Style.Border.BottomBorder = XLBorderStyleValues.Hair;

        row++; // spacer between sections
    }

    private static void WriteTotalRow(
        IXLWorksheet sheet, ref int row, int labelCol, int valueCol,
        string label, decimal value, string money, bool bold, bool green = false)
    {
        var labelCell = sheet.Cell(row, labelCol);
        labelCell.Value = label;
        labelCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        labelCell.Style.Font.Bold = bold;

        var valueCell = sheet.Cell(row, valueCol);
        valueCell.Value = value;
        valueCell.Style.NumberFormat.Format = money;
        valueCell.Style.Font.Bold = bold;

        if (green)
        {
            labelCell.Style.Font.FontColor = XLColor.FromHtml("#129867");
            valueCell.Style.Font.FontColor = XLColor.FromHtml("#129867");
        }
        if (bold)
        {
            labelCell.Style.Font.FontSize = 13;
            valueCell.Style.Font.FontSize = 13;
        }
        row++;
    }
}
