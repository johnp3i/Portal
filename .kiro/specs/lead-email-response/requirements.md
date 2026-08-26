# Requirements Document

## Introduction

The Lead Email Response feature extends the existing "Log Response" action on the Lead Detail page to optionally send an actual email to the sales contact/lead. Currently, responses are logged as records for tracking phone, WhatsApp, and in-person responses, but no email is dispatched. This feature adds the ability to compose, preview, and send HTML emails directly from the platform using the platform's SMTP infrastructure with the business user's identity in the From name and Reply-To header.

The architecture uses a dedicated sales sending address (e.g., leads@3inventors.com) with a custom From name format ("John Smith – CompanyName") and Reply-To set to the business owner's email. The platform never collects money or acts on behalf of the business — it facilitates communication only.

Phase 1 scope includes: email composition with rich HTML editor, rendered preview, send-to-self testing, rate limiting, delivery status tracking, legal footer auto-append, and an email send log history page. Excluded from Phase 1: unsubscribe link, reply tracking, file attachments, CC/BCC fields, and per-business SMTP configuration.

This feature is available to Professional and Enterprise tier subscriptions only.

## Glossary

- **Email_Service**: The service responsible for composing, validating, rate-limit checking, and dispatching emails to contacts via the platform SMTP infrastructure
- **Response_Service**: The existing service responsible for preparing lead response content from templates, rendering placeholders, and recording sent responses — extended with email dispatch orchestration
- **Sales_Controller**: The ASP.NET Core MVC controller responsible for pipeline views, contact management, lead request actions, and the new email send actions
- **LeadResponse**: A record in `[sales].[LeadResponse]` capturing a communication event with a contact, extended with email-specific fields (ChannelTypeId, EmailSubject, IsEmailSent, SentToEmail, EmailDeliveryStatusTypeId, EmailFailureReason, IsSentToSelf)
- **ChannelType**: A lookup table defining communication channel types: Email, Phone, WhatsApp, In-Person, Other
- **EmailDeliveryStatusType**: A lookup table defining email delivery outcomes: Sent, Failed, Bounced
- **EmailSendLog**: A rate-limiting audit table recording every email dispatch event with BusinessId, UserId, ContactEmail, and SentAtUtc
- **LeadResponseTemplate**: An existing configurable email template per product with placeholders, extended with a BodyHtml field for rich HTML content
- **Rate_Limit_Service**: The service responsible for enforcing per-user hourly limits, per-business daily limits, and per-contact cooldown periods before allowing an email dispatch
- **Email_Compose_Modal**: The UI modal on Lead Detail that allows composing, previewing, and sending an email to a contact
- **Legal_Footer**: The auto-appended block at the bottom of every outbound email containing the business name and registered address
- **Platform_SMTP**: The existing platform email infrastructure used for transactional emails (payment reminders, payslips), now extended for sales email dispatch via a dedicated sending address
- **From_Name**: The display name shown in the recipient's inbox, formatted as "FirstName LastName – BusinessName"
- **Reply_To_Address**: The business owner's email address set as the Reply-To header so that recipient replies are delivered directly to the business owner's inbox
- **Sending_Address**: The dedicated platform email address used exclusively for sales emails (e.g., leads@3inventors.com), separate from the transactional email sender
- **Send_Test_To_Me**: A preview action that dispatches the composed email to the logged-in user's own email address without recording a LeadResponse
- **Send_Copy_To_Me**: An option that, when enabled, delivers a duplicate of the outbound email to the sender's own email address alongside the contact delivery

## Requirements

### Requirement 1: Channel Type Data Model

**User Story:** As a business operator, I want each lead response to record the communication channel used, so that I can track how my team communicates with leads across different channels.

#### Acceptance Criteria

