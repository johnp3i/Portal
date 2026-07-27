# Detail Page Action Bar — Layout Pattern

## Principle

Detail pages (Quotation Detail, Invoice Detail, etc.) separate navigation from actions using a two-part layout:

1. **Topbar** — contains only the page identity (eyebrow, heading, subtitle) and the back navigation button
2. **Action Bar** — a dedicated `glass card-pad` section below the topbar containing all action buttons, grouped by intent

This prevents button clutter in the header area, keeps the page title readable, and creates visual hierarchy between navigation and actions.

## Structure

```
┌─────────────────────────────────────────────────────────────┐
│ ● SECTION LABEL                           ← Back to [List]  │
│ Reference · Customer Name                                    │
│ Subtitle description                                         │
├─────────────────────────────────────────────────────────────┤
│ [Status Actions]              │  [Document Actions]          │
│ Edit  Send  Archive  Convert  │  Preview  PDF  Duplicate  ×  │
└─────────────────────────────────────────────────────────────┘
```

## Topbar

- Standard `.topbar` div with heading and subtitle on the left
- Back button on the right using `btn btn-outline` class (blue outline, no fill)
- No other buttons in the topbar

```html
<div class="topbar">
    <div>
        <div class="eyebrow"><span class="dot"></span> Section Label</div>
        <h1 class="topbar-heading">Reference · Customer</h1>
        <p class="topbar-subtitle">Description text.</p>
    </div>
    <div>
        <a asp-action="Index" asp-controller="Controller" class="btn btn-outline">&larr; Back to List</a>
    </div>
</div>
```

## Action Bar

- Placed directly below the topbar, before any content sections
- Uses `class="glass card-pad"` with tighter padding: `padding:14px 20px`
- Bottom margin: `margin-bottom:22px`
- Contains a flex container with `justify-content:space-between`
- Two groups separated by a subtle vertical divider

```html
<section class="glass card-pad" style="margin-bottom:22px;padding:14px 20px;">
    <div style="display:flex;justify-content:space-between;align-items:center;flex-wrap:wrap;gap:10px;">
        <!-- Status Actions (left) -->
        <div style="display:flex;gap:10px;flex-wrap:wrap;align-items:center;">
            <!-- Buttons that change the entity's state -->
        </div>
        <!-- Divider -->
        <div style="width:1px;height:28px;background:rgba(13,94,166,.1);flex-shrink:0;"></div>
        <!-- Document Actions (right) -->
        <div style="display:flex;gap:10px;flex-wrap:wrap;align-items:center;">
            <!-- Buttons that produce output or utility operations -->
        </div>
    </div>
</section>
```

## Button Grouping Rules

### Left Group — Status Actions
Buttons that change the entity's lifecycle or state:
- Edit
- Send / Issue / Mark as [status]
- Archive / Unarchive
- Convert (to invoice, to customer, etc.)
- Share

### Right Group — Document Actions
Buttons that produce output or perform utility operations:
- Preview
- Download PDF
- Print
- Duplicate
- Delete (always last, uses `btn btn-danger`)

## Button Styling

| Action Type | Class | Example |
|------------|-------|---------|
| Back navigation | `btn btn-outline` | ← Back to Quotations |
| Primary action (most important) | `btn btn-primary` | Send Quotation, Share |
| Success/conversion action | `btn btn-green` | Mark Accepted, Convert to Invoice |
| Secondary action | `btn btn-secondary` | Edit, Preview, Download PDF, Duplicate, Archive |
| Destructive action | `btn btn-danger` | Delete |

## Divider

The vertical divider between the two groups:
- Width: `1px`
- Height: `28px`
- Color: `rgba(13,94,166,.1)` (subtle blue-grey)
- `flex-shrink:0` to prevent collapse

On mobile (when buttons wrap), the divider naturally disappears into the flow without breaking layout.

## Responsive Behaviour

Both groups use `flex-wrap:wrap` — on smaller screens, buttons wrap within their group. The `gap:10px` ensures consistent spacing. The outer container's `justify-content:space-between` pushes groups apart on wide screens but allows stacking on narrow ones.

## Applied Pages

| Page | Left Group | Right Group |
|------|-----------|-------------|
| Quotation Detail | Edit, Send, Mark Accepted, Archive, Convert to Invoice | Preview Proposal, Download PDF, Duplicate, Delete |
| Invoice Detail | Edit, Share, Duplicate | Preview, Download PDF, Print, Delete |

## When to Use

Apply this pattern to any detail page that has more than 3 action buttons. Pages with only 1-2 actions can keep them in the topbar.
