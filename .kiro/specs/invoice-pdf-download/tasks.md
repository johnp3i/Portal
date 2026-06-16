# Tasks: Invoice PDF Download

## Task 1: Create IInvoicePdfService Interface and Implementation

- [x] 1.1 Create `IInvoicePdfService` interface in `Portal.Infrastructure/Services/IInvoicePdfService.cs` with `Task<byte[]> GenerateAsync(int invoiceId, CancellationToken cancellationToken = default)` method
- [x] 1.2 Create `InvoicePdfService` class in `Portal.Web/Services/InvoicePdfService.cs` implementing `IInvoicePdfService`
  - Inject `IInvoiceRenderer`, `IWebHostEnvironment`, `ILogoService`, `ICurrentTenantService`
  - `GenerateAsync` calls `_invoiceRenderer.RenderAsync(invoiceId)` to get HTML
  - Post-process HTML to replace logo `<img src="/uploads/...">` with base64 data URI using `GetLogoAsDataUri` helper
  - Launch PuppeteerSharp with `--no-sandbox`, `--disable-setuid-sandbox` args
  - Set content with `WaitUntilNavigation.Networkidle0`
  - Generate PDF with A4 portrait, `PrintBackground = true`, zero margins (Top/Bottom/Left/Right = "0mm")
  - Respect CancellationToken (throw if cancelled after PDF generation)
- [x] 1.3 Register `IInvoicePdfService` as scoped in `Program.cs`: `builder.Services.AddScoped<IInvoicePdfService, InvoicePdfService>()`

## Task 2: Add Download PDF Controller Action

- [x] 2.1 Add `AxGetDownloadPdf(int id)` action method to `InvoiceController`
  - Decorate with `[HttpGet]`
  - Inject `IInvoicePdfService` into controller constructor (add field `_invoicePdfService`)
  - Inject `ILogger<InvoiceController>` if not already present
  - Validate invoice exists via `_invoiceService.GetInvoiceByIdAsync(id)`
  - Validate invoice belongs to current business (`invoice.BusinessId == _tenantService.CurrentBusinessId`)
  - Return `NotFound()` for missing or cross-tenant invoices
  - Call `_invoicePdfService.GenerateAsync(id, cts.Token)` with a 30-second `CancellationTokenSource`
  - Generate filename using `GenerateInvoicePdfFilename(invoice.InvoiceNumber)`
  - Return `File(pdfBytes, "application/pdf", filename)` on success
  - Catch `OperationCanceledException` → return `StatusCode(500, new { success = false, message = "PDF generation timed out. Please try again." })`
  - Catch `Exception ex` → log error, return `StatusCode(500, new { success = false, message = "Failed to generate PDF. Please try again." })`
- [x] 2.2 Add private static helper `GenerateInvoicePdfFilename(string invoiceNumber)` to `InvoiceController`
  - Remove invalid filename characters: `< > : " / \ | ? *`
  - If sanitized result is empty, return `"INV-download.pdf"`
  - Otherwise return `$"INV-{sanitized}.pdf"`

## Task 3: Add Download PDF Button to Invoice Detail Page

- [x] 3.1 Add "Download PDF" button to the topbar action area in `Views/Invoice/Detail.cshtml`
  - Place after the "Preview" button and before the "Share" button
  - Use: `<button type="button" class="btn btn-secondary" onclick="downloadInvoicePdf(@invoice.Id)">Download PDF</button>`
- [x] 3.2 Add `downloadInvoicePdf(invoiceId)` JavaScript function to the Detail view's script section
  - `BlockUI.show('Generating PDF...')`
  - `fetch('/Invoice/AxGetDownloadPdf/' + invoiceId)` (GET, no antiforgery token needed)
  - Check `response.ok` and `content-type` header contains `application/pdf`
  - On success: create blob URL, create temporary `<a>` element with `download` attribute set to filename from Content-Disposition header, trigger click, cleanup
  - On error: parse JSON body, `BlockUI.hide()`, `Swal.fire({ title: 'Error', text: data.message, icon: 'error', confirmButtonColor: '#0D5EA6' })`
  - On network exception: `BlockUI.hide()`, show generic SweetAlert2 error
  - Always `BlockUI.hide()` on success path (no success dialog — download is the confirmation)

## Task 4: Add PDF Action Link to Invoice Index Page

- [x] 4.1 Add "PDF" action link in the actions column of `Views/Invoice/Index.cshtml`
  - Add after the "Preview" link: `<a href="javascript:void(0)" onclick="downloadInvoicePdf(@item.Id)" class="tbl-action tbl-action--secondary">PDF</a>`
