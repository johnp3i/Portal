# PDF Signature Missing Bugfix Design

## Overview

Invoice PDFs generated via PuppeteerSharp are missing the digital signature block. The signature renders correctly in the browser but gets clipped in PDF output because `margin-top:auto` (a flexbox push-to-bottom technique) does not behave consistently in CSS paged media (print mode). The fix replaces `margin-top:auto` with a fixed `margin-top:40px` to guarantee the signature remains within the printable area.

## Glossary

- **Bug_Condition (C)**: The condition where `margin-top:auto` is used on the signature wrapper div and the document is rendered to PDF via PuppeteerSharp — causing the signature to overflow the A4 page boundary
- **Property (P)**: The digital signature block SHALL be fully visible in the generated PDF output
- **Preservation**: Browser rendering of the invoice snapshot, non-signature PDF content, and invoices without auto-signature must remain unchanged
- **Snapshot.cshtml**: The Razor view at `Portal.Web/Views/Invoice/Snapshot.cshtml` that renders the invoice HTML for both browser display and PDF generation
- **PuppeteerSharp**: The headless Chromium library used by `InvoicePdfService` to convert rendered HTML into PDF
- **Paged Media**: CSS rendering mode used for print/PDF where content is divided into discrete pages with fixed dimensions

## Bug Details

### Bug Condition

The bug manifests when an invoice with auto-signature enabled is rendered to PDF by PuppeteerSharp. The signature wrapper div uses `margin-top:auto` to push itself to the bottom of the page in flexbox layout. This CSS technique works in the browser's continuous rendering mode but fails in paged media (print/PDF mode) because:

1. PuppeteerSharp renders in print mode where `margin-top:auto` resolves to `0` or is ignored in block flow context
2. The parent container's flex behavior doesn't translate to paged media consistently
3. With `Bottom = "0mm"` in PdfOptions, any content that extends beyond the A4 content area is clipped without warning

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input of type InvoiceRenderRequest
  OUTPUT: boolean
  
  RETURN input.renderTarget == "PDF" (PuppeteerSharp)
         AND input.invoice.IsAutoInvoiceSignatureEnabled == true
         AND input.invoice.hasDefaultSignature == true
         AND signatureWrapperDiv.style CONTAINS "margin-top:auto"
