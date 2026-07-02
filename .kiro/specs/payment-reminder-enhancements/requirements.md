# Requirements Document

## Introduction

This document specifies enhancements to the existing Payment Reminders module in the Portal platform. Three new capabilities are introduced:

1. **Reminder History with Open Tracking** — Extends the existing `PaymentReminderLog` to record whether each reminder email was opened by the recipient, and surfaces this information in the reminder history UI.
2. **Test Reminder Sending** — Allows the business owner to send a preview reminder to an alternate email address (their own or another) before sending the real reminder to the customer.
3. **Upcoming Reminders Preview** — Provides a view showing which reminders are scheduled to fire next based on the configured schedule and invoice due dates, giving the business owner visibility into what will be sent automatically.

## Glossary

- **Reminder_Service**: The `PaymentReminderService` responsible for evaluating, sending, and querying payment reminders.
- **Reminder_Controller**: The `PaymentReminderController` handling HTTP requests for reminder operations.
- **Reminder_Log**: The `[reminder].[PaymentReminderLog]` table that stores the audit record of every reminder sent or failed.
- **Reminder_Schedule**: The `[reminder].[PaymentReminderSchedule]` table containing per-tier configuration for a business.
- **Tracking_Pixel**: A 1x1 transparent image embedded in the reminder email HTML, whose request to the server signals that the email was opened.
- **Open_Event**: A recorded instance of the Tracking_Pixel being loaded by the recipient's email client.
- **Test_Reminder**: A reminder email sent to an address other than the customer's, marked as a test send and excluded from evaluation logic and reminder counts.
- **Upcoming_Reminder**: A computed projection of which invoices will trigger reminders on future dates based on the current Reminder_Schedule and invoice due dates.
- **Business_Owner**: The authenticated user with access to the Payment Reminders module.
- **Evaluation_Engine**: The logic within Reminder_Service that determines which invoices qualify for reminders on a given date.

## Requirements

### Requirement 1: Open Tracking Pixel Embedding

**User Story:** As a Business_Owner, I want each reminder email to contain an invisible tracking pixel, so that the system can detect when the recipient opens the email.

#### Acceptance Criteria

1. WHEN a payment reminder email is rendered, THE Reminder_Service SHALL embed a Tracking_Pixel image tag referencing a unique URL that includes the corresponding Reminder_Log record identifier.
2. THE Tracking_Pixel URL SHALL follow the pattern `/PaymentReminder/Track/{trackingToken}` where `trackingToken` is a URL-safe Base64-encoded unique identifier stored in the Reminder_Log.
3. THE Tracking_Pixel image tag SHALL use a 1x1 transparent PNG with `width="1" height="1" style="display:block"` attributes to prevent layout disruption in email clients.
4. WHEN a Reminder_Log record is created, THE Reminder_Service SHALL generate a cryptographically random tracking token (minimum 32 bytes) and store it in the `TrackingToken` column.

### Requirement 2: Open Event Recording

**User Story:** As a Business_Owner, I want to know when a customer opens a reminder email, so that I can assess reminder effectiveness.

#### Acceptance Criteria

1. WHEN a GET request is received at the tracking pixel endpoint, THE Reminder_Controller SHALL look up the Reminder_Log by its TrackingToken.
2. WHEN a valid TrackingToken is matched and the Reminder_Log has not yet been marked as opened, THE Reminder_Controller SHALL set `IsOpened` to true and record `OpenedAtUtc` with the current UTC timestamp.
3. WHEN a valid TrackingToken is matched and the Reminder_Log has already been marked as opened, THE Reminder_Controller SHALL update `OpenCount` by incrementing by one and update `LastOpenedAtUtc` with the current UTC timestamp.
4. THE tracking pixel endpoint SHALL return a 1x1 transparent PNG image with `Content-Type: image/png` and appropriate cache-control headers (`no-store, no-cache`) to encourage re-fetches.
5. IF an invalid or missing TrackingToken is received, THEN THE Reminder_Controller SHALL return the same 1x1 transparent PNG (to avoid exposing tracking errors to the recipient).
6. THE tracking pixel endpoint SHALL be accessible without authentication (anonymous access) since email clients load images without portal credentials.

### Requirement 3: Reminder History Enhancement

**User Story:** As a Business_Owner, I want to see open status in the reminder history panel, so that I know which reminders were read by the customer.

#### Acceptance Criteria

1. WHEN the reminder history is retrieved for an invoice, THE Reminder_Service SHALL include `IsOpened`, `OpenedAtUtc`, `OpenCount`, and `LastOpenedAtUtc` fields in each history record.
2. WHEN a reminder log entry has `IsOpened` set to true, THE history panel SHALL display an "Opened" badge with the first-opened timestamp.
3. WHEN a reminder log entry has `IsOpened` set to false, THE history panel SHALL display a "Not opened" indicator.
4. THE history panel SHALL display `OpenCount` when the value exceeds 1, indicating multiple opens.
5. WHEN a reminder log entry is a Test_Reminder (indicated by `IsTestSend` flag), THE history panel SHALL display a "Test" badge and exclude it from effectiveness metrics.

