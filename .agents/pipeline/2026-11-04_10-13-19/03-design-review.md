# Design Review — Edit a Payment's Method and Reference

Reviewed: `02-design.md` against `01-requirements.md`, with every claim about existing
code checked against the actual source in `c:\Users\user\Documents\GitHub\Portal`.

This is a first-pass (fresh) review — no prior review to reconcile.

Overall the design is unusually well-grounded: the vast majority of its claims about
existing code are accurate (constructor shapes, DI auto-wiring, the receipt-snapshot read
path, the composite statement reference, the controller tenant/user resolution pattern,
the camelCase JSON serialization, the positional vs. ordinal reader behaviour). The
findings below are the places where the design is ambiguous, internally inconsistent, or
rests on a claim the source does not fully support.

Verdict: **CHANGES_REQUESTED** (2 HIGH, 3 MEDIUM, 4 NIT).

---

## Findings

### 1. [MEDIUM] Method validation diverges from the dropdown's active-only source
The service algorithm validates the new method with
`_portalDbContext.PaymentMethodTypes.AnyAsync(pmt => pmt.Id == newPaymentMethodTypeId)`
(no `IsActive` filter), and the design's own inline comment on that line reads
`// Validate the new method exists (active list, same source as the dropdown)`. That
comment is wrong about its own code: the dropdown is populated by
`GetPaymentMethodTypesAsync()`, which **does** filter `.Where(pmt => pmt.IsActive)` (verified
in `PaymentService.GetPaymentMethodTypesAsync`). So an **inactive** method id would pass
server validation even though it can never be chosen from the dropdown, and the modal's
pre-select (`select.value = methodId`) would silently fail for a payment whose current
method was since deactivated.

Decide and lock one rule. The behaviour must be internally consistent between the
dropdown, the pre-select, and the validation.

Concrete fix — match the dropdown (recommended), and keep the current method selectable
even if since deactivated:
```csharp
// valid = currently-active method OR the payment's own existing method (so a legacy
// payment on a since-deactivated method can still be re-saved without forcing a change)
var methodExists = await _portalDbContext.PaymentMethodTypes
    .AnyAsync(pmt => pmt.Id == newPaymentMethodTypeId && pmt.IsActive);
if (!methodExists && newPaymentMethodTypeId != payment.PaymentMethodTypeId)
    return ServiceResult.Fail("The selected payment method is not valid.");
```
If instead the intent is "any row that exists is valid," remove the `active list` wording
from the comment so the design doesn't claim a filter it doesn't apply. Either way, fix the
mismatch.

### 2. [MEDIUM] `reloadAfterPaymentEdit` indirection is self-referential and load-order fragile
The design defines, in `revenue-payment.js`, a top-level
`function reloadAfterPaymentEdit() { ... }` whose body is:
```js
if (typeof window.reloadAfterPaymentEdit === 'function' && window.reloadAfterPaymentEdit !== reloadAfterPaymentEdit) {
    return window.reloadAfterPaymentEdit();
}
location.reload();
```
A top-level function declaration in a classic `<script>` **is** `window.reloadAfterPaymentEdit`.
So the guard is comparing `window.reloadAfterPaymentEdit` against the very same function
object — it only works if the statement page **reassigns** `window.reloadAfterPaymentEdit`
to a *different* function (`function(){ generateStatement(); }`) and does so **after**
`revenue-payment.js` has loaded. That ordering is implicit and unstated. If the statement's
inline override runs before the file (or the file re-declares after it), the override is
clobbered and the statement silently falls back to `location.reload()` — a full page reload
on a page whose entire content is client-rendered from a filter form, which wipes the
generated statement. Also, `submitEditPayment` calls a bare `reloadAfterPaymentEdit()`,
which resolves to the file's function, not `window.`'s — making the two-name scheme
confusing.

Concrete fix — use two distinctly-named symbols so there is no self-comparison and no
load-order dependency:
```js
// in revenue-payment.js
function defaultReloadAfterPaymentEdit() { location.reload(); }
function runReloadAfterPaymentEdit() {
    if (typeof window.onPaymentEdited === 'function') return window.onPaymentEdited();
    defaultReloadAfterPaymentEdit();
}
// submitEditPayment calls runReloadAfterPaymentEdit()
```
```js
// statement Index.cshtml inline script (any order relative to the file is fine)
window.onPaymentEdited = function () { generateStatement(); };
```

