# Requirements Document

## Introduction

This feature introduces a structured subscription plan schema to the 3 Inventors Portal. The platform currently operates on an invitation-only model with no formal plan definitions, user count enforcement, or feature gating. This spec defines the database schema for subscription plans, plan-to-feature mappings, tenant-to-plan associations, and user limit enforcement within the InvitationService. The schema is designed for the current single "Business" tier while supporting future extensibility for Starter and Enterprise tiers.

## Glossary

- **Plan**: A subscription tier record defining pricing, billing cycle, user limits, and metadata for a subscription offering.
- **Plan_Feature**: A record associating a specific platform module with a Plan, defining which modules are accessible under that Plan.
- **Business**: The tenant entity representing a subscribing company within the platform (existing table in `[dbo]` schema).
- **Business_Plan**: The association between a Business (tenant) and their active Plan, including subscription lifecycle dates.
- **Invitation_Service**: The service responsible for creating invitation tokens and provisioning user-business relationships upon registration.
- **Portal_Module**: A discrete functional area of the platform (e.g., Quotations, Invoicing, Purchases) as defined in the PortalModules constant class.
- **User_Limit**: The maximum number of users permitted under a given Plan for a single Business tenant.
- **Seed_Data**: Pre-populated database records inserted during migration to establish the initial Business plan configuration.

## Requirements

### Requirement 1: Plan Table Schema

**User Story:** As a platform architect, I want a Plans table that defines subscription tiers with pricing, user limits, and metadata, so that the platform can support structured subscription management.

#### Acceptance Criteria

1. THE Plan table SHALL store a unique integer identity column as the primary key.
2. THE Plan table SHALL store a Name column (NVARCHAR(100), NOT NULL) representing the display name of the plan.
3. THE Plan table SHALL store a Slug column (NVARCHAR(50), NOT NULL, UNIQUE) representing a URL-safe identifier for the plan, restricted to lowercase alphanumeric characters and hyphens only (pattern: `^[a-z0-9]+(-[a-z0-9]+)*$`).
4. THE Plan table SHALL store a MonthlyPriceEur column (DECIMAL(10,2), NOT NULL, CHECK >= 0.00) representing the monthly equivalent price in euros, with a minimum value of 0.00.
5. THE Plan table SHALL store an AnnualPriceEur column (DECIMAL(10,2), NULL, CHECK >= 0.00 when not NULL) representing the annual price in euros when billed yearly, with a minimum value of 0.00 when provided.
6. THE Plan table SHALL store a MaxUsers column (INT, NOT NULL) representing the maximum number of users permitted under the plan, where the value must be -1 (indicating unlimited users) or a positive integer greater than or equal to 1.
7. THE Plan table SHALL store an IsActive column (BIT, NOT NULL, DEFAULT 1) indicating whether the plan is currently available for subscription.
8. THE Plan table SHALL store a DisplayOrder column (INT, NOT NULL) controlling the presentation sequence on pricing pages.
9. THE Plan table SHALL store a Description column (NVARCHAR(500), NULL) providing a short summary of the plan for display purposes.
10. THE Plan table SHALL store a CreatedAtUtc column (DATETIME, NOT NULL, DEFAULT GETUTCDATE()) recording when the plan record was created.
11. THE Plan table SHALL reside in the `[dbo]` schema.
12. THE Plan table SHALL store an UpdatedAtUtc column (DATETIME, NOT NULL, DEFAULT GETUTCDATE()) recording when the plan record was last modified.

### Requirement 2: Plan Feature Table Schema

**User Story:** As a platform architect, I want a PlanFeatures table that maps modules to plans, so that the platform can determine which features each subscription tier includes.

#### Acceptance Criteria

