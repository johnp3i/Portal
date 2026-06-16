# Requirements Document

## Introduction

This feature adds a direct "Download PDF" action to quotation proposals, replacing the current `window.print()` workaround on the Proposal Snapshot view. The system generates a properly formatted PDF that is visually identical to the existing Proposal Snapshot view (`Views/Proposal/Snapshot.cshtml`), using PuppeteerSharp for HTML-to-PDF rendering. The feature follows the same pattern established by the Invoice PDF Download feature, introducing a dedicated `IProposalPdfService` for decoupled, reusable PDF generation.

## Glossary

- **Proposal_PDF_Service**: The service responsible for rendering proposal HTML to a PDF byte array using PuppeteerSharp. Exposed via the `IProposalPdfService` interface.
- **Proposal_Service**: The existing `IProposalService` that orchestrates proposal snapshot generation, including building the `ProposalRenderModel` and rendering via `IProposalRenderer`.
- **Proposal_Renderer**: The existing `IProposalRenderer` service that renders a `ProposalRenderModel` into a self-contained HTML string using the `Snapshot.cshtml` Razor view.
- **Snapshot_View**: The standalone Razor view (`Views/Proposal/Snapshot.cshtml`) that renders a complete, styled proposal without layout dependencies. This is the HTML source for PDF generation.
- **Quotation_Controller**: The existing authenticated MVC controller handling quotation-related HTTP requests.
- **Proposal_Controller**: The existing unauthenticated MVC controller that serves shared proposals via token-based URLs.
- **Quotation_Detail_Page**: The authenticated page where business users view a single quotation's full details and perform actions.
- **Quotation_Index_Page**: The authenticated page listing all quotations with filter, search, and action columns.
- **PDF_Filename**: The download filename for a generated proposal PDF, following the format `QUO-{reference}.pdf` where `{reference}` is the quotation reference string (e.g., `QUO-2025-00042.pdf`).

## Requirements

### Requirement 1: Proposal PDF Service Interface

**User Story:** As a developer, I want a dedicated `IProposalPdfService` interface, so that PDF generation is decoupled from the controller and can be reused (e.g., for emailing proposal PDFs in the future).

#### Acceptance Criteria

1. THE Proposal_PDF_Service SHALL accept an integer quotation identifier, a list of hero logo identifiers, an optional meta logo identifier, and an optional CancellationToken, and return the generated PDF as a byte array.
2. THE Proposal_PDF_Service SHALL use the Proposal_Service PreviewAsync method to obtain the proposal HTML from the Snapshot_View.
3. THE Proposal_PDF_Service SHALL use PuppeteerSharp to convert the HTML string into a PDF document.
4. THE Proposal_PDF_Service SHALL generate the PDF in A4 portrait format with print backgrounds enabled and zero margins on all sides (top, bottom, left, and right set to "0mm").
5. IF the combined duration of HTML retrieval and PDF generation exceeds 30 seconds, THEN THE Proposal_PDF_Service SHALL cancel the operation and throw a timeout exception.
6. IF the quotation identifier does not correspond to an existing quotation for the current business, THEN THE Proposal_PDF_Service SHALL propagate the exception thrown by the Proposal_Service.
7. IF the Proposal_Renderer throws an exception during HTML rendering, THEN THE Proposal_PDF_Service SHALL propagate the exception to the caller without suppressing it.
8. THE Proposal_PDF_Service SHALL post-process the rendered HTML by matching `<img>` tags whose `src` attribute begins with `/uploads/`, resolving each matched path to its physical file on disk, and replacing the `src` value with a base64 data URI (format: `data:{contentType};base64,{encodedBytes}`) so that each logo appears in the PDF without external HTTP requests.
9. IF a logo file referenced by a `/uploads/` src attribute does not exist on disk, THEN THE Proposal_PDF_Service SHALL leave that specific image tag unchanged and continue processing remaining images.
10. WHEN the HTML content is set in the headless browser, THE Proposal_PDF_Service SHALL wait for the page to reach network idle state (zero active network connections for at least 500 milliseconds) before capturing the PDF.

### Requirement 2: PDF Download Controller Action (Authenticated)

**User Story:** As a business user, I want to download a quotation proposal as a PDF file from a URL endpoint, so that I can save or share a professional proposal document.

#### Acceptance Criteria

