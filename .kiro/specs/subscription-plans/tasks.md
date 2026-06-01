# Implementation Plan: Subscription Plans

## Overview

This plan implements the subscription plan schema for the 3 Inventors Portal, adding three new tables (`Plan`, `PlanFeature`, `BusinessPlan`), EF Core entity models, repository classes, service-layer user limit enforcement in `InvitationService`, and comprehensive property-based and unit tests. Tasks follow the established Portal pattern: Migration → Entity → DbContext → Repository → Service → Tests.

## Tasks

- [x] 1. Create migration scripts for subscription plan tables
  - [x] 1.1 Create migration `073_CreatePlanTable.sql`
    - Create `[dbo].[Plan]` table with all columns: Id (INT IDENTITY PK), Name (NVARCHAR(100)), Slug (NVARCHAR(50) UNIQUE), MonthlyPriceEur (DECIMAL(10,2)), AnnualPriceEur (DECIMAL(10,2) NULL), MaxUsers (INT), IsActive (BIT DEFAULT 1), DisplayOrder (INT), Description (NVARCHAR(500) NULL), CreatedAtUtc (DATETIME DEFAULT GETUTCDATE()), UpdatedAtUtc (DATETIME DEFAULT GETUTCDATE())
    - Include CHECK constraints: CK_Plan_MonthlyPriceEur (>= 0.00), CK_Plan_AnnualPriceEur (NULL OR >= 0.00), CK_Plan_MaxUsers (-1 OR >= 1)
    - Include unique constraint UX_Plan_Slug on Slug column
    - Wrap all DDL in IF NOT EXISTS guards, use GO batch terminators, include header comment block
    - _Requirements: 1.1–1.12, 7.1–7.8_

  - [x] 1.2 Create migration `074_CreatePlanFeatureTable.sql`
    - Create `[dbo].[PlanFeature]` table with columns: Id (INT IDENTITY PK), PlanId (INT NOT NULL FK), ModuleName (NVARCHAR(50) NOT NULL), IsIncluded (BIT DEFAULT 1), CreatedAtUtc (DATETIME DEFAULT GETUTCDATE())
    - Include FK constraint FK_PlanFeature_Plan referencing Plan(Id) with NO ACTION delete
    - Include unique constraint UX_PlanFeature_PlanId_ModuleName on (PlanId, ModuleName)
    - Include nonclustered index IX_PlanFeature_PlanId on PlanId
    - Wrap all DDL in IF NOT EXISTS guards, use GO batch terminators, include header comment block
    - _Requirements: 2.1–2.9, 7.1–7.8_

  - [x] 1.3 Create migration `075_CreateBusinessPlanTable.sql`
    - Create `[dbo].[BusinessPlan]` table with columns: Id (INT IDENTITY PK), BusinessId (INT NOT NULL FK), PlanId (INT NOT NULL FK), StartDateUtc (DATETIME NOT NULL), EndDateUtc (DATETIME NULL), IsActive (BIT DEFAULT 1), CreatedAtUtc (DATETIME DEFAULT GETUTCDATE())
    - Include FK constraints: FK_BusinessPlan_Business referencing Business(Id) NO ACTION, FK_BusinessPlan_Plan referencing Plan(Id) NO ACTION
    - Include filtered unique index UX_BusinessPlan_BusinessId_IsActive on (BusinessId, IsActive) WHERE IsActive = 1
    - Include nonclustered indexes: IX_BusinessPlan_BusinessId, IX_BusinessPlan_PlanId
    - Wrap all DDL in IF NOT EXISTS guards, use GO batch terminators, include header comment block
    - _Requirements: 3.1–3.12, 7.1–7.8_

  - [x] 1.4 Create migration `076_SeedBusinessPlan.sql`
    - Insert Plan record: Name "Business", Slug "business", MonthlyPriceEur 29.00, AnnualPriceEur 348.00, MaxUsers 5, IsActive 1, DisplayOrder 2, Description NULL
    - Insert 9 PlanFeature records (customer, quotation, invoice, revenue, purchase, vat, credit, audit, products) with IsIncluded = 1, referencing PlanId by querying Slug "business"
    - Make script idempotent: check for existing Plan with Slug "business" before insert, check existing PlanId+ModuleName before each PlanFeature insert
    - Include header comment block with idempotency statement
    - _Requirements: 4.1–4.4, 7.1–7.8_