1. THE Plan_Feature table SHALL store an Id column (INT, IDENTITY, NOT NULL) as the primary key.
2. THE Plan_Feature table SHALL store a PlanId column (INT, NOT NULL) as a foreign key referencing the Plan table's Id column.
3. THE Plan_Feature table SHALL store a ModuleName column (NVARCHAR(50), NOT NULL) identifying the platform module included in the plan, where the value corresponds to one of the defined Portal_Module constants (customer, quotation, invoice, revenue, purchase, vat, credit, audit, products).
4. THE Plan_Feature table SHALL store an IsIncluded column (BIT, NOT NULL, DEFAULT 1) indicating whether the module is active for the plan.
5. THE Plan_Feature table SHALL enforce a unique constraint on the combination of PlanId and ModuleName to prevent duplicate module assignments.
6. THE Plan_Feature table SHALL store a CreatedAtUtc column (DATETIME, NOT NULL, DEFAULT GETUTCDATE()) recording when the feature mapping was created.
7. THE Plan_Feature table SHALL reside in the `[dbo]` schema.
8. WHEN a Plan record is deleted, THE database SHALL prevent the deletion if associated Plan_Feature records exist (NO ACTION referential integrity).
9. IF an INSERT or UPDATE violates the unique constraint on PlanId and ModuleName, THEN THE database SHALL reject the operation and return an error indicating a duplicate module assignment for the plan.

### Requirement 3: Business Plan Association

**User Story:** As a platform architect, I want a BusinessPlan table that links each tenant to their active subscription plan, so that the platform can determine a tenant's entitlements at runtime.

#### Acceptance Criteria

1. THE Business_Plan table SHALL store a unique integer identity column as the primary key.
2. THE Business_Plan table SHALL store a BusinessId column (INT, NOT NULL) as a foreign key referencing the Business table.
3. THE Business_Plan table SHALL store a PlanId column (INT, NOT NULL) as a foreign key referencing the Plan table.
4. THE Business_Plan table SHALL store a StartDateUtc column (DATETIME, NOT NULL) recording when the subscription became active.
5. THE Business_Plan table SHALL store an EndDateUtc column (DATETIME, NULL) recording when the subscription expires or was terminated.
6. THE Business_Plan table SHALL store an IsActive column (BIT, NOT NULL, DEFAULT 1) indicating whether this is the current active subscription for the business.
7. THE Business_Plan table SHALL store a CreatedAtUtc column (DATETIME, NOT NULL, DEFAULT GETUTCDATE()) recording when the association record was created.
8. THE Business_Plan table SHALL reside in the `[dbo]` schema.
9. THE Business_Plan table SHALL enforce a unique filtered constraint on the combination of BusinessId and IsActive where IsActive equals 1, ensuring each business has at most one active plan at any time.
10. WHEN a Business record is deleted, THE database SHALL prevent the deletion if associated Business_Plan records exist (NO ACTION referential integrity).
11. WHEN a Plan record is deleted, THE database SHALL prevent the deletion if associated Business_Plan records exist (NO ACTION referential integrity).
12. THE Business_Plan table SHALL include non-clustered indexes on the BusinessId and PlanId foreign key columns to support query performance for entitlement lookups.

### Requirement 4: Seed Data for Business Plan

**User Story:** As a platform operator, I want the initial Business plan seeded with all current modules, so that existing tenants continue operating without disruption after the schema is deployed.

#### Acceptance Criteria

1. WHEN the seed migration executes, THE Seed_Data SHALL insert a Plan record with Name "Business", Slug "business", MonthlyPriceEur 29.00, AnnualPriceEur 348.00, MaxUsers 5, IsActive 1, DisplayOrder 2, and Description NULL.
2. WHEN the seed migration executes, THE Seed_Data SHALL insert Plan_Feature records for all nine current Portal_Module values (customer, quotation, invoice, revenue, purchase, vat, credit, audit, products) with IsIncluded set to 1, referencing the Plan record by querying its PlanId from the Slug "business" rather than hardcoding an identity value.
3. WHEN the seed migration executes, THE Seed_Data SHALL be idempotent by checking for an existing Plan record with Slug "business" before inserting the Plan, and checking for existing PlanId and ModuleName combinations before inserting each Plan_Feature record, so that repeated execution produces no duplicate rows and no errors.
4. THE Seed_Data SHALL assign DisplayOrder 2 to the Business plan, leaving DisplayOrder values 1 and 3 unused so that future Starter and Enterprise tiers can be inserted without reordering existing records.

