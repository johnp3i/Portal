# Dashboard Onboarding & Quick Actions — Requirements

## Overview

Improve the Dashboard experience for new and returning users by:
1. Adding a "Getting Started" checklist card for first-time users
2. Redesigning the quick action shortcuts as a prominent, intentional section
3. Adding a "Help" link in the sidebar footer

## Requirements

### R1: Getting Started Card (first login experience)

- Show a collapsible card on the Dashboard titled "Getting Started"
- Only visible when the business has incomplete setup items
- Checklist items (each links to the relevant page):
  - Complete your business profile → `/MyBusiness`
  - Create your first customer → `/Customer`
  - Create your first quotation → `/Quotation/Create`
  - Issue your first invoice → `/Invoice`
  - Record a payment → `/Revenue`
- Each item shows ✅ (green check) when completed, ○ (circle) when pending
- Completion is determined by data existence (has customers? has quotations? has invoices? has payments?)
- Card is dismissible (user can hide it permanently via a "Dismiss" button)
- Dismissal stored in localStorage (`portal_onboarding_dismissed`)
- Once ALL items are complete, the card auto-hides

### R2: Quick Actions — Redesign as "Action Hub"

Replace the current flat button row with a styled card section:
- Title: "Quick Actions" with a subtle lightning icon
- Buttons displayed as icon+label cards in a responsive grid (2-3 columns)
- Each action card: icon on top, label below, subtle hover effect
- Actions:
  - New Quotation → `/Quotation/Create`
  - Create Invoice → `/Invoice/Create`
  - New Customer → `/Customer` (with create modal trigger)
  - Record Payment → `/Revenue`
  - Record Purchase → `/Purchase/Create`
  - Customer Statement → `/Statement`
- Positioned below the KPI cards, above the Getting Started card (if visible)
- Always visible (not first-login only)

### R3: Help Link in Sidebar

- Add a help icon + "Help" text link at the bottom of the sidebar navigation
- Positioned above the SubscriptionStatusIndicator component
- Links to `/Help` (a dedicated help/guide page) or opens a help modal
- Uses a question mark circle icon
- Visible in expanded sidebar, icon-only when collapsed (with tooltip)

### R4: Help Page (lightweight)

- Route: `/Help` or `/GettingStarted`
- Content:
  - Visual workflow: Quotation → Invoice → Payment → VAT
  - Section-by-section one-paragraph explanations
  - Links to each section
- No heavy documentation — just orientation
- Accessible to all authenticated users

## Out of Scope (for now)

- Video tutorials
- Interactive product tours / tooltips
- Per-page guided walkthroughs
- AI-powered help chat
