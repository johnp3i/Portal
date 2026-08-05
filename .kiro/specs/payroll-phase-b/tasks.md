# Implementation Plan: Payroll Phase B (Audit, Unlock, and P&L Integration)

## Overview

Phase B extends the Phase A Core Engine with unlock/re-finalise capabilities, an immutable field-level audit trail, and automatic P&L synchronisation via the existing Purchase system. Implementation follows a strict dependency order: Phase A retroactive fixes first, then database schema, EF Core entities, repository extensions, service layer (state machine, audit, P&L), controller endpoints, view layer, and finally tests. All new tables reside in the `[payroll]` schema. P&L integration uses the existing `[purchase]` schema with a cross-schema FK.

## Tasks

- [x] 1. Phase A retroactive fixes (prerequisite for all Phase B work)
  - [x] 1.1 Add UpdateAllPayslipStatusesInPeriodAsync to PayrollRepository
    - Add method to `Portal.Infrastructure/Repositories/PayrollRepository.cs`
    - UPDATE `[payroll].[Payslip]` SET PayslipStatusTypeId = @StatusId WHERE PayslipPeriodId = @PeriodId
    - Use full table names, `catch (Exception ex) { throw; }` pattern
    - _Requirements: 1.1, 2.4, 5.3_

  - [x] 1.2 Add status cascade to FinalisePeriodAsync and batch generation
    - In `PayrollService.FinalisePeriodAsync`: call `UpdateAllPayslipStatusesInPeriodAsync(id, 3)` after period status update
    - In `PayrollService.ConfirmBatchGenerationAsync`: call `UpdateAllPayslipStatusesInPeriodAsync(id, 2)` after period moves to Preview
    - Ensures all payslips match their period status going forward
    - _Requirements: 1.1, 2.4, 5.3_

  - [x] 1.3 Replace PeriodStatusNames dictionary with DB lookup
    - Add `GetStatusNamesAsync()` method to PayrollRepository: SELECT from `[payroll].[PayslipStatusType]`
    - Remove hardcoded `PeriodStatusNames` dictionary from PayrollService
    - Replace with lazy-loaded cached dictionary via repository call
    - _Requirements: 1.1_

  - [x] 1.4 Add optimistic concurrency to UpdatePeriodStatusAsync
    - Modify existing `UpdatePeriodStatusAsync` signature to accept `byte expectedCurrentStatus` parameter
    - Add `WHERE PayslipPeriod.PayslipStatusTypeId = @ExpectedCurrentStatus` to the UPDATE query
    - Return `bool` (true if 1 row affected, false if 0 — concurrency conflict)
    - Update all existing callers of `UpdatePeriodStatusAsync` to pass expected current status
    - _Requirements: 1.5_

- [x] 2. SQL migrations — Phase B schema additions
  - [x] 2.1 Seed new PayslipStatusType values and create audit lookup table
    - Create SQL migration file in `Portal.Database/Seeds/`
    - INSERT INTO `[payroll].[PayslipStatusType]` values (4, 'Unlocked'), (5, 'Re-finalised')
    - CREATE TABLE `[payroll].[PayslipAuditActionType]` (Id TINYINT PK, Name NVARCHAR(20))
    - Seed values: (1, 'Unlocked'), (2, 'Edited'), (3, 'Re-finalised')
    - _Requirements: 1.1, 4.2, 10.3_

  - [x] 2.2 Create PayslipAuditLog table with indexes
    - CREATE TABLE `[payroll].[PayslipAuditLog]` with all columns per design (Id INT IDENTITY PK, PayslipId INT FK, UserId NVARCHAR(450), PayslipAuditActionTypeId TINYINT FK, FieldName NVARCHAR(100) NULL, OldValue NVARCHAR(500) NULL, NewValue NVARCHAR(500) NULL, CreatedAtUtc DATETIME NOT NULL DEFAULT GETUTCDATE())
    - Add FK to `[payroll].[Payslip]` with NO ACTION on delete (prevent cascade)
    - Add FK to `[payroll].[PayslipAuditActionType]`
    - Create index IX_PayslipAuditLog_PayslipId on (PayslipId) INCLUDE (CreatedAtUtc, PayslipAuditActionTypeId)
    - Create index IX_PayslipAuditLog_CreatedAtUtc on (CreatedAtUtc DESC) INCLUDE (PayslipId, UserId)
    - _Requirements: 4.1, 10.1, 10.2, 10.4, 10.5, 10.7_

  - [x] 2.3 ALTER Purchase and Supplier tables for P&L integration
    - ALTER TABLE `[purchase].[Purchase]` ADD PayslipPeriodId INT NULL
    - Add FK constraint [FK_Purchase_PayslipPeriod] referencing `[payroll].[PayslipPeriod]`
    - Create filtered index IX_Purchase_PayslipPeriodId WHERE PayslipPeriodId IS NOT NULL
    - ALTER TABLE `[purchase].[Purchase]` ADD CancelledByUserId NVARCHAR(450) NULL
    - ALTER TABLE `[purchase].[Supplier]` ADD IsSystemGenerated BIT NOT NULL DEFAULT 0
    - _Requirements: 6.3, 7.3, 10.6_

  - [x] 2.4 Update raw SQL SELECT queries in PurchaseRepository and SupplierRepository
    - After adding new columns, EF Core's `FromSqlRaw` will throw if SELECT queries don't return them (same issue as `IsOnboardingDismissed` incident)
    - **PurchaseRepository.cs** — add `[PayslipPeriodId], [CancelledByUserId]` to the column list in:
      - `GetAllByBusinessIdAsync`
      - `GetByIdAndBusinessIdAsync`
      - `GetFilteredAsync`
      - `GetUnassignedByDateRangeAsync`
    - **SupplierRepository.cs** — add `[IsSystemGenerated]` to the column list in:
      - `GetAllByBusinessIdAsync`
      - `GetByIdAndBusinessIdAsync`
      - `GetPagedByBusinessIdAsync` (also update the manual DataReader mapping to read `IsSystemGenerated`: `IsSystemGenerated = reader.GetBoolean(reader.GetOrdinal("IsSystemGenerated"))`)
    - _Requirements: 6.3, 7.3, 10.6_

