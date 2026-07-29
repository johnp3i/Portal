# Tasks: Stripe API Keys Configuration

## Task 1: Database Schema and Entity

- [x] 1.1 Create SQL migration script `Portal.Database/Migrations/XXX_CreateStripeBusinessApiKeysTable.sql`:
  - Create `[stripe]` schema
  - Create `[stripe].[BusinessApiKeys]` table with columns: Id (PK, IDENTITY), BusinessId (FK), KeyType (NVARCHAR(50)), EncryptedValue (NVARCHAR(MAX)), CreatedAtUtc (DATETIME, DEFAULT GETUTCDATE()), UpdatedAtUtc (DATETIME NULL)
  - Add UNIQUE constraint on (BusinessId, KeyType)
  - Add FK to `[portal].[Businesses](Id)`
- [x] 1.2 Create `Portal.Infrastructure/Entities/BusinessApiKey.cs` entity class with navigation property to Business
- [x] 1.3 Create `Portal.Infrastructure/Constants/StripeKeyTypes.cs` static class with `ConnectClientId`, `SecretKey`, `WebhookSecret` constants
- [x] 1.4 Add `DbSet<BusinessApiKey>` to `PortalDbContext` and configure EF Core mapping (unique index, FK relationship, default value for CreatedAtUtc)

## Task 2: Encryption Service

- [x] 2.1 Create `Portal.Web/Services/Stripe/IStripeKeyEncryptionService.cs` interface with `Encrypt(string)`, `Decrypt(string)`, and `Mask(string)` methods
- [x] 2.2 Create `Portal.Web/Services/Stripe/StripeKeyEncryptionService.cs` implementation:
  - Inject `IDataProtectionProvider`
  - Create protector with purpose string `"StripeApiKeys.v1"`
  - `Encrypt` calls `protector.Protect(plainText)`
  - `Decrypt` calls `protector.Unprotect(cipherText)`
  - `Mask` returns prefix + `****...` + last 4 characters (handle short keys gracefully)
- [x] 2.3 Register `IStripeKeyEncryptionService` as scoped in DI (Program.cs or service extension)

## Task 3: Repository

- [x] 3.1 Create `Portal.Infrastructure/Repositories/BusinessApiKeysRepository.cs`:
  - `GetByBusinessIdAsync(int businessId)` — returns all keys for a business
  - `GetByBusinessIdAndKeyTypeAsync(int businessId, string keyType)` — returns single key
  - `UpsertAsync(BusinessApiKey entity)` — INSERT or UPDATE based on existence (use BusinessId + KeyType unique constraint)
  - `DeleteAllByBusinessIdAsync(int businessId)` — deletes all keys for a business
- [x] 3.2 Register `BusinessApiKeysRepository` in DI

## Task 4: Key Resolution Service

- [x] 4.1 Create `Portal.Web/Services/Stripe/IStripeKeyResolutionService.cs` interface with `ResolveKeysAsync(int businessId)` and `HasBusinessKeysAsync(int businessId)`
- [x] 4.2 Create `Portal.Web/Services/Stripe/ResolvedStripeKeys.cs` model class
- [x] 4.3 Create `Portal.Web/Services/Stripe/StripeKeyResolutionService.cs` implementation:
  - Inject `BusinessApiKeysRepository`, `IStripeKeyEncryptionService`, `IOptions<StripeSettings>`, `IHttpContextAccessor`
  - `ResolveKeysAsync`: query DB for business keys → if all three exist, decrypt and return with `IsFromDatabase = true` → else fallback to `IOptions<StripeSettings>` values
  - Auto-generate `ConnectOAuthRedirectUri` from current request domain (e.g., `https://{host}/MyBusiness/StripeOAuthCallback`)
  - `HasBusinessKeysAsync`: check if any keys exist in DB for businessId
- [x] 4.4 Register `IStripeKeyResolutionService` as scoped in DI
- [x] 4.5 Refactor `StripeConnectService` to inject `IStripeKeyResolutionService` instead of reading directly from `IOptions<StripeSettings>`:
  - Replace `_stripeSettings.ConnectClientId` with resolved key
  - Replace `_stripeSettings.ConnectOAuthRedirectUri` with resolved redirect URI
  - Keep `IOptions<StripeSettings>` only for non-Connect settings (PublishableKey, DefaultTaxRateId, BaseUrl, WebhookSigningSecret for platform webhook)

## Task 5: Controller Endpoints

- [x] 5.1 Add `AxGetStripeKeyStatus` endpoint to `MyBusinessController`:
  - If owner: query DB, decrypt, mask, return `StripeKeyStatusResponse` with masked values
  - If team member: return only `IsConfigured` boolean
  - If no keys: return empty/null masked values with `IsConfigured = false`
