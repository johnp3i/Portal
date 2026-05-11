# MyChair Modules — Execution Checklists (KIRO Ready)
Version: v1.0

---

# 1. QUOTATION MODULE

## Domain
- [ ] Quotation
- [ ] QuotationLine
- [ ] OptionGroups (Device, Setup)
- [ ] Single selection per group

## Pricing
- [ ] Selected options only
- [ ] Min/Max calculation

## UI
- [ ] Option selection
- [ ] Dynamic pricing

## Persistence
- [ ] Immutable snapshot

## Conversion
- [ ] Quotation → Invoice (no recalculation)

---

# 2. INSIGHTS MODULE

## Data
- [ ] Validate aggregated inputs

## KPI
- [ ] Sales
- [ ] Invoices
- [ ] Avg Ticket

## Signals
- [ ] Trends
- [ ] Comparisons

## Story Engine
- [ ] Deterministic insights

## UI
- [ ] KPI cards
- [ ] Charts
- [ ] Story panel

---

# 3. REVENUE CONTROL MODULE

## Domain
- [ ] Invoice
- [ ] Payment
- [ ] Customer

## Logic
- [ ] Outstanding calculation

## Status
- [ ] Paid / Partial / Overdue

## Payments
- [ ] Validation
- [ ] Recalculation

## UI
- [ ] Dashboard
- [ ] Receivables
- [ ] Detail
- [ ] Payments

---

# GLOBAL RULES

- [ ] No UI logic in domain
- [ ] Deterministic calculations
- [ ] Follow design system
- [ ] No architectural violations

---

END