- [x] 3. EF Core entities and DbContext configuration (Phase B additions)
  - [x] 3.1 Create new entity classes for Phase B
    - Create `Portal.Infrastructure/Entities/PayslipAuditLog.cs` with all properties per design
    - Create `Portal.Infrastructure/Entities/PayslipAuditActionType.cs` (Id byte, Name string)
    - Add `PayslipPeriodId` (int?), `CancelledByUserId` (string?), and `PayslipPeriod` navigation property to existing Purchase entity
    - Add `IsSystemGenerated` (bool) property to existing Supplier entity
    - _Requirements: 4.1, 10.1, 10.6_

  - [x] 3.2 Add DbContext configuration for Phase B entities
    - Register PayslipAuditLog and PayslipAuditActionType DbSets
    - Configure PayslipAuditLog: `[payroll]` schema, FK to Payslip with DeleteBehavior.NoAction, FK to PayslipAuditActionType with DeleteBehavior.NoAction, max lengths per design
    - Configure PayslipAuditActionType: `[payroll]` schema, TINYINT PK
    - Extend Purchase configuration: PayslipPeriodId optional, CancelledByUserId max 450, FK to PayslipPeriod with DeleteBehavior.Restrict
    - Extend Supplier configuration: IsSystemGenerated required with default false
    - _Requirements: 10.1, 10.2, 10.6, 10.7_

- [x] 4. DTO and request models (Phase B additions)
  - [x] 4.1 Create Phase B DTOs and request models
    - Create `Portal.Infrastructure/Models/Payroll/PayslipAuditLogDto.cs` (Id, UserFullName, ActionName, ActionTypeId, FieldName, OldValue, NewValue, CreatedAtUtc)
    - Create `Portal.Infrastructure/Models/Payroll/PeriodAuditGroupDto.cs` (PayslipId, EmployeeName, Entries list)
    - Create `Portal.Infrastructure/Models/Payroll/UnlockPeriodRequest.cs` (PeriodId int)
    - Create `Portal.Infrastructure/Models/Payroll/RefinalisePeriodRequest.cs` (PeriodId int)
    - _Requirements: 9.1, 9.3, 9.5_

- [x] 5. Build checkpoint
  - Ensure the project compiles with all new entities, DTOs, schema configuration, and Phase A retroactive fixes
  - Verify no build errors from DbContext configuration or missing references
  - Ensure all existing callers of UpdatePeriodStatusAsync compile with new signature
  - Ask the user if questions arise

- [x] 6. Repository layer (Phase B extensions)
  - [x] 6.1 Add audit log methods to PayrollRepository
    - InsertAuditLogAsync(PayslipAuditLog entry): INSERT single audit entry
    - InsertAuditLogBatchAsync(List<PayslipAuditLog> entries): INSERT multiple entries efficiently
    - GetAuditLogsByPayslipAsync(int payslipId): SELECT all audit entries for a payslip, JOIN to AspNetUsers for UserFullName, JOIN to PayslipAuditActionType for ActionName, ORDER BY CreatedAtUtc DESC
    - GetAuditLogsByPeriodAsync(int periodId): SELECT all audit entries for all payslips in a period, JOIN through Payslip → PayslipPeriod, include Employee name for grouping
    - _Requirements: 4.1, 4.7, 9.2, 9.5_

  - [x] 6.2 Add P&L integration methods to PayrollRepository and PurchaseRepository
    - GetPayrollPurchasesByPeriodAsync(int businessId, int periodId): SELECT from `[purchase].[Purchase]` WHERE PayslipPeriodId = @PeriodId AND Purchase.BusinessId = @BusinessId AND IsCancelled = 0
    - Add GetPayslipsByPeriodWithLinesAsync(int periodId): SELECT payslips with earning and deduction lines eager-loaded (needed for re-finalisation recalculation)
    - Extend existing PurchaseRepository (or use PayrollRepository) to support Insert and Cancel operations for payroll-generated Purchase records
    - _Requirements: 6.1, 6.2, 6.3, 7.1, 7.2_

  - [x] 6.3 Add supplier and expense category helper methods
    - GetPayrollSupplierAsync(int businessId): SELECT from `[purchase].[Supplier]` WHERE Supplier.BusinessId = @BusinessId AND Supplier.IsSystemGenerated = 1 AND Supplier.Name = 'Payroll (Internal)'
    - InsertPayrollSupplierAsync(int businessId): INSERT `[purchase].[Supplier]` with IsSystemGenerated = 1, IsActive = 1
    - GetOrCreateExpenseCategoryAsync(int businessId, string name): Check if category exists, create if not
    - Add protection in existing supplier deletion logic: reject if IsSystemGenerated = 1
    - _Requirements: 6.3, 7.3_

