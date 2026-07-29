# Requirements Document

## Introduction

This feature enables business owners to configure their Stripe Connect API keys directly through the Portal UI (Business Settings → Automation tab), eliminating the need for developer-level User Secrets setup. Keys are stored encrypted per-business in the database and override platform-level defaults from User Secrets. Only the business owner can manage keys; team members see a simple configuration status indicator.

## Glossary

- **Business_Owner**: A user with the `IsOwner` claim set to true for a given business
- **Team_Member**: Any authenticated user associated with a business who does not have the `IsOwner` claim
- **Key_Configuration_Panel**: The UI section on the Automation tab of Business Settings where the Business Owner enters and manages Stripe Connect API keys
- **Data_Protection_API**: The ASP.NET Core Data Protection system used to encrypt sensitive key values at rest using AES-256
- **Masked_Key**: A partially obscured representation of a stored key showing the prefix and last 4 characters (e.g., `sk_test_****...Hf4K`)
- **Key_Validation_Service**: The component responsible for making lightweight Stripe API calls to verify that entered keys are valid
- **Key_Resolution_Service**: The component responsible for resolving the active Stripe keys for a business by checking per-business DB keys first, then falling back to platform User Secrets

## Requirements

### Requirement 1: Key Configuration Panel Visibility

**User Story:** As a business owner, I want to see a key configuration section on the Automation tab, so that I can manage my Stripe Connect API keys without developer assistance.

#### Acceptance Criteria

1. WHILE the authenticated user has the IsOwner claim for the current business, THE Key_Configuration_Panel SHALL display on the Automation tab above the existing "Card Payments (Stripe Connect)" section
2. WHILE the authenticated user is a Team_Member without the IsOwner claim, THE Automation tab SHALL display a read-only status indicator showing "Stripe: Configured" or "Stripe: Not configured" in place of the Key_Configuration_Panel
3. THE Key_Configuration_Panel SHALL display input fields for three keys: Connect Client ID (ca_...), Secret Key (sk_...), and Connect Webhook Secret (whsec_...)
4. THE Key_Configuration_Panel SHALL NOT display an input field for ConnectOAuthRedirectUri because the Key_Resolution_Service auto-generates the redirect URI from the current domain

### Requirement 2: Key Storage and Encryption

**User Story:** As a business owner, I want my API keys stored securely, so that sensitive credentials are protected at rest.

#### Acceptance Criteria

1. WHEN the Business_Owner saves API keys, THE Key_Configuration_Panel SHALL encrypt each key value using the Data_Protection_API before persisting to the database
2. THE Data_Protection_API SHALL use a purpose string specific to Stripe key encryption to isolate the key material from other protected data in the application
3. WHEN retrieving stored keys for display, THE Key_Configuration_Panel SHALL decrypt the value using the Data_Protection_API and present only the Masked_Key representation
4. THE database record SHALL store the encrypted ciphertext, the BusinessId foreign key, the key type identifier, and the CreatedAtUtc timestamp

### Requirement 3: Key Masking and Display

**User Story:** As a business owner, I want to see masked versions of my saved keys, so that I can confirm which keys are configured without exposing the full value on screen.

#### Acceptance Criteria

1. WHEN keys have been saved, THE Key_Configuration_Panel SHALL display each key as a Masked_Key showing the first prefix characters and last 4 characters separated by asterisks (e.g., `sk_test_****...Hf4K`)
2. WHEN no keys have been saved, THE Key_Configuration_Panel SHALL display empty input fields with placeholder text indicating the expected key format
3. THE Key_Configuration_Panel SHALL NOT send full decrypted key values to the browser in the initial page load response

### Requirement 4: Reveal Full Key Value

**User Story:** As a business owner, I want to reveal the full value of a saved key, so that I can copy it or verify it when needed.

#### Acceptance Criteria

1. WHEN the Business_Owner clicks the "Reveal" button next to a masked key, THE Key_Configuration_Panel SHALL make an authenticated request to a dedicated reveal endpoint
2. THE reveal endpoint SHALL verify that the requesting user has the IsOwner claim before returning the decrypted key value
3. WHEN the reveal request succeeds, THE Key_Configuration_Panel SHALL display the full decrypted key value in the corresponding field for a limited viewing session
4. THE reveal endpoint SHALL log the reveal action in the audit trail including the user identity, key type, business ID, and timestamp
5. IF a non-owner user attempts to access the reveal endpoint, THEN THE reveal endpoint SHALL return an HTTP 403 Forbidden response

### Requirement 5: Key Validation on Save

