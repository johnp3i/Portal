# Implementation Plan: Pipeline Mobile Responsive

## Overview

Add a mobile-responsive layer to the `/Sales/Pipeline` page using CSS media queries and JavaScript enhancements. All changes are client-side only — CSS `@media (max-width: 768px)` rules in an inline `<style>` block within `Pipeline.cshtml`, plus JS additions in `pipeline.js` and `follow-up-tasks.js` for stage pill navigation, KPI toggle, and swipe gestures.

## Tasks

- [x] 1. Add inline style block and FAB element to Pipeline.cshtml
  - [x] 1.1 Add the inline `<style>` block with all `@media (max-width: 768px)` rules to Pipeline.cshtml
    - Add the complete CSS block before the topbar containing: topbar responsive rules (flex-direction column, 28px heading, hide desktop New Lead button), filter panel full-width stacking (scoped to `#pipelineFilters` to avoid affecting modals), task card layout adaptation (flex-wrap, buttons below title, 44px touch targets), kanban board horizontal snap-scroll, stage pill nav display, meeting cards vertical stack, KPI footer 2x2 grid with compact/expanded states, FAB positioning with `env(safe-area-inset-bottom)` for notched devices, 44px min touch targets on all interactive elements, 16px content padding, and desktop-hide rules for `.stage-pill-nav` and `.pipeline-fab`
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 2.2, 2.3, 3.1, 3.2, 4.3, 4.4, 4.5, 5.1, 5.2, 6.4, 7.1, 7.2, 7.3, 8.1, 8.2, 8.3_

  - [x] 1.2 Add the FAB HTML element, filter section ID, and KPI section `id` attribute to Pipeline.cshtml
    - Insert the `<button class="pipeline-fab">` element with the "+" SVG icon and `onclick="openCreateLeadModal()"` before the `@section Scripts` block
    - Add `id="pipelineFilters"` to the filter section's `.glass.card-pad` container so CSS can scope filter styles without affecting modals
    - Add `id="kpiFooterSection"` and `class="kpi-footer"` to the KPI footer section's inner `<div>` so JS can target it for expand/collapse
    - Add `kpi-value` and `kpi-label` classes to the KPI value and label elements for CSS targeting
    - _Requirements: 4.1, 4.2, 3.3, 2.1_

  - [x] 1.3 Bump the pipeline.js version query string in the script tag
    - Change `pipeline.js?v=9` to `pipeline.js?v=10` for cache-busting after JS changes
    - _Requirements: 6.1, 9.1_

- [x] 2. Implement stage pill navigator and KPI toggle in pipeline.js
  - [x] 2.1 Add `renderStagePillNav(stages)` function to pipeline.js
    - Create a function that generates a `<div id="stagePillNav" class="stage-pill-nav">` with one `<button class="stage-pill">` per stage, coloured with the stage's colour, and inserts it before `#pipelineBoard`
    - Each pill gets an `onclick` handler calling `scrollToStage(index)` which calls `scrollIntoView({ behavior: 'smooth', inline: 'start' })` on the corresponding `.pipeline-column`
    - Call `renderStagePillNav(result.data)` inside `loadPipelineData()` after `renderKanban(result.data)` completes
    - _Requirements: 6.1, 6.2, 6.3_

  - [x] 2.2 Add `initKpiToggle()` function to pipeline.js
    - Get `#kpiFooterSection` element; if not found, return early
    - Attach a click listener that checks `window.innerWidth <= 768` and toggles the `.kpi-expanded` class on the section
    - Call `initKpiToggle()` at the end of the DOMContentLoaded handler
    - _Requirements: 3.3_