END FUNCTION
```

### Examples

- **Invoice with signature, PDF download**: Signature is present in the HTML but missing from the PDF output. The signature div is pushed below page 1's boundary and clipped.
- **Invoice with signature, browser view**: Signature renders correctly because continuous layout allows `margin-top:auto` to push content down within the flex container.
- **Invoice without signature, PDF download**: No issue — the signature block is not rendered at all.
- **Short invoice (few line items) with signature, PDF**: May render correctly because the content fits within page 1 even with `margin-top:auto`, but this is non-deterministic.

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- Browser rendering of invoices must continue to display the signature correctly with appropriate spacing below the content
- Invoices without auto-signature enabled must continue to generate PDFs without a signature block
- All non-signature content (logo, line items, totals, notes, payment details, branding footer) must retain their current positioning and styling in both browser and PDF
- The `_SignatureBlock` partial rendering flow (`ICurrentTenantService`, `SignatureService.GetDefaultAsync`) must remain unmodified
- Other PDF generation settings (page size, margins, header/footer) must not be altered

**Scope:**
All inputs that do NOT involve PDF rendering of an invoice with auto-signature enabled should be completely unaffected by this fix. This includes:
- Browser-only invoice viewing
- PDF generation of invoices without signatures
- Proposal snapshots (confirmed: Proposal/Snapshot.cshtml does not use `margin-top:auto`)
- Any other PDF generation in the system

## Hypothesized Root Cause

Based on investigation of the code and CSS paged media behavior:

1. **CSS `margin-top:auto` in Paged Media**: In CSS flexbox, `margin-top:auto` absorbs remaining space to push an element to the bottom. However, in paged media (print mode used by PuppeteerSharp), the flex container's height is constrained to the page content area. The auto margin calculation either resolves to `0` or pushes the element beyond the page boundary, depending on the content height and page dimensions.

2. **Zero Bottom Margin in PdfOptions**: The PDF generation uses `Bottom = "0mm"`, meaning there is absolutely no overflow buffer. Any content that exceeds the A4 content area (297mm height minus top margin) is hard-clipped.

3. **No Page Break Logic for Signature**: The signature wrapper has `break-inside:avoid;page-break-inside:avoid` which prevents the signature from being split across pages, but there is no `break-before:auto` or similar rule that would force it onto a new page if it doesn't fit on the current one.

4. **Interaction Effect**: The combination of `margin-top:auto` (potentially pushing content down) + `break-inside:avoid` (preventing split) + zero bottom margin (no overflow buffer) creates a scenario where the signature is forced below the page boundary as a single unit with no recovery mechanism.

## Correctness Properties

Property 1: Bug Condition - Signature Visible in PDF

_For any_ invoice where auto-signature is enabled and a default signature exists, the generated PDF SHALL contain the signature image fully visible within the printable page area, regardless of invoice content length.

**Validates: Requirements 2.1, 2.2, 2.3**

Property 2: Preservation - Non-Signature Content Unchanged

_For any_ invoice rendered to PDF or browser, all non-signature content (logo, line items, totals, notes, payment details, branding) SHALL produce the same visual output as the original code, preserving layout and positioning.

**Validates: Requirements 3.1, 3.2, 3.3, 3.4**

## Fix Implementation

### Changes Required

**File**: `Portal.Web/Views/Invoice/Snapshot.cshtml`

**Line**: ~399 (the document footer wrapper div)

**Specific Changes**:

1. **Replace `margin-top:auto` with `margin-top:40px`**: This removes the flex-push-to-bottom behavior and replaces it with a consistent fixed margin that works in both browser and paged media contexts.

   Before:
   ```html
   <div style="break-inside:avoid;page-break-inside:avoid;margin-top:auto;padding-top:32px;">
   ```
   
   After:
   ```html
   <div style="break-inside:avoid;page-break-inside:avoid;margin-top:40px;padding-top:32px;">
   ```

**Trade-off**: The signature will no longer be pushed to the absolute bottom of the page in the browser view. Instead, it will appear 40px below the preceding content. This is an acceptable trade-off because:
- The signature being visible in PDFs is a functional requirement
- The aesthetic push-to-bottom was a nice-to-have that breaks PDF generation
- 40px provides sufficient visual separation in both contexts
- The `padding-top:32px` already provides additional internal spacing

**Proposal Snapshot**: Confirmed that `Portal.Web/Views/Proposal/Snapshot.cshtml` does NOT use `margin-top:auto` — no changes needed there.

## Testing Strategy

### Validation Approach

This is a CSS rendering fix. The bug is visual in nature and cannot be validated through automated property-based testing because:
- The bug exists in the interaction between CSS and PuppeteerSharp's rendering engine
- Automated tests cannot meaningfully assert pixel-level PDF content without OCR/image comparison tools
- The fix is a single CSS property change with well-understood behavior

Validation is manual/visual: generate a PDF before and after the fix and confirm the signature is visible.

### Exploratory Bug Condition Checking

**Goal**: Confirm the bug exists by generating a PDF with the current code.

**Test Plan**:
1. Create an invoice with auto-signature enabled
2. Download the PDF
3. Observe that the signature is missing from the PDF output
4. View the same invoice in browser and confirm signature is visible

### Fix Checking

**Goal**: Verify the signature appears in the PDF after the CSS change.

**Manual Verification**:
1. Apply the `margin-top:auto` → `margin-top:40px` change
2. Generate a PDF for an invoice with auto-signature enabled
3. Confirm the signature image is fully visible in the PDF
4. Test with invoices of varying lengths (short, medium, long content)

### Preservation Checking

**Goal**: Verify existing behavior is unchanged.

**Manual Verification**:
1. View an invoice with signature in the browser — confirm signature still displays correctly
2. Generate a PDF for an invoice WITHOUT auto-signature — confirm no regression
3. Verify all other PDF content (logo, line items, totals, notes) renders correctly
4. Verify the Proposal Snapshot is unaffected (it doesn't use `margin-top:auto`)

### Unit Tests

Not applicable — this is a CSS-only fix in a Razor view. No backend logic is changed.

### Property-Based Tests

Not applicable — CSS rendering behavior cannot be meaningfully tested through property-based testing. The rendering engine (PuppeteerSharp/Chromium) is the system under test, not application code.

### Integration Tests

Not applicable for automated testing. The validation is visual confirmation that the PDF output contains the signature. A future enhancement could add PDF content assertion tests using a PDF parsing library, but that is out of scope for this bugfix.
