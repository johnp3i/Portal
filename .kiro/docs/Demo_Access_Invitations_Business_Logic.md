# Demo Access Invitations — Business Logic & Implementation Guide

## Purpose

This document explains how the Demo Access Invitations feature works in the Portal platform. It is intended for agents or developers who need to understand the full business logic, data flow, and implementation details.

---

## What It Does

The SuperAdmin can send **magic link invitations** to prospective customers. When a prospect clicks the link, they are automatically authenticated into a designated demo business with pre-configured module permissions. No account creation is required from the prospect's side — the system handles everything.

---

## Key Concepts

| Concept | Description |
|---------|-------------|
| **Demo Business** | A `Business` record with `IsDemoAccount = 1`. Created specifically for demos — separate from real customer data. |
| **Demo Invitation** | A record in `[portal].[DemoInvitation]` with a unique token, recipient info, expiry, and status. |
| **Magic Link** | URL format: `https://{host}/Demo/Enter?token={43-char-token}` — clicking it auto-authenticates the prospect. |
| **Demo User** | An `ApplicationUser` created automatically when a prospect first clicks the link. Has role "DemoUser". |
| **Module Permissions** | Per-invitation configuration: each module can be `full`, `readonly`, or `none`. |

---

## Data Flow

### 1. SuperAdmin Creates an Invitation

```
SuperAdmin → POST /Admin/DemoInvitations/Create (JSON body)
  → DemoInvitationService.CreateAsync()
    → Validates: email format, business is demo, expiry is future, at least one module granted
    → Rejects if email belongs to an existing non-demo user
    → Generates 32-byte crypto random token (Base64URL, 43 chars)
    → Persists DemoInvitation (status: 'sent') + DemoInvitationPermission records
    → Sends branded email via PortalEmailService (EmailDepartmentEnum.Sales)
  → Returns { success: true, message: "Invitation sent to ..." }
```

### 2. Prospect Clicks the Magic Link

```
Prospect → GET /Demo/Enter?token=XXXXX
  → DemoController.Enter()
    → Calls DemoInvitationService.ValidateAndTrackAccessAsync(token)
      → Looks up token in DB
      → If not found → "invalid"
      → If status is 'revoked' → "revoked"
      → If ExpiresAtUtc ≤ now → updates status to 'expired', returns "expired"
      → If valid → increments AccessCount, sets Last/FirstAccessedAtUtc, status → 'accessed'
    → Signs out any existing session
    → Creates or retrieves demo ApplicationUser (by email)
    → Assigns "DemoUser" role
    → Creates UserBusiness mapping for demo business
    → Signs in with claims: DemoInvitationId, BusinessId, IsDemoSession
    → Cookie: 2-hour sliding expiry
    → Redirects to Dashboard
```

### 3. Permission Enforcement (Every Request)

```
Request → DemoPermissionFilter (global IAsyncAuthorizationFilter)
  → Checks for DemoInvitationId claim (if absent → skip, not a demo user)
  → Blocks all email-sending actions (Share, EmailStatement, etc.) → "Email sending is disabled in demo mode"
  → Resolves module from controller name via mapping table
  → If not a module controller (Home, Account, Demo) → allow through
  → Loads permissions from DB for this invitation
  → AccessLevel 'none' or missing → DemoAccessRestricted view (403)
  → AccessLevel 'readonly' + non-GET request → blocks (except data-fetching POSTs like "GetXxx")
  → AccessLevel 'full' → allows all operations
  → Sets HttpContext.Items["DemoReadOnly"] = true for readonly modules (views can show banner)
```

---

## Invitation Statuses

| Status | Meaning | Transitions To |
|--------|---------|----------------|
| `sent` | Created and email sent, prospect hasn't clicked yet | → `accessed`, `expired`, `revoked` |
| `accessed` | Prospect has clicked the link at least once | → `expired`, `revoked` |
| `expired` | ExpiresAtUtc has passed (auto-detected on next access attempt) | Terminal |
| `revoked` | SuperAdmin manually revoked the invitation | Terminal |

---

## Module-to-Controller Mapping