- [x] 7. Service layer — PayslipPeriodStatusService (state machine)
  - [x] 7.1 Create IPayslipPeriodStatusService and implementation
    - Create `Portal.Infrastructure/Services/IPayslipPeriodStatusService.cs` interface
    - Create `Portal.Infrastructure/Services/PayslipPeriodStatusService.cs`
    - Implement AllowedTransitions dictionary: {Draft→Preview, Preview→Finalised, Finalised→Unlocked, Unlocked→Re-finalised, Re-finalised→Unlocked}
    - Implement IsTransitionAllowed(byte currentStatusId, byte targetStatusId): lookup in dictionary
    - Implement GetAllowedTransitions(byte currentStatusId): return valid targets
    - _Requirements: 1.2, 1.3, 1.4, 1.5_

  - [x] 7.2 Implement UnlockPeriodAsync in PayslipPeriodStatusService
    - Validate role is Owner or SuperAdmin (return authorisation error otherwise)
    - Validate period status is Finalised (3) or Re-finalised (5)
    - Begin transaction
    - Call UpdatePeriodStatusAsync with optimistic concurrency (expected current status)
    - Call UpdateAllPayslipStatusesInPeriodAsync(periodId, 4)
    - Get all payslips in period, create audit entry per payslip (ActionTypeId = 1, Unlocked)
    - Commit transaction
    - Return ServiceResult.Ok() or ServiceResult.Fail() with appropriate error message
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 11.1, 11.5_

  - [x] 7.3 Implement RefinalisePeriodAsync in PayslipPeriodStatusService
    - Validate role is Owner or SuperAdmin
    - Validate period status is Unlocked (4)
    - Load all payslips with earning lines, load active deductions with rates
    - Recalculate each payslip via IPayslipCalculationEngine — if any fail validation, return error with employee name
    - Begin transaction
    - Persist recalculated totals for each payslip (UpdatePayslipTotalsAsync)
    - Call UpdatePeriodStatusAsync(periodId, 5, Unlocked, DateTime.UtcNow) with optimistic concurrency
    - Call UpdateAllPayslipStatusesInPeriodAsync(periodId, 5)
    - Call IPayrollPnlService.AdjustPnlEntriesAsync(periodId, businessId, userId) for P&L update — pass userId so cancelled entries record who performed the action
    - Create audit entry per payslip (ActionTypeId = 3, Re-finalised)
    - Commit transaction; rollback on any failure
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 11.2_

- [x] 8. Service layer — PayslipAuditService (change tracking)
  - [x] 8.1 Create IPayslipAuditService and implementation
    - Create `Portal.Infrastructure/Services/IPayslipAuditService.cs` interface
    - Create `Portal.Infrastructure/Services/PayslipAuditService.cs`
    - Implement RecordStatusChangeAsync(int payslipId, string userId, byte actionTypeId): create entry with null FieldName/OldValue/NewValue
    - Implement RecordManagerNotesChangeAsync: compare old vs new, create entry with FieldName = "ManagerNotes" if different
    - Implement RecordPayslipAddedOrRemovedAsync: FieldName = "Payslip", OldValue = employeeName (removal) or NewValue = employeeName (addition)
    - _Requirements: 4.3, 4.6, 4.8_

  - [x] 8.2 Implement RecordEarningLineChangesAsync with disambiguation logic
    - Compare old earning lines vs new earning lines by EarningTypeId
    - Detect additions (new type not in old), removals (old type not in new), and amount modifications
    - For duplicate earning types (same EarningTypeName multiple times): use positional index `[{0-based index}]` format, ordered by Id
    - For single instances: use simple `EarningLine:{EarningTypeName}:Amount` format
    - Create batch of PayslipAuditLog entries, call InsertAuditLogBatchAsync
    - _Requirements: 4.3, 4.4, 4.5_

  - [x] 8.3 Integrate audit tracking into existing PayrollService edit flow
    - In SaveEarningLinesAsync: replace `if (period.PayslipStatusTypeId == 3)` with `if (!_periodStatusService.IsEditableStatus(period.PayslipStatusTypeId))` — this blocks edits on both Finalised (3) AND Re-finalised (5) statuses
    - In SaveManagerNotesAsync: same replacement — `if (!_periodStatusService.IsEditableStatus(period.PayslipStatusTypeId))` instead of checking only status 3
    - The `IsEditableStatus` helper returns true ONLY for Draft (1), Preview (2), and Unlocked (4)
    - When period is Unlocked, capture old earning lines BEFORE modification, then call RecordEarningLineChangesAsync after save
    - When period is Unlocked, call RecordManagerNotesChangeAsync with old and new values
    - Gate audit calls on period status being Unlocked (Draft/Preview edits are not audited)
    - _Requirements: 3.1, 3.2, 3.5, 4.3, 4.8_

  - [x] 8.4 Implement GetAuditHistoryAsync and GetPeriodAuditSummaryAsync
    - GetAuditHistoryAsync: call repository, map to PayslipAuditLogDto list (reverse chronological)
    - GetPeriodAuditSummaryAsync: call repository, group by payslip/employee, map to PeriodAuditGroupDto list
    - Validate business ownership before returning audit data
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6_

