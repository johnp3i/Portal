# Customer Purchases Dashboard - Testing Scenarios

## Prerequisites

1. Run migrations `233_CreateInsightSchemaAndFeatureInterest.sql` and `234_AddCustomerPurchasesIndexes.sql` against the Portal database (creates `[insight].[FeatureInterest]` and three indexes). Both scripts are idempotent.
2. Log in as a user with access to the Customer module (`PortalModules.Customer`).
3. Test data in one business, ideally:
   - A customer with 20+ invoices over the last 24 months, a mix of paid, part-paid and unpaid.
   - At least one overdue invoice (balance above zero, due date in the past) and one not yet due.
   - At least one invoice reduced by an applied credit note.
   - Invoice lines with a `ProductCode`, lines without one, one adjustment line, and some lines with and without `CostPrice`.
   - A customer with no invoices.
   - A customer billed in two currencies (for the currency switch).
   - A second business with its own customer (for tenant isolation).
4. Keep a browser dev-tools window open (Network, Application > Cookies).

---

## Scenario 1: Entry point on the Customers list

1. Open `/Customer`.
2. **Expected:** every row's Actions column shows Edit, Statement, **Purchases**, then Deactivate (active rows).
3. Filter to inactive customers.
4. **Expected:** the Purchases button is still shown on inactive rows.
5. Click **Purchases** on a customer.
6. **Expected:** same tab navigates to `/Customer/Purchases/{id}`. No confirmation dialog.
7. Press browser Back.
8. **Expected:** the Customers list keeps its search, filters and page.

---

## Scenario 2: Page header and navigation

1. On the dashboard, check the breadcrumb.
2. **Expected:** "Sales / Customers / {name} / Purchases"; "Customers" links to `/Customer`.
3. **Expected:** heading "Purchases - {name}", subtitle says quotations are not included.
4. **Expected:** buttons "Back to Customers", "Statement" (opens `/Statement?customerId={id}`), "Export Excel".
5. Open a customer with a very long name (100+ chars).
6. **Expected:** the name wraps; no horizontal page scroll.

---

## Scenario 3: KPI values

1. For a customer with known data, compare each card with the database (see queries below).
2. **Expected:**
   - Net revenue, last 12 months = sum of (invoice total minus applied credit) for the 12 calendar months ending this month; the change % compares with the 12 months before.
   - Lifetime net revenue, invoice count and first month match all counted invoices.
   - Average invoice = period net revenue / period invoice count.
   - Last purchase shows days ago and date; "buys about every N days" appears only with 3+ invoices.
   - Pending invoices = count and total outstanding (total minus valid payments minus applied credit).
   - Overdue = outstanding on invoices with balance above zero and due date before today.
3. For a customer whose previous 12 months had no invoices, **Expected:** "No prior period" instead of a percentage.

---

## Scenario 4: Collapsible KPI cards and cookie

1. Click the arrow on the "Average invoice" card.
2. **Expected:** the card collapses to its label and value; detail line hidden; `aria-expanded="false"`.
3. In dev tools, check the cookie `cp_kpi_collapsed`.
4. **Expected:** value `avginv`, path `/`, SameSite Lax, about 1 year expiry.
5. Reload the page.
6. **Expected:** the card is already collapsed on first paint (no flash).
7. Open another customer's Purchases page.
8. **Expected:** the same card is collapsed there too.
9. Click "Collapse all", then "Expand all".
10. **Expected:** all six collapse, then all expand; the cookie becomes empty after Expand all.
11. Edit the cookie by hand to `avginv,bogus,,REV12`.
12. **Expected:** only `avginv` is applied; unknown and wrongly cased entries are ignored.
13. Collapse a card, then change the Period.
14. **Expected:** values refresh but the card stays collapsed.

---

## Scenario 5: Period filter

