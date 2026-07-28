# User Impersonation — Design

## Architecture

```
Admin/Users page → "Login As" button
    → POST /Admin/Users/Impersonate/{userBusinessId}
        → ImpersonationService.StartAsync(targetUserBusinessId, superAdminUserId)
            → Store original identity in session/cookie
            → SignInManager.SignInAsync(targetUser, isPersistent: false)
            → Add claim: "IsImpersonating" = "true"
            → Add claim: "OriginalUserId" = superAdminUserId
            → Add claim: "OriginalUserName" = superAdminDisplayName
            → Write AuditLog
        → Redirect to /Home/Index (as impersonated user)

Banner "Return to your account"
    → POST /Admin/Users/EndImpersonation
        → ImpersonationService.EndAsync()
            → Read "OriginalUserId" from claims
            → SignInManager.SignInAsync(originalUser, isPersistent: false)
            → Remove impersonation claims
            → Write AuditLog
        → Redirect to /Admin/Users
```

## Implementation Approach

### Claims-Based State

Impersonation state is tracked via additional claims added during sign-in:

| Claim | Value | Purpose |
|-------|-------|---------|
| `IsImpersonating` | `"true"` | Enables banner rendering |
| `OriginalUserId` | SuperAdmin's UserId | Used to restore session |
| `OriginalUserName` | SuperAdmin's display name | Shown in banner |

These claims are added to the `ClaimsPrincipal` during `SignInManager.SignInAsync()` using `AdditionalClaims`.

### Service: ImpersonationService

```csharp
public class ImpersonationService
{
    Task<ServiceResult> StartImpersonationAsync(int targetUserBusinessId, ClaimsPrincipal currentUser);
    Task<ServiceResult> EndImpersonationAsync(ClaimsPrincipal currentUser);
}
```

**StartImpersonationAsync:**
1. Validate caller is SuperAdmin
2. Load target UserBusiness + ApplicationUser
3. Validate target is not SuperAdmin
4. Build claims for target user (BusinessId, IsOwner, permissions, etc.)
5. Add impersonation claims (IsImpersonating, OriginalUserId, OriginalUserName)
6. Call `SignInManager.SignInAsync(targetUser, new AuthenticationProperties { IsPersistent = false }, additionalClaims)`
7. Write audit log entry

**EndImpersonationAsync:**
1. Read OriginalUserId from claims
2. Load original SuperAdmin user
3. Sign in as original user (restoring all original claims)
4. Write audit log entry

### Banner Component

A Razor partial in `_Layout.cshtml` that renders when `User.HasClaim("IsImpersonating", "true")`:

```html
@if (User.HasClaim("IsImpersonating", "true"))
{
    <div class="impersonation-banner">
        You are viewing as <strong>@User.Identity.Name</strong> — 
        <form method="post" action="/Admin/Users/EndImpersonation" style="display:inline;">
            @Html.AntiForgeryToken()
            <button type="submit">Return to your account</button>
        </form>
    </div>
}
```

### Admin/Users View Changes

Add a "Login As" button in the Actions column:

```html
@if (!isSelf && user.Role != "SuperAdmin")
{
    <button onclick="impersonateUser(@user.UserBusinessId, '@user.FullName')">Login As</button>
}
```

With SweetAlert2 confirmation before POSTing.

### Sign-In Claims Resolution

The existing claims resolution logic (in `CustomClaimsFactory` or `SignInManager` override) must be reused when signing in as the target user. This ensures the impersonated session has the correct:
- `BusinessId` claim
- `IsOwner` claim  
- Module permission claims
- Any other business-specific claims

### Security

- `[Authorize(Roles = "SuperAdmin")]` on both Impersonate and EndImpersonation endpoints
- Except: EndImpersonation must also work when the impersonated user doesn't have SuperAdmin role — so it checks `User.HasClaim("IsImpersonating", "true")` instead
- CSRF protection via `[ValidateAntiForgeryToken]`
- No impersonating other SuperAdmins

## Files to Create/Modify

### New Files
| File | Purpose |
|------|---------|
| `Portal.Web/Services/ImpersonationService.cs` | Start/End impersonation logic |

### Modified Files
| File | Change |
|------|--------|
| `Portal.Web/Controllers/AdminController.cs` | Add Impersonate + EndImpersonation actions |
| `Portal.Web/Views/Admin/Index.cshtml` | Add "Login As" button + JS confirmation |
| `Portal.Web/Views/Shared/_Layout.cshtml` | Add impersonation banner |
| `Portal.Web/Program.cs` | Register ImpersonationService |

## Edge Cases

1. **Tab isolation**: Since claims are in the auth cookie, ALL tabs share the impersonated state. This is acceptable — same as normal login.
2. **Session expiry during impersonation**: User gets logged out entirely. On re-login, they're back as themselves (SuperAdmin). Safe.
3. **Target user is deactivated**: Allow impersonation of deactivated users (useful for debugging "why can't they log in" scenarios). The `SignInAsync` bypasses login checks.
4. **Concurrent impersonation**: Only one impersonation at a time. Starting a new one replaces the current session.
