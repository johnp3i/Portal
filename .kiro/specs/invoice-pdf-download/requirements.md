# Requirements Document

## Introduction

This feature adds a direct "Download PDF" action to invoices, replacing the current workaround of using browser print-to-PDF from the Invoice Preview page. The system will generate a properly formatted PDF that is visually identical to the existing Invoice Snapshot view, using PuppeteerSharp for HTML-to-PDF rendering. The feature follows the same pattern already established by the Customer Statement and Credit Note PDF generation in the codebase.

## Glossary

- **Invoice_PDF_Service**: The service responsible for rendering invoice HTML to a PDF byte array using PuppeteerSharp. Exposed via the `IInvoicePdfService` interface.
- **Invoice_Renderer**: The existing `IInvoiceRenderer` service that fetches invoice data and renders the `Snapshot.cshtml` Razor view into a self-contained HTML string.
- **Snapshot_View**: The standalone Razor view (`Views/Invoice/Snapshot.cshtml`) that renders a complete, styled invoice without layout dependencies. This is the HTML source for PDF generation.
- **Invoice_Controller**: The existing MVC controller handling invoice-related HTTP requests.
- **Invoice_Detail_Page**: The authenticated page where business users view a single invoice's full details and perform actions.
- **PDF_Filename**: The download filename for a generated invoice PDF, following the format `INV-{number}.pdf` where `{number}` is the invoice number (e.g., `INV-1-00090.pdf`).

## Requirements

### Requirement 1: Invoice PDF Service Interface

**User Story:** As a developer, I want a dedicated `IInvoicePdfService` interface, so that PDF generation is decoupled from the controller and can be reused (e.g., for emailing invoice PDFs in the future).

#### Acceptance Criteria

1. THE Invoice_PDF_Service SHALL accept an integer invoice identifier and an optional CancellationToken, and return the generated PDF as a byte array.
2. THE Invoice_PDF_Service SHALL use the Invoice_Renderer to obtain the invoice HTML from the Snapshot_View.
3. THE Invoice_PDF_Service SHALL use PuppeteerSharp to convert the HTML string into a PDF document.
4. THE Invoice_PDF_Service SHALL generate the PDF in A4 portrait format with print backgrounds enabled and 10mm margins on all sides.
5. IF PDF generation exceeds 30 seconds, THEN THE Invoice_PDF_Service SHALL cancel the operation and throw a timeout exception.
6. IF the invoice identifier does not correspond to an existing invoice, THEN THE Invoice_PDF_Service SHALL throw an exception indicating the invoice was not found.
7. IF the Invoice_Renderer throws an exception during HTML rendering, THEN THE Invoice_PDF_Service SHALL propagate the exception to the caller without suppressing it.

### Requirement 2: PDF Download Controller Action

**User Story:** As a business user, I want to download an invoice as a PDF file from a URL endpoint, so that I can save or share a professional invoice document.

#### Acceptance Criteria

1. WHEN a GET request is made to the download endpoint with a valid invoice identifier, THE Invoice_Controller SHALL return the PDF as a file download with content type `application/pdf` and a Content-Disposition header of type `attachment`.
2. WHEN a GET request is made to the download endpoint, THE Invoice_Controller SHALL set the download filename to the PDF_Filename format using the invoice number.
3. IF the invoice identifier does not correspond to an existing invoice, THEN THE Invoice_Controller SHALL return a 404 Not Found response.
4. IF the invoice does not belong to the current authenticated user's business, THEN THE Invoice_Controller SHALL return a 404 Not Found response.
5. IF PDF generation fails due to a timeout, THEN THE Invoice_Controller SHALL return a 500 status JSON response with `success: false` and a message indicating the generation timed out.
6. IF PDF generation fails due to an unexpected error, THEN THE Invoice_Controller SHALL log the error and return a 500 status JSON response with `success: false` and a message indicating a general failure without exposing internal details.
7. IF the invoice identifier is not a valid integer, THEN THE Invoice_Controller SHALL return a 404 Not Found response.

### Requirement 3: Download PDF Button on Invoice Detail Page

**User Story:** As a business user, I want a visible "Download PDF" button on the Invoice Detail page, so that I can generate and download a PDF with a single click.

#### Acceptance Criteria

1. THE Invoice_Detail_Page SHALL display a "Download PDF" button in the invoice action area alongside existing action buttons.
2. WHEN the user clicks the "Download PDF" button, THE Invoice_Detail_Page SHALL make a fetch request to the PDF download endpoint and trigger a browser file download from the response blob.
3. WHILE the PDF is being generated, THE Invoice_Detail_Page SHALL display BlockUI with the message "Generating PDF..." to prevent repeated clicks and signal progress.
4. WHEN the download completes successfully, THE Invoice_Detail_Page SHALL call BlockUI.hide() without displaying a success dialog (the file download itself is the confirmation).
5. IF the download request returns a non-OK HTTP status or the response content type is not `application/pdf`, THEN THE Invoice_Detail_Page SHALL parse the JSON error body and display the error message using SweetAlert2.

