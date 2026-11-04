# Requirements — Edit a Payment's Method and Reference

## Summary

Users need the ability to correct a recorded payment's **payment method** and its **reference text** after the fact — for example, a customer said they would pay in cash but actually made a bank transfer, and the payment was already recorded with the wrong method. Today a payment's method and reference are fixed at record time; the only post-record action is void.

This feature adds an **Edit Payment** action to the two surfaces where payments are listed — the Revenue Dashboard (`#paymentsTable` rows) and the customer Statement page (client-rendered transaction table) — opening a shared modal that edits **only** the payment method and the reference. All other fields (amount, payment date, allocation, customer/invoice links) stay locked. The change cascades to any non-voided receipt so a re-viewed/re-downloaded receipt reflects the corrected method and reference, and is recorded in the append-only audit log. Voided payments cannot be edited.

This is a decided, tightly-scoped feature. One shared service method, one shared controller endpoint, and one shared edit modal serve both pages — no parallel implementations.

### Assumptions (flagged for design review)

1. **One shared modal, newly created.** The request says "one shared edit modal, reused by both pages." The existing `_PaymentModal.cshtml` (dashboard) and the inline Record-Payment / `_GlobalPaymentModal` markup (statement) are record-payment modals, not edit modals. The edit modal is assumed to be a **new** shared partial (e.g. `_EditPaymentModal.cshtml`) rendered on both pages, modelled on the DB-driven dropdown pattern those existing modals use. The design chooses the exact file/location.
2. **Endpoint lives in `RevenueController`.** `RevenueController` already owns `RecordPayment`, `VoidPayment`, `AxPostVoidPaymentSmart`, and renders `_PaymentModal`; the Statement page's void/record actions already call `/Revenue/...` endpoints. The new endpoint is assumed to live in `RevenueController` and be called by both pages' JS. The design confirms.
3. **Statement line reference is a composite display string.** `StatementService` builds `StatementLineDto.Reference` as a composite (`"{Method} · Ref: {reference} · {invoiceNumber}"`), not the raw reference. The modal must preselect from the payment's **raw** current method id and **raw** reference, so the raw values (and `PaymentMethodTypeId`) must be carried through to the statement row — not parsed back out of the composite string. The design decides how (extend `StatementPaymentDto` + `StatementLineDto`, or fetch the payment fresh when the modal opens).
4. **Statement payment lines currently render with `IsVoided = false` hardcoded.** The statement JS already guards the Edit/Void/Receipt actions behind `!line.isVoided`, and `renderTransactionTable` only reaches voided-payment styling when `line.isVoided` is true — but `StatementService` sets `IsVoided = false` on every payment line it builds. Whether voided payments appear in the statement at all is out of scope to change; the requirement is only that the Edit button must never appear for a voided payment. If the statement never surfaces voided payment lines, the client guard is still required as defence in depth, and the **server** guard is authoritative regardless.
5. **VAT-period lock stance: allow the edit.** Method + reference change neither the amount nor the payment date (which drives VAT-period assignment), so the edit has no VAT impact. The default decision is to **allow** editing method/reference even when the payment's date falls in a submitted VAT period. The design must confirm this against the codebase's actual stance (the credit-note precedent blocks period-changing/amount-changing mutations, which is not analogous) and document the final decision with reasoning. This is the one genuinely open decision; requirements record the default, design locks it.
6. **"Reference" is optional and may be cleared.** The reference input accepts empty/blank, saved as SQL `NULL` (consistent with how `Payment.Reference` and `PaymentReceipt.PaymentReference` are already stored nullable).

## Functional Requirements

### Edit entry points

- **FR-1** On the Revenue Dashboard payments table (`Views/Revenue/Dashboard.cshtml`, rendered by `wwwroot/js/revenue-dashboard.js`), each **non-voided** payment row shows an **Edit** button alongside the existing Receipt and Void buttons. Voided rows show no Edit button (they already render `—`).
- **FR-2** On the Statement page transaction table (`Views/Statement/Index.cshtml`, `renderTransactionTable`), each **non-voided** payment line shows an **Edit** button alongside the existing Receipt and Void buttons. Voided payment lines show no Edit button.
- **FR-3** Both Edit buttons open the **same** shared Edit Payment modal and, on save, call the **same** controller endpoint. No duplicated edit logic between the two pages.

### Edit modal

