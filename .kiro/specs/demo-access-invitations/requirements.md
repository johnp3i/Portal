# Requirements Document

## Introduction

This feature enables the SuperAdmin to send demo access invitations to prospective customers via email. Each invitation contains a magic link that auto-authenticates the recipient into a designated demo business account with configurable module permissions and a configurable expiry date. The system supports multiple demo businesses across different industries, tracks access metrics per invitation, and allows the SuperAdmin to manage (revoke, resend) active invitations from the Admin panel.

## Glossary

- **SuperAdmin**: The platform administrator user with the "SuperAdmin" role who manages demo invitations via the Admin panel.
- **Demo_Business**: A business record in `[portal].[Business]` with `IsDemoAccount = 1`, created specifically for platform demonstration purposes.
- **Demo_Invitation**: The database entity stored in `[portal].[DemoInvitation]` representing a single demo access invitation with token, recipient, permissions, expiry, and tracking data.
- **Invitation_Token**: A cryptographically random, URL-safe, Base64URL-encoded string (32 bytes) that uniquely identifies a Demo_Invitation and is embedded in the magic link.
- **Demo_Entry_Endpoint**: The public (unauthenticated) controller action at `GET /Demo/Enter?token=XXXXX` that validates the token and auto-signs the recipient into the demo session.
- **Demo_Session**: The authenticated session created when a prospect accesses the Demo_Entry_Endpoint, scoped to the associated Demo_Business with configured module permissions.
- **Invitation_Management_UI**: The section within the Admin panel where the SuperAdmin creates, views, revokes, and resends demo invitations.
- **Portal_Email_Service**: The existing `PortalEmailService` that sends branded emails via SMTP using the `IEmailSender` infrastructure.
- **Module_Permission**: A combination of a module name (from `PortalModules`) and an access level (from `AccessLevels`) that defines what the demo session user can access.

## Requirements

### Requirement 1: IsDemoAccount Column on Business Table

**User Story:** As a platform developer, I want to flag specific businesses as demo accounts, so that the system can distinguish demo businesses from real customer businesses.

#### Acceptance Criteria

1. THE Portal_Database SHALL contain a column `IsDemoAccount` of type BIT, NOT NULL, with a default value of 0 on the `[portal].[Business]` table.
2. WHEN a business has `IsDemoAccount = 1`, THE system SHALL treat that business as a Demo_Business available for selection during invitation creation.
3. THE Portal_Database SHALL include a non-clustered index on `[portal].[Business].[IsDemoAccount]` filtered to `IsDemoAccount = 1` for efficient querying of demo businesses.

### Requirement 2: DemoInvitation Table Schema

**User Story:** As a platform developer, I want a dedicated table to store demo invitation data, so that invitations are persisted with all metadata needed for token validation, access tracking, and administration.

#### Acceptance Criteria

1. THE Portal_Database SHALL contain a `[portal].[DemoInvitation]` table with columns: Id (INT, PK, identity), BusinessId (INT, NOT NULL, FK to `[portal].[Business]`), Token (NVARCHAR(100), NOT NULL, unique), RecipientEmail (NVARCHAR(256), NOT NULL), RecipientName (NVARCHAR(200), nullable), ExpiresAtUtc (DATETIME2, NOT NULL), Status (NVARCHAR(20), NOT NULL), CreatedByUserId (NVARCHAR(450), NOT NULL, FK to `[dbo].[AspNetUsers]`), FirstAccessedAtUtc (DATETIME2, nullable), LastAccessedAtUtc (DATETIME2, nullable), AccessCount (INT, NOT NULL, default 0), RevokedAtUtc (DATETIME2, nullable), and CreatedAtUtc (DATETIME2, NOT NULL, default GETUTCDATE()).
2. THE Portal_Database SHALL enforce a unique constraint on `[portal].[DemoInvitation].[Token]` for token lookup integrity.
3. THE Portal_Database SHALL enforce a check constraint on `[portal].[DemoInvitation].[Status]` allowing values: 'sent', 'accessed', 'expired', 'revoked'.
4. THE Portal_Database SHALL enforce foreign key constraints from DemoInvitation.BusinessId to `[portal].[Business].[Id]` and from DemoInvitation.CreatedByUserId to `[dbo].[AspNetUsers].[Id]`.
5. THE Portal_Database SHALL include a non-clustered index on `[portal].[DemoInvitation].[Token]` for efficient token lookup during entry validation.

