# Requirements: Mobile Action Bar Rollout

## Introduction

The Purchases list page has a mobile-only "action bar" (Variant A) that replaces the stacked
full-width topbar buttons on phones with a single compact row:

```
[ + Create (flex) | Filters (count) | More ⋯ ]
```

This spec generalizes that pattern into shared, reusable CSS/JS and rolls it out to the other
list/index pages, so mobile users get a consistent, native-app-like toolbar and reach the data
faster. The reference implementation and full build instructions already exist:

- Pattern guide: `.kiro/docs/mobile-action-bar-pattern.md`
- Visual mockup: `.kiro/docs/mockups/mobile-list-action-bar.html` (Variant A)
- Reference page: `Portal.Web/Views/Purchase/Index.cshtml`

**Hard constraint carried from the reference work:** the desktop layout must not change. The
action bar is a separate mobile-only element rendered below the `.topbar`; the desktop `.topbar`
keeps its own buttons. No `!important` overrides or specificity hacks on `.topbar` itself.

## Glossary

- **Action bar** — the `.mobile-action-bar` element with `.mab-*` children.
- **List page** — an index page whose primary content is a table/list with a `.topbar` and
  (usually) a filter panel.
- **Shared filter system** — the existing `.filter-toggle` button + `.filter-panel` container +
  `mobile-nav.js` toggle. On mobile the panel is collapsed by default and expands via `.expanded`.
- **Wired** — a filter panel that has a `.filter-toggle` with `aria-controls="<panelId>"` driving it.

## Requirement 1: Shared, reusable styles and behaviour

**User Story:** As a developer, I want the action-bar CSS and JS to live in shared files, so that
each page only needs markup and not copied styles/scripts.

### Acceptance Criteria
1. THE `.mobile-action-bar`, `.mab-*`, and `.topbar-actions-desktop` CSS SHALL live in
   `Portal.Web/wwwroot/css/mobile.css` (base rules + a `@media (max-width: 768px)` block).
2. THE filter-card chrome-stripping rule SHALL use a shared class (e.g. `mobile-filter-card`)
   rather than a per-page class.
3. THE More-menu open/close behaviour SHALL be generalized (e.g. in `mobile-nav.js`), driven by a
   data attribute or convention, so pages do not each define bespoke toggle JS.
4. WHEN the shared CSS/JS is in place, THE Purchases page SHALL be refactored to consume the
   shared versions and SHALL remain visually and behaviourally identical.
5. THE shared implementation SHALL NOT alter any desktop (>1100px) rendering.

## Requirement 2: Per-page action bar on eligible list pages

**User Story:** As a mobile user, I want a compact action bar on each list page, so that I can
create, filter, and access secondary actions without a wall of stacked buttons.

### Acceptance Criteria
1. FOR each in-scope list page (see Requirement 5), THE page SHALL render a `.mobile-action-bar`
   below the `.topbar`, hidden on desktop and shown at `≤768px`.
2. WHERE the page has a primary create/add action, THE action bar SHALL show it as the flexed
   `.mab-primary` button.
3. WHERE the page has secondary/export actions, THE action bar SHALL place them in the More ⋯
   dropdown (`.mab-more-panel`).
4. WHERE the page has a filter panel, THE action bar SHALL include a Filters button that toggles
   that panel and displays an active-filter count badge when one or more filters are applied.
5. ON mobile, THE desktop topbar action buttons SHALL be hidden (`.topbar-actions-desktop`).
6. Export/secondary links in the action bar SHALL preserve the current filter query-string,
   matching their desktop counterparts.

## Requirement 3: Filter toggle wiring for unwired panels

**User Story:** As a mobile user, I want the Filters button to work on every list page, even those
whose filter panel was not previously collapsible.

### Acceptance Criteria
1. FOR in-scope pages whose filter panel is NOT currently wired (no `.filter-toggle` /
   `aria-controls`), THE panel SHALL be given a unique `id` and the action-bar Filters button SHALL
   reference it via `aria-controls`.
