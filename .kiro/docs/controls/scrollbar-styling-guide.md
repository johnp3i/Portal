# Scrollbar Styling Guide

## Overview

All horizontally scrollable containers in the Portal use a custom-styled scrollbar that is thin, rounded, and uses the theme's blue accent colour. The native browser scrollbar (thick grey bar) must never appear.

## Applied To

The custom scrollbar styles are applied globally via CSS to:

- `.pipeline-board` — Kanban board on the Lead Board page
- `.table-responsive` — Any horizontally scrolling table wrapper (Bulk Entry, Purchase list, Invoice list, etc.)

Any new scrollable container should use one of these classes, or have the scrollbar styles explicitly added.

## Visual Specification

| Property | Value | Description |
|----------|-------|-------------|
| Height | 6px | Thin and unobtrusive |
| Track background | `rgba(13,94,166,.04)` | Nearly invisible blue tint |
| Track border-radius | 10px | Fully rounded |
| Thumb background | `rgba(13,94,166,.18)` | Soft blue, visible but subtle |
| Thumb border-radius | 10px | Pill-shaped |
| Thumb hover | `rgba(13,94,166,.32)` | Darker on hover for feedback |

## CSS Implementation

Located in `Portal.Web/wwwroot/css/site.css`:

```css
/* WebKit (Chrome, Safari, Edge) */
.pipeline-board::-webkit-scrollbar,
.table-responsive::-webkit-scrollbar {
    height: 6px;
}

.pipeline-board::-webkit-scrollbar-track,
.table-responsive::-webkit-scrollbar-track {
    background: rgba(13,94,166,.04);
    border-radius: 10px;
    margin: 0 8px;
}

.pipeline-board::-webkit-scrollbar-thumb,
.table-responsive::-webkit-scrollbar-thumb {
    background: rgba(13,94,166,.18);
    border-radius: 10px;
}

.pipeline-board::-webkit-scrollbar-thumb:hover,
.table-responsive::-webkit-scrollbar-thumb:hover {
    background: rgba(13,94,166,.32);
}

/* Firefox */
.pipeline-board,
.table-responsive {
    scrollbar-width: thin;
    scrollbar-color: rgba(13,94,166,.18) rgba(13,94,166,.04);
}
```

## When to Use

- Any container that has `overflow-x: auto` and may show a horizontal scrollbar
- Wrap tables in `<div class="table-responsive">` to get the styled scrollbar automatically
- For non-table scrollable areas (like the Kanban board), use the `.pipeline-board` class or add the scrollbar CSS to the container's class

## Rules

### DO

- Use `.table-responsive` wrapper for any table that might overflow
- Use `.pipeline-board` for kanban/card-based horizontal layouts
- Let the scrollbar appear naturally — do not hide it with `overflow: hidden`

### DON'T

- Don't use the default browser scrollbar (no unstyled `overflow-x: auto` without one of these classes)
- Don't create custom scrollbar styles inline — use the global classes
- Don't set scrollbar height larger than 6px — keep it thin
- Don't use different colours — always use the blue-tinted theme colours

## Adding to a New Component

If you create a new horizontally scrollable container that isn't a table or kanban board:

1. Add the same scrollbar CSS rules to `site.css` for your new class
2. Or reuse `.table-responsive` if it's a table wrapper
3. Always include both WebKit (`::-webkit-scrollbar`) and Firefox (`scrollbar-width`, `scrollbar-color`) properties

## File Location

CSS source: `Portal.Web/wwwroot/css/site.css` — section "CUSTOM SCROLLBAR"