### Requirement 3: DemoInvitationPermission Table Schema

**User Story:** As a platform developer, I want to store the configured module permissions per invitation, so that each demo session enforces the access levels chosen by the SuperAdmin at invitation time.

#### Acceptance Criteria

1. THE Portal_Database SHALL contain a `[portal].[DemoInvitationPermission]` table with columns: Id (INT, PK, identity), DemoInvitationId (INT, NOT NULL, FK to `[portal].[DemoInvitation]`), Module (NVARCHAR(50), NOT NULL), AccessLevel (NVARCHAR(20), NOT NULL), and CreatedAtUtc (DATETIME2, NOT NULL, default GETUTCDATE()).
2. THE Portal_Database SHALL enforce a unique constraint on (DemoInvitationId, Module) to allow at most one permission entry per module per invitation.
3. THE Portal_Database SHALL enforce a check constraint on `Module` allowing values: 'customer', 'quotation', 'invoice', 'revenue', 'purchase', 'vat', 'credit', 'audit', 'products'.
4. THE Portal_Database SHALL enforce a check constraint on `AccessLevel` allowing values: 'full', 'readonly', 'none'.

### Requirement 4: Token Generation

**User Story:** As a platform developer, I want invitation tokens to be cryptographically secure and URL-safe, so that tokens cannot be guessed or forged.

#### Acceptance Criteria

1. WHEN a new Demo_Invitation is created, THE system SHALL generate a 32-byte cryptographically random value using a secure random number generator.
2. THE system SHALL encode the random value as a Base64URL string (no padding) to produce the Invitation_Token.
3. THE system SHALL verify uniqueness of the generated token against existing records before persisting the Demo_Invitation.
4. IF a generated token collides with an existing token, THEN THE system SHALL regenerate a new token and retry (up to 3 attempts) before returning an error.

### Requirement 5: Create Demo Invitation

**User Story:** As a SuperAdmin, I want to create a new demo invitation by selecting a demo business, configuring permissions, setting an expiry, and entering a recipient email, so that I can share platform demos with prospects.

#### Acceptance Criteria

1. THE Invitation_Management_UI SHALL provide a form allowing the SuperAdmin to: select a Demo_Business from a dropdown (populated with businesses where `IsDemoAccount = 1`), enter a recipient email address, enter an optional recipient name, set an expiry date, and select module permissions with access levels.
2. WHEN the SuperAdmin submits the invitation form, THE system SHALL validate that the recipient email is a valid email format, the selected business has `IsDemoAccount = 1`, the expiry date is in the future, and at least one module permission is granted with access level 'full' or 'readonly'.
3. WHEN validation passes, THE system SHALL generate an Invitation_Token, persist the Demo_Invitation with status 'sent', persist the configured DemoInvitationPermission records, and trigger the invitation email.
4. IF validation fails, THEN THE system SHALL display specific validation error messages using SweetAlert2 without clearing the form.
5. THE Invitation_Management_UI SHALL display a success confirmation via SweetAlert2 after the invitation is created and email is sent.

### Requirement 6: Invitation Email Delivery

**User Story:** As a SuperAdmin, I want the system to send a branded email with the magic link to the recipient, so that prospects receive a professional invitation to access the demo.

#### Acceptance Criteria