- [x] 9. Service layer — PayrollPnlService (expense sync)
  - [x] 9.1 Create IPayrollPnlService and implementation
    - Create `Portal.Infrastructure/Services/IPayrollPnlService.cs` interface
    - Create `Portal.Infrastructure/Services/PayrollPnlService.cs`
    - Implement EnsurePayrollPnlSetupAsync(int businessId): idempotent creation of "Payroll (Internal)" supplier (IsSystemGenerated=1) and two expense categories ("Payroll - Salary Cost", "Payroll - Employer Contributions")
    - _Requirements: 6.3, 6.5_

  - [x] 9.2 Implement CreatePnlEntriesAsync
    - Call EnsurePayrollPnlSetupAsync first
    - Calculate totals: salaryCost = SUM(Payslip.TotalEarnings), employerContributions = SUM(Payslip.TotalEmployerContributions)
    - Create Purchase entry for Salary Cost: InvoiceNumber = "PAY-{Year}-{Month:00}-SAL", InvoiceDate = last day of period month, Description = "Payroll - {MonthName} {Year}", AmountExcludingVat = salaryCost, VatAmount = 0, TotalAmount = salaryCost, PayslipPeriodId = periodId
    - Create Purchase entry for Employer Contributions: InvoiceNumber = "PAY-{Year}-{Month:00}-EMP", same date/description pattern, amounts from employer contributions sum
    - Both entries: PurchaseTypeId = 3 (Expense), PurchaseOriginTypeId = 1 (Domestic), IsCancelled = false
    - Return ServiceResult indicating success/failure
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6_

  - [x] 9.3 Implement AdjustPnlEntriesAsync
    - Signature: `Task<ServiceResult> AdjustPnlEntriesAsync(int periodId, int businessId, string userId)`
    - Find existing active (non-cancelled) Purchase records for the period
    - Mark each as cancelled: IsCancelled = true, CancelledAtUtc = DateTime.UtcNow, CancelledByUserId = userId
    - Create two new Purchase records with recalculated totals (same logic as CreatePnlEntriesAsync)
    - Original entries retain their amounts (soft-delete pattern for audit)
    - Must execute within caller's transaction scope
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6_

  - [x] 9.4 Integrate P&L into existing FinalisePeriodAsync
    - **Note:** This task builds on changes from Task 1.2. Task 1.2 adds the status cascade call. This task wraps the entire method in a transaction and adds the P&L call. The final implementation combines both changes.
    - Wrap existing FinalisePeriodAsync in a transaction (if not already)
    - After period status update and cascade, call CreatePnlEntriesAsync within same transaction
    - If P&L creation fails, rollback entire finalisation and return error
    - Backward-compatible: existing behaviour preserved, P&L call added
    - _Requirements: 6.1, 6.2, 6.5, 6.6_

- [x] 10. Build checkpoint
  - Ensure the project compiles with all three new services (StatusService, AuditService, PnlService)
  - Verify DI registration compiles (add registrations in this checkpoint)
  - Verify service-to-repository method calls match expected signatures
  - Ask the user if questions arise

- [x] 11. DI registration (Phase B services)
  - [x] 11.1 Register Phase B services in DI container
    - Register `IPayslipPeriodStatusService` / `PayslipPeriodStatusService` as Scoped
    - Register `IPayslipAuditService` / `PayslipAuditService` as Scoped
    - Register `IPayrollPnlService` / `PayrollPnlService` as Scoped
    - Inject new services into PayrollService constructor
    - _Requirements: 1.1, 11.1_

