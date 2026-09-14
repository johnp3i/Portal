# Tech Debt & Correctness Backlog

Running list of known functional/correctness issues and consistency debt to address, in
priority order. Items are added as they're discovered (often incidentally during feature work)
and moved to **Resolved** once fixed and verified. Security-specific items live separately in
`security-audit/security-backlog.md`.

Severity: **High** (wrong numbers shown to users / data integrity) > **Medium** (inconsistency,
edge cases) > **Low** (cosmetic, minor).

---

## Open

_(none currently)_

---

## Resolved

### TD-3 — Upcoming supplier payments counted purchases that were already paid

- **Severity:** High (wrong "money going out" figure + nagging reminders on settled purchases).
- **Discovered / Resolved:** 2026-08-24, from a user scenario: a purchase with a targeted due
  date of 7/9 and a supplier due date of 11/9 was paid, yet the Daily Brief kept reporting it as a
  supplier payment coming due ("1 supplier payment(s) coming due this week, totalling €264.16 (1
  already overdue)"). The purchase had no notion of being paid, so nothing dropped it off.
- **Files:** `Portal.Database/Migrations/208_AddPaidStateToPurchase.sql`,
  `Portal.Infrastructure/Entities/Purchase.cs`, `Portal.Infrastructure/Data/PortalDbContext.cs`
  (`ConfigurePurchase`), `Portal.Infrastructure/Services/DashboardService.cs`
  (`GetUpcomingSupplierPaymentsAsync`), `Portal.Infrastructure/Services/Notifications/DigestEmailBuilder.cs`,
  `Portal.Infrastructure/Repositories/PurchaseRepository.cs`,
  `Portal.Infrastructure/Services/PurchaseService.cs` + `IPurchaseService.cs`,
  `Portal.Web/Controllers/PurchaseController.cs`, `Portal.Web/Views/Purchase/Index.cshtml`.
- **Issue:** the same shape as TD-0/TD-1 on the payables side — a "due/outstanding" figure derived
  without subtracting what settles it. `GetUpcomingSupplierPaymentsAsync` (shared by the Daily Brief
  attention line, the Weekly Outstanding Balance digest, and the dashboard upcoming-payments widget)
  surfaced every non-cancelled purchase with a due date in range, with no concept of "paid".
- **Fix applied (Option A — `IsPaid` flag, chosen as "v1 but solid"):** added
  `IsPaid BIT NOT NULL DEFAULT 0` + `PaidAtUtc DATETIME NULL` to `[purchase].[Purchase]`; entity +
  EF config; `GetUpcomingSupplierPaymentsAsync` now filters `AND [purchase].[Purchase].[IsPaid] = 0`
  (one query fix covers all three surfaces); a business-scoped mark-paid / mark-unpaid path
  (`PurchaseService.SetPurchasePaidStateAsync` → `PurchaseRepository.SetPaidStateAsync` setting
  `PaidAtUtc = CASE WHEN @IsPaid=1 THEN GETUTCDATE() ELSE NULL END`, audit-logged) with an
  `AxPostSetPaidState` endpoint and a Paid pill + Mark paid/unpaid action on the purchases list.
  Note: `PaidAtUtc` was added alongside the flag (not a bare toggle) to avoid blind reporting later.
- **Not done (intentional / future Option B):** no `PurchasePayment` table, no partial supplier
  payments, no payment history. Option A is designed to extend to B later if per-payment tracking
  is needed. Paid purchases are excluded from the query, not cancelled/soft-deleted — cancelling a
  paid purchase would distort expense reporting.
- **Recurring pattern (3rd occurrence):** "a due/outstanding total computed without subtracting
  what settles it" has now bitten three surfaces — overdue invoices vs credit notes (TD-0),
  outstanding/partially-paid KPIs vs credit notes (TD-1), and upcoming supplier payments vs
  payment (TD-3). Consider a steering note generalising the invariant beyond receivables: *any
  "still owed / still due" figure must subtract every settlement mechanism that exists for that
  entity (payments, credit notes, paid-state), not just the most obvious one.*

### TD-1 — Outstanding & Partially-Paid KPI amounts omit applied credit notes

- **Severity:** Medium (amount over-statement on user-facing surfaces).
- **Discovered:** 2026-08-24, while fixing the Daily Brief phantom-overdue bug (the overdue path
  had the same defect and was fixed then; the sibling KPI queries were left in scope-tight).
- **Resolved:** 2026-08-24.
- **Files:** `Portal.Infrastructure/Services/DashboardService.cs` (`GetKpiDataAsync` —
  `outstandingQuery`, `partiallyPaidQuery`), `Portal.Infrastructure/Services/ReceivablesQueryService.cs`
  (`GetReceivablesAsync`), `Portal.Infrastructure/Models/ReceivableDto.cs`.
- **Issue:** These queries computed outstanding as `TotalAmount − SUM(valid Payments)` and did
  **not** subtract applied credit notes. The authoritative formula (used by
  `FinancialStatusEngine.ComputeOutstandingBalance` and `PaymentService.RecordPaymentAsync`) is
  `TotalAmount − Payments − AppliedCreditNotes`. So **Outstanding Receivables**, **Partially Paid**
  (dashboard KPIs) and every per-invoice `OutstandingBalance` on the receivables list were
  over-stated for any invoice reduced (fully or partly) by a credit note — and the receivables
  list could show a positive balance / a "Pay" button on an invoice actually settled by credit.
