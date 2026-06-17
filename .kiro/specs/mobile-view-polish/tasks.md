# Implementation Plan: Mobile View Polish

## Overview

Systematic per-view audit and polish to ensure every Portal view renders cleanly on mobile using the established design policies (Layers 1–11). Each task involves reading the view's current markup, identifying elements that don't inherit policies (inline styles, missing classes, non-standard layouts), and applying the correct policy classes or targeted CSS overrides.

Reference: `.kiro/docs/Mobile_Design_Policies.md`

## Tasks

- [ ] 1. Dashboard view polish
  - [ ] 1.1 Verify gauge row renders as 2×2 grid — fix any inline styles that override `.gauge-row`
    - _Requirements: 1.1_

  - [ ] 1.2 Verify quotation stats strip renders as structured rows on phone
    - _Requirements: 1.2, 1.3_

  - [ ] 1.3 Verify charts stack vertically and fill width — fix any inline `height` or `width` that blocks responsive sizing
    - _Requirements: 1.4_

  - [ ] 1.4 Verify dashboard mini-tables use `.dashboard-card` card-list layout
    - _Requirements: 1.5_

  - [ ] 1.5 Verify quick action buttons render as 2-column tile grid
    - _Requirements: 1.6_

- [ ] 2. Invoice Index polish
  - [ ] 2.1 Verify filter panel collapses and toggle button works
    - _Requirements: 2.1, 2.2_

  - [ ] 2.2 Verify button row uses `.topbar-actions` compact layout
    - _Requirements: 2.3, 2.4_

  - [ ] 2.3 Verify table uses `.index-table` card layout with correct `data-mobile` attributes
    - _Requirements: 2.5, 2.6, 2.7_

- [ ] 3. Quotation Index polish
  - [ ] 3.1 Verify filter panel collapses and toggle button works
    - _Requirements: 3.1_

  - [ ] 3.2 Verify button row uses `.topbar-actions` compact layout
    - _Requirements: 3.2_

  - [ ] 3.3 Verify table uses `.index-table` card layout with correct `data-mobile` attributes
    - _Requirements: 3.3, 3.4, 3.5_

- [ ] 4. Invoice Detail polish
  - [ ] 4.1 Add `.topbar-actions` class to action button container
    - _Requirements: 4.1_

  - [ ] 4.2 Ensure invoice meta fields stack vertically on phone (fix inline flex containers)
    - _Requirements: 4.2_

  - [ ] 4.3 Ensure line items table uses `.table-responsive` with scroll or card layout
    - _Requirements: 4.3_

  - [ ] 4.4 Verify totals section renders full-width
    - _Requirements: 4.4_

  - [ ] 4.5 Verify status/action section renders correctly
    - _Requirements: 4.5_

- [ ] 5. Invoice Create/Edit polish
  - [ ] 5.1 Ensure all form fields use `.form-grid` or `.grid-2` for responsive collapse
    - _Requirements: 5.1, 5.5_

  - [ ] 5.2 Ensure line item entry grid stacks on phone
    - _Requirements: 5.2_

  - [ ] 5.3 Ensure "Add Line Item" button is full-width on phone
    - _Requirements: 5.3_

  - [ ] 5.4 Verify input types for date fields and customer select
    - _Requirements: 5.4_

- [ ] 6. Quotation Detail polish
  - [ ] 6.1 Add `.topbar-actions` class to action button container
    - _Requirements: 6.1_

  - [ ] 6.2 Ensure quotation meta fields stack vertically on phone
    - _Requirements: 6.2_

  - [ ] 6.3 Ensure line items and section breakdown render correctly
    - _Requirements: 6.3, 6.4_

- [ ] 7. Quotation Create/Edit polish
  - [ ] 7.1 Ensure all form fields collapse to single column on phone
    - _Requirements: 7.1, 7.5_

  - [ ] 7.2 Ensure line item grid stacks on phone
    - _Requirements: 7.2_

  - [ ] 7.3 Ensure Add Line/Section buttons full-width
    - _Requirements: 7.3_

  - [ ] 7.4 Verify catalog search usability on phone
    - _Requirements: 7.4_

- [ ] 8. Revenue Dashboard polish
  - [ ] 8.1 Ensure KPI cards render full-width stacked with accent bars on phone
    - _Requirements: 8.1, 8.4_

  - [ ] 8.2 Ensure action buttons full-width stacked
    - _Requirements: 8.2_

  - [ ] 8.3 Ensure receivables table uses row-card or scroll layout
    - _Requirements: 8.3_

