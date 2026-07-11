# Implementation Plan: Recurring Expense Validation

## Overview

This plan implements recurring expense validation for the Portal's purchase module. Business users configure rules defining expected purchase patterns per supplier (optionally scoped to an expense category). The system validates actual purchases against these rules for a given date range and produces an advisory report. The feature integrates into the VAT submission detail page and is also available as a standalone validation view.

The implementation follows the existing Controller → Service → Repository layering with raw SQL data access.

## Tasks

- [x] 1. Database migration
  - [x] 1.1 Create migration `Portal.Database/Migrations/113_CreateSupplierRecurringRuleTable.sql`
    - Create `[billing].[SupplierRecurringRule]` table with columns: Id (INT IDENTITY PK), BusinessId (INT NOT NULL FK to `[portal].[Business]`), SupplierId (INT NOT NULL FK to `[purchase].[Supplier]`), ExpenseCategoryId (INT NULL FK to `[purchase].[ExpenseCategory]`), FrequencyMonths (INT NOT NULL), ExpectedAmount (DECIMAL(18,2) NULL), AmountTolerancePercent (DECIMAL(5,2) NULL DEFAULT 5.00), GracePeriodDays (INT NOT NULL DEFAULT 0), Description (NVARCHAR(200) NOT NULL), IsActive (BIT NOT NULL DEFAULT 1), IsDeleted (BIT NOT NULL DEFAULT 0), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE())
    - Add non-clustered index `IX_SupplierRecurringRule_BusinessId_SupplierId` on (BusinessId, SupplierId)
    - Add non-clustered index `IX_SupplierRecurringRule_BusinessId` on BusinessId
    - Add FK constraints to `[portal].[Business]`, `[purchase].[Supplier]`, and `[purchase].[ExpenseCategory]`
    - Use idempotent pattern (IF NOT EXISTS) consistent with existing migrations
    - Include `USE [Guardian]` at the top
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

- [x] 2. Entity and models
  - [x] 2.1 Create `Portal.Infrastructure/Entities/SupplierRecurringRule.cs`
    - Define entity class with Id, BusinessId, SupplierId, ExpenseCategoryId (int?), FrequencyMonths, ExpectedAmount (decimal?), AmountTolerancePercent (decimal?), GracePeriodDays, Description, IsActive, IsDeleted, CreatedAtUtc
    - Add navigation properties: Business, Supplier, ExpenseCategory (nullable)
    - _Requirements: 1.1, 2.1, 2.2, 2.4, 2.6, 2.7, 2.8_

  - [x] 2.2 Create request/response models in `Portal.Web/Models/`
    - Create `RecurringExpenseValidateRequest.cs` with StartDate (DateOnly) and EndDate (DateOnly)
    - Create `RecurringExpenseValidationResult.cs` with Summary (ValidationSummary) and RuleResults (List<RuleValidationResult>)
    - Create `ValidationSummary.cs` with TotalRules, PassCount, WarningCount, FailCount
    - Create `RuleValidationResult.cs` with RuleId, SupplierName, CategoryName, Description, FrequencyMonths, ExpectedCount, ActualCount, Status, ExpectedAmount, IsAmountMatched, AmountMatchCount
    - Create `SaveRecurringRuleRequest.cs` with Id (int?), SupplierId, ExpenseCategoryId (int?), FrequencyMonths, ExpectedAmount (decimal?), AmountTolerancePercent (decimal?), GracePeriodDays, Description
    - Create `DeleteRecurringRuleRequest.cs` with Id
    - Create `ToggleRecurringRuleRequest.cs` with Id
    - Create `RecurringRuleViewModel.cs` with Id, SupplierId, SupplierName, ExpenseCategoryId, CategoryName, FrequencyMonths, FrequencyLabel, ExpectedAmount, AmountTolerancePercent, GracePeriodDays, Description, IsActive
    - _Requirements: 6.1, 6.2, 9.2, 9.3, 12.1, 12.2, 12.3_

  - [x] 2.3 Register `SupplierRecurringRule` entity in `Portal.Infrastructure/Data/PortalDbContext.cs`
    - Add DbSet<SupplierRecurringRule> property
    - Configure entity in OnModelCreating if needed (table name, schema, relationships)
    - _Requirements: 1.1_