### Requirement 4: PDF Visual Fidelity

**User Story:** As a business user, I want the downloaded PDF to look identical to the Invoice Snapshot view, so that I get a professional document without manual formatting.

#### Acceptance Criteria

1. THE Invoice_PDF_Service SHALL produce a PDF that preserves the layout, fonts, colours, spacing, and content of the Snapshot_View such that all text, totals, and structural elements appear in the same position and size as the browser-rendered view.
2. THE Invoice_PDF_Service SHALL embed all CSS styles inline within the HTML before PDF generation (the Snapshot_View already uses inline styles).
3. THE Invoice_PDF_Service SHALL render the business logo as a base64 data URI embedded directly in the HTML, ensuring the image appears in the PDF without external HTTP requests.
4. IF the business does not have a logo uploaded, THEN THE Invoice_PDF_Service SHALL generate the PDF without a logo image and without rendering a broken image placeholder.
5. THE Invoice_PDF_Service SHALL generate the PDF with zero margins (margin-top, margin-bottom, margin-left, margin-right all set to 0) so that the Snapshot_View's own internal padding controls the page layout.
6. THE Invoice_PDF_Service SHALL wait for the page to reach network idle state (no more than 2 active network connections for at least 500 milliseconds) before capturing the PDF content.

### Requirement 5: PDF Filename Format

**User Story:** As a business user, I want the downloaded PDF filename to contain the invoice number, so that I can identify invoice files easily on my file system.

#### Acceptance Criteria

1. WHEN a PDF is downloaded, THE Invoice_Controller SHALL name the file using the pattern `INV-{number}.pdf` where `{number}` is the full invoice number portion (e.g., `INV-1-00090.pdf`).
2. IF the invoice number contains characters that are invalid in Windows, macOS, or Linux filenames (specifically: `< > : " / \ | ? *`), THEN THE Invoice_Controller SHALL sanitize the filename by removing those characters.
3. IF sanitization of the invoice number results in an empty string, THEN THE Invoice_Controller SHALL fall back to the filename `INV-download.pdf`.

### Requirement 6: Download PDF from Shared Invoice View (Anonymous)

**User Story:** As a customer viewing a shared invoice, I want to download the invoice as a PDF, so that I can save a professional copy for my accounting records without needing a Portal account.

#### Acceptance Criteria

1. THE InvoiceViewController SHALL expose a GET endpoint at `/invoice-view/{token}/download-pdf` that generates a PDF from the shared invoice's snapshot HTML.
2. WHEN a valid share token is provided and the share is active and not expired, THE endpoint SHALL return the PDF as a file download with content type `application/pdf`.
3. IF the share token is invalid, inactive, or expired, THEN THE endpoint SHALL return a 404 Not Found response.
4. THE endpoint SHALL NOT require authentication (anonymous access via token-based authorization).
5. THE shared invoice view SHALL replace the current `window.print()` button with a proper "Download PDF" button that triggers a fetch request to the PDF download endpoint.
6. WHILE the PDF is being generated, THE shared invoice view SHALL display BlockUI with the message "Generating PDF..." to signal progress.
7. IF the PDF generation fails, THEN THE shared invoice view SHALL display an error message using SweetAlert2.
8. THE PDF SHALL be generated from the stored `SnapshotHtml` with embedded base64 logo (same visual fidelity as the authenticated download).
9. THE download filename SHALL follow the same `INV-{number}.pdf` format used for authenticated downloads.

### Requirement 7: Download PDF Action in Invoice List View

**User Story:** As a business user, I want a "PDF" download action in the invoices table actions column, so that I can quickly download any invoice as a PDF directly from the list without navigating to the detail page.

#### Acceptance Criteria

1. THE Invoice list view SHALL display a "PDF" action link in the actions column of each invoice row.
2. WHEN the user clicks the "PDF" action link, THE Invoice list view SHALL initiate a file download by making a fetch request to the PDF download endpoint for that invoice.
3. WHILE the PDF is being generated, THE Invoice list view SHALL display BlockUI with the message "Generating PDF..." to prevent repeated clicks and signal progress.
4. WHEN the download completes successfully, THE Invoice list view SHALL call BlockUI.hide() without displaying a success dialog.
5. IF the download request returns an error, THEN THE Invoice list view SHALL display the error message using SweetAlert2.
