# Small Business Admin Time Savings — Feature Proposals

**Date:** August 2026 **Audience:** 3 Inventors product team **Focus:** Features that reduce daily business administration time for companies with 1–3 people **Print-ready:** Yes

## The Problem

Small businesses (1–3 people) split their day between two things: doing the work that earns revenue, and administering the business. Every minute spent on invoicing, chasing payments, tracking expenses, preparing VAT, and sending follow-ups is a minute not spent on customers.

The Portal already handles the core operations — quotations, invoicing, revenue control, purchases, VAT. But there are gaps where the business owner is still doing repetitive manual work that the platform could automate, prompt, or eliminate.

This document proposes 11 features, organised by the admin bottleneck they solve. Each includes a description, the time it saves, the estimated effort, and the subscription tier.

## Category 1: Payment Collection

The platform already has automated payment reminders, Stripe Connect, payment links, and payment schedules. These proposals extend that foundation.

### Proposal 1: Smart Reminder Timing (Value-Based Cadence)

**Problem:** All invoices follow the same reminder schedule (e.g., 7 days, 14 days, 30 days overdue). A €200 invoice and a €15,000 invoice get the same treatment. High-value invoices should be chased earlier and more frequently.

**Solution:** Allow the business owner to define a value threshold. Invoices above the threshold follow an accelerated reminder cadence (e.g., start at day 3 instead of day 7, remind every 5 days instead of every 7). The threshold is configurable per business.

**Example:**

-   Default cadence: Remind at 7, 14, 30 days overdue
-   High-value cadence (invoices \> €2,000): Remind at 3, 7, 14, 21, 30 days overdue

**Time saved:** Indirect — faster payment collection, fewer forgotten high-value invoices.

**Effort:** 2–3 days **Tier:** Professional (extends existing automated reminders) **Complexity:** Quick win — builds on existing reminder engine

***

### Proposal 2: "Thank You" Auto-Email on Payment Received

**Problem:** When a customer pays, the business owner usually does nothing — no acknowledgment, no receipt, no confirmation. The customer wonders if their payment was received. The owner has to manually send a "thanks, got it" email if they remember.

**Solution:** When a payment is recorded against an invoice (manual entry or Stripe webhook), automatically send a brief confirmation email to the customer: "Thank you for your payment of €420 for invoice INV-00089." Include the receipt PDF if the business has receipts enabled.

**Configurable:** The business owner can enable/disable this per business. Default: enabled.

**Example email:**

>   Subject: Payment received — INV-00089

>   Dear [Customer Name],

>   Thank you for your payment of €420.00 for invoice INV-00089.

>   [Download Receipt] (if applicable)

>   Kind regards, [Business Name]

**Time saved:** \~15 minutes per week (eliminates manual "payment received" emails for 5–10 invoices/week).

**Effort:** 1–2 days **Tier:** Foundation (basic courtesy, not a premium feature — builds trust) **Complexity:** Quick win — uses existing email infrastructure and receipt generation

***

### Proposal 3: Weekly Outstanding Balance Digest Email

**Problem:** A business owner with 20–30 active invoices doesn't log in daily to check what's outstanding. They lose track of overdue invoices until cash flow gets tight. By the time they notice, the invoice is 45 days overdue and the conversation is awkward.

**Solution:** Every Monday morning at 08:00, send the business owner an email digest:

**Example:**

>   Subject: Weekly Receivables Summary — 25 Aug 2026

>   **Outstanding: €3,200** across 4 invoices

>   1 invoice overdue by 30+ days: €1,800 (Customer X)

>   2 invoices due this week: €900

>   1 invoice due next week: €500

>   [View Revenue Dashboard →]

**Configurable:** The business owner can enable/disable and choose the day (default: Monday).

**Time saved:** \~30 minutes per week (replaces the "let me log in and check" ritual and prevents overdue invoices from being forgotten).

**Effort:** 2–3 days **Tier:** Professional (financial intelligence / automation) **Complexity:** Quick win — aggregation query + scheduled email

***

## Category 2: Document Creation

Quotations and invoices are the core workflow. These proposals reduce repetitive creation tasks.

***

### Proposal 4: Recurring Invoices

**Problem:** A business that charges the same customer the same amount every month (maintenance contract, retainer, rent, subscription) creates the same invoice manually 12 times per year. For 10 monthly clients, that's 120 invoices per year — each taking 3–5 minutes to create, review, and issue.

**Solution:** Define a recurring invoice schedule:

-   Customer, line items, amount, frequency (monthly/quarterly/annually)
-   Auto-generate a draft invoice on the specified day of each period
-   Notify the business owner: "3 recurring invoices generated for September. Review and issue."
-   One-click issue, or auto-issue if the owner enables it

