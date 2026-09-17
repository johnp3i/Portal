# Mobile Action Bar Pattern (list pages)

> **Purpose:** a reusable, mobile-only toolbar for list/index pages. It replaces the
> stacked full-width buttons that appear on phones with a single compact row:
>
> ```
> [ + Create (flex) | Filters (count) | More ⋯ ]
> ```
>
> **Reference implementation:** `Portal.Web/Views/Purchase/Index.cshtml` (the Purchases list).
> This document is self-contained — an agent can apply the pattern to any other list page
> (Meetings, Invoices, Quotations, Suppliers, etc.) by following it end to end.

---

## 1. What it is and why

Desktop list pages have a `.topbar` with a primary button (e.g. **Create Purchase**) plus
several secondary/export buttons (Bulk Entry, CSV Import, Export CSV, Export PDF). On a phone
those buttons previously stacked into tall full-width rows, and the filter card added another
block — pushing the actual data far down the screen.

The **mobile action bar** fixes this with a native-app feel:

- **Primary** action (Create) — a prominent button that flexes to fill the row.
- **Filters** — a compact ghost button that toggles the page's existing filter panel and shows
  a small **badge with the number of active filters**.
- **More ⋯** — a compact icon button opening a dropdown with the secondary/export actions.

### Golden rule — do NOT touch the desktop layout

The mobile action bar is a **separate element rendered below the `.topbar`**. It is
`display:none` on desktop and `display:flex` on mobile. The desktop `.topbar` keeps its own
buttons exactly as before. **Never** try to re-flow the `.topbar` itself with `!important`
overrides or specificity hacks — that path was tried and abandoned because the global
`mobile.css` topbar rules fight back. Keep the mobile bar independent.

### Breakpoint

Mobile = `@media (max-width: 768px)` (phone). This matches the rest of `mobile.css`.

---

## 2. Prerequisites (already true across the app)

These shared pieces already exist and the pattern relies on them:

1. **Design tokens** in `Portal.Web/wwwroot/css/site.css` — use `var(--blue)`, `var(--text)`,
   etc. Never hard-code hex where a token exists.
2. **Shared filter system:** every list page has a `.filter-toggle` button + a
   `.filter-panel` container with a unique `id`. On mobile the panel is collapsed by default
   (`max-height:0`) and expands via the `.expanded` class. The toggle JS lives in
   `Portal.Web/wwwroot/js/mobile-nav.js`.
3. **Enhanced toggle lookup (already merged):** the `mobile-nav.js` filter toggle resolves its
   panel by `aria-controls` id **first**, then falls back to the nearest containing card. This
   is what lets the Filters button live in the action bar (outside the filter card) rather than
   inside it. Do not revert this.
4. **Eyebrow hidden on mobile (already merged):** `mobile.css` hides `.topbar .eyebrow` below
   1100px. No per-page work needed.

---

## 3. How to apply it to a new list page

Assume a page `Foo/Index.cshtml` with:
- a `.topbar` containing a primary `Create` action and some secondary/export buttons,
- a filter `<section>` with a `.filter-panel` whose id is e.g. `fooFilterPanel`,
- a controller that already accepts the filter query-string params.

### Step 1 — Compute the active-filter count (top of the view)

In the top `@{ }` block, sum the filters that are currently applied. Count each **applied**
filter as 1 (nullable has-value, or non-empty search string):

```cshtml
@{
    var activeFilterCount =
        (Model.SupplierId.HasValue ? 1 : 0)
        + (Model.CategoryId.HasValue ? 1 : 0)
        + (Model.DateFrom.HasValue ? 1 : 0)
        + (Model.DateTo.HasValue ? 1 : 0)
        + (!string.IsNullOrWhiteSpace(Model.SearchTerm) ? 1 : 0);
        // ...one term per real filter this page supports
}
```

### Step 2 — Wrap the desktop topbar buttons in a hideable container

Give the topbar's button `<div>` the class `topbar-actions-desktop`. Leave its contents
(the desktop buttons) exactly as they are:

```cshtml
<div class="topbar-actions-desktop" style="display:flex;gap:12px;flex-wrap:wrap;">
    <a asp-action="Create" class="btn btn-primary">Create Foo</a>
    <a asp-action="BulkThing" class="btn btn-secondary">Bulk Thing</a>
    <a class="btn btn-secondary" href="/Foo/ExportCsv?...">Export CSV</a>
    <!-- etc. -->
</div>
```

### Step 3 — Add the mobile action bar directly AFTER the closing `</div>` of `.topbar`

Use the `mab-*` classes verbatim. Point the Filters button's `aria-controls` at this page's
filter panel id, and give the More panel a unique id (e.g. `fooMorePanel`). The More toggle's
`onclick` calls a small per-page JS function (Step 5).

