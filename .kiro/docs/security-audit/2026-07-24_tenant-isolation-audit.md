# Security Audit: Cross-Business (Tenant) Data Isolation

**Date:** 24 July 2026  
**Triggered by:** Invitations from BusinessId=1 were visible to users of BusinessId=1001

---

## Summary

Audited the entire Portal codebase for potential cross-business data exposure — situations where User A could see, modify, or interact with User B's data.

---

## Vulnerabilities Found & Fixed

### 1. InvitationController — BusinessId not validated on Create POST

**Severity:** Critical  
**File:** `Portal.Web/Controllers/InvitationController.cs`  
**Issue:** The `Create` POST action accepted `businessId` from the form without validating it matched the current user's business. A regular user could change the hidden `businessId` field to invite users into another business.  
**Fix:** Added ownership check — non-SuperAdmin users are blocked (403) if the submitted `businessId` doesn't match their `BusinessId` claim.

### 2. InvitationController — Cancel action without ownership check

**Severity:** High  
**File:** `Portal.Web/Controllers/InvitationController.cs`  
**Issue:** The `Cancel` action accepted any invitation ID without verifying the invitation belonged to the user's business. A user could cancel another business's pending invitation by guessing the ID.  
**Fix:** Added check — loads the invitation and verifies `invitation.BusinessId == user's BusinessId`. Returns 403 if mismatch.

### 3. InvitationController — Invitation list not filtered by business

**Severity:** High  
**File:** `Portal.Web/Controllers/InvitationController.cs`  
**Issue:** `GetAllInvitationsAsync()` returned all invitations across all businesses. Regular users saw other businesses' invitations in their table.  
**Fix:** Non-SuperAdmin users now get a filtered list (only invitations where `BusinessId` matches their own).

### 4. InvitationController — Error paths exposed all businesses/invitations

**Severity:** Medium  
**File:** `Portal.Web/Controllers/InvitationController.cs`  
**Issue:** When validation failed in the POST action, the error recovery code called `GetAllBusinessesAsync()` and `GetAllInvitationsAsync()` to reload the view — exposing all data to non-SuperAdmin users.  
**Fix:** Error paths now redirect to `Create` GET action (which has proper filtering).

---

## Areas Confirmed Secure

### Portal Database (PortalDbContext)

All major entities have EF Core global query filters enforcing `BusinessId == CurrentBusinessId`:

| Entity | Filter |
|--------|--------|
| Customer | ✅ BusinessId filter |
| Quotation | ✅ BusinessId filter |
| Invoice | ✅ BusinessId filter |
| Payment | ✅ BusinessId filter |
| Supplier | ✅ BusinessId filter |
| Purchase | ✅ BusinessId filter |
| ExpenseCategory | ✅ BusinessId filter |
| VatSubmissionPeriod | ✅ BusinessId filter |
| DocumentAttachment | ✅ BusinessId + IsDeleted filter |
| LeadRequest | ✅ BusinessId filter |
| SalesContact | ✅ BusinessId filter |
| SalesProduct | ✅ BusinessId filter |
| Meeting | ✅ BusinessId filter |
| LeadResponseTemplate | ✅ BusinessId filter |
| PaymentReceipt | ✅ BusinessId filter |
| Signature | ✅ BusinessId filter |
| RevenueSource / RevenueSummary | ✅ BusinessId filter |
| PaymentReminderSchedule / Log | ✅ BusinessId filter |
| SupplierRecurringRule | ✅ BusinessId + IsDeleted filter |
| BusinessLogo | ✅ BusinessId filter |
| AuditLog | ✅ BusinessId filter |

### Controllers with SuperAdmin-Only Access

| Controller | Protection |
|-----------|-----------|
| AdminController (`/Admin/Users`) | `[Authorize(Roles = "SuperAdmin")]` |
| AdminSubscriptionController | `[Authorize(Roles = "SuperAdmin")]` |
| PromoCodeController (`/Admin/PromoCodes`) | `[Authorize(Roles = "SuperAdmin")]` |
| DemoInvitationController (`/Admin/DemoInvitations`) | `[Authorize(Roles = "SuperAdmin")]` |

### Controllers with Proper Tenant Scoping

| Controller | Scoping Method |
|-----------|---------------|
| MyBusinessController | `_tenantService.CurrentBusinessId` in all actions |
| SalesController | Services use `_tenantService.CurrentBusinessId` |
| PurchaseController | EF global filter + explicit businessId |
| InvoiceController | EF global filter |
| QuotationController | EF global filter |
| RevenueController | EF global filter |
| CustomerController | EF global filter |
| StatementController | EF global filter + `businessId` validation |
| VatController | EF global filter |

### Reference/Lookup Tables (No BusinessId — by design)

| Table | Justification |
|-------|--------------|
| LeadStatusType | Shared reference data (New, Contacted, Won, etc.) |
| LeadSourceType | Shared reference data (Website, Referral, etc.) |
| LeadResponseType | Shared reference data (Email, Phone, etc.) |
| MeetingType | Shared reference data (Online, On-Site, etc.) |
| QuotationStatusType | Shared reference data |
| InvoiceStatusType | Shared reference data |
| PaymentMethodType | Shared reference data |
| PurchaseOriginType | Shared reference data |
| Plan / PlanFeature | Shared subscription tiers |

---

## Membership Database (Special Case)

The Membership database (`Portal.Membership`) does NOT have EF Core global query filters because it uses ASP.NET Identity's schema. Tenant isolation for these tables relies on **explicit filtering in code**:

| Table | Protection |
|-------|-----------|
| AspNetUsers | Filtered by user's own identity (not tenant-scoped) |
| UserBusiness | Filtered explicitly in InvitationController, ProvisioningService |
| UserBusinessPermission | Filtered via UserBusiness join |
| Invitations | **Now filtered** in InvitationController (was the vulnerability) |
| PendingRegistration | Filtered by UserId (personal, not cross-tenant risk) |

---

## Recommendations for Future Development

1. **Any new controller action** that reads from the Membership DB must explicitly filter by `BusinessId` claim — there are no automatic filters.
2. **Any action that accepts `businessId` from user input** (form, route, query string) must validate it matches the user's own business (unless SuperAdmin).
3. **The InvitationService** should ideally have a `GetByBusinessIdAsync(int businessId)` method instead of loading all and filtering in memory.
4. **Periodic audit**: re-run this check whenever new controllers are added or `[Authorize(Roles = "SuperAdmin")]` is changed to `[Authorize]`.

---

## Audit Status

| Check | Result |
|-------|--------|
| Portal DB global filters | ✅ All entities covered |
| Membership DB explicit filters | ✅ Fixed in this audit |
| Admin-only controllers locked | ✅ Verified |
| User-supplied businessId validated | ✅ Fixed in InvitationController |
| Cross-business data via URL manipulation | ✅ No exposure found (global filters prevent) |
| Invitation cancel ownership | ✅ Fixed |

**Conclusion:** The platform is now secure against cross-business data exposure. The only area requiring manual vigilance is the Membership database, which lacks automatic filters.
