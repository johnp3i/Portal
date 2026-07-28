# Implementation Plan: Sidebar Scroll Navigation

## Overview

Convert the Portal sidebar from a grid-based layout to a fixed, independently scrollable panel with scroll position persistence via sessionStorage. This is a CSS/JS-only change targeting `site.css` and `_Layout.cshtml`.

## Tasks

- [x] 1. Update site.css with fixed sidebar and independent content scrolling
  - [x] 1.1 Restructure sidebar positioning from grid-based to fixed
    - Add `html, body { height: 100%; overflow: hidden }` rule (desktop only, scoped above 1100px)
    - Change `.app` from `display: grid; grid-template-columns: 280px 1fr` to `display: block`
    - Change `.sidebar` to `position: fixed; top: 0; left: 0; bottom: 0; width: 280px; overflow-y: auto; overflow-x: hidden; z-index: 100`
    - Add custom scrollbar styles for `.sidebar::-webkit-scrollbar` (5px width, subtle blue thumb)
    - Change `.content` to `margin-left: 280px; height: 100vh; overflow-y: auto`
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 2.1, 2.2, 2.3, 2.4, 2.5, 4.1, 4.2, 4.3, 4.4_

  - [x] 1.2 Update collapsed state CSS
    - Change `.app.sidebar-collapsed .sidebar` to use `width: 64px` instead of just padding changes
    - Add `.app.sidebar-collapsed .content { margin-left: 64px }`
    - Ensure sidebar toggle animation and tooltips still function with the new fixed positioning
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

- [x] 2. Add sidebar scroll position persistence JavaScript
  - [x] 2.1 Add scroll save/restore script to _Layout.cshtml
    - Add inline `<script>` block after the existing `toggleSidebar()` function
    - On page load: read `sessionStorage['sidebar-scroll-pos']` and set `sidebar.scrollTop`
    - On sidebar link click: save `sidebar.scrollTop` to `sessionStorage['sidebar-scroll-pos']` before navigation
    - Use event delegation on `.sidebar` to capture all `<a>` clicks
    - Handle the case where sessionStorage is unavailable (silent fail, start at 0)
    - _Requirements: 3.1, 3.2, 3.3, 6.1, 6.2, 6.3, 6.4_

- [x] 3. Verify mobile responsiveness is unaffected
  - [x] 3.1 Confirm mobile.css overrides take precedence
    - Verify that the `@media (max-width: 1100px)` rules in mobile.css still use `!important` for sidebar positioning
    - Ensure the new `html, body { overflow: hidden }` rule is scoped to desktop only (above 1100px) or does not conflict with mobile's `overflow-x: clip`
    - Test that the off-canvas drawer behavior is preserved on viewports ≤ 1100px
    - _Requirements: 1.4, 5.1, 5.2_

- [x] 4. Build verification checkpoint
  - Ensure `dotnet build` passes without errors, ask the user if questions arise.

## Notes

- No backend changes required — this is entirely CSS and JavaScript.
- The mobile layout already uses `position: fixed !important` for the sidebar (off-canvas drawer), so no mobile.css changes are needed.
- The `html, body { overflow: hidden }` rule must be scoped to desktop viewports (min-width: 1101px) to avoid conflicting with mobile's existing overflow handling.
- The `.app` grid is only removed for desktop — mobile.css already overrides it with `display: block !important` and `grid-template-columns: 1fr !important`.
- sessionStorage is used (not localStorage) because scroll position is ephemeral per browsing session.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2", "2.1"] },
    { "id": 2, "tasks": ["3.1"] }
  ]
}
```