- [x] 2. Create EF Core entity models and DbContext configuration
  - [x] 2.1 Create entity classes for Plan, PlanFeature, and BusinessPlan
    - Create `Portal.Infrastructure/Entities/Plan.cs` with all properties and navigation collections (PlanFeatures, BusinessPlans)
    - Create `Portal.Infrastructure/Entities/PlanFeature.cs` with all properties and navigation to Plan
    - Create `Portal.Infrastructure/Entities/BusinessPlan.cs` with all properties and navigation to Business and Plan
    - Follow existing entity conventions (namespace Portal.Infrastructure.Entities, null-forgiving initializers)
    - _Requirements: 1.1–1.12, 2.1–2.7, 3.1–3.8_

  - [x] 2.2 Add DbSet properties and configuration to PortalDbContext
    - Add `DbSet<Plan>`, `DbSet<PlanFeature>`, `DbSet<BusinessPlan>` properties to PortalDbContext
    - Add private static configuration methods: ConfigurePlan, ConfigurePlanFeature, ConfigureBusinessPlan
    - Configure all column mappings, constraints, indexes, relationships, and delete behaviors as specified in the design
    - Call configuration methods from OnModelCreating
    - _Requirements: 1.1–1.12, 2.1–2.9, 3.1–3.12_

- [x] 3. Checkpoint - Verify schema and model alignment
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Implement repository layer
  - [x] 4.1 Create PlanRepository
    - Create `Portal.Infrastructure/Repositories/PlanRepository.cs` extending `GenericStoredProcedureRepository<Plan>`
    - Implement `GetBySlugAsync(string slug)` using direct SQL with full table names (no aliases)
    - Implement `GetByIdAsync(int id)` using direct SQL
    - Implement `GetAllActiveAsync()` returning active plans ordered by DisplayOrder
    - Follow repository standards: try/catch with rethrow, SqlParameter for all params, null-safe with DBNull.Value
    - _Requirements: 1.1–1.12, 6.1_

  - [x] 4.2 Create PlanFeatureRepository
    - Create `Portal.Infrastructure/Repositories/PlanFeatureRepository.cs` extending `GenericStoredProcedureRepository<PlanFeature>`
    - Implement `GetByPlanIdAsync(int planId)` using direct SQL with full table names
    - Follow repository standards: try/catch with rethrow, SqlParameter for all params
    - _Requirements: 2.1–2.7_

  - [x] 4.3 Create BusinessPlanRepository
    - Create `Portal.Infrastructure/Repositories/BusinessPlanRepository.cs` extending `GenericStoredProcedureRepository<BusinessPlan>`
    - Implement `GetActiveByBusinessIdAsync(int businessId)` using direct SQL with INNER JOIN to Plan table to eagerly load Plan.MaxUsers
    - Use full table names in SQL (no aliases), follow repository standards
    - _Requirements: 3.1–3.9, 5.1_

- [x] 5. Implement service layer - user limit enforcement
  - [x] 5.1 Modify InvitationService to enforce user limits
    - Add `IBusinessPlanRepository` dependency via constructor injection
    - In `CreateInvitationAsync`, before existing logic: query active BusinessPlan for the target businessId
    - If no active plan exists, throw `InvalidOperationException` with descriptive message
    - If MaxUsers == -1, skip seat check and proceed
    - If MaxUsers > 0, count active UserBusiness records + pending (unused, unexpired) Invitations for the business
    - If occupiedSeats >= MaxUsers, throw `InvalidOperationException` with descriptive message including counts
    - Register `IBusinessPlanRepository` in DI container
    - _Requirements: 5.1–5.5_