1. Select each option: Last 12 months, This year, Last year, All time.
2. **Expected:** one request per change, BlockUI shown then hidden, sections refresh without a page reload, the URL query string updates (`?period=...`).
3. Select **Custom range**; set only one date.
4. **Expected:** no request is sent until both dates are valid.
5. Set start after end.
6. **Expected:** SweetAlert2 warning; previous results kept.
7. Set a valid range.
8. **Expected:** both dates are inclusive; results refresh.
9. Copy the URL, open it in a new tab.
10. **Expected:** same period is selected and shown.
11. Edit the URL to `?period=banana`.
12. **Expected:** falls back to Last 12 months and a warning appears.

---

## Scenario 6: Currency switch

1. Open the single-currency customer.
2. **Expected:** no currency switch is shown.
3. Open the two-currency customer.
4. **Expected:** a switch lists both codes (alphabetical); the default is the currency with the higher revenue in the period.
5. Click the other currency.
6. **Expected:** every section recomputes for that currency only; amounts show that currency; totals never mix.
7. Check the URL.
8. **Expected:** `currency=XXX` appears only after an explicit choice; reload keeps it.
9. Set the period so one currency has no invoices.
10. **Expected:** the switch stays visible; that currency shows empty states.

---

## Scenario 7: Monthly trend chart

1. **Expected:** exactly 12 bars, oldest left; "N inv" under each month label; the current month is a lighter colour with an asterisk.
2. **Expected:** the dashed same-month-last-year line is shown by default.
3. Untick "Compare with same month last year".
4. **Expected:** the line disappears at once with no network request.
5. Hover or tab to a bar.
6. **Expected:** tooltip with month, amount and invoice count.
7. Change the Period.
8. **Expected:** the chart does not change (it is always the last 12 months) and the subtitle says so.
9. Narrow the window below 768px.
10. **Expected:** the chart scrolls horizontally; bars do not overlap.

---

## Scenario 8: Product mix and grouping

1. **Expected columns, in order:** Product, Qty, Revenue, Share, Avg price vs list, Last bought, Margin. No unpaid or allocated column anywhere.
2. Compare with the data: lines with the same code (any case, extra spaces) fall in one row; lines without a code are grouped by description and show "no product code".
3. **Expected:** adjustment lines are not in the table.
4. **Expected:** rows sorted by revenue (highest first); shares add up to about 100%.
5. **Expected:** a product with some costed lines shows its margin with a "partial" label; a product with no cost shows "-".
6. **Expected:** the margin coverage percentage appears under the table.
7. **Expected:** "Avg price vs list" shows "-" unless the Selected currency is the business's most-used invoice currency and the catalogue price is above 0.
8. **Expected:** the donut shows the top 5 plus "Other" (only when there are more than 5 products) with the total in the centre.

---

## Scenario 9: Product drill-down

1. Click a product row (or Tab to it and press Enter or Space).
2. **Expected:** BlockUI briefly; a detail row lists that product's lines (date, invoice link, qty, total, status), newest first.
3. Collapse and expand again.
4. **Expected:** no second request (cached).
5. Change the Period, expand again.
6. **Expected:** a fresh request is made.
7. For a product with more than 200 lines.
8. **Expected:** "Showing the latest 200 of N lines".
9. Check statuses.
10. **Expected:** each line shows exactly one of Unpaid, Partially paid, Paid, Overdue, Written off.

---

## Scenario 10: Buying rhythm

1. **Expected:** at most 5 products, each with typical gap, days since last purchase and a chip.
2. A product bought only once.
3. **Expected:** not listed.
4. Check chips against the rules:
   - days since last purchase less than 50% of the gap: "On track"
   - 50% or more and expected date more than 7 days away: "Reorder around {date}"
   - expected date within 7 days or already past: "Reorder soon"

---

## Scenario 11: Payment behaviour

1. **Expected:** average days to pay next to average terms; on-time count, total and percentage; "Currently overdue" equals the Overdue KPI; credit notes in the period (Issued and Applied only).
2. A customer with no settled invoices.
3. **Expected:** "No settled invoices yet".
4. Add a Draft credit note for the customer.
5. **Expected:** it is not counted.

---

## Scenario 12: Pending invoices, overdue and write-offs

1. **Expected:** the table lists every pending invoice, oldest due date first, with total, paid, credit notes, outstanding and a status pill:
   - "Overdue N days" (or "Overdue 1 day")
   - "Partially paid, due in N days"
   - "Unpaid, due in N days" (or "due today")