- [ ] 9. Customer views polish
  - [ ] 9.1 Add `.index-table` or scroll layout to Customer Index table
    - _Requirements: 9.1_

  - [ ] 9.2 Ensure Customer Create/Edit form uses responsive grid
    - _Requirements: 9.2_

  - [ ] 9.3 Add filter toggle if filter panel exists
    - _Requirements: 9.3_

- [ ] 10. Purchase views polish
  - [ ] 10.1 Add `.index-table` or scroll layout to Purchase Index table
    - _Requirements: 10.1_

  - [ ] 10.2 Ensure Purchase Create/Edit form uses responsive grid
    - _Requirements: 10.2_

  - [ ] 10.3 Ensure Bulk Entry grid uses horizontal scroll
    - _Requirements: 10.3_

  - [ ] 10.4 Add filter toggle if filter panel exists
    - _Requirements: 10.4_

- [ ] 11. Supplier views polish
  - [ ] 11.1 Ensure Supplier Dashboard KPI cards render as 2-column grid
    - _Requirements: 11.1_

  - [ ] 11.2 Ensure purchases table uses horizontal scroll
    - _Requirements: 11.2_

  - [ ] 11.3 Add `.index-table` or scroll layout to Supplier Index table
    - _Requirements: 11.3_

- [ ] 12. VAT views polish
  - [ ] 12.1 Add row-card or scroll layout to VAT Periods Index table
    - _Requirements: 12.1_

  - [ ] 12.2 Ensure VAT Detail meta grid stacks vertically
    - _Requirements: 12.2_

  - [ ] 12.3 Ensure breakdown tables use horizontal scroll
    - _Requirements: 12.3_

  - [ ] 12.4 Verify VAT chart fills width
    - _Requirements: 12.4_

- [ ] 13. Credit Note views polish
  - [ ] 13.1 Add `.index-table` or scroll layout to Credit Note Index table
    - _Requirements: 13.1_

  - [ ] 13.2 Ensure Credit Note Detail stacks sections vertically
    - _Requirements: 13.2_

  - [ ] 13.3 Ensure Credit Note Create form uses responsive grid
    - _Requirements: 13.3_

  - [ ] 13.4 Ensure summary cards render as 2-column grid
    - _Requirements: 13.4_

- [ ] 14. Admin views polish
  - [ ] 14.1 Ensure all Admin tables use `.table-responsive` with scroll-hint
    - _Requirements: 14.1_

  - [ ] 14.2 Ensure filter controls stack vertically
    - _Requirements: 14.2_

  - [ ] 14.3 Ensure action buttons full-width on phone
    - _Requirements: 14.3_

- [ ] 15. Business Profile polish
  - [ ] 15.1 Ensure Business Profile form uses responsive grid (single column phone, 2-column tablet)
    - _Requirements: 15.1, 15.3_

  - [ ] 15.2 Ensure logo upload section renders full-width
    - _Requirements: 15.2_

- [ ] 16. Customer Statement polish
  - [ ] 16.1 Ensure filter panel stacks vertically on phone
    - _Requirements: 16.1_

  - [ ] 16.2 Ensure statement table uses horizontal scroll with hint
    - _Requirements: 16.2_

  - [ ] 16.3 Ensure summary totals render full-width
    - _Requirements: 16.3_

## Notes

- Each task starts by reading the current view HTML to identify which policy classes are missing
- Changes are primarily additive: adding CSS classes, `data-mobile` attributes, or wrapping elements
- No business logic changes — only markup and CSS adjustments
- All changes must preserve existing desktop rendering (>1100px)
- Reference the design policies in `.kiro/docs/Mobile_Design_Policies.md` for target behaviour
- Test at 375px (phone) and 810px (tablet) in Chrome DevTools

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "1.4", "1.5"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3", "3.1", "3.2", "3.3"] },
    { "id": 2, "tasks": ["4.1", "4.2", "4.3", "4.4", "4.5", "5.1", "5.2", "5.3", "5.4"] },
    { "id": 3, "tasks": ["6.1", "6.2", "6.3", "7.1", "7.2", "7.3", "7.4"] },
    { "id": 4, "tasks": ["8.1", "8.2", "8.3", "9.1", "9.2", "9.3"] },
    { "id": 5, "tasks": ["10.1", "10.2", "10.3", "10.4", "11.1", "11.2", "11.3"] },
    { "id": 6, "tasks": ["12.1", "12.2", "12.3", "12.4", "13.1", "13.2", "13.3", "13.4"] },
    { "id": 7, "tasks": ["14.1", "14.2", "14.3", "15.1", "15.2", "16.1", "16.2", "16.3"] }
  ]
}
```
