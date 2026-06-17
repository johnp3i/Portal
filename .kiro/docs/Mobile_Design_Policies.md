# Mobile Design Policies

## Purpose

This document defines the **design policy layers** that govern how every view in the Portal platform renders on mobile and tablet viewports. Instead of polishing views one-by-one, these policies target component *types* — any view using the matching patterns automatically inherits the mobile treatment.

The goal: every page should feel like a **standalone mobile app**, not a scaled-down desktop site.

---

## Architecture

All mobile policies live in `/css/mobile.css`, loaded after `site.css`. They are organized as numbered layers, each targeting a specific component type.

| Breakpoint | Name | Width |
|------------|------|-------|
| Phone | `max-width: 768px` | ≤768px |
| Tablet | `min-width: 769px` and `max-width: 1100px` | 769–1100px |
| Desktop | `min-width: 1101px` | >1100px (untouched) |

---

## Global Structural Rules

### Fixed Topbar

The mobile topbar uses `position: fixed` (not sticky) to guarantee it remains at the top during scroll. The `overflow-x: clip` on html/body prevents horizontal overflow without breaking fixed/sticky positioning.

| Property | Value |
|----------|-------|
| Position | `fixed`, top: 0, left: 0, right: 0 |
| Height | 56px |
| Background | `rgba(255,255,255,0.95)` + `backdrop-filter: blur(12px)` |
| Z-index | 100 |
| Content offset | `padding-top: 68px` on `.content` |

### Bottom Tab Bar (Phone only)

| Property | Value |
|----------|-------|
| Position | `fixed`, bottom: 0 |
| Height | 60px |
| Z-index | 50 |
| Content offset | `padding-bottom: 76px` on `.content` |

---

## Layer 1: Tables

**Target:** All data tables (invoices, quotations, purchases, audit logs, etc.)

### Shared (≤1100px)

| Rule | Value | Rationale |
|------|-------|-----------|
| Horizontal scroll | `overflow-x: auto; -webkit-overflow-scrolling: touch` | Preserves column structure without breaking layout |
| Minimum table width | `600px` | Prevents columns from collapsing illegibly |
| Header cells | `font-size: 10px; uppercase; sticky top: 0` | Always visible while scrolling vertically |
| Body cells | `padding: 12px; font-size: 13px; white-space: nowrap` | Touch-friendly row height, no wrapping |
| Row interaction | `:active` background highlight | Touch feedback |
| Alternating rows | `nth-child(even)` light background | Readability on small screens |
| Scroll hint | Centered text above table | Indicates horizontal scroll availability |

### Phone (≤768px)

| Rule | Value | Rationale |
|------|-------|-----------|
| Tighter cells | `padding: 10px 8px; font-size: 12px` | Maximize data density |
| Header cells | `padding: 8px; font-size: 9px` | Compact headers |
| Sticky first column | `position: sticky; left: 0; z-index: 1; border-right` | Locks identifier column (reference, name) while user scrolls horizontally |

### Design Intent

Tables should feel like a **native spreadsheet app** — the first column (usually the identifier like invoice number or quotation reference) stays pinned while the user swipes horizontally to see additional data.

---

## Layer 2: Forms

**Target:** All create/edit views (quotations, invoices, customers, purchases, business profile, etc.)

### Shared (≤1100px)

| Rule | Value | Rationale |
|------|-------|-----------|
| Input min-height | `44px` | iOS minimum touch target |
| Input font-size | `16px` | Prevents iOS auto-zoom on focus |
| Input border-radius | `12px` | App-like rounded inputs |
| Focus state | Blue border + `box-shadow: 0 0 0 3px rgba(blue, 0.08)` | Clear focus indicator |
| Labels | `12px; uppercase; letter-spacing: 0.04em; margin-bottom: 6px` | Clear, readable field labels |
| Field spacing | `margin-bottom: 14px` | Breathing room between fields |
| Textarea | `min-height: 100px` | Usable height on mobile |

### Phone (≤768px)

| Rule | Value | Rationale |
|------|-------|-----------|
| All fields | `width: 100%` | Single-column layout, full-width inputs |
| Field spacing | `margin-bottom: 12px` | Tighter but still comfortable |
| Section headings | `font-size: 16px` | Proportional to smaller viewport |

### Tablet (769–1100px)

