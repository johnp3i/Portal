# Technical Design — Edit a Payment's Method and Reference

## Overview

This feature adds an **Edit Payment** action that lets a user correct an already-recorded payment's
**payment method** and **reference text** — nothing else. The two surfaces that list payments (the
Revenue Dashboard `#paymentsTable` and the customer Statement transaction table) each gain an Edit
button on **non-voided** payment rows. Both open one shared modal and, on save, call one shared
controller endpoint (`RevenueController.AxPostUpdatePaymentMethod`) that delegates to one new service
method (`PaymentService.UpdatePaymentMethodAndReferenceAsync`). The service, inside a single
transaction, loads the payment (tenant-scoped), refuses voided payments, validates the new method,
updates the payment, cascades the change to the payment's non-voided receipt snapshot, and writes an
append-only audit entry. Amount, date, allocation, and customer/invoice links are never touched.

The design follows the patterns already established in the codebase: raw-SQL repositories matching
`PaymentRepository`/`PaymentReceiptRepository` style, the `ServiceResult` return contract, the
`CreditNoteService` transaction + `AuditLogRepository.InsertAsync` audit pattern, the `AxPost…`
controller convention returning `Json(new { success, message })`, and the vanilla-`fetch` +
`RequestVerificationToken` + `BlockUI` + `SweetAlert2` UI convention.

The technology stack is locked: ASP.NET Core MVC 8, EF Core (database-first) against SQL Server,
raw-SQL repositories over `PortalDbContext`, Razor views + vanilla JS (no jQuery/no framework),
SweetAlert2 and the project's `BlockUI` helper. No new libraries are introduced.

## Components changed (file inventory)

| Layer | File | Change |
|-------|------|--------|
| Entity DTO | `Portal.Infrastructure/Models/RecentPaymentDto.cs` | add `int PaymentMethodTypeId` |
| Query DTO | `Portal.Infrastructure/Models/StatementPaymentDto.cs` | add `int PaymentMethodTypeId` |
| Line DTO | `Portal.Infrastructure/Models/StatementLineDto.cs` | add `int? PaymentMethodTypeId` + `string? RawReference` |
| Repo | `Portal.Infrastructure/Repositories/PaymentRepository.cs` | add `UpdateMethodAndReferenceAsync`; extend `GetRecentPaymentsPagedAsync` SELECT |
| Repo | `Portal.Infrastructure/Repositories/PaymentReceiptRepository.cs` | add `UpdateMethodAndReferenceByPaymentIdAsync` |
| Repo | `Portal.Infrastructure/Repositories/StatementRepository.cs` | extend `GetPaymentsInPeriodAsync` SELECT with `PaymentMethodTypeId` |
| Service | `Portal.Infrastructure/Services/StatementService.cs` | carry raw method id + raw reference onto the payment line |
| Service iface | `Portal.Infrastructure/Services/IPaymentService.cs` | add `UpdatePaymentMethodAndReferenceAsync` |
| Service | `Portal.Infrastructure/Services/PaymentService.cs` | implement method; inject `AuditLogRepository` + `PaymentReceiptRepository` |
| Controller | `Portal.Web/Controllers/RevenueController.cs` | add `AxPostUpdatePaymentMethod` |
| View (modal) | `Portal.Web/Views/Shared/_EditPaymentModal.cshtml` | **new** shared partial |
| View | `Portal.Web/Views/Revenue/Dashboard.cshtml` | render Edit button (server rows); include modal partial; emit method id in row |
| View | `Portal.Web/Views/Statement/Index.cshtml` | render Edit button (JS rows); include modal partial |
| JS | `Portal.Web/wwwroot/js/revenue-dashboard.js` | render Edit button in JS-built rows; carry method id |
| JS | `Portal.Web/wwwroot/js/revenue-payment.js` | add `openEditPaymentModal`/`submitEditPayment`/`closeEditPaymentModal`, exposed on `window` |

No DI registration changes are required beyond the constructor edit (see "DI" below): `PaymentService`,
`PaymentRepository`, `PaymentReceiptRepository`, and `AuditLogRepository` are all already registered
(the receipt repository is consumed by `PaymentReceiptService`, the audit repository by
`CreditNoteService`/`StatementService`).

## Data flow for the modal pre-fill (the pre-selection problem)

The modal must pre-select the payment's **current** `PaymentMethodTypeId` and show its **raw**
reference. Neither surface carries the raw method id today, and the statement's `Reference` field is a
**composite display string** (`"{Method} · Ref: {ref} · {InvoiceNumber} · from €X payment"`) that must
not be reverse-parsed. Two options were considered:

- **(A) Fetch the payment fresh when the Edit button is clicked** (new `AxGetPayment` endpoint).
- **(B) Carry `PaymentMethodTypeId` + raw reference through the existing row data.**

**Chosen: (B).** The dashboard already carries the raw `Reference` on `RecentPaymentDto`; adding one
int (`PaymentMethodTypeId`) is a trivial, consistent extension and avoids a second round-trip and a
second endpoint. The statement path needs the raw reference anyway (the composite is unusable for
pre-fill), so (A) would still require touching the same query. (B) keeps the modal open-on-click
instant and matches how the dashboard's row already ships `reference`. The requirements explicitly
endorse this (FR-7).

### Dashboard path
- `PaymentRepository.GetRecentPaymentsPagedAsync` already SELECTs `Payment.Reference`; add
  `[revenue].[Payment].[PaymentMethodTypeId]` to the SELECT and map it to the new
  `RecentPaymentDto.PaymentMethodTypeId`. (Full table names, no aliases — repository standard.)
- Server-rendered rows in `Dashboard.cshtml` already have `item.PaymentMethodTypeId`,
  `item.Reference`, plus customer/invoice/amount/date in scope — the Edit button's `onclick` passes
  these to `openEditPaymentModal(...)`.
- JS-rendered rows in `revenue-dashboard.js` (`renderPaymentsTable`) read `item.paymentMethodTypeId`
  (camel-cased automatically by the `GetRecentPayments` JSON serializer, which serializes the DTO).

### Statement path
- `StatementRepository.GetPaymentsInPeriodAsync` SELECTs `PaymentMethodType.Name`; add
  `[revenue].[Payment].[PaymentMethodTypeId]` to the SELECT and map it to a new
  `StatementPaymentDto.PaymentMethodTypeId`.
- `StatementService.GenerateStatementAsync`, where it builds each payment `StatementLineDto`, sets two
  new fields: `PaymentMethodTypeId = payment.PaymentMethodTypeId` and `RawReference = payment.Reference`
  (the raw nullable reference, NOT the composite). The existing composite `Reference` is unchanged.
- `StatementController.Generate`'s anonymous projection adds `paymentMethodTypeId = l.PaymentMethodTypeId`
  and `rawReference = l.RawReference` so the client JS receives them.
- `renderTransactionTable` passes `line.paymentMethodTypeId` and `line.rawReference` to
  `openEditPaymentModal(...)`.

Only payment lines carry these; invoice/credit-note/opening/closing lines leave `PaymentMethodTypeId`
null, which is correct (they never render an Edit button).

## The shared modal — `_EditPaymentModal.cshtml`

A new partial under `Views/Shared/` (shared because both a Revenue view and a Statement view include
it; `_GenerateReceiptModal.cshtml` already lives in `Shared/` and is included by both pages, so this is
the established home for cross-page modals). It is rendered once per page via
`@await Html.PartialAsync("_EditPaymentModal")` added to both `Dashboard.cshtml` and
`Statement/Index.cshtml`, right beside their existing `_GenerateReceiptModal` include.

Markup mirrors `_PaymentModal.cshtml` styling (same overlay, card, `.field` classes) and the same
DB-driven dropdown:

```cshtml
@{
    var paymentMethods = ViewBag.PaymentMethods as List<Portal.Infrastructure.Entities.PaymentMethodType>
                         ?? new List<Portal.Infrastructure.Entities.PaymentMethodType>();
}
<div id="editPaymentModal" style="display:none; …overlay…">
  <div style="…card…">
    <h2>Edit Payment</h2>
    <p class="muted">Correct the payment method or reference. Amount and date cannot be changed.</p>

    <!-- read-only context (disabled, not submittable) -->
    <div id="editPaymentContext" …>
      <div>Customer: <span id="editPaymentCustomer">—</span></div>
      <div>Invoice:  <span id="editPaymentInvoice">—</span></div>
      <div>Amount:   <span id="editPaymentAmount">—</span></div>
      <div>Date:     <span id="editPaymentDate">—</span></div>
    </div>

    <form id="editPaymentForm" onsubmit="return false;">
      @Html.AntiForgeryToken()
      <input type="hidden" id="editPaymentId" />
      <div class="field">
        <label for="editPaymentMethod">Payment Method *</label>
        <select id="editPaymentMethod" required>
          @foreach (var method in paymentMethods)
          {
              <option value="@method.Id">@method.Name</option>
          }
        </select>
      </div>
      <div class="field">
        <label for="editPaymentReference">Reference</label>
        <input type="text" id="editPaymentReference" maxlength="200" placeholder="e.g. TXN-12345" />
      </div>
      <div style="…buttons…">
        <button type="button" class="btn btn-secondary" onclick="closeEditPaymentModal()">Cancel</button>
        <button type="button" class="btn btn-primary" onclick="submitEditPayment()">Save</button>
      </div>
    </form>
  </div>
</div>
```