```cshtml
<!-- Mobile-only action bar. Hidden on desktop; does not touch the topbar above. -->
<div class="mobile-action-bar">
    <a asp-action="Create" class="mab-btn mab-primary">+ Create</a>

    <button type="button" class="mab-btn mab-ghost filter-toggle"
            aria-expanded="false" aria-controls="fooFilterPanel">
        <svg width="15" height="15" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path d="M3 4h18M7 9h10M10 14h4"/></svg>
        <span>Filters</span>
        @if (activeFilterCount > 0)
        {
            <span class="mab-count">@activeFilterCount</span>
        }
    </button>

    <div class="mab-more">
        <button type="button" class="mab-btn mab-ghost mab-more-toggle"
                aria-haspopup="true" aria-expanded="false"
                onclick="toggleFooMoreMenu(event)" title="More">
            <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2.5" viewBox="0 0 24 24"><circle cx="5" cy="12" r="1.4"/><circle cx="12" cy="12" r="1.4"/><circle cx="19" cy="12" r="1.4"/></svg>
        </button>
        <div class="mab-more-panel" id="fooMorePanel" role="menu">
            <a asp-action="BulkThing" class="mab-more-item" role="menuitem">Bulk Thing</a>
            <a class="mab-more-item" role="menuitem" href="/Foo/ExportCsv?...">Export CSV</a>
            <!-- one .mab-more-item per secondary/export action -->
        </div>
    </div>
</div>
```

**Notes**
- The Filters button MUST carry the `filter-toggle` class AND `aria-controls="<yourPanelId>"`.
  That is the entire wiring — the shared `mobile-nav.js` handles expand/collapse and the
  `active` state. Do not add bespoke filter JS.
- Put the SAME export query-string on both the desktop buttons and the `mab-more-item` links so
  exports respect active filters in either layout.

### Step 4 — Tag the filter `<section>` so its chrome collapses on mobile

Add a page-specific class (e.g. `foo-filter-card`) to the filter `<section class="glass card-pad">`.
On mobile the toggle lives in the action bar, so the card itself should show no background/border
when collapsed; only the expanded panel becomes a card. (CSS in Step 6.)

```cshtml
<section class="glass card-pad foo-filter-card" style="margin-bottom:22px;">
    <form id="filterForm" method="get" asp-action="Index">
        <div class="filter-panel" id="fooFilterPanel">
            <!-- filter fields -->
        </div>
    </form>
</section>
```

> If this page previously had its own in-card `.filter-toggle` button, **remove it** — the
> action-bar Filters button replaces it. Two toggles targeting the same panel is a bug.

### Step 5 — Add the More-menu toggle JS (per page)

The More dropdown is trivial open/close with click-outside-to-close. Rename the function per
page (`toggleFooMoreMenu`) and point it at this page's panel id:

```html
<script>
    function toggleFooMoreMenu(event) {
        event.stopPropagation();
        var panel = document.getElementById('fooMorePanel');
        var toggle = event.currentTarget;
        var isOpen = panel.classList.toggle('open');
        toggle.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
    }
    document.addEventListener('click', function (e) {
        var panel = document.getElementById('fooMorePanel');
        if (!panel || !panel.classList.contains('open')) return;
        if (e.target.closest('.mab-more')) return;
        panel.classList.remove('open');
        var toggle = document.querySelector('.mab-more-toggle');
        if (toggle) toggle.setAttribute('aria-expanded', 'false');
    });
</script>
```

### Step 6 — Add the CSS

The `.mab-*` classes are **not yet global** (they live in the Purchases view today). Until they
are promoted to `mobile.css` (see Section 5), copy the block below into the page's `<style>`.
The **base rules** (outside the media query) hide the bar and pre-style the dropdown; the
**mobile rules** (inside `@@media (max-width: 768px)`) lay out the row.

> In a Razor `.cshtml`, `@media` must be written as `@@media` (the doubled `@` escapes to a
> literal `@`). In a plain `.css` file it is just `@media`.

**Base rules (desktop + shared):**

```css
/* Mobile action bar — a standalone row below the topbar, shown only on mobile.
   Desktop is unaffected (the topbar keeps its own buttons). */
.mobile-action-bar { display: none; }               /* shown in the mobile media query */
.mab-more-panel {
    display: none; position: absolute; top: calc(100% + 6px); right: 0;
    min-width: 190px; background: #fff;
    border: 1px solid rgba(13, 94, 166, 0.12); border-radius: 14px;
    box-shadow: 0 12px 30px rgba(11, 27, 40, 0.14); padding: 6px; z-index: 200;
}
.mab-more-panel.open { display: block; }
.mab-more-item {
    display: block; padding: 12px 14px; border-radius: 10px;
    font-size: 14px; font-weight: 700; color: #0B1B28; text-decoration: none; cursor: pointer;
}
.mab-more-item:hover, .mab-more-item:active {
    background: rgba(13, 94, 166, 0.06); color: #0D5EA6; text-decoration: none;
}
```