| Rule | Value | Rationale |
|------|-------|-----------|
| Form grid | `grid-template-columns: 1fr 1fr; gap: 16px` | Two fields side-by-side |
| Full-span fields | `grid-column: 1 / -1` | Description/notes take full width |

### Design Intent

Forms should feel like a **native settings screen** — large tap targets, clear labels, no zooming, single-column on phone, efficient two-column on tablet.

---

## Layer 3: KPI / Stat Cards

**Target:** Dashboard gauges, revenue cards, credit note summaries, quotation stats.

### Shared (≤1100px)

| Rule | Value | Rationale |
|------|-------|-----------|
| Card styling | `border-radius: 14px; border: 1px solid var(--line); padding: 14px 16px` | App-like card appearance |
| Layout | `display: flex; flex-direction: column; gap: 6px` | Clean vertical stack |
| Label | `10px; uppercase; 800 weight; letter-spacing: 0.06em` | Micro-label pattern |
| Value | `Manrope; 20px; 800 weight; letter-spacing: -0.02em` | Bold, prominent number |
| Description | `11px; color: var(--muted)` | Supporting context |
| Accent bars | `border-left-width: 4px; border-radius: 14px` | Color coding preserved |

### Phone (≤768px)

| Rule | Value | Rationale |
|------|-------|-----------|
| Grid | `grid-template-columns: 1fr 1fr` | 2×2 grid fits phone width |
| Value size | `18px` | Slightly smaller for phone |
| Card padding | `12px 14px` | Compact |

### Tablet (769–1100px)

| Rule | Value | Rationale |
|------|-------|-----------|
| Grid | `grid-template-columns: 1fr 1fr` | 2-column, wider cards |
| Value size | `22px` | Room for slightly larger numbers |

### Design Intent

KPI cards should feel like an **Apple Health / banking app summary** — clean cards with bold numbers, color-coded accents, and a 2×2 grid layout that's immediately scannable.

---

## Layer 4: Metric Rows

**Target:** VAT Summary panel, Revenue Insights, any label-value pair lists.

### Shared (≤1100px)

| Rule | Value | Rationale |
|------|-------|-----------|
| Layout | `display: flex; justify-content: space-between; align-items: center` | Label left, value right |
| Row padding | `10px 0` | Comfortable vertical rhythm |
| Dividers | `border-bottom: 1px solid rgba(blue, 0.06)` | Subtle separation |
| Label | `13px; font-weight: 600` | Clear without shouting |
| Value | `Manrope; 15px; 800 weight; text-align: right` | Prominent number |
| Dark surface | `border-bottom-color: rgba(255,255,255,0.1)` | Works on dark cards |

### Phone (≤768px)

| Rule | Value | Rationale |
|------|-------|-----------|
| Label | `12px` | Proportional |
| Value | `14px` | Proportional |

### Design Intent

Metric rows should feel like **iOS Settings rows** — clean key-value pairs with consistent alignment. No wrapping, no clutter.

---

## Layer 5: Chart Containers

**Target:** VAT Liability chart, revenue trend charts, any canvas-based visualizations.

### Shared (≤1100px)

| Rule | Value | Rationale |
|------|-------|-----------|
| Width | `100%` | Fill available space |
| Overflow | `overflow-x: auto` | If chart exceeds container, allow scroll |
| Side-by-side layouts | Stack vertically (`grid-column: 1 / -1`) | Charts need full width on small screens |

### Phone (≤768px)

| Rule | Value | Rationale |
|------|-------|-----------|
| Minimum height | `180px` | Prevent charts from being too short to read |
| Height | `auto !important` | Override fixed inline heights |

### Tablet (769–1100px)

| Rule | Value | Rationale |
|------|-------|-----------|
| Minimum height | `220px` | More vertical space available |

### Design Intent

Charts should remain **readable and interactive** on mobile. When a chart sits beside a summary panel on desktop, it stacks below on mobile to get full viewport width.

---

## Layer 6: Action Button Groups

**Target:** Page header buttons (Edit, Preview, Download PDF, Share, Delete), in-card action rows.

### Shared (≤1100px)

| Rule | Value | Rationale |
|------|-------|-----------|
| Border radius | `12px` | Consistent app-like buttons |

### Phone (≤768px)