The dropdown options come **only** from `ViewBag.PaymentMethods` (`IPaymentService.GetPaymentMethodTypesAsync()`);
no ids or labels are hardcoded (FR-5 — this is the exact bug class the requirements call out). Both
`RevenueController.Dashboard` and `StatementController.Index` already set
`ViewBag.PaymentMethods = await _paymentService.GetPaymentMethodTypesAsync()` (verified in source), so
no controller change is needed for the dropdown data (FR-13 — verify-and-reuse, do not duplicate).

Each page renders exactly one `#editPaymentModal`. Because the partial is page-scoped and only the two
target pages include it, there is no id collision. The antiforgery token already exists on both pages
(dashboard via `_PaymentModal`'s `@Html.AntiForgeryToken()` and the page; statement via its top-level
`@Html.AntiForgeryToken()`), and `getAntiForgeryToken()` reads `input[name="__RequestVerificationToken"]`
— the token inside the modal form is harmless/duplicate-safe.

### JS entry point (shared, in `revenue-payment.js`)

`revenue-payment.js` is loaded by the dashboard. The statement page does **not** currently load it, so
the three new functions will be defined in `revenue-payment.js` **and** the statement page will add
`<script src="/js/revenue-payment.js"></script>`. (Alternative: duplicate the functions inline in the
statement page — rejected, it would create the parallel implementation the requirements forbid.) The
statement page already defines its own `getAntiForgeryToken`, `escapeHtml`, and `BlockUI` usage; the
shared functions use `getAntiForgeryToken()` which both pages provide, so loading the file on the
statement page is safe (its other exported functions like `voidPayment` simply go unused there, same as
any shared script).

```js
function openEditPaymentModal(paymentId, methodId, rawReference, customer, invoice, amount, date) {
    document.getElementById('editPaymentId').value = paymentId;
    document.getElementById('editPaymentMethod').value = methodId;      // pre-select current method
    document.getElementById('editPaymentReference').value = rawReference || '';
    document.getElementById('editPaymentCustomer').textContent = customer || '—';
    document.getElementById('editPaymentInvoice').textContent  = invoice || '—';
    document.getElementById('editPaymentAmount').textContent   = amount || '—';
    document.getElementById('editPaymentDate').textContent     = date || '—';
    document.getElementById('editPaymentModal').style.display = 'flex';
}
function closeEditPaymentModal() { document.getElementById('editPaymentModal').style.display = 'none'; }

async function submitEditPayment() {
    var paymentId = document.getElementById('editPaymentId').value;
    var methodId  = document.getElementById('editPaymentMethod').value;
    var reference = document.getElementById('editPaymentReference').value;
    if (!methodId) { Swal.fire({ icon:'warning', title:'Payment method required', confirmButtonColor:'#0D5EA6' }); return; }

    BlockUI.show('Saving payment...');
    try {
        var fd = new FormData();
        fd.append('paymentId', paymentId);
        fd.append('paymentMethodTypeId', methodId);
        fd.append('reference', reference);               // empty string → server treats as null
        var res = await fetch('/Revenue/AxPostUpdatePaymentMethod', {
            method: 'POST',
            headers: { 'RequestVerificationToken': getAntiForgeryToken() },
            body: fd
        });
        var data = await res.json();
        BlockUI.hide();
        if (data.success) {
            closeEditPaymentModal();
            Swal.fire({ icon:'success', title:'Payment updated', text:data.message, confirmButtonColor:'#0D5EA6' })
                .then(function () { reloadAfterPaymentEdit(); });
        } else {
            Swal.fire({ icon:'error', title:'Error', text:data.message, confirmButtonColor:'#0D5EA6' });
        }
    } catch (e) {
        BlockUI.hide();
        Swal.fire({ icon:'error', title:'Error', text:'An unexpected error occurred. Please try again.', confirmButtonColor:'#0D5EA6' });
    }
}
window.openEditPaymentModal = openEditPaymentModal;
window.closeEditPaymentModal = closeEditPaymentModal;
window.submitEditPayment = submitEditPayment;
```