- **FR-4** The modal is titled "Edit Payment" and contains:
  - A **read-only context line** showing customer / invoice / amount / date (displayed but disabled — not submittable).
  - An **editable Payment Method dropdown**.
  - An **editable Reference text input**.
  - **Cancel** and **Save** buttons.
- **FR-5** The Payment Method dropdown is populated from the real `PaymentMethodType` rows via `IPaymentService.GetPaymentMethodTypesAsync()`, exposed to the view through `ViewBag.PaymentMethods`, rendering `<option value="@method.Id">@method.Name</option>`. Method ids and labels are **never** hardcoded. (This mirrors the fix already applied to `_PaymentModal.cshtml`, `_GlobalPaymentModal.cshtml`, and `StatementController.Index`, which exists precisely because a previously hardcoded dropdown had ids that did not match the seeded DB ids: `1=Cash, 2=BankTransfer, 3=Card, 4=Cheque, 5=Other`.)
- **FR-6** When opened, the modal pre-selects the dropdown to the payment's current `PaymentMethodTypeId` and pre-fills the Reference input with the payment's current reference value.
- **FR-7** To support FR-6, the data behind each page's payment rows must carry `PaymentMethodTypeId` and the raw reference:
  - Revenue dashboard: `RecentPaymentDto` (fed by `PaymentRepository.GetRecentPaymentsPagedAsync`) gains `PaymentMethodTypeId` (it already carries `Reference` and `PaymentMethodName`).
  - Statement: the statement payment line must carry `PaymentMethodTypeId` and the raw reference through to the client (via `StatementPaymentDto` → `StatementLineDto`, following how those rows are currently built), since `StatementLineDto.Reference` is a composite display string unsuitable for pre-fill.

### Service behaviour

- **FR-8** Add `Task<ServiceResult> UpdatePaymentMethodAndReferenceAsync(int paymentId, int newPaymentMethodTypeId, string? newReference, int businessId, string userId)` to `IPaymentService` / `PaymentService`. It executes within a **single DB transaction** and performs, in order:
  1. Load the payment via `PaymentRepository.GetByIdAndBusinessIdAsync(paymentId, businessId)`. **Fail** (`ServiceResult.Fail`) if not found (tenant isolation).
  2. **Fail** if `payment.IsVoided` — voided payments cannot be edited.
  3. **Validate** that `newPaymentMethodTypeId` exists in `PaymentMethodTypes`; fail with a clear message if it does not.
  4. Apply the **VAT-period lock decision** from Assumption 5 (design-locked). Default: allow.
  5. Update `Payment.PaymentMethodTypeId` and `Payment.Reference` using a raw-SQL update consistent with `PaymentRepository.UpdateCreditAmountAsync` style (add a new repository method, e.g. `UpdateMethodAndReferenceAsync`).
  6. **Cascade to receipt:** for the non-voided `PaymentReceipt` whose `PaymentId == paymentId` (if any), update its `PaymentMethodTypeId` and `PaymentReference` to match. A new receipt-repository update method is added (none exists today). Receipt detail reads the method name from `receipt.PaymentMethodTypeId`, not live from the payment, so this cascade is required for the receipt to reflect the change.
  7. Write an **append-only AuditLog** entry via `AuditLogRepository.InsertAsync`, mirroring the `CreditNoteService` pattern: `BusinessId`, `UserId`, `Action = "PaymentMethodUpdated"`, `TableName = "Payment"`, `RecordId = paymentId.ToString()`, `OldValues`/`NewValues` as small JSON capturing old/new method id and reference, `Timestamp = DateTime.UtcNow`.
  8. Return `ServiceResult.Ok()` / `ServiceResult.Fail(...)`.
- **FR-9** `PaymentService` does not currently inject `AuditLogRepository`; add it to the constructor.
- **FR-10** All `catch` blocks use `catch (Exception ex)` (house rule), and repository methods rethrow with `throw;`.

### Controller endpoint

- **FR-11** Add one AJAX JSON endpoint, `AxPostUpdatePaymentMethod(int paymentId, int paymentMethodTypeId, string? reference)`, decorated `[HttpPost]` `[ValidateAntiForgeryToken]`, wrapped in `try/catch (Exception ex)`, returning `Json(new { success, message })`. It resolves `businessId` from the tenant service and `userId` from claims (as `RecordPayment` does), calls `UpdatePaymentMethodAndReferenceAsync`, and returns the service result.
- **FR-12** Both the Statement page and the Revenue dashboard JS call this one endpoint.
- **FR-13** Ensure both hosting views expose `ViewBag.PaymentMethods` for the modal dropdown. `StatementController.Index` and `RevenueController.Dashboard` already set `ViewBag.PaymentMethods = await _paymentService.GetPaymentMethodTypesAsync()`; verify and reuse — do not duplicate.

