# Requirements Document

## Introduction

The /Sales/Pipeline page currently has mobile responsiveness issues that prevent effective use on small screens (< 768px). The primary pain point is the Today's Actions section where task cards have inline action buttons (Notes, Edit, Unprocessed, Complete, Snooze) that overflow on narrow viewports. This feature introduces a mobile-responsive layer following native-app design patterns so the Pipeline page looks and feels like a native mobile application on small screens.

## Glossary

- **Pipeline_Page**: The /Sales/Pipeline view that displays the sales pipeline with Kanban board, table view, KPIs, today's actions, upcoming meetings, and recent lead activity
- **Task_Card**: A rendered card within the Today's Actions panel showing a follow-up task with its type icon, title, urgency badge, and action buttons (Notes, Edit, Unprocessed, Complete, Snooze)
- **Action_Buttons**: The set of interactive buttons on each Task_Card that allow the user to mark as unprocessed, complete, snooze, edit, or view notes
- **Mobile_Viewport**: A screen width of 768px or less, matching the application's CSS breakpoint for mobile devices
- **KPI_Footer**: The section at the bottom of the Pipeline_Page displaying key metrics: Total Active Leads, This Month, Conversion Rate, and Avg Days to Won
- **Kanban_Board**: The horizontally-scrollable board displaying lead cards grouped by pipeline stage columns
- **Stage_Pill_Navigator**: A compact horizontal row of tappable stage indicators placed above the Kanban_Board for quick scrolling to a specific stage
- **Floating_Action_Button**: A fixed-position circular button anchored to the bottom-right corner of the Mobile_Viewport that provides quick access to lead creation
- **Touch_Target**: An interactive element that meets the minimum 44x44 CSS pixel tap area recommended for accessible mobile interfaces
- **Filter_Panel**: The section containing Product and Assigned To dropdown filters and their action buttons

## Requirements

### Requirement 1: Task Card Mobile Layout

**User Story:** As a sales user on a mobile device, I want the Today's Actions task cards to display action buttons below the task title instead of inline, so that buttons do not overflow or get clipped on narrow screens.

#### Acceptance Criteria

1. WHILE the Pipeline_Page is rendered in a Mobile_Viewport, THE Task_Card SHALL stack the Action_Buttons below the task title and metadata row instead of displaying them inline to the right
2. WHILE the Pipeline_Page is rendered in a Mobile_Viewport, THE Task_Card SHALL display the Unprocessed and Complete buttons as compact icon-only buttons with tooltip text
3. WHILE the Pipeline_Page is rendered in a Mobile_Viewport, THE Task_Card SHALL truncate the task title text with an ellipsis when the title exceeds the available width
4. WHILE the Pipeline_Page is rendered in a Mobile_Viewport, THE Task_Card SHALL ensure all Action_Buttons meet the minimum 44x44 CSS pixel Touch_Target size
5. WHILE the Pipeline_Page is rendered in a viewport wider than 768px, THE Task_Card SHALL display Action_Buttons inline to the right of the task content as per the current desktop layout

### Requirement 2: Filter Panel Mobile Responsiveness

**User Story:** As a sales user on a mobile device, I want the filter dropdowns to be full-width, so that I can easily interact with them without horizontal overflow.

#### Acceptance Criteria

1. WHILE the Pipeline_Page is rendered in a Mobile_Viewport, THE Filter_Panel SHALL render the Product and Assigned To dropdowns at full viewport width stacked vertically
2. WHILE the Pipeline_Page is rendered in a Mobile_Viewport, THE Filter_Panel SHALL render the Filter and Clear buttons at full width stacked below the dropdowns
3. WHILE the Pipeline_Page is rendered in a Mobile_Viewport, THE Filter_Panel dropdown controls SHALL meet the minimum 44px height Touch_Target requirement

### Requirement 3: KPI Footer Mobile Compactness

**User Story:** As a sales user on a mobile device, I want the KPI metrics to display in a compact layout that does not consume excessive vertical space, so that I can see key metrics quickly.

#### Acceptance Criteria

1. WHILE the Pipeline_Page is rendered in a Mobile_Viewport, THE KPI_Footer SHALL display all four metrics in a single compact row using a 2x2 grid layout
2. WHILE the Pipeline_Page is rendered in a Mobile_Viewport, THE KPI_Footer SHALL reduce the metric value font size to 20px and the label font size to 10px
3. WHEN a user taps the KPI_Footer in a Mobile_Viewport, THE KPI_Footer SHALL expand to show full-size metric values with their labels

### Requirement 4: Floating Action Button for Lead Creation

**User Story:** As a sales user on a mobile device, I want a floating action button for creating a new lead, so that lead creation is always accessible without scrolling to the top of the page.

#### Acceptance Criteria

