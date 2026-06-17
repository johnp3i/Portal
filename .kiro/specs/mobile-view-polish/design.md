# Design: Mobile View Polish

## Overview

This design describes the technical approach for applying mobile design policies (Layers 1–11) to every view in the Portal platform. The implementation is purely additive — adding CSS classes, `data-mobile` attributes, and minimal HTML elements (filter toggle buttons) to existing views. No business logic, controllers, or database changes are required.

## Architecture

### Design Policy Activation Pattern

Each design policy layer is already implemented in `/css/mobile.css` and activates via specific CSS classes or element patterns:

| Layer | Activation | Class/Pattern |
|-------|-----------|---------------|
| 1. Tables | Auto (all `<table>` in `.table-responsive`) | `.table-responsive` wrapper |
| 2. Forms | Auto (all `input`, `select`, `textarea`) | Standard form elements |
| 3. KPI Cards | Auto (`.gauge-row`, `.gauge-item`) | `.gauge-row` container |
| 4. Metric Rows | Auto (`.metric-row`) | `.metric-row` elements |
| 5. Charts | Auto (`canvas`, `.chart-container`) | Standard chart elements |
| 6. Button Groups | Auto (`.topbar [style*="display:flex"]`) | Inline flex containers in `.topbar` |
| 7. Page Header | Auto (`.topbar`, `.topbar-heading`) | Standard topbar classes |
| 8. Dashboard Mini-Tables | **Manual** | `.dashboard-card` on section |
| 9. Collapsible Filters | **Manual** | `.filter-toggle` button + `.filter-panel` |
| 10. Compact Button Row | **Manual** | `.topbar-actions` + `.topbar-actions-secondary` |
| 11. Index Table Cards | **Manual** | `.index-table` on wrapper + `data-mobile` on `<td>` |

**Auto layers** (1–7) apply automatically to any view using standard classes. **Manual layers** (8–11) require explicit class additions per view.

### Per-View Audit Strategy

For each view, the implementation follows this decision tree:

```
1. Read the view HTML
2. Does it use standard classes (.gauge-row, .table-responsive, .form-grid, .filter-panel, .topbar)?
   → YES: Auto layers already apply. Verify at 375px. Done.
   → NO: Identify which inline styles or custom markup blocks the policy.
3. Can we add a policy class to activate the layer?
   → YES: Add the class (e.g., .dashboard-card, .index-table, .topbar-actions)
   → NO: Add a targeted CSS override in mobile.css for that specific view.
4. Does the table need card layout on phone?
   → YES: Add .index-table + data-mobile attributes
   → NO: Ensure .table-responsive wrapper with .scroll-hint exists
5. Does the page have filters?
   → YES: Add .filter-toggle button + ensure .filter-panel class is present
   → NO: Skip filter toggle
```

## Component Design

### 1. Dashboard View

**Current State:** Gauge row uses `.gauge-row` (auto). Charts use `canvas` in inline-height wrappers. Mini-tables have `.dashboard-card` (already applied). Quick actions use inline styles on `<a>` tags inside `.filter-panel`.

**Changes Required:**
- Verify gauge row progress bars (inline `width` styles) don't overflow on 2x2 grid
- Charts: CSS Layer 5 overrides inline `height:220px` with `height: auto !important` — verify this works
- Quick actions: Already handled by existing phone CSS for `.filter-panel a[style*="border-radius:24px"]`
- Stats strip: Already restyled in latest mobile.css update

**Risk:** Inline `style="height:220px"` on chart containers might resist the CSS override in some browsers. Mitigation: `!important` is already used.

### 2. Invoice Index

**Current State:** Already has `.index-table`, `data-mobile` attributes, `.filter-toggle` button, `.topbar-actions` structure.

**Changes Required:** Verification only — all manual layers already applied.

### 3. Quotation Index

**Current State:** Already has `.index-table`, `data-mobile` attributes, `.filter-toggle` button, `.topbar-actions` structure.

**Changes Required:** Verification only — all manual layers already applied.

### 4. Invoice Detail

**Current State:** Action buttons in a `div style="display:flex;gap:12px;flex-wrap:wrap"` inside `.topbar`. Meta fields in flex containers with `gap:32px`. Line items table in `.table-responsive`.

**Changes Required:**
- Add `.topbar-actions` class to the button container div
- Layer 6 (auto) will stack buttons on phone via the inline-style flex selector
- Meta fields: Layer 7 collapses `.topbar` to `flex-direction: column`; internal flex containers need `flex-wrap: wrap` override or explicit stacking
- Line items: `.table-responsive` already provides horizontal scroll
- Add targeted CSS for `[style*="gap:32px"]` to stack vertically on phone

### 5. Invoice Create/Edit

**Current State:** Form uses `div.field` elements in flex/grid containers with inline styles. Line item rows use custom grid.

**Changes Required:**
- Verify Layer 2 (Forms) applies correctly to all inputs
- Line item grid: Add CSS to collapse `.line-item` to single column on phone (already in site.css `@media max-width:1100px`)
- Verify "Add Line Item" button inherits full-width from Layer 6
- Confirm `input[type="date"]` renders native date picker on mobile

### 6. Quotation Detail

**Current State:** Similar structure to Invoice Detail — action buttons in topbar flex, meta in flex containers, sections with line items.

**Changes Required:**
- Add `.topbar-actions` class to button container
- Same meta field stacking approach as Invoice Detail
- Section breakdown: cards already use `.glass .card-pad` which gets responsive treatment

### 7. Quotation Create/Edit

**Current State:** Similar to Invoice Create/Edit with additional section management (Add Section button).

