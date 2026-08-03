# Requirements Document: Expense Category Templates

## Introduction

New businesses often don't know which expense categories to create. This feature provides a set of pre-defined category templates managed by the SuperAdmin. Business users can browse these templates and import selected ones into their own business — saving setup time and encouraging consistent categorisation across the platform.

## Glossary

- **Category Template**: A pre-defined expense category record managed at the platform level by SuperAdmin
- **Import**: The action of copying a template into the business's own `ExpenseCategory` table
- **Business Category**: An existing `ExpenseCategory` record owned by a specific business

## Requirements

### Requirement 1: Template Data Model

**User Story:** As a SuperAdmin, I want a dedicated table for category templates, so that they exist independently of any business's categories.

#### Acceptance Criteria

1. THE database SHALL contain a `[purchase].[ExpenseCategoryTemplate]` table with columns: Id (INT IDENTITY PK), Name (NVARCHAR(100) NOT NULL), Description (NVARCHAR(500) NULL), IsActive (BIT NOT NULL DEFAULT 1), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE()).
2. THE table SHALL be seeded with common categories: Office Supplies, Utilities, Rent & Property, Professional Services, Travel & Transport, Software & Subscriptions, Insurance, Marketing & Advertising, Bank Fees & Charges, Equipment & Maintenance, Telecommunications, Training & Development, Staff Expenses, Cleaning & Hygiene, Legal & Compliance.
3. THE template records SHALL NOT be scoped to any business — they are platform-wide.

### Requirement 2: SuperAdmin Management

**User Story:** As a SuperAdmin, I want to add, edit, and deactivate category templates, so that I can keep the template library relevant and up-to-date.

#### Acceptance Criteria

1. THE SuperAdmin panel SHALL include a "Category Templates" management page.
2. THE SuperAdmin SHALL be able to create new templates (Name, Description).
3. THE SuperAdmin SHALL be able to edit existing templates (Name, Description).
4. THE SuperAdmin SHALL be able to deactivate templates (soft-hide from user import list).
5. THE SuperAdmin SHALL be able to reactivate deactivated templates.
6. ONLY SuperAdmin users SHALL have access to template management.

### Requirement 3: Business User Import

**User Story:** As a business user, I want to browse available category templates and import the ones relevant to my business, so that I don't have to create common categories from scratch.

#### Acceptance Criteria

1. THE Expense Categories page SHALL include an "Import from Templates" button.
2. WHEN clicked, A modal SHALL display all active templates with checkboxes.
3. TEMPLATES already imported (matching name exists in business categories) SHALL be shown as "Already added" (disabled checkbox).
4. THE user SHALL be able to select multiple templates and click "Import Selected".
5. WHEN imported, EACH selected template SHALL create a new `ExpenseCategory` record for the business with the template's Name.
6. THE import SHALL NOT duplicate — if a category with the same name already exists, it SHALL be skipped with a count message.

### Requirement 4: No Retroactive Impact

**User Story:** As a business with existing categories, I want template imports to be purely additive and never modify my existing categories.

#### Acceptance Criteria

1. IMPORTING templates SHALL NOT modify, rename, or delete any existing business categories.
2. THE relationship between templates and imported categories SHALL be one-way (copy at import time, no ongoing link).
3. AFTER import, THE business category is fully independent — editing or deleting it does not affect the template.