### 3. [MEDIUM] Dashboard server-row button wiring is left as an unresolved either/or
For the dashboard **server-rendered** rows the design shows an `onclick` that inlines
HTML-encoded strings into a single-quoted JS literal, then says the implementer "should
prefer" `data-*` + `addEventListener` "if any customer name could contain an apostrophe."
This is exactly the "use X or Y" ambiguity a design is supposed to resolve. Customer names
*can* contain apostrophes (O'Brien), and `Html.Encode` produces `&#39;` which does **not**
neutralise a `'` inside a JS string literal — it renders as `&#39;` text, breaking the
display at best and the literal at worst. Leaving this as a preference pushes a real
correctness decision onto the coder.

Concrete fix — lock the `data-*` + delegated-listener approach for the dashboard server
rows and specify it:
```cshtml
<button type="button" class="tbl-action tbl-action--primary js-edit-payment"
    data-payment-id="@item.PaymentId"
    data-method-id="@item.PaymentMethodTypeId"
    data-reference="@item.Reference"
    data-customer="@item.CustomerName"
    data-invoice="@item.InvoiceNumber"
    data-amount="@currency@item.Amount.ToString("N2")"
    data-date="@item.PaymentDateUtc.ToString("yyyy-MM-dd")">Edit</button>
```
```js
document.addEventListener('click', function (e) {
    var btn = e.target.closest('.js-edit-payment');
    if (!btn) return;
    openEditPaymentModal(btn.dataset.paymentId, btn.dataset.methodId, btn.dataset.reference,
        btn.dataset.customer, btn.dataset.invoice, btn.dataset.amount, btn.dataset.date);
});
```
Razor attribute-encodes `data-*` values correctly, so this removes the escaping hazard
entirely. (The JS-built rows on both pages already pass values as function args and are
unaffected.)

### 4. [HIGH] No-op short-circuit contradicts AC-8 / NFR-3 when a receipt exists
The algorithm adds an optional no-op short-circuit: if `oldMethodId == new && oldReference
== reference`, return `Ok()` **before** the transaction, writing no audit row. The design
argues this "does not violate AC-8 (which concerns a *change* being saved)." That reasoning
is sound for the payment row itself, but it has a concrete failure mode the design doesn't
address: a payment and its **receipt snapshot can legitimately be out of sync** before this
feature ever runs — e.g. the receipt was generated, then (in some other path or a prior
partial edit) the payment changed. With the short-circuit, a user who opens the modal and
saves the same values the payment already has will get a success toast, but the stale
receipt is **never** re-synced, and no audit row records the attempt. More importantly, the
short-circuit compares only the *payment's* current values, not the *receipt's*, so "nothing
changed on the payment" does not imply "nothing needs cascading."

Concrete fix — either (a) drop the short-circuit entirely (simplest; always run the
transaction, always cascade, always audit — the cost is one cheap UPDATE touching 0–2 rows),
or (b) keep it but only short-circuit when the payment values are unchanged **and** you have
confirmed the non-voided receipt (if any) already matches. Option (a) is recommended and
makes AC-8/NFR-3 unconditionally true: every successful save produces exactly one audit row.
If (a) is taken, remove the "no row for no-op" note under Invariant Ownership so the design
and the acceptance criteria agree.

### 5. [HIGH] `getAntiForgeryToken` is redeclared, and the design's justification is factually wrong
The design states the shared functions "use `getAntiForgeryToken()` which both pages
provide, so loading the file on the statement page is safe." In fact **both**
`revenue-payment.js` (`function getAntiForgeryToken()` at its top) **and** the statement
page's inline script (`function getAntiForgeryToken()`) declare a top-level function of the
same name. Loading the file on the statement page therefore creates two identical top-level
`function` declarations in the same global scope. For classic scripts this is legal (the
later declaration wins) and, because the two bodies are equivalent, harmless **today** — but
the design's stated reason is backwards (the file brings its *own* token helper; it does not
depend on the page providing one), and the redeclaration is a latent footgun: if either
definition drifts, or if the statement's inline block is ever converted to a module/strict
context, this silently breaks or throws. This matters because the whole shared-code premise
rests on "loading the file on the statement page is safe," and the given justification is
incorrect.

Concrete fix — state the real reason (the file is self-contained and ships its own
`getAntiForgeryToken`, so the statement page's identical inline copy is redundant, not a
dependency), and remove the statement page's now-duplicate inline `getAntiForgeryToken`
declaration when wiring in `revenue-payment.js` so there is exactly one definition. Both
pages have the `__RequestVerificationToken` input, so the single shared helper works on both.

