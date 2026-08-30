# Feature Enhancement Recommendations

**Date:** August 2026  
**Context:** All subscription tiers live (Foundation, Professional, Enterprise). Payroll complete. Sales pipeline operational. Price tiers shipped.

---

## Quick Wins (1–3 days each)

### 1. Lead Email Response (Sales Module)

Already documented in `future-features/lead-email-response.md`. The infrastructure exists — SMTP, templates, rendering. Only needs wiring the "Send Email" button in the Lead Detail response modal. Upgrades the sales pipeline from a log to an actual communication tool.

**Effort:** 1–2 days  
**Tier:** Professional  
**Impact:** High — sales teams can respond directly from the platform instead of switching to email

---

### 2. Invoice Partial Payment Reminders

Current automated reminders fire for the full outstanding balance. For invoices with partial payments already received, the reminder should acknowledge the partial payment and request the remaining balance. Small wording change in the reminder template engine.

**Effort:** 1 day  
**Tier:** Professional (automated reminders)  
**Impact:** Medium — avoids irritating customers who've already made partial payments

---

### 3. Product Usage Analytics on Product Detail Page

The Product Detail page has KPI cards (revenue, units, margin) but lacks trend comparisons. Add:
- Month-over-month revenue change (% indicator)
- Comparison vs same period last year
- "Trending up/down" badge

**Effort:** 1–2 days  
**Tier:** Foundation  
**Impact:** Low-medium — helps product pricing decisions

---

### 4. Quotation Expiry Warning Banner

Quotations have a `ValidUntil` date. When a quotation is approaching expiry (within 7 days) or has expired, show a visible banner on the quotation edit/detail page. Optionally surface on the dashboard brief.

**Effort:** 1 day  
**Tier:** Foundation  
**Impact:** Medium — prevents missed deal deadlines

---

### 5. Customer Statement PDF Export

Customer statements exist as a view. Add a "Download PDF" button that renders the statement as a professional branded PDF (same PuppeteerSharp + Razor pattern as invoices/payslips). Customers often request statements for their own accounting.

**Effort:** 1–2 days  
**Tier:** Foundation  
**Impact:** Medium — commonly requested by end-users

---

## Medium Effort (1–2 weeks each)

### 6. Recurring Expenses Auto-Detection

The platform already has Recurring Expense Validation (manual rules). Extend it with auto-detection: identify suppliers with regular monthly charges (same amount ± 5%, same frequency) and suggest creating recurring expense rules automatically.

**Effort:** 1 week  
**Tier:** Professional  
**Impact:** Medium — reduces setup friction for expense rules

---

### 7. Customer Intelligence — Basic (Top Customers + At-Risk)

The `Customer_Intelligence_Brief.md` is well-documented. Phase 1 would be just:
- Top Customers by revenue (sorted table with period filter)
- At-Risk detection (customers whose order frequency dropped vs historical average)

Both derive from existing invoice data — no new input needed.

**Effort:** 1–2 weeks  
**Tier:** Professional  
**Impact:** High — transforms the platform into an intelligence tool; strong upgrade trigger from Foundation

---

### 8. Document Templates (Invoice/Quotation Branding)

Allow businesses to customise PDF templates: logo placement, colour scheme, footer text, payment terms text. Currently all businesses get the same template. Even 2–3 template options would differentiate.

**Effort:** 1–2 weeks  
**Tier:** Foundation (basic logo/colour) + Professional (full template editor)  
**Impact:** High — branding is important to business owners; one of the most common feature requests for invoicing platforms

---

### 9. Dashboard Today's Brief Enhancement

The dashboard already has a brief section. Extend it with:
- Invoices expiring this week (upcoming due dates)
- Unpaid invoices older than 30 days count
- Weekly revenue vs previous week comparison
- Next upcoming VAT submission deadline

**Effort:** 1 week  
**Tier:** Foundation  
**Impact:** Medium — makes the dashboard more actionable

---

### 10. Bulk Actions on Invoice/Quotation Lists

Select multiple invoices/quotations and perform bulk actions:
- Send reminders (all selected)
- Download all as ZIP
- Change status (mark as issued/written off)
- Export selected to Excel

**Effort:** 1–2 weeks  
**Tier:** Professional  
**Impact:** Medium — saves time for businesses with high invoice volume

---

## Larger Features (Enterprise Roadmap)

### 11. Client Portal (Customer Self-Service)

Listed as "Coming Soon" on Enterprise card. Customers log in to see their invoices, outstanding balances, download statements, and make payments.

**Effort:** 3–4 weeks  
**Tier:** Enterprise  
**Impact:** Very high — differentiator from competitors; reduces admin time dramatically

---

### 12. Multi-Currency Support

Listed as "Coming Soon" on Enterprise card. Invoice in customer's currency, report in base currency. Requires exchange rate management (manual or API).

**Effort:** 3–4 weeks  
**Tier:** Enterprise  
**Impact:** High for businesses with international clients

---

### 13. API Access & Webhooks

Listed as "Coming Soon" on Enterprise card. REST API for external integrations + webhook subscriptions for real-time event notifications.

**Effort:** 4–6 weeks  
**Tier:** Enterprise  
**Impact:** High for businesses that need to connect to accounting software, CRMs, or custom tools

---

### 14. Activity Timeline & Notifications

Listed as "Coming Soon" on Enterprise card. Real-time activity feed showing what team members are doing. Email digests for key events.

**Effort:** 2–3 weeks  
**Tier:** Enterprise  
**Impact:** Medium-high for teams — visibility into business operations

