# Button Styling Guide

## Overview

The Portal uses two distinct button systems depending on context:

1. **Action Buttons (`.btn`)** — Used in topbars, filter panels, modals, and standalone CTAs
2. **Table Action Buttons (`.tbl-action`)** — Used exclusively inside table rows for per-record actions

Both are defined in `Portal.Web/wwwroot/css/site.css`.

---

## 1. Standard Action Buttons (`.btn`)

Used for primary page actions, form submissions, filter controls, and navigation CTAs.

### Classes

| Class | Appearance | Use Case |
|-------|-----------|----------|
| `.btn .btn-primary` | Solid blue gradient, white text, shadow | Primary actions: Save, Create, Filter, Submit |
| `.btn .btn-secondary` | White background, blue text, subtle border | Secondary actions: Cancel, Clear, Back |
| `.btn .btn-outline` | Transparent background, blue text, border | Tertiary actions: Back to list, Export |

### CSS Definition

```css
.btn {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 10px 20px;
  border-radius: 12px;
  font-size: 13px;
  font-weight: 700;
  border: none;
  cursor: pointer;
  text-decoration: none;
  font-family: inherit;
  transition: all .15s;
}

.btn-primary {
  background: linear-gradient(180deg, #1A6BB8, #0D5EA6);
  color: #fff;
  box-shadow: 0 8px 20px rgba(13,94,166,.18);
}

.btn-secondary {
  background: #fff;
  color: #0D5EA6;
  border: 1.5px solid rgba(13,94,166,.18);
}

.btn-outline {
  background: transparent;
  color: #0D5EA6;
  border: 1.5px solid rgba(13,94,166,.18);
}
```

### Usage Examples

```html
<!-- Primary CTA in topbar -->
<button class="btn btn-primary">Create Purchase</button>

<!-- Secondary action -->
<a href="/Purchase" class="btn btn-secondary">Back to Purchases</a>

<!-- Filter panel buttons -->
<button type="submit" class="btn btn-primary">Filter</button>
<a asp-action="Index" class="btn btn-secondary">Clear</a>
```

---

## 2. Table Action Buttons (`.tbl-action`)

Used ONLY inside `<table>` rows for per-record operations (Edit, Delete, Preview, etc.).

### Classes

| Class | Appearance | Use Case |
|-------|-----------|----------|
| `.tbl-action .tbl-action--primary` | Blue text on light blue background | Edit, Detail, Enable — primary row actions |
| `.tbl-action .tbl-action--secondary` | Grey text on light grey background | Preview, PDF, Disable — secondary row actions |
| `.tbl-action .tbl-action--danger` | Red text on light red background | Delete, Cancel, Void — destructive row actions |

### CSS Definition

```css
.tbl-action {
  font-size: 12px;
  font-weight: 700;
  padding: 6px 14px;
  border-radius: 8px;
  cursor: pointer;
  text-decoration: none;
  display: inline-flex;
  align-items: center;
  border: none;
  transition: all .15s;
  font-family: inherit;
}

.tbl-action--primary {
  color: #0D5EA6;
  background: rgba(13,94,166,.08);
}
.tbl-action--primary:hover {
  background: rgba(13,94,166,.14);
}

.tbl-action--secondary {
  color: #5a6a7a;
  background: rgba(90,106,122,.08);
}
.tbl-action--secondary:hover {
  background: rgba(90,106,122,.14);
}

.tbl-action--danger {
  color: #C24A4A;
  background: rgba(194,74,74,.08);
}
.tbl-action--danger:hover {
  background: rgba(194,74,74,.14);
}
```

### Usage Examples

```html
<!-- Standard table row actions -->
<td style="white-space:nowrap;">
    <a asp-action="Detail" asp-route-id="@item.Id" class="tbl-action tbl-action--primary">Detail</a>
    <a asp-action="Edit" asp-route-id="@item.Id" class="tbl-action tbl-action--primary">Edit</a>
    <a href="#" class="tbl-action tbl-action--secondary">Preview</a>
    <a href="#" class="tbl-action tbl-action--secondary">PDF</a>
    <button type="button" class="tbl-action tbl-action--danger" onclick="deleteItem(@item.Id)">Delete</button>
</td>

<!-- Toggle actions (Enable/Disable) -->
<td style="white-space:nowrap;">
    <button type="button" class="tbl-action tbl-action--primary" onclick="editRule(@rule.Id)">Edit</button>
    <button type="button" class="tbl-action tbl-action--secondary" onclick="toggleRule(@rule.Id)">Disable</button>
    <button type="button" class="tbl-action tbl-action--danger" onclick="deleteRule(@rule.Id)">Delete</button>
</td>
```

---

## Rules

### DO

- Use `.tbl-action` variants inside tables — they are compact and visually appropriate for row-level actions
- Use `.btn` variants for page-level actions (topbar, filter panels, modals, CTAs)
- Add `style="white-space:nowrap;"` to the actions `<td>` to prevent button wrapping
- Use `<a>` for navigation actions (Detail, Edit, Preview) and `<button>` for JS-triggered actions (Delete, Toggle)
- Keep action button text short: "Edit", "Delete", "PDF", "Disable" — not "Edit this record"

### DON'T

- Don't use `.btn` classes inside table rows — they're too large and break row density
- Don't use `.tbl-action` outside of tables — they're too subtle for standalone CTAs
- Don't mix `.btn` and `.tbl-action` in the same action group
- Don't use inline styles to override button colours — use the correct class variant instead
- Don't add `border` to `.tbl-action` buttons — they use background-only styling (no borders)

---

## Mapping Actions to Variants

| Action Type | Table Context | Page Context |
|------------|--------------|--------------|
| Create / Save / Submit | N/A (not in tables) | `.btn .btn-primary` |
| Edit / Detail / Enable | `.tbl-action .tbl-action--primary` | `.btn .btn-secondary` |
| Preview / PDF / Export | `.tbl-action .tbl-action--secondary` | `.btn .btn-secondary` |
| Delete / Cancel / Void / Deactivate | `.tbl-action .tbl-action--danger` | `.btn .btn-secondary` + SweetAlert2 confirmation |
| Disable (toggleable) | `.tbl-action .tbl-action--secondary` | N/A |
| Back / Clear / Cancel (non-destructive) | N/A | `.btn .btn-secondary` or `.btn .btn-outline` |

---

## File Location

CSS source: `Portal.Web/wwwroot/css/site.css` (lines 93–100)

All views should reference these classes. No custom button styles should be created inline.