### 6. [NIT] Statement Edit button passes a mislabeled amount and an empty invoice
The statement JS Edit wiring passes `line.credit` as the amount and `''` as the invoice,
with the comment that context is "advisory." `line.credit` is the statement line's credit
column, which for a payment line equals the payment amount — correct, but the name reads as
if a credit note is being passed. The empty invoice is acceptable per the design's own edge
case. No behavioural bug; worth a one-line clarification that `line.credit` is the payment
amount on a Payment line, to spare the coder a double-take.

### 7. [NIT] Reference length cap (200) is asserted but not verified against the column
The input-validation table states `reference ≤ 200 chars` with "the column enforces the
ceiling server-side." The design did not cite the actual `[revenue].[Payment].[Reference]`
or `[revenue].[PaymentReceipt].[PaymentReference]` column length, and I did not find a
schema definition confirming 200. `maxlength="200"` on the input is a client-only hint; if
the column is e.g. `NVARCHAR(100)`, an over-length value pasted past the maxlength (or sent
directly to the endpoint) would throw a truncation error surfaced as a generic 500. Confirm
the column length and either match the `maxlength`/server guard to it or add an explicit
length check in the service. Low severity because the existing record-payment inputs already
write this column, but the "column enforces the ceiling" claim is currently unverified.