**Mobile rules (inside `@media (max-width: 768px)`):**

```css
.mobile-action-bar { display: flex; align-items: stretch; gap: 8px; margin: 0 0 16px; }

.mab-btn {
    font-family: inherit; font-weight: 700; font-size: 14px;
    border-radius: 12px; border: 1.5px solid transparent;
    display: inline-flex; align-items: center; justify-content: center; gap: 6px;
    cursor: pointer; padding: 12px 14px; white-space: nowrap; text-decoration: none;
}
/* Create — primary, grows to fill the row */
.mab-primary {
    flex: 1 1 auto;
    background: linear-gradient(180deg,#1A6BB8 0%, var(--blue) 100%);
    color: #fff; box-shadow: 0 8px 18px rgba(13,94,166,.22);
}
/* Filters + More — compact ghost buttons */
.mab-ghost {
    flex: 0 0 auto; background: rgba(13,94,166,.06);
    border-color: rgba(13,94,166,.14); color: var(--blue);
}
.mab-ghost.active { background: var(--blue); color: #fff; border-color: var(--blue); }
.mab-more-toggle { padding: 12px 13px; }

/* Active-filter count badge */
.mab-count {
    display: inline-flex; align-items: center; justify-content: center;
    min-width: 18px; height: 18px; padding: 0 5px; border-radius: 10px;
    background: var(--blue); color: #fff; font-size: 10px; font-weight: 800; line-height: 1;
}
.mab-ghost.active .mab-count { background: #fff; color: var(--blue); }
.mab-more { position: relative; flex: 0 0 auto; }

/* Hide the desktop topbar buttons on mobile (the mobile bar replaces them). */
.topbar-actions-desktop { display: none !important; }

/* Filter card: toggle lives in the mobile bar, so strip the card chrome;
   only the expanded panel shows as its own card. Rename .foo-filter-card per page. */
.foo-filter-card {
    background: transparent !important; border: none !important;
    box-shadow: none !important; padding: 0 !important; margin-bottom: 0 !important;
}
.foo-filter-card .filter-panel.expanded {
    background: #fff; border: 1px solid rgba(13, 94, 166, 0.10);
    border-radius: 16px; padding: 14px !important; margin: 0 0 22px !important;
}
```

---

## 4. Verification checklist

After applying, verify:

- [ ] **Build** the web project — 0 `error CS` / 0 `error RZ`. (An `MSB3021` DLL/exe copy-lock
      error is environmental — a running app holding the output — not a code error.)
- [ ] **Desktop unchanged**: the `.topbar` still shows all its buttons; the mobile bar is not visible.
- [ ] **Mobile (≤768px)**: exactly one row — `[ + Create | Filters | More ⋯ ]` — sits below the
      title; the desktop buttons are hidden.
- [ ] **Filters** toggles the panel open/closed; the button gets the `active` (solid-blue) state
      when open; the count badge shows the number of applied filters.
- [ ] **More ⋯** opens the dropdown; tapping outside closes it; each item links correctly and
      preserves the active filter query-string on exports.
- [ ] No empty white filter card when the panel is collapsed.
- [ ] The eyebrow badge is hidden on mobile (handled globally — nothing to do per page).

> Rendering a phone viewport cannot be done from the build tools; a human must visually confirm
> on a device or by narrowing the browser to ≤768px.

---

## 5. Recommended next step: promote to `mobile.css`

Today the `.mab-*` CSS is duplicated per page (currently only Purchases). Once a second page
adopts the pattern, **move the `.mab-*` / `.mobile-action-bar` / `.topbar-actions-desktop` CSS
into `Portal.Web/wwwroot/css/mobile.css`** (base rules near the other topbar rules; mobile rules
inside a `@media (max-width: 768px)` block). Then each page only needs the markup (Steps 2–4) and
the small per-page More-menu JS (Step 5) — no CSS copy. The filter-card chrome rule can be
generalized by adding a shared class (e.g. `mobile-filter-card`) instead of a per-page name.

Keep the More-menu JS per page for now (each targets its own panel id); if it recurs enough,
generalize it in `mobile-nav.js` by deriving the panel id from a `data-*` attribute on the toggle.

---

## 6. Files involved (reference implementation)

- `Portal.Web/Views/Purchase/Index.cshtml` — markup + per-page `<style>` + More-menu JS.
- `Portal.Web/wwwroot/js/mobile-nav.js` — shared filter toggle (aria-controls-first lookup).
- `Portal.Web/wwwroot/css/mobile.css` — shared `.filter-toggle` / `.filter-panel` behavior and
  the global `.topbar .eyebrow { display:none }` mobile rule.
- `.kiro/docs/mockups/mobile-list-action-bar.html` — visual mockup of the three variants
  (Variant A is the one implemented).
```
