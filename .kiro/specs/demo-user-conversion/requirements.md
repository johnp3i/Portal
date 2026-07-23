# Demo User → Real Registration Conversion

## Overview

When a prospect receives a demo invitation, the system automatically creates a dummy `AspNetUsers` record with a random password to bridge the demo session. If that same prospect later registers with a promo code (or a paid plan), the registration fails because the email already exists in Identity.

This spec defines the behaviour for detecting demo-only users during registration and converting them to real users seamlessly — following the normal email confirmation flow.

## Background

- Demo invitations create `AspNetUsers` records automatically (see `.kiro/specs/demo-access-invitations/design.md`)
- The demo user's password is random and unknown to the user
- The demo user has `UserBusiness` records pointing only to demo businesses (`IsDemoAccount = true`)
- The demo user has `EmailConfirmed = true` (set during demo sign-in)
- Real registrations require `EmailConfirmed = false` and a confirmation email flow

## Requirements

### R1: Detect demo-only users during registration

When a user submits the registration form and the email already exists in `AspNetUsers`:
- Query `[membership].[UserBusiness]` for all active records linked to that user
- Query `[portal].[Business]` for those business IDs
- If **all** linked businesses have `IsDemoAccount = true` → this is a demo-only user (convertible)
- If **any** linked business has `IsDemoAccount = false` → this is a real user (block registration)
- If the user has **no** `UserBusiness` records → treat as demo-only (safe to convert)

### R2: Convert demo user to real user

When a demo-only user is detected:
- Reset password to the one provided in the registration form (via `GeneratePasswordResetTokenAsync` + `ResetPasswordAsync`)
- Update `FirstName` and `LastName` from the registration form
- Set `EmailConfirmed = false` (forces email confirmation)
- Do NOT delete the existing demo `UserBusiness` records
- Proceed with the normal registration flow (PendingRegistration, confirmation email, provisioning after confirm)

### R3: Preserve demo access

After conversion:
- Existing demo `UserBusiness` records remain intact (user can still access demos if invited again)
- The new real business (created during provisioning) becomes `IsDefault = true`
- Demo `UserBusiness` records become `IsDefault = false`

### R4: Block real user duplicates

When a user with at least one non-demo business attempts to register with the same email:
- Return: "An account with this email address already exists."
- No changes to the existing user
- Same behaviour as today for genuine duplicates

### R5: Email confirmation flow

The converted user must:
- Receive a confirmation email (same as new registrations)
- Click the confirmation link to verify their email
- Only after confirmation does provisioning occur (business creation, subscription, permissions)
- This ensures the user genuinely controls the email address

### R6: Logging

Log the conversion event:
- `LogInformation("Demo user {UserId} converted for real registration with email {Email}")`
- Include the user ID and email for traceability

## Files to modify

| File | Change |
|------|--------|
| `Portal.Web/Services/RegistrationService.cs` | Replace duplicate check with demo-only detection + conversion logic |
| `Portal.Web/Services/RegistrationService.cs` | Add `IsDemoOnlyUserAsync` helper method |
| `Portal.Web/Services/RegistrationService.cs` | Inject `PortalDbContext` for `IsDemoAccount` check |

## Out of scope

- Changing the demo invitation flow (it still creates dummy users as before)
- Migrating existing demo users retroactively
- In-app promo code redemption (post-login upgrade)
- Deleting demo `UserBusiness` records on conversion
