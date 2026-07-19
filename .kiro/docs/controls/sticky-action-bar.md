# Sticky Action Bar

## Purpose

A fixed footer bar that holds the primary form action buttons (Save, Cancel) at the bottom of the viewport. Used on pages with long forms or multiple card sections where the save button would otherwise be buried inside a content card and hard to find.

## When to Use

- Any form page with 2+ card sections (header fields, line items, totals, attachments, etc.)
- Entry/edit pages where the user scrolls through content before saving
- Pages where the save action applies to the entire form, not just one section

## When NOT to Use

- Simple single-section forms where the button is already visible without scrolling
- Modal dialogs (they have their own footer)
- List pages (no form to save)

## Visual Design

- Fixed to the bottom of the viewport (`position: fixed; bottom: 0`)
- Background: `#d6e6f0` (light blue — creates clear contrast with the `#F7FAFC` page background)
- Top border: `1.5px solid rgba(13,94,166,.15)`
- Box shadow: `0 -4px 20px rgba(0,0,0,.08)` (subtle upward shadow for elevation)
- Padding: `14px 32px`
- Buttons aligned to the right (`justify-content: flex-end`)
- z-index: `1000` (above page content, below modals)

## Implementation

### 1. HTML Structure

Place this at the bottom of the page, after all content sections and before `@Html.AntiForgeryToken()`:

```html
<!-- Bottom spacer for sticky action bar -->
<div style="height:80px;"></div>

<!-- Sticky Action Bar -->
@if (!isLocked)
{
    <div class="sticky-action-bar">
        <div class="sticky-action-bar__inner">
            <a href="/Controller/Index" class="btn btn-secondary">Cancel</a>
            <button type="button" class="btn btn-primary" onclick="submitForm()">Save [Entity Name]</button>
        </div>
    </div>
}
else
{
    <div class="sticky-action-bar">
        <div class="sticky-action-bar__inner">
            <a href="/Controller/Index" class="btn btn-secondary">Back to List</a>
        </div>
    </div>
}
```

### 2. CSS

Add to the page's `<style>` block:

```css
.sticky-action-bar {
    position: fixed;
    bottom: 0;
    left: 0;
    right: 0;
    z-index: 1000;
    background: #d6e6f0;
    border-top: 1.5px solid rgba(13,94,166,.15);
    box-shadow: 0 -4px 20px rgba(0,0,0,.08);
    padding: 14px 32px;
}
.sticky-action-bar__inner {
    display: flex;
    justify-content: flex-end;
    align-items: center;
    gap: 12px;
}
```

### 3. Bottom Spacer

Always add a spacer `<div style="height:80px;"></div>` before the sticky bar to prevent it from overlapping the last content section.

## Migration Guide — Converting Existing Pages

To convert an existing page that has its save button inside a card section:

1. **Remove** the inline button row from inside the card section:
   ```html
   <!-- DELETE THIS -->
   <div style="display:flex;gap:12px;justify-content:flex-end;margin-top:28px;">
       <a href="..." class="btn btn-secondary">Cancel</a>
       <button type="button" class="btn btn-primary" onclick="...">Save</button>
   </div>
   ```

2. **Add** the bottom spacer and sticky bar HTML (from section 1 above) after all content sections.

3. **Add** the CSS (from section 2 above) to the page's style block.

4. **Update button labels** to be explicit about what is being saved:
   - "Save Z-Report" (not "Save Changes")
   - "Create Invoice" (not "Create")
   - "Save Customer" (not "Submit")

## Button Label Convention

| Mode | Primary Button Label |
|------|---------------------|
| Create | "Create [Entity Name]" |
| Edit | "Save [Entity Name]" |
| Locked/readonly | No primary button — only "Back to List" |

## Reference Implementation

See `Portal.Web/Views/ZReport/Entry.cshtml` for the first implementation of this pattern.

## Notes

- The bar is always visible regardless of scroll position
- On mobile, the bar spans the full width naturally
- The `isLocked` condition hides the save button when the record cannot be edited (e.g. assigned to a submitted VAT period)
- The bar uses the project's standard `.btn .btn-primary` and `.btn .btn-secondary` button classes — no custom button styles needed