- [x] 12. Controller layer (Phase B endpoints)
  - [x] 12.1 Add unlock and re-finalise AJAX endpoints to PayrollController
    - Add `AxPostUnlockPeriod(int periodId)`: validate user role from claims, call service UnlockPeriodAsync, return Json success/fail
    - Add `AxPostRefinalisePeriod(int periodId)`: validate user role, call service RefinalisePeriodAsync, return Json success/fail
    - Role detection uses claims (NOT role lookup): `var isOwner = User.HasClaim("IsOwner", "true"); var isSuperAdmin = User.IsInRole("SuperAdmin");`
    - If neither Owner nor SuperAdmin: return `Json(new { success = false, message = "Only the business owner or a SuperAdmin can perform this action." })`
    - Map claims to role string for service layer: `var userRole = isSuperAdmin ? "SuperAdmin" : isOwner ? "Owner" : "User";`
    - Both endpoints: try/catch with generic error JSON on exception, ModuleAccess attribute enforced at controller level
    - _Requirements: 2.1, 2.2, 5.2, 11.1, 11.2, 11.5_

  - [x] 12.2 Add audit history endpoints to PayrollController
    - Add `AxGetAuditHistory(int payslipId)`: call service GetPayslipAuditHistoryAsync, return Json with audit entries
    - Add `AxGetPeriodAuditSummary(int periodId)`: call service GetPeriodAuditSummaryAsync, return Json with grouped entries
    - Add page action `PayslipAuditHistory(int payslipId)`: render audit timeline view
    - Add page action `PeriodAuditSummary(int periodId)`: render period-level audit summary view
    - _Requirements: 9.1, 9.2, 9.5, 11.3_

- [x] 13. View layer — Unlock and Re-finalise UI
  - [x] 13.1 Add unlock/re-finalise buttons and status badges to PeriodDetail view
    - Extend existing `Portal.Web/Views/Payroll/PeriodDetail.cshtml`
    - Add colour-coded status badge: Draft=grey, Preview=blue, Finalised=green, Unlocked=amber, Re-finalised=green
    - Add "Unlock Period" button: visible only to Owner/SuperAdmin, only when status is Finalised or Re-finalised
    - Add "Re-finalise" button: visible only to Owner/SuperAdmin, only when status is Unlocked
    - Hide buttons from users without required role (server-side ViewBag flag)
    - _Requirements: 2.3, 8.1, 11.4_

  - [x] 13.2 Implement unlock confirmation dialog (SweetAlert2)
    - Create `unlockPeriod(periodId, monthName, year)` JavaScript function
    - SweetAlert2 warning dialog: title "Unlock Period?", text "Editing will affect P&L for {monthName} {year}", icon warning, confirmButtonText "Proceed", cancelButtonText "Cancel", confirmButtonColor '#C24A4A'
    - On confirm: BlockUI.show('Unlocking period...') → fetch POST AxPostUnlockPeriod → BlockUI.hide() → SweetAlert2 success/error → reload on success
    - On cancel: abort, maintain current status
    - Include antiforgery token in request headers
    - _Requirements: 2.6, 8.1, 8.2, 8.3, 8.4, 8.5, 8.6_

  - [x] 13.3 Implement re-finalise confirmation dialog (SweetAlert2)
    - Create `refinalisePeriod(periodId, monthName, year)` JavaScript function
    - SweetAlert2 info dialog: title "Re-finalise this period?", text "P&L entries will be updated to reflect your changes.", icon info, confirmButtonText "Re-finalise", cancelButtonText "Cancel", confirmButtonColor '#0D5EA6'
    - On confirm: BlockUI.show('Re-finalising period...') → fetch POST AxPostRefinalisePeriod → BlockUI.hide() → SweetAlert2 success/error → reload on success
    - Include antiforgery token in request headers
    - _Requirements: 5.2, 5.5_

  - [x] 13.4 Update PayslipDetail view for edit gating
    - Enable earning line edit controls ONLY when period status is Unlocked (or Draft/Preview as before)
    - Enforce read-only mode when period is Finalised or Re-finalised (disable edit buttons, hide save actions)
    - Add "Audit History" button linking to the payslip audit timeline view
    - _Requirements: 3.1, 3.2, 3.5, 9.1_

- [x] 14. View layer — Audit History views
  - [x] 14.1 Create PayslipAuditHistory view (timeline)
    - Create `Portal.Web/Views/Payroll/PayslipAuditHistory.cshtml`
    - Render vertical timeline in reverse chronological order
    - Each entry shows: user full name, action badge (colour-coded: Unlocked=amber, Edited=blue, Re-finalised=green), field name (human-readable), old value → new value with visual diff styling, timestamp in business locale
    - Status-change entries (Unlocked, Re-finalised) display as simple event markers without field details
    - Read-only view — no modification controls
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.6_

  - [x] 14.2 Create PeriodAuditSummary view
    - Create `Portal.Web/Views/Payroll/PeriodAuditSummary.cshtml`
    - Display all audit events across all payslips in the period
    - Group by employee name with collapsible sections
    - Each group shows the same timeline entry format as the payslip-level view
    - Link from PeriodDetail view ("View Audit Summary" button, visible to all payroll users)
    - _Requirements: 9.5, 11.3_

