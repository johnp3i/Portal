# Table Styling Guide

## Overview

All data tables in the Portal follow a consistent visual pattern. This guide defines the mandatory rules for table structure, column types, status badges, action buttons, and responsive behaviour.

---

## Table Container

Tables are always placed inside a `.glass.card-pad` section:

```html
<section class="glass card-pad">
    <table>
        ...
    </table>
</section>
```

For tables that may overflow on smaller screens, wrap in `.table-responsive`:

```html
<section class="glass card-pad">
    <div class="table-responsive index-table">
        <div class="scroll-hint">&larr; Scroll horizontally &rarr;</div>
        <table>
            ...
        </table>
    </div>
</section>
```

---

## Table Header (`<thead>`)

### Rules

- Every column MUST have a `<th>` with a descriptive label
- The last column for row actions MUST be labelled `<th>Actions</th>`
- Never leave a `<th></th>` empty — if a column has no label, question whether it should exist

### Example

```html
<thead>
    <tr>
        <th>Name</th>
        <th>Email</th>
        <th>Status</th>
        <th>Created</th>
        <th>Actions</th>
    </tr>
</thead>
```

---

## Status Columns

Status values MUST always use the `.pill` badge classes. Never render status as plain text.

### Available Status Pills

| Class | Colour | Use For |
|-------|--------|---------|
| `.pill .pill-green` | Green | Active, Completed, Paid, Pass |
| `.pill .pill-red` | Red | Inactive, Cancelled, Failed, Overdue |
| `.pill .pill-blue` | Blue | Upcoming, Issued, EU RC, In Progress |
| `.pill .pill-gold` | Gold | Pending, Warning, Partially Paid |
| `.pill .pill-cyan` | Cyan | EU Paid, Info states |
| `.pill .pill-grey` | Grey | Draft, Expense, Neutral states |

### Example

```html
<td>
    @if (item.IsActive)
    {
        <span class="pill pill-green">Active</span>
    }
    else
    {
        <span class="pill pill-red">Inactive</span>
    }
</td>
```

### Status Mapping Reference

| Status Value | Pill Class |
|-------------|-----------|
| Active | `pill-green` |
| Inactive | `pill-red` |
| Completed | `pill-green` |
| Cancelled | `pill-red` |
| Upcoming | `pill-blue` |
| Pending | `pill-gold` |
| Submitted | `pill-green` |
| Draft | `pill-grey` |
| Overdue | `pill-red` |
| Partially Paid | `pill-gold` |
| Paid | `pill-green` |
| Voided | `pill-red` |

---

## Action Columns

Row-level action buttons MUST use the `.tbl-action` classes (see `button-styling-guide.md`).

### Rules

- Use `<td style="white-space:nowrap;">` to prevent button wrapping
- Primary actions (Edit, Detail, Enable): `tbl-action tbl-action--primary`
- Secondary actions (Preview, PDF, Calendar Task, Export): `tbl-action tbl-action--secondary`
- Destructive actions (Delete, Deactivate, Cancel, Void): `tbl-action tbl-action--danger`
- Use `<a>` for navigation, `<button>` for JS-triggered actions
- Keep text labels short (1–2 words max)

### Example

```html
<td style="white-space:nowrap;">
    <a href="/Entity/Detail/@item.Id" class="tbl-action tbl-action--primary">Detail</a>
    <button class="tbl-action tbl-action--primary" onclick="edit(@item.Id)">Edit</button>
    <button class="tbl-action tbl-action--secondary" onclick="export(@item.Id)">PDF</button>
    <button class="tbl-action tbl-action--danger" onclick="deactivate(@item.Id)">Deactivate</button>
</td>
```

### Conditional Actions

Show/hide action buttons based on state:

```html
<td style="white-space:nowrap;">
    <a href="/Detail/@item.Id" class="tbl-action tbl-action--primary">Detail</a>
    <button class="tbl-action tbl-action--primary" onclick="edit(@item.Id)">Edit</button>
    @if (item.IsActive)
    {
        <button class="tbl-action tbl-action--danger" onclick="deactivate(@item.Id)">Deactivate</button>
    }
    else
    {
        <button class="tbl-action tbl-action--secondary" onclick="activate(@item.Id)">Activate</button>
    }
</td>
```

Note: "Activate" uses `tbl-action--secondary` (muted) since it's an error-correction action, not a primary workflow.

