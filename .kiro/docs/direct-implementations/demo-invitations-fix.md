# Demo Invitations — Bug Fix & Improvements

**Date:** 27 August 2026, Thursday
**Session type:** Review-driven fixes

## Summary

Fixed a runtime error on `/Admin/DemoInvitations` and performed a comprehensive review of the demo account structure, addressing 8 architectural issues plus 2 user-requested enhancements.

## Bug Fix

**IsOnboardingDismissed missing from FromSql query** — `DemoInvitationRepository.GetDemoBusinessesAsync()` queried `[portal].[Business]` with an explicit SELECT column list that was missing `[IsOnboardingDismissed]`. The column was added in migration 161 and the entity was updated, but this query was not. EF Core threw `InvalidOperationException` when trying to materialize the `Business` entity. Fixed by adding the missing column.

## Improvements Implemented

### 1. GetByIdAsync added to DemoInvitationRepository
- `ResendEmailAsync` was loading ALL invitations via `GetAllAsync()` to find one by ID — a full table scan per resend.
- Added `GetByIdAsync(int id)` method and updated `ResendEmailAsync` to use it.

### 2. Permissions cached in claims (zero DB calls per request)
- `DemoPermissionFilter` was calling `GetPermissionsForInvitationAsync` on every HTTP request during a demo session, hitting the database each time.
- Permissions are now serialized as a JSON claim (`DemoPermissions`) at sign-in in `DemoController.Enter`. The filter reads from claims — no DB call.

### 3. Invitation status revalidation with IMemoryCache
- Previously, if an admin revoked an invitation mid-session, the demo user continued until the 2-hour cookie expired.
- Added `DemoInvitationExpiresAtUtc` claim for fast expiry check (no DB). Added periodic status revalidation via `IMemoryCache` with 5-minute TTL using `GetInvitationStatusAsync`.
- Registered `AddMemoryCache()` in `Program.cs`.

### 4. Transactional permission update
- `UpdatePermissionsAsync` ran `DELETE` then loop `INSERT` without a transaction — partial writes were possible if an insert failed.
- Replaced with `ReplacePermissionsAsync` — a single method that wraps delete + inserts in an ADO.NET transaction with commit/rollback.

### 5. Catch block convention fix
- All `catch (Exception)`, `catch (ValidationException)`, and `catch (InvalidOperationException)` blocks in `DemoInvitationRepository.cs` and `DemoInvitationService.cs` updated to include the `ex` variable per coding golden rules.

### 6. O(1) module resolution in ModuleControllerMap
- `ResolveModule` was using LINQ `FirstOrDefault` over all dictionary entries — O(n) per call, running on every request.
- Built a static reverse dictionary (`controller → module`) at class initialization. `ResolveModule` is now a single `TryGetValue` — O(1).

### 7. Email failure warning surfaced to admin
- `CreateAsync` previously swallowed email failures silently — the admin saw a success message with no indication the email didn't go out.
- Created `DemoInvitationCreateResult` model with `IsEmailSent` flag. Controller now returns `{ success: true, warning: true, message: "Invitation created but email delivery failed. Use Resend to retry." }` when email fails.

### 8. Customer email check on invitation creation
- No validation existed to prevent sending demo invitations to existing customers.
- Added `IsCustomerEmailAsync` to the repository — queries `[customer].[Customer]` by email.
- `CreateAsync` now rejects invitations to emails belonging to existing customers with: "This email belongs to an existing customer. Demo invitations should only be sent to new prospects, not current customers."

### 9. Missing modules in CHECK constraint and ModuleControllerMap
- The `CK_DemoInvitationPermission_Module` CHECK constraint (migration 152) was missing `stripe_connect`, `compliance`, and `payroll`.
- Created **migration 180** to drop and recreate the constraint with all 32 modules synced to `PortalModules.All`.
- Added missing controller mappings to `ModuleControllerMap`: `PayrollReport`, `PayrollCompliance` (Payroll module), `Receipt` (Revenue module).

## Files Modified

| File | Changes |
|------|---------|
| `DemoInvitationRepository.cs` | Added `GetByIdAsync`, `ReplacePermissionsAsync`, `IsCustomerEmailAsync`. Fixed catch blocks. |
| `DemoInvitationService.cs` | Uses `GetByIdAsync` for resend. Returns `DemoInvitationCreateResult`. Customer email check. `GetInvitationStatusAsync`. Transactional permission update. Fixed catch blocks. |
| `IDemoInvitationService.cs` | Updated `CreateAsync` return type. Added `GetInvitationStatusAsync`. |
| `DemoInvitationCreateResult.cs` | New model (Invitation + IsEmailSent). |
| `DemoController.cs` | Serializes permissions + expiry as claims at sign-in. |
| `DemoPermissionFilter.cs` | Reads permissions from claims. Expiry check from claims. IMemoryCache status revalidation. |
| `DemoInvitationController.cs` | Surfaces email warning in JSON response. |
| `ModuleControllerMap.cs` | O(1) reverse lookup. Added missing controllers. |
| `Program.cs` | Added `AddMemoryCache()`. |
| `180_ExpandDemoPermissionModuleConstraint_v2.sql` | Adds stripe_connect, compliance, payroll to CHECK constraint. |