- [x] 3. Checkpoint — Verify schema and models compile
  - Run `dotnet build` and ensure no compilation errors. Ask the user if questions arise.

- [x] 4. Repository layer
  - [x] 4.1 Create `Portal.Infrastructure/Repositories/SupplierRecurringRuleRepository.cs`
    - Extend `GenericStoredProcedureRepository<SupplierRecurringRule>`
    - Implement `GetActiveByBusinessIdAsync(int businessId)` — returns all rules where IsActive = 1 AND IsDeleted = 0, using full table names in SQL
    - Implement `GetAllByBusinessIdAsync(int businessId)` — returns all rules where IsDeleted = 0 (active and inactive, but not soft-deleted)
    - Implement `GetByIdAsync(int id, int businessId)` — returns single rule filtered by business for tenant safety
    - Implement `InsertAsync(SupplierRecurringRule entity)` — INSERT with all columns
    - Implement `UpdateAsync(SupplierRecurringRule entity)` — UPDATE SupplierId, ExpenseCategoryId, FrequencyMonths, ExpectedAmount, AmountTolerancePercent, GracePeriodDays, Description
    - Implement `SoftDeleteAsync(int id, int businessId)` — UPDATE IsDeleted = 1 filtered by both Id and BusinessId
    - Implement `ToggleIsActiveAsync(int id, int businessId, bool isActive)` — UPDATE IsActive filtered by Id and BusinessId
    - Use raw SQL with SqlParameter, full table names (no aliases), try/catch with `(Exception ex) { throw; }`
    - _Requirements: 1.1, 9.1, 11.1, 13.1_

  - [x] 4.2 Add purchase lookup methods to `Portal.Infrastructure/Repositories/PurchaseRepository.cs`
    - Add `CountQualifyingPurchasesAsync(int businessId, int supplierId, int? expenseCategoryId, DateOnly startDate, DateOnly endDate)` — COUNT(*) WHERE IsCancelled=0 AND SupplierId=@SupplierId AND (categoryId IS NULL OR ExpenseCategoryId=@CategoryId) AND InvoiceDate BETWEEN @Start AND @End
    - Add `CountAmountMatchingPurchasesAsync(int businessId, int supplierId, int? expenseCategoryId, DateOnly startDate, DateOnly endDate, decimal expectedAmount, decimal tolerancePercent)` — same as above plus AmountExcludingVat BETWEEN lower and upper bound
    - Use raw SQL with SqlParameter, full table names, try/catch with `(Exception ex) { throw; }`
    - _Requirements: 3.2, 4.1, 4.2, 5.1, 5.2, 14.1_