---

## Data Formatting

### Dates

- Display format: `dd MMM yyyy` (e.g., "21 Jul 2026")
- With time: `dd MMM yyyy HH:mm` (e.g., "21 Jul 2026 10:00")
- Use `@item.CreatedAtUtc.ToString("dd MMM yyyy")` in Razor

### Currency

- Always prefix with the business currency symbol from `ViewBag.CurrencySymbol` or `Model.CurrencySymbol`
- Format: `@currency@amount.ToString("N2")` → "€1,234.56"
- Right-align amount columns: `<td style="text-align:right;font-weight:700;">`

### Truncated Text

- Long text fields (descriptions, notes) should be truncated with ellipsis:
- Max 80–100 characters in a table cell
- Pattern: `@(item.Description?.Length > 80 ? item.Description.Substring(0, 80) + "…" : item.Description ?? "—")`

### Empty Values

- Use an em dash "—" for null/empty fields: `@(item.Value ?? "—")`
- Never leave cells blank — always show "—" for missing data

---

## Pagination

When a table is paginated, place pagination below the table within the same card:

```html
<div style="display:flex;justify-content:space-between;align-items:center;margin-top:18px;flex-wrap:wrap;gap:12px;">
    <div style="font-size:14px;color:#5a6a7a;">Showing 1-15 of 42</div>
    <div style="display:flex;gap:4px;">
        <!-- Page buttons -->
    </div>
</div>
```

---

## Row Styling

### Inactive/Voided Rows

Rows for inactive or voided records should be visually muted:

```html
<tr style="opacity:.6;">
    ...
</tr>
```

### Hover Effect

Tables use CSS hover on `tr`:

```css
tr:hover { background: rgba(13,94,166,.02); }
```

This is defined globally in `site.css` — no inline styles needed.

---

## What Is NOT Allowed

| Don't | Do |
|-------|-----|
| `<th></th>` (empty header) | `<th>Actions</th>` |
| Plain text status ("Active") | `<span class="pill pill-green">Active</span>` |
| `.badge .badge-success` (undefined) | `.pill .pill-green` |
| `.btn .btn-sm` in table rows | `.tbl-action .tbl-action--primary` |
| Emoji icons (✏️ 🗑️) as buttons | Text labels with `tbl-action` classes |
| Inline colour overrides on buttons | Use the correct `tbl-action` variant |
| Empty cells for null data | Show "—" (em dash) |
| Unstyled `<select>` for status | Use `.pill` badges (read-only display) |

---

## Complete Table Example

```html
<section class="glass card-pad">
    <table>
        <thead>
            <tr>
                <th>Name</th>
                <th>Email</th>
                <th>Company</th>
                <th>Status</th>
                <th>Created</th>
                <th>Actions</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var contact in Model.Items)
            {
                <tr style="@(!contact.IsActive ? "opacity:.6;" : "")">
                    <td><strong>@contact.FirstName @contact.LastName</strong></td>
                    <td>@(contact.Email ?? "—")</td>
                    <td>@(contact.CompanyName ?? "—")</td>
                    <td>
                        @if (contact.IsActive)
                        {
                            <span class="pill pill-green">Active</span>
                        }
                        else
                        {
                            <span class="pill pill-red">Inactive</span>
                        }
                    </td>
                    <td>@contact.CreatedAtUtc.ToString("dd MMM yyyy")</td>
                    <td style="white-space:nowrap;">
                        <a href="/Detail/@contact.Id" class="tbl-action tbl-action--primary">Detail</a>
                        <button class="tbl-action tbl-action--primary" onclick="edit(@contact.Id)">Edit</button>
                        @if (contact.IsActive)
                        {
                            <button class="tbl-action tbl-action--danger" onclick="deactivate(@contact.Id)">Deactivate</button>
                        }
                        else
                        {
                            <button class="tbl-action tbl-action--secondary" onclick="activate(@contact.Id)">Activate</button>
                        }
                    </td>
                </tr>
            }
        </tbody>
    </table>
</section>
```

---

## File Location

CSS source: `Portal.Web/wwwroot/css/site.css`

Related guides:
- `button-styling-guide.md` — Button classes reference
- `quick-date-filter-shortcuts.md` — Date filter pattern for list pages

All views must follow these rules. No custom table styles should be created inline.
