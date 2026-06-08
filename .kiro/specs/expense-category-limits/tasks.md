# Implementation Plan: Expense Category Limits

## Overview

This plan implements advisory spending limits per expense category for business accounts. Business owners can configure annual (calendar year) and/or per-VAT-period spending thresholds. The system evaluates cumulative spending in real time via AJAX when creating or editing purchases, displaying soft warnings without blocking form submission. The implementation follows the existing Controller → Service → Repository layering with raw SQL data access.

## Tasks

- [x] 1. Database migration
  - [x] 1.1 Create migration `Portal.Database/Migrations/088_CreateExpenseCategoryLimitTable.sql`
    - Create `[purchase].[ExpenseCategoryLimit]` table with columns: Id (INT IDENTITY PK), BusinessId (INT NOT NULL FK to `[portal].[Business]`), ExpenseCategoryId (INT NOT NULL FK to `[purchase].[ExpenseCategory]`), AnnualLimitEur (DECIMAL(18,2) NULL), PeriodLimitEur (DECIMAL(18,2) NULL), CreatedAtUtc (DATETIME2 NOT NULL DEFAULT GETUTCDATE())
    - Add unique index `UX_ExpenseCategoryLimit_BusinessId_ExpenseCategoryId` on (BusinessId, ExpenseCategoryId)
    - Add non-clustered index `IX_ExpenseCategoryLimit_BusinessId` on BusinessId
    - Add FK constraints to `[portal].[Business]` and `[purchase].[ExpenseCategory]`
    - Use idempotent pattern (IF NOT EXISTS) consistent with existing migrations
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

- [ ] 2. Entity and models
  - [x] 2.1 Create `Portal.Infrastructure/Entities/ExpenseCategoryLimit.cs`
    - Define entity class with Id, BusinessId, ExpenseCategoryId, AnnualLimitEur (decimal?), PeriodLimitEur (decimal?), CreatedAtUtc, and navigation properties (Business, ExpenseCategory)
    - _Requirements: 1.1, 2.1, 2.2_

  - [x] 2.2 Create request/response models in `Portal.Web/Models/`
    - Create `CheckLimitsRequest.cs` with ExpenseCategoryId, TotalAmount, InvoiceDate, PurchaseId (nullable)
    - Create `LimitCheckResult.cs` with HasWarning bool and List<LimitWarning> Warnings
    - Create `LimitWarning.cs` with LimitType (string), ConfiguredLimit, CumulativeTotal, ProjectedTotal, ExceededBy
    - Create `SaveLimitRequest.cs` with ExpenseCategoryId, AnnualLimitEur (nullable), PeriodLimitEur (nullable)
    - Create `ClearLimitRequest.cs` with ExpenseCategoryId and LimitType (string)
    - Create `ExpenseCategoryLimitViewModel.cs` with ExpenseCategoryId, CategoryName, AnnualLimitEur (nullable), PeriodLimitEur (nullable)
    - _Requirements: 5.5, 10.1, 10.2, 8.1_

  - [x] 2.3 Register `ExpenseCategoryLimit` entity in `Portal.Infrastructure/Data/PortalDbContext.cs`
    - Add DbSet<ExpenseCategoryLimit> property
    - _Requirements: 1.1_

- [x] 3. Checkpoint — Verify schema and models compile
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Repository layer
  - [x] 4.1 Create `Portal.Infrastructure/Repositories/ExpenseCategoryLimitRepository.cs`
    - Extend `GenericStoredProcedureRepository<ExpenseCategoryLimit>`
    - Implement `GetByBusinessAndCategoryAsync(int businessId, int expenseCategoryId)` — returns single record or null
    - Implement `GetAllByBusinessIdAsync(int businessId)` — returns all limits for business
    - Implement `InsertAsync(ExpenseCategoryLimit entity)`
    - Implement `UpdateAsync(ExpenseCategoryLimit entity)` — updates AnnualLimitEur and PeriodLimitEur
    - Implement `ClearLimitFieldAsync(int businessId, int expenseCategoryId, string fieldName)` — sets specified column to NULL
    - Use raw SQL with SqlParameter, full table names (no aliases), try/catch with rethrow
    - _Requirements: 1.1, 8.5, 9.1_

  - [x] 4.2 Add spending aggregation methods to `Portal.Infrastructure/Repositories/PurchaseRepository.cs`
    - Add `GetAnnualSpendingAsync(int businessId, int expenseCategoryId, int year, int? excludePurchaseId)` — SUM(TotalAmount) WHERE IsCancelled=0 AND YEAR(InvoiceDate)=@Year, excluding specified purchase if provided
    - Add `GetPeriodSpendingAsync(int businessId, int expenseCategoryId, DateOnly periodStart, DateOnly periodEnd, int? excludePurchaseId)` — SUM(TotalAmount) WHERE IsCancelled=0 AND InvoiceDate BETWEEN @PeriodStart AND @PeriodEnd, excluding specified purchase if provided
    - Use raw SQL with SqlParameter, full table names, try/catch with rethrow
    - _Requirements: 3.1, 4.1, 4.2, 7.1_

