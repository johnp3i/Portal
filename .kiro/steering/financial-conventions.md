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
