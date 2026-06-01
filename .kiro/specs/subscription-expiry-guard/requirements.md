# Requirements Document

## Introduction

The Subscription Expiry Guard is a server-side safety net that enforces subscription expiration independently of Stripe webhook delivery. On every module-gated page access, the system checks whether the subscription's CurrentPeriodEnd has passed while the Status remains "active". If expired, the system grants one final grace access with a warning modal, updates the status to "cancelled", and fully locks out the user on subsequent attempts. This ensures the platform never relies solely on Stripe webhooks for expiration enforcement.

## Glossary

- **Expiry_Guard**: The server-side logic within SubscriptionPlanService that detects expired subscriptions by comparing CurrentPeriodEnd against the current UTC time
- **Grace_Access**: A single module access granted to a user after their subscription has been detected as expired, accompanied by a warning modal
- **Module_Access_Attribute**: The ASP.NET Core authorization filter that gates controller actions behind subscription and permission checks
- **Subscription_Plan_Service**: The service that queries [billing].[Subscription] and returns subscription access status for a given business
- **Subscription_Table**: The [billing].[Subscription] table containing Status, CurrentPeriodEnd, and other subscription lifecycle columns
- **Grace_Login_Flag**: A per-business flag stored in [billing].[Subscription] indicating whether the grace access has already been consumed
- **SweetAlert2_Modal**: The project-standard confirmation/alert dialog library (Swal.fire) used for user-facing notifications
- **SuperAdmin**: A platform role that bypasses all subscription and module access checks
- **Three_Inventors_Account**: The internal business account (Business ID = 1) with a long-term subscription that is exempt from expiry guard enforcement

## Requirements

### Requirement 1: Detect Expired Subscription on Module Access

**User Story:** As a platform operator, I want the system to detect expired subscriptions at the point of module access, so that expiration is enforced even when Stripe webhooks are delayed or missed.

#### Acceptance Criteria

1. WHEN a user accesses a module-gated page AND the subscription Status is "active" AND CurrentPeriodEnd is earlier than the current UTC time, THE Expiry_Guard SHALL report the subscription as expired to the Module_Access_Attribute by returning HasActiveSubscription as false in the SubscriptionAccessResult
2. WHEN a user accesses a module-gated page AND the subscription Status is "active" AND CurrentPeriodEnd is equal to or later than the current UTC time, THE Expiry_Guard SHALL treat the subscription as valid and allow the request to proceed to module permission checks
3. WHILE the user holds the "SuperAdmin" role, THE Module_Access_Attribute SHALL bypass the Expiry_Guard check entirely
4. WHILE the business is the Three_Inventors_Account (Business ID = 1), THE Expiry_Guard SHALL bypass expiration detection and allow the request to proceed to module permission checks
5. IF the subscription Status is not "active" (e.g., "trialing", "past_due", "cancelled"), THEN THE Expiry_Guard SHALL skip expiration detection and defer to the existing subscription status handling in Module_Access_Attribute
6. THE Expiry_Guard SHALL compare CurrentPeriodEnd against DateTime.UtcNow with no grace period or tolerance buffer (strict less-than comparison)

### Requirement 2: Grant One Grace Access with Warning

**User Story:** As a user whose subscription has just expired, I want to receive one final access with a clear warning, so that I am not abruptly locked out and have a chance to renew.

#### Acceptance Criteria