- [x] 4.2 Add `downloadInvoicePdf(invoiceId)` JavaScript function to the Index view's script section
  - Same implementation as Task 3.2 (identical function body)
  - BlockUI.show → fetch → blob download or SweetAlert2 error

## Task 5: Unit Tests

- [x] 5.1 Create test file `Portal.Tests/Unit/Services/InvoicePdfFilenameTests.cs`
  - Test `GenerateInvoicePdfFilename` with normal invoice number (e.g., "1-00090") → returns "INV-1-00090.pdf"
  - Test with invalid chars (e.g., "1/00:090") → returns "INV-100090.pdf"
  - Test with all invalid chars (e.g., "<>:\"|?*") → returns "INV-download.pdf"
  - Test with empty string → returns "INV-download.pdf"
  - Test with whitespace-only string → returns "INV-download.pdf"
- [x] 5.2 Create test file `Portal.Tests/Unit/Controllers/InvoiceControllerDownloadPdfTests.cs`
  - Test `AxGetDownloadPdf` with non-existent invoice ID → returns NotFound
  - Test with invoice belonging to different business → returns NotFound
  - Test with mocked service throwing `OperationCanceledException` → returns 500 JSON with timeout message
  - Test with mocked service throwing generic exception → returns 500 JSON with generic message (no internals)
  - Test success path → returns FileResult with "application/pdf" content type

## Task 6: Property-Based Tests

- [x] 6.1 Add FsCheck.xUnit NuGet package to `Portal.Tests` project (if not already present)
- [x] 6.2 Create `Portal.Tests/Property/InvoicePdfFilenamePropertyTests.cs`
  - Property 1: For any non-empty string that contains at least one valid filename character, the result starts with "INV-" and ends with ".pdf"
    - Tag: `Feature: invoice-pdf-download, Property 1: Invoice PDF filename format`
    - Minimum 100 iterations
  - Property 2: For any arbitrary string, the sanitized filename never contains any of `< > : " / \ | ? *`
    - Tag: `Feature: invoice-pdf-download, Property 2: Filename sanitization removes all invalid characters`
    - Minimum 100 iterations
  - Property 3: For any non-empty byte array and valid MIME type string, encoding to data URI and decoding the base64 portion yields the original bytes
    - Tag: `Feature: invoice-pdf-download, Property 3: Logo data URI encoding round-trip`
    - Minimum 100 iterations


## Task 7: Add PDF Download to Shared Invoice View (Anonymous)

- [x] 7.1 Add `DownloadPdf(string token)` action to `InvoiceViewController`
  - Decorate with `[HttpGet("/invoice-view/{token}/download-pdf")]`
  - Inject `IInvoicePdfService` or PuppeteerSharp dependencies (IWebHostEnvironment, ILogoService) into the controller
  - Validate share token via `_sharingService.GetByTokenAsync(token)`
  - Return `NotFound()` if share is null, inactive, or expired
  - Generate PDF from `share.SnapshotHtml` using PuppeteerSharp (same pattern as `InvoicePdfService.GeneratePdfFromHtmlAsync`)
  - Post-process HTML to embed logo as base64 data URI before PDF generation
  - Extract invoice number from share metadata or HTML for filename generation
  - Return `File(pdfBytes, "application/pdf", filename)` on success
  - Catch `OperationCanceledException` → return `StatusCode(500, new { success = false, message = "PDF generation timed out. Please try again." })`
  - Catch `Exception ex` → log error, return `StatusCode(500, new { success = false, message = "Failed to generate PDF. Please try again." })`
- [x] 7.2 Replace `window.print()` button in `InvoiceViewController.ViewInvoice` with a proper "Download PDF" button
  - Change the button's `onclick` from `window.print()` to `downloadInvoicePdf()`
  - Inject a `<script>` block with the `downloadInvoicePdf()` function that fetches `/invoice-view/{token}/download-pdf`
  - Use the same BlockUI → fetch → blob download → SweetAlert2 error pattern as the authenticated views
  - Ensure BlockUI and SweetAlert2 dependencies are available in the shared view (check if already loaded via the snapshot HTML or inject CDN links)
- [x] 7.3 Add unit tests for the anonymous PDF download endpoint
  - Test with invalid token → returns NotFound
  - Test with expired share → returns NotFound
  - Test with inactive share → returns NotFound
  - Test with valid active share → returns FileResult with "application/pdf" content type