1. WHEN a GET request is made to the download endpoint with a valid quotation identifier, THE Quotation_Controller SHALL return the PDF as a file download with content type `application/pdf` and a Content-Disposition header of type `attachment`.
2. WHEN a GET request is made to the download endpoint with a valid quotation identifier, THE Quotation_Controller SHALL set the download filename to the PDF_Filename format using the quotation reference string.
3. IF the quotation identifier does not correspond to an existing quotation, THEN THE Quotation_Controller SHALL return a 404 Not Found response.
4. IF the quotation does not belong to the current authenticated user's business, THEN THE Quotation_Controller SHALL return a 404 Not Found response.
5. IF PDF generation fails due to a timeout (OperationCanceledException), THEN THE Quotation_Controller SHALL return a 500 status JSON response with `success: false` and a message indicating the generation timed out, without exposing internal exception details.
6. IF PDF generation fails due to an unexpected error, THEN THE Quotation_Controller SHALL log the exception and return a 500 status JSON response with `success: false` and a generic failure message that does not contain the exception message, stack trace, or any internal system details.
7. IF the quotation identifier is not a valid integer, THEN THE Quotation_Controller SHALL return a 404 Not Found response.
8. IF the business has a primary logo, THEN THE Quotation_Controller SHALL pass the primary logo identifier as both the hero logo list and the meta logo when invoking the Proposal_PDF_Service.
9. IF the business does not have a primary logo, THEN THE Quotation_Controller SHALL pass an empty hero logo list and null meta logo to the Proposal_PDF_Service, allowing PDF generation to proceed without logo images.
10. IF the user is not authenticated, THEN THE Quotation_Controller SHALL reject the request before processing (endpoint requires authentication).

### Requirement 3: Download PDF Button on Quotation Detail Page

**User Story:** As a business user, I want a visible "Download PDF" button on the Quotation Detail page, so that I can generate and download a proposal PDF with a single click.

#### Acceptance Criteria

1. THE Quotation_Detail_Page SHALL display a "Download PDF" button in the quotation action area alongside existing action buttons.
2. WHEN the user clicks the "Download PDF" button, THE Quotation_Detail_Page SHALL make a fetch request to the PDF download endpoint and trigger a browser file download using the response blob with the filename extracted from the Content-Disposition header.
3. WHILE the PDF is being generated, THE Quotation_Detail_Page SHALL display BlockUI with the message "Generating PDF..." to prevent repeated clicks and signal progress.
4. WHEN the fetch request completes (whether successfully or with an error), THE Quotation_Detail_Page SHALL call BlockUI.hide() before performing any subsequent action such as triggering the download or displaying an error dialog.
5. IF the download request returns a non-OK HTTP status or the response content type is not `application/pdf`, THEN THE Quotation_Detail_Page SHALL attempt to parse the JSON error body and display the error message using SweetAlert2 with the error icon; IF JSON parsing fails, THEN THE Quotation_Detail_Page SHALL display a generic failure message using SweetAlert2.
6. IF the fetch request fails due to a network error (e.g., no connectivity, DNS failure, or request timeout), THEN THE Quotation_Detail_Page SHALL display a SweetAlert2 dialog with the error icon and a message indicating the download could not be completed due to a connection problem.

### Requirement 4: PDF Visual Fidelity

**User Story:** As a business user, I want the downloaded PDF to look identical to the Proposal Snapshot view, so that I get a professional document without manual formatting.

#### Acceptance Criteria

1. THE Proposal_PDF_Service SHALL produce a PDF in A4 portrait format with print backgrounds enabled, preserving the layout, fonts, colours, spacing, and content of the Snapshot_View such that all text, totals, and structural elements appear in the same position and size as the browser-rendered view.
2. THE Proposal_PDF_Service SHALL render business logos as base64 data URIs embedded directly in the HTML, ensuring images appear in the PDF without external HTTP requests.
3. IF the business does not have any logos uploaded, THEN THE Proposal_PDF_Service SHALL generate the PDF without logo images and without rendering broken image placeholders.
4. THE Proposal_PDF_Service SHALL generate the PDF with zero margins (margin-top, margin-bottom, margin-left, margin-right all set to 0) so that the Snapshot_View's own internal padding controls the page layout.
5. THE Proposal_PDF_Service SHALL wait for the page to reach network idle state (no more than 0 active network connections for at least 500 milliseconds) before capturing the PDF content.
6. THE Proposal_PDF_Service SHALL exclude the download bar element (the sticky bar with the current `window.print()` button) from the generated PDF output.
7. THE Proposal_PDF_Service SHALL embed all CSS styles inline within the HTML before PDF generation so that the rendered PDF does not depend on external stylesheet requests.

