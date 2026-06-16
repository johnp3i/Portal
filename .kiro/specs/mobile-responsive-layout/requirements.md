# Requirements Document

## Introduction

The Portal platform is a multi-tenant back-office application (ASP.NET Core MVC 8) currently designed exclusively for desktop browsers. The existing layout uses a fixed CSS grid (`grid-template-columns: 280px 1fr`) with a single `@media (max-width:1100px)` breakpoint that hides the sidebar entirely, leaving mobile users without navigation. This feature introduces a fully responsive layout that enables phone and tablet users to access all platform functionality through adaptive navigation, content reflow, and touch-friendly UI components. The implementation is purely additive — existing desktop behaviour remains unchanged.

## Glossary

- **Mobile_Stylesheet**: A dedicated CSS file (`/css/mobile.css`) loaded after `site.css` that contains all responsive overrides for phone and tablet viewports
- **Phone_Viewport**: A screen width of 768px or less
- **Tablet_Viewport**: A screen width between 769px and 1100px (inclusive)
- **Mobile_Topbar**: A sticky header bar displayed on Phone_Viewport and Tablet_Viewport containing the hamburger button, logo, and account avatar
- **Off_Canvas_Drawer**: A slide-in navigation panel that enters from the left side of the screen, overlaying the page content with a backdrop
- **Backdrop**: A semi-transparent overlay behind the Off_Canvas_Drawer that blocks interaction with page content
- **Hamburger_Button**: A button rendered in the Mobile_Topbar that triggers the Off_Canvas_Drawer to open
- **Bottom_Tab_Bar**: A fixed bar at the bottom of the screen providing quick access to primary modules on Phone_Viewport
- **Scroll_Hint**: A visible text indicator informing the user that a table can be scrolled horizontally
- **Portal_Layout**: The `_Layout.cshtml` shared layout file and its associated CSS that defines the application shell (sidebar + content grid)

## Requirements

### Requirement 1: Mobile Stylesheet Loading

**User Story:** As a developer, I want responsive styles isolated in a dedicated stylesheet loaded after the main CSS, so that desktop styles remain untouched and mobile overrides are maintainable.

#### Acceptance Criteria

1. THE Portal_Layout SHALL include a reference to `/css/mobile.css` after the existing `/css/site.css` stylesheet link
2. THE Mobile_Stylesheet SHALL contain all responsive rules for Phone_Viewport and Tablet_Viewport without duplicating or modifying rules in `site.css`
3. THE Mobile_Stylesheet SHALL define two breakpoints: a max-width of 768px for Phone_Viewport and a range of 769px to 1100px for Tablet_Viewport

### Requirement 2: Mobile Topbar

**User Story:** As a mobile user, I want a sticky top navigation bar with menu access, branding, and my account, so that I can navigate the platform without scrolling back to the top.

#### Acceptance Criteria

1. WHILE the viewport width is at or below 1100px, THE Mobile_Topbar SHALL be visible and sticky at the top of the screen
2. THE Mobile_Topbar SHALL display the Hamburger_Button on the left, the platform logo centered, and the account avatar on the right
3. WHILE the viewport width is above 1100px, THE Mobile_Topbar SHALL be hidden
4. THE Mobile_Topbar SHALL use a semi-transparent white background with backdrop blur to maintain readability over scrolled content

### Requirement 3: Off-Canvas Drawer Navigation

**User Story:** As a mobile user, I want to open and close a slide-in sidebar menu, so that I can access all navigation items without losing the context of the current page.

#### Acceptance Criteria

1. WHEN the user taps the Hamburger_Button, THE Off_Canvas_Drawer SHALL slide in from the left edge with a 280px width and the Backdrop SHALL become visible
2. WHEN the user taps the Backdrop, THE Off_Canvas_Drawer SHALL close and the Backdrop SHALL be hidden
3. WHEN the user taps the close button inside the Off_Canvas_Drawer, THE Off_Canvas_Drawer SHALL close and the Backdrop SHALL be hidden
4. WHEN the user navigates to a new page via a link in the Off_Canvas_Drawer, THE Off_Canvas_Drawer SHALL close automatically
5. THE Off_Canvas_Drawer SHALL contain the same navigation items as the desktop sidebar, including module links, workspace links, and account-level links
6. THE Off_Canvas_Drawer SHALL animate with a cubic-bezier transition lasting approximately 300ms

### Requirement 4: Responsive Content Area

**User Story:** As a mobile user, I want the main content area to use the full screen width with appropriate padding, so that I have maximum space to view data on small screens.