### 8. [NIT] Audit `NewValues`/`OldValues` JSON is hand-built via a `JsonString` helper
The design hand-rolls JSON with a `JsonString(s)` helper (escaping `"` and `\`). This matches
`StatementService`'s existing hand-built JSON style, so it is consistent with the codebase —
but note that `CreditNoteService` (the design's cited precedent for this audit call) uses a
non-JSON `key=value` format, not JSON. The design is fine to prefer the JSON style; just
don't describe it as "mirroring the `CreditNoteService` pattern" for the value format, since
that precedent is `key=value`. Reference `StatementService` instead for the JSON shape.

### 9. [NIT] `_PaymentModal` vs `_GlobalPaymentModal` reference-length precedent is stated loosely
The design says `_PaymentModal` "uses no explicit cap but the statement global modal uses
`maxlength="200"`." This is an incidental styling justification and not load-bearing, but it
is an unverified assertion in the doc. Not worth blocking; fold into finding 7 when the
column length is confirmed.

---

## Verified Assumptions (checked against source)

- **`PaymentService` constructor shape** — matches exactly: injects `PaymentRepository`,
  `InvoiceRepository`, `CreditNoteRepository`, `IFinancialStatusEngine`,
  `IPaymentScheduleService`, `IPaymentAllocationEngine`, `PortalDbContext`, optional
  `IPaymentReceiptService?`, optional `INotificationProducer?`. Adding required
  `AuditLogRepository` + `PaymentReceiptRepository` is consistent with `CreditNoteService`.
- **DI needs no registration change beyond the constructor** — confirmed. `IPaymentService`
  is registered as `AddScoped<IPaymentService, PaymentService>()` (auto constructor
  injection), and `AuditLogRepository` + `PaymentReceiptRepository` are both already
  registered over `PortalDbContext`. The ambient-transaction enlistment claim holds because
  all three repos are constructed with the same scoped `PortalDbContext`.
- **Receipt snapshot read path** — confirmed: `PaymentReceiptService.GetReceiptAsync` reads
  the method **name** by looking up `receipt.PaymentMethodTypeId` and returns
  `receipt.PaymentReference`, so a receipt cascade is genuinely required for a re-viewed
  receipt to reflect the change (FR-8 step 6 / AC-7).
- **No receipt-update method exists** — confirmed: `PaymentReceiptRepository` has Insert,
  GetById, GetByPaymentId, GetPaged, Void, GenerateReceiptNumber — no method+reference update.
- **`UpdateCreditAmountAsync` style** — confirmed as a valid template (raw `ExecuteSqlRawAsync`,
  full table names, keyed on `[Id]` only).
- **Composite statement reference** — confirmed: `StatementService` builds
  `Reference = "{Method} · Ref: {ref} · {InvoiceNumber}[· from €X payment]"`, unusable for
  pre-fill; raw reference and method id must be carried separately. `StatementLineDto` and
  `StatementPaymentDto` currently lack `PaymentMethodTypeId` / raw-reference fields — the
  design's additions are real and necessary.
- **`StatementController.Generate` projection** — confirmed it is an anonymous object that
  does NOT currently emit `paymentMethodTypeId`/`rawReference`; the design's planned additions
  are needed and correctly placed.
- **Statement sets `IsVoided = false` on every payment line** — confirmed; client guard is
  defence-in-depth, server guard authoritative (matches Assumption 4).
- **`renderTransactionTable` guard** — confirmed: `if (line.type === 'Payment' &&
  line.paymentId && !line.isVoided)` is where Receipt/Void render; Edit slots in correctly.
- **Dashboard rows** — confirmed: server rows render Receipt/Void inside `@if (!item.IsVoided)`;
  JS rows (`renderPaymentsTable`) inside `if (!item.isVoided)`; both match the design.
- **camelCase serialization** — confirmed: `GetRecentPayments` returns `data = result.Items`
  (a `List<RecentPaymentDto>`) serialized with default camelCasing, so a new DTO
  `PaymentMethodTypeId` auto-exposes as `item.paymentMethodTypeId` in JS.
- **Positional vs ordinal readers** — confirmed: `GetRecentPaymentsPagedAsync` uses positional
  `reader.GetXxx(n)` (10 columns, ordinals 0–9), so appending `PaymentMethodTypeId` as a new
  trailing column read at ordinal 10 is safe; `GetPaymentsInPeriodAsync` uses
  `reader.GetOrdinal(name)` and is index-agnostic. The design's guidance is correct.
- **Controller conventions** — confirmed: `RevenueController` is `[ModuleAccess(PortalModules.
  Revenue)]`, imports `Serilog` and uses `Log.Error`, resolves
  `businessId = _tenantService.CurrentBusinessId` and
  `userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? ""`
  exactly as the design's `AxPostUpdatePaymentMethod` snippet does. `ServiceResult.Ok()`
  (parameterless) and `ServiceResult.Fail(string)` exist.
- **`_GenerateReceiptModal` is a Shared partial included by both pages** — confirmed
  (`Views/Statement/Index.cshtml` and `Views/Revenue/Dashboard.cshtml` both
  `Html.PartialAsync("_GenerateReceiptModal")`), validating "Shared/" as the home for the new
  `_EditPaymentModal` and the include placement.
- **`ViewBag.PaymentMethods` on both views** — the dashboard includes `_PaymentModal` and both
  pages render DB-driven method dropdowns; the requirement to reuse `ViewBag.PaymentMethods`
  without re-fetching is consistent with how those pages already work. (I did not open both
  controller actions line-by-line to re-confirm the exact `ViewBag.PaymentMethods` assignment;
  the design claims it is "verified in source." See Unverified below.)
- **`Swal` + `BlockUI` available on both pages** — confirmed: `_Layout.cshtml` loads
  SweetAlert2 (CDN) and `/js/block-ui.js` globally, so the shared AJAX flow (BlockUI → fetch →
  BlockUI.hide → Swal) works on the statement page too. Matches the UI-feedback house rule.
- **`AuditLog` entity shape** — confirmed: `BusinessId int?`, `UserId string?`, `RecordId
  string`, nullable `OldValues`/`NewValues`, `Timestamp DateTime`. The design's field
  assignments type-check.
- **`AuditLogRepository.InsertAsync` enlists in the ambient transaction** — confirmed via
  `ExecuteSqlRawAsync` on the shared `PortalDbContext`.

## Unverified / Wrong Assumptions

- **WRONG — "both pages provide `getAntiForgeryToken`, so loading the file is safe."** The
  file ships its own `getAntiForgeryToken`; the statement page also declares one. The safety
  does not come from the page providing it — it comes from the file being self-contained, and
  it introduces a duplicate top-level declaration. See finding 5.
- **WRONG (self-contradicting comment) — method validation "active list, same source as the
  dropdown."** The query has no `IsActive` filter; the dropdown does. See finding 1.
- **UNVERIFIED — `ViewBag.PaymentMethods` is already set by both `StatementController.Index`
  and `RevenueController.Dashboard`.** The design asserts this is "verified in source" and
  FR-13 says "verify and reuse." I confirmed both pages render DB-driven method dropdowns and
  that `GetPaymentMethodTypesAsync` exists, but I did not open both controller GET actions to
  see the literal `ViewBag.PaymentMethods = await _paymentService.GetPaymentMethodTypesAsync()`
  assignment. If either page does **not** set it, `_EditPaymentModal`'s dropdown renders empty
  on that page. The coder must confirm the assignment on **both** `Index` and `Dashboard`
  before relying on it; add it where missing.
- **UNVERIFIED — `[revenue].[Payment].[Reference]` /
  `[revenue].[PaymentReceipt].[PaymentReference]` column length is ≥ 200.** The 200-char cap
  and "the column enforces the ceiling server-side" claim were not checked against a schema
  definition. See finding 7.
- **UNVERIFIED — statement inline override vs. file load order.** The `reloadAfterPaymentEdit`
  scheme's correctness depends on an unstated script-ordering assumption. See finding 2.