### Requirement 5: PDF Filename Format

**User Story:** As a business user, I want the downloaded PDF filename to contain the quotation reference, so that I can identify proposal files easily on my file system.

#### Acceptance Criteria

1. WHEN a PDF is downloaded, THE Quotation_Controller SHALL name the file using the pattern `QUO-{reference}.pdf` where `{reference}` is the full quotation reference string (e.g., `QUO-2025-00042.pdf`).
2. IF the quotation reference contains characters that are invalid in Windows, macOS, or Linux filenames (specifically: `< > : " / \ | ? *` and ASCII control characters 0x00–0x1F), THEN THE Quotation_Controller SHALL sanitize the filename by removing those characters and trimming any leading or trailing whitespace, spaces, or dots from the result.
3. IF sanitization of the quotation reference results in an empty or whitespace-only string, THEN THE Quotation_Controller SHALL fall back to the filename `QUO-download.pdf`.

### Requirement 6: Download PDF from Shared Proposal View (Anonymous)

**User Story:** As a customer viewing a shared proposal, I want to download the proposal as a PDF, so that I can save a professional copy for my records without needing a Portal account.

#### Acceptance Criteria

1. THE Proposal_Controller SHALL expose a GET endpoint at `/proposal/{token}/download-pdf` that generates a PDF from the shared proposal's stored snapshot HTML.
2. WHEN a valid share token is provided and the share is active and not expired, THE endpoint SHALL return the PDF as a file download with content type `application/pdf` and a Content-Disposition header of type `attachment`.
3. IF the share token is invalid, inactive, or expired, THEN THE endpoint SHALL return a 404 Not Found response.
4. THE endpoint SHALL NOT require authentication (anonymous access via token-based authorization).
5. THE shared proposal view SHALL replace the current `window.print()` button with a "Download PDF" button that triggers a fetch request to the `/proposal/{token}/download-pdf` endpoint and saves the response blob as a file download in the browser.
6. WHILE the PDF is being generated, THE shared proposal view SHALL display BlockUI with the message "Generating PDF..." to prevent repeated clicks and signal progress.
7. IF the download request returns a non-OK HTTP status or the response content type is not `application/pdf`, THEN THE shared proposal view SHALL hide BlockUI and display an error message using SweetAlert2 indicating the PDF could not be generated.
8. THE PDF SHALL be generated from the stored `SnapshotHtml` with logos embedded as base64 data URIs (same visual fidelity as the authenticated download).
9. THE download filename SHALL follow the same `QUO-{reference}.pdf` format and filename sanitization rules used for authenticated downloads.
10. THE endpoint SHALL exclude the download bar element from the generated PDF by removing it from the snapshot HTML before PDF rendering.
11. IF PDF generation exceeds 30 seconds, THEN THE endpoint SHALL cancel the operation and return a 500 status JSON response with `success: false` and a message indicating the generation timed out.
12. IF the stored SnapshotHtml is null or empty, THEN THE endpoint SHALL return a 404 Not Found response.

### Requirement 7: Download PDF Action in Quotation List View

**User Story:** As a business user, I want a "PDF" download action in the quotations table actions column, so that I can quickly download any quotation proposal as a PDF directly from the list without navigating to the detail page.

#### Acceptance Criteria

1. THE Quotation_Index_Page SHALL display a "PDF" action link in the actions column of each quotation row.
2. WHEN the user clicks the "PDF" action link, THE Quotation_Index_Page SHALL make a fetch request to the PDF download endpoint for that quotation and trigger a browser file download using the filename from the response Content-Disposition header.
3. WHILE the PDF is being generated, THE Quotation_Index_Page SHALL display BlockUI with the message "Generating PDF..." to prevent repeated clicks and signal progress.
4. WHEN the download completes successfully, THE Quotation_Index_Page SHALL dismiss BlockUI without displaying a success dialog (the file download itself is the confirmation).
5. IF the download request returns a non-OK HTTP status or the response content type is not `application/pdf`, THEN THE Quotation_Index_Page SHALL dismiss BlockUI, parse the JSON error body, and display the error message using SweetAlert2 with the error icon.