#### Acceptance Criteria

1. WHILE the viewport is Phone_Viewport, THE content area SHALL span the full viewport width with 16px horizontal padding
2. WHILE the viewport is Tablet_Viewport, THE content area SHALL span the full viewport width with 18px horizontal padding
3. WHILE the viewport is Phone_Viewport or Tablet_Viewport, THE existing desktop sidebar SHALL be hidden from the grid layout

### Requirement 5: Horizontally Scrollable Tables

**User Story:** As a mobile user, I want data tables to scroll horizontally rather than breaking their layout, so that I can view all columns by swiping.

#### Acceptance Criteria

1. WHILE the viewport is Phone_Viewport or Tablet_Viewport, THE Mobile_Stylesheet SHALL wrap all data tables in a horizontally scrollable container with momentum scrolling enabled
2. WHEN a table is scrollable, THE Scroll_Hint text SHALL be visible above the table indicating horizontal scroll availability
3. THE table content SHALL maintain its desktop column structure and minimum width to preserve data readability

### Requirement 6: Responsive Grid Layouts

**User Story:** As a mobile user, I want KPI cards and form fields to reflow into fewer columns on smaller screens, so that each item is legible without horizontal scrolling.

#### Acceptance Criteria

1. WHILE the viewport is Tablet_Viewport, THE `.grid-4` layout SHALL collapse to a 2-column grid
2. WHILE the viewport is Phone_Viewport, THE `.grid-4` layout SHALL collapse to a 1-column grid
3. WHILE the viewport is Tablet_Viewport, THE `.grid-3` layout SHALL collapse to a 2-column grid
4. WHILE the viewport is Phone_Viewport, THE `.grid-3` layout SHALL collapse to a 1-column grid
5. WHILE the viewport is Phone_Viewport, THE `.grid-2` and `.form-grid` layouts SHALL collapse to a 1-column grid
6. WHILE the viewport is Phone_Viewport, THE `.gauge-row` (KPI dashboard row) SHALL collapse to a 2-column grid
7. WHILE the viewport is Tablet_Viewport, THE `.gauge-row` SHALL collapse to a 2-column grid

### Requirement 7: Stacked Filters

**User Story:** As a mobile user, I want filter controls to stack vertically, so that each input is full-width and easy to interact with on touch screens.

#### Acceptance Criteria

1. WHILE the viewport is Phone_Viewport, THE filter panels SHALL arrange all filter fields and buttons in a single vertical column with full-width inputs
2. WHILE the viewport is Tablet_Viewport, THE filter panels SHALL wrap filter fields into rows that accommodate two fields side-by-side where space permits
3. WHILE the viewport is Phone_Viewport, THE filter buttons SHALL display at full width in a stacked or evenly-split row layout

### Requirement 8: Full-Width Action Buttons

**User Story:** As a mobile user, I want primary action buttons to span the full width of the screen, so that they are easy to tap without precise targeting.

#### Acceptance Criteria

1. WHILE the viewport is Phone_Viewport, THE primary action buttons (`.btn-primary`, `.btn-green`, `.btn-danger`) within page content SHALL expand to full width
2. WHILE the viewport is Tablet_Viewport, THE primary action buttons SHALL retain their intrinsic width

### Requirement 9: Bottom Tab Bar

**User Story:** As a mobile user, I want quick access to the most-used modules via a bottom navigation bar, so that I can switch between key sections with one tap.

#### Acceptance Criteria

1. WHILE the viewport is Phone_Viewport, THE Bottom_Tab_Bar SHALL be visible and fixed to the bottom of the screen
2. THE Bottom_Tab_Bar SHALL display icons and labels for the primary modules: Dashboard, Quotations, Invoices, and Revenue
3. THE Bottom_Tab_Bar SHALL highlight the currently active module
4. WHILE the viewport is above Phone_Viewport, THE Bottom_Tab_Bar SHALL be hidden
5. WHILE the Bottom_Tab_Bar is visible, THE page content SHALL include bottom padding equal to the height of the Bottom_Tab_Bar to prevent content from being obscured

### Requirement 10: Account Menu Repositioning

**User Story:** As a mobile user, I want my account menu accessible from the mobile header, so that I can sign out or access account settings without opening the full sidebar.

#### Acceptance Criteria

1. WHILE the viewport is Phone_Viewport or Tablet_Viewport, THE desktop account menu (absolute-positioned in content area) SHALL be hidden
2. WHEN the user taps the account avatar in the Mobile_Topbar, THE account dropdown menu SHALL appear below the avatar
3. THE mobile account dropdown SHALL contain the same options as the desktop account dropdown: signed-in identity, billing link (for owners), and sign-out action

