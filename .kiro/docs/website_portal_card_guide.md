# Website Platform Card: Business Management Platform

## Context

This document guides the 3 Inventors website agent on how to create the fourth platform card in the "Operational Platforms" section on the 3inventors.com homepage. The new card represents the **Business Management Platform** (the Portal product).

---

## Section Structure (Existing Pattern)

The "Operational Platforms" section currently displays three cards in a row. Each card follows this structure:

1. **Logo** — product logo image at the top
2. **Category label** — uppercase small text (e.g., "WORKFORCE INTELLIGENCE")
3. **Description** — 2-3 sentences explaining what the platform does and the value it delivers
4. **Keyword pills** — 2-3 short tags summarising core capability areas
5. **Visit link** — link to the product's dedicated landing page with arrow icon

All cards share consistent spacing, border-radius, and the blue top border accent.

---

## New Card Content

### Logo

**File:** `portal.logo`

### Category Label

```
BUSINESS MANAGEMENT PLATFORM
```

### Description

```
Gives businesses a structured environment to quote, invoice, track revenue, and manage operations — with clarity, control, and financial visibility built in from day one.
```

### Keyword Pills

```
Quotations    Invoicing    Revenue Control
```

### Visit Link

```
Visit portal.3inventors.com →
```

**URL:** `https://portal.3inventors.com`

---

## Design Notes

- The card must match the existing three cards in layout, spacing, typography, and interaction style.
- The top border accent colour should remain the same blue used across all platform cards.
- The logo should be rendered at the same dimensions as the other platform logos (WorkforcePI, EOMFA, Mychair).
- The keyword pills follow the same rounded outline style as existing pills (light border, no fill, small text).
- The section heading "Three platforms. One intelligence ecosystem." should be updated to **"Four platforms. One intelligence ecosystem."**
- The introductory paragraph ("Every product is part of a larger direction...") remains unchanged.

---

## Positioning Rationale

The Portal's tagline on its login page is **"Simple. Clear. Reliable."** — this reflects the product's philosophy: operational seriousness without complexity.

The description focuses on what a visitor needs to understand at a glance:
- **What it is:** A business management environment (not just invoicing, not just accounting)
- **What it covers:** Quotations, invoicing, revenue tracking, and operations
- **What makes it different:** Clarity and control are built into the structure, not bolted on

This aligns with the other cards which each state: what the platform does + the operational outcome it produces.

---

## Consistency Checklist

- [ ] Card uses the same HTML structure as the existing three platform cards
- [ ] Logo dimensions match other platform logos
- [ ] Category label is uppercase, same font-size and letter-spacing as siblings
- [ ] Description is 1-3 sentences, same font-size and line-height as siblings
- [ ] Keyword pills use same styling (border, padding, font-size, border-radius)
- [ ] Visit link uses same arrow icon and colour as siblings
- [ ] Section heading updated from "Three" to "Four"
- [ ] Card grid layout accommodates 4 cards (verify responsive behaviour)

---

## Responsive Consideration

With four cards instead of three, the layout may need adjustment:
- **Desktop (>1200px):** 4 cards in a row, or 2x2 grid depending on card width
- **Tablet (768-1200px):** 2x2 grid
- **Mobile (<768px):** Single column stack

The website agent should verify the chosen approach maintains readability and visual balance.

---

## Landing Page Features Section Update

The current features section on portal.3inventors.com uses a 4-card grid that only represents Foundation-level capabilities:

| Current Card | Scope |
|---|---|
| Quotations & Invoices | Foundation |
| Revenue Control | Foundation |
| VAT Submissions | Foundation |
| Purchases & Products | Foundation |

This makes the platform look like a basic invoicing/VAT tool. The Professional and Enterprise differentiators (automation, intelligence, sales pipeline, payroll) are invisible until the visitor scrolls to the pricing section.

### Updated Features Section — 6 Cards

Replace the current 4-card grid with 6 cards that represent the full platform across tiers. Use a subtle tier indicator on each card (e.g., a small label or the card being grouped under a tier heading).