---

## Operational Improvements (No Tier Change)

### 15. Email Delivery Tracking (Open/Click)

Payment reminders and shared invoice emails already send. Add open/click tracking to show which customers opened their reminders. Already listed as a Professional feature ("Open tracking & email analytics") but not yet implemented.

**Effort:** 1 week  
**Tier:** Professional  
**Impact:** Medium — gives visibility into whether customers are ignoring invoices or genuinely not seeing them

---

### 16. Webhook for Payment Received

When a payment is recorded (manual or Stripe), fire a webhook to external systems. Useful for businesses integrating with their CRM or fulfilment system. Would be a lightweight precursor to the full API (item 13).

**Effort:** 3–5 days  
**Tier:** Enterprise  
**Impact:** Low-medium (niche but valuable for integration-ready businesses)

---

### 17. Data Export (Full Business Backup)

Allow business owners to export all their data (customers, invoices, quotations, payments, purchases) as a structured ZIP of CSVs. Important for trust and data portability.

**Effort:** 3–5 days  
**Tier:** Foundation (it's a trust feature, not a premium feature)  
**Impact:** Low direct value, but critical for trust and reduces churn anxiety

---

## Recommendation Priority

| # | Feature | Effort | Impact | Suggested Next |
|---|---------|--------|--------|----------------|
| 1 | Lead Email Response | 1–2d | High | ✅ Quick win |
| 7 | Customer Intelligence (basic) | 1–2w | High | ✅ Strong differentiator |
| 5 | Customer Statement PDF | 1–2d | Medium | ✅ Quick win |
| 8 | Document Templates | 1–2w | High | ✅ Common request |
| 4 | Quotation Expiry Warning | 1d | Medium | ✅ Quick win |
| 9 | Dashboard Brief Enhancement | 1w | Medium | Consider |
| 11 | Client Portal | 3–4w | Very high | Enterprise roadmap |
| 17 | Data Export | 3–5d | Trust | Consider for Foundation |
| 15 | Email Open/Click Tracking | 1w | Medium | Professional promise |

---

## Implementation Timetable

| # | Feature | Target Start | Target Delivery | Tier | Status |
|---|---------|-------------|-----------------|------|--------|
| 1 | Lead Email Response | Sep 2026 W1 | Sep 2026 W1 | Professional | 🔲 Not Started |
| 5 | Customer Statement PDF | Sep 2026 W1 | Sep 2026 W1 | Foundation | ✅ Completed (already exists) |
| 4 | Quotation Expiry Warning | Sep 2026 W1 | Sep 2026 W1 | Foundation | ✅ Completed 2026-08-20 |
| 3 | Product Usage Trend Indicators | Sep 2026 W2 | Sep 2026 W2 | Foundation | 🔲 Not Started |
| 2 | Invoice Partial Payment Reminders | Sep 2026 W2 | Sep 2026 W2 | Professional | 🔲 Not Started |
| 9 | Dashboard Brief Enhancement | Sep 2026 W2–W3 | Sep 2026 W3 | Foundation | ✅ Completed (sales-tasks-meetings-enhancements, Aug 2026) |
| 7 | Customer Intelligence (basic) | Sep 2026 W3–W4 | Oct 2026 W1 | Professional | 🔲 Not Started |
| 8 | Document Templates | Oct 2026 W1–W2 | Oct 2026 W2 | Foundation + Professional | 🔲 Not Started |
| 6 | Recurring Expenses Auto-Detection | Oct 2026 W2–W3 | Oct 2026 W3 | Professional | 🔲 Not Started |
| 10 | Bulk Actions (Invoice/Quotation) | Oct 2026 W3–W4 | Oct 2026 W4 | Professional | 🔲 Not Started |
| 15 | Email Open/Click Tracking | Nov 2026 W1 | Nov 2026 W1 | Professional | 🔲 Not Started |
| 17 | Data Export (Full Backup) | Nov 2026 W1–W2 | Nov 2026 W2 | Foundation | 🔲 Not Started |
| 14 | Activity Timeline & Notifications | Nov 2026 W2–W4 | Nov 2026 W4 | Enterprise | 🔲 Not Started |
| 16 | Webhook for Payment Received | Dec 2026 W1 | Dec 2026 W1 | Enterprise | 🔲 Not Started |
| 11 | Client Portal | Dec 2026 W1–W4 | Jan 2027 W1 | Enterprise | 🔲 Not Started |
| 12 | Multi-Currency Support | Jan 2027 W1–W4 | Jan 2027 W4 | Enterprise | 🔲 Not Started |
| 13 | API Access & Webhooks | Feb 2027 W1–W4 | Mar 2027 W1 | Enterprise | 🔲 Not Started |

### Status Legend

| Symbol | Meaning |
|--------|---------|
| 🔲 | Not Started |
| 🟡 | In Progress |
| ✅ | Completed |
| ⏸️ | Paused / Parked |
| ❌ | Cancelled |

---

## Notes

- Items 11–14 are already on the Enterprise card as "Coming Soon" — they should be prioritised based on customer demand
- The "Customer Intelligence" feature (item 7) is the strongest Professional upgrade trigger — businesses on Foundation would see analytics they can't access, creating natural upsell pressure
- "Document Templates" (item 8) is the #1 feature request across invoicing platforms globally — even basic customisation (logo position, brand colour) moves the needle
- All quick wins (items 1, 4, 5) are low-risk and can ship within a sprint
