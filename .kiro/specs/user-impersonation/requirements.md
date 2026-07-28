# User Impersonation — Requirements

## Overview

Allow the SuperAdmin to "Login As" any user on the platform. This enables remote debugging, support resolution, and verifying user-reported issues without needing the user's password.

## Functional Requirements

### FR-1: Impersonation Trigger
- SuperAdmin can click a "Login As" button next to any user on the `/Admin/Users` page.
- The button must NOT appear next to the SuperAdmin's own row.

### FR-2: Session Swap
- When impersonation starts:
  - The SuperAdmin's original session identity is stored (e.g., in a claim or cookie).
  - The system signs in as the target user using ASP.NET Core Identity's `SignInManager.SignInAsync()`.
  - The target user's `BusinessId`, `IsOwner`, and module permissions are applied as claims.
  - No password is required — only SuperAdmin can trigger this.

### FR-3: Impersonation Banner
- While impersonating, a persistent banner is shown at the top of every page:
  - Text: "You are viewing as [Full Name] ([Business Name]) — Return to your account"
  - Style: high-contrast, clearly distinguishable from normal UI (e.g., amber/warning background).
  - The banner must be visible on ALL pages (layout-level).
  - "Return to your account" is a clickable link that ends impersonation.

### FR-4: Return to Admin
- Clicking "Return to your account" in the banner:
  - Signs out the impersonated user session.
  - Restores the SuperAdmin's original session.
  - Redirects to `/Admin/Users`.

### FR-5: Read-Only Safeguard (Optional — Phase 2)
- In this first phase, impersonation is FULL access (the SuperAdmin sees and can do everything the user can do).
- A future phase could add a read-only mode that prevents mutations.

### FR-6: Audit Trail
- Every impersonation start and end must be logged in the `AuditLog` table:
  - Action: "ImpersonationStarted" / "ImpersonationEnded"
  - PerformedByUserId: the SuperAdmin's UserId
  - TargetUserId: the impersonated user's UserId
  - Timestamp

### FR-7: Security Constraints
- Only users with the `SuperAdmin` role can impersonate.
- A SuperAdmin cannot impersonate another SuperAdmin.
- Impersonation must not leak across browser tabs (session-based, not token-based).
- The impersonation state must survive page navigation but NOT survive browser close (session cookie, not persistent).

## Non-Functional Requirements

### NFR-1: Performance
- Impersonation sign-in must complete in under 500ms.

### NFR-2: Safety
- If the impersonation cookie/claim is corrupted, the system must fall back to a logged-out state (never grant access without valid identity).

### NFR-3: No Password Exposure
- The SuperAdmin never sees or needs the target user's password.
- The target user's password hash is never read or compared.