- [x] 5. Service layer
  - [x] 5.1 Create `Portal.Infrastructure/Services/IRecurringExpenseValidationService.cs`
    - Define interface with: ValidateAsync(int businessId, DateOnly startDate, DateOnly endDate), GetRulesForBusinessAsync(int businessId), SaveRuleAsync(int businessId, SaveRecurringRuleRequest request), DeleteRuleAsync(int businessId, int ruleId), ToggleRuleAsync(int businessId, int ruleId)
    - _Requirements: 3.1, 4.1, 9.1, 9.2, 9.3, 12.1_

  - [x] 5.2 Create `Portal.Infrastructure/Services/RecurringExpenseValidationService.cs`
    - Inject SupplierRecurringRuleRepository, PurchaseRepository, PortalDbContext
    - Implement `ValidateAsync(int businessId, DateOnly startDate, DateOnly endDate)`:
      - Get all active rules for business
      - For each rule: calculate periodMonths, expectedCount = floor(periodMonths / frequencyMonths); skip if expectedCount == 0
      - Calculate lookupStart = startDate - GracePeriodDays, lookupEnd = endDate + GracePeriodDays
      - Call CountQualifyingPurchasesAsync to get actualCount
      - If ExpectedAmount configured: call CountAmountMatchingPurchasesAsync for amountMatchCount
      - Determine status: PASS if actual >= expected, WARNING if actual > 0 but < expected, FAIL if actual == 0
      - For amount: PASS if amountMatch >= expected, WARNING if amountMatch > 0, FAIL if amountMatch == 0; overall = worst of frequency and amount
      - Build summary (totals, pass/warn/fail counts)
      - Sort results: FAIL first, WARNING second, PASS last
      - Wrap each rule evaluation in try/catch — partial failure doesn't block others
    - Implement `GetRulesForBusinessAsync(int businessId)` — get all rules, join with supplier/category names, build view models with frequency labels
    - Implement `SaveRuleAsync(int businessId, SaveRecurringRuleRequest request)`:
      - Validate: FrequencyMonths >= 1, Description not empty (max 200), GracePeriodDays 0-15
      - If ExpectedAmount set: validate > 0, ensure AmountTolerancePercent is set (default 5)
      - If Id is null: insert new rule; if Id is set: verify ownership and update
    - Implement `DeleteRuleAsync(int businessId, int ruleId)` — verify ownership, soft-delete (set IsDeleted = 1)
    - Implement `ToggleRuleAsync(int businessId, int ruleId)` — get current state, toggle IsActive
    - _Requirements: 2.1–2.8, 3.1–3.6, 4.1–4.6, 5.1–5.4, 6.1–6.5, 10.1, 10.2, 11.1, 11.2, 13.1, 14.1, 14.2_

- [x] 6. Checkpoint — Verify service layer compiles
  - Run `dotnet build` and ensure no compilation errors. Ask the user if questions arise.

- [x] 7. Controller
  - [x] 7.1 Create `Portal.Web/Controllers/RecurringExpenseController.cs`
    - Add [Authorize] and [ModuleAccess(PortalModules.Purchase)] attributes
    - Inject IRecurringExpenseValidationService, ICurrentTenantService (or equivalent for getting BusinessId)
    - Implement `Index()` GET — standalone validation view, pass VAT periods to ViewBag for dropdown
    - Implement `Rules()` GET — rule management view, pass suppliers and categories to ViewBag
    - Implement `Validate([FromBody] RecurringExpenseValidateRequest)` POST — calls ValidateAsync, wraps in try/catch always returning valid JSON (fail-safe)
    - Implement `AxPostSaveRule([FromBody] SaveRecurringRuleRequest)` POST with [ValidateAntiForgeryToken] — calls SaveRuleAsync, returns Json(new { success, message })
    - Implement `AxPostDeleteRule([FromBody] DeleteRecurringRuleRequest)` POST with [ValidateAntiForgeryToken] — calls DeleteRuleAsync (soft-delete), returns Json(new { success, message })
    - Implement `AxPostToggleRule([FromBody] ToggleRecurringRuleRequest)` POST with [ValidateAntiForgeryToken] — calls ToggleRuleAsync, returns Json(new { success, message })
    - _Requirements: 7.1–7.6, 8.1–8.8, 9.1–9.8, 11.1–11.4, 12.1–12.5_

- [x] 8. DI registration
  - [x] 8.1 Register SupplierRecurringRuleRepository and IRecurringExpenseValidationService/RecurringExpenseValidationService in DI container (Program.cs or service extension)
    - Register repository as scoped
    - Register service as scoped
    - _Requirements: 12.1_

- [x] 9. Checkpoint — Verify controller and DI compile
  - Run `dotnet build` and ensure no compilation errors. Ask the user if questions arise.