1. THE Portal_Database SHALL contain a `[sales].[ChannelType]` lookup table with columns: Id (TINYINT, PK, identity), Name (NVARCHAR(50), NOT NULL), CreatedAtUtc (DATETIME, NOT NULL, default GETUTCDATE())
2. THE Portal_Database SHALL seed the `[sales].[ChannelType]` table with values: (1, 'Email'), (2, 'Phone'), (3, 'WhatsApp'), (4, 'In-Person'), (5, 'Other')
3. THE Portal_Database SHALL contain a `[sales].[EmailDeliveryStatusType]` lookup table with columns: Id (TINYINT, PK, identity), Name (NVARCHAR(50), NOT NULL), CreatedAtUtc (DATETIME, NOT NULL, default GETUTCDATE())
4. THE Portal_Database SHALL seed the `[sales].[EmailDeliveryStatusType]` table with values: (1, 'Sent'), (2, 'Failed'), (3, 'Bounced')
5. THE Portal_Database SHALL add the following columns to the existing `[sales].[LeadResponse]` table: ChannelTypeId (TINYINT, NULL, FK to [sales].[ChannelType]), EmailSubject (NVARCHAR(200), NULL), IsEmailSent (BIT, NOT NULL, DEFAULT 0), SentToEmail (NVARCHAR(256), NULL), EmailDeliveryStatusTypeId (TINYINT, NULL, FK to [sales].[EmailDeliveryStatusType]), EmailFailureReason (NVARCHAR(500), NULL), IsSentToSelf (BIT, NOT NULL, DEFAULT 0)
6. THE Portal_Database SHALL add a BodyHtml column (NVARCHAR(MAX), NULL) to the existing `[sales].[LeadResponseTemplate]` table for storing rich HTML email content

### Requirement 2: Email Send Log for Rate Limiting

**User Story:** As a platform operator, I want every email dispatch event recorded in a dedicated log table, so that rate limits can be enforced and email activity audited.

#### Acceptance Criteria

1. THE Portal_Database SHALL contain a `[sales].[EmailSendLog]` table with columns: Id (INT, PK, identity), BusinessId (INT, NOT NULL, FK to Business), UserId (NVARCHAR(450), NOT NULL), ContactEmail (NVARCHAR(256), NOT NULL), SentAtUtc (DATETIME, NOT NULL, default GETUTCDATE()), CreatedAtUtc (DATETIME, NOT NULL, default GETUTCDATE())
2. WHEN an email is successfully dispatched, THE Email_Service SHALL insert a record into `[sales].[EmailSendLog]` with the BusinessId, UserId, recipient ContactEmail, and the current UTC timestamp
3. THE Portal_Database SHALL create a non-clustered index on `[sales].[EmailSendLog]` covering (UserId, SentAtUtc) for efficient per-user hourly rate limit queries
4. THE Portal_Database SHALL create a non-clustered index on `[sales].[EmailSendLog]` covering (BusinessId, SentAtUtc) for efficient per-business daily rate limit queries
5. THE Portal_Database SHALL create a non-clustered index on `[sales].[EmailSendLog]` covering (ContactEmail, SentAtUtc) for efficient per-contact cooldown queries

### Requirement 3: Rate Limiting Enforcement

**User Story:** As a platform operator, I want email sending rate-limited per user, per business, and per contact, so that the platform SMTP reputation is protected and contacts are not spammed.

#### Acceptance Criteria

1. WHEN a user attempts to send an email, THE Rate_Limit_Service SHALL check whether the user has sent 20 or more emails within the preceding 60-minute window (per-user hourly limit)
2. WHEN a user attempts to send an email, THE Rate_Limit_Service SHALL check whether the business has sent 100 or more emails within the preceding 24-hour window (per-business daily limit)
3. WHEN a user attempts to send an email to a specific contact email address, THE Rate_Limit_Service SHALL check whether an email was sent to that same address within the preceding 60-minute window (per-contact cooldown)
4. IF any rate limit check fails, THEN THE Email_Service SHALL reject the send request and return a descriptive error message identifying which limit was exceeded (e.g., "You have reached the hourly email limit of 20. Please wait before sending more emails.")
5. IF the per-contact cooldown check fails, THEN THE Email_Service SHALL return an error message including the approximate minutes remaining before the contact can be emailed again
6. THE Rate_Limit_Service SHALL exclude "Send Test to Me" dispatches from all rate limit counters (test emails do not count toward limits)

### Requirement 4: Email Composition and Sending

**User Story:** As a salesperson, I want to compose and send an email directly from the Lead Detail page, so that I can communicate with leads via email without leaving the platform.

#### Acceptance Criteria

