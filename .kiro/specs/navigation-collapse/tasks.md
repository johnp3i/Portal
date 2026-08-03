# Implementation Plan: Collapsible Navigation Sections

## Overview

Adds expand/collapse functionality to sidebar navigation section headers with localStorage persistence. Purely frontend — no backend changes needed.

## Tasks

- [x] 1. Update navigation partial view
  - [x] 1.1 Add chevron toggle icon to each `.nav-title` section header
  - [x] 1.2 Wrap each section's sub-items in a container div with a data attribute (e.g., `data-nav-section="finance"`)
  - [x] 1.3 Add CSS classes for collapsed state (`.nav-section-collapsed`) with `max-height:0; overflow:hidden; transition`

- [x] 2. JavaScript toggle logic
  - [x] 2.1 Add click handler on section headers to toggle collapsed class
  - [x] 2.2 Rotate chevron icon on toggle (CSS transform)
  - [x] 2.3 Save collapsed/expanded state to localStorage on each toggle
  - [x] 2.4 On page load, read localStorage and apply saved state

- [x] 3. Active section auto-expand
  - [x] 3.1 On page load, detect which nav item has `.active` class
  - [x] 3.2 If the active item's parent section is collapsed, expand it (without saving to localStorage)

- [x] 4. Visual polish
  - [x] 4.1 Ensure collapsed sections maintain proper spacing/margins
  - [x] 4.2 Test with sidebar collapsed mode (icon-only) — chevrons should hide in collapsed sidebar
  - [x] 4.3 Test all section states across page navigation

## Notes

- No backend changes — entirely frontend (CSS + JS in the navigation partial)
- localStorage key: `portalNavState` storing a JSON object like `{"finance":false,"purchasing":true}`
- Sections without sub-items (if any) should not show a chevron
- The sidebar collapse (icon-only mode) should override — when sidebar is collapsed, all sections are hidden anyway

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3", "2.4"] },
    { "id": 2, "tasks": ["3.1", "3.2"] },
    { "id": 3, "tasks": ["4.1", "4.2", "4.3"] }
  ]
}
```
