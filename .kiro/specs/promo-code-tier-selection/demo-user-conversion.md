# Demo User → Real Registration Conversion

## Problem

When a prospect accesses a demo via magic link, the system creates a dummy `AspNetUsers` record with a random password (per the demo-access-invitations spec). The user never knows this password — it's only used to bridge the demo session.

If that same prospect later tries to register with a promo code (same email), `UserManager.CreateAsync()` fails because the email already exists in Identity.

## Solution

During promo code registration, if the email already exists in `AspNetUsers`:

1. Check if the existing user is **demo-only** (all their `UserBusiness` records point to businesses where `IsDemoAccount = true`)
2. If yes → **convert** the existing user to a real user:
   - Reset password to the one provided in the registration form
   - Update `FirstName`, `LastName` from the registration form
   - Set `EmailConfirmed = false` (forces email confirmation — same as normal registration)
   - Send confirmation email (normal flow)
   - Create `PendingRegistration` record (provisioning happens after email confirmation)
   - The existing demo `UserBusiness` links remain (non-default)
   - Proceed with normal registration success flow (redirect to "confirm your email" page)
3. If no → the user has a real business. Show: "This email is already registered. Please log in."

## Implementation Location

`Portal.Web/Services/RegistrationService.cs` — in `RegisterAsync()`, before `_userManager.CreateAsync()`:

```csharp
// Check if email already exists
var existingUser = await _userManager.FindByEmailAsync(model.Email);

if (existingUser != null)
{
    // Check if this is a demo-only user (all linked businesses are demo accounts)
    var isDemoOnly = await IsDemoOnlyUserAsync(existingUser.Id);

    if (!isDemoOnly)
    {
        return RegistrationResult.Failure("This email is already registered. Please log in.");
    }

    // Convert demo user to real user
    var resetToken = await _userManager.GeneratePasswordResetTokenAsync(existingUser);
    var resetResult = await _userManager.ResetPasswordAsync(existingUser, resetToken, model.Password);
    if (!resetResult.Succeeded)
    {
        return RegistrationResult.Failure("Registration could not be completed. Please try again.");
    }

    existingUser.FirstName = model.FirstName;
    existingUser.LastName = model.LastName;
    existingUser.EmailConfirmed = true;
    await _userManager.UpdateAsync(existingUser);

    // Use existing user for the rest of the flow (PendingRegistration, etc.)
    user = existingUser;
    // Skip _userManager.CreateAsync() — user already exists
}
else
{
    // Normal path: create new user
    user = new ApplicationUser { ... };
    var identityResult = await _userManager.CreateAsync(user, model.Password);
    ...
}
```

## Helper Method

```csharp
private async Task<bool> IsDemoOnlyUserAsync(string userId)
{
    var userBusinesses = await _membershipDbContext.UserBusinesses
        .Where(ub => ub.UserId == userId && ub.IsActive)
        .ToListAsync();

    if (!userBusinesses.Any())
        return true; // No businesses at all — safe to convert

    var businessIds = userBusinesses.Select(ub => ub.BusinessId).ToList();

    // Check if ALL linked businesses are demo accounts
    // This requires querying the Portal DB — use the PortalDbContext or a cross-DB service
    // For now, check against the IsDemoAccount flag via a repository method
    var allAreDemos = await _portalDbContext.Businesses
        .Where(b => businessIds.Contains(b.Id))
        .AllAsync(b => b.IsDemoAccount);

    return allAreDemos;
}
```

## Important Notes

- The demo `UserBusiness` records are NOT deleted — the user can still access demos if invited again
- The new business becomes `IsDefault = true`; demo businesses become `IsDefault = false`
- The `PendingRegistration` record still gets created (for provisioning flow continuity)
- No changes to the demo invitation flow itself — it still creates/reuses demo users as before

## Testing Scenarios

1. **Fresh email (no existing user)** → normal registration path
2. **Email exists, demo-only user** → converts, creates new business, user gets promo tier
3. **Email exists, has real business** → rejected with "already registered" message
4. **Email exists, has both demo + real business** → rejected (not demo-only)