- [x] 10. Rule Management UI
  - [x] 10.1 Create `Portal.Web/Views/RecurringExpense/Rules.cshtml`
    - Topbar with eyebrow "Purchases", heading "Recurring Expense Rules", subtitle explaining the feature
    - "Add Rule" button opening a form section or modal
    - Form fields: Supplier (autocomplete), Category (autocomplete, optional), Frequency (dropdown: Monthly/Bimonthly/Quarterly with custom months option), Expected Amount (optional number input), Tolerance % (shown conditionally, default 5), Grace Period Days (0-15 slider or input), Description (text input, required)
    - Table/card list of existing rules grouped by supplier, showing: supplier name, category (or "Any"), frequency label, expected amount (or "—"), description, active/inactive badge
    - Per-rule actions: Edit (populates form), Delete (SweetAlert2 confirmation with destructive styling — performs soft-delete), Disable/Enable toggle
    - Active rules show: Edit, Disable, Delete buttons
    - Inactive rules show: Enable, Delete buttons (row visually greyed out)
    - AJAX pattern: BlockUI.show → fetch → BlockUI.hide → Swal.fire for save/delete/toggle
    - Include antiforgery token in all POST requests
    - Follow .glass.card-pad layout pattern, filter+table structure
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 9.7, 9.8_

- [x] 11. Standalone Validation View
  - [x] 11.1 Create `Portal.Web/Views/RecurringExpense/Index.cshtml`
    - Topbar with eyebrow "Purchases", heading "Recurring Expense Validation", subtitle
    - Filter section (.glass.card-pad with margin-bottom:22px): VAT period dropdown (pre-populated with business periods) OR custom date range (from/to date pickers), "Validate" button
    - Default selection: current open VAT period if one exists
    - Results section (.glass.card-pad): validation report rendered after AJAX response
    - Report layout: summary bar at top (total rules, pass count in green, warning count in amber, fail count in red)
    - Results table: one row per rule showing supplier, category, description, frequency, expected/actual counts, status badge (colour-coded), amount info if applicable
    - Sorted: FAIL → WARNING → PASS
    - Empty state when no rules configured: message + link to /RecurringExpense/Rules
    - AJAX pattern: BlockUI.show('Validating...') → fetch POST /RecurringExpense/Validate → BlockUI.hide → render results (no Swal needed for successful validation, only on error)
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 6.1, 6.2, 6.3, 6.4, 6.5_

- [x] 12. VAT Submission Integration
  - [x] 12.1 Add recurring expense validation panel to `Portal.Web/Views/Vat/Detail.cshtml`
    - Add a collapsible section below the existing VAT breakdown card (margin-top:24px)
    - Section heading: "Recurring Expense Check" with expand/collapse toggle
    - On page load: AJAX POST to /RecurringExpense/Validate with period's PeriodStartDate and PeriodEndDate
    - Render same report format as standalone view (summary + rule results)
    - "Re-validate" button to refresh results
    - When no active rules exist: show message "No recurring expense rules configured" with link to /RecurringExpense/Rules
    - Panel is advisory — does not affect any submit/filing actions on the page
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6_

- [x] 13. Navigation Integration
  - [x] 13.1 Add "Recurring Expenses" navigation item under the Purchases module
    - Add to ModuleNavigation ViewComponent or directly in the layout where Purchase sub-items are rendered
    - Item should link to /RecurringExpense (standalone validation view)
    - Consider adding "Rules" as a sub-action accessible from the standalone view (link/button in topbar)
    - _Requirements: 8.1, 8.8, 9.8_

- [x] 14. Checkpoint — Full integration test
  - Run `dotnet build` to verify everything compiles
  - Manually verify: create a rule, navigate to standalone view, validate against a period, check VAT detail panel
  - Ask the user if questions arise