1. WHEN a Demo_Invitation is created, THE Portal_Email_Service SHALL send an email to the RecipientEmail containing a magic link in the format: `https://{host}/Demo/Enter?token={Invitation_Token}`.
2. THE invitation email SHALL include the Demo_Business name, the expiry date in a human-readable format, and a clear call-to-action button linking to the magic link.
3. THE invitation email SHALL use the existing email template format and SMTP infrastructure (EmailDepartmentEnum).
4. IF the email delivery fails, THEN THE system SHALL log the error and display an error message to the SuperAdmin via SweetAlert2, while still persisting the Demo_Invitation record with status 'sent'.

### Requirement 7: Demo Entry Endpoint — Token Validation

**User Story:** As a prospect clicking the magic link, I want the system to validate my token and provide clear feedback, so that I can access the demo or understand why access is denied.

#### Acceptance Criteria

1. THE Demo_Entry_Endpoint SHALL accept a GET request at `/Demo/Enter` with a query parameter `token`.
2. WHEN the token matches an existing Demo_Invitation with status 'sent' or 'accessed' and ExpiresAtUtc is in the future, THE Demo_Entry_Endpoint SHALL consider the token valid.
3. IF the token does not exist in the database, THEN THE Demo_Entry_Endpoint SHALL display an error page with the message "This demo link is not valid."
4. IF the token exists but ExpiresAtUtc is in the past, THEN THE Demo_Entry_Endpoint SHALL update the status to 'expired' and display a friendly page with the message "This demo link has expired."
5. IF the token exists but status is 'revoked', THEN THE Demo_Entry_Endpoint SHALL display a friendly page with the message "This demo link has been revoked."
6. IF the token parameter is missing or empty, THEN THE Demo_Entry_Endpoint SHALL display the invalid link error page.

### Requirement 8: Demo Entry Endpoint — Session Creation

**User Story:** As a prospect with a valid token, I want to be automatically signed into the demo business, so that I can explore the platform without creating credentials.

#### Acceptance Criteria

1. WHEN the token is validated successfully, THE Demo_Entry_Endpoint SHALL create or retrieve a demo user account associated with the Demo_Business for the given RecipientEmail.
2. THE Demo_Entry_Endpoint SHALL sign the demo user into the application using ASP.NET Core Identity sign-in (cookie authentication).
3. THE Demo_Entry_Endpoint SHALL set the demo user's session business context to the Demo_Business associated with the invitation.
4. WHEN the demo session is created, THE Demo_Entry_Endpoint SHALL redirect the user to the platform dashboard.
5. THE demo session SHALL respect the Module_Permission entries configured on the Demo_Invitation, granting only the specified access levels to the specified modules.

### Requirement 9: Access Tracking

**User Story:** As a SuperAdmin, I want to track when and how often prospects access the demo, so that I can gauge interest and follow up with engaged prospects.

#### Acceptance Criteria

1. WHEN a prospect accesses the Demo_Entry_Endpoint with a valid token for the first time, THE system SHALL set FirstAccessedAtUtc to the current UTC timestamp and update the status to 'accessed'.
2. WHEN a prospect accesses the Demo_Entry_Endpoint with a valid token, THE system SHALL increment the AccessCount by 1 and update LastAccessedAtUtc to the current UTC timestamp.
3. THE system SHALL perform access tracking updates within the same operation as token validation to ensure consistency.

### Requirement 10: Invitation List View

**User Story:** As a SuperAdmin, I want to view all demo invitations in a table, so that I can monitor invitation status, access metrics, and manage active invitations.

#### Acceptance Criteria

1. THE Invitation_Management_UI SHALL display a table listing all Demo_Invitations with columns: recipient email, recipient name, demo business name, status, expiry date, access count, first accessed date, and creation date.
2. THE Invitation_Management_UI SHALL sort invitations by creation date in descending order (newest first) by default.
3. THE Invitation_Management_UI SHALL visually differentiate invitation statuses using colour-coded badges (sent = blue, accessed = green, expired = amber, revoked = red).
4. THE Invitation_Management_UI SHALL provide pagination when the invitation count exceeds 10 records per page.