**User Story:** As a business owner, I want the system to validate my keys when I save them, so that I know immediately if I entered incorrect credentials.

#### Acceptance Criteria

1. WHEN the Business_Owner submits keys for saving, THE Key_Validation_Service SHALL make a lightweight Stripe API call using the provided Secret Key to verify validity
2. WHEN the Secret Key validation succeeds, THE Key_Validation_Service SHALL verify the Connect Client ID by confirming it matches the expected format and is associated with the authenticated Stripe platform
3. IF the Secret Key is invalid or the Stripe API returns an authentication error, THEN THE Key_Configuration_Panel SHALL display a clear validation error message identifying which key failed validation
4. IF the Stripe API is unreachable during validation, THEN THE Key_Configuration_Panel SHALL display an error message indicating that validation could not be completed and the keys were not saved
5. WHEN all validations pass, THE Key_Configuration_Panel SHALL persist the encrypted keys and display a success confirmation

### Requirement 6: Key Resolution Hierarchy

**User Story:** As the system, I want to resolve Stripe keys with per-business overrides taking precedence over platform defaults, so that businesses can use their own keys while the platform maintains fallback configuration.

#### Acceptance Criteria

1. WHEN resolving Stripe Connect keys for a business, THE Key_Resolution_Service SHALL check the per-business database keys first
2. IF per-business database keys exist and are complete for the business, THEN THE Key_Resolution_Service SHALL use the per-business keys
3. IF per-business database keys do not exist, THEN THE Key_Resolution_Service SHALL fall back to the platform-level User Secrets configuration
4. THE Key_Resolution_Service SHALL auto-generate the ConnectOAuthRedirectUri from the current application domain regardless of whether per-business or platform keys are used

### Requirement 7: Connect Button Activation

**User Story:** As a business owner, I want the "Connect with Stripe" OAuth button to activate only after valid keys are configured, so that I cannot accidentally start the OAuth flow without proper credentials.

#### Acceptance Criteria

1. WHILE per-business Stripe keys are not configured and platform-level User Secrets keys are not available, THE "Connect with Stripe" button SHALL be disabled with a tooltip explaining that API keys must be configured first
2. WHEN valid Stripe keys become available (either per-business or platform-level), THE "Connect with Stripe" button SHALL become active and clickable
3. THE Key_Configuration_Panel SHALL display a visual indicator showing whether the current key configuration is sufficient to enable the Connect flow

### Requirement 8: Access Control Enforcement

**User Story:** As a platform administrator, I want strict access control on key management endpoints, so that only authorised business owners can view or modify API keys.

#### Acceptance Criteria

1. THE save endpoint SHALL verify the IsOwner claim before accepting key submission
2. THE reveal endpoint SHALL verify the IsOwner claim before returning decrypted key values
3. IF an unauthenticated request is made to any key management endpoint, THEN THE endpoint SHALL return an HTTP 401 Unauthorized response
4. IF an authenticated user without the IsOwner claim attempts to access any key management endpoint, THEN THE endpoint SHALL return an HTTP 403 Forbidden response
5. THE reveal endpoint SHALL enforce rate limiting of 10 requests per minute per user to prevent automated key extraction

### Requirement 9: Audit Trail

**User Story:** As a business owner, I want key management actions logged, so that I have a record of who accessed or modified Stripe credentials.

#### Acceptance Criteria

1. WHEN keys are saved or updated, THE system SHALL log an audit entry recording the user identity, action type, business ID, and timestamp
2. WHEN a key reveal action is performed, THE system SHALL log an audit entry recording the user identity, key type revealed, business ID, and timestamp
3. WHEN keys are deleted, THE system SHALL log an audit entry recording the user identity, action type, business ID, and timestamp
4. THE audit entries SHALL be stored in a format queryable for security review purposes

### Requirement 10: Key Deletion

**User Story:** As a business owner, I want to remove my configured keys, so that I can revert to platform defaults or clear outdated credentials.

#### Acceptance Criteria

1. WHEN the Business_Owner clicks "Remove Keys" and confirms the action, THE Key_Configuration_Panel SHALL delete all stored per-business Stripe keys from the database
2. WHEN keys are deleted, THE Key_Resolution_Service SHALL fall back to platform-level User Secrets for the affected business
3. IF the business has an active Stripe connection and the owner removes keys that have no platform fallback, THEN THE Key_Configuration_Panel SHALL display a warning that the Stripe Connect integration will stop functioning
4. THE deletion action SHALL require confirmation via a destructive action dialog before proceeding
