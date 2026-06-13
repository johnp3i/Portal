# Implementation Plan: Inline Customer Creation

## Overview

This plan implements inline customer creation across the Quotation and Invoice forms. The implementation progresses from back-end changes (repository → service → controller) to front-end components (shared modal partial → JavaScript module → view integration), ensuring each step builds on the previous one with no orphaned code.

## Tasks

- [x] 1. Modify repository and service layer
  - [x] 1.1 Update `CustomerRepository.InsertAsync` to return the new customer Id
    - Change return type from `Task` to `Task<int>`
    - Append `SELECT CAST(SCOPE_IDENTITY() AS INT)` to the INSERT statement
    - Use `ExecuteSqlRawAsync` replaced with a scalar query approach to retrieve the identity value
    - Existing callers remain backward-compatible (can discard return value)
    - _Requirements: 6.1, 6.2_

  - [x] 1.2 Add duplicate name validation to `CustomerService.CreateCustomerAsync`
    - Add `ValidateUniqueNameAsync(string name, int businessId)` private method
    - Query active customers for the tenant via `_customerRepository.GetAllByBusinessIdAsync(businessId)`
    - Perform case-insensitive comparison on Name
    - Throw `ArgumentException("A customer with this name already exists")` on duplicate
    - Call this validation inside `CreateCustomerAsync` after existing validations
    - Update `CreateCustomerAsync` to capture and set `customer.Id` from the new `InsertAsync` return value
    - _Requirements: 6.2, 6.3, 6.5_

  - [x]* 1.3 Write property tests for customer service validation
    - **Property 6: Server-side Name validation** — Generate whitespace-only strings, verify `ArgumentException` thrown
    - **Property 7: Duplicate Name rejection within same tenant** — Generate random names, insert once, attempt duplicate, verify rejection
    - **Validates: Requirements 6.3, 6.5**

- [x] 2. Add CreateInline controller action
  - [x] 2.1 Add `CreateInline` POST action to `CustomerController`
    - Decorate with `[HttpPost]`, `[ValidateAntiForgeryToken]`, `[ModuleAccess(PortalModules.Customer, AccessLevels.Full)]`
    - Accept `CustomerFormViewModel model` parameter
    - Check `ModelState.IsValid` — return `Json(new { success = false, message = "..." })` on failure
    - Call `_customerService.CreateCustomerAsync(customer)` inside try/catch
    - On success return `Json(new { success = true, id = customer.Id, name = customer.Name })`
    - On `ArgumentException` return `Json(new { success = false, message = ex.Message })`
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_

- [x] 3. Checkpoint - Ensure back-end compiles and logic is correct
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Create shared modal partial view
  - [x] 4.1 Create `_CustomerModal.cshtml` in `Portal.Web/Views/Shared/`
    - Fixed overlay backdrop: `position:fixed;inset:0;z-index:10000;background:rgba(0,0,0,.4);backdrop-filter:blur(2px)`
    - Inner card: `background:#fff;border-radius:24px;padding:32px;max-width:460px;box-shadow:0 20px 60px rgba(13,94,166,.18)`
    - Include all 10 customer fields (Name required, Email with type="email", others optional) with maxlength attributes matching `CustomerFormViewModel`
    - Form grid: `display:grid;grid-template-columns:1fr 1fr;gap:16px`
    - Name field spans full width; include validation message placeholder divs
    - Include "Save" (btn btn-primary) and "Cancel" (btn btn-secondary) buttons
    - Include `@Html.AntiForgeryToken()` hidden input
    - Modal hidden by default (`display:none`)
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6_

- [x] 5. Create JavaScript module for modal lifecycle
  - [x] 5.1 Create `customer-modal.js` in `Portal.Web/wwwroot/js/`
    - Implement `openCustomerModal(dropdownId)` — shows modal, clears fields, sets focus on Name, stores target dropdown id
    - Implement `closeCustomerModal()` — hides modal
    - Implement `submitCustomerModal()` — client-side validation, BlockUI.show("Creating customer..."), fetch POST to `/Customer/CreateInline`, handle response
    - Client-side validation: Name required (non-whitespace), Email format if non-empty
    - Display inline red validation messages adjacent to failing fields
    - Clear previous validation messages before re-evaluating
    - On success: BlockUI.hide(), close modal, append new `<option>` to target dropdown, set selected value, Swal.fire success
    - On server error: BlockUI.hide(), Swal.fire error with server message
    - On network error: BlockUI.hide(), Swal.fire generic error
    - On malformed response (missing id/name): leave dropdown unchanged, Swal.fire warning
    - Close modal on Escape key and backdrop click (without server request)
    - Include antiforgery token from DOM in request headers
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8, 5.1, 5.2, 5.3, 5.4, 7.1, 7.2, 7.3_

  - [x]* 5.2 Write property tests for client-side validation (fast-check)
    - **Property 1: Whitespace Name rejection** — Generate whitespace-only strings, verify validation rejects
    - **Property 2: Email format validation** — Generate invalid/valid emails, verify correct acceptance/rejection
    - **Validates: Requirements 3.1, 3.2, 3.4**

  - [x]* 5.3 Write property tests for dropdown update logic (fast-check)
    - **Property 4: Dropdown update and auto-selection** — Generate random {id, name} pairs, verify DOM manipulation
    - **Validates: Requirements 5.1, 5.2**

- [x] 6. Integrate modal into Quotation views
  - [x] 6.1 Update `Quotation/Create.cshtml` — Add "Add New" button next to customer dropdown
    - Wrap customer `<select>` and button in a flex row (`display:flex;gap:10px;align-items:flex-end`)
    - Add button: `<button type="button" class="btn btn-secondary" onclick="openCustomerModal('CustomerId')">+ Add New</button>`
    - Render partial: `@await Html.PartialAsync("_CustomerModal")`
    - Add script reference: `<script src="~/js/customer-modal.js"></script>`
    - _Requirements: 1.1, 1.5_

  - [x] 6.2 Update `Quotation/Edit.cshtml` — Add "Add New" button next to customer dropdown
    - Same flex-row pattern around the CustomerId `<select>`
    - Add button with `onclick="openCustomerModal('CustomerId')"`
    - Render partial and include script reference in Scripts section
    - _Requirements: 1.2, 1.5_

- [x] 7. Integrate modal into Invoice views
  - [x] 7.1 Update `Invoice/Create.cshtml` — Add "Add New" button next to customer dropdown
    - Wrap `#customerId` select and button in a flex row
    - Add button: `<button type="button" class="btn btn-secondary" onclick="openCustomerModal('customerId')">+ Add New</button>`
    - Render partial: `@await Html.PartialAsync("_CustomerModal")`
    - Add script reference in Scripts section
    - _Requirements: 1.3, 1.5_

  - [x] 7.2 Update `Invoice/Edit.cshtml` — Add "Add New" button next to customer dropdown
    - Same flex-row pattern around the `#customerId` select
    - Add button with `onclick="openCustomerModal('customerId')"`
    - Render partial and include script reference in Scripts section
    - _Requirements: 1.4, 1.5_

- [x] 8. Final checkpoint - Ensure all components are wired and functional
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- The `dropdownId` parameter handles the difference between Quotation views (`CustomerId` via asp-for) and Invoice views (`customerId` via raw HTML)
- No database schema migration is needed — only a query change in the repository

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2"] },
    { "id": 2, "tasks": ["1.3", "2.1"] },
    { "id": 3, "tasks": ["4.1"] },
    { "id": 4, "tasks": ["5.1"] },
    { "id": 5, "tasks": ["5.2", "5.3", "6.1", "6.2", "7.1", "7.2"] }
  ]
}
```