**The automation pipeline becomes:**

```
Recurring schedule → Auto-draft invoice → Auto-payment link → Auto-reminder → Customer pays → Auto-record
```

The business owner does nothing. Money flows in.

**Time saved:** 2–4 hours per month for a business with 10–20 monthly clients.

**Effort:** 1–2 weeks **Tier:** Professional (automation feature) **Complexity:** Medium-complex — needs a schedule entity, a background job (or manual trigger), and draft generation logic

### Proposal 5: Quick "Next Month" Invoice Duplicate

**Problem:** Even without full recurring invoices (Proposal 4), business owners often duplicate last month's invoice and manually adjust the date. The current duplicate feature copies everything but the owner still needs to change the invoice date, due date, and number.

**Solution:** A "Create Next Month's Invoice" button on the invoice detail page that:

1.  Duplicates all line items
2.  Sets the invoice date to the 1st of next month (or the same day-of-month)
3.  Sets the due date based on the original payment terms (e.g., +30 days)
4.  Creates as a draft for review

This is a lighter alternative to full recurring invoices — useful on Foundation tier.

**Time saved:** 2–3 minutes per invoice, adds up for businesses with 10+ monthly clients.

**Effort:** 2–3 days **Tier:** Foundation **Complexity:** Quick win — extends existing duplicate logic

***

### Proposal 6: Favourite / Recently Used Line Items

**Problem:** A business that sells the same 5–10 services/products creates invoices with the same line items repeatedly. The product catalog helps, but navigating it each time adds friction. The owner types the same descriptions, quantities, and prices over and over.

**Solution:** When adding a line item, show a "Recently Used" section above the catalog search. Display the last 5–10 line items the user added (across any invoice/quotation), with a one-click "Add" button that pre-fills the description, quantity, unit price, and VAT rate.

**Alternative:** A "Favourites" star icon on each product in the catalog. Starred products appear in a separate "Favourites" tab in the line item selector.

**Time saved:** 10–15 minutes per invoice batch (for a batch of 10 invoices with similar items).

**Effort:** 2–3 days **Tier:** Foundation **Complexity:** Quick win — query recent line items from the last 30 days, deduplicate by description

## Category 3: Financial Awareness

Small business owners often don't know their financial position until they sit down with a spreadsheet. These proposals bring awareness to them passively.

***

### Proposal 7: Weekly Financial Snapshot Email

**Problem:** The owner doesn't have a CFO or accountant reviewing their numbers weekly. They're running the business on gut feel. When cash gets tight, it's usually a surprise.

**Solution:** Every Monday (configurable), send a financial summary email:

**Example:**

>   Subject: Weekly Financial Snapshot — 25 Aug 2026

>   **This Week:**

>   Invoiced: €2,400

>   Received: €1,600

>   Purchases recorded: €800

>   Net cash movement: +€800

>   **Overall Position:**

>   Outstanding receivables: €3,200

>   This month's revenue: €8,400

>   This month's expenses: €3,600

>   [View Dashboard →]

This overlaps slightly with Proposal 3 (outstanding balance digest) — they could be combined into a single weekly email with two sections: receivables + financial summary.

**Time saved:** \~30 minutes per week (replaces manual revenue/expense checking).

**Effort:** 3–5 days **Tier:** Professional (financial intelligence) **Complexity:** Quick win — aggregation queries + scheduled email. Could share infrastructure with Proposal 3.

***

### Proposal 8: Supplier Payment Due Dates

**Problem:** Purchases are recorded in the platform but without a payment due date. The business owner tracks supplier payment deadlines in their head or on a separate spreadsheet. They miss a supplier payment, get a late fee, or damage the relationship.

**Solution:** Add an optional `DueDate` field to the Purchase entity. When set, surface upcoming supplier payment deadlines on the dashboard:

>   **Supplier Payments Due This Week:**

>   Office Supplies Ltd — €340 due 28 Aug

>   Cloud Hosting — €89 due 01 Sep

This gives a complete cash picture: what customers owe you (receivables) AND what you owe suppliers (payables).

**Time saved:** \~30 minutes per week (eliminates separate payables tracking).

**Effort:** 1 week **Tier:** Foundation (operational, not intelligence) **Complexity:** Medium — one DB migration, UI change on purchase form, dashboard widget

## Category 4: VAT & Compliance

***

### Proposal 9: VAT Period Pre-Submission Checklist

