# Implementation Plan: AJAX Line Save

## Overview

Convert quotation line item Save/Add buttons from full form POSTs to AJAX fetch() calls with blockUI spinner overlay. Implementation touches the controller (JSON response branch), a new JS module (form interception + fetch), a new CSS file (overlay + animations), and a view reference update.

## Tasks

- [x] 1. Add AJAX detection and JSON response path to QuotationController
  - [x] 1.1 Add IsAjaxRequest() private helper method to QuotationController
    - Add `private bool IsAjaxRequest() => Request.Headers["X-Requested-With"] == "XMLHttpRequest";`
    - _Requirements: 6.1, 6.2, 6.3_

  - [x] 1.2 Modify UpdateLine action to return JSON for AJAX requests
    - After successful update: if IsAjaxRequest(), return `Json(new { success = true })`
    - On ModelState invalid + AJAX: return `Json(new { success = false, message = "Validation failed" })`
    - On ArgumentException/InvalidOperationException + AJAX: return `Json(new { success = false, message = ex.Message })`
    - Non-AJAX requests continue returning RedirectToAction unchanged
    - _Requirements: 1.4, 6.1, 6.3, 6.4_

  - [x] 1.3 Modify AddLine action to return JSON for AJAX requests
    - After successful add: if IsAjaxRequest(), return `Json(new { success = true })`
    - On ModelState invalid + AJAX: return `Json(new { success = false, message = "Validation failed" })`
    - On ArgumentException/InvalidOperationException + AJAX: return `Json(new { success = false, message = ex.Message })`
    - Non-AJAX requests continue returning RedirectToAction unchanged
    - _Requirements: 6.2, 6.3, 6.4_

  - [ ]* 1.4 Write property test: AJAX response shape (Property 2)
    - **Property 2: AJAX response shape**
    - Generate random valid/invalid QuotationLineFormViewModel instances, POST to controller with X-Requested-With header, assert response is always JSON with boolean `success` and optional `message` string
    - **Validates: Requirements 1.4, 6.1, 6.2, 6.4**

  - [ ]* 1.5 Write property test: Non-AJAX backward compatibility (Property 3)
    - **Property 3: Non-AJAX backward compatibility**
    - Generate random valid QuotationLineFormViewModel instances, POST without AJAX header, assert response is always a RedirectToActionResult (HTTP 302)
    - **Validates: Requirements 6.3, 7.1**

- [x] 2. Checkpoint - Ensure controller changes compile and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 3. Create the CSS file for overlay, spinner, and feedback animations
  - [x] 3.1 Create Portal.Web/wwwroot/css/quotation-line-save.css
    - `.blockui-overlay` — fixed full-viewport semi-transparent backdrop (`rgba(0,0,0,0.3)`) with flexbox-centered spinner, `z-index: 9999`
    - `.blockui-overlay .spinner` — CSS-only spinning circle using border animation
    - `.line-card--saved` — `@keyframes` green highlight that fades over 2 seconds
    - `.line-card__error` — inline error message styling (red text, small font, margin-top, dismiss button)
    - _Requirements: 3.1, 3.2, 4.1, 4.2, 5.1_

- [x] 4. Create the JavaScript module for AJAX form interception
  - [x] 4.1 Create Portal.Web/wwwroot/js/quotation-line-save.js
    - IIFE that runs on `DOMContentLoaded`
    - `initLineSaveHandlers()` — queries all forms whose action contains `/UpdateLine` or `/AddLine`, attaches submit listeners
    - `handleSubmit(e)` — preventDefault, show overlay, serialize FormData, fetch with `X-Requested-With: XMLHttpRequest` header, handle response
    - `showOverlay()` / `hideOverlay()` — create/remove `.blockui-overlay` element
    - `flashSuccess(lineCard)` — add `.line-card--saved` class, remove after 2s
    - `showError(lineCard, message)` — insert `.line-card__error` element adjacent to card
    - `clearError(lineCard)` — remove existing error element for that card
    - 30-second AbortController timeout with timeout error message
    - On UpdateLine success: flash green, no reload
    - On AddLine success: reload page to render new line with server-assigned ID
    - On error: show inline error, preserve all form values
    - Network error: show generic "Unable to reach the server. Check your connection." message
    - Non-JSON response (e.g. 500 HTML): catch parse error, show "An unexpected error occurred."
    - _Requirements: 1.1, 1.2, 1.3, 1.5, 2.1, 2.2, 2.3, 3.1, 3.2, 3.3, 3.4, 4.1, 4.2, 4.3, 5.1, 5.2, 5.3, 5.4, 7.2_

  - [ ]* 4.2 Write property test: Overlay lifecycle (Property 5)
    - **Property 5: Overlay lifecycle**
    - Simulate success, error, network failure, and timeout scenarios in jsdom; assert `.blockui-overlay` is removed from DOM in all cases
    - **Validates: Requirements 3.3, 3.4**

  - [ ]* 4.3 Write property test: Success animation application (Property 6)
    - **Property 6: Success animation application**
    - Simulate UpdateLine success response, assert `.line-card--saved` class is added then removed after timer
    - **Validates: Requirements 4.1, 4.2, 4.3**

  - [ ]* 4.4 Write property test: Error message rendering (Property 7)
    - **Property 7: Error message rendering**
    - Generate random non-empty error message strings, simulate error response, assert message text appears in DOM adjacent to line card
    - **Validates: Requirements 5.1, 2.3**

- [x] 5. Reference new assets in the Edit view
  - [x] 5.1 Add CSS and JS references to Portal.Web/Views/Quotation/Edit.cshtml
    - Add `<link rel="stylesheet" href="~/css/quotation-line-save.css" />` in the styles section
    - Add `<script src="~/js/quotation-line-save.js"></script>` at the bottom of the page
    - _Requirements: 7.2_

- [x] 6. Final checkpoint - Ensure all tests pass and feature works end-to-end
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- No changes to `_SectionCards.cshtml` form markup — JS discovers forms by action URL pattern
- No database changes required
- Graceful degradation is inherent: forms retain `method="post"` and `action` attributes, so they work without JS
- Property tests validate universal correctness properties from the design document