2. **Expected:** the footer total equals the Pending invoices KPI amount.
3. Take an unpaid invoice whose stored financial status is still Unpaid but whose due date has passed.
4. **Expected:** it shows as Overdue (overdue is derived, not read from the stored status).
5. Mark an invoice as written off (financial status 5) while it still has a balance.
6. **Expected:** it is not in Pending or Overdue and the drill-down shows "Written off".
7. Apply a credit note to an invoice so its balance is zero.
8. **Expected:** it leaves the pending list.
9. A customer with no pending invoices.
10. **Expected:** "No pending invoices" with no table header or footer.
11. Add a quotation for the customer.
12. **Expected:** no figure on the page changes.

---

## Scenario 13: Teaser cards - Notify me

1. **Expected:** Discounts and Price drift cards tagged LATER, no computed figures.
2. Click "Notify me when available" on Discounts.
3. **Expected:** BlockUI, then a success SweetAlert2; the button reads "You will be notified" and is disabled.
4. Reload.
5. **Expected:** the button is still disabled with the same text.
6. Query `[insight].[FeatureInterest]` (see below).
7. **Expected:** exactly one row for that business, user and `discounts`.
8. In dev tools, replay the POST twice.
9. **Expected:** still one row, both calls succeed.
10. Replay the POST with `featureKey` set to `bogus` or empty.
11. **Expected:** 400 validation response; no row; the page shows a warning if triggered from the UI.
12. Replay the POST without the antiforgery header.
13. **Expected:** request rejected; no row.

---

## Scenario 14: Excel export

1. Click "Export Excel" with a period and currency selected.
2. **Expected:** BlockUI, then a file `Purchases_{name}_{yyyyMMdd}.xlsx` downloads.
3. Open it.
4. **Expected:** sheets "Product mix" and "Pending invoices"; customer name, period, currency and UTC time above each table.
5. **Expected:** the Product mix sheet has Product, Code, Qty, Revenue, Share, Avg price paid, List price, Difference %, Last bought, Margin; money and percent cells are real numbers with number formats; rows match the screen.
6. **Expected:** the Pending invoices sheet has a total row equal to the on-screen footer.
7. Use a customer name containing `/ \ : * ?`.
8. **Expected:** the file name has those characters removed.

---

## Scenario 15: Empty states

1. Open the customer with no invoices.
2. **Expected:** one card: "No invoices yet for this customer"; no filters, KPIs, chart or tables.
3. Pick a period with no invoices for a customer that has invoices elsewhere.
4. **Expected:** empty-state messages in the product mix, donut and rhythm sections; period-independent KPIs (last 12 months, lifetime, last purchase, pending, overdue) still show.

---

## Scenario 16: Security and tenant isolation

1. As Business A, copy a customer id from Business B and open `/Customer/Purchases/{thatId}`.
2. **Expected:** the same not-found response as for an id that does not exist.
3. Repeat for `AxGetPurchasesData`, `AxGetProductPurchaseLines` and `AxGetExportPurchases`.
4. **Expected:** identical not-found responses; no data.
5. Log in as a user without Customer module access.
6. **Expected:** access denied like the other Customer pages.
7. Log out and request the page.
8. **Expected:** redirected to login.

---

## Scenario 17: Error handling

1. In dev tools, set the network to Offline, then change the Period.
2. **Expected:** BlockUI disappears, an error SweetAlert2 appears, and the previous data and period remain.
3. Throttle the network so a request exceeds 30 seconds.
4. **Expected:** the request is aborted, an error dialog appears, BlockUI is hidden.
5. Force a server error (for example stop the database).
6. **Expected:** a general message only; no exception text or stack trace is shown.

---

## Scenario 18: Responsive layout and accessibility

