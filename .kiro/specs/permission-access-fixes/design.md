# Permission Access Fixes Bugfix Design

## Overview

Six permission and access control inconsistencies exist in the Portal's controller infrastructure. These range from a UI breakage (Starter-plan users seeing error popups on Invoice Detail due to unconditionally rendered reminder partials) to medium-severity gaps where controllers are unmapped in `ModuleControllerMap`, allowing potential bypass of plan-based gating. The fix strategy is to apply minimal, targeted changes to the affected files: conditionally render reminder partials, correct a module access attribute, add missing controller names to the map, and create a migration to seed `audit_log` for the Professional plan.

## Glossary

- **Bug_Condition (C)**: The set of conditions where a controller is either incorrectly gated, unmapped in `ModuleControllerMap`, or a view renders content requiring a module the user's plan doesn't include
- **Property (P)**: The desired behavior — every module-gated controller resolves correctly in `ModuleControllerMap`, view partials only render when the user's plan includes the required module, and plan features are seeded via migrations
- **Preservation**: All existing behavior for users whose plans already include the affected modules must remain unchanged
- **ModuleControllerMap**: Static class in `Portal.Web.Filters` that maps module keys to controller names, used by `PlanPermissionFilter`, `DemoPermissionFilter`, and `UserPermissionFilter` to resolve which module a controller belongs to
- **PlanPermissionFilter**: Global filter that evaluates whether the current user's subscription plan includes the module associated with the requested controller
- **IPlanCheckService.IsModuleInPlanAsync**: Service method that checks if a given module key exists in the current business's active subscription plan features

## Bug Details

### Bug Condition

The bug manifests across six scenarios where the permission infrastructure is inconsistent. The common thread is that controller access is either ungated, incorrectly gated, or the UI renders gated content unconditionally.

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input of type { controllerName: string, userPlan: string, viewContext: string }
  OUTPUT: boolean
  
  RETURN (input.controllerName == "CreditNote" AND getAttribute(input.controllerName) == "invoice")
         OR (input.controllerName IN ["ExpenseCategory", "ExpenseCategoryLimit"] 
             AND ModuleControllerMap.ResolveModule(input.controllerName) == null)
         OR (input.controllerName IN ["Statement", "Logo", "LineItemCatalog", 
             "LineItemCatalogManagement", "ProposalSection"]
             AND ModuleControllerMap.ResolveModule(input.controllerName) == null)
         OR (input.viewContext == "InvoiceDetail" 
             AND input.userPlan does NOT include "payment_reminder_manual"
             AND reminderPartialsRendered == true)
         OR (freshDeploy == true AND "audit_log" NOT IN ProfessionalPlan.features)