**Refresh after save (FR-15).** The two pages refresh differently, so a tiny indirection is used
instead of a hardcoded reload: the shared `submitEditPayment` calls `reloadAfterPaymentEdit()` if the
page defines one, else falls back to `location.reload()`.

```js
function reloadAfterPaymentEdit() {
    if (typeof window.reloadAfterPaymentEdit === 'function' && window.reloadAfterPaymentEdit !== reloadAfterPaymentEdit) {
        return window.reloadAfterPaymentEdit();
    }
    location.reload();
}
```

- **Dashboard:** `location.reload()` is acceptable and matches the existing `voidPayment` success
  behaviour (which already calls `location.reload()`), so no page override is needed.
- **Statement:** the page defines `window.reloadAfterPaymentEdit = function () { generateStatement(); };`
  in its inline script, matching how its `voidPaymentFromStatement` success path calls
  `generateStatement()`. Re-generating rebuilds the composite reference and re-reads the method name.

### Button wiring

- **Dashboard server rows (`Dashboard.cshtml`)** — inside the existing `@if (!item.IsVoided)` block,
  before/after Receipt+Void:
  ```cshtml
  <button type="button" class="tbl-action tbl-action--primary"
      onclick="openEditPaymentModal(@item.PaymentId, @item.PaymentMethodTypeId,
               '@Html.Raw(Html.Encode(item.Reference))', '@Html.Raw(Html.Encode(item.CustomerName))',
               '@Html.Raw(Html.Encode(item.InvoiceNumber))', '@currency@item.Amount.ToString("N2")',
               '@item.PaymentDateUtc.ToString("yyyy-MM-dd")')">Edit</button>
  ```
  String args are HTML-encoded for the attribute; because values flow into a JS string literal in an
  `onclick`, the design uses `Html.Encode` and single-quoted JS strings. (Customer names rarely contain
  quotes; to be robust the implementer should prefer passing the values via `data-*` attributes +
  `addEventListener` if any customer name could contain an apostrophe. See "Edge cases".)
- **Dashboard JS rows (`revenue-dashboard.js` `renderPaymentsTable`)** — in the `if (!item.isVoided)`
  branch, add an Edit button built the same way as Receipt/Void, passing
  `item.paymentMethodTypeId, item.reference, item.customerName, item.invoiceNumber,
  currencySymbol + formatAmount(item.amount), formatDate(item.paymentDateUtc)`.
- **Statement JS rows (`Index.cshtml` `renderTransactionTable`)** — in the existing
  `if (line.type === 'Payment' && line.paymentId && !line.isVoided)` block, append an Edit button:
  ```js
  var editBtn = document.createElement('button');
  editBtn.textContent = 'Edit';
  editBtn.className = 'tbl-action tbl-action--primary';
  editBtn.style.marginLeft = '6px';
  editBtn.onclick = function () {
      openEditPaymentModal(line.paymentId, line.paymentMethodTypeId, line.rawReference,
                           document.getElementById('customerName').textContent, '', line.credit, line.date);
  };
  tdActions.appendChild(editBtn);
  ```
  (The statement line does not carry a clean invoice number separate from the composite; invoice is
  passed empty and the context line simply shows a dash — acceptable, the context is advisory and the
  authoritative customer/amount/date are present. Amount uses `line.credit`, the payment amount.)

The voided guard is enforced in three places: the server `@if (!item.IsVoided)` (dashboard rows), the
`!item.isVoided`/`!line.isVoided` JS guards (both tables), **and authoritatively** in the service
(step 2 below). The statement sets `IsVoided = false` on every payment line it builds (per requirements
Assumption 4); the client guard is defence-in-depth and the server guard is the real gate.

## Service — `PaymentService.UpdatePaymentMethodAndReferenceAsync`

### Signature (added to `IPaymentService` and `PaymentService`)

```csharp
Task<ServiceResult> UpdatePaymentMethodAndReferenceAsync(
    int paymentId, int newPaymentMethodTypeId, string? newReference, int businessId, string userId);
```

### DI / constructor change (FR-9)

`PaymentService` currently injects `PaymentRepository`, `InvoiceRepository`, `CreditNoteRepository`,
`IFinancialStatusEngine`, `IPaymentScheduleService`, `IPaymentAllocationEngine`, `PortalDbContext`,
optional `IPaymentReceiptService?`, optional `INotificationProducer?`. Add two constructor dependencies:
`AuditLogRepository auditLogRepository` and `PaymentReceiptRepository paymentReceiptRepository`, stored
in readonly fields.

