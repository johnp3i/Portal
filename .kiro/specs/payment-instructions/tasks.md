# Implementation Plan: Payment Instructions

## Overview

Payment Instructions replaces Module 4 (Stripe Connect) with a lightweight bank-transfer payment flow. Implementation follows a bottom-up approach: database migrations first, then entity/repository updates, service layer, controller endpoints, and finally UI injection into the shared invoice page and business settings.

## Tasks

- [x] 1. Database migrations and schema changes
  - [x] 1.1 Create migration to add SwiftBic column to BusinessPaymentDetail
    - Create SQL migration file adding `[SwiftBic] NVARCHAR(11) NULL` to `[portal].[BusinessPaymentDetail]`
    - Follow existing migration numbering convention in `Portal.Database/Migrations/`
    - _Requirements: 6.1_

  - [x] 1.2 Create migration to add IsPaymentInstructionsEnabled to Business
    - Create SQL migration file adding `[IsPaymentInstructionsEnabled] BIT NOT NULL` with `DEFAULT 0` constraint to `[portal].[Business]`
    - _Requirements: 1.4_

  - [x] 1.3 Create migration to seed PaymentOnboard financial status
    - Create SQL migration file inserting `Id=6, Name='PaymentOnboard'` into `[invoice].[InvoiceFinancialStatusType]` using `SET IDENTITY_INSERT ON`
    - _Requirements: 5.1_

- [x] 2. Update entity classes and EF Core configuration
  - [x] 2.1 Add SwiftBic property to BusinessPaymentDetail entity
    - Add `public string? SwiftBic { get; set; }` to the `BusinessPaymentDetail` entity class
    - Update EF Core configuration: `.HasMaxLength(11).IsRequired(false)`
    - _Requirements: 6.1_

  - [x] 2.2 Add IsPaymentInstructionsEnabled property to Business entity
    - Add `public bool IsPaymentInstructionsEnabled { get; set; }` to the `Business` entity class
    - Update EF Core configuration: `.IsRequired().HasDefaultValue(false)`
    - _Requirements: 1.4_

- [x] 3. Update BusinessPaymentDetailRepository
  - [x] 3.1 Add SwiftBic to all repository queries
    - Update SELECT statements in `BusinessPaymentDetailRepository` to include `[SwiftBic]`
    - Update `InsertAsync` and `UpdateAsync` methods to accept and persist the `SwiftBic` field
    - Use full table names in queries (no aliases), null-safe parameters with `?? (object)DBNull.Value`
    - _Requirements: 6.1, 6.2_

- [x] 4. Create IPaymentInstructionsService interface and DTOs
  - [x] 4.1 Create service interface and data models
    - Create `IPaymentInstructionsService` in `Portal.Infrastructure.Services` with methods: `GetPaymentInstructionsAsync`, `DeclarePaymentAsync`, `SetPaymentInstructionsEnabledAsync`, `IsEnabledForBusinessAsync`
    - Create `PaymentInstructionsData` DTO (BusinessName, BankName, Iban, PayeeName, SwiftBic, OutstandingAmount, CurrencySymbol, DueDate, TransferReference)
    - Create `PaymentDeclarationResult` DTO (Success, Message, DeclaredAtUtc)
    - Create `ToggleResult` DTO (Success, Message)
    - _Requirements: 3.1, 3.2, 4.2_

