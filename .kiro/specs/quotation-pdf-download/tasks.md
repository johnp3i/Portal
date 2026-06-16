# Implementation Plan: Quotation PDF Download

## Overview

This implementation adds PDF download capability for quotation proposals across three access points: authenticated detail page, authenticated index page, and anonymous shared proposal view. The design mirrors the existing Invoice PDF Download pattern, introducing `IProposalPdfService` for decoupled PDF generation using PuppeteerSharp.

## Tasks

- [x] 1. Create IProposalPdfService interface and implementation
  - [x] 1.1 Create `IProposalPdfService` interface in `Portal.Infrastructure/Services/IProposalPdfService.cs`
    - Define `Task<byte[]> GenerateAsync(int quotationId, List<int> heroLogoIds, int? metaLogoId, CancellationToken cancellationToken = default)`
    - Mirror the `IInvoicePdfService` interface pattern with XML doc comments
    - _Requirements: 1.1_

  - [x] 1.2 Create `ProposalPdfService` class in `Portal.Web/Services/ProposalPdfService.cs`
    - Inject `IProposalService`, `IWebHostEnvironment`, `ILogoService`, `ICurrentTenantService`
    - `GenerateAsync` calls `_proposalService.PreviewAsync(quotationId, heroLogoIds, metaLogoId)` to get HTML
    - Post-process HTML: replace `<img src="/uploads/...">` with base64 data URIs using regex pattern `(<img\s[^>]*src\s*=\s*"")(/uploads/[^""]+)("")`
    - Remove `.download-bar` element from HTML using regex: `<div class="download-bar">[\s\S]*?</div>`
    - Launch PuppeteerSharp with `--no-sandbox`, `--disable-setuid-sandbox` args
    - Set content with `WaitUntilNavigation.Networkidle0`
    - Generate PDF: A4 portrait, `PrintBackground = true`, zero margins (Top/Bottom/Left/Right = "0mm")
    - Respect CancellationToken (throw if cancelled after PDF generation)
    - If logo file does not exist on disk, leave image tag unchanged and continue
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 1.9, 1.10, 4.1, 4.2, 4.4, 4.5, 4.6, 4.7_

  - [x] 1.3 Register `IProposalPdfService` as scoped in `Program.cs`
    - Add `builder.Services.AddScoped<IProposalPdfService, ProposalPdfService>()`
    - _Requirements: 1.1_

- [x] 2. Add authenticated PDF download controller action
  - [x] 2.1 Add `IProposalPdfService` and `ILogger<QuotationController>` injection to `QuotationController`
    - Add private readonly fields `_proposalPdfService` and `_logger`
    - Add parameters to the constructor
    - _Requirements: 2.1_

  - [x] 2.2 Add `AxGetDownloadPdf(int id)` action method to `QuotationController`
    - Decorate with `[HttpGet]`
    - Validate quotation exists via `_quotationService.GetQuotationByIdAsync(id)`
    - Validate quotation belongs to current business (`quotation.BusinessId == _tenantService.CurrentBusinessId`)
    - Return `NotFound()` for missing or cross-tenant quotations
    - Resolve primary logo: get logos via `_logoService.GetByBusinessIdAsync()`, find primary, pass as hero list and meta logo
    - If no primary logo, pass empty list and null meta logo
    - Call `_proposalPdfService.GenerateAsync(id, heroLogoIds, metaLogoId, cts.Token)` with 30-second `CancellationTokenSource`
    - Generate filename using `GenerateProposalPdfFilename(quotation.Reference)`
    - Return `File(pdfBytes, "application/pdf", filename)` on success
    - Catch `OperationCanceledException` → return `StatusCode(500, new { success = false, message = "PDF generation timed out. Please try again." })`
    - Catch `Exception ex` → log error, return `StatusCode(500, new { success = false, message = "Failed to generate PDF. Please try again." })`
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 2.9, 2.10_

  - [x] 2.3 Add private static helper `GenerateProposalPdfFilename(string reference)` to `QuotationController`
    - Remove invalid filename characters: `< > : " / \ | ? *` and ASCII control characters (0x00–0x1F)
    - Trim leading/trailing whitespace, spaces, and dots from the sanitized reference
    - If sanitized result is empty or whitespace-only, return `"QUO-download.pdf"`
    - Otherwise return `$"QUO-{sanitized}.pdf"`
    - _Requirements: 5.1, 5.2, 5.3_