### Requirement 5: User Limit Enforcement in Invitation Service

**User Story:** As a super admin, I want the invitation process to reject new invitations when the tenant has reached their plan's user limit, so that subscription boundaries are respected.

#### Acceptance Criteria

1. WHEN a super admin creates an invitation, THE Invitation_Service SHALL retrieve the active Plan for the target Business and determine the MaxUsers value.
2. WHEN the current occupied seat count for the Business equals or exceeds the MaxUsers value, THE Invitation_Service SHALL reject the invitation and return a descriptive error message indicating the user limit has been reached.
3. IF the MaxUsers value is -1 (unlimited), THEN THE Invitation_Service SHALL permit the invitation without a seat count check.
4. THE Invitation_Service SHALL calculate the occupied seat count as the sum of active users (users with an active UserBusiness record for the target Business) plus pending invitations (invitations that are unused and not expired for the target Business).
5. IF no active Business_Plan record exists for the target Business, THEN THE Invitation_Service SHALL reject the invitation and return a descriptive error message indicating no active subscription was found.

### Requirement 6: Future Tier Extensibility

**User Story:** As a platform architect, I want the plan schema to accommodate future Starter and Enterprise tiers without schema changes, so that new plans can be added through data inserts alone.

#### Acceptance Criteria

1. THE Plan table schema SHALL allow inserting a new plan record (e.g., Name "Starter", Slug "starter", MonthlyPriceEur 9.00, AnnualPriceEur NULL, MaxUsers 1, IsActive 1, DisplayOrder 1) using a standard INSERT statement without requiring any ALTER TABLE, new column, or constraint modification.
2. THE Plan_Feature table schema SHALL allow inserting a subset of the available Portal_Module values for a given PlanId (e.g., 3 of 9 modules for a Starter plan) using standard INSERT statements without requiring schema modifications.
3. THE Plan table SHALL accept a MaxUsers value of 1 for single-user plans (Starter) and -1 for unlimited-user plans (Enterprise) using the existing INT column definition without constraint violations.
4. THE Plan table SHALL accept AnnualPriceEur as NULL for monthly-only plans and as a populated DECIMAL(10,2) value for plans offering annual billing, using the existing nullable column definition.
5. WHEN a new plan tier is inserted into the Plan table, THE Plan_Feature table SHALL accept INSERT statements associating any combination of one or more Portal_Module values with the new PlanId without requiring changes to the unique constraint or foreign key definitions.

### Requirement 7: Migration Script Standards

**User Story:** As a database administrator, I want the migration scripts to follow established Portal conventions, so that the schema changes integrate cleanly with the existing migration pipeline.

#### Acceptance Criteria

1. THE migration scripts SHALL use sequential three-digit zero-padded numbering with the format `NNN_PascalCaseDescription.sql` (next available number after the current highest migration in the target folder).
2. THE migration scripts SHALL wrap all DDL statements (CREATE TABLE, CREATE INDEX, ALTER TABLE ADD COLUMN, ADD CONSTRAINT) in IF NOT EXISTS checks using INFORMATION_SCHEMA, sys.indexes, sys.columns, or sys.foreign_keys queries to ensure idempotent execution.
3. THE migration scripts SHALL use the `[dbo]` schema for all new tables.
4. THE migration scripts SHALL define foreign key constraints with explicit constraint names following the pattern `FK_ChildTable_ParentTable`.
5. THE migration scripts SHALL include a nonclustered index on every foreign key column, named following the pattern `IX_TableName_ColumnName`.
6. THE migration scripts SHALL include a header comment block containing the migration number and filename, a description of the changes, related requirement references, and an idempotency statement.
7. THE migration scripts SHALL separate each DDL statement with a `GO` batch terminator.
8. THE migration scripts SHALL define primary key constraints with explicit names following the pattern `PK_TableName`.
