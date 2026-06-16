# Implementation Plan: Mobile Responsive Layout

## Overview

This plan implements a fully responsive mobile layout for the Portal platform by adding a dedicated `mobile.css` stylesheet, minimal HTML additions to `_Layout.cshtml` (mobile topbar, backdrop, bottom tab bar), and a vanilla JS interaction module (`mobile-nav.js`). The existing desktop layout remains completely untouched above 1100px. Implementation proceeds from structural HTML additions, through CSS responsive rules, to JS interactions, finishing with integration wiring and automated tests.

## Tasks

- [x] 1. Add mobile HTML structure to _Layout.cshtml
  - [x] 1.1 Add mobile.css stylesheet reference and mobile-nav.js script reference
    - Add `<link rel="stylesheet" href="~/css/mobile.css" />` after the existing `site.css` reference in `_Layout.cshtml`
    - Add `<script src="~/js/mobile-nav.js"></script>` before the closing `</body>` tag
    - _Requirements: 1.1, 1.2_

  - [x] 1.2 Add Mobile Topbar HTML element
    - Insert `<header class="mobile-topbar">` immediately inside `.app` before `<aside class="sidebar">`
    - Include hamburger button (left), platform logo (center), account avatar (right)
    - Add `aria-label="Open navigation"` on hamburger button
    - Use existing `ViewContext.RouteData` and Identity claims for avatar initials
    - _Requirements: 2.1, 2.2, 13.1_

  - [x] 1.3 Add Backdrop overlay element
    - Insert `<div class="mobile-backdrop" aria-hidden="true"></div>` after the sidebar element, before `<main>`
    - _Requirements: 3.1, 3.2, 13.3_

  - [x] 1.4 Add Bottom Tab Bar HTML element
    - Insert `<nav class="bottom-tab-bar" aria-label="Quick navigation">` after `<main>`, before closing `.app`
    - Include 4 tab items: Dashboard (`/`), Quotes (`/Quotation`), Invoices (`/Invoice`), Revenue (`/Revenue`)
    - Highlight active tab based on `currentController` from `ViewContext.RouteData.Values["controller"]`
    - _Requirements: 9.1, 9.2, 9.3, 13.2_

  - [x] 1.5 Add Mobile Account Dropdown markup
    - Add a dropdown container positioned below the avatar in the Mobile Topbar
    - Include: signed-in identity display, billing link (conditionally shown for owners via `User.HasClaim("IsOwner", "true")`), sign-out action
    - Hidden by default; toggled via JS
    - _Requirements: 10.2, 10.3_

- [x] 2. Create mobile.css with responsive rules
  - [x] 2.1 Create mobile.css with base structure and CSS custom property reuse
    - Create `Portal.Web/wwwroot/css/mobile.css`
    - Define two media query blocks: `@media (max-width: 768px)` for Phone and `@media (min-width: 769px) and (max-width: 1100px)` for Tablet
    - Add `@media (prefers-reduced-motion: reduce)` block to disable all transitions
    - Reuse existing CSS custom properties (`--bg`, `--blue`, `--line`, `--muted`, `--text`, etc.)
    - _Requirements: 1.2, 1.3_

  - [x] 2.2 Implement Mobile Topbar styles
    - `display: none` above 1100px
    - `position: sticky; top: 0; z-index: 100` at ≤1100px
    - Semi-transparent white background with `backdrop-filter: blur(10px)`
    - Flex layout: hamburger left, logo center, avatar right
    - _Requirements: 2.1, 2.3, 2.4_

  - [x] 2.3 Implement Off-Canvas Drawer styles
    - At ≤1100px: `position: fixed; top: 0; left: -280px; width: 280px; height: 100vh; z-index: 300`
    - Transition: `left .3s cubic-bezier(.4, 0, .2, 1)`
    - When `.drawer-open` on `#appShell`: `left: 0`
    - Overflow-y: auto for scrollable content
    - Hide existing sidebar from grid layout on mobile/tablet
    - _Requirements: 3.1, 3.5, 3.6, 4.3_

  - [x] 2.4 Implement Backdrop styles
    - `position: fixed; inset: 0; z-index: 200; background: rgba(0,0,0,0.4)`
    - Default: `opacity: 0; pointer-events: none`
    - When `.drawer-open` on `#appShell`: `opacity: 1; pointer-events: auto`
    - Transition: `opacity .25s`
    - _Requirements: 3.1, 3.2_

  - [x] 2.5 Implement Bottom Tab Bar styles
    - `display: none` above 768px
    - Phone: `position: fixed; bottom: 0; left: 0; right: 0; z-index: 50`
    - Flex with `justify-content: space-around`
    - Semi-transparent white with `backdrop-filter: blur(10px)`
    - Active item color: `var(--blue)`
    - Add `padding-bottom` to content area equal to tab bar height (~60px) on phone
    - _Requirements: 9.1, 9.3, 9.4, 9.5_

  - [x] 2.6 Implement responsive content area styles
    - Phone: full viewport width with 16px horizontal padding
    - Tablet: full viewport width with 18px horizontal padding
    - _Requirements: 4.1, 4.2_

  - [x] 2.7 Implement responsive grid collapse rules
    - Phone: `.grid-4`, `.grid-3`, `.grid-2`, `.form-grid` → `grid-template-columns: 1fr`
    - Phone: `.gauge-row` → `grid-template-columns: 1fr 1fr`
    - Tablet: `.grid-4`, `.grid-3` → `grid-template-columns: 1fr 1fr`
    - Tablet: `.grid-2`, `.form-grid` → retain `1fr 1fr`
    - Tablet: `.gauge-row` → `grid-template-columns: 1fr 1fr`
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7_

  - [x] 2.8 Implement horizontally scrollable table styles
    - Wrap tables in `overflow-x: auto; -webkit-overflow-scrolling: touch` container
    - Scroll hint text visible above table
    - Maintain desktop `min-width` on table to preserve column structure
    - _Requirements: 5.1, 5.2, 5.3_

  - [x] 2.9 Implement stacked filter styles
    - Phone: filter panels → `flex-direction: column; width: 100%` with full-width inputs
    - Phone: filter buttons → full-width stacked or evenly-split row
    - Tablet: filter fields wrap into rows accommodating two fields side-by-side
    - _Requirements: 7.1, 7.2, 7.3_

  - [x] 2.10 Implement full-width action button styles
    - Phone: `.btn-primary`, `.btn-green`, `.btn-danger` within `.content` → `width: 100%; display: block`
    - Tablet: retain intrinsic width
    - _Requirements: 8.1, 8.2_

  - [x] 2.11 Implement Account Menu repositioning styles
    - Hide desktop `#accountMenu` at ≤1100px
    - Position mobile account dropdown below avatar (absolute, right-aligned)
    - _Requirements: 10.1, 10.2_

  - [x] 2.12 Implement desktop preservation rules
    - Verify all mobile rules are wrapped in media queries ≤1100px
    - Ensure no overrides affect viewport > 1100px
    - Desktop grid `280px 1fr` remains intact
    - _Requirements: 11.1, 11.2, 11.3, 11.4_