1. WHEN the user clicks "Respond" on the Lead Detail page, THE Email_Compose_Modal SHALL open displaying: recipient email (read-only, sourced from Contact.Email), subject (editable, pre-filled from the matching LeadResponseTemplate.Subject), and body (rich HTML editor pre-filled with the rendered template including signature block)
2. THE Email_Compose_Modal SHALL provide an Edit/Preview toggle allowing the user to switch between the rich-text editor view and a rendered HTML preview of the email as the recipient would see it
3. WHEN the user clicks "Send Email", THE Email_Service SHALL compose the email with: From name formatted as "FirstName LastName – BusinessName", From address set to the dedicated Sending_Address, Reply-To set to the business owner's email, Subject from the modal subject field, and Body from the modal editor content with the Legal_Footer auto-appended
4. WHEN the user clicks "Send Email" and all rate limit checks pass, THE Email_Service SHALL dispatch the email via Platform_SMTP and record a LeadResponse with ChannelTypeId = 1 (Email), IsEmailSent = true, EmailDeliveryStatusTypeId = 1 (Sent), and the SentToEmail populated with the recipient address
5. WHEN the user clicks "Log Only", THE Response_Service SHALL record the LeadResponse with ChannelTypeId = 1 (Email), IsEmailSent = false, and no email dispatch (preserving existing log-only behaviour)
6. WHEN the contact has no email address (Contact.Email is null or empty), THE Email_Compose_Modal SHALL disable the "Send Email" button and display a message: "This contact has no email address. Add an email to send a response."
7. THE Email_Compose_Modal SHALL provide a "Cancel" button that closes the modal without recording any response or sending any email

### Requirement 5: Send Test to Me

**User Story:** As a salesperson, I want to send a test email to my own address before sending to the contact, so that I can verify formatting, placeholders, and content render correctly in a real inbox.

#### Acceptance Criteria

1. THE Email_Compose_Modal SHALL provide a "Send Test to Me" button that dispatches the composed email to the logged-in user's own email address
2. WHEN the user clicks "Send Test to Me", THE Email_Service SHALL send the email to the authenticated user's email address with the same From name, subject, body, and Legal_Footer that would be sent to the contact
3. WHEN a test email is dispatched, THE Email_Service SHALL NOT record a LeadResponse entry (test emails are not logged as communication events)
4. WHEN a test email is dispatched, THE Rate_Limit_Service SHALL NOT count it against any rate limit (per-user, per-business, or per-contact)
5. WHEN the test email dispatch succeeds, THE Email_Compose_Modal SHALL display a success notification: "Test email sent to {userEmail}. Check your inbox."
6. IF the test email dispatch fails, THEN THE Email_Compose_Modal SHALL display an error notification with the failure reason

### Requirement 6: Send Copy to Me

**User Story:** As a salesperson, I want the option to receive a copy of the email I send to a contact, so that I have a record in my own inbox for reference.

#### Acceptance Criteria

1. THE Email_Compose_Modal SHALL provide an "Also send me a copy" checkbox (unchecked by default)
2. WHEN the "Also send me a copy" checkbox is checked and the user clicks "Send Email", THE Email_Service SHALL dispatch an additional copy of the email to the authenticated user's email address after successfully sending to the contact
3. WHEN a copy is sent to the user, THE Email_Service SHALL record IsSentToSelf = true on the corresponding LeadResponse record for audit purposes
4. THE copy sent to the user SHALL be identical to the email sent to the contact (same subject, body, and Legal_Footer)
5. THE copy dispatch SHALL NOT count against per-contact cooldown limits (it is sent to the user, not the contact)

### Requirement 7: Email Delivery Failure Handling

**User Story:** As a salesperson, I want immediate feedback when an email fails to deliver, so that I know to follow up via another channel and the response is still logged for tracking.

#### Acceptance Criteria

1. IF the SMTP dispatch fails (connection failure, authentication error, or timeout exceeding 10 seconds), THEN THE Email_Service SHALL record the LeadResponse with IsEmailSent = false, EmailDeliveryStatusTypeId = 2 (Failed), and EmailFailureReason populated with the error description
2. IF the SMTP dispatch fails, THEN THE Email_Compose_Modal SHALL display an error notification: "Email could not be sent. The response has been logged without delivery." with the specific failure reason
3. IF a bounce notification is received asynchronously for a previously sent email, THEN THE Email_Service SHALL update the corresponding LeadResponse record's EmailDeliveryStatusTypeId to 3 (Bounced) and populate EmailFailureReason with the bounce reason
4. WHEN a contact's email address accumulates 3 or more bounced deliveries, THE Email_Service SHALL mark the Contact record's email as problematic (flag for review)
5. WHEN a contact's email is marked as problematic, THE Email_Compose_Modal SHALL display a warning badge next to the recipient field: "⚠️ Previous deliveries to this address have bounced"

### Requirement 8: Legal Footer Auto-Append

**User Story:** As a platform operator, I want a legal footer automatically appended to every outbound sales email, so that the platform maintains compliance with business communication requirements.

#### Acceptance Criteria

