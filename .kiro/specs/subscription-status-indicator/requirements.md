# Requirements Document

## Introduction

The Subscription Status Indicator is a persistent UI element displayed in the sidebar of the authenticated layout. It shows the current business's subscription plan name and status (active, trialing, past_due, cancelled) so users can understand their subscription state at a glance without navigating to a dedicated billing page. The indicator uses color-coded badges aligned with the MyChair Design System and provides a direct link to the Stripe Customer Portal for self-service billing management.

## Glossary

- **Indicator**: The subscription status UI element rendered at the bottom of the sidebar navigation
- **Subscription_Status_Badge**: A color-coded pill element displaying the subscription status text
- **SubscriptionPlanService**: The existing service that resolves subscription access, plan name, and status for a given business
- **SubscriptionAccessResult**: The model returned by SubscriptionPlanService containing HasActiveSubscription, SubscriptionStatus, PlanName, and IncludedModules
- **Sidebar**: The left-hand navigation panel rendered by _Layout.cshtml on all authenticated pages
- **SuperAdmin**: A user role that bypasses subscription checks and may not have a business association
- **Three_Inventors_Account**: The business with BusinessId = 1 that has a permanent subscription
- **Stripe_Customer_Portal**: The Stripe-hosted page where customers manage billing, payment methods, and invoices

## Requirements

### Requirement 1: Display Subscription Indicator in Sidebar

**User Story:** As an authenticated user, I want to see my subscription plan name and status in the sidebar, so that I can understand my current subscription state at a glance on every page.

#### Acceptance Criteria

1. WHEN an authenticated user loads any page using the main layout, THE Indicator SHALL render at the bottom of the sidebar navigation area, below all navigation links
2. THE Indicator SHALL display the plan name retrieved from SubscriptionAccessResult.PlanName, truncated with an ellipsis if the text exceeds 20 characters
3. THE Indicator SHALL display the subscription status as a Subscription_Status_Badge positioned to the right of the plan name text on the same line
4. WHILE the sidebar is in its expanded state, THE Indicator SHALL display both the plan name text and the Subscription_Status_Badge
5. WHILE the sidebar is in its collapsed state, THE Indicator SHALL display only the Subscription_Status_Badge without the plan name text

### Requirement 2: Color-Code Status Badge by Subscription State

**User Story:** As an authenticated user, I want the status badge to be visually distinct for each subscription state, so that I can immediately recognise whether my subscription is healthy or requires attention.

#### Acceptance Criteria

1. WHEN SubscriptionAccessResult.SubscriptionStatus equals "active" (case-insensitive comparison), THE Subscription_Status_Badge SHALL render with background color #129867 (Success) and white (#FFFFFF) text displaying "Active"
2. WHEN SubscriptionAccessResult.SubscriptionStatus equals "trialing" (case-insensitive comparison), THE Subscription_Status_Badge SHALL render with background color #0D5EA6 (Primary Blue) and white (#FFFFFF) text displaying "Trial"
3. WHEN SubscriptionAccessResult.SubscriptionStatus equals "past_due" (case-insensitive comparison), THE Subscription_Status_Badge SHALL render with background color #C8912E (Warning) and white (#FFFFFF) text displaying "Past Due"
4. WHEN SubscriptionAccessResult.SubscriptionStatus equals "cancelled" (case-insensitive comparison), THE Subscription_Status_Badge SHALL render with background color #C24A4A (Danger) and white (#FFFFFF) text displaying "Cancelled"
5. IF SubscriptionAccessResult.SubscriptionStatus contains a value not matching any of "active", "trialing", "past_due", or "cancelled" (case-insensitive), THEN THE Subscription_Status_Badge SHALL render with background color #C24A4A (Danger) and white (#FFFFFF) text displaying "Unknown"

### Requirement 3: Link Indicator to Internal Billing Page

**User Story:** As an authenticated user, I want to click the subscription indicator to access my billing page, so that I can view invoices and manage my subscription without searching for the link.

#### Acceptance Criteria

1. WHEN the user clicks the Indicator, THE Indicator SHALL navigate the user to the internal billing page at `/Account/Billing` within the same browser tab
2. THE Indicator SHALL render as an anchor element with href set to `/Account/Billing`
3. WHILE the user does not have the "owner" role for the current business, THE Indicator SHALL render as a non-clickable element (no anchor tag or href) since billing access is restricted to business owners
4. WHEN the Indicator is rendered as a clickable link, THE anchor element SHALL include an aria-label attribute with the value "View billing and subscription"

### Requirement 4: Hide Indicator for SuperAdmin Without Business