1. WHEN the Expiry_Guard detects an expired subscription AND the Grace_Login_Flag is false, THE Subscription_Plan_Service SHALL allow access for that single request and return a result indicating grace access was granted to the Module_Access_Attribute
2. WHEN the Expiry_Guard grants a grace access, THE Subscription_Plan_Service SHALL set a boolean flag in HttpContext.Items to trigger the expiry warning modal on the response
3. WHEN the Expiry_Guard grants a grace access, THE SweetAlert2_Modal SHALL display the message: "Your subscription has expired. This is your last access. Please renew to continue using the platform."
4. WHEN the grace access modal is displayed, THE SweetAlert2_Modal SHALL use icon type "warning" and the primary confirmation button colour (#0D5EA6)
5. IF two or more concurrent requests arrive for the same business while the Grace_Login_Flag is false, THEN THE Subscription_Plan_Service SHALL ensure that at most one request receives grace access (subsequent concurrent requests SHALL be denied access)

### Requirement 3: Update Subscription Status After Grace Access

**User Story:** As a platform operator, I want the subscription status to be updated to "cancelled" after the grace access is consumed, so that subsequent access attempts are denied.

#### Acceptance Criteria

1. WHEN the Expiry_Guard grants a grace access, THE Subscription_Plan_Service SHALL update the subscription Status from "active" to "cancelled", set the CancelledAtUtc column to the current UTC time, and set IsGraceAccessUsed to true in the Subscription_Table within a single atomic database operation (all three columns succeed or none are written)
2. WHEN the Expiry_Guard grants a grace access, THE Subscription_Plan_Service SHALL complete the status update before the HTTP response is returned to the client
3. IF two concurrent requests from the same business both detect an expired subscription with IsGraceAccessUsed equal to false, THEN THE Subscription_Plan_Service SHALL ensure only one request performs the status update and is treated as the grace access
4. IF the database update fails during grace access processing, THEN THE Subscription_Plan_Service SHALL log the error at Warning severity via the configured logger and allow the current request to proceed without retrying the update (fail-open for the grace access only)

### Requirement 4: Lock Out User After Grace Access Is Consumed

**User Story:** As a platform operator, I want users to be fully locked out after their grace access has been consumed, so that expired subscriptions cannot continue accessing gated content.

#### Acceptance Criteria

1. WHEN a user accesses a module-gated page AND the subscription Status is "cancelled", THE Module_Access_Attribute SHALL deny access and redirect the user to the existing "Subscription Required" page without rendering any module content
2. WHEN the Expiry_Guard detects an expired subscription AND the Grace_Login_Flag is true, THE Subscription_Plan_Service SHALL report the subscription as inactive AND THE Module_Access_Attribute SHALL deny access and redirect to the "Subscription Required" page, regardless of the current Status column value
3. THE Expiry_Guard SHALL NOT grant more than one grace access per subscription period, enforced by checking the Grace_Login_Flag before granting access
4. IF multiple concurrent requests arrive while the Grace_Login_Flag is false, THEN THE Expiry_Guard SHALL ensure that at most one request receives grace access by reading the Grace_Login_Flag within the same database transaction that sets it to true

### Requirement 5: Grace Login Flag Database Column

**User Story:** As a developer, I want a persistent flag tracking whether the grace access has been used, so that the system reliably prevents multiple grace logins across requests and server restarts.

#### Acceptance Criteria

1. THE [billing].[Subscription] table SHALL contain a column named IsGraceAccessUsed of type BIT, NOT NULL, with a default constraint of 0
2. WHEN a grace login is successfully granted to a user with an expired subscription, THE system SHALL set IsGraceAccessUsed to 1 (true) for that subscription record
3. WHEN a Stripe webhook updates a subscription status to "active" with a CurrentPeriodEnd later than the previously stored value, THE system SHALL reset IsGraceAccessUsed to 0 (false)
4. IF IsGraceAccessUsed is 1 (true) for a subscription, THEN THE system SHALL deny any subsequent grace login attempt for that subscription

### Requirement 6: Coexistence with Stripe Webhooks

**User Story:** As a platform operator, I want the expiry guard to work alongside Stripe webhooks without conflict, so that whichever mechanism fires first correctly enforces expiration.

#### Acceptance Criteria

1. WHEN a Stripe webhook sets the subscription Status to "cancelled" before the Expiry_Guard detects expiry, THE Module_Access_Attribute SHALL deny access using the existing cancellation flow (no grace access is granted regardless of the IsGraceAccessUsed flag value)
2. WHEN a Stripe webhook receives a `customer.subscription.deleted` or `customer.subscription.updated` event AND the subscription Status is already "cancelled", THE webhook handler SHALL skip the status update, return HTTP 200, and record the webhook event in the WebhookEvent table without raising an error or modifying the subscription record
3. THE Expiry_Guard SHALL NOT modify subscriptions that have a Status other than "active" (only "active" subscriptions with a passed CurrentPeriodEnd are eligible for grace processing)
4. IF the Expiry_Guard reads the subscription Status as "active" AND a concurrent Stripe webhook sets the Status to "cancelled" before the Expiry_Guard completes its update, THEN THE Subscription_Plan_Service SHALL treat the resulting "cancelled" state as valid and SHALL NOT revert the status back to "active"

### Requirement 7: Expiry Warning Modal Presentation

**User Story:** As a user, I want the expiry warning to be clearly visible and non-dismissible without acknowledgement, so that I understand this is my last access.

#### Acceptance Criteria

1. WHEN the grace access flag is set in HttpContext.Items, THE view layer SHALL render a SweetAlert2 modal on page load using Swal.fire with the icon set to "warning"
2. THE SweetAlert2_Modal SHALL use the title "Subscription Expired" and display a body message indicating that the user's subscription has expired and that this is their final grace access session
3. THE SweetAlert2_Modal SHALL use allowOutsideClick set to false and allowEscapeKey set to false, preventing dismissal by any method other than clicking the confirmation button
4. THE SweetAlert2_Modal SHALL display a single confirmation button with the text "I Understand" and confirmButtonColor set to "#0D5EA6"
5. WHEN the user clicks the confirmation button, THE SweetAlert2_Modal SHALL close and allow normal interaction with the page content (the grace access page remains usable)
