# Demo User Conversion — Tasks

## Task 1: Inject PortalDbContext into RegistrationService

- Add `PortalDbContext _portalDbContext` field
- Add constructor parameter
- No other service changes in this task

## Task 2: Add IsDemoOnlyUserAsync helper method

- Add private method to `RegistrationService`
- Query `MembershipDbContext.UserBusinesses` for active records by userId
- Query `PortalDbContext.Businesses` to check `IsDemoAccount` on all linked businesses
- Return `true` if user has no businesses OR all businesses are demos
- Return `false` if any business has `IsDemoAccount = false`

## Task 3: Update RegisterAsync — demo user detection and conversion

Replace the existing duplicate email check:
- If email exists in Identity:
  - Call `IsDemoOnlyUserAsync`
  - If demo-only: reset password, update name, set `EmailConfirmed = false`
  - If not: return "An account with this email address already exists."
- If email doesn't exist: normal `CreateAsync` path (unchanged)
- Both paths converge at the `PendingRegistration` / confirmation email logic

## Task 4: Verify build compiles

- Run `dotnet build` on Portal.Web
- Fix any compilation errors

## Task 5: Test scenarios

Manual testing:
1. **Fresh email** → registers normally (no change in behaviour)
2. **Email exists as demo-only user** → converts, receives confirmation email, can confirm and provision
3. **Email exists with real business** → blocked with "already exists" message
4. **Demo user converts then confirms email** → provisioning creates new business + subscription correctly
5. **Converted user can still access demo** → demo UserBusiness still works with demo invitation link