### UI feedback

- **FR-14** The save flow follows the project AJAX convention: `BlockUI.show(...)` before the request, vanilla `fetch` with the `RequestVerificationToken` antiforgery header, `BlockUI.hide()` in **both** success and error paths, then `Swal.fire(...)` for success/error feedback. Never native `alert`/`confirm`.
- **FR-15** On success, the relevant table refreshes (dashboard re-fetches/reloads the payments table; statement regenerates) so the updated method and reference are visible without a manual refresh.

## Non-Functional Requirements

- **NFR-1 Tenant isolation.** Every load and mutation is scoped by `businessId`; a payment belonging to another business must behave as "not found."
- **NFR-2 Atomicity.** Payment update, receipt cascade, and audit insert all succeed or all roll back within one transaction.
- **NFR-3 Auditability.** Every successful edit produces exactly one append-only audit entry with the old and new values.
- **NFR-4 Consistency with house standards.** `AxPost` naming, `catch (Exception ex)`, raw-SQL repository style with null-safe parameters (`?? (object)DBNull.Value`), full table names in SQL (no short aliases), SweetAlert2 + BlockUI for UI feedback.

## Acceptance Criteria

1. On the Revenue Dashboard, a non-voided payment row shows Edit, Receipt, and Void buttons; a voided row shows none of them (renders `—`).
2. On the Statement page, a non-voided payment line shows Edit, Receipt, and Void buttons; a voided payment line shows no Edit button.
3. Clicking Edit on either page opens a modal titled "Edit Payment" showing customer/invoice/amount/date as disabled read-only context, plus an editable method dropdown and an editable reference input.
4. The method dropdown's options come from the live `PaymentMethodType` table (ids and names), rendered as `<option value="@method.Id">@method.Name</option>` — no hardcoded ids or labels anywhere in the modal.
5. On open, the dropdown is pre-selected to the payment's current `PaymentMethodTypeId` and the reference input shows the payment's current reference (blank if none).
6. Saving a change to method and/or reference persists the new values to `[revenue].[Payment]` for the correct payment (verified by re-opening the modal or reloading, which shows the new values).
7. After a save, if a non-voided receipt exists for that payment, re-viewing the receipt shows the updated payment method name and reference (the receipt snapshot was updated).
8. After a save, exactly one `[audit].[AuditLog]` row exists with `Action = "PaymentMethodUpdated"`, `TableName = "Payment"`, `RecordId` = the payment id, and `OldValues`/`NewValues` containing the before/after method id and reference.
9. Attempting the edit (via the endpoint) for a **voided** payment returns `success = false` with a message stating voided payments cannot be edited, and makes no DB change.
10. Attempting the edit with a `paymentMethodTypeId` that does not exist in `PaymentMethodTypes` returns `success = false` with a clear message and makes no DB change.
11. Attempting the edit for a payment belonging to a different business returns a not-found failure and makes no DB change.
12. The VAT-period behaviour matches the design-locked decision (default: edit is allowed regardless of the period's submission state, because method/reference do not affect VAT).
13. If any step of the transaction fails, no partial change persists (payment, receipt, and audit are all rolled back).
14. Both pages call the same `/Revenue/AxPostUpdatePaymentMethod` endpoint; there is one service method and one modal partial — no duplicated edit implementation.
15. The save flow blocks the UI during the request, unblocks on both success and error, and shows a SweetAlert2 success or error message; no native `alert`/`confirm` is used. On success the originating table refreshes to show the new values.
16. The solution builds with `dotnet build Portal.Web/Portal.Web.csproj -c Debug` reporting `0 Error(s)`.

## Out of Scope

- Editing amount, payment date, allocation, or customer/invoice links.
- Any editing of voided payments.
- A new payment-method display-label scheme — render `@method.Name` as the rest of the app does.
- Data migration/backfill of historically mis-saved payment methods.
- Changing whether voided payments are listed on the statement.
- Any change to the credit-note emphasis or invoice-total work already completed.
- Receipt regeneration or re-numbering (only the existing non-voided receipt's method/reference snapshot is updated in place).