1. Check widths 1200, 810 and 375 px.
2. **Expected:** KPI cards in 3 columns above 900px, 2 columns from 561 to 900px, 1 column at 560px or less; product mix and donut stack at 900px or less; header buttons wrap; every table scrolls horizontally inside its card; the page itself never scrolls sideways.
3. Resize across 900px and 560px with a filter selected.
4. **Expected:** layout changes without a reload and the filter state is kept.
5. Use only the keyboard.
6. **Expected:** KPI toggles, product rows and chart bars are reachable; focus is visible; `aria-expanded` changes.
7. **Expected:** every status colour has a text label; each chart has a text alternative.

---

## Verification Checklist

| # | Check | Pass |
|---|-------|------|
| 1 | Purchases button after Statement, before Deactivate, shown for inactive customers | [ ] |
| 2 | Breadcrumb, heading, header buttons correct | [ ] |
| 3 | Six KPIs match the database | [ ] |
| 4 | KPI collapse works, cookie `cp_kpi_collapsed` written and read server-side | [ ] |
| 5 | Period options, custom range, URL sharing, invalid period fallback | [ ] |
| 6 | Currency switch only for multi-currency customers; no cross-currency sums | [ ] |
| 7 | 12-month chart, compare line, tooltip, current month marker | [ ] |
| 8 | Product grouping by code, then description; adjustment lines excluded | [ ] |
| 9 | Margin coverage and "partial" label | [ ] |
| 10 | Drill-down loads once, 200-line notice, correct statuses | [ ] |
| 11 | Buying rhythm chips at the 50% and 7-day boundaries | [ ] |
| 12 | Payment behaviour and credit notes (Draft excluded) | [ ] |
| 13 | Pending table footer equals Pending KPI | [ ] |
| 14 | Overdue derived from due date, not the stored status | [ ] |
| 15 | Written-off invoices not pending or overdue | [ ] |
| 16 | Quotations change nothing | [ ] |
| 17 | Notify me stores one row, idempotent, validated, antiforgery enforced | [ ] |
| 18 | Excel export content, numeric cells, file name | [ ] |
| 19 | Empty states | [ ] |
| 20 | Tenant isolation: identical not-found everywhere | [ ] |
| 21 | Errors: BlockUI hidden, previous data kept, no internals leaked | [ ] |
| 22 | Responsive at 375, 810 and 1200 px; keyboard access | [ ] |
| 23 | No native alert, confirm or prompt dialogs | [ ] |
| 24 | Page loads in under 2 seconds for a customer with about 1000 invoices | [ ] |

---

## Database Queries for Manual Inspection

Set the variables first (use the business and customer you are testing).

```sql
USE [Portal]
GO

DECLARE @BusinessId INT = 1;
DECLARE @CustomerId INT = 1;
```

### Counted invoices with outstanding balance (compare with Pending, Overdue and the pending table)

```sql
SELECT [invoice].[Invoice].[Id],
       [invoice].[Invoice].[InvoiceNumber],
       [invoice].[Invoice].[InvoiceDate],
       [invoice].[Invoice].[DueDate],
       [invoice].[Invoice].[CurrencyCode],
       [invoice].[Invoice].[TotalAmount],
       [invoice].[Invoice].[InvoiceFinancialStatusTypeId],
       (SELECT ISNULL(SUM([revenue].[Payment].[Amount]), 0)
        FROM [revenue].[Payment]
        WHERE [revenue].[Payment].[InvoiceId] = [invoice].[Invoice].[Id]
          AND [revenue].[Payment].[BusinessId] = @BusinessId
          AND [revenue].[Payment].[IsVoided] = 0
          AND [revenue].[Payment].[PaymentDateUtc] <= GETUTCDATE()) AS [TotalPaid],
       (SELECT ISNULL(SUM([credit].[CreditNoteApplication].[AmountApplied]), 0)
        FROM [credit].[CreditNoteApplication]
        INNER JOIN [credit].[CreditNote]
            ON [credit].[CreditNoteApplication].[CreditNoteId] = [credit].[CreditNote].[Id]
        WHERE [credit].[CreditNoteApplication].[InvoiceId] = [invoice].[Invoice].[Id]
          AND [credit].[CreditNote].[BusinessId] = @BusinessId
          AND [credit].[CreditNoteApplication].[IsVoided] = 0) AS [TotalCredited]
FROM [invoice].[Invoice]
WHERE [invoice].[Invoice].[BusinessId] = @BusinessId
  AND [invoice].[Invoice].[CustomerId] = @CustomerId
  AND [invoice].[Invoice].[IsDeleted] = 0
  AND [invoice].[Invoice].[InvoiceStatusTypeId] <> 1
  AND [invoice].[Invoice].[InvoiceStatusTypeId] <> 3
ORDER BY [invoice].[Invoice].[DueDate];
```