### Requirement 4: Test Reminder Sending

**User Story:** As a Business_Owner, I want to send a test reminder to myself or another email address before sending the real reminder to my customer, so that I can verify the email content and appearance.

#### Acceptance Criteria

1. WHEN the Business_Owner initiates a test reminder send, THE Reminder_Controller SHALL accept an `invoiceId`, an `escalationTier`, and a `testRecipientEmail` parameter.
2. THE Reminder_Service SHALL validate that `testRecipientEmail` is a well-formed email address before sending.
3. WHEN a test reminder is sent successfully, THE Reminder_Service SHALL create a Reminder_Log entry with `IsTestSend` set to true and `RecipientEmail` set to the test address.
4. THE Evaluation_Engine SHALL exclude Reminder_Log entries where `IsTestSend` is true from all reminder cap calculations (`MaxRemindersPerTier`), interval checks (`MinIntervalDays`), and idempotency checks.
5. THE dashboard widget calculations SHALL exclude Reminder_Log entries where `IsTestSend` is true from effectiveness metrics.
6. WHEN a test reminder is sent, THE email content SHALL be identical to a real reminder (same template, same invoice data, same escalation tier styling) with the addition of a "[TEST]" prefix in the subject line.
7. THE test send action SHALL require the same module access as manual reminder sending (`PaymentReminderManual`).
8. IF the invoice referenced in the test send does not belong to the current business, THEN THE Reminder_Controller SHALL return a validation error and refuse to send.

### Requirement 5: Upcoming Reminders Preview

**User Story:** As a Business_Owner, I want to see a preview of which reminders will be sent in the coming days, so that I can anticipate customer communications and adjust the schedule if needed.

#### Acceptance Criteria

1. WHEN the Business_Owner requests the upcoming reminders preview, THE Reminder_Service SHALL compute the projection for a configurable number of days ahead (default: 14 days).
2. THE Reminder_Service SHALL evaluate each eligible invoice against each enabled tier in the Reminder_Schedule, applying the same exclusion rules as the Evaluation_Engine (opt-out, disputed, partial payment suppression, max reminders cap, min interval enforcement).
3. FOR EACH projected reminder, THE Reminder_Service SHALL return: the invoice number, customer name, scheduled send date, escalation tier, and outstanding amount.
4. THE upcoming reminders preview SHALL apply tenant isolation — projections for Business A SHALL never include invoices from Business B.
5. THE upcoming reminders preview SHALL NOT create any Reminder_Log entries or send any emails; it is a read-only projection.
6. WHEN no upcoming reminders are projected within the preview window, THE Reminder_Service SHALL return an empty collection.
7. THE upcoming reminders preview SHALL be accessible via a dedicated page route at `/PaymentReminder/Upcoming` and also as an AJAX endpoint for widget embedding.
8. THE upcoming reminders preview page SHALL require the `PaymentReminderAuto` module permission (since it relates to automated scheduling).
9. WHEN the Business_Owner adjusts the schedule configuration, THE upcoming reminders preview SHALL reflect the updated schedule on the next request (no caching of projections).

### Requirement 6: Database Schema Extension

**User Story:** As a developer, I want the database schema to support open tracking and test send flags, so that the new features have persistent storage.

#### Acceptance Criteria

1. THE database migration SHALL add the following columns to `[reminder].[PaymentReminderLog]`: `TrackingToken` (NVARCHAR(64), nullable, unique filtered index), `IsOpened` (BIT, NOT NULL, default 0), `OpenedAtUtc` (DATETIME, nullable), `OpenCount` (INT, NOT NULL, default 0), `LastOpenedAtUtc` (DATETIME, nullable), `IsTestSend` (BIT, NOT NULL, default 0).
2. THE migration SHALL create a unique filtered index on `TrackingToken` where `TrackingToken IS NOT NULL` to enable fast lookups for the tracking pixel endpoint.
3. THE migration SHALL create a filtered index on `(BusinessId, IsTestSend)` where `IsTestSend = 0` to support efficient queries that exclude test sends.
4. THE `PaymentReminderLog` entity class SHALL include the new properties with appropriate data annotations.
5. THE EF Core DbContext configuration SHALL map the new columns with correct types, defaults, and index definitions.

### Requirement 7: Security and Privacy

**User Story:** As a Business_Owner, I want open tracking to be secure and non-exploitable, so that tracking tokens cannot be guessed or enumerated.

#### Acceptance Criteria

1. THE TrackingToken SHALL be generated using a cryptographically secure random number generator producing at least 32 bytes of entropy, encoded as URL-safe Base64.
2. THE tracking pixel endpoint SHALL implement rate limiting (maximum 100 requests per token per hour) to mitigate denial-of-service via token enumeration.
3. THE tracking pixel endpoint SHALL NOT reveal whether a token exists or is valid — the same 1x1 pixel response is returned regardless.
4. THE test reminder send endpoint SHALL validate that the `testRecipientEmail` domain has valid MX records or matches a known email provider pattern (to reduce misuse).
5. THE Reminder_Controller SHALL enforce antiforgery token validation on the test send POST endpoint.

