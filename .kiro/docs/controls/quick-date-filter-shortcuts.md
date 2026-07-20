# Quick Date Filter Shortcuts

## Purpose

A row of pill-style buttons below the filter panel fields that allow users to instantly set predefined date ranges (This Month, Last 3 Months, Last Year, etc.) without manually picking dates. Clicking a shortcut sets the From/To date fields and auto-submits the filter form.

## When to Use

- Any list page that has a filter panel with From Date and To Date fields
- Pages where users frequently filter by common time periods (monthly, quarterly, yearly)

## Visual Design

- Positioned below the main filter field row, separated by `margin-top: 14px`
- Left-aligned with a small "QUICK:" label prefix
- Buttons use the `.period-shortcut` class — pill-shaped, light border, hover highlight, active state in blue
- Standard presets: This Month, Last Month, Last 3 Months, Last 6 Months, This Year, Last Year, All Time

## Implementation

### 1. Add the form ID

The filter `<form>` must have an `id` attribute so the JavaScript can submit it programmatically:

```html
<form id="filterForm" method="get" action="/Controller/Action">
```

### 2. Add the shortcut row inside the form

Place this block immediately after the filter fields `</div>` and before the closing `</form>`:

```html
<div style="margin-top:14px;display:flex;gap:8px;flex-wrap:wrap;align-items:center;">
    <span style="font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;color:#5E7385;margin-right:4px;">Quick:</span>
    <button type="button" class="period-shortcut" onclick="setQuickPeriod('this_month')">This Month</button>
    <button type="button" class="period-shortcut" onclick="setQuickPeriod('last_month')">Last Month</button>
    <button type="button" class="period-shortcut" onclick="setQuickPeriod('last_3')">Last 3 Months</button>
    <button type="button" class="period-shortcut" onclick="setQuickPeriod('last_6')">Last 6 Months</button>
    <button type="button" class="period-shortcut" onclick="setQuickPeriod('this_year')">This Year</button>
    <button type="button" class="period-shortcut" onclick="setQuickPeriod('last_year')">Last Year</button>
    <button type="button" class="period-shortcut" onclick="setQuickPeriod('all')">All Time</button>
</div>
```

### 3. Add the CSS

Place inside `@section Scripts { }` before the `<script>` block, or in the page's `<style>` section:

```css
.period-shortcut {
    padding: 5px 12px;
    border-radius: 8px;
    border: 1.5px solid rgba(13,94,166,.12);
    background: #fff;
    font-size: 12px;
    font-weight: 600;
    font-family: 'Inter', sans-serif;
    color: #5E7385;
    cursor: pointer;
    transition: all .15s;
}
.period-shortcut:hover {
    background: rgba(13,94,166,.06);
    border-color: rgba(13,94,166,.25);
    color: #0D5EA6;
}
.period-shortcut.active {
    background: rgba(13,94,166,.08);
    border-color: #0D5EA6;
    color: #0D5EA6;
}
```

### 4. Add the JavaScript

Place inside the `<script>` block within `@section Scripts { }`:

```javascript
function setQuickPeriod(preset) {
    var now = new Date();
    var from, to;
    switch (preset) {
        case 'this_month': from = new Date(now.getFullYear(), now.getMonth(), 1); to = now; break;
        case 'last_month': from = new Date(now.getFullYear(), now.getMonth() - 1, 1); to = new Date(now.getFullYear(), now.getMonth(), 0); break;
        case 'last_3': from = new Date(now.getFullYear(), now.getMonth() - 3, 1); to = now; break;
        case 'last_6': from = new Date(now.getFullYear(), now.getMonth() - 6, 1); to = now; break;
        case 'this_year': from = new Date(now.getFullYear(), 0, 1); to = now; break;
        case 'last_year': from = new Date(now.getFullYear() - 1, 0, 1); to = new Date(now.getFullYear() - 1, 11, 31); break;
        case 'all': from = null; to = null; break;
    }
    document.getElementById('dateFrom').value = from ? formatDateISO(from) : '';
    document.getElementById('dateTo').value = to ? formatDateISO(to) : '';
    document.querySelectorAll('.period-shortcut').forEach(function(b) { b.classList.remove('active'); });
    event.target.classList.add('active');
    document.getElementById('filterForm').submit();
}

function formatDateISO(d) {
    return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
}
```

### 5. Required element IDs

The JavaScript assumes these element IDs exist on the page:

| ID | Element | Purpose |
|---|---|---|
| `filterForm` | The `<form>` wrapping the filters | Auto-submitted after setting dates |
| `dateFrom` | The "From Date" `<input type="date">` | Receives the computed start date |
| `dateTo` | The "To Date" `<input type="date">` | Receives the computed end date |

If your page uses different IDs for the date inputs (e.g. `fromDateFilter`, `toDateFilter`), update the `getElementById` calls in `setQuickPeriod` to match.

## Migration Guide — Adding to an Existing Filter Panel

1. **Add `id="filterForm"`** to the existing `<form>` tag
2. **Verify** the date inputs have `id="dateFrom"` and `id="dateTo"` (or adjust the JS)
3. **Insert** the shortcut button row HTML (step 2) after the filter fields div
4. **Add** the CSS (step 3) to the page
5. **Add** the `setQuickPeriod` and `formatDateISO` functions (step 4) to the page's script section
6. **Test** by clicking "This Month" — the form should auto-submit with the current month's date range

## Date Calculation Reference

| Preset | From | To |
|---|---|---|
| This Month | 1st of current month | Today |
| Last Month | 1st of previous month | Last day of previous month |
| Last 3 Months | 1st of (current month - 3) | Today |
| Last 6 Months | 1st of (current month - 6) | Today |
| This Year | Jan 1 of current year | Today |
| Last Year | Jan 1 of previous year | Dec 31 of previous year |
| All Time | Empty (clears both fields) | Empty (clears both fields) |

## Reference Implementations

- `Portal.Web/Views/ZReport/Index.cshtml` — Z-Reports list with quick shortcuts
- `Portal.Web/Views/SalesImport/Records.cshtml` — Sales Records list with quick shortcuts
- `Portal.Web/Views/Receipt/Index.cshtml` — Receipt list (original implementation, uses AJAX reload instead of form submit)

## Notes

- The "All Time" preset clears both date fields and submits — showing all records unfiltered by date
- The `.active` class is purely visual feedback for the current click — it does not persist across page reloads (the form submits and the page redraws)
- If the page uses AJAX-based filtering (like Receipts), replace `document.getElementById('filterForm').submit()` with the page's reload function (e.g. `loadReceipts(1)`)