**Problem:** Before submitting VAT, the business owner manually checks: "Have I recorded all purchases? Are there unassigned invoices? Do my numbers look right?" They often miss something, submit incorrect VAT, and discover the error later.

**Solution:** When the owner opens a VAT period to review before submission, show an automated checklist:

-   [ ] All invoices in this period have VAT assigned — **3 invoices unassigned** (link to fix)
-   [ ] Purchase count is consistent with previous periods — **This period: 12 purchases. Last period: 18. 33% fewer — is this expected?**
-   [ ] No invoices with 0% VAT that should have VAT applied — **1 invoice flagged** (link to review)
-   [ ] All credit notes are assigned to this period — **OK**
-   [x] Output VAT computed: €1,240
-   [x] Input VAT computed: €380
-   [x] Net payable: €860

The checklist is informational — it doesn't block submission. It just highlights potential issues before the numbers go to the tax office.

**Time saved:** 1–2 hours per VAT period (quarterly: 4–8 hours per year). More importantly, prevents costly errors.

**Effort:** 3–5 days **Tier:** Foundation **Complexity:** Quick win — queries against existing data, display as a checklist component

## Category 5: End-of-Day Operations

***

### Proposal 10: Daily/Weekly Action Prompt

**Problem:** Small business owners don't have a process for "closing the books" at the end of the day or week. Unrecorded payments pile up, accepted quotations don't get converted, and expenses from last week are still in a drawer.

**Solution:** When the owner logs in (or as a Friday email), show a summary of pending actions:

**Example (login prompt):**

>   **This Week's Pending Actions:**

>   3 quotations accepted but not yet converted to invoices

>   2 Stripe payments received but not matched to invoices

>   5 invoices issued but not yet sent/shared with customers

>   1 VAT period closing in 10 days — 2 unassigned invoices remaining

Each item links directly to the relevant page. The owner can act immediately or dismiss.

**Time saved:** \~20 minutes per week (prevents work from piling up and becoming a bigger problem later).

**Effort:** 3–5 days **Tier:** Foundation **Complexity:** Quick win — aggregation queries, displayed as a dismissable card on the dashboard or as a weekly email

***

### Proposal 11: Unrecorded Revenue Detection (Stripe Reconciliation)

**Problem:** A customer pays via Stripe, but the business owner doesn't record it against the invoice. The payment sits in the Stripe account, the invoice shows as unpaid, and the automated reminders keep firing — embarrassing the business and irritating the customer.

**Solution:** Periodically check Stripe payment events against recorded payments. When a Stripe payment doesn't match any recorded payment in the platform, surface a reconciliation prompt:

>   **Unmatched Stripe Payments:**

>   €420.00 received on 25 Aug from card ending 4242 — **Match to invoice?** [INV-00089] [INV-00090] [Other]

The owner clicks the correct invoice, and the payment is recorded automatically. This is the last piece of the automated payment pipeline — it catches the cases where auto-recording fails or the customer paid outside the shared link flow.

**Time saved:** Prevents lost revenue and embarrassing double-reminders.

**Effort:** 1 week **Tier:** Professional (Stripe integration required) **Complexity:** Medium — needs Stripe event polling/webhook and reconciliation logic

## Quotation Follow-Up

***

### Proposal 12: Quotation Follow-Up Reminder

**Problem:** A quotation was sent 5 days ago and the customer hasn't responded. The business owner forgets to follow up, and the deal goes cold. By the time they remember, it's been 3 weeks and the customer has moved on.

**Solution:** When a quotation has been in "Sent" status for a configurable number of days (default: 5), show a reminder to the business owner:

>   **Quotation Follow-Up:**

>   QUO-2026-07-00009 sent to OVIS ART LTD — 5 days ago, no response

>   QUO-2026-07-00007 sent to Cyprus Tech — 8 days ago, no response

>   [Follow Up] [Dismiss] [Archive]

This doesn't email the customer automatically — it reminds the owner to take action. The "Follow Up" button could open the quotation detail with a "Send follow-up" option.

**Time saved:** \~30 minutes per week (prevents lost deals and eliminates the "I forgot to follow up" problem).

**Effort:** 2–3 days **Tier:** Foundation (this is basic operational awareness, not automation) **Complexity:** Quick win — query quotations in "Sent" status older than N days

## Summary Table