- [x] 5.2 Add `AxPostSaveStripeKeys` endpoint to `MyBusinessController`:
  - `CanEdit()` check → 403 if not owner
  - Validate request model (at least SecretKey required)
  - Call Stripe API `new AccountService().GetAsync("self")` with provided SecretKey to validate
  - On success: encrypt each key via `IStripeKeyEncryptionService`, upsert via repository
  - Log audit entry (save action)
  - Return `{ success: true, message: "Keys saved successfully." }`
  - On Stripe validation failure: return `{ success: false, message: "..." }` identifying which key failed
  - On Stripe unreachable: return `{ success: false, message: "Validation could not be completed." }`
- [x] 5.3 Add `AxPostRevealStripeKey` endpoint to `MyBusinessController`:
  - `CanEdit()` check → 403 if not owner
  - Rate limit check (10 requests/min/user) — use in-memory rate limiter or `IMemoryCache`
  - Validate `KeyType` is in `StripeKeyTypes.All`
  - Retrieve from DB, decrypt, return full value
  - Log audit entry (reveal action with key type)
  - Return `{ success: true, value: "sk_test_..." }`
- [x] 5.4 Add `AxPostDeleteStripeKeys` endpoint to `MyBusinessController`:
  - `CanEdit()` check → 403 if not owner
  - Delete all keys for business via repository
  - Log audit entry (delete action)
  - Return `{ success: true, message: "Keys removed. Platform defaults will be used." }`

## Task 6: Audit Logging

- [x] 6.1 Add audit log entries for key management actions using existing `AuditLog` table or `SystemLoggerExtensions`:
  - Action types: `stripe_keys_saved`, `stripe_key_revealed`, `stripe_keys_deleted`
  - Include: UserId, BusinessId, KeyType (for reveal), Timestamp
  - Use existing audit infrastructure pattern from the codebase

## Task 7: UI — Key Configuration Panel

- [x] 7.1 Add Stripe API Keys section to the Automation tab view (above existing "Card Payments (Stripe Connect)" section):
  - For owners: three input fields (Connect Client ID, Secret Key, Webhook Secret) with masked display when saved
  - For non-owners: read-only badge showing "Stripe: Configured" or "Stripe: Not configured"
  - "Save Keys" button, "Remove Keys" button (destructive, with SweetAlert2 confirmation)
  - "Reveal" button next to each masked key field
  - Visual indicator showing whether keys enable the Connect flow
- [x] 7.2 Implement JavaScript for Save Keys:
  - `BlockUI.show('Validating and saving...')` → fetch `AxPostSaveStripeKeys` → `BlockUI.hide()` → SweetAlert2 result
  - On success: reload panel to show masked keys
  - On validation error: show which key failed
- [x] 7.3 Implement JavaScript for Reveal Key:
  - `BlockUI.show('Decrypting...')` → fetch `AxPostRevealStripeKey` → `BlockUI.hide()` → populate input field with full value
  - Auto-hide after 30 seconds (re-mask)
- [x] 7.4 Implement JavaScript for Delete Keys:
  - SweetAlert2 confirmation dialog (destructive style, `confirmButtonColor: '#C24A4A'`)
  - If active connection with no platform fallback: show warning text in dialog
  - `BlockUI.show('Removing...')` → fetch `AxPostDeleteStripeKeys` → `BlockUI.hide()` → SweetAlert2 success → reload panel
- [x] 7.5 Add Connect button enable/disable logic:
  - If keys are configured (per-business or platform): button active
  - If no keys available: button disabled with tooltip "Configure API keys first"

## Task 8: Rate Limiting

- [x] 8.1 Implement rate limiting for the reveal endpoint:
  - Use `IMemoryCache` with sliding window (10 requests per minute per user)
  - Key format: `stripe_reveal_{userId}_{minute}`
  - Return HTTP 429 with message when limit exceeded

## Task 9: Integration and Wiring

- [x] 9.1 Register all new services in DI container (Program.cs or a `StripeServiceExtensions.cs`):
  - `IStripeKeyEncryptionService` → `StripeKeyEncryptionService` (Scoped)
  - `IStripeKeyResolutionService` → `StripeKeyResolutionService` (Scoped)
  - `BusinessApiKeysRepository` (Scoped)
- [x] 9.2 Update `StripeConnectService` constructor to accept `IStripeKeyResolutionService` and use resolved keys in `GetOAuthConnectUrlAsync` and webhook handling
- [x] 9.3 Ensure the `[stripe]` schema migration runs before application startup (add to migration sequence)

## Task 10: Testing

- [x] 10.1 Write unit tests for `StripeKeyEncryptionService`:
  - Encrypt/decrypt round-trip
  - Mask format correctness (prefix + `****...` + last 4)
  - Short key masking (graceful handling)
- [x] 10.2 Write unit tests for `StripeKeyResolutionService`:
  - DB keys present → returns DB keys
  - No DB keys → falls back to User Secrets
  - RedirectUri always generated from domain
- [x] 10.3 Write unit tests for controller endpoints:
  - Owner access → success
  - Non-owner access → 403
  - Rate limiting → 429 after threshold
  - Stripe validation failure → appropriate error message
- [x] 10.4 Write integration test for save → retrieve → reveal → delete flow