The `DemoPermissionFilter` uses this mapping to determine which module a controller belongs to:

| Module | Controllers |
|--------|------------|
| `customer` | Customer, Customers |
| `quotation` | Quotation, Quotations, Proposal |
| `invoice` | Invoice, Invoices |
| `revenue` | Payment, Payments, Revenue |
| `purchase` | Purchase, Purchases, Supplier, Expense |
| `vat` | Vat, VatSubmission |
| `credit` | CreditNote, CreditNotes |
| `audit` | AuditLog, Audit |
| `products` | Product, Products |

Controllers not in this map (Home, Account, Demo, MyBusiness, Admin, Statement) are not subject to demo permission checks.

---

## Access Levels

| Level | GET Requests | POST/PUT/DELETE | Use Case |
|-------|-------------|-----------------|----------|
| `full` | ✅ Allowed | ✅ Allowed | Prospect can fully interact with module |
| `readonly` | ✅ Allowed | ❌ Blocked (403) | Prospect can browse but not modify data |
| `none` | ❌ Blocked (403 page) | ❌ Blocked | Module is hidden from the demo |

**Exception:** POST actions starting with "Get" (e.g., `GetInvoiceBreakdown`) are allowed even in `readonly` mode because they are data-fetching operations, not write operations.

---

## Email Blocking

All demo users are **blocked from sending any emails** regardless of module access level. This prevents demo users from sending real emails to customers via Share, Email Statement, or similar actions. Blocked actions:
- `Share` (any controller)
- `EmailStatement`
- Any action containing `SendEmail` or `ResendEmail`

---

## Demo User Account Management

| Behaviour | Detail |
|-----------|--------|
| **One user per email** | If the same email gets multiple invitations, the system reuses the same Identity user |
| **UserBusiness mapping** | Created per (userId, businessId) pair so the user can access the demo business |
| **Password** | Randomly generated 32-char password (never used — demo users auth via token only) |
| **Role** | "DemoUser" — used to distinguish demo users from real users |
| **Existing user check** | If the email belongs to a real registered user (non-DemoUser role), the invitation is rejected |

---

## Token Security

| Property | Value |
|----------|-------|
| Length | 43 characters (Base64URL encoding of 32 bytes) |
| Entropy | 256 bits — computationally infeasible to guess |
| Character set | `[A-Za-z0-9_-]` (URL-safe, no padding) |
| Uniqueness | Enforced by unique DB constraint + collision retry (up to 3 attempts) |
| Storage | Plain text in DB (not hashed — tokens are single-use-ish and not credentials) |

---

## Session Management

| Property | Value |
|----------|-------|
| Cookie expiry | 2-hour sliding (refreshes on activity) |
| Persistent | No (session cookie, cleared on browser close) |
| Claims injected | `DemoInvitationId`, `BusinessId`, `IsDemoSession` |
| Expired session redirect | `/Demo/SessionExpired` (not the regular login page) |
| Effect on regular users | None — demo claims only present for demo sessions |

---

## Admin Panel (SuperAdmin Only)

### Available Actions

| Action | Endpoint | When Available |
|--------|----------|----------------|
| Create invitation | `GET/POST /Admin/DemoInvitations/Create` | Always |
| View all invitations | `GET /Admin/DemoInvitations` | Always |
| Revoke | `POST /Admin/DemoInvitations/Revoke` | Status is 'sent' or 'accessed' |
| Resend email | `POST /Admin/DemoInvitations/Resend` | Status is 'sent' or 'accessed' AND not expired |
| View permissions | `GET /Admin/DemoInvitations/Permissions/{id}` | Always |
| Update permissions | `POST /Admin/DemoInvitations/Permissions` | Always (live update) |

### List View Columns

Recipient email, recipient name, demo business name, status (colour-coded badge), expiry date, access count, first accessed date, creation date.

---

## Database Tables

### `[portal].[DemoInvitation]`