| Rule | Value | Rationale |
|------|-------|-----------|
| Layout | `flex-direction: column; gap: 8px` | Stack vertically |
| Button width | `100%` | Full-width tap targets |
| Button padding | `12px 16px; font-size: 13px` | Comfortable touch size |
| Primary CTA | `14px font; 14px radius; box-shadow` | Elevated, prominent |
| Destructive | `background: var(--red); color: #fff` | Clear danger signal |

### Tablet (769–1100px)

| Rule | Value | Rationale |
|------|-------|-----------|
| Layout | `flex-wrap: wrap; gap: 8px` | Buttons wrap naturally |

### Design Intent

On phone, buttons should be **easy one-thumb targets**. The primary action is visually elevated. Destructive actions are clearly red. No cramped side-by-side buttons.

---

## Layer 7: Page Header / Topbar

**Target:** Every page's heading area (eyebrow, title, subtitle, primary CTA).

### Shared (≤1100px)

| Rule | Value | Rationale |
|------|-------|-----------|
| Layout | `flex-direction: column; align-items: flex-start; gap: 10px` | Stack vertically |
| Heading | `24px; line-height: 1.2; letter-spacing: -0.02em` | Readable, not oversized |
| Subtitle | `13px; color: var(--muted)` | Supporting text |
| Eyebrow | `10px; padding: 5px 10px` | Compact badge |

### Phone (≤768px)

| Rule | Value | Rationale |
|------|-------|-----------|
| Heading | `20px` | Proportional to 375px width |
| Subtitle | `12px` | Compact |
| Gap | `6px` | Tighter spacing |
| CTA button | `width: 100%; display: block` | Full-width primary action below title |

### Design Intent

Page headers should be **compact and hierarchical** — eyebrow identifies the section, heading is bold but not oversized, and the primary CTA is immediately accessible as a full-width button below the title.

---

## Implementation Approach

### Step 1: Define Layers in CSS (this document governs)
All layers are implemented as clearly commented sections in `/css/mobile.css`.

### Step 2: Verify Automatic Coverage
Open each view at 375px width in DevTools. Most views will automatically inherit the correct treatment because they use standard classes (`.gauge-row`, `.table-responsive`, `.form-grid`, `.filter-panel`, etc.).

### Step 3: Targeted Per-View Fixes
For the ~20% of views that use inline styles or non-standard markup:
- Add missing wrapper classes (e.g., `.table-responsive` around tables)
- Replace inline styles with class-based equivalents where practical
- Add `.scroll-hint` elements above scrollable tables

### Step 4: Visual QA Checklist
For each view, verify at 375px:
- [ ] Topbar stays fixed at top on scroll
- [ ] Tables scroll horizontally with sticky first column
- [ ] KPI cards show in 2×2 grid
- [ ] Forms have full-width inputs with 44px height
- [ ] Buttons are full-width and stacked
- [ ] No horizontal overflow on the page
- [ ] Bottom tab bar is visible and not overlapping content
- [ ] Charts fill available width

---

## Views Affected

All portal views are covered by these policies:

| Module | Views |
|--------|-------|
| Dashboard | Main dashboard with KPI gauges |
| Quotations | Index, Detail, Create/Edit |
| Invoices | Index, Detail, Create/Edit, Preview |
| Revenue | Dashboard, Receivables, Customer Statement |
| Credit Notes | Index, Detail, Create |
| Purchases | Index, Create/Edit, Bulk Entry |
| Suppliers | Index, Dashboard |
| VAT | Periods Index, Detail |
| Customers | Index, Create/Edit |
| Admin | Audit Logs, System Logs, Users, Module Access |
| Business | Profile, Logo Management |

---

## Layer 8: Dashboard Summary Cards (Mini-Tables)

**Target:** Dashboard "Recent Invoices", "Overdue Invoices", "Recent Payments", "Recent Quotations" — small preview tables that show 3–5 rows.

### Problem

These cards use the same `<table>` markup as full-page index tables, so they inherit the horizontal scroll policy. But on a dashboard card, horizontal scrolling is a bad UX — the user expects to scan key info at a glance without swiping.

### Solution: Compact Card List Layout on Phone

On phone, dashboard mini-tables should **transform into a card list** (one card per row) instead of remaining as a scrollable table.

### Phone (≤768px)