- [x] 3. Implement swipe gesture handler in pipeline.js
  - [x] 3.1 Add `initSwipeGesture(cardElement)` function to pipeline.js
    - Guard: if `window.innerWidth > 768` or `!('ontouchstart' in window)`, return immediately (no-op on desktop)
    - Add `transition: transform 0.2s ease` to card element for smooth snap animation (remove during drag, restore on touchend)
    - Attach `touchstart` (record `startX`, set `isSwiping = true`, remove transition), `touchmove` (calculate deltaX, if `> 40px` threshold apply `translateX` transform up to `revealWidth` of 140px), and `touchend` (restore transition, close any OTHER `.swipe-revealed` cards first, if deltaX > threshold snap open with `translateX(-140px)` and add `.swipe-revealed` class; otherwise snap closed to `translateX(0)` and remove class) — all with `{ passive: true }`
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

  - [x] 3.2 Wire swipe gesture into task card rendering in pipeline.js
    - After the Today's Actions section renders task cards into `#todaysActionsList`, query all task card elements and call `initSwipeGesture(card)` on each
    - Add a hidden action panel (Complete + Snooze buttons) positioned behind each card using `position: absolute; right: 0` that becomes visible when the card is swiped open
    - Ensure only one card can be in "revealed" state at a time (close others on new swipe)
    - _Requirements: 9.1, 9.2_

- [x] 4. Checkpoint — Verify mobile layout
  - Ensure all CSS rules render correctly at 375px and 768px viewports. Verify FAB shows on mobile and hides on desktop, stage pills appear and scroll the board, KPI section toggles between compact and expanded, task cards show buttons below title, and swipe gesture reveals action panel. Ask the user if questions arise.

- [x] 5. Integration and final wiring
  - [x] 5.1 Verify desktop regression — no visual changes above 768px
    - Confirm all mobile-only elements (`.stage-pill-nav`, `.pipeline-fab`) have `display: none` by default and only appear inside the `@media` block
    - Confirm task card layout remains inline on desktop (no flex-wrap applied)
    - Confirm KPI toggle click handler is gated by `window.innerWidth <= 768`
    - _Requirements: 1.5, 4.5, 6.4, 9.4_

  - [ ]* 5.2 Write unit tests for stage pill navigation
    - Test that `renderStagePillNav()` creates one button per stage with correct text and colour styling
    - Test that `scrollToStage(index)` calls `scrollIntoView` on the correct `.pipeline-column` element
    - _Requirements: 6.1, 6.2, 6.3_

  - [ ]* 5.3 Write unit tests for swipe gesture handler
    - Test that swipe of < 40px does NOT apply translateX transform
    - Test that swipe of ≥ 40px applies `translateX(-140px)` and adds `.swipe-revealed` class
    - Test that swipe handler does not attach when `window.innerWidth > 768`
    - _Requirements: 9.1, 9.3, 9.4_

  - [ ]* 5.4 Write unit tests for KPI expand/collapse
    - Test that clicking KPI section adds `.kpi-expanded` class when viewport ≤ 768px
    - Test that clicking KPI section does NOT toggle class when viewport > 768px
    - _Requirements: 3.3_

- [x] 6. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- No property-based tests — this feature is CSS/DOM/touch interactions with no pure functions suitable for PBT
- All changes are CSS + JS only; no server-side modifications, no new endpoints, no database changes
- Swipe gesture handler is in pipeline.js (same file as Today's Actions rendering) — NOT in follow-up-tasks.js (which is the standalone Tasks page)
- The design uses concrete JavaScript, so the implementation language is JavaScript (vanilla, matching the existing codebase)
- Filter panel CSS is scoped to `#pipelineFilters` to avoid affecting modal dialogs
- FAB uses `env(safe-area-inset-bottom)` for notched device compatibility (iPhone X+)
- Swipe includes CSS transition (`transform 0.2s ease`) for smooth snap animation
- Only one task card can be in "revealed" swipe state at a time (others auto-close)

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["1.3", "2.1", "2.2", "3.1"] },
    { "id": 2, "tasks": ["3.2"] },
    { "id": 3, "tasks": ["4"] },
    { "id": 4, "tasks": ["5.1", "5.2", "5.3", "5.4"] },
    { "id": 5, "tasks": ["6"] }
  ]
}
```
