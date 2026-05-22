# Layout & Spacing Standards

## Card Sections (`.glass.card-pad`)

All page content is wrapped in `<section class="glass card-pad">` cards. These are the primary content containers.

### Spacing Between Cards

| Context | Margin | Example |
|---------|--------|---------|
| Filter card → Data table card | `margin-bottom: 22px` | Purchase list, VAT periods list |
| Stacked content cards | `margin-top: 24px` | VAT detail breakdown → filing status |
| Card after topbar | No explicit margin (handled by global layout) | All pages |

### Standard Card Pattern: Filter + Table

When a page has a filter panel above a data table, follow this structure:

```html
<!-- Filter Panel -->
<section class="glass card-pad" style="margin-bottom:22px;">
    <!-- filter controls -->
</section>

<!-- Data Table -->
<section class="glass card-pad">
    <table>...</table>
    <!-- pagination -->
</section>
```

## Page Structure

Every page follows this order:

1. **Topbar** — `.topbar` with eyebrow label, heading (42px), and muted description
2. **Filter section** (optional) — `.glass.card-pad` with `margin-bottom:22px`
3. **Main content** — `.glass.card-pad` containing the table or primary content
4. **Secondary sections** (optional) — additional `.glass.card-pad` with `margin-top:24px`

## Filter Panel Layout

Filters use flexbox with consistent spacing:

```html
<div style="display:flex;gap:14px;align-items:flex-end;flex-wrap:wrap;">
    <div class="field" style="min-width:180px;">
        <label>Label</label>
        <select>...</select>
    </div>
    <!-- more fields -->
    <div style="padding-bottom:2px;">
        <button class="btn btn-primary">Filter</button>
        <button class="btn btn-secondary">Clear</button>
    </div>
</div>
```

- Gap between filter fields: `14px`
- Minimum field width: `180px`
- Buttons aligned to bottom with `padding-bottom:2px`

## Pagination

Pagination sits below the table within the same card:

```html
<div style="display:flex;justify-content:space-between;align-items:center;margin-top:18px;flex-wrap:wrap;gap:12px;">
    <div id="paginationInfo">Showing 1-10 of 25</div>
    <div id="paginationControls"><!-- page buttons --></div>
</div>
```

- Margin above pagination: `18px`
- Info text: `font-size:14px; color:#5a6a7a`
- Page buttons: `padding:6px 12px; border-radius:8px; font-size:13px; font-weight:700`

## Empty States

When no data exists, show within the main card:

```html
<div class="empty-state">
    <p>No items found.</p>
    <!-- optional action button -->
</div>
```

## Consistency Checklist

When creating a new list/table view, verify:

- [ ] Filter card uses `margin-bottom:22px`
- [ ] Table is inside its own `.glass.card-pad` (no inline margin)
- [ ] Pagination uses `margin-top:18px` within the table card
- [ ] Filter fields use `min-width:180px` and `gap:14px`
- [ ] Buttons in filter row have `padding-bottom:2px` wrapper
- [ ] Empty state is inside the main content card
