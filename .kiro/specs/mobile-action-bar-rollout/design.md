# Design: Mobile Action Bar Rollout

## Overview

Two phases: **(A)** promote the action-bar CSS/JS from the Purchases view into shared files and
refactor Purchases to consume them; **(B)** apply the (now shared) pattern page-by-page. The
pattern, verbatim code, and per-page steps are documented in
`.kiro/docs/mobile-action-bar-pattern.md` — this design defines how to make it shared and the
page-specific decisions.

Guiding principle (unchanged): the action bar is an independent mobile-only element; the desktop
`.topbar` is never re-flowed. Desktop stays byte-for-byte the same.

## Architecture

```
mobile.css  ── .mobile-action-bar / .mab-* (base + @media ≤768px)   ← shared (Phase A)
            ── .mobile-filter-card chrome-strip rule                 ← shared (Phase A)
            ── existing .filter-toggle / .filter-panel behaviour     ← already shared
mobile-nav.js ── filter-toggle (aria-controls-first) [already shared]
              ── generalized More-menu open/close (data-attr driven) ← shared (Phase A)

Each in-scope View/*/Index.cshtml:
  - @{ activeFilterCount = ... }                    (page-specific)
  - .topbar-actions-desktop wrapper on topbar buttons
  - <div class="mobile-action-bar"> … </div>        (markup only; no per-page CSS/JS)
  - filter <section class="… mobile-filter-card">   (shared class)
  - filter panel has id + action-bar Filters button aria-controls it
```

## Phase A — Shared CSS/JS

### A1. Move CSS to `mobile.css`
- Cut the base rules (`.mobile-action-bar { display:none }`, `.mab-more-panel`, `.mab-more-item`,
  hovers) from `Purchase/Index.cshtml` `<style>` and paste near the existing topbar/filter rules
  in `mobile.css` (outside any media query for the base bits).
- Cut the `@media (max-width: 768px)` action-bar rules (`.mobile-action-bar { display:flex }`,
  `.mab-btn/.mab-primary/.mab-ghost/.mab-count/.mab-more`, `.topbar-actions-desktop { display:none }`)
  into the appropriate `@media (max-width: 768px)` block in `mobile.css`.
- Replace the per-page `.purchase-filter-card` rule with a shared `.mobile-filter-card` rule
  (same declarations). In `mobile.css`, the expanded-panel card look keys off
  `.mobile-filter-card .filter-panel.expanded`.
- In plain CSS use `@media`; the `@@media` escaping only applies inside Razor `<style>` blocks.

### A2. Generalize the More-menu JS in `mobile-nav.js`
- Replace the per-page `togglePurchaseMoreMenu(event)` with a shared, delegated handler:
  - Any click on a `.mab-more-toggle` toggles the sibling `.mab-more-panel` (found within the
    same `.mab-more` container) by toggling `.open`, and syncs `aria-expanded`.
  - A document-level click outside `.mab-more` closes any open panel.
- This removes the need for an `onclick` attribute and a per-page `<script>`. Markup keeps only
  the `.mab-more-toggle` button and `.mab-more-panel` (id optional now).

### A3. Refactor Purchases to consume shared versions
- Remove the moved CSS from the view `<style>` (keep only genuinely page-specific rules like the
  origin-breakdown grid and the `#purchaseTable` card layout).
- Remove the `togglePurchaseMoreMenu` script and the `onclick` attribute (delegated handler now).
- Rename `purchase-filter-card` → `mobile-filter-card` on the filter `<section>`.
- Verify Purchases looks and behaves exactly as before (build + visual check).

## Phase B — Per-page application

For each in-scope page, apply Steps 1–5 from the pattern guide (compute count, wrap desktop
buttons, add action-bar markup, tag filter card `mobile-filter-card`, wire Filters `aria-controls`,
remove any in-card `.filter-toggle`). No CSS/JS per page after Phase A.

### Per-page decisions

| Page | Primary (mab-primary) | More items | Filter panel | Wiring needed | Bar type |
|------|----------------------|------------|--------------|---------------|----------|
| Invoice/Index | Create Invoice | exports/secondary | `invoiceFilterPanel` | wired | full |
| Quotation/Index | Create Quotation | secondary | `quotationFilterPanel` | wired | full |
| Customer/Index | Create Customer | secondary | `customerFilterPanel` | wired | full |
| CreditNote/Index | Create Credit Note | secondary | add id + toggle | **needs wiring** | full |
| Supplier/Index | Add Supplier | — | add id + toggle | **needs wiring** | full/reduced |
| Revenue/Receivables | (none — read list) | exports | check panel | verify | reduced |
| Sales/Meetings | Schedule Meeting | secondary | check panel | verify | full |
| Sales/Contacts | Add Contact | — | check panel | verify | full |
| Sales/Tasks | Add Task | — | check panel | verify | full |
| ZReport/Index | New Z-Report | import/export | `zreportFilterPanel` | wired | full |
| SystemLogs/Index | (none) | — | `systemLogsFilterPanel` | wired | Filters-only |
| Audit/Index | (none) | — | `auditFilterPanel` | wired | Filters-only |
| Admin/Index | Invite/Add user? | — | `usersFilterPanel` | wired | full/reduced |
| Attachment/Index | (none) | — | `attachmentFilterPanel` | wired | Filters-only |
| PromoCode/Index | Create Promo | — | `promoCodeFilterPanel` | wired | full |
| PaymentReminder/History | (none) | — | `historyFilterPanel` | wired | Filters-only |
| SalesImport/Records | (none) | import | `salesFilterPanel` | wired | reduced |
| Vat/Index | (create period?) | — | add id + toggle | **needs wiring** | reduced/full |

> The "Primary/None" and exact More items must be confirmed by reading each view's current topbar
> before editing — the table above is the working plan, not gospel. If a page has no create
> primary, the bar renders `[ Filters | More ]` (or just Filters); `.mab-primary` is omitted and
> the Filters/More buttons still sit left-aligned.

### Reduced-bar layout note
When there is no `.mab-primary`, nothing needs to flex to fill; the ghost buttons stay compact and
left-aligned. Optionally add `justify-content:flex-start` — decide during implementation; keep it
consistent across reduced-bar pages.

### Active-filter count
Each page computes its own `activeFilterCount` by summing its applied filter fields (nullable
`.HasValue` or non-empty string = 1 each). This is page-specific and derived from the page's own
view model.

## Error handling / edge cases
- Pages with a filter panel but no `.filter-toggle` (CreditNote, Supplier, Vat, and any others
  found at implementation time) get a unique panel `id`; the Filters button drives it via
  `aria-controls`. The shared `mobile-nav.js` handles the rest.
- Pages with an always-expanded filter (`Statement/Index` uses `.filter-toggle active`) are
  excluded unless explicitly requested.
- If a page's topbar has only one button (just Create) and no filters, it is not a meaningful
  candidate — skip.

## Verification (per page)
1. Build web: 0 `error CS` / 0 `error RZ` (ignore `MSB3021`).
2. Desktop unchanged (topbar buttons intact; action bar not visible).
3. Mobile ≤768px: one action-bar row; desktop buttons hidden; Filters toggles panel + shows count;
   More opens/closes; exports preserve filters.
4. Manual visual check on device/narrowed viewport (cannot be automated here).

## Testing strategy
No automated tests (pure view/CSS/JS presentation). Verification is build-clean + manual visual
per page, tracked in tasks.md. The Purchases page serves as the regression baseline for Phase A.