Outstanding = TotalAmount - TotalPaid - TotalCredited. Pending = outstanding above 0 and `InvoiceFinancialStatusTypeId` not 5. Overdue = pending and DueDate before today (UTC).

### Product lines grouped like the dashboard (last 12 months, one currency)

```sql
SELECT ISNULL(NULLIF(UPPER(LTRIM(RTRIM([invoice].[InvoiceLine].[ProductCode]))), ''),
              'DESC: ' + UPPER(LTRIM(RTRIM([invoice].[InvoiceLine].[Description])))) AS [GroupKey],
       SUM([invoice].[InvoiceLine].[Quantity]) AS [Qty],
       SUM([invoice].[InvoiceLine].[LineTotal]) AS [Revenue],
       MAX([invoice].[Invoice].[InvoiceDate]) AS [LastBought]
FROM [invoice].[InvoiceLine]
INNER JOIN [invoice].[Invoice]
    ON [invoice].[InvoiceLine].[InvoiceId] = [invoice].[Invoice].[Id]
WHERE [invoice].[Invoice].[BusinessId] = @BusinessId
  AND [invoice].[Invoice].[CustomerId] = @CustomerId
  AND [invoice].[Invoice].[IsDeleted] = 0
  AND [invoice].[Invoice].[InvoiceStatusTypeId] <> 1
  AND [invoice].[Invoice].[InvoiceStatusTypeId] <> 3
  AND [invoice].[Invoice].[CurrencyCode] = 'EUR'
  AND [invoice].[InvoiceLine].[IsAdjustmentLine] = 0
  AND [invoice].[Invoice].[InvoiceDate] >= DATEFROMPARTS(YEAR(DATEADD(MONTH, -11, GETUTCDATE())), MONTH(DATEADD(MONTH, -11, GETUTCDATE())), 1)
GROUP BY ISNULL(NULLIF(UPPER(LTRIM(RTRIM([invoice].[InvoiceLine].[ProductCode]))), ''),
                'DESC: ' + UPPER(LTRIM(RTRIM([invoice].[InvoiceLine].[Description]))))
ORDER BY [Revenue] DESC;
```

### Credit notes in a period (Issued and Applied only, currency from the linked invoice)

```sql
SELECT COUNT(*) AS [CreditNoteCount],
       ISNULL(SUM([credit].[CreditNote].[TotalAmount]), 0) AS [CreditNoteTotal]
FROM [credit].[CreditNote]
INNER JOIN [invoice].[Invoice]
    ON [credit].[CreditNote].[InvoiceId] = [invoice].[Invoice].[Id]
WHERE [credit].[CreditNote].[BusinessId] = @BusinessId
  AND [credit].[CreditNote].[CustomerId] = @CustomerId
  AND [credit].[CreditNote].[CreditNoteStatusTypeId] IN (2, 3)
  AND [invoice].[Invoice].[CurrencyCode] = 'EUR';
```

### Feature interest rows

```sql
SELECT * FROM [insight].[FeatureInterest]
WHERE [insight].[FeatureInterest].[BusinessId] = @BusinessId;
```

### Migration and index check

```sql
SELECT name FROM sys.indexes
WHERE name IN (N'IX_Invoice_BusinessId_CustomerId_InvoiceDate', N'IX_InvoiceLine_InvoiceId',
               N'IX_Payment_InvoiceId', N'UX_FeatureInterest_Business_User_Feature');
```

### Clean up test interest rows

```sql
DELETE FROM [insight].[FeatureInterest]
WHERE [insight].[FeatureInterest].[BusinessId] = @BusinessId
  AND [insight].[FeatureInterest].[UserId] = N'<your user id>';
```