- **Why the receipt *repository* and not `IPaymentReceiptService`?** The cascade is a targeted column
  update on the existing receipt snapshot; `IPaymentReceiptService` exposes generate/void/get but no
  "update method+reference" operation, and `PaymentService` already holds only an **optional**
  `IPaymentReceiptService?`. Injecting `PaymentReceiptRepository` directly (it is a registered concrete
  repository, same as `PaymentRepository`) keeps the update inside the single transaction on the shared
  `PortalDbContext` and matches how `CreditNoteService` injects concrete repositories. The receipt
  repo's commands honour `_context.Database.CurrentTransaction`, so they enlist in the service's
  transaction automatically.
- Both new dependencies are **required** (non-nullable). `AuditLogRepository` and
  `PaymentReceiptRepository` are already in the DI container; making them required is consistent with
  `CreditNoteService`. (The existing optional `IPaymentReceiptService?` stays optional and untouched.)

### Algorithm (single transaction, ordered)

```csharp
public async Task<ServiceResult> UpdatePaymentMethodAndReferenceAsync(
    int paymentId, int newPaymentMethodTypeId, string? newReference, int businessId, string userId)
{
    try
    {
        // Normalise: blank reference → null (FR, Assumption 6)
        var reference = string.IsNullOrWhiteSpace(newReference) ? null : newReference.Trim();

        // 1. Load tenant-scoped
        var payment = await _paymentRepository.GetByIdAndBusinessIdAsync(paymentId, businessId);
        if (payment == null)
            return ServiceResult.Fail("Payment not found.");

        // 2. Refuse voided
        if (payment.IsVoided)
            return ServiceResult.Fail("A voided payment cannot be edited.");

        // 3. Validate the new method exists (active list, same source as the dropdown)
        var methodExists = await _portalDbContext.PaymentMethodTypes
            .AnyAsync(pmt => pmt.Id == newPaymentMethodTypeId);
        if (!methodExists)
            return ServiceResult.Fail("The selected payment method is not valid.");

        // 4. VAT-period lock: ALLOW (see decision below). No guard.

        // capture old values for audit BEFORE mutating
        var oldMethodId = payment.PaymentMethodTypeId;
        var oldReference = payment.Reference;

        // short-circuit no-op (optional): if nothing changed, return Ok without writing.
        if (oldMethodId == newPaymentMethodTypeId && oldReference == reference)
            return ServiceResult.Ok();

        using var transaction = await _portalDbContext.Database.BeginTransactionAsync();
        try
        {
            // 5. Update the payment
            await _paymentRepository.UpdateMethodAndReferenceAsync(paymentId, newPaymentMethodTypeId, reference);

            // 6. Cascade to the non-voided receipt snapshot (if any)
            await _paymentReceiptRepository.UpdateMethodAndReferenceByPaymentIdAsync(
                paymentId, businessId, newPaymentMethodTypeId, reference);

            // 7. Append-only audit (mirrors CreditNoteService)
            await _auditLogRepository.InsertAsync(new AuditLog
            {
                BusinessId = businessId,
                UserId = userId,
                Action = "PaymentMethodUpdated",
                TableName = "Payment",
                RecordId = paymentId.ToString(),
                OldValues = $"{{\"PaymentMethodTypeId\":{oldMethodId},\"Reference\":{JsonString(oldReference)}}}",
                NewValues = $"{{\"PaymentMethodTypeId\":{newPaymentMethodTypeId},\"Reference\":{JsonString(reference)}}}",
                Timestamp = DateTime.UtcNow
            });

            await transaction.CommitAsync();
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
    catch (Exception ex)
    {
        throw;
    }
}
```