- [x] 5. Service layer
  - [x] 5.1 Create `Portal.Infrastructure/Services/IExpenseCategoryLimitService.cs`
    - Define interface with: GetLimitsForBusinessAsync(), EvaluateLimitsAsync(CheckLimitsRequest request), SaveLimitAsync(int expenseCategoryId, decimal? annualLimitEur, decimal? periodLimitEur), ClearLimitAsync(int expenseCategoryId, string limitType)
    - _Requirements: 2.3, 2.4, 2.5, 3.1, 4.1, 8.2, 8.3, 8.4, 8.5_

  - [x] 5.2 Create `Portal.Infrastructure/Services/ExpenseCategoryLimitService.cs`
    - Inject ICurrentTenantService, ExpenseCategoryLimitRepository, PurchaseRepository, PortalDbContext
    - Implement `GetLimitsForBusinessAsync()` — join limits with active expense categories for the business
    - Implement `EvaluateLimitsAsync(CheckLimitsRequest)`:
      - Get limit config for business+category; if null or both limits null, return no warnings
      - If AnnualLimitEur is set: get annual spending via repository (exclude current PurchaseId if editing), compare (cumulative + TotalAmount) > limit
      - If PeriodLimitEur is set: find VAT period containing InvoiceDate, get period spending (exclude current PurchaseId if editing), compare (cumulative + TotalAmount) > limit; skip if no matching period
      - Build LimitWarning objects with configuredLimit, cumulativeTotal, projectedTotal, exceededBy
    - Implement `SaveLimitAsync()` — validate > 0, upsert pattern (insert or update)
    - Implement `ClearLimitAsync()` — delegate to repository ClearLimitFieldAsync
    - Wrap EvaluateLimitsAsync in try/catch returning empty result on failure (fail-safe)
    - _Requirements: 2.3, 2.4, 2.5, 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 5.1, 5.5, 7.1, 7.2, 7.3, 8.5, 8.6, 9.1, 9.2, 10.3, 10.4_

- [x] 6. Checkpoint — Verify service layer compiles
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Controller
  - [x] 7.1 Create `Portal.Web/Controllers/ExpenseCategoryLimitController.cs`
    - Add [Authorize] and [ModuleAccess(PortalModules.Purchase)] attributes
    - Inject IExpenseCategoryLimitService
    - Implement `Index()` GET — returns management view with all categories and their limits
    - Implement `CheckLimits([FromBody] CheckLimitsRequest)` POST — calls EvaluateLimitsAsync, wraps in try/catch always returning valid JSON (fail-safe)
    - Implement `Save([FromBody] SaveLimitRequest)` POST with [ValidateAntiForgeryToken] — calls SaveLimitAsync, returns Json(new { success, message })
    - Implement `Clear([FromBody] ClearLimitRequest)` POST with [ValidateAntiForgeryToken] — calls ClearLimitAsync, returns Json(new { success, message })
    - _Requirements: 5.2, 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.7, 9.3, 9.4, 10.1, 10.2, 10.3, 10.4, 10.5_

- [x] 8. DI registration
  - [x] 8.1 Register ExpenseCategoryLimitRepository and IExpenseCategoryLimitService/ExpenseCategoryLimitService in DI container (Program.cs or service extension)
    - Register repository as scoped
    - Register service as scoped
    - _Requirements: 10.1_

- [x] 9. Limit Management UI
  - [x] 9.1 Create `Portal.Web/Views/ExpenseCategoryLimit/Index.cshtml`
    - Topbar with eyebrow "Expense Category Limits", heading, subtitle
    - Table listing all active expense categories with columns: Category Name, Annual Limit (EUR), Period Limit (EUR), Actions
    - Inline editable fields for AnnualLimitEur and PeriodLimitEur (number inputs, step=0.01, min=0.01)
    - Save button per row triggering AJAX POST to /ExpenseCategoryLimit/Save
    - Clear button per limit field triggering AJAX POST to /ExpenseCategoryLimit/Clear
    - Use BlockUI.show/hide, SweetAlert2 confirmations on save, standard fetch pattern with antiforgery token
    - Follow .glass.card-pad layout pattern
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.7_

- [x] 10. Purchase form — AJAX limit check integration
  - [x] 10.1 Add limit warning banner HTML and JavaScript to `Portal.Web/Views/Purchase/Create.cshtml`
    - Add warning banner container (hidden by default) near the expense category/amount fields
    - Style warning as advisory (amber/orange, "Warning" label, not red/error)
    - Add `checkLimits()` function that POSTs to /ExpenseCategoryLimit/CheckLimits with expenseCategoryId, totalAmount, invoiceDate, purchaseId=null
    - Hook into category `onSelect` callback and amount/total `oninput` events with debounce (300ms)
    - On response with warnings: render each warning showing limit type, configured limit, current total, projected total, exceeded by
    - On response with no warnings or on error: clear the warning banner
    - Do NOT use BlockUI for this call (real-time, non-blocking)
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 6.1, 6.2, 6.4, 6.5, 6.6_

  - [x] 10.2 Add limit warning banner HTML and JavaScript to `Portal.Web/Views/Purchase/Edit.cshtml`
    - Same warning banner and checkLimits() logic as Create, but include Model.Id as purchaseId in the request
    - Hook into same events (category change, amount change) with debounce
    - On page load with existing category, trigger initial limit check
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 7.1, 7.2, 7.3_