1. WHILE the Pipeline_Page is rendered in a Mobile_Viewport, THE Pipeline_Page SHALL display a Floating_Action_Button fixed to the bottom-right corner of the viewport with a "+" icon
2. WHEN the user taps the Floating_Action_Button, THE Pipeline_Page SHALL open the Create Lead modal
3. WHILE the Pipeline_Page is rendered in a Mobile_Viewport, THE Floating_Action_Button SHALL have a minimum size of 56x56 CSS pixels to ensure reliable tap interaction
4. WHILE the Pipeline_Page is rendered in a Mobile_Viewport, THE Pipeline_Page SHALL hide the "New Lead" button in the topbar to avoid duplicate entry points
5. WHILE the Pipeline_Page is rendered in a viewport wider than 768px, THE Pipeline_Page SHALL hide the Floating_Action_Button and display the topbar "New Lead" button

### Requirement 5: Upcoming Meetings Mobile Layout

**User Story:** As a sales user on a mobile device, I want the Upcoming Meetings cards to be full-width and vertically stacked, so that meeting details are readable without horizontal scrolling.

#### Acceptance Criteria

1. WHILE the Pipeline_Page is rendered in a Mobile_Viewport, THE Pipeline_Page SHALL render Upcoming Meetings cards at full viewport width stacked vertically
2. WHILE the Pipeline_Page is rendered in a Mobile_Viewport, THE Pipeline_Page SHALL ensure meeting card tap areas meet the minimum 44px Touch_Target height

### Requirement 6: Kanban Board Stage Pill Navigation

**User Story:** As a sales user on a mobile device, I want pill-shaped stage indicators above the Kanban board, so that I can quickly navigate to a specific pipeline stage without manually scrolling.

#### Acceptance Criteria

1. WHILE the Pipeline_Page is rendered in a Mobile_Viewport, THE Pipeline_Page SHALL display a Stage_Pill_Navigator row above the Kanban_Board containing one pill per pipeline stage
2. WHEN the user taps a pill in the Stage_Pill_Navigator, THE Kanban_Board SHALL scroll horizontally to bring the corresponding stage column into view
3. WHILE the Pipeline_Page is rendered in a Mobile_Viewport, THE Stage_Pill_Navigator SHALL use the stage colour for each pill indicator
4. WHILE the Pipeline_Page is rendered in a viewport wider than 768px, THE Pipeline_Page SHALL hide the Stage_Pill_Navigator

### Requirement 7: Touch-Friendly Spacing and Interactions

**User Story:** As a sales user on a mobile device, I want all interactive elements to have adequate touch-friendly spacing, so that I can tap controls accurately without accidental presses.

#### Acceptance Criteria

1. WHILE the Pipeline_Page is rendered in a Mobile_Viewport, THE Pipeline_Page SHALL ensure all interactive elements (buttons, links, dropdown controls) have a minimum Touch_Target size of 44x44 CSS pixels
2. WHILE the Pipeline_Page is rendered in a Mobile_Viewport, THE Pipeline_Page SHALL add a minimum gap of 8px between adjacent interactive elements to prevent accidental taps
3. WHILE the Pipeline_Page is rendered in a Mobile_Viewport, THE Pipeline_Page SHALL apply 16px horizontal padding to the main content area to prevent content from touching screen edges

### Requirement 8: Topbar Mobile Responsiveness

**User Story:** As a sales user on a mobile device, I want the page topbar to adapt to narrow screens with appropriate sizing and spacing, so that the page header remains readable and functional.

#### Acceptance Criteria

1. WHILE the Pipeline_Page is rendered in a Mobile_Viewport, THE Pipeline_Page SHALL reduce the topbar heading font size to 28px
2. WHILE the Pipeline_Page is rendered in a Mobile_Viewport, THE Pipeline_Page SHALL stack the Board/Table view toggle buttons below the page title
3. WHILE the Pipeline_Page is rendered in a Mobile_Viewport, THE Pipeline_Page SHALL render the Board and Table toggle buttons at a minimum height of 44px

### Requirement 9: Task Card Swipe Gesture for Quick Actions

**User Story:** As a sales user on a mobile device, I want to swipe left on a task card to reveal quick action buttons, so that I can complete or snooze tasks with a natural mobile gesture.

#### Acceptance Criteria

1. WHILE the Pipeline_Page is rendered in a Mobile_Viewport, WHEN the user swipes left on a Task_Card, THE Task_Card SHALL reveal a hidden action panel containing Complete and Snooze buttons
2. WHILE the Pipeline_Page is rendered in a Mobile_Viewport, WHEN the user swipes right on a revealed Task_Card action panel, THE Task_Card SHALL close the action panel and return to its default state
3. WHILE the Pipeline_Page is rendered in a Mobile_Viewport, THE Task_Card swipe interaction SHALL require a minimum horizontal drag of 40px before activating to prevent accidental triggers
4. WHILE the Pipeline_Page is rendered in a viewport wider than 768px, THE Task_Card SHALL not respond to swipe gestures