END FUNCTION
```

### Examples

- **Issue 1**: A Starter-plan user opens `/Invoice/Detail/5` → `_ReminderHistoryPanel` renders → AJAX call to `PaymentReminderController` is blocked by `PlanPermissionFilter` → SweetAlert2 error popup appears on page load
- **Issue 2**: `CreditNoteController` has `[ModuleAccess(PortalModules.Invoice)]` but `ModuleControllerMap` maps "CreditNote" to the `credit` module → inconsistent authorization evaluation
- **Issue 3**: A user navigates to `/ExpenseCategory/Index` → `PlanPermissionFilter` calls `ModuleControllerMap.ResolveModule("ExpenseCategory")` → returns `null` → filter allows request without plan check
- **Issue 4**: Fresh database deployment → Professional plan has no `audit_log` feature row → Professional users cannot access audit log functionality
- **Issue 5**: A user navigates to `/Statement/Index` → `ModuleControllerMap.ResolveModule("Statement")` returns `null` → filter bypass path executes without plan validation

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- Professional-plan and Enterprise-plan users navigating to Invoice Detail SHALL continue to see the Reminder History Panel and Test Send Modal with full functionality
- Users whose plan includes the `credit` module SHALL continue to access CreditNote features without disruption
- Users whose plan includes the `purchase` module SHALL continue to access Expense, Supplier, and Purchase controllers
- Enterprise-plan users SHALL continue to have `audit_log` access as seeded in migration 097
- All controllers currently mapped correctly in `ModuleControllerMap` SHALL continue resolving to their correct module
- SuperAdmin users SHALL continue to access `AuditController` via role-based authorization regardless of plan features

**Scope:**
All requests and views that do NOT involve the six identified bug conditions should be completely unaffected by these fixes. This includes:
- All other controller actions across the application
- All other view rendering logic
- The PlanPermissionFilter's resolution logic for already-mapped controllers
- Existing plan feature seeding for Starter and Enterprise tiers

## Hypothesized Root Cause

Based on the bug analysis, the root causes are confirmed (not hypothesized) since the code has been inspected:

1. **CreditNoteController Attribute Mismatch**: The `CreditNoteController` was decorated with `[ModuleAccess(PortalModules.Invoice)]` instead of `[ModuleAccess(PortalModules.Credit)]`. This creates a disconnect where the filter resolves the controller to `credit` via the map but the attribute says `invoice`.

2. **Incomplete ModuleControllerMap Entries**: When `ExpenseCategoryController`, `ExpenseCategoryLimitController`, `StatementController`, `LogoController`, `LineItemCatalogController`, `LineItemCatalogManagementController`, and `ProposalSectionController` were created, they were given correct `[ModuleAccess]` attributes but were never added to `ModuleControllerMap.Map`. The global filters fall through to the bypass path when `ResolveModule` returns `null`.

3. **Unconditional Partial Rendering**: The `Invoice/Detail.cshtml` view renders `_ReminderHistoryPanel` and `_TestSendModal` without checking whether the user's plan includes `payment_reminder_manual`. The partials immediately fire AJAX to `PaymentReminderController` which is correctly gated, producing errors for Starter users.

4. **Missing Migration for audit_log on Professional Plan**: Migration 097 seeds `audit_log` only for Enterprise. The Professional plan should also include it, but no migration exists to add it. This was manually inserted in some environments but not captured in source-controlled migrations.

## Correctness Properties

Property 1: Bug Condition - ModuleControllerMap Resolution Completeness

_For any_ controller that has a `[ModuleAccess]` attribute and is not exempt from plan checking (i.e., not SuperAdmin-only), the `ModuleControllerMap.ResolveModule` function SHALL return the correct module key matching the controller's `[ModuleAccess]` attribute value.

**Validates: Requirements 2.2, 2.3, 2.5**

Property 2: Preservation - Existing Module Resolution Unchanged

_For any_ controller that was already correctly mapped in `ModuleControllerMap` before this fix, the `ModuleControllerMap.ResolveModule` function SHALL continue to return the same module key as before, preserving all existing authorization behavior.

**Validates: Requirements 3.2, 3.3, 3.5**

Property 3: Bug Condition - Conditional Reminder Rendering

_For any_ user whose active subscription plan does NOT include `payment_reminder_manual`, the Invoice Detail view SHALL NOT render `_ReminderHistoryPanel` or `_TestSendModal` partials.

**Validates: Requirements 2.1**

Property 4: Preservation - Reminder Rendering for Eligible Plans

_For any_ user whose active subscription plan includes `payment_reminder_manual`, the Invoice Detail view SHALL continue to render `_ReminderHistoryPanel` and `_TestSendModal` partials with full functionality.

**Validates: Requirements 3.1**

Property 5: Bug Condition - Professional Plan audit_log Seeding

_For any_ fresh database deployment, the Professional plan SHALL include `audit_log` as a seeded feature via migration script.

**Validates: Requirements 2.4**

## Fix Implementation

### Changes Required

**File**: `Portal.Web/Views/Invoice/Detail.cshtml`

**Change 1 — Conditional Reminder Partial Rendering**:
1. Wrap the `<!-- Reminder History Panel -->` section and `<!-- Test Send Modal -->` section in `@if (ViewBag.HasPaymentReminderAccess == true)`
2. This prevents the partials from rendering (and firing AJAX) for plans without `payment_reminder_manual`

**File**: `Portal.Web/Controllers/InvoiceController.cs`

**Change 2 — Set ViewBag.HasPaymentReminderAccess**:
1. Inject `IPlanCheckService` into the InvoiceController constructor
2. In the `Detail` action method, call `await _planCheckService.IsModuleInPlanAsync(PortalModules.PaymentReminderManual)`
3. Assign result to `ViewBag.HasPaymentReminderAccess`

**File**: `Portal.Web/Controllers/CreditNoteController.cs`

**Change 3 — Correct Module Access Attribute**:
1. Change `[ModuleAccess(PortalModules.Invoice)]` to `[ModuleAccess(PortalModules.Credit)]`

**File**: `Portal.Web/Filters/ModuleControllerMap.cs`

**Change 4 — Add Missing Controller Names to Existing Map Entries**:
1. Add `"ExpenseCategory"`, `"ExpenseCategoryLimit"` to the `PortalModules.Purchase` entry
2. Add `"Statement"` to the `PortalModules.Revenue` entry
3. Add `"Logo"`, `"LineItemCatalog"`, `"LineItemCatalogManagement"`, `"ProposalSection"` to the `PortalModules.Quotation` entry

**File**: `Portal.Database/Migrations/105_AddAuditLogToProfessionalPlan.sql`

**Change 5 — Create Migration for Professional Plan audit_log**:
1. Create idempotent migration that inserts `audit_log` feature for the Professional plan
2. Use `IF NOT EXISTS` guard to prevent duplicate inserts
3. Follow existing migration conventions (USE [Portal], GO, header comments)

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bug on unfixed code, then verify the fix works correctly and preserves existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bugs BEFORE implementing the fix. Confirm the root causes identified above.

**Test Plan**: Write tests that verify `ModuleControllerMap.ResolveModule` returns `null` for the unmapped controllers, and verify that `CreditNoteController`'s attribute conflicts with its map entry. Run these tests on the UNFIXED code to observe failures.

**Test Cases**:
1. **ExpenseCategory Resolution Test**: Call `ModuleControllerMap.ResolveModule("ExpenseCategory")` — returns `null` on unfixed code (should return `"purchase"`)
2. **ExpenseCategoryLimit Resolution Test**: Call `ModuleControllerMap.ResolveModule("ExpenseCategoryLimit")` — returns `null` on unfixed code (should return `"purchase"`)
3. **Statement Resolution Test**: Call `ModuleControllerMap.ResolveModule("Statement")` — returns `null` on unfixed code (should return `"revenue"`)
4. **Logo Resolution Test**: Call `ModuleControllerMap.ResolveModule("Logo")` — returns `null` on unfixed code (should return `"quotation"`)
5. **LineItemCatalog Resolution Test**: Call `ModuleControllerMap.ResolveModule("LineItemCatalog")` — returns `null` on unfixed code (should return `"quotation"`)
6. **CreditNote Attribute Mismatch Test**: Reflect `CreditNoteController`'s `[ModuleAccess]` attribute — reads `PortalModules.Invoice` but map resolves to `PortalModules.Credit`

**Expected Counterexamples**:
- `ModuleControllerMap.ResolveModule` returns `null` for all unmapped controllers
- `CreditNoteController` attribute value differs from its map resolution

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed code produces the expected behavior.

**Pseudocode:**
```
FOR ALL controllerName WHERE isBugCondition(controllerName) DO
  result := ModuleControllerMap_fixed.ResolveModule(controllerName)
  ASSERT result != null
  ASSERT result == expectedModule(controllerName)
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed `ModuleControllerMap` produces the same result as the original.