| Rule | Value | Rationale |
|------|-------|-----------|
| Table display | `display: block` (each row becomes a stacked card) | All data visible without scroll |
| Row layout | Vertical stack: primary identifier top, secondary data below | Scannable at a glance |
| Header row | Hidden | Labels become inline with values |
| Card spacing | `padding: 12px; border-bottom: 1px solid var(--line)` | Clear row separation |
| Key fields visible | Invoice #, Customer, Status, Amount all shown | No data cropping |
| Status pill | Inline with amount, right-aligned | Compact status indicator |

### Expected Mobile Rendering

Instead of a cropped table, each invoice row renders as:

```
┌─────────────────────────────────────┐
│ INV-1-00090                    UNPAID│
│ Hatlo Trading Ltd             €172.55│
├─────────────────────────────────────┤
│ INV-1-00089                     PAID │
│ Hatlo Trading Ltd              €71.40│
└─────────────────────────────────────┘
```

### Tablet (769–1100px)

| Rule | Value | Rationale |
|------|-------|-----------|
| Layout | Standard table with horizontal scroll | Tablet has enough width for 3–4 columns |
| Grid | 2-column grid preserved for side-by-side cards | Matches desktop layout |

### CSS Approach

```css
/* Dashboard mini-tables: card list on phone */
@media (max-width: 768px) {
    .dashboard-card table thead { display: none; }
    .dashboard-card table,
    .dashboard-card table tbody,
    .dashboard-card table tr,
    .dashboard-card table td {
        display: block;
        width: 100%;
    }
    .dashboard-card table tr {
        padding: 12px 0;
        border-bottom: 1px solid var(--line);
        display: flex;
        flex-wrap: wrap;
        justify-content: space-between;
        align-items: center;
    }
    .dashboard-card table td {
        padding: 2px 0;
        white-space: normal;
        border: none;
    }
    /* First cell (identifier) — bold, full width or left */
    .dashboard-card table td:first-child {
        font-weight: 700;
        font-size: 13px;
    }
    /* Last cell (amount) — right-aligned */
    .dashboard-card table td:last-child {
        font-weight: 700;
        text-align: right;
    }
}
```

### When to Apply

Add class `.dashboard-card` to the dashboard summary card containers (Recent Invoices, Overdue Invoices, Recent Payments, Recent Quotations). These are the only tables that should convert to card layout — full-page index tables keep horizontal scroll.

### Design Intent

Dashboard mini-tables should feel like a **notification/activity feed** — every row is fully visible without any interaction. The user sees the essential data (who, what, how much, status) in one scan.

---

## Layer 9: Collapsible Filter Panels

**Target:** Invoice Index, Quotation Index, Purchase Index, Customer Index, Audit Logs — any page with a filter section above a data table.

### Problem

On phone, filter panels with 3–5 fields expand to full-width stacked inputs, consuming the entire first screen. The user has to scroll past the filters to see any data. This makes the page feel heavy and unusable.

### Solution: Collapsed by Default + Compact Inline

On phone, filters should be **collapsed behind a toggle button** by default. The user taps "Filters" to expand them. When expanded, inputs use reduced padding and smaller font to minimize vertical space.

### Phone (≤768px)

| Rule | Value | Rationale |
|------|-------|-----------|
| Default state | Collapsed (hidden) | Data is visible immediately on load |
| Toggle button | "Filters" pill button with filter icon, above the table | Easy to discover and tap |
| Expanded layout | Vertical stack, compact inputs | Minimizes vertical space when open |
| Input height | `38px` (reduced from 44px) | Compact but still tappable |
| Input font-size | `14px` | Slightly smaller, still readable |
| Label visibility | Hidden (use placeholder text instead) | Saves vertical space |
| Filter button row | 2 buttons side-by-side (Apply / Clear) at 50% width each | Quick dismiss |
| Animation | `max-height` transition, 200ms | Smooth reveal |

### Expected Mobile Rendering (Collapsed)

```
┌─────────────────────────────────────┐
│ Invoice List                        │
│ Filter, review, and manage...       │
│                                     │
│ [+ Create Invoice]                  │
│ [Filters ▾]  [Export ▾]            │
├─────────────────────────────────────┤
│ ← Scroll horizontally →            │
│ INV #    CUSTOMER    STATUS   TOTAL │
│ ...                                 │
└─────────────────────────────────────┘
```

### Expected Mobile Rendering (Expanded)