- [x] 6. Checkpoint - Verify repositories and service compile correctly
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Write property-based tests for subscription plan logic
  - [x] 7.1 Write property test for slug format validation
    - **Property 1: Slug format validation**
    - Generate random strings (valid slugs matching `^[a-z0-9]+(-[a-z0-9]+)*$` + invalid strings with uppercase, spaces, special chars, leading/trailing hyphens, consecutive hyphens)
    - Verify the validation logic accepts valid slugs and rejects invalid ones
    - Place in `Portal.Tests/PropertyBased/PlanSlugValidationPropertyTests.cs`
    - **Validates: Requirements 1.3**

  - [x] 7.2 Write property test for price constraint validation
    - **Property 2: Price constraint validation**
    - Generate random decimals (positive, zero, negative, null for AnnualPriceEur)
    - Verify MonthlyPriceEur accepted iff >= 0.00, AnnualPriceEur accepted iff NULL or >= 0.00
    - Place in `Portal.Tests/PropertyBased/PlanPriceValidationPropertyTests.cs`
    - **Validates: Requirements 1.4, 1.5**

  - [x] 7.3 Write property test for MaxUsers constraint validation
    - **Property 3: MaxUsers constraint validation**
    - Generate random integers (including -1, 0, negatives, positives)
    - Verify MaxUsers accepted iff value == -1 or value >= 1
    - Place in `Portal.Tests/PropertyBased/PlanMaxUsersValidationPropertyTests.cs`
    - **Validates: Requirements 1.6**

  - [x] 7.4 Write property test for ModuleName validation
    - **Property 4: ModuleName validation**
    - Generate random strings (valid module names from PortalModules.All + invalid strings)
    - Verify ModuleName accepted iff it is a member of the defined set (customer, quotation, invoice, revenue, purchase, vat, credit, audit, products)
    - Place in `Portal.Tests/PropertyBased/PlanFeatureModuleNamePropertyTests.cs`
    - **Validates: Requirements 2.3**

  - [x] 7.5 Write property test for single active plan per business
    - **Property 5: Single active plan per business**
    - Generate random business IDs with multiple plan insertion attempts
    - Verify that at most one BusinessPlan with IsActive = 1 exists per business at any time
    - Place in `Portal.Tests/PropertyBased/BusinessPlanSingleActivePropertyTests.cs`
    - **Validates: Requirements 3.9**

  - [x] 7.6 Write property test for user limit enforcement
    - **Property 6: User limit enforcement**
    - Generate random tuples of (maxUsers, activeUsers, pendingInvitations)
    - Verify invitation permitted iff MaxUsers == -1 OR (activeUsers + pendingInvitations) < MaxUsers
    - Place in `Portal.Tests/PropertyBased/UserLimitEnforcementPropertyTests.cs`
    - **Validates: Requirements 5.2, 5.3**

  - [x] 7.7 Write property test for seat count calculation
    - **Property 7: Seat count calculation**
    - Generate random combinations of active users and pending invitations
    - Verify occupied seat count always equals activeUserCount + pendingInvitationCount
    - Place in `Portal.Tests/PropertyBased/SeatCountCalculationPropertyTests.cs`
    - **Validates: Requirements 5.4**

- [x] 8. Write unit tests for InvitationService user limit enforcement
  - [x] 8.1 Write unit tests for InvitationService plan enforcement
    - Test: no active plan returns InvalidOperationException with "no active subscription plan" message
    - Test: MaxUsers == -1 (unlimited) permits invitation without seat check
    - Test: occupiedSeats < MaxUsers permits invitation
    - Test: occupiedSeats == MaxUsers rejects invitation with descriptive error
    - Test: occupiedSeats > MaxUsers rejects invitation (edge case with concurrent inserts)
    - Test: pending expired invitations are NOT counted in seat calculation
    - Test: inactive UserBusiness records are NOT counted in seat calculation
    - Place in `Portal.Tests/Unit/Services/InvitationServiceUserLimitTests.cs`
    - _Requirements: 5.1–5.5_

- [x] 9. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document (Properties 1–7)
- Unit tests validate specific examples and edge cases for the InvitationService
- Migration scripts follow the established Portal convention: sequential numbering (073–076), IF NOT EXISTS guards, GO terminators, explicit constraint names
- Repositories follow the Table Repository pattern with `GenericStoredProcedureRepository<T>` base class
- All SQL in repositories uses full table names (no aliases) per repository-standards steering

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2"] },
    { "id": 2, "tasks": ["1.3"] },
    { "id": 3, "tasks": ["1.4", "2.1"] },
    { "id": 4, "tasks": ["2.2"] },
    { "id": 5, "tasks": ["4.1", "4.2", "4.3"] },
    { "id": 6, "tasks": ["5.1"] },
    { "id": 7, "tasks": ["7.1", "7.2", "7.3", "7.4"] },
    { "id": 8, "tasks": ["7.5", "7.6", "7.7", "8.1"] }
  ]
}
```