- [x] 3. Checkpoint - Verify HTML and CSS integration
  - Ensure all HTML elements render correctly at desktop, tablet, and phone widths. Verify no desktop regression by checking the 1200px viewport. Ask the user if questions arise.

- [x] 4. Create mobile-nav.js interaction module
  - [x] 4.1 Implement drawer open/close interactions
    - Create `Portal.Web/wwwroot/js/mobile-nav.js`
    - Use IIFE pattern matching existing code style
    - Guard with null check on `document.getElementById('appShell')`
    - Hamburger click → add `.drawer-open` to `#appShell`
    - Close button click → remove `.drawer-open` from `#appShell`
    - Backdrop tap → remove `.drawer-open` from `#appShell`
    - Navigation link click inside drawer → remove `.drawer-open` from `#appShell`
    - Add `Escape` key handler to close drawer
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 12.1, 12.2, 12.3_

  - [x] 4.2 Implement mobile account dropdown interactions
    - Avatar click → toggle mobile account dropdown visibility
    - Outside click → close account dropdown (reuse existing click-outside pattern)
    - _Requirements: 10.2, 12.1_

- [x] 5. Checkpoint - Verify full mobile interaction flow
  - Ensure drawer opens/closes smoothly, backdrop works, bottom tab bar highlights correctly, account dropdown toggles. Verify desktop sidebar toggle still functions. Ask the user if questions arise.

- [x] 6. Per-view responsive adaptations
  - [x] 6.1 Adapt Dashboard view for mobile
    - Ensure KPI cards, charts, and summary widgets reflow to single/two-column on phone
    - Charts resize to fit available width
    - _Requirements: 14.1_

  - [x] 6.2 Adapt Quotation views (Index, Detail, Create/Edit) for mobile
    - Index: responsive table with horizontal scroll, accessible action buttons
    - Detail: stack sections vertically with full-width cards on phone
    - Create/Edit: single-column line-item form grid on phone, full-width inputs, full-width "Add Line" button
    - _Requirements: 14.2, 14.3, 14.4_

  - [x] 6.3 Adapt Invoice views (Index, Detail, Create/Edit) for mobile
    - Follow same responsive patterns as Quotation views
    - _Requirements: 14.5_

  - [x] 6.4 Adapt Customer views (Index, Create/Edit) for mobile
    - Stack fields vertically on phone with full-width inputs
    - _Requirements: 14.6_

  - [x] 6.5 Adapt Purchase views (Index, Create/Edit, Bulk Entry) for mobile
    - Stack inputs vertically on phone
    - Allow horizontal table scroll for bulk entry grids
    - _Requirements: 14.7_

  - [x] 6.6 Adapt Supplier views (Index, Dashboard) for mobile
    - Collapse KPI/chart grid to single column on phone
    - Horizontally scroll purchases table
    - _Requirements: 14.8_

  - [x] 6.7 Adapt VAT views (Periods Index, Detail) for mobile
    - Collapse meta grid and breakdown tables into scrollable containers on phone
    - _Requirements: 14.9_

  - [x] 6.8 Adapt Revenue Dashboard for mobile
    - Reflow KPI cards and chart containers into stacked layout on phone
    - _Requirements: 14.10_

  - [x] 6.9 Adapt Admin views (Audit Logs, System Logs, User Management, Module Access) for mobile
    - Horizontal scroll for data tables
    - Stack filter controls vertically on phone
    - _Requirements: 14.11_

  - [x] 6.10 Adapt remaining views (My Business, Credit Notes, Customer Statement) for mobile
    - My Business: single-column form fields on phone
    - Credit Notes: follow Invoice responsive pattern
    - Customer Statement: stacked filter panel, horizontally scrollable statement table
    - _Requirements: 14.12, 14.13, 14.14_

  - [x] 6.11 Verify tablet 2-column minimum across all views
    - Ensure all views maintain minimum 2-column layouts for form fields and side-by-side sections on tablet viewport
    - _Requirements: 14.15_