- **Fix applied:** added the same non-voided `[credit].[CreditNoteApplication]` → `[credit].[CreditNote]`
  (scoped by `BusinessId`) subtraction the overdue query already uses. In `DashboardService` both
  KPI queries now subtract `AppliedCredit.TotalCredited` (via `LEFT JOIN`); in `ReceivablesQueryService`
  the `OutstandingBalance` correlated subquery now subtracts applied credit and a `TotalCredited`
  column was surfaced on `ReceivableDto`. `DashboardKpiDataAccuracyTests` mirror helpers were made
  credit-note-aware and a dedicated `GetKpiData_AppliedCreditNotes_ReduceOutstandingOverdueAndPartiallyPaid`
  test added (voided credit ignored, credit-only zeroed invoices excluded from overdue). All 8 tests
  in the class pass.
- **Convention captured:** `.kiro/steering/financial-conventions.md` (credit-note-aware balance).
- **Reference:** `FinancialStatusEngine.ComputeOutstandingBalance`;
  `CreditNoteRepository.GetTotalAppliedCreditAsync`.

### TD-2 — Persisted `InvoiceFinancialStatusTypeId` can be stale (Overdue not set proactively)

- **Severity:** Medium.
- **Discovered:** 2026-08-24, during the Daily Brief overdue investigation.
- **Resolved:** 2026-08-24 (via option **b** — derive-always convention).
- **Files:** `Portal.Infrastructure/Services/ReceivablesQueryService.cs`,
  `Portal.Infrastructure/Models/ReceivableDto.cs`, `Portal.Web/Views/Revenue/Receivables.cshtml`,
  `.kiro/steering/financial-conventions.md`.
- **Issue:** `FinancialStatusEngine.RecalculateStatusAsync` only runs on payment/credit-note
  events, not when an invoice's `DueDate` simply passes. So an unpaid invoice that becomes overdue
  purely by the passage of time can stay marked `Unpaid (1)` instead of `Overdue (4)`. Consumers
  that trust the persisted status under-report overdue (and, after a credit note zeroes a balance,
  can over-report it). The receivables list was one such consumer — it displayed the persisted
  `FinancialStatusName` directly.
- **Decision:** chose option (b) — **overdue is always derived** (`OutstandingBalance > 0 && DueDate <
  today`), never read from the persisted status — over option (a) a scheduled sweep. Rationale:
  no background job / no staleness window, and the overdue KPI + Daily Brief already work this way.
- **Fix applied:** `ReceivablesQueryService` now exposes a derived `ReceivableDto.IsOverdue`; the
  receivables list renders the "Overdue" pill from that flag rather than the persisted status name.
  The persisted `InvoiceFinancialStatusTypeId` remains advisory (for Unpaid/PartiallyPaid/Paid/
  WrittenOff distinctions) but is no longer authoritative for the overdue question. Convention
  documented in `.kiro/steering/financial-conventions.md` for all future consumers.
- **Not done (intentional):** no scheduled status-flip job and no bulk backfill of stale rows —
  the derive-always convention makes them unnecessary. If a future consumer genuinely cannot
  recompute (e.g. raw-row exports), revisit option (a) then.

### TD-0.1 — Portal.Tests assembly failed to compile (Moq expression trees, incidental)

- **Severity:** Medium (blocked the entire test suite from running).
- **Discovered / Resolved:** 2026-08-24, while trying to run the TD-1 accuracy tests.
- **Files:** `Portal.Tests/Unit/Services/InvoiceEmailServiceTests.cs`,
  `Portal.Tests/PropertyBased/Billing/InvoiceEmailIdempotencyPropertyTests.cs`,
  `Portal.Tests/PropertyBased/DemoEmailContentCompletenessPropertyTests.cs`.
- **Issue:** `IEmailSender.SendEmailAsync` gained an optional `string? replyTo = null` parameter,
  but 10 Moq `Setup`/`Verify`/`Callback` expression trees still called it with only 4 arguments.
  C# forbids omitting optional arguments inside expression trees (`CS0854`), so the whole test
  assembly failed to compile — unrelated to TD-1/TD-2 but blocking their verification.
- **Fix applied:** supplied the 5th argument explicitly (`It.IsAny<string?>()`) at each site, and
  extended the one typed `.Callback<...>` with the matching `string?` type + lambda parameter.

---

## Resolved (earlier)

### TD-0 — Daily Brief / overdue KPI counted credit-noted invoices as overdue

- **Severity:** High
- **Discovered / Resolved:** 2026-08-24.
- **File:** `Portal.Infrastructure/Services/DashboardService.cs` — `GetKpiDataAsync`
  (`overdueQuery`) and `GetOldestOverdueInvoiceAsync`.
- **Issue:** The overdue outstanding formula was `TotalAmount − Payments` only, omitting applied
  credit notes. An invoice fully settled by a credit note (balance 0) still showed a positive
  balance and was counted as overdue — the Daily Brief reported "2 invoices overdue totalling
  €129.55" / "Oldest unpaid INV-1-00085 … 174 days overdue" when nothing was actually overdue.
- **Fix applied:** both queries now `LEFT JOIN [credit].[CreditNoteApplication]` (non-voided,
  scoped to the business) and subtract `AppliedCredit.TotalCredited`, matching
  `FinancialStatusEngine.ComputeOutstandingBalance`. Deliberately did NOT add an
  `InvoiceFinancialStatusTypeId = 4` filter, to preserve the balance-driven overdue semantics
  the existing tests enshrine and avoid false negatives from stale status (see TD-2). Fixes the
  overdue numbers everywhere they surface: Daily Brief, Weekly Financial Snapshot attention
  section, and the dashboard overdue KPI (shared queries).