1. THE Email_Service SHALL auto-append a Legal_Footer block to the bottom of every outbound email body (both contact emails and copy-to-self emails)
2. THE Legal_Footer SHALL contain: the business registered name and the business registered address, formatted as a simple text block separated from the email body by a horizontal rule
3. THE Legal_Footer SHALL NOT include an unsubscribe link in Phase 1 (to be added in Phase 2 if required)
4. THE Email_Service SHALL source the business name and address from the authenticated Business profile record
5. IF the business has no registered address configured, THEN THE Email_Service SHALL include only the business name in the Legal_Footer

### Requirement 9: Rich HTML Template Editor

**User Story:** As a business operator, I want to create rich HTML email templates with formatting, tables, links, and images, so that my sales emails look professional and branded.

#### Acceptance Criteria

1. THE Sales_Controller SHALL provide a template editing interface for the BodyHtml field on LeadResponseTemplate records, using a rich-text editor with a formatting toolbar
2. THE rich-text editor SHALL support: bold, italic, underline, hyperlinks, bulleted lists, numbered lists, tables, and image insertion via absolute URL
3. THE rich-text editor SHALL store content as sanitized HTML in the BodyHtml field (no JavaScript, no external stylesheets, inline CSS only)
4. WHEN a LeadResponseTemplate has a BodyHtml value, THE Response_Service SHALL use the BodyHtml content (with placeholders rendered) as the email body instead of the plain-text BodyTemplate
5. WHEN a LeadResponseTemplate has no BodyHtml value (null), THE Response_Service SHALL fall back to the existing plain-text BodyTemplate wrapped in a basic HTML email container
6. THE template editor SHALL support a signature section at the bottom of the template body, including support for images via absolute URL (e.g., company logo), formatted text, and table layouts
7. THE Email_Service SHALL sanitize all HTML content before dispatch to remove any script tags, event handlers, or potentially dangerous markup while preserving safe formatting elements

### Requirement 10: Email Send Log History Page

**User Story:** As a business operator, I want to view a history of all emails sent from the platform, so that I can audit communication activity and monitor usage against rate limits.

#### Acceptance Criteria

1. THE Sales_Controller SHALL expose an action at the route /Sales/EmailHistory that renders the Email Send Log history page
2. THE Email Send Log history page SHALL display a paginated table (15 records per page) showing: date/time sent (formatted as local time), sender name (resolved from UserId), recipient email, subject line, delivery status (with colour-coded badge: green for Sent, red for Failed, amber for Bounced), and a link to view the associated lead
3. THE Email Send Log history page SHALL provide filter controls for: date range, sender (dropdown of team members), delivery status (All, Sent, Failed, Bounced), and a search box for recipient email
4. THE Sales_Controller SHALL add an "Email Log" sub-navigation item to the Sales module sidebar, positioned after "Templates"
5. WHEN a user clicks on a row in the email log, THE page SHALL expand or navigate to show the full email body and delivery details for that record

### Requirement 11: Subscription Tier Gating

**User Story:** As a platform operator, I want email sending restricted to Professional and Enterprise subscriptions, so that the feature is available only to paying tiers that justify the SMTP infrastructure cost.

#### Acceptance Criteria

1. WHEN a user on a Foundation subscription accesses the Lead Detail page, THE Email_Compose_Modal SHALL hide the "Send Email" button and display only the "Log Only" option (preserving existing behaviour)
2. WHEN a user on a Professional or Enterprise subscription accesses the Lead Detail page, THE Email_Compose_Modal SHALL display both "Send Email" and "Log Only" action buttons
3. IF a Foundation-tier user attempts to call the email send API endpoint directly, THEN THE Sales_Controller SHALL return a forbidden response with message: "Email sending is available on Professional and Enterprise plans."
4. THE Email_Compose_Modal SHALL display a subtle upgrade prompt for Foundation users: "Upgrade to Professional to send emails directly to your leads."

### Requirement 12: Email Address Validation

**User Story:** As a salesperson, I want invalid email addresses rejected before attempting to send, so that I avoid wasting rate limit quota on emails that will certainly fail.

#### Acceptance Criteria

1. WHEN the user clicks "Send Email", THE Email_Service SHALL validate the recipient email address format before attempting SMTP dispatch
2. IF the recipient email address fails format validation, THEN THE Email_Service SHALL reject the send request with an error: "Invalid email address format. Please update the contact's email."
3. THE Email_Service SHALL validate that the email address contains exactly one @ symbol, has a non-empty local part, has a non-empty domain part with at least one dot, and does not contain whitespace
4. THE Email_Service SHALL NOT attempt SMTP dispatch for an invalid email address (failed validation does not consume rate limit quota)