```
┌─────────────────────────────────────┐
│ [Filters ▴]  [Export ▾]            │
├─────────────────────────────────────┤
│ [Search by invoice...]              │
│ [All Statuses ▾] [All Financial ▾] │
│ [All Customers ▾] [All Periods ▾]  │
│ [Apply]  [Clear]                    │
├─────────────────────────────────────┤
│ Table data...                       │
└─────────────────────────────────────┘
```

### Tablet (769–1100px)

| Rule | Value | Rationale |
|------|-------|-----------|
| Default state | Always visible (not collapsed) | Tablet has enough height |
| Layout | 2-column grid for fields | Efficient use of width |
| Input height | `40px` | Slightly compact |

### CSS Approach

```css
@media (max-width: 768px) {
    /* Filter panel — collapsed by default */
    .filter-panel {
        max-height: 0;
        overflow: hidden;
        transition: max-height 0.2s ease;
        padding: 0 !important;
        margin: 0 !important;
        border: none !important;
    }
    .filter-panel.expanded {
        max-height: 500px;
        padding: 12px !important;
        margin-bottom: 12px !important;
    }
    /* Compact inputs inside filter */
    .filter-panel input,
    .filter-panel select {
        min-height: 38px;
        padding: 8px 12px;
        font-size: 14px;
    }
    .filter-panel .field label {
        display: none; /* use placeholder instead */
    }
    /* 2-column filter grid when expanded */
    .filter-panel.expanded .field {
        flex: 1 1 calc(50% - 6px) !important;
        min-width: calc(50% - 6px) !important;
    }
    .filter-panel.expanded {
        display: flex !important;
        flex-wrap: wrap !important;
        flex-direction: row !important;
        gap: 8px !important;
    }
    /* Search field — always full width */
    .filter-panel .field:has(input[type="text"]),
    .filter-panel .field:has(input[type="search"]) {
        flex: 1 1 100% !important;
        min-width: 100% !important;
    }
}
```

### JS Requirement

A small addition to `mobile-nav.js`:

```javascript
// Filter panel toggle
document.querySelectorAll('.filter-toggle').forEach(btn => {
    btn.addEventListener('click', () => {
        const panel = btn.closest('.glass, section')
                         .querySelector('.filter-panel');
        if (panel) {
            panel.classList.toggle('expanded');
            btn.classList.toggle('active');
        }
    });
});
```

### HTML Addition Required

Add a toggle button before or above the filter panel in index views:

```html
<button class="filter-toggle btn btn-secondary" style="display:none;">
    <svg>...</svg> Filters
</button>
```

Shown via CSS on phone viewport only.

### Design Intent

Filters are a **tool, not the content**. On phone, data should be visible immediately. Filters appear on demand, are compact (2-column grid), and dismiss quickly.

---

## Layer 10: Compact Action Button Row

**Target:** Page header buttons on index pages (Create Invoice, Export CSV, Export PDF, etc.)

### Problem

On phone, action buttons stack vertically at full-width. A page with 3 buttons (Create, Export CSV, Export PDF) wastes 3 full rows of space before the user reaches the data.

### Solution: Compact Horizontal Row with Overflow

| Strategy | Description |
|----------|-------------|
| Primary action | Full-width, prominent (Create Invoice) |
| Secondary actions | Grouped into a compact row or overflow menu |

### Phone (≤768px)

| Rule | Value | Rationale |
|------|-------|-----------|
| Primary CTA | Full-width, prominent style | Easy one-thumb tap |
| Secondary buttons | Inline row, compact padding (`8px 12px`), `font-size: 12px` | Fit 2–3 buttons on one row |
| Button row gap | `8px` | Tight but tappable |
| Layout | Primary full-width on top, secondaries in a row below | Clear hierarchy |

### Expected Mobile Rendering

```
┌─────────────────────────────────────┐
│ Invoice List                        │
│ [         + Create Invoice         ]│ ← full-width primary
│ [Export CSV] [Export PDF] [Filters ▾]│ ← compact row
└─────────────────────────────────────┘
```

### CSS Approach