| Column | Type | Notes |
|--------|------|-------|
| Id | INT IDENTITY PK | |
| BusinessId | INT FK → Business | Demo business this invitation grants access to |
| Token | NVARCHAR(100) UNIQUE | The magic link token |
| RecipientEmail | NVARCHAR(256) | Who receives the invitation |
| RecipientName | NVARCHAR(200) NULL | Optional display name |
| ExpiresAtUtc | DATETIME2 | When the link stops working |
| Status | NVARCHAR(20) CHECK | 'sent', 'accessed', 'expired', 'revoked' |
| CreatedByUserId | NVARCHAR(450) FK → AspNetUsers | SuperAdmin who created it |
| FirstAccessedAtUtc | DATETIME2 NULL | First time prospect clicked the link |
| LastAccessedAtUtc | DATETIME2 NULL | Most recent click |
| AccessCount | INT DEFAULT 0 | Total number of times the link was used |
| RevokedAtUtc | DATETIME2 NULL | When it was revoked (if applicable) |
| CreatedAtUtc | DATETIME2 DEFAULT GETUTCDATE() | |

### `[portal].[DemoInvitationPermission]`

| Column | Type | Notes |
|--------|------|-------|
| Id | INT IDENTITY PK | |
| DemoInvitationId | INT FK → DemoInvitation | |
| Module | NVARCHAR(50) CHECK | 'customer', 'quotation', 'invoice', 'revenue', 'purchase', 'vat', 'credit', 'audit', 'products' |
| AccessLevel | NVARCHAR(20) CHECK | 'full', 'readonly', 'none' |
| CreatedAtUtc | DATETIME2 DEFAULT GETUTCDATE() | |

Unique constraint on (DemoInvitationId, Module).

---

## Key Files

| File | Purpose |
|------|---------|
| `Portal.Web/Controllers/DemoController.cs` | Public entry endpoint (magic link handler) |
| `Portal.Web/Controllers/DemoInvitationController.cs` | Admin CRUD (SuperAdmin only) |
| `Portal.Web/Services/DemoInvitationService.cs` | Core business logic |
| `Portal.Infrastructure/Services/IDemoInvitationService.cs` | Service interface |
| `Portal.Infrastructure/Repositories/DemoInvitationRepository.cs` | Data access |
| `Portal.Web/Filters/DemoPermissionFilter.cs` | Global authorization filter |
| `Portal.Infrastructure/Entities/DemoInvitation.cs` | Entity model |
| `Portal.Infrastructure/Entities/DemoInvitationPermission.cs` | Permission entity |
| `Portal.Infrastructure/Constants/PortalModules.cs` | Module name constants |
| `Portal.Infrastructure/Constants/AccessLevels.cs` | Access level constants |
| `Views/Demo/DemoInvalid.cshtml` | Error: invalid token |
| `Views/Demo/DemoExpired.cshtml` | Error: expired token |
| `Views/Demo/DemoRevoked.cshtml` | Error: revoked token |
| `Views/Shared/DemoAccessRestricted.cshtml` | 403: module not accessible |
| `Views/DemoInvitation/Index.cshtml` | Admin list view |
| `Views/DemoInvitation/Create.cshtml` | Admin create form |

---

## Deviations from Original Spec

The implementation includes features not in the original spec:

1. **Email blocking for demo users** — All email-sending actions are explicitly blocked in the permission filter (Share, EmailStatement, etc.). This prevents demo users from sending real emails to third parties.

2. **Existing user rejection** — The CreateAsync method checks if the recipient email belongs to an existing registered user (non-DemoUser role) and rejects the invitation. This prevents accidentally creating demo sessions for real platform users.

3. **Permission update endpoint** — `POST /Admin/DemoInvitations/Permissions` allows the SuperAdmin to update module permissions on an existing invitation without revoking and recreating it.

4. **Readonly POST exception** — POST actions starting with "Get" (data-fetching operations) are allowed even in readonly mode. This is necessary because some views use POST for AJAX data retrieval (e.g., `GetInvoiceBreakdown`).

5. **DemoReadOnly context flag** — `HttpContext.Items["DemoReadOnly"]` is set for readonly modules, allowing views to conditionally show a "read-only" banner or hide action buttons.

6. **Sign-out before demo sign-in** — The entry endpoint signs out any existing session before creating the demo session, preventing session confusion.