- [x] 15. Supplier protection (IsSystemGenerated)
  - [x] 15.1 Add IsSystemGenerated protection to supplier management
    - Update supplier deletion service logic: reject deletion if IsSystemGenerated = 1 with error "This supplier is system-generated and cannot be deleted."
    - Update supplier list queries to filter WHERE IsSystemGenerated = 0 in supplier management views (user-facing supplier list)
    - Payroll P&L queries still access system-generated suppliers internally
    - _Requirements: 6.3_

  - [x] 15.2 Create What's New announcement seed SQL for Phase B
    - Create SQL seed file `Portal.Database/Seeds/Seed_WhatsNew_PayrollPhaseB.sql`
    - Follow existing pattern from `Seed_WhatsNew_FollowUpTasks.sql`
    - USE [Portal] header, IF NOT EXISTS guard on Title
    - Title: "Payroll Audit Trail & P&L Integration"
    - Summary: Brief description of unlock/edit/re-finalise capabilities and automatic P&L synchronisation
    - DetailHtml: Bullet list covering — Unlock & Re-finalise periods, Field-level audit trail, Automatic P&L entries, Role-restricted access (Owner/SuperAdmin)
    - ModuleKey: 'payroll'
    - CtaLabel: 'Open Payroll', CtaUrl: '/Payroll'
    - IsActive: 1, PublishedAtUtc: GETUTCDATE()
    - _Requirements: N/A (user-facing announcement)_

- [x] 16. Build and integration checkpoint
  - Ensure all Phase B code compiles: services, repository extensions, controller endpoints, views
  - Verify unlock → edit → re-finalise flow compiles end-to-end
  - Verify P&L creation compiles within finalisation transaction
  - Verify audit service integrates correctly with existing edit flows
  - Ask the user if questions arise

- [ ] 17. Property-based tests
  - [ ]* 17.1 Write property test for status transition enforcement
    - **Property 1: Status transition enforcement**
    - Generate all byte pairs (0–5) × (0–5), verify IsTransitionAllowed returns true iff pair is in the valid set {(1,2), (2,3), (3,4), (4,5), (5,4)}
    - Create `Portal.Tests/PropertyTests/Payroll/StatusTransitionPropertyTests.cs`
    - Use FsCheck + xUnit
    - **Validates: Requirements 1.2, 1.3, 1.4, 1.5**

  - [ ]* 17.2 Write property test for role-restricted operations
    - **Property 2: Role-restricted operations**
    - Generate random role strings, verify unlock/re-finalise succeed only for "Owner" and "SuperAdmin"
    - Create `Portal.Tests/PropertyTests/Payroll/RoleRestrictionPropertyTests.cs`
    - **Validates: Requirements 2.1, 11.1, 11.2, 11.5**

  - [ ]* 17.3 Write property test for period-payslip status synchronisation
    - **Property 3: Period-payslip status synchronisation**
    - Generate periods with 1–20 payslips, verify all payslip statuses match period status after transition
    - Create `Portal.Tests/PropertyTests/Payroll/PeriodPayslipStatusSyncPropertyTests.cs`
    - **Validates: Requirements 2.4, 5.3**

  - [ ]* 17.4 Write property test for audit entry creation on status transition
    - **Property 4: Audit entry creation on status transition**
    - Generate periods with varying payslip counts (1–50), verify audit entry count equals payslip count after unlock/re-finalise
    - Create `Portal.Tests/PropertyTests/Payroll/AuditStatusChangePropertyTests.cs`
    - **Validates: Requirements 2.5, 5.4**

  - [ ]* 17.5 Write property test for editability gated by period status
    - **Property 5: Editability gated by period status**
    - Generate all 5 statuses, verify edit operations allowed only for Draft (1), Preview (2), and Unlocked (4)
    - Create `Portal.Tests/PropertyTests/Payroll/EditabilityPropertyTests.cs`
    - **Validates: Requirements 3.1, 3.2, 3.3, 3.5**

  - [ ]* 17.6 Write property test for audit trail completeness for field edits
    - **Property 6: Audit trail completeness for field edits**
    - Generate earning line modifications (additions, removals, amount changes), verify correct audit entries with proper FieldName conventions
    - Create `Portal.Tests/PropertyTests/Payroll/AuditFieldEditPropertyTests.cs`
    - **Validates: Requirements 4.3, 4.4, 4.5, 4.6, 4.8**

  - [ ]* 17.7 Write property test for P&L entries match period totals
    - **Property 7: P&L entries match period totals on finalisation**
    - Generate periods with random payslip amounts (1–20 payslips, amounts 100–50000), verify Purchase totals equal SUM(TotalEarnings) and SUM(TotalEmployerContributions)
    - Create `Portal.Tests/PropertyTests/Payroll/PnlTotalsPropertyTests.cs`
    - **Validates: Requirements 6.1, 6.2, 7.2**

  - [ ]* 17.8 Write property test for P&L reversal preserves history
    - **Property 8: P&L reversal preserves history**
    - Generate re-finalise scenarios, verify old Purchase entries marked cancelled with original amounts preserved unchanged
    - Create `Portal.Tests/PropertyTests/Payroll/PnlReversalPropertyTests.cs`
    - **Validates: Requirements 7.1, 7.6**

  - [ ]* 17.9 Write property test for P&L description format
    - **Property 9: P&L description format**
    - Generate random year (2020–2099) and month (1–12) combinations, verify Description matches "Payroll - {MonthName} {Year}"
    - Create `Portal.Tests/PropertyTests/Payroll/PnlDescriptionPropertyTests.cs`
    - **Validates: Requirements 6.4**

  - [ ]* 17.10 Write property test for audit history ordering
    - **Property 10: Audit history ordering**
    - Generate audit entries with random timestamps, verify returned list is ordered by CreatedAtUtc descending
    - Create `Portal.Tests/PropertyTests/Payroll/AuditOrderingPropertyTests.cs`
    - **Validates: Requirements 9.2**

  - [ ]* 17.11 Write property test for re-finalisation validation gate
    - **Property 11: Re-finalisation validation gate**
    - Generate periods where at least one payslip fails calculation validation (missing deduction rate), verify re-finalisation is rejected with no state changes
    - Create `Portal.Tests/PropertyTests/Payroll/RefinalisationValidationPropertyTests.cs`
    - **Validates: Requirements 5.6**

  - [ ]* 17.12 Write property test for ProcessedAtUtc timestamp on re-finalisation
    - **Property 12: ProcessedAtUtc timestamp on re-finalisation**
    - Generate re-finalise operations, verify ProcessedAtUtc is set to a non-null UTC timestamp
    - Create `Portal.Tests/PropertyTests/Payroll/ProcessedAtTimestampPropertyTests.cs`
    - **Validates: Requirements 1.6**

  - [ ]* 17.13 Write property test for optimistic concurrency
    - **Property 13: Optimistic concurrency on status transitions**
    - Simulate concurrent status transitions on same period (mocked repo returns false on second call), verify exactly one succeeds and no partial state changes
    - Create `Portal.Tests/PropertyTests/Payroll/ConcurrencyPropertyTests.cs`
    - **Validates: Requirements 1.5, 2.3**