- [x] 3. Checkpoint - Ensure service and controller compile
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Add Download PDF button to Quotation Detail page
  - [x] 4.1 Add "Download PDF" button to the action area in `Views/Quotation/Detail.cshtml`
    - Place alongside existing action buttons
    - Use: `<button type="button" class="btn btn-secondary" onclick="downloadQuotationPdf(@quotation.Id)">Download PDF</button>`
    - _Requirements: 3.1_

  - [x] 4.2 Add `downloadQuotationPdf(quotationId)` JavaScript function to the Detail view's script section
    - `BlockUI.show('Generating PDF...')`
    - `fetch('/Quotation/AxGetDownloadPdf/' + quotationId)` (GET request)
    - Check `response.ok` and `content-type` header contains `application/pdf`
    - On success: create blob URL, create temporary `<a>` element with `download` attribute set to filename from Content-Disposition header, trigger click, cleanup, `BlockUI.hide()`
    - On error (non-OK or non-PDF content type): `BlockUI.hide()`, parse JSON body, `Swal.fire({ title: 'Error', text: data.message || 'Failed to generate PDF.', icon: 'error', confirmButtonColor: '#0D5EA6' })`
    - On network exception: `BlockUI.hide()`, `Swal.fire({ title: 'Error', text: 'Could not complete download due to a connection problem.', icon: 'error', confirmButtonColor: '#0D5EA6' })`
    - _Requirements: 3.2, 3.3, 3.4, 3.5, 3.6_

- [x] 5. Add PDF action link to Quotation Index page
  - [x] 5.1 Add "PDF" action link in the actions column of `Views/Quotation/Index.cshtml`
    - Add: `<a href="javascript:void(0)" onclick="downloadQuotationPdf(@item.Id)" class="tbl-action tbl-action--secondary">PDF</a>`
    - _Requirements: 7.1_

  - [x] 5.2 Add `downloadQuotationPdf(quotationId)` JavaScript function to the Index view's script section
    - Same implementation as Task 4.2 (identical function body)
    - BlockUI.show → fetch → blob download or SweetAlert2 error → BlockUI.hide
    - _Requirements: 7.2, 7.3, 7.4, 7.5_

- [x] 6. Add anonymous PDF download to shared Proposal view
  - [x] 6.1 Add `IWebHostEnvironment`, `ILogoService`, and `ILogger<ProposalController>` injection to `ProposalController`
    - Add private readonly fields and constructor parameters
    - _Requirements: 6.1_

  - [x] 6.2 Add `DownloadPdf(string token)` action to `ProposalController`
    - Decorate with `[HttpGet("/proposal/{token}/download-pdf")]`
    - Validate token is not null/whitespace → return `NotFound()` if invalid
    - Get share via `_proposalService.GetByTokenAsync(token)` → return `NotFound()` if null
    - Check `share.IsActive` and `share.ExpiresAtUtc > DateTimeOffset.UtcNow` → return `NotFound()` if inactive/expired
    - Check `share.SnapshotHtml` is not null or empty → return `NotFound()` if missing
    - Remove `.download-bar` element from SnapshotHtml using regex
    - Embed logos as base64 data URIs (resolve `/uploads/` paths to physical files)
    - Generate PDF using PuppeteerSharp inline (same pattern as `InvoiceViewController.DownloadPdf`): A4 portrait, PrintBackground = true, zero margins, Networkidle0
    - Use 30-second `CancellationTokenSource`
    - Generate filename using `GenerateProposalPdfFilename` helper (add as private static method or reuse)
    - Return `File(pdfBytes, "application/pdf", filename)` on success
    - Catch `OperationCanceledException` → return `StatusCode(500, new { success = false, message = "PDF generation timed out. Please try again." })`
    - Catch `Exception ex` → log error, return `StatusCode(500, new { success = false, message = "Failed to generate PDF. Please try again." })`
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.8, 6.9, 6.10, 6.11, 6.12_

  - [x] 6.3 Replace `window.print()` button in the shared proposal view with "Download PDF" button
    - In `ProposalController.ViewProposal`, inject a "Download PDF" button and `downloadProposalPdf()` JavaScript function into the HTML
    - The button triggers `fetch('/proposal/{token}/download-pdf')`
    - Use same BlockUI → fetch → blob download → SweetAlert2 error pattern
    - `BlockUI.show('Generating PDF...')` while generating
    - On error: `BlockUI.hide()`, SweetAlert2 error message
    - On success: `BlockUI.hide()`, trigger file download
    - _Requirements: 6.5, 6.6, 6.7_