- [x] 5. Implement PaymentInstructionsService
  - [x] 5.1 Implement GetPaymentInstructionsAsync
    - Query business toggle status, fetch active `BusinessPaymentDetail` with lowest `SortOrder`
    - Calculate outstanding amount (TotalAmount minus sum of confirmed payments)
    - Generate transfer reference in format `{InvoiceNumber} — {BusinessName}` (em-dash)
    - Return null if toggle disabled or no active payment details
    - _Requirements: 3.1, 3.2, 3.5, 3.6_

  - [x] 5.2 Implement DeclarePaymentAsync
    - Validate share token (active, not expired, matches existing invoice)
    - Check rate limit: count audit log entries for token in last hour, reject if >= 3
    - Verify invoice financial status is eligible (Unpaid=1, PartiallyPaid=2, Overdue=4)
    - Update `InvoiceFinancialStatusTypeId` to 6 (PaymentOnboard)
    - Create audit log entry with invoice ID, share token, UTC timestamp, IP address
    - Do NOT create a Payment record
    - _Requirements: 4.2, 4.3, 7.1, 7.2, 7.3, 7.4, 7.5_

  - [x] 5.3 Implement SetPaymentInstructionsEnabledAsync
    - Check business has at least one active `BusinessPaymentDetail` before enabling
    - Persist `IsPaymentInstructionsEnabled` value to business record
    - Return error result if no active bank details exist
    - _Requirements: 1.2, 1.3, 1.5_

  - [x] 5.4 Implement IsEnabledForBusinessAsync
    - Simple query to return `Business.IsPaymentInstructionsEnabled` value
    - _Requirements: 2.1, 2.2_

  - [x] 5.5 Write property tests for PaymentInstructionsService — toggle logic _(SKIPPED — optional, not implemented for MVP)_
    - **Property 1: Toggle persistence** — enabling/disabling results in correct persisted value
    - **Property 2: Toggle requires active bank details** — toggle rejected when no active records exist
    - **Validates: Requirements 1.2, 1.3, 1.5**

  - [x] 5.6 Write property tests for PaymentInstructionsService — payment instructions data _(SKIPPED — optional, not implemented for MVP)_
    - **Property 5: Transfer reference format** — format is always `{InvoiceNumber} — {BusinessName}`
    - **Property 6: Lowest SortOrder selection** — always returns record with minimum SortOrder
    - **Property 7: Outstanding amount calculation** — equals TotalAmount minus sum of payments, minimum 0
    - **Validates: Requirements 3.2, 3.5, 3.6**

  - [x] 5.7 Write property tests for PaymentInstructionsService — payment declaration _(SKIPPED — optional, not implemented for MVP)_
    - **Property 8: Payment declaration state transition** — eligible invoice transitions to PaymentOnboard with audit entry
    - **Property 13: Share token validation** — invalid/expired tokens rejected
    - **Property 14: Rate limiting** — 3+ declarations in last hour are rejected
    - **Property 15: No Payment record created on declaration** — Payment table unchanged
    - **Validates: Requirements 4.2, 4.3, 7.1, 7.2, 7.3, 7.4, 7.5**

- [x] 6. Checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Add toggle endpoint to MyBusinessController
  - [x] 7.1 Implement AxPostTogglePaymentInstructions endpoint
    - Add `[HttpPost]` method `AxPostTogglePaymentInstructions(bool enabled)` to `MyBusinessController`
    - Call `IPaymentInstructionsService.SetPaymentInstructionsEnabledAsync`
    - Return `Json(new { success, message })` following project conventions
    - Use try/catch with `(Exception ex)` and rethrow pattern in service, log and return error JSON in controller
    - _Requirements: 1.2, 1.3, 1.5_

- [x] 8. Add payment-instructions GET endpoint to InvoiceViewController
  - [x] 8.1 Implement AxGetPaymentInstructions endpoint
    - Add `[HttpGet][AllowAnonymous]` method at route `/invoice-view/{token}/payment-instructions`
    - Validate share token, look up invoice and business
    - Call `IPaymentInstructionsService.GetPaymentInstructionsAsync`
    - Return `Json(new { success, data })` with payment instructions or error
    - _Requirements: 3.1, 3.2, 3.3, 3.5, 3.6_

- [x] 9. Add declare-payment POST endpoint to InvoiceViewController
  - [x] 9.1 Implement declare-payment endpoint
    - Add `[HttpPost][AllowAnonymous]` method at route `/invoice-view/{token}/declare-payment`
    - Extract client IP address from `HttpContext.Connection.RemoteIpAddress`
    - Call `IPaymentInstructionsService.DeclarePaymentAsync(token, ipAddress)`
    - Return `Json(new { success, message })` result
    - _Requirements: 4.2, 4.3, 7.1, 7.2, 7.3, 7.4, 7.5_

  - [x] 9.2 Write property tests for button visibility logic _(SKIPPED — optional, not implemented for MVP)_
    - **Property 3: Button visibility rule** — button visible iff toggle enabled AND status in {1, 2, 4}
    - **Property 11: SwiftBic conditional display** — SWIFT/BIC appears iff non-null and non-empty
    - **Validates: Requirements 2.1, 2.2, 2.3, 6.3, 6.4**

- [x] 10. Inject "Pay by Bank Transfer" button and modal into shared invoice page
  - [x] 10.1 Inject button HTML and modal into InvoiceViewController.ViewInvoice
    - Extend the existing `ViewInvoice` action to inject "Pay by Bank Transfer" button HTML after acceptance section when eligible (toggle enabled AND status in {1, 2, 4})
    - Inject hidden modal HTML at end of body with: info card (outstanding amount, due date, transfer reference), bank details section (bank name, IBAN with copy button, SWIFT/BIC conditional row, payee name), warning note, and "I've made the payment" button
    - Inject inline `<script>` block handling: button click → show modal, close modal, copy-to-clipboard, "I've made the payment" → BlockUI → fetch POST `/invoice-view/{token}/declare-payment` → BlockUI hide → SweetAlert2 result → replace button with status badge
    - AJAX call to fetch bank details on modal open (GET `/invoice-view/{token}/payment-instructions`), not on page load
    - Show "Payment Onboard — Awaiting Verification" status badge when invoice status is PaymentOnboard (6)
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.2, 4.4, 4.5, 4.6_