`JsonString(s)` is a tiny local helper producing `"null"` for null or a JSON-escaped quoted string
otherwise (escaping `"` and `\`), so the audit `OldValues`/`NewValues` are valid JSON and match the
small-JSON style already used by `StatementService` audit rows. Both `catch` blocks use
`catch (Exception ex)` (house rule, `coding_golden_rules`).

**Transaction scope.** All writes (payment update, receipt cascade, audit) run on the same
`PortalDbContext` inside one `BeginTransactionAsync`, matching `CreditNoteService.ApplyCreditNoteAsync`.
NFR-2 (atomicity) and AC-13 (all-or-nothing) are satisfied because the three repo calls honour
`CurrentTransaction` and the `catch` rolls back. No `FinancialStatusEngine` recalculation is triggered —
method/reference changes cannot affect any balance, so recalc would be pointless work (and is correctly
absent).

### VAT-period lock decision (FR-8 step 4, Assumption 5 — LOCKED)

**Decision: ALLOW the edit regardless of the payment's VAT-period submission state. No VAT guard is
added.**

Reasoning, grounded in the codebase: the only VAT-submission lock precedent is `CreditNoteService`,
which blocks **creating** a credit note into a submitted period and **voiding** a non-Draft credit note
in a submitted period. Both of those mutate a period's **financial totals** (they change VAT output /
the amount that was filed). A payment's method and reference are **not** part of any VAT computation —
VAT period assignment is driven by `Payment.PaymentDateUtc` (explicitly locked, unchanged here) and the
amounts, none of which this feature can alter. There is **no** existing payment mutation that blocks on
submitted periods to follow as precedent (`VoidPayment`/`VoidPaymentSmart` do not consult
`VatSubmissions` at all). Blocking a method/reference correction in a filed period would therefore be a
new, stricter rule with no financial justification, and would defeat the feature's whole purpose (a
customer's "I paid cash → actually bank transfer" correction commonly surfaces after the period is
filed). Since the edit is VAT-neutral and audit-logged, allowing it is both safe and consistent. The
`VatSubmission.IsSubmitted` flag is left entirely out of this path.

## Repository additions

### `PaymentRepository.UpdateMethodAndReferenceAsync`

Modelled exactly on `UpdateCreditAmountAsync` (raw `ExecuteSqlRawAsync`, full table names, null-safe
parameter):

```csharp
public virtual async Task UpdateMethodAndReferenceAsync(int paymentId, int paymentMethodTypeId, string? reference)
{
    try
    {
        const string query = @"
            UPDATE [revenue].[Payment]
            SET [PaymentMethodTypeId] = @PaymentMethodTypeId,
                [Reference] = @Reference
            WHERE [revenue].[Payment].[Id] = @PaymentId";

        await _context.Database.ExecuteSqlRawAsync(query,
            new SqlParameter("@PaymentId", paymentId),
            new SqlParameter("@PaymentMethodTypeId", paymentMethodTypeId),
            new SqlParameter("@Reference", reference ?? (object)DBNull.Value));
    }
    catch (Exception ex)
    {
        throw;
    }
}
```

Tenant scoping is already guaranteed because the service loaded the payment via
`GetByIdAndBusinessIdAsync` before calling this; the update keys on the primary key `Id` (same as
`UpdateCreditAmountAsync`, which also keys on `Id` only). `ExecuteSqlRawAsync` enlists in the ambient
`CurrentTransaction` automatically.

### `PaymentReceiptRepository.UpdateMethodAndReferenceByPaymentIdAsync`

The receipt snapshot stores `PaymentMethodTypeId` and `PaymentReference` (see `PaymentReceipt` insert).
`PaymentReceiptService.GetReceiptAsync` reads the method **name** from `receipt.PaymentMethodTypeId`
(not live from the payment), so the cascade is what makes a re-viewed/re-downloaded receipt reflect the
change (FR-8 step 6, AC-7). New method, scoped by `BusinessId` and restricted to the **non-voided**
receipt:

```csharp
public virtual async Task UpdateMethodAndReferenceByPaymentIdAsync(
    int paymentId, int businessId, int paymentMethodTypeId, string? reference)
{
    try
    {
        const string query = @"
            UPDATE [revenue].[PaymentReceipt]
            SET [PaymentMethodTypeId] = @PaymentMethodTypeId,
                [PaymentReference] = @PaymentReference
            WHERE [revenue].[PaymentReceipt].[PaymentId] = @PaymentId
              AND [revenue].[PaymentReceipt].[BusinessId] = @BusinessId
              AND [revenue].[PaymentReceipt].[IsVoided] = 0";

        await _context.Database.ExecuteSqlRawAsync(query,
            new SqlParameter("@PaymentId", paymentId),
            new SqlParameter("@BusinessId", businessId),
            new SqlParameter("@PaymentMethodTypeId", paymentMethodTypeId),
            new SqlParameter("@PaymentReference", reference ?? (object)DBNull.Value));
    }
    catch (Exception ex)
    {
        throw;
    }
}
```

Updating zero rows (no receipt, or only a voided receipt) is a valid no-op — `ExecuteSqlRawAsync`
returns 0 and the service does not depend on the count. Voided receipts are deliberately left untouched
(a voided receipt is cancelled data; editing it would contradict the same principle that bars editing
voided payments).

### `GetRecentPaymentsPagedAsync` and `GetPaymentsInPeriodAsync` SELECT extensions

Add `[revenue].[Payment].[PaymentMethodTypeId]` to each SELECT and read it into the DTO. These are the
only two reader-column additions; the data-reader index for existing columns must be kept in sync
(append the new column at the end of the SELECT list and read it at the new trailing ordinal for
`GetRecentPaymentsPagedAsync`, which uses positional `reader.GetInt32(n)`; `GetPaymentsInPeriodAsync`
uses `reader.GetOrdinal("PaymentMethodTypeId")` by name, so it is index-agnostic).

## Error handling (concrete, per failure point)

| Operation | Failure condition | Recoverable? | Caller receives | Logged? |
|-----------|-------------------|--------------|-----------------|---------|
| Load payment | not found / other business | recoverable (user error) | `success:false, "Payment not found."` | no (expected) |
| Voided guard | `payment.IsVoided` | recoverable | `success:false, "A voided payment cannot be edited."` | no |
| Method validation | id absent from `PaymentMethodTypes` | recoverable | `success:false, "The selected payment method is not valid."` | no |
| Payment UPDATE | SQL/connection error | fatal to this request | rolled back; `throw` → controller catch → `success:false, "An unexpected error occurred."` | yes (controller) |
| Receipt cascade | SQL error | fatal | rolled back with payment update | yes (controller) |
| Audit insert | SQL error | fatal | rolled back (payment + receipt reverted) | yes (controller) |

The service returns `ServiceResult.Fail(...)` for the three validation cases (no exception) and `throw`s
on infrastructure failures (repositories rethrow with `throw;` per repository standard). The controller
wraps the call in `try/catch (Exception ex)` and converts any thrown exception into a generic
`success:false` JSON — matching every other `AxPost…` action in `RevenueController`, which log via
`Serilog` where they already do (this action will `Log.Error(ex, …)` like `AxPostApplyCredit`).

## Controller — `RevenueController.AxPostUpdatePaymentMethod`

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> AxPostUpdatePaymentMethod(int paymentId, int paymentMethodTypeId, string? reference)
{
    try
    {
        var businessId = _tenantService.CurrentBusinessId;
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? string.Empty;

        var result = await _paymentService.UpdatePaymentMethodAndReferenceAsync(
            paymentId, paymentMethodTypeId, reference, businessId, userId);

        return Json(new { success = result.Success, message = result.Success ? "Payment updated successfully." : result.Message });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "AxPostUpdatePaymentMethod failed for paymentId={PaymentId}", paymentId);
        return Json(new { success = false, message = "An unexpected error occurred." });
    }
}
```

Lives in `RevenueController` (Assumption 2 — confirmed): it already owns `RecordPayment`,
`VoidPayment`, `AxPostVoidPaymentSmart`, and both target pages already POST payment actions to
`/Revenue/…`. `[ModuleAccess(PortalModules.Revenue)]` on the controller already gates access. Name uses
the `AxPost{Action}` convention (`coding_golden_rules`). `businessId`/`userId` resolution matches
`RecordPayment`. Both pages call this one endpoint (FR-11/FR-12/AC-14).

## Input validation (each external input)

| Input | Required | Type | Limits | On failure |
|-------|----------|------|--------|-----------|
| `paymentId` | yes | int (model-bound) | must resolve to a tenant-owned payment | "Payment not found." |
| `paymentMethodTypeId` | yes | int | must exist in `PaymentMethodTypes` | "The selected payment method is not valid." |
| `reference` | no | string | ≤ 200 chars (DB/UI cap; input `maxlength=200`) | blank → stored `NULL`; over-length is truncated client-side by `maxlength`, and the column enforces the ceiling server-side |

The reference length matches the existing payment-reference inputs (`_PaymentModal` uses no explicit cap
but the statement global modal uses `maxlength="200"`; the Edit modal uses `maxlength="200"` for
consistency). Client-side the Save button blocks an empty method selection; the server re-validates
(never trusts the client). No numeric/format validation is needed beyond existence.

## Invariant ownership

- **Tenant isolation (NFR-1/AC-11):** owned by the **service**, via `GetByIdAndBusinessIdAsync` on load
  and `BusinessId` in the receipt-cascade WHERE. The controller supplies `businessId` from
  `ICurrentTenantService`; repositories never widen scope.
- **"Voided payments are immutable" (AC-9):** owned authoritatively by the **service** (step 2); the UI
  guards are advisory.
- **Atomicity (NFR-2/AC-13):** owned by the **service** transaction.
- **Audit completeness (NFR-3/AC-8):** owned by the **service** (exactly one insert per successful,
  non-no-op edit). Note the no-op short-circuit means an edit that changes nothing writes no audit row;
  this is intentional and does not violate AC-8 (which concerns a *change* being saved). If the team
  prefers an audit row even for no-ops, drop the short-circuit — flagged as a minor choice, defaulting
  to "no row for no-op".
- **DB-driven method list (AC-4):** owned by the **view** (`ViewBag.PaymentMethods` loop); no hardcoding.

## Edge cases

- **No receipt / only a voided receipt:** cascade updates 0 rows — correct no-op; voided receipts are
  intentionally not updated.
- **Reference cleared:** empty/whitespace → `null` in both `Payment.Reference` and
  `PaymentReceipt.PaymentReference` (both nullable). AC covers blank display.
- **Customer name with an apostrophe** (dashboard server-rendered `onclick` string arg): a raw `'`
  would break the single-quoted JS string literal. **Mitigation:** the implementer should prefer wiring
  the Edit button via `data-*` attributes + a delegated `addEventListener` (reading
  `dataset.paymentMethodTypeId`, `dataset.reference`, etc.) rather than inlining values into the
  `onclick`. This is the robust pattern and avoids HTML/JS-escaping pitfalls entirely. The JS-rendered
  rows (dashboard JS + statement) already pass JS values as function arguments, so they are unaffected.
- **Scheduled/auto-allocated/child payments:** method+reference are plain columns on every payment row;
  editing them has no schedule or allocation side-effect (no recalc needed), so child/scheduled payments
  are editable with no special handling. (The feature targets the dashboard and statement rows the
  requirements name; child-allocation rows are not separately surfaced for edit.)
- **Concurrent void then edit:** if the payment is voided between row render and save, the service's
  step-2 guard rejects the stale edit — fail-closed.
- **Statement line context invoice:** passed empty (the clean invoice number is not separately carried);
  the context line shows `—`. Acceptable; customer/amount/date are authoritative.

## Testability

- **Unit-testable (service):** `UpdatePaymentMethodAndReferenceAsync` logic with mocked
  `PaymentRepository`/`PaymentReceiptRepository`/`AuditLogRepository` and an in-memory/mocked
  `PortalDbContext` for the `PaymentMethodTypes.AnyAsync` check:
  - not found → Fail; voided → Fail (no writes); invalid method → Fail (no writes);
  - happy path → payment update + receipt cascade + exactly one audit insert, in a committed txn;
  - no-op short-circuit → no writes.
  The repositories are `virtual` on a `GenericStoredProcedureRepository<T>` base, so they are mockable
  (the existing test approach for these repos).
- **Integration-testable:** against a test SQL DB — verify `[revenue].[Payment]` columns change, the
  non-voided `[revenue].[PaymentReceipt]` snapshot changes, a voided receipt does **not**, exactly one
  `[audit].[AuditLog]` row appears with the expected `Action`/`RecordId`/JSON, and a failure injected at
  the audit step rolls back the payment+receipt (AC-13).
- **Manual/browser (advisable, not automated here):** both pages show Edit only on non-voided rows; the
  dropdown pre-selects the true current method (the bug-class guard); save refreshes the table.
- **Hard to test without a browser:** the Razor `onclick`-string escaping edge (apostrophe) — another
  reason the design recommends the `data-*` + `addEventListener` wiring, which is also easier to assert
  in a DOM test.

## Verification gate

Build must report `0 Error(s)`:
`dotnet build Portal.Web/Portal.Web.csproj -c Debug`, capturing output to a file and reading it back
(the terminal wrapper can swallow stdout). If the `Portal.Infrastructure.dll` / `sourcelink.json`
"being used by another process" lock appears (a running app transiently locks it), retry the build once
— it is an environment lock, not a code error. Pre-existing CS0168/CS8602/CS8629 warnings in unrelated
files are acceptable. No runtime correctness is claimed beyond compilation; browser testing of both
surfaces remains advisable. Clean up any temp build-output file created.

## Out of scope (restated)

No editing of amount/date/allocation/customer-invoice links; no editing of voided payments; no new
method display-label scheme (`@method.Name` as elsewhere); no historical data migration; no change to
whether voided payments appear on the statement; no credit-note/invoice-total changes; no receipt
regeneration or re-numbering (only the existing non-voided receipt's method/reference snapshot is
updated in place).