- [x] 7. Checkpoint - Full responsive verification
  - Ensure all views render correctly at phone (375px), tablet (810px), and desktop (1200px) viewports. Ensure no horizontal overflow on any page at phone width. Ask the user if questions arise.

- [ ] 8. Write Playwright test suites
  - [ ]* 8.1 Write Playwright test for element visibility by viewport (Suite 1)
    - At 375px: topbar visible, bottom tab bar visible, sidebar hidden, desktop account menu hidden
    - At 900px: topbar visible, bottom tab bar hidden, sidebar hidden
    - At 1200px: topbar hidden, bottom tab bar hidden, sidebar visible
    - _Requirements: 2.1, 2.3, 9.1, 9.4, 11.2_

  - [ ]* 8.2 Write Playwright test for off-canvas drawer interactions (Suite 2)
    - Click hamburger → drawer visible, backdrop visible
    - Click backdrop → drawer hidden, backdrop hidden
    - Click close button → drawer hidden
    - Click nav link → drawer closes
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

  - [ ]* 8.3 Write Playwright test for grid collapse rules (Suite 3)
    - At 375px: `.grid-4` is 1-column, `.gauge-row` is 2-column
    - At 900px: `.grid-4` is 2-column
    - At 1200px: `.grid-4` is 4-column
    - _Requirements: 6.1, 6.2, 6.6, 6.7_

  - [ ]* 8.4 Write Playwright test for table scroll behaviour (Suite 4)
    - At 375px: table container has `overflow-x: auto`
    - Scroll hint text visible
    - _Requirements: 5.1, 5.2_

  - [ ]* 8.5 Write Playwright test for action buttons (Suite 5)
    - At 375px: `.btn-primary` in content is full-width
    - At 900px: retains intrinsic width
    - _Requirements: 8.1, 8.2_

  - [ ]* 8.6 Write Playwright test for filter stacking (Suite 6)
    - At 375px: filter panel children stacked vertically
    - At 900px: allows side-by-side fields
    - _Requirements: 7.1, 7.2_

  - [ ]* 8.7 Write Playwright test for mobile account menu (Suite 7)
    - At 375px: click avatar → mobile dropdown appears
    - At 1200px: desktop account menu visible
    - _Requirements: 10.1, 10.2_

  - [ ]* 8.8 Write Playwright test for bottom tab bar (Suite 8)
    - At 375px: tab bar fixed at bottom, 4 items displayed, active tab matches route
    - Content has bottom padding
    - _Requirements: 9.1, 9.2, 9.3, 9.5_

  - [ ]* 8.9 Write Playwright test for desktop preservation (Suite 9)
    - At 1200px: grid is `280px 1fr`, sidebar visible, no mobile elements visible
    - Sidebar collapse/expand via existing toggle still works
    - _Requirements: 11.1, 11.2, 11.3, 11.4_

- [x] 9. Final checkpoint - Ensure all tests pass
  - Ensure all Playwright tests pass across all viewport configurations. Verify no regressions in existing desktop functionality. Ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation at reasonable breaks
- No property-based tests are included — the design explicitly identifies PBT as not applicable to this CSS/HTML/JS feature
- Testing uses example-based Playwright viewport tests as defined in the design
- All CSS rules reuse existing design system tokens (no new CSS variables introduced)
- The `mobile-nav.js` uses vanilla JS IIFE pattern consistent with existing codebase conventions
- No database changes, no new dependencies, no server-side logic changes

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1"] },
    { "id": 1, "tasks": ["1.2", "1.3", "1.4", "1.5", "2.2", "2.3", "2.4", "2.5", "2.6"] },
    { "id": 2, "tasks": ["2.7", "2.8", "2.9", "2.10", "2.11", "2.12"] },
    { "id": 3, "tasks": ["4.1", "4.2"] },
    { "id": 4, "tasks": ["6.1", "6.2", "6.3", "6.4", "6.5", "6.6", "6.7", "6.8", "6.9", "6.10"] },
    { "id": 5, "tasks": ["6.11"] },
    { "id": 6, "tasks": ["8.1", "8.2", "8.3", "8.4", "8.5", "8.6", "8.7", "8.8", "8.9"] }
  ]
}
```