| \# | Feature                      | Admin Pain Point                       | Time Saved                   | Effort    | Tier         | Type        |
|----|------------------------------|----------------------------------------|------------------------------|-----------|--------------|-------------|
| 1  | Smart Reminder Timing        | Slow collection on high-value invoices | Indirect (faster collection) | 2–3 days  | Professional | Enhancement |
| 2  | Thank You Auto-Email         | Manual payment confirmations           | 15 min/week                  | 1–2 days  | Foundation   | Quick win   |
| 3  | Weekly Outstanding Digest    | Forgetting overdue invoices            | 30 min/week                  | 2–3 days  | Professional | Quick win   |
| 4  | Recurring Invoices           | Repetitive monthly invoice creation    | 2–4 hours/month              | 1–2 weeks | Professional | Complex     |
| 5  | Quick "Next Month" Duplicate | Manual date adjustment on duplicates   | 2–3 min/invoice              | 2–3 days  | Foundation   | Quick win   |
| 6  | Favourite Line Items         | Re-typing the same descriptions        | 10–15 min/batch              | 2–3 days  | Foundation   | Quick win   |
| 7  | Weekly Financial Snapshot    | No visibility into financial position  | 30 min/week                  | 3–5 days  | Professional | Quick win   |
| 8  | Supplier Payment Due Dates   | Missing supplier deadlines             | 30 min/week                  | 1 week    | Foundation   | Medium      |
| 9  | VAT Pre-Submission Checklist | Errors in VAT filings                  | 1–2 hours/quarter            | 3–5 days  | Foundation   | Quick win   |
| 10 | Daily/Weekly Action Prompt   | Work piling up unnoticed               | 20 min/week                  | 3–5 days  | Foundation   | Quick win   |
| 11 | Unrecorded Revenue Detection | Lost Stripe payments                   | Prevents lost revenue        | 1 week    | Professional | Medium      |
| 12 | Quotation Follow-Up Reminder | Forgotten quotation follow-ups         | 30 min/week                  | 2–3 days  | Foundation   | Quick win   |

## Recommended Implementation Order

### Phase 1: Quick Wins (September 2026)

These can each ship in 1–3 days and immediately reduce admin time.

| Priority | Feature                             | Effort   | Why First                                               |
|----------|-------------------------------------|----------|---------------------------------------------------------|
| 1        | Thank You Auto-Email (\#2)          | 1–2 days | Immediate customer trust, zero ongoing effort for owner |
| 2        | Quotation Follow-Up Reminder (\#12) | 2–3 days | Prevents lost revenue from forgotten quotations         |
| 3        | Quick "Next Month" Duplicate (\#5)  | 2–3 days | Instant time saver for recurring billing businesses     |
| 4        | Favourite Line Items (\#6)          | 2–3 days | Speeds up the daily invoicing workflow                  |

### Phase 2: Weekly Intelligence (October 2026)

Email digests that keep the owner informed without logging in.

| Priority | Feature                           | Effort   | Why Next                                       |
|----------|-----------------------------------|----------|------------------------------------------------|
| 5        | Weekly Outstanding Digest (\#3)   | 2–3 days | Prevents overdue invoices from being forgotten |
| 6        | Weekly Financial Snapshot (\#7)   | 3–5 days | Can share email infrastructure with \#3        |
| 7        | Daily/Weekly Action Prompt (\#10) | 3–5 days | Catches pending actions before they pile up    |

### Phase 3: Operational Depth (October–November 2026)

Medium-effort features that fill structural gaps.

| Priority | Feature                            | Effort   | Why Then                                |
|----------|------------------------------------|----------|-----------------------------------------|
| 8        | VAT Pre-Submission Checklist (\#9) | 3–5 days | Aligns with Q3 VAT submission deadlines |
| 9        | Supplier Payment Due Dates (\#8)   | 1 week   | Completes the cash visibility picture   |
| 10       | Smart Reminder Timing (\#1)        | 2–3 days | Enhances existing automation            |

### Phase 4: The Big One (November–December 2026)

| Priority | Feature                             | Effort    | Why Last                                                  |
|----------|-------------------------------------|-----------|-----------------------------------------------------------|
| 11       | Recurring Invoices (\#4)            | 1–2 weeks | Biggest single time saver — needs careful design          |
| 12       | Unrecorded Revenue Detection (\#11) | 1 week    | Requires Stripe reconciliation — builds on Stripe Connect |

## Design Principle

Every feature in this document follows the same philosophy: **reduce the time between "something happened" and "the right action is taken."**

-   A payment arrives → Thank the customer automatically
-   An invoice is overdue → Remind intelligently based on value
-   A quotation goes unanswered → Prompt the owner to follow up
-   A VAT period is approaching → Check for issues before submission
-   A supplier payment is due → Surface it before it's late

The goal is not to add complexity. It's to make the platform work harder so the business owner doesn't have to.

***

*Document prepared for review. Print and annotate — we can prioritise and create specs for any items you approve.*