#### Card 1: Quotations & Invoicing
**Icon:** Document/paper icon  
**Category:** Sales  
**Title:** Quotations & Invoicing  
**Description:** Create professional quotations, convert them to invoices in one click, issue credit notes, and share documents through secure links — with full PDF generation and digital signatures.  
**Tier:** Foundation

#### Card 2: Revenue & Payments
**Icon:** Currency/euro icon  
**Category:** Finance  
**Title:** Revenue & Payments  
**Description:** Record payments, allocate credit, track outstanding balances, generate payment receipts, and give customers the ability to pay by card via Stripe — all from one clear revenue dashboard.  
**Tier:** Foundation + Professional

#### Card 3: VAT & Compliance
**Icon:** Clipboard/checkmark icon  
**Category:** Compliance  
**Title:** VAT & Compliance  
**Description:** Organise VAT periods automatically, review input and output VAT, track business filings and regulatory deadlines, and keep every submission structured and audit-ready.  
**Tier:** Foundation + Professional

#### Card 4: Automation & Intelligence
**Icon:** Lightning bolt or gear icon  
**Category:** Automation  
**Title:** Automation & Intelligence  
**Description:** Automated payment reminders that escalate in tone, cash flow forecasting at 30/60/90 days, profit & loss summaries, expense insights, and payment schedules with instalment tracking — the platform works while you focus on selling.  
**Tier:** Professional

#### Card 5: Sales Pipeline
**Icon:** Funnel or pipeline icon  
**Category:** Growth  
**Title:** Sales Pipeline  
**Description:** Track leads through a visual pipeline, schedule meetings, assign follow-up tasks, manage contacts, log responses, and monitor your team's sales activity — from first contact to closed deal.  
**Tier:** Professional

#### Card 6: Payroll & Team
**Icon:** People/team icon  
**Category:** Scale  
**Title:** Payroll & Team  
**Description:** Generate payslips with PAYE calculations, manage employer contributions, produce compliance reports, and run your growing team with unlimited users, granular permissions, and full audit capabilities.  
**Tier:** Enterprise

### Layout

- **Desktop:** 3 columns × 2 rows (matching the current visual rhythm)
- **Tablet:** 2 columns × 3 rows
- **Mobile:** Single column stack

### Tier Indicator Style

Each card should have a small, non-intrusive tier label below the description or in the top-right corner:

- Foundation cards: no label needed (baseline expectation)
- Professional cards: small pill "Professional" in the platform's accent blue
- Enterprise cards: small pill "Enterprise" in a darker shade or with a subtle star/shield icon

This helps visitors immediately understand the platform's depth without needing to reach the pricing section.

### Features That Must Be Represented (Currently Missing)

These items from the Subscription Tier Model are absent from the current landing page features section and must appear in the updated cards or their descriptions:

| Feature | Tier | Where It Goes |
|---|---|---|
| Payment Receipts & Digital Signatures | Foundation | Card 2 (Revenue & Payments) |
| Auto-generated Payment Links (in emails & shared pages) | Professional | Card 2 (Revenue & Payments) |
| Business Applications Tracker (compliance filings) | Professional | Card 3 (VAT & Compliance) |
| Sales Pipeline / Opportunities (full module) | Professional | Card 5 (Sales Pipeline) — new card |
| Payroll with annual summaries & P&L integration | Enterprise | Card 6 (Payroll & Team) — new card |
| Custom branding on client-facing pages | Enterprise | Card 6 (Payroll & Team) or pricing card |
| Purchase & Sales Invoice Import (CSV/Excel) | Professional | Can remain in pricing card or added to Card 3 |

### Tone Guidance

Match the existing landing page tone:
- Operational, calm, structured
- Focus on outcomes ("the platform works while you focus on selling")
- No hype, no superlatives
- Short sentences, clear value statements
- Show depth without overwhelming — each card is 1-2 sentences max