**Pseudocode:**
```
FOR ALL controllerName WHERE NOT isBugCondition(controllerName) DO
  ASSERT ModuleControllerMap_original.ResolveModule(controllerName) 
      == ModuleControllerMap_fixed.ResolveModule(controllerName)
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many controller name permutations automatically
- It catches edge cases where adding new entries might accidentally shadow existing ones
- It provides strong guarantees that all pre-existing resolutions remain unchanged

**Test Plan**: Capture the original `ModuleControllerMap.Map` state, apply the fix, then write property-based tests verifying all original mappings still resolve identically.

**Test Cases**:
1. **Existing Customer Module Preservation**: Verify `ResolveModule("Customer")` still returns `"customer"` after fix
2. **Existing Invoice Module Preservation**: Verify `ResolveModule("Invoice")` still returns `"invoice"` after fix
3. **Existing CreditNote Module Preservation**: Verify `ResolveModule("CreditNote")` still returns `"credit"` after fix (this changes from the attribute perspective but the map already had this)
4. **Case-Insensitive Preservation**: Verify case-insensitive lookup continues to work for all existing entries

### Unit Tests

- Test `ModuleControllerMap.ResolveModule` returns correct module for each newly added controller name
- Test `CreditNoteController` has `[ModuleAccess(PortalModules.Credit)]` via reflection
- Test that `ViewBag.HasPaymentReminderAccess` is set correctly in `InvoiceController.Detail` for different plan states
- Test that `_ReminderHistoryPanel` partial is not rendered when `HasPaymentReminderAccess` is false

### Property-Based Tests

- Generate random controller names from the complete map and verify all resolve to a valid module key
- Generate random controller names NOT in the map and verify they resolve to `null` (bypass path)
- Verify that for all plan tiers, the correct set of modules is accessible

### Integration Tests

- Test full flow: Starter user loads Invoice Detail → no error popup, no reminder panel visible
- Test full flow: Professional user loads Invoice Detail → reminder panel renders, AJAX succeeds
- Test CreditNote access with `credit` module in plan → access granted
- Test ExpenseCategory access with `purchase` module in plan → access granted via filter
- Test Statement access with `revenue` module in plan → access granted via filter
