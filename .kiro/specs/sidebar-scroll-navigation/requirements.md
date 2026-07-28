# Requirements Document

## Introduction

The navigation sidebar in the Portal application has grown to contain many module links across multiple sections (Pipeline, Sales, Finance, Purchasing, Catalog, Documents, Administration, Account). Users must scroll the entire page to access navigation items at the bottom of the sidebar, and the sidebar scroll position resets on every page navigation because the entire layout reloads. This feature makes the sidebar a fixed, independently scrollable region that persists across page navigations without full reloads.

## Glossary

- **Sidebar**: The `<aside class="sidebar">` element containing the brand logo, business identity card, module navigation links, and account section, positioned on the left side of the viewport.
- **Main_Content**: The `<main class="content">` element containing the page-specific content rendered by each controller action.
- **Navigation_State**: The current scroll position within the Sidebar and the expanded/collapsed state of the sidebar toggle.
- **ViewComponent**: An ASP.NET Core MVC component that renders a reusable UI fragment (e.g., ModuleNavigation, BusinessIdentityCard).
- **SPA_Navigation**: A navigation approach where only the Main_Content area is replaced on page transitions, without reloading the Sidebar or layout shell.

## Requirements

### Requirement 1: Fixed Sidebar Positioning

**User Story:** As a user, I want the sidebar to remain fixed on the left side of the viewport, so that I always have access to navigation links regardless of how far I scroll the main content.

#### Acceptance Criteria

1. THE Sidebar SHALL remain fixed to the left edge of the viewport at all times while the user is authenticated.
2. THE Sidebar SHALL span the full height of the viewport from top to bottom.
3. WHILE the Main_Content is scrolled, THE Sidebar SHALL remain stationary and unaffected by the scroll position of the Main_Content.
4. THE Sidebar SHALL maintain its fixed position on viewports wider than 768px (desktop/tablet landscape).

### Requirement 2: Independent Sidebar Scrolling

**User Story:** As a user, I want to scroll through all navigation items within the sidebar without affecting the main content scroll position, so that I can reach navigation links at the bottom of a long menu.

#### Acceptance Criteria

1. WHEN the Sidebar content exceeds the viewport height, THE Sidebar SHALL display a vertical scrollbar within its own boundaries.
2. THE Sidebar SHALL allow independent vertical scrolling confined to its own content area.
3. WHILE the user scrolls within the Sidebar, THE Main_Content scroll position SHALL remain unchanged.
4. WHILE the user scrolls within the Main_Content, THE Sidebar scroll position SHALL remain unchanged.
5. THE Sidebar SHALL use `overflow-y: auto` so that the scrollbar only appears when content overflows.

### Requirement 3: Persistent Sidebar Across Navigation

**User Story:** As a user, I want the sidebar to remain in place without reloading when I navigate between pages, so that my scroll position and visual context are preserved.

#### Acceptance Criteria

1. WHEN the user clicks a navigation link within the Sidebar, THE Sidebar SHALL NOT reload or re-render.
2. WHEN the user navigates between pages, THE Navigation_State (scroll position) SHALL be preserved.
3. WHEN the user navigates between pages, THE Main_Content area SHALL update to reflect the new page content without affecting the Sidebar.
4. IF a full page reload occurs (browser refresh or direct URL entry), THEN THE Sidebar SHALL restore from scratch but retain the collapsed/expanded state from localStorage.

### Requirement 4: Independent Main Content Scrolling

**User Story:** As a user, I want the main content area to scroll independently from the sidebar, so that long pages do not interfere with my navigation access.

#### Acceptance Criteria

1. THE Main_Content SHALL scroll independently within its own boundaries.
2. THE Main_Content SHALL occupy the remaining viewport width to the right of the Sidebar.
3. THE Main_Content SHALL span the full viewport height and use `overflow-y: auto` for vertical scrolling.
4. WHEN the page content exceeds the viewport height, THE Main_Content SHALL display its own vertical scrollbar.

### Requirement 5: Sidebar Collapse Compatibility

**User Story:** As a user, I want the sidebar fixed/scrollable behavior to work correctly in both expanded (280px) and collapsed (64px) states.

#### Acceptance Criteria

1. WHILE the Sidebar is in expanded state (280px), THE Sidebar SHALL remain fixed and independently scrollable.
2. WHILE the Sidebar is in collapsed state (64px), THE Sidebar SHALL remain fixed and independently scrollable.
3. WHEN the user toggles the Sidebar between collapsed and expanded states, THE fixed positioning and scroll behavior SHALL continue to function correctly.
4. WHEN the Sidebar is toggled, THE Main_Content width SHALL adjust to fill the remaining viewport space.

### Requirement 6: Scroll Position Preservation via JavaScript

**User Story:** As a user, I want my sidebar scroll position to be remembered as I navigate between pages, so that I do not have to re-scroll to find my current section every time.

#### Acceptance Criteria

1. WHEN the user clicks a navigation link, THE Sidebar SHALL save its current scroll position to sessionStorage before navigation occurs.
2. WHEN a page loads, THE Sidebar SHALL restore its scroll position from sessionStorage.
3. IF no saved scroll position exists in sessionStorage, THEN THE Sidebar SHALL start at scroll position zero (top).
4. WHEN the user performs a hard refresh (Ctrl+F5), THE Sidebar scroll position in sessionStorage SHALL be used to restore position.