### Requirement 11: Revoke Invitation

**User Story:** As a SuperAdmin, I want to revoke an active invitation, so that I can immediately prevent further access when needed.

#### Acceptance Criteria

1. THE Invitation_Management_UI SHALL display a "Revoke" action button for invitations with status 'sent' or 'accessed'.
2. WHEN the SuperAdmin clicks the Revoke button, THE system SHALL display a SweetAlert2 confirmation dialog asking "Are you sure you want to revoke this invitation?"
3. WHEN the SuperAdmin confirms revocation, THE system SHALL update the Demo_Invitation status to 'revoked' and set RevokedAtUtc to the current UTC timestamp.
4. WHEN a revoked token is used at the Demo_Entry_Endpoint, THE system SHALL deny access immediately and display the revoked message page.
5. THE Invitation_Management_UI SHALL hide the Revoke button for invitations with status 'expired' or 'revoked'.

### Requirement 12: Resend Invitation Email

**User Story:** As a SuperAdmin, I want to resend the invitation email, so that I can remind prospects who may have missed the original email.

#### Acceptance Criteria

1. THE Invitation_Management_UI SHALL display a "Resend" action button for invitations with status 'sent' or 'accessed' and where ExpiresAtUtc is in the future.
2. WHEN the SuperAdmin clicks the Resend button, THE Portal_Email_Service SHALL send the invitation email again to the same RecipientEmail with the same magic link.
3. THE Invitation_Management_UI SHALL display a success confirmation via SweetAlert2 after the email is resent.
4. IF the resend fails, THEN THE system SHALL display an error message via SweetAlert2 with the failure reason.
5. THE Invitation_Management_UI SHALL hide the Resend button for invitations that are expired or revoked.

### Requirement 13: Demo Session Timeout

**User Story:** As a platform developer, I want demo sessions to expire after a period of inactivity, so that demo sessions do not remain active indefinitely.

#### Acceptance Criteria

1. THE Demo_Session SHALL enforce a sliding expiration timeout of 2 hours of inactivity.
2. WHEN the demo session timeout is reached, THE system SHALL sign the user out and redirect to a "session expired" page.
3. THE session timeout SHALL apply only to users authenticated via a Demo_Invitation token, not to regular platform users.

### Requirement 14: Demo User Permissions Enforcement

**User Story:** As a platform developer, I want the demo session to enforce module-level permissions based on the invitation configuration, so that prospects only see modules the SuperAdmin intended to showcase.

#### Acceptance Criteria

1. WHEN a demo user navigates to a module, THE system SHALL check the Module_Permission records associated with the active Demo_Invitation.
2. IF the demo user attempts to access a module with AccessLevel 'none' or no permission entry, THEN THE system SHALL deny access and display an "Access restricted" page.
3. WHEN a demo user has AccessLevel 'readonly' for a module, THE system SHALL allow viewing data but prevent any create, update, or delete operations within that module.
4. WHEN a demo user has AccessLevel 'full' for a module, THE system SHALL allow all standard operations within that module.
5. THE permission enforcement SHALL use the same module names defined in `PortalModules`: customer, quotation, invoice, revenue, purchase, vat, credit, audit, products.

### Requirement 15: SuperAdmin Authorization

**User Story:** As a platform developer, I want the invitation management functionality to be restricted to SuperAdmin users, so that only authorized administrators can create and manage demo invitations.

#### Acceptance Criteria

1. THE Invitation_Management_UI SHALL be accessible only to users with the "SuperAdmin" role.
2. THE invitation creation, revocation, and resend API endpoints SHALL reject requests from users without the "SuperAdmin" role and return HTTP 403 Forbidden.
3. THE Admin panel navigation SHALL display the "Demo Invitations" menu item only for users with the "SuperAdmin" role.