- [x] 11. Checkpoint — Full integration test
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 12. Property-based tests
  - [ ]* 12.1 Write property test for annual spending summation (Property 1)
    - **Property 1: Annual spending summation scoped to calendar year**
    - Generate random sets of purchases with varying InvoiceDate years, include cancelled purchases, verify sum matches only non-cancelled purchases in the target year
    - **Validates: Requirements 3.1**

  - [ ]* 12.2 Write property test for annual warning threshold (Property 2)
    - **Property 2: Annual warning if and only if projected total exceeds annual limit**
    - Generate random AnnualLimitEur, cumulative total, and new amount; verify warning returned iff (cumulative + amount) > limit
    - **Validates: Requirements 3.2, 3.3, 3.4**

  - [ ]* 12.3 Write property test for period spending summation (Property 3)
    - **Property 3: Period spending summation scoped to VAT period date range**
    - Generate random VAT period date ranges and purchases with varying dates, verify sum includes only non-cancelled purchases within the period
    - **Validates: Requirements 4.1, 4.2**

  - [ ]* 12.4 Write property test for period warning threshold (Property 4)
    - **Property 4: Period warning if and only if projected total exceeds period limit**
    - Generate random PeriodLimitEur, cumulative total, and new amount; verify warning returned iff (cumulative + amount) > limit
    - **Validates: Requirements 4.3, 4.4, 4.5**

  - [ ]* 12.5 Write property test for null limit no-warning (Property 5)
    - **Property 5: Null limit produces no warning**
    - Generate random amounts with null AnnualLimitEur and/or null PeriodLimitEur; verify no corresponding warning is ever produced
    - **Validates: Requirements 2.3, 3.5, 4.6, 10.3**

  - [ ]* 12.6 Write property test for edit-mode exclusion (Property 6)
    - **Property 6: Edit-mode exclusion from cumulative totals**
    - Generate a set of purchases including one being edited; verify cumulative total equals total minus the excluded purchase
    - **Validates: Requirements 7.1, 7.2, 7.3**

  - [ ]* 12.7 Write property test for limit value validation (Property 7)
    - **Property 7: Limit value validation rejects non-positive values**
    - Generate random decimal values; verify only values > 0 are accepted, all others rejected
    - **Validates: Requirements 2.4, 8.6**

  - [ ]* 12.8 Write property test for upsert idempotency (Property 8)
    - **Property 8: Upsert produces exactly one record per business-category pair**
    - Generate multiple save operations for same business+category; verify exactly one record exists after all saves
    - **Validates: Requirements 8.5**

  - [ ]* 12.9 Write property test for tenant isolation (Property 9)
    - **Property 9: Tenant isolation on all limit queries and spending calculations**
    - Generate limits and purchases across multiple business IDs; verify queries for one business never return data from another
    - **Validates: Requirements 9.1, 9.2**

  - [ ]* 12.10 Write property test for API response structure (Property 10)
    - **Property 10: API response structure contains required warning fields**
    - Generate valid requests that trigger warnings; verify response contains hasWarning=true with correct limitType, configuredLimit, cumulativeTotal, projectedTotal, exceededBy values
    - **Validates: Requirements 5.5, 10.2**

- [x] 13. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Migration is numbered 088 since 087 is the latest existing migration
- The design specifies C# (ASP.NET Core MVC 8) as the implementation language throughout
- All AJAX endpoints follow the established BlockUI + fetch + SweetAlert2 pattern for management UI; the limit check on purchase forms does NOT use BlockUI since it's a background real-time check
- Cancelled purchases (IsCancelled = 1) are excluded from all cumulative spending calculations
- The CheckLimits endpoint is fail-safe: any error returns `{ hasWarning: false, warnings: [] }` so the purchase form is never blocked
- The unique constraint on (BusinessId, ExpenseCategoryId) enforces the upsert pattern at the database level
- Property-based tests use FsCheck with xUnit integration, minimum 100 iterations per property
- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3"] },
    { "id": 2, "tasks": ["4.1", "4.2"] },
    { "id": 3, "tasks": ["5.1", "5.2"] },
    { "id": 4, "tasks": ["7.1", "8.1"] },
    { "id": 5, "tasks": ["9.1", "10.1", "10.2"] },
    { "id": 6, "tasks": ["12.1", "12.2", "12.3", "12.4", "12.5", "12.6", "12.7", "12.8", "12.9", "12.10"] }
  ]
}
```