**User Story:** As a SuperAdmin user without a business association, I want the indicator to be hidden, so that irrelevant subscription information does not clutter my interface.

#### Acceptance Criteria

1. WHILE the authenticated user has the SuperAdmin role AND the user's BusinessId claim is null, THE Indicator SHALL not render any HTML element in the sidebar
2. WHILE the authenticated user has the SuperAdmin role AND the user's BusinessId claim resolves to an existing business record, THE Indicator SHALL render in the sidebar showing that business's subscription status following the same display rules as non-SuperAdmin users
3. IF the authenticated user has the SuperAdmin role AND the user's BusinessId claim resolves to a business with no subscription record in the Subscription_Table, THEN THE Indicator SHALL not render any HTML element in the sidebar

### Requirement 5: Handle Missing Subscription Gracefully

**User Story:** As an authenticated user whose business has no subscription record, I want the indicator to show a meaningful state, so that I understand I need to subscribe rather than seeing a broken or empty element.

#### Acceptance Criteria

1. WHEN SubscriptionAccessResult.HasActiveSubscription is false AND SubscriptionAccessResult.PlanName is null or an empty string, THE Indicator SHALL display "No Plan" as the plan name text
2. WHEN SubscriptionAccessResult.SubscriptionStatus is null or an empty string, THE Subscription_Status_Badge SHALL render with background color #C24A4A (Danger) and white (#FFFFFF) text displaying "No Subscription"
3. IF SubscriptionAccessResult.HasActiveSubscription is false AND SubscriptionAccessResult.PlanName is null or an empty string AND SubscriptionAccessResult.SubscriptionStatus is null or an empty string, THEN THE Indicator SHALL remain visible in the sidebar at its standard position below all navigation links, rendering identically to a subscribed state in layout and dimensions
4. WHILE the sidebar is in its collapsed state AND SubscriptionAccessResult.SubscriptionStatus is null or an empty string, THE Indicator SHALL display only the Subscription_Status_Badge showing "No Subscription" without the plan name text

### Requirement 6: Implement as ViewComponent

**User Story:** As a developer, I want the subscription indicator implemented as a ViewComponent, so that it follows the established pattern for sidebar elements and can be invoked from the shared layout.

#### Acceptance Criteria

1. THE Indicator SHALL be implemented as an ASP.NET Core ViewComponent named SubscriptionStatusIndicatorViewComponent
2. WHEN the ViewComponent is invoked for an authenticated user with a valid BusinessId claim, THE SubscriptionStatusIndicatorViewComponent SHALL retrieve subscription data by calling SubscriptionPlanService.GetAccessAsync with the BusinessId claim value parsed as an integer
3. THE SubscriptionStatusIndicatorViewComponent SHALL be invoked in _Layout.cshtml within the sidebar element using `@await Component.InvokeAsync("SubscriptionStatusIndicator")`
4. THE SubscriptionStatusIndicatorViewComponent SHALL reuse the per-request cached SubscriptionAccessResult from SubscriptionPlanService without triggering additional database queries
5. IF the user is not authenticated OR the BusinessId claim is missing or cannot be parsed as a non-zero integer, THEN THE SubscriptionStatusIndicatorViewComponent SHALL return an empty Content result (rendering nothing in the sidebar)
6. THE SubscriptionStatusIndicatorViewComponent SHALL pass the SubscriptionAccessResult as the view model to its associated Razor view, exposing HasActiveSubscription, IsGraceAccess, SubscriptionStatus, and PlanName for rendering

### Requirement 7: Accessible Indicator Design

**User Story:** As a user relying on assistive technology, I want the subscription indicator to be accessible, so that I can understand my subscription status through screen readers.

#### Acceptance Criteria

1. THE Subscription_Status_Badge SHALL include an aria-label attribute with the format "Subscription status: {BadgeText}" where {BadgeText} is the visible badge text (e.g., "Active", "Trial", "Past Due", "Cancelled", "No Subscription")
2. WHEN the Indicator is rendered as a link, THE Indicator link SHALL include an aria-label attribute with the value "View billing and subscription"
3. IF the Indicator is rendered as a non-clickable element (user is not a business owner), THEN THE Indicator SHALL use a role="status" attribute and omit the link aria-label
4. THE Indicator SHALL meet WCAG 2.1 AA colour contrast requirements with a minimum contrast ratio of 4.5:1 between the badge text colour and the badge background colour for all status badge combinations
5. WHEN the Indicator is rendered as a link, THE Indicator SHALL be reachable via the Tab key, display a visible focus indicator when focused, and be activatable via the Enter key
