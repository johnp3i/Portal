# Implementation Plan: Expense Category Templates

## Overview

Adds a platform-wide category template library managed by SuperAdmin. Business users can import templates into their own expense categories via a one-click modal on the Categories page.

## Tasks

- [ ] 1. Database migration and seed
  - [ ] 1.1 Create migration for `[purchase].[ExpenseCategoryTemplate]` table (Id, Name, Description, IsActive, CreatedAtUtc)
  - [ ] 1.2 Create seed script with 15 common categories

- [ ] 2. Entity and DbContext
  - [ ] 2.1 Create `ExpenseCategoryTemplate` entity
  - [ ] 2.2 Register in PortalDbContext (no global query filter — platform-wide)
  - [ ] 2.3 Configure entity (table mapping, constraints, defaults)

- [ ] 3. Repository
  - [ ] 3.1 Create `ExpenseCategoryTemplateRepository` with: GetAllActiveAsync, GetAllAsync (including inactive), InsertAsync, UpdateAsync, DeactivateAsync, ReactivateAsync

- [ ] 4. Service layer
  - [ ] 4.1 Create `IExpenseCategoryTemplateService` / `ExpenseCategoryTemplateService`
    - GetActiveTemplatesAsync (for user import modal)
    - GetAllTemplatesAsync (for SuperAdmin management)
    - CreateAsync, UpdateAsync, DeactivateAsync, ReactivateAsync (SuperAdmin)
    - ImportTemplatesAsync(businessId, templateIds[]) — creates ExpenseCategory records, skips duplicates

- [ ] 5. SuperAdmin management UI
  - [ ] 5.1 Create controller endpoint or add to existing Admin controller
  - [ ] 5.2 Create management page: table of templates, add/edit/deactivate/reactivate actions
  - [ ] 5.3 Add navigation entry under Administration section

- [ ] 6. Business user import UI
  - [ ] 6.1 Add "Import from Templates" button to ExpenseCategory Index page
  - [ ] 6.2 Create modal: list of active templates with checkboxes, "Already added" indicator, Import button
  - [ ] 6.3 AJAX endpoint for import: accepts template IDs, creates categories, returns count
  - [ ] 6.4 Show SweetAlert2 result: "X categories imported, Y skipped (already exist)"

- [ ] 7. DI registration
  - [ ] 7.1 Register repository and service in Program.cs

- [ ] 8. Verification
  - [ ] 8.1 Verify SuperAdmin can CRUD templates
  - [ ] 8.2 Verify business user can import without duplicating
  - [ ] 8.3 Verify import is additive only (no existing categories modified)

## Notes

- Templates are platform-wide (no BusinessId) — SuperAdmin manages for all businesses
- Import is a copy operation — no ongoing relationship between template and business category
- Duplicate detection by exact name match (case-insensitive)
- The seed script provides 15 common categories covering most SME expense types
- No plan gating — available to all tiers (Foundation feature)

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3"] },
    { "id": 2, "tasks": ["3.1"] },
    { "id": 3, "tasks": ["4.1"] },
    { "id": 4, "tasks": ["5.1", "5.2", "5.3", "6.1", "6.2", "6.3", "6.4"] },
    { "id": 5, "tasks": ["7.1"] },
    { "id": 6, "tasks": ["8.1", "8.2", "8.3"] }
  ]
}
```