### Requirement 11: Desktop Layout Preservation

**User Story:** As a desktop user, I want the existing layout to remain completely unchanged, so that my workflow is not disrupted by the mobile enhancements.

#### Acceptance Criteria

1. WHILE the viewport width is above 1100px, THE Portal_Layout SHALL render the desktop grid (`grid-template-columns: 280px 1fr`) with the sidebar visible
2. WHILE the viewport width is above 1100px, THE Mobile_Topbar, Off_Canvas_Drawer, Backdrop, and Bottom_Tab_Bar SHALL be hidden
3. THE Mobile_Stylesheet SHALL use media queries exclusively and SHALL NOT override any desktop styles at viewports above 1100px
4. THE existing sidebar collapse/expand toggle (localStorage-persisted) SHALL continue to function on desktop viewports

### Requirement 12: Vanilla JavaScript Interaction

**User Story:** As a developer, I want mobile navigation toggling implemented with vanilla JavaScript, so that no additional frameworks or libraries are introduced.

#### Acceptance Criteria

1. THE hamburger open, drawer close, and backdrop tap interactions SHALL be implemented using vanilla JavaScript event listeners
2. THE implementation SHALL NOT introduce any JavaScript framework, library, or dependency beyond what is already loaded in the Portal_Layout
3. THE implementation SHALL reuse the existing sidebar toggle pattern (`classList.toggle`) already present in the Portal_Layout

### Requirement 13: Minimal HTML Additions

**User Story:** As a developer, I want the HTML changes limited to the addition of the mobile topbar and bottom tab bar elements, so that the existing view structure is preserved.

#### Acceptance Criteria

1. THE Portal_Layout SHALL add a Mobile_Topbar element containing the Hamburger_Button, logo, and avatar
2. THE Portal_Layout SHALL add a Bottom_Tab_Bar element containing the module quick-access links
3. THE Portal_Layout SHALL add a Backdrop overlay element for the Off_Canvas_Drawer
4. IF any other existing HTML elements require modification, THEN THE modification SHALL be limited to adding CSS classes or data attributes — existing structure and behaviour SHALL remain intact

### Requirement 14: Per-View Responsive Adaptation

**User Story:** As a mobile user, I want every page in the platform to be usable on my phone or tablet, so that no view is broken or unusable on smaller screens.

#### Acceptance Criteria

1. THE Dashboard view SHALL reflow KPI cards, charts, and summary widgets into a single or two-column layout on Phone_Viewport, with charts resizing to fit the available width
2. THE Quotation Index view SHALL display a responsive table with horizontal scroll and accessible action buttons on each row
3. THE Quotation Detail view SHALL stack the detail sections (header, line items, totals, actions) vertically with full-width cards on Phone_Viewport
4. THE Quotation Create/Edit view SHALL present the line-item form grid in a single-column layout on Phone_Viewport with full-width inputs and an "Add Line" button at full width
5. THE Invoice Index, Detail, and Create/Edit views SHALL follow the same responsive behaviour as their Quotation counterparts
6. THE Customer Index and Create/Edit views SHALL stack fields vertically on Phone_Viewport with full-width inputs
7. THE Purchase Index, Create/Edit, and Bulk Entry views SHALL stack inputs vertically on Phone_Viewport and allow horizontal table scroll for bulk entry grids
8. THE Supplier Index and Dashboard views SHALL collapse the KPI/chart grid to single column on Phone_Viewport and horizontally scroll the purchases table
9. THE VAT Periods Index and Detail views SHALL collapse the meta grid and breakdown tables into scrollable containers on Phone_Viewport
10. THE Revenue Dashboard view SHALL reflow KPI cards and chart containers into a stacked layout on Phone_Viewport
11. THE Admin views (Audit Logs, System Logs, User Management, Module Access) SHALL use horizontal scroll for their data tables and stack filter controls vertically on Phone_Viewport
12. THE My Business / Business Profile view SHALL present the form fields in a single-column layout on Phone_Viewport
13. THE Credit Note views (Index, Detail, Create) SHALL follow the same responsive pattern as Invoice views
14. THE Customer Statement view SHALL stack the filter panel vertically and allow the statement table to scroll horizontally on Phone_Viewport
15. WHILE the viewport is Tablet_Viewport, ALL views SHALL maintain a minimum of 2-column layouts for form fields and side-by-side sections where the desktop uses 2 or more columns
