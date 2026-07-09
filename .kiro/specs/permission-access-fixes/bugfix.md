# Bugfix Requirements Document

## Introduction

Six permission and access control inconsistencies have been identified in the Portal controller infrastructure. These range from a critical UI breakage (Starter users seeing error popups on Invoice Detail) to medium-severity gaps where controllers are unmapped in `ModuleControllerMap`, allowing potential bypass of plan-based gating. Together, these issues degrade security posture and user experience for specific plan tiers.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN a Starter-plan user navigates to Invoice Detail (`/Invoice/Detail/{id}`) THEN the system unconditionally renders `_ReminderHistoryPanel` and `_TestSendModal` partials, which fire AJAX calls to `PaymentReminderController` (gated by `payment_reminder_manual`), resulting in a SweetAlert2 error popup on page load.

1.2 WHEN the `PlanPermissionFilter` evaluates `CreditNoteController` THEN the system finds a mismatch: the controller's `[ModuleAccess(PortalModules.Invoice)]` attribute disagrees with `ModuleControllerMap` which maps "CreditNote" to the `credit` module, creating inconsistent authorization behavior.

1.3 WHEN a demo user or the `PlanPermissionFilter` attempts to resolve the module for `ExpenseCategoryController` or `ExpenseCategoryLimitController` THEN the system finds no matching entry in `ModuleControllerMap`, allowing the request to bypass plan-based module restriction.

1.4 WHEN a fresh database is deployed THEN the system does not include the `audit_log` module for the Professional plan because it was manually inserted and is not present in any migration script, leaving Professional-plan users without audit log access.

1.5 WHEN the `PlanPermissionFilter` attempts to resolve the module for `StatementController`, `LogoController`, `LineItemCatalogController`, `LineItemCatalogManagementController`, or `ProposalSectionController` THEN the system finds no matching entry in `ModuleControllerMap`, potentially allowing unfiltered access via the global filter bypass path.

1.6 WHEN the `audit` module key is evaluated for plan seeding THEN the system finds it exists in `PortalModules.All` but is not seeded in any plan — however this is intentional as it is only used by SuperAdmin controllers that bypass plan checks via `[Authorize(Roles = "SuperAdmin")]`.

### Expected Behavior (Correct)

2.1 WHEN a Starter-plan user navigates to Invoice Detail THEN the system SHALL only render `_ReminderHistoryPanel` and `_TestSendModal` partials if the user's plan includes the `payment_reminder_manual` module; otherwise the reminder section SHALL be hidden without errors.

2.2 WHEN the `PlanPermissionFilter` evaluates `CreditNoteController` THEN the system SHALL use `[ModuleAccess(PortalModules.Credit)]` so the attribute is consistent with the `ModuleControllerMap` entry that maps "CreditNote" to the `credit` module.

2.3 WHEN the `PlanPermissionFilter` attempts to resolve the module for `ExpenseCategoryController` or `ExpenseCategoryLimitController` THEN the system SHALL find them mapped under the `purchase` module in `ModuleControllerMap`.

2.4 WHEN a fresh database is deployed THEN the system SHALL include `audit_log` as a feature for the Professional plan via a migration script, ensuring consistent state across all environments.

2.5 WHEN the `PlanPermissionFilter` attempts to resolve the module for `StatementController`, `LogoController`, `LineItemCatalogController`, `LineItemCatalogManagementController`, or `ProposalSectionController` THEN the system SHALL find them mapped to the appropriate module entries in `ModuleControllerMap`.

2.6 WHEN the `audit` module key is present in `PortalModules.All` THEN the system SHALL leave it as-is for backward compatibility since `AuditController` already relies on `[Authorize(Roles = "SuperAdmin")]` without `[ModuleAccess]`.

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a Professional-plan or Enterprise-plan user navigates to Invoice Detail THEN the system SHALL CONTINUE TO render the `_ReminderHistoryPanel` and `_TestSendModal` partials with full functionality (since these plans include `payment_reminder_manual`).

3.2 WHEN any user accesses CreditNote functionality and their plan includes the `credit` module THEN the system SHALL CONTINUE TO allow access without disruption.

3.3 WHEN any user accesses Expense, Supplier, or Purchase functionality and their plan includes the `purchase` module THEN the system SHALL CONTINUE TO allow access through the existing `[ModuleAccess(PortalModules.Purchase)]` attribute on those controllers.

3.4 WHEN Enterprise-plan users access the `audit_log` module THEN the system SHALL CONTINUE TO have that module available as seeded in migration 097.

3.5 WHEN any controller that is already correctly mapped in `ModuleControllerMap` is accessed THEN the system SHALL CONTINUE TO resolve to its correct module without changes.

3.6 WHEN a SuperAdmin accesses the `AuditController` THEN the system SHALL CONTINUE TO allow access via role-based authorization regardless of plan features.
