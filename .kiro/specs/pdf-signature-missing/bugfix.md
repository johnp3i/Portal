# Bugfix Requirements Document

## Introduction

When a user issues an invoice with auto-signature enabled, the digital signature renders correctly in the browser view (Snapshot.cshtml) but is missing from the generated PDF. The PDF is produced by `InvoicePdfService` using PuppeteerSharp to convert the rendered HTML to PDF. The signature is present in the HTML string passed to PuppeteerSharp, indicating the issue lies in how the HTML-to-PDF conversion handles the signature element — likely due to CSS layout behavior (`margin-top:auto`) not translating correctly to print/PDF mode, or the signature section overflowing the A4 page boundary with zero bottom margin.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN an invoice with auto-signature enabled is downloaded as PDF THEN the system generates a PDF where the digital signature image is not visible in the output document

1.2 WHEN the signature block uses `margin-top:auto` CSS positioning in PuppeteerSharp's print/PDF rendering mode THEN the system produces a layout where the signature is pushed below the visible page boundary

1.3 WHEN the PDF margin options specify `Bottom = "0mm"` and the signature section extends beyond the A4 page content area THEN the system clips the signature content without any visible indication of the overflow

### Expected Behavior (Correct)

2.1 WHEN an invoice with auto-signature enabled is downloaded as PDF THEN the system SHALL generate a PDF where the digital signature image is fully visible and correctly positioned on the document

2.2 WHEN the signature block is rendered in PuppeteerSharp's print/PDF mode THEN the system SHALL use a layout strategy that ensures the signature remains within the printable page area regardless of CSS print-mode limitations

2.3 WHEN the invoice content approaches the A4 page boundary THEN the system SHALL ensure the signature section either fits within the remaining space or flows to the next page, never being clipped or hidden

### Unchanged Behavior (Regression Prevention)

3.1 WHEN an invoice is viewed in the browser (Snapshot.cshtml) THEN the system SHALL CONTINUE TO display the digital signature correctly using the existing inline base64 `<img>` rendering approach

3.2 WHEN an invoice without auto-signature enabled is downloaded as PDF THEN the system SHALL CONTINUE TO generate the PDF without any signature block, exactly as before

3.3 WHEN an invoice PDF is generated with other content (logo, line items, totals, footer) THEN the system SHALL CONTINUE TO render all non-signature elements with their current positioning and styling

3.4 WHEN the `_SignatureBlock` partial is rendered in the browser view THEN the system SHALL CONTINUE TO use the existing `@inject ICurrentTenantService` and `SignatureService.GetImageStreamAsync` flow without modification
