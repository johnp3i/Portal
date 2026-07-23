# Demo User Conversion — Design

## Data Flow

```
User submits registration form (email: prospect@example.com)
    │
    ▼
FindByEmailAsync(email)
    │
    ├── NULL → Normal path: CreateAsync → PendingRegistration → Confirmation Email
    │
    └── EXISTS → Check IsDemoOnlyUserAsync(userId)
                    │
                    ├── TRUE (demo-only) → Convert:
                    │       1. ResetPasswordAsync(newPassword)
                    │       2. Update FirstName, LastName
                    │       3. Set EmailConfirmed = false
                    │       4. Create PendingRegistration
                    │       5. Send confirmation email
                    │       6. Return success
                    │
                    └── FALSE (has real business) → Return "already exists" error
```

## IsDemoOnlyUserAsync Logic

```csharp
private async Task<bool> IsDemoOnlyUserAsync(string userId)
{
    // Get all active UserBusiness records for this user
    var userBusinesses = await _membershipDbContext.UserBusinesses
        .Where(ub => ub.UserId == userId && ub.IsActive)
        .Select(ub => ub.BusinessId)
        .ToListAsync();

    // No businesses at all → safe to convert (orphan demo user)
    if (!userBusinesses.Any())
        return true;

    // Check if ALL linked businesses are demo accounts
    var allAreDemos = await _portalDbContext.Businesses
        .Where(b => userBusinesses.Contains(b.Id))
        .AllAsync(b => b.IsDemoAccount);

    return allAreDemos;
}
```

## Cross-Database Query

This requires querying both databases in a single operation:
- `MembershipDbContext` → `UserBusiness` (to get business IDs)
- `PortalDbContext` → `Business` (to check `IsDemoAccount`)

Both are already available as scoped services. The `RegistrationService` currently has `MembershipDbContext` — we add `PortalDbContext`.

## Conversion Sequence

```csharp
// 1. Reset password
var resetToken = await _userManager.GeneratePasswordResetTokenAsync(existingUser);
var resetResult = await _userManager.ResetPasswordAsync(existingUser, resetToken, model.Password);

// 2. Update profile
existingUser.FirstName = model.FirstName;
existingUser.LastName = model.LastName;
existingUser.EmailConfirmed = false;
await _userManager.UpdateAsync(existingUser);

// 3. Continue with normal flow (PendingRegistration + confirmation email)
// The `user` variable points to existingUser from here on
```

## Edge Cases

| Case | Behaviour |
|------|-----------|
| User has 0 UserBusiness records | Demo-only (convert) |
| User has 1 demo UserBusiness | Demo-only (convert) |
| User has 3 demo UserBusiness records | Demo-only (convert) |
| User has 1 demo + 1 real UserBusiness | NOT demo-only (block) |
| User has 0 demo + 1 real UserBusiness | NOT demo-only (block) |
| Password reset fails (Identity rules) | Return Identity errors to the user |
| User already has a PendingRegistration | Should still work — new PendingRegistration overwrites intent |

## Security Considerations

- The conversion resets the password — only the person who controls the email can complete registration (via confirmation link)
- Setting `EmailConfirmed = false` ensures the demo cookie (which was authenticated) is invalidated for protected routes that require confirmed email
- The demo session cookie (`DemoScheme`) is separate from the primary auth cookie — no conflict
