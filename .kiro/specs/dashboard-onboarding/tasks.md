# Dashboard Onboarding & Quick Actions — Tasks

## Task 1: Add completion flags to Dashboard controller
- Query database for existence of: customers, quotations, invoices, payments, business profile
- Pass as ViewBag booleans to the Dashboard view
- Also pass `ViewBag.UserFirstName` from claims or Identity

## Task 2: Redesign Quick Actions section
- Replace the current flat button row with styled icon+label cards
- Use a responsive grid (auto-fill, minmax 140px)
- Add CSS for `.quick-action-card` hover/active states
- Actions: New Quotation, Create Invoice, New Customer, Record Payment, Record Purchase, Customer Statement

## Task 3: Add Getting Started card
- Add below Quick Actions section
- Collapsible `<details>` element
- Show checklist with completion status (✅ / ○)
- Each item links to the relevant page
- "Dismiss" button stores dismissal in localStorage
- Hide card if dismissed OR all items complete
- JS: check localStorage on load, hide if dismissed

## Task 4: Add Help link to sidebar
- Add a nav-item link to `/Help` in `_Layout.cshtml`
- Position above the SubscriptionStatusIndicator
- Question mark circle icon
- Collapsed sidebar: icon only with tooltip

## Task 5: Create Help page
- Route: `/Help` (HomeController or dedicated HelpController)
- Simple, clean page with:
  - Workflow diagram (Quotation → Invoice → Payment → VAT)
  - Section explanations (one paragraph each)
  - Links to each module's main page
- No heavy docs — just orientation

## Task 6: Verify and test
- New user (no data): Getting Started visible with all items unchecked
- Existing user (has data): Getting Started items checked, card auto-hides when all done
- Dismissed card stays hidden across page loads
- Quick Actions work on all screen sizes
- Help page loads correctly
