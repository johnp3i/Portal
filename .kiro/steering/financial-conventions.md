# Financial Conventions

Correctness conventions for money-related data in the Portal. These are hard rules — deviating
produces wrong numbers shown to users.

## Outstanding Balance — always credit-note aware

The authoritative outstanding-balance formula is:

```
OutstandingBalance = TotalAmount − valid Payments − applied Credit Notes
```

implemented by `FinancialStatusEngine.ComputeOutstandingBalance(totalAmount, payments, appliedCreditTotal)`.

Any query or computation that derives an invoice balance **must** subtract applied credit notes,
not just payments. Omitting credit notes over-states balances for any invoice reduced by a credit
note (and can show a positive balance for an invoice fully settled by credit).

- Applied credit = `SUM([credit].[CreditNoteApplication].[AmountApplied])` where the application is
  non-voided (`IsVoided = 0`) and its parent `[credit].[CreditNote]` belongs to the business.
- Reference sum: `CreditNoteRepository.GetTotalAppliedCreditAsync`.
- Known-correct SQL join pattern lives in `DashboardService.GetKpiDataAsync` (all three KPI
  queries) and `ReceivablesQueryService.GetReceivablesAsync`.

## Overdue — always derived, never read from persisted status

**"Overdue" is a derived state, not a stored fact.** Always compute it as:

```
IsOverdue = OutstandingBalance > 0 AND DueDate < today (UTC)
```

**Do NOT** determine overdue by reading `Invoice.InvoiceFinancialStatusTypeId == 4 (Overdue)`.

Why: `InvoiceFinancialStatusTypeId` is maintained by `FinancialStatusEngine.RecalculateStatusAsync`,
which only runs on payment / credit-note events — **not** when a due date simply passes with the
passage of time. So an unpaid invoice can become overdue in reality while still persisted as
`Unpaid (1)`. Consumers that trust the persisted status will under-report overdue.

Conversely, the persisted status can also be stale in the other direction (e.g. still `Overdue (4)`
after a credit note zeroed the balance), so it must never be the source of truth for overdue.

Consumers that already follow this convention (use as reference):
- `DashboardService.GetKpiDataAsync` — overdue KPI derives from balance + `DueDate < today`, with
  no `InvoiceFinancialStatusTypeId = 4` filter.
- Daily Brief / Weekly Financial Snapshot attention items.
- `RevenueController.InvoiceDetail` — `isOverdue = DueDate < today && outstanding > 0`.
- `ReceivablesQueryService.GetReceivablesAsync` — exposes a derived `ReceivableDto.IsOverdue`; the
  receivables list renders the "Overdue" pill from that flag, not the persisted status name.

The persisted `InvoiceFinancialStatusTypeId` remains useful for non-overdue distinctions
(Unpaid vs PartiallyPaid vs Paid vs WrittenOff) and as an advisory/index. It is simply not
authoritative for the overdue question.

## "Still owed / still due" — always subtract every settlement mechanism

Generalise the credit-note rule above: **any figure that means "still owed" or "still due" must
subtract every mechanism that can settle that entity — not just the most obvious one.** A
"due/outstanding" total computed against only one settlement path over-states what is actually
owed and produces phantom reminders/alerts.

This exact bug shape has recurred three times, each time because a settlement path was omitted:

| Surface | "Due" figure | Settlement path that was missed |
|---------|--------------|---------------------------------|
| Overdue invoices (Daily Brief, overdue KPI) | invoice outstanding | applied **credit notes** |
| Outstanding & Partially-Paid KPIs, receivables list | invoice outstanding | applied **credit notes** |
| Upcoming supplier payments (Daily Brief, weekly digest, dashboard widget) | purchase due | purchase **paid-state** (`IsPaid`) |

Before shipping any "outstanding / due / owed / payable / receivable" query, enumerate **all**
the ways that entity can be settled and confirm each is subtracted or excluded:

- **Invoices** (money in): subtract valid **payments** AND applied **credit notes**
  (see the two sections above). Overdue is additionally derived, never read from persisted status.
- **Purchases** (money out): exclude those marked paid — `AND [purchase].[Purchase].[IsPaid] = 0`
  (and cancelled). A paid purchase is settled and must drop off every "upcoming supplier
  payment" surface. Reference: `DashboardService.GetUpcomingSupplierPaymentsAsync` (one query,
  shared by the Daily Brief attention line, the Weekly Outstanding Balance digest, and the
  dashboard upcoming-payments widget — fix it once, fix it everywhere).

When a new settlement mechanism is added to an entity (a new payment type, a write-off, a
paid-state, a partial-settlement table), grep for every place that computes that entity's
"due/outstanding" total and update each — these figures are frequently duplicated across a
digest, a KPI, and a widget. Prefer a single shared query/method so the invariant lives in one
place.