2. THE existing `mobile-nav.js` behaviour (resolve panel by `aria-controls` first, else nearest
   card) SHALL be relied upon; no bespoke per-page filter JS SHALL be added.
3. WHERE a page previously had an in-card `.filter-toggle`, it SHALL be removed once the action-bar
   Filters button replaces it (no duplicate toggles targeting the same panel).

## Requirement 4: No regressions

### Acceptance Criteria
1. THE web project SHALL build with 0 `error CS` and 0 `error RZ` after each page change.
   (An `MSB3021` DLL/exe copy-lock is environmental and does not count as a build failure.)
2. Desktop rendering of every touched page SHALL remain unchanged.
3. Existing filter behaviour (apply, clear, query-string persistence, pagination) SHALL be
   unaffected on both desktop and mobile.
4. Pages NOT in scope SHALL remain untouched.

## Requirement 5: Scope — which pages comply

Pages are classified as **In scope (full bar)**, **In scope (reduced bar)**, or **Excluded**.

### 5.1 In scope — full bar (primary + Filters + More)
Wired filter panel already present:
- Invoice (`Invoice/Index`)
- Quotation (`Quotation/Index`)
- Customer (`Customer/Index`)
- Purchase (`Purchase/Index`) — **already done (reference)**

Unwired filter panel (needs Requirement 3 wiring):
- Supplier (`Supplier/Index`)
- CreditNote (`CreditNote/Index`)

Sales module list pages (evaluate primary action per page):
- Sales Meetings (`Sales/Meetings`) — already partially restructured; align to the pattern
- Sales Contacts (`Sales/Contacts`)
- Sales Tasks (`Sales/Tasks`)

Revenue:
- Receivables (`Revenue/Receivables`)

### 5.2 In scope — reduced bar (Filters + More, or Filters only; no create primary)
Read-only / admin lists where there is no user "create" primary:
- ZReport (`ZReport/Index`) — has create ("New Z-Report"); may be full bar — confirm at design time
- SystemLogs (`SystemLogs/Index`)
- Audit (`Audit/Index`)
- Admin Users (`Admin/Index`)
- Attachment (`Attachment/Index`)
- PromoCode (`PromoCode/Index`)
- PaymentReminder History (`PaymentReminder/History`)
- SalesImport Records (`SalesImport/Records`)
- Vat Periods (`Vat/Index`) — unwired panel

### 5.3 Excluded (not list pages / not applicable)
- Detail pages: `Revenue/InvoiceDetail`, `Sales/LeadDetail`, `Sales/ContactDetail`,
  `Supplier/Dashboard`, `Vat/Detail`, `Vat/PeriodReport`, `Receipt/Detail`.
- Dashboards: `Revenue/Dashboard`, `Sales/Pipeline`, `Sales/Insights`, `Sales/Activity`.
- Wizards / gates / errors: `SetupWizard/Wizard`, all `Shared/*` gate & error pages.
- `Statement/Index` (filter is always-expanded by design; single-purpose form) — excluded unless
  requested.
- `RecurringExpense/*`, `Receipt/Index`, `Sales/Products/Templates/Team` — evaluate at design time;
  default to excluded unless they are genuine filtered list pages with multiple topbar actions.

### 5.4 Rollout ordering
1. Requirement 1 (shared CSS/JS + Purchases refactor) — **must be first**.
2. High-traffic full-bar pages: Invoice, Quotation, Customer, CreditNote, Supplier, Receivables,
   Sales Meetings.
3. Reduced-bar / admin pages, as time allows.

Each page is applied, built, and visually verified independently before moving on.

## Out of scope
- Any desktop redesign.
- Changing filter fields, controller filter logic, or export contents.
- The mobile card-list table layout (separate concern already handled per page as needed).
- Automated visual/responsive tests (verification is manual on a device / narrowed viewport).