- [x] 11. Checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 12. Add toggle UI to Business Settings page
  - [x] 12.1 Add Payment Instructions toggle to settings view
    - Add a toggle row labelled "Show bank transfer payment option on shared invoices" to the Business Settings page
    - Wire toggle change event: BlockUI → fetch POST `AxPostTogglePaymentInstructions` → Unblock → Reload (quick toggle operation — no SweetAlert2 needed)
    - If business has no active `BusinessPaymentDetail`, disable the toggle and show info message: "Add bank details in your payment details section before enabling this option"
    - _Requirements: 1.1, 1.2, 1.3, 1.5_

  - [x] 12.2 Add SWIFT/BIC input field to payment details section
    - Add SWIFT/BIC input field (max 11 characters) to the add/edit bank account form in Business Settings
    - Include client-side validation for max length
    - _Requirements: 6.1, 6.2_

- [x] 13. Update Invoice Detail page for PaymentOnboard status
  - [x] 13.1 Add PaymentOnboard info banner to Invoice Detail view
    - When invoice has `InvoiceFinancialStatusTypeId = 6`, display an amber informational banner: "The customer has declared that payment was made via bank transfer. This is a customer declaration only — please verify receipt on your bank statement before marking as paid."
    - Include a "Record Payment" CTA button linking to the existing payment recording flow
    - Ensure business owner can still record payments and change status manually
    - _Requirements: 5.2, 5.3_

- [x] 14. Update invoice list and filters for PaymentOnboard status
  - [x] 14.1 Add PaymentOnboard to invoice list filters and display
    - Add "PaymentOnboard" option to financial status filter dropdowns on invoice list page
    - Ensure PaymentOnboard invoices render with appropriate badge styling in the list
    - _Requirements: 5.4_

  - [x] 14.2 Write property tests for PaymentOnboard status behaviour _(SKIPPED — optional, not implemented for MVP)_
    - **Property 9: PaymentOnboard does not lock invoice** — business can still record payments and change status
    - **Property 10: Auto-transition to Paid on full payment** — status becomes Paid when payments >= TotalAmount, regardless of previous PaymentOnboard
    - **Property 12: Audit log creation on declaration** — audit entry contains invoice ID, token, UTC timestamp, IP
    - **Validates: Requirements 5.3, 5.5, 7.1**

- [x] 15. Update Phase 1 Development Timetable
  - [x] 15.1 Replace Module 4 (Stripe Connect) with Payment Instructions tasks
    - Update `.kiro/docs/Phase1_Development_Timetable.md` to replace Stripe Connect module tasks with Payment Instructions tasks
    - Remove any references to Stripe API keys, OAuth flows, webhook endpoints
    - Add a section documenting Option C (Stripe Connect) as a future upgrade path
    - _Requirements: 8.1, 8.2, 8.3, 9.1, 9.2, 9.3_

- [x] 16. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Status

Module complete and deployed. All implementation tasks (1–4, 5.1–5.4, 6–8, 9.1, 10–13, 14.1, 15–16) are done.
The optional property-test tasks (5.5, 5.6, 5.7, 9.2, 14.2) were **intentionally not implemented** for this MVP
and remain marked `[~]` (skipped). If test coverage is added later, revisit those tasks specifically.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests use FsCheck (FsCheck.Xunit for .NET) with minimum 100 iterations per property
- The modal HTML/JS injection follows the existing pattern in `InvoiceViewController.ViewInvoice()` (same approach as acceptance UI and download buttons)
- All AJAX calls follow the BlockUI → fetch → Unblock → SweetAlert2 pattern (except quick toggle operations which use BlockUI → fetch → Unblock → Reload)
- Rate limiting uses SQL-based counting on the AuditLog table (3 declarations per token per hour)
- The Payment declaration does NOT create a Payment record — only changes invoice financial status and logs

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["2.1", "2.2"] },
    { "id": 2, "tasks": ["3.1", "4.1"] },
    { "id": 3, "tasks": ["5.1", "5.2", "5.3", "5.4"] },
    { "id": 4, "tasks": ["5.5", "5.6", "5.7", "7.1"] },
    { "id": 5, "tasks": ["8.1", "9.1"] },
    { "id": 6, "tasks": ["9.2", "10.1"] },
    { "id": 7, "tasks": ["12.1", "12.2", "13.1", "14.1"] },
    { "id": 8, "tasks": ["14.2", "15.1"] }
  ]
}
```