- [ ] 18. Unit tests
  - [ ]* 18.1 Write unit tests for PayslipPeriodStatusService
    - Create `Portal.Tests/Unit/Payroll/PayslipPeriodStatusServiceTests.cs`
    - Test: Unlock Draft period → Fail with "Only Finalised or Re-finalised periods can be unlocked."
    - Test: Unlock by standard user → Fail with authorisation error
    - Test: Unlock Finalised period by Owner → Success, status = Unlocked
    - Test: Unlock Re-finalised period by SuperAdmin → Success
    - Test: Re-finalise non-Unlocked period → Fail with "Only Unlocked periods can be re-finalised."
    - Test: Re-finalise with missing deduction rate → Fail with validation error including employee name
    - Test: Concurrent unlock (UpdatePeriodStatusAsync returns false) → Fail with concurrency error message
    - Use Moq for repository and service mocks
    - _Requirements: 1.2, 1.3, 1.4, 1.5, 2.1, 2.2, 5.6, 11.1, 11.2_

  - [ ]* 18.2 Write unit tests for PayslipAuditService
    - Create `Portal.Tests/Unit/Payroll/PayslipAuditServiceTests.cs`
    - Test: FieldName for overtime earning line amount change → "EarningLine:Overtime:Amount"
    - Test: FieldName for earning line removal → "EarningLine:Bonus" with OldValue=amount, NewValue=null
    - Test: FieldName for earning line addition → "EarningLine:Bonus" with OldValue=null, NewValue=amount
    - Test: Duplicate earning type disambiguation → "EarningLine:Overtime[0]:Amount", "EarningLine:Overtime[1]:Amount"
    - Test: ManagerNotes change records correct old/new values
    - Test: ManagerNotes unchanged → no audit entry created
    - _Requirements: 4.3, 4.4, 4.5, 4.6_

  - [ ]* 18.3 Write unit tests for PayrollPnlService
    - Create `Portal.Tests/Unit/Payroll/PayrollPnlServiceTests.cs`
    - Test: P&L description for January 2027 → "Payroll - January 2027"
    - Test: P&L description for December 2025 → "Payroll - December 2025"
    - Test: InvoiceNumber format → "PAY-2027-07-SAL" and "PAY-2027-07-EMP"
    - Test: InvoiceDate = last day of period month
    - Test: P&L totals match sum of payslip earnings and employer contributions
    - Test: AdjustPnlEntriesAsync cancels existing entries and creates new ones
    - Test: EnsurePayrollPnlSetupAsync is idempotent (second call doesn't create duplicates)
    - _Requirements: 6.1, 6.2, 6.4, 7.1, 7.2, 7.6_

- [ ] 19. Integration tests
  - [ ]* 19.1 Write integration tests for unlock → edit → re-finalise cycle
    - Create `Portal.Tests/Integration/Payroll/PayrollPhaseB_IntegrationTests.cs`
    - Test full cycle: Finalise period → verify P&L created → Unlock → Edit earning line → Re-finalise → verify P&L adjusted
    - Verify status transitions cascade to payslips at each step
    - Verify audit entries created at each step
    - Use EF Core InMemory provider for database
    - _Requirements: 2.3, 2.4, 3.1, 5.2, 5.3, 6.1, 7.1_

  - [ ]* 19.2 Write integration tests for transaction atomicity
    - Test: Force failure during P&L creation mid-finalisation → verify period status NOT changed, no orphan Purchase records
    - Test: Force failure during re-finalisation P&L adjustment → verify no partial state (status unchanged, old P&L entries still active)
    - _Requirements: 6.5, 6.6, 7.4, 7.5_

  - [ ]* 19.3 Write integration tests for permission enforcement at API level
    - Test: Call AxPostUnlockPeriod as non-Owner user → verify rejection
    - Test: Call AxPostRefinalisePeriod as non-Owner user → verify rejection
    - Test: Verify audit history accessible to standard payroll users (read-only)
    - _Requirements: 11.1, 11.2, 11.3, 11.5_

- [x] 20. Final checkpoint
  - Ensure all tests pass and the full project compiles with no errors
  - Verify end-to-end: controller → service → repository path wired correctly for all Phase B flows
  - Verify DI registrations resolve without runtime errors
  - Verify Phase A retroactive fixes don't break existing Phase A functionality
  - Ask the user if questions arise

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- The Phase A retroactive fix (Task 1) is a strict prerequisite — it must be completed before any other Phase B work
- Optimistic concurrency (Task 1.4) changes the signature of `UpdatePeriodStatusAsync` — all existing callers in Phase A must be updated simultaneously
- The `PeriodStatusNames` replacement (Task 1.3) removes a hardcoded dictionary — ensure all references to the old dictionary are updated
- P&L integration reuses the existing Purchase/Expense system — no new financial tables are created
- The `IsSystemGenerated` supplier flag (Task 15.1) requires both backend protection and UI filtering
- Task 2.4 is critical: after ALTERing Purchase/Supplier tables, ALL raw SQL SELECT queries using `FromSqlRaw` must include the new columns or EF Core will throw at runtime (same root cause as the `IsOnboardingDismissed` incident)
- Task 9.4 builds on Task 1.2 — both modify FinalisePeriodAsync. Task 1.2 adds status cascade, Task 9.4 wraps in a transaction and adds P&L. The final implementation combines both.
- Controller role detection: "Owner" is via `User.HasClaim("IsOwner", "true")`, "SuperAdmin" is via `User.IsInRole("SuperAdmin")`. The controller maps claims to a role string before passing to service layer.
- Audit entries are append-only — the table has no UPDATE/DELETE capability by design
- All AJAX calls follow BlockUI → fetch → Unblock → SweetAlert2 pattern
- Repository uses full table names in queries (no aliases), `catch (Exception ex) { throw; }` pattern
- Property-based tests use FsCheck + FsCheck.Xunit (already in project)
- Unit tests use xUnit + Moq (existing project standard)
- Cross-schema FK (Purchase.PayslipPeriodId → PayslipPeriod.Id) follows established project convention

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "1.4"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3"] },
    { "id": 2, "tasks": ["2.4", "3.1", "3.2"] },
    { "id": 3, "tasks": ["4.1"] },
    { "id": 4, "tasks": ["6.1", "6.2", "6.3"] },
    { "id": 5, "tasks": ["7.1"] },
    { "id": 6, "tasks": ["7.2", "7.3"] },
    { "id": 7, "tasks": ["8.1", "8.2"] },
    { "id": 8, "tasks": ["8.3", "8.4"] },
    { "id": 9, "tasks": ["9.1"] },
    { "id": 10, "tasks": ["9.2", "9.3", "9.4"] },
    { "id": 11, "tasks": ["11.1"] },
    { "id": 12, "tasks": ["12.1", "12.2"] },
    { "id": 13, "tasks": ["13.1", "13.2", "13.3", "13.4"] },
    { "id": 14, "tasks": ["14.1", "14.2", "15.1", "15.2"] },
    { "id": 15, "tasks": ["17.1", "17.2", "17.5", "17.9"] },
    { "id": 16, "tasks": ["17.3", "17.4", "17.6", "17.7", "17.8", "17.10", "17.11", "17.12", "17.13"] },
    { "id": 17, "tasks": ["18.1", "18.2", "18.3"] },
    { "id": 18, "tasks": ["19.1", "19.2", "19.3"] }
  ]
}
```