- [x] 15. Property-based tests
  - [x]* 15.1 Write property test for expected count calculation (Property 1)
    - **Property 1: Expected count calculation is deterministic**
    - Generate random date ranges and frequency values; verify expectedCount = floor(periodMonths / frequencyMonths) and that result 0 causes rule to be skipped
    - **Validates: Requirements 3.1**

  - [x]* 15.2 Write property test for cancelled purchase exclusion (Property 2)
    - **Property 2: Qualifying purchase count excludes cancelled purchases**
    - Generate mixed sets of cancelled and non-cancelled purchases; verify only non-cancelled are counted
    - **Validates: Requirements 14.1, 14.2**

  - [x]* 15.3 Write property test for category-scoped filtering (Property 3)
    - **Property 3: Category-scoped rules only count category-matched purchases**
    - Generate purchases with varied categories for same supplier; verify only matching category counted when rule has ExpenseCategoryId set
    - **Validates: Requirements 2.3**

  - [x]* 15.4 Write property test for category-null filtering (Property 4)
    - **Property 4: Category-null rules count all purchases from supplier**
    - Generate purchases with varied categories for same supplier; verify all are counted when rule has ExpenseCategoryId = null
    - **Validates: Requirements 2.2**

  - [x]* 15.5 Write property test for grace period behaviour (Property 5)
    - **Property 5: Grace period widens lookup but not expectation**
    - Generate rules with various grace periods; verify lookup window is extended but expectedCount remains unchanged
    - **Validates: Requirements 5.1, 5.2, 5.3**

  - [x]* 15.6 Write property test for amount tolerance (Property 6)
    - **Property 6: Amount tolerance range is symmetric**
    - Generate expected amounts and tolerances; verify boundary values are correctly included/excluded
    - **Validates: Requirements 4.2**

  - [x]* 15.7 Write property test for status determination (Property 7)
    - **Property 7: Status determination is consistent with counts**
    - Generate various actualCount/expectedCount pairs; verify PASS when actual >= expected, WARNING when 0 < actual < expected, FAIL when actual == 0
    - **Validates: Requirements 3.3, 3.4, 3.5**

  - [x]* 15.8 Write property test for tenant isolation (Property 8)
    - **Property 8: Tenant isolation on all queries**
    - Generate rules and purchases across multiple business IDs; verify queries for one business never return data from another
    - **Validates: Requirements 11.1, 11.2**

  - [x]* 15.9 Write property test for deactivated rule exclusion (Property 9)
    - **Property 9: Deactivated rules are excluded from validation**
    - Generate mix of active and inactive rules; verify only active rules appear in validation results
    - **Validates: Requirements 13.1**

  - [x]* 15.10 Write property test for report sorting (Property 10)
    - **Property 10: Validation report sorting order**
    - Generate results with mixed statuses; verify FAIL comes first, then WARNING, then PASS
    - **Validates: Requirements 6.5**

- [x] 16. Final checkpoint — Ensure all tests pass
  - Run `dotnet test` and verify property-based tests pass
  - Run `dotnet build` for final compilation check
  - Ask the user if questions arise

## Notes

- Migration is numbered 113 since 112 is the latest existing migration
- The `[billing]` schema already exists (created in migration 076)
- Table references: `[portal].[Business]`, `[purchase].[Supplier]`, `[purchase].[ExpenseCategory]`
- The design specifies C# (ASP.NET Core MVC 8) as the implementation language throughout
- All AJAX endpoints follow the established BlockUI + fetch + SweetAlert2 pattern
- The Validate endpoint is fail-safe: any error returns an empty validation result so the UI is never blocked
- Cancelled purchases (IsCancelled = 1) are excluded from all qualifying purchase counts
- The VAT panel integration is purely additive — no changes to existing VAT submission logic
- Tasks marked with `*` are optional and can be skipped for faster MVP delivery
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation between logical phases

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"], "description": "Database schema" },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3"], "description": "Entity and models" },
    { "id": 2, "tasks": ["3"], "description": "Checkpoint: compile" },
    { "id": 3, "tasks": ["4.1", "4.2"], "description": "Repository layer" },
    { "id": 4, "tasks": ["5.1", "5.2"], "description": "Service layer" },
    { "id": 5, "tasks": ["6"], "description": "Checkpoint: compile" },
    { "id": 6, "tasks": ["7.1", "8.1"], "description": "Controller and DI" },
    { "id": 7, "tasks": ["9"], "description": "Checkpoint: compile" },
    { "id": 8, "tasks": ["10.1", "11.1", "12.1", "13.1"], "description": "UI layer (parallel)" },
    { "id": 9, "tasks": ["14"], "description": "Checkpoint: full integration" },
    { "id": 10, "tasks": ["15.1", "15.2", "15.3", "15.4", "15.5", "15.6", "15.7", "15.8", "15.9", "15.10"], "description": "Property-based tests" },
    { "id": 11, "tasks": ["16"], "description": "Final checkpoint" }
  ]
}
```