**Changes Required:**
- Same form collapse approach as Invoice Create/Edit
- Catalog search modal: SweetAlert2 modals are already responsive
- Verify "Add Section" button gets full-width treatment

### 8. Revenue Dashboard

**Current State:** KPI cards use inline flex with `border-left` colour accents. Action buttons are `<a>` elements with inline styles. No standard `.gauge-row` class.

**Changes Required:**
- Add `.gauge-row` or custom CSS targeting the KPI card container for 2-column grid
- Verify Layer 3 accent bar rule applies to `[style*="border-left"]`
- Action buttons: Add `.topbar-actions` or ensure they inherit full-width from context
- If receivables table exists, add `.table-responsive` wrapper

### 9. Customer Views

**Current State:** Customer Index likely uses a table without `.index-table`. Create/Edit uses form fields.

**Changes Required:**
- Customer Index: Add `.index-table` + `data-mobile` attributes OR ensure `.table-responsive` with scroll
- Customer Create/Edit: Verify Layer 2 (Forms) applies — fields should auto-collapse
- Add `.filter-toggle` if filter panel exists

### 10. Purchase Views

**Current State:** Purchase Index has filter panel and table. Bulk Entry has a grid of inputs.

**Changes Required:**
- Purchase Index: Add `.index-table` or ensure `.table-responsive` wrapper
- Add `.filter-toggle` button if filter panel exists
- Bulk Entry: Ensure table wrapper uses `.table-responsive` with momentum scroll
- Create/Edit: Verify Layer 2 applies

### 11. Supplier Views

**Current State:** Supplier Dashboard has KPI cards and purchase history table. Index has supplier list.

**Changes Required:**
- Verify KPI cards use `.gauge-row` or add targeted CSS
- Ensure tables use `.table-responsive` with `.scroll-hint`
- Supplier Index: Add `.index-table` if applicable

### 12. VAT Views

**Current State:** Periods Index has a table. Detail has meta grid and breakdown tables. Chart uses canvas.

**Changes Required:**
- Periods Index: Add `.index-table` or `.table-responsive` wrapper
- Detail meta grid: Verify grid collapse from Layer 3/7
- Breakdown tables: Ensure `.table-responsive` wrappers exist
- Chart: Layer 5 auto-applies

### 13. Credit Note Views

**Current State:** Follows similar patterns to Invoice views.

**Changes Required:**
- Index: Add `.index-table` + `data-mobile` attributes
- Detail: Add `.topbar-actions` to button container
- Create: Verify Layer 2 (Forms) applies
- Summary cards: Verify `.gauge-row` or equivalent grid class

### 14. Admin Views

**Current State:** Tables with filter controls. Standard `.glass .card-pad` sections.

**Changes Required:**
- Ensure all tables have `.table-responsive` wrappers with `.scroll-hint`
- Ensure filter controls have `.filter-panel` class (auto-stacking from Layer 9)
- Add `.filter-toggle` if panels are large
- Action buttons: Verify full-width from Layer 6

### 15. Business Profile

**Current State:** Form fields in grid/flex containers.

**Changes Required:**
- Verify Layer 2 (Forms) collapses to single column on phone
- Logo upload section: Ensure it's not constrained by inline `width`

### 16. Customer Statement

**Current State:** Filter panel + statement table.

**Changes Required:**
- Ensure `.filter-panel` class present (auto-stacking)
- Add `.filter-toggle` if not present
- Ensure statement table has `.table-responsive` wrapper with `.scroll-hint`
- Summary totals: Verify they render full-width (likely already do via card container)

## CSS Changes Summary

Most views will NOT need new CSS rules. The existing Layers 1–11 cover the patterns. Targeted additions may include:

```css
/* Invoice/Quotation Detail — meta field stacking */
@media (max-width: 768px) {
    .topbar [style*="gap:32px"],
    .topbar [style*="gap: 32px"] {
        flex-direction: column;
        gap: 12px !important;
    }
}
```

## HTML Changes Summary

| Change Type | Where | What |
|-------------|-------|------|
| Add `.topbar-actions` class | Invoice Detail, Quotation Detail, Credit Note Detail | Button container div |
| Add `.index-table` + `data-mobile` | Customer Index, Purchase Index, Credit Note Index, Supplier Index, VAT Periods Index | Main data table |
| Add `.filter-toggle` button | Purchase Index, Customer Index, Admin views | Before `.filter-panel` |
| Add `.table-responsive` wrapper | Any table without it | Around `<table>` |
| Add `.scroll-hint` | Above any newly wrapped `.table-responsive` | Before the wrapper div |
| Add `.dashboard-card` | Already done | Dashboard mini-tables |

## JavaScript Changes

None required beyond the existing filter toggle handler in `mobile-nav.js` (already handles all `.filter-toggle` buttons dynamically via `querySelectorAll`).

## Testing Strategy

Each view is tested manually at:
- **375px** (iPhone SE) — Phone viewport
- **810px** (iPad) — Tablet viewport
- **1200px** — Desktop (verify no regression)

Verification checklist per view:
1. No horizontal overflow (check body width = viewport width)
2. Fixed topbar stays at top during scroll
3. Tables either use card layout or horizontal scroll with scroll-hint
4. Forms collapse to single-column with 44px inputs
5. Buttons stack vertically and are full-width
6. KPI cards render as 2-column grid
7. Charts fill available width
8. Filter toggle works (if applicable)
9. Bottom tab bar visible and not overlapping content

## No Changes Required

- Controllers (no business logic changes)
- Database / migrations
- Services / repositories
- JavaScript business logic
- Desktop layout (all changes scoped to <=1100px media queries)