- [x] 7. Checkpoint - Ensure all code compiles and views render correctly
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Unit tests
  - [x] 8.1 Create `Portal.Tests/Unit/Services/ProposalPdfFilenameTests.cs`
    - Test `GenerateProposalPdfFilename` with normal reference (e.g., "2025-00042") → returns "QUO-2025-00042.pdf"
    - Test with invalid chars (e.g., "2025/00:042") → returns "QUO-200042.pdf"
    - Test with all invalid chars (e.g., "<>:\"|?*") → returns "QUO-download.pdf"
    - Test with empty string → returns "QUO-download.pdf"
    - Test with whitespace-only string → returns "QUO-download.pdf"
    - Test with leading/trailing dots (e.g., "..2025..") → returns "QUO-2025.pdf"
    - Test with leading/trailing spaces (e.g., " 2025 ") → returns "QUO-2025.pdf"
    - Test with ASCII control characters → returns sanitized filename
    - _Requirements: 5.1, 5.2, 5.3_

  - [x] 8.2 Create `Portal.Tests/Unit/Controllers/QuotationControllerDownloadPdfTests.cs`
    - Test `AxGetDownloadPdf` with non-existent quotation ID → returns NotFound
    - Test with quotation belonging to different business → returns NotFound
    - Test with mocked service throwing `OperationCanceledException` → returns 500 JSON with timeout message
    - Test with mocked service throwing generic exception → returns 500 JSON with generic message (no internals exposed)
    - Test success path → returns FileResult with "application/pdf" content type and correct filename
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6_

  - [x] 8.3 Create `Portal.Tests/Unit/Controllers/ProposalControllerDownloadPdfTests.cs`
    - Test `DownloadPdf` with invalid/null token → returns NotFound
    - Test with expired share → returns NotFound
    - Test with inactive share → returns NotFound
    - Test with null SnapshotHtml → returns NotFound
    - Test with valid active share → returns FileResult with "application/pdf" content type
    - _Requirements: 6.1, 6.2, 6.3, 6.12_

- [x] 9. Property-based tests
  - [x]* 9.1 Write property test for proposal PDF filename format
    - **Property 1: Proposal PDF filename format**
    - For any non-empty string containing at least one valid filename character, result starts with "QUO-" and ends with ".pdf"
    - Use `[Property(MaxTest = 100)]`, reflection to invoke `GenerateProposalPdfFilename`
    - Tag: `Feature: quotation-pdf-download, Property 1: Proposal PDF filename format`
    - **Validates: Requirements 2.2, 5.1, 6.9**

  - [x]* 9.2 Write property test for filename sanitization
    - **Property 2: Filename sanitization removes all invalid characters and trims**
    - For any arbitrary string, the generated filename never contains `< > : " / \ | ? *` or ASCII control characters, and the reference portion has no leading/trailing whitespace or dots
    - Use `[Property(MaxTest = 100)]`, reflection to invoke `GenerateProposalPdfFilename`
    - Tag: `Feature: quotation-pdf-download, Property 2: Filename sanitization removes all invalid characters and trims`
    - **Validates: Requirements 5.2**

  - [x]* 9.3 Write property test for logo data URI round-trip
    - **Property 3: Logo data URI encoding round-trip**
    - For any non-empty byte array and valid MIME type, encoding to data URI and decoding base64 portion yields original bytes
    - Use `[Property(MaxTest = 100)]`
    - Tag: `Feature: quotation-pdf-download, Property 3: Logo data URI encoding round-trip`
    - **Validates: Requirements 1.8, 4.2, 6.8**

  - [x]* 9.4 Write property test for download bar exclusion
    - **Property 4: Download bar exclusion from PDF HTML**
    - For any HTML string containing a `<div class="download-bar">...</div>` element, after removal processing, the result does not contain `class="download-bar"`
    - Use `[Property(MaxTest = 100)]`
    - Tag: `Feature: quotation-pdf-download, Property 4: Download bar exclusion from PDF HTML`
    - **Validates: Requirements 4.6, 6.10**

- [x] 10. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The implementation mirrors the Invoice PDF Download pattern for consistency
- All JavaScript follows the BlockUI → fetch → blob download → SweetAlert2 error pattern established in the project
- `GenerateProposalPdfFilename` is a private static method accessed via reflection in tests (same pattern as invoice)

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2", "1.3"] },
    { "id": 2, "tasks": ["2.1"] },
    { "id": 3, "tasks": ["2.2", "2.3"] },
    { "id": 4, "tasks": ["4.1", "5.1", "6.1"] },
    { "id": 5, "tasks": ["4.2", "5.2", "6.2"] },
    { "id": 6, "tasks": ["6.3"] },
    { "id": 7, "tasks": ["8.1", "8.2", "8.3"] },
    { "id": 8, "tasks": ["9.1", "9.2", "9.3", "9.4"] }
  ]
}
```
