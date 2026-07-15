# Known Issue: Table Right-Edge Text Clipping

## Problem

On pages with `.table-responsive` containers (which use `overflow-x: auto`), the last column's rightmost characters get clipped by 1–2 pixels. This is especially visible on:
- Bold text (font-weight: 700)
- Currency amounts with trailing zeros (e.g., €1,190.00)
- Coloured text in the last column

The issue occurs because `overflow-x: auto` creates a scroll boundary exactly at the container's content edge. The browser's sub-pixel font rendering can push the last character slightly beyond that boundary.

## Visual Example

```
                €1,190.00    ← fully visible
                €1,190.00    ← fully visible  
     €0.00      €1,190.0     ← last digit clipped!
```

The footer/totals row is most affected because it typically has bold + coloured text which renders slightly wider than normal weight.

## Root Cause

```html
<div class="table-responsive">   <!-- overflow-x: auto — clips at boundary -->
    <table>
        <td style="text-align:right;">€1,190.00</td>  <!-- no right padding -->
    </table>
</div>
```

The combination of:
1. `overflow-x: auto` on the container
2. `text-align: right` on the last column
3. No right padding on the container or the cell
4. Bold/coloured font rendering wider than the calculated text width

## Solution

Add a small right padding to BOTH the container and the last cell:

```html
<!-- Fix 1: Container padding -->
<div class="table-responsive" style="padding-right:2px;">

<!-- Fix 2: Last cell padding (especially in tfoot/totals rows) -->
<td style="text-align:right; padding-right:4px;">€1,190.00</td>
```

**Why both?**
- Container padding (2px) prevents the scroll boundary from sitting flush against content
- Cell padding (4px) gives the bold/coloured text rendering room to breathe

## Where This Applies

Any table in the project that:
- Uses `.table-responsive` wrapper
- Has right-aligned numeric/currency values in the last column
- Has a bold totals row (tfoot)

**Pages affected (check these):**
- `/Statement` — Transaction History totals row ✅ (fixed)
- `/Purchase` — if the last column has right-aligned amounts
- `/Revenue/Receivables` — Outstanding balance column
- `/Vat` — VAT amounts in summary tables
- `/Invoice` — Line totals on Invoice Detail

## Prevention

When creating new tables with right-aligned last columns, always add:
```css
.table-responsive { padding-right: 2px; }
```

Or add it globally in the site stylesheet for all `.table-responsive` containers.