```css
@media (max-width: 768px) {
    /* Top action bar — primary full-width, rest inline */
    .topbar [style*="display:flex"][style*="gap"] {
        flex-direction: column !important;
        gap: 8px !important;
    }
    /* Primary CTA — full width */
    .topbar .btn-primary,
    .topbar .btn-green {
        width: 100%;
        order: -1; /* always first */
    }
    /* Secondary buttons — compact inline row */
    .topbar .btn-secondary,
    .topbar .btn:not(.btn-primary):not(.btn-green):not(.btn-danger) {
        width: auto;
        display: inline-flex;
        padding: 8px 14px;
        font-size: 12px;
    }
}
```

### Design Intent

The primary action dominates. Secondary actions are compact and grouped — never blocking access to the actual page content.

---

## Layer 11: Index Page Tables (Full Data Visibility)

**Target:** Invoice Index, Quotation Index, Purchase Index — full-page data tables with many rows.

### Problem

On phone with horizontal scroll, the user only sees the first 2 columns (Invoice # and Customer). Status, Amount, Date are hidden and require swiping. Unlike dashboard mini-tables, these are the main content of the page.

### Solution: Responsive Row Cards on Phone

On phone, full-page index tables should transform into **row cards** where each record displays as a compact card showing all essential fields without horizontal scroll.

### Phone (≤768px)

| Rule | Value | Rationale |
|------|-------|-----------|
| Table header | Hidden | Labels become inline within each card |
| Each row | Rendered as a card with 2-line layout | All data visible |
| Line 1 | Reference (bold, left) + Status pill (right) | Identifier + state |
| Line 2 | Customer (left) + Amount (right, bold) | Who + how much |
| Row separation | `border-bottom: 1px solid var(--line)` + `padding: 12px 0` | Clean separation |
| Tap action | Entire row tappable (link wrap) | Easy navigation to detail |

### Expected Mobile Rendering

```
┌─────────────────────────────────────┐
│ INV-1-00090              [UNPAID]   │
│ Hatlo Trading Ltd           €172.55 │
├─────────────────────────────────────┤
│ INV-1-00089                [PAID]   │
│ Hatlo Trading Ltd            €71.40 │
├─────────────────────────────────────┤
│ INV-1-00085              [ISSUED]   │
│ Pancyprian Fed. of Labor    €53.55  │
└─────────────────────────────────────┘
```

### Tablet (769–1100px)

| Rule | Value | Rationale |
|------|-------|-----------|
| Layout | Standard table with horizontal scroll | Tablet fits 4–5 columns |
| Scroll hint | Visible | Indicates more columns available |

### CSS Approach

```css
@media (max-width: 768px) {
    /* Index page tables — card list on phone */
    .index-table thead { display: none; }
    .index-table,
    .index-table tbody,
    .index-table tr,
    .index-table td {
        display: block;
        width: 100%;
    }
    .index-table tr {
        padding: 12px 0;
        border-bottom: 1px solid var(--line);
        display: grid;
        grid-template-columns: 1fr auto;
        grid-template-rows: auto auto;
        gap: 2px 8px;
        align-items: center;
    }
    /* Cell 1: Reference — top left, bold */
    .index-table td:nth-child(1) {
        grid-row: 1;
        grid-column: 1;
        font-weight: 700;
        font-size: 13px;
    }
    /* Cell 2: Customer — bottom left */
    .index-table td:nth-child(2) {
        grid-row: 2;
        grid-column: 1;
        font-size: 12px;
        color: var(--muted);
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
    }
    /* Status cell — top right */
    .index-table td:has(.pill) {
        grid-row: 1;
        grid-column: 2;
        text-align: right;
    }
    /* Amount cell — bottom right */
    .index-table td:last-child {
        grid-row: 2;
        grid-column: 2;
        font-weight: 700;
        text-align: right;
        font-size: 13px;
    }
    /* Hide non-essential columns on phone (date, actions) */
    .index-table td.hide-phone {
        display: none;
    }
}
```

### HTML Requirement

Add class `.index-table` to the main data table on index pages. Optionally add `.hide-phone` to non-essential columns (date columns, action links that can be accessed by tapping the row).

### Design Intent

Index page tables should feel like an **email inbox or banking transaction list** — each entry shows who, what, how much, and status in a compact 2-line card format. The user taps a row to see full detail.

---

## What Is NOT Covered by Policies

These require individual per-view attention:
- Invoice/Proposal **Preview** and **Share** pages (standalone HTML, not using layout)
- PDF generation templates (not rendered on mobile)
- Modal dialogs and SweetAlert2 popups (already responsive by default)
- Login/Registration pages (separate layout)
