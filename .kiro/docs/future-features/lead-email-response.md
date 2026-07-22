# Future Feature: Lead Email Response

## Summary

Extend the "Log Response" action on Lead Detail to optionally send an actual email to the contact. Currently, responses are logged as records (for tracking responses made via phone, WhatsApp, in-person, etc.) but no email is dispatched.

## Current State (Phase 1)

- User clicks "Log Response" on Lead Detail
- Modal shows prepared text from a matching template (or empty)
- User edits and clicks "Save"
- A `[sales].[LeadResponse]` record is created
- Stage auto-advances (New → Contacted) if applicable
- NO email is sent

## Proposed Behaviour

### Two-Action Modal

When the user clicks "Respond" (renamed button in future):

1. Modal shows:
   - **To**: Contact email (pre-filled, read-only)
   - **Subject**: From template's Subject field (editable)
   - **Body**: Rendered template with placeholders replaced (editable)
   - **Channel indicator**: "Email" (could expand to SMS in future)

2. Two action buttons:
   - **Send Email** — Delivers the email AND records the response
   - **Log Only** — Records the response without sending (current behaviour)

### Email Composition

| Field | Source |
|-------|--------|
| From | Business email from `[portal].[BusinessProfile].Email` or configured SMTP sender |
| To | Contact email from `[sales].[Contact].Email` |
| Subject | `[sales].[LeadResponseTemplate].Subject` (rendered with placeholders) |
| Body | Rendered template body (HTML formatted) |
| Reply-To | Business email |

### Placeholders (already implemented)

- `{{ContactName}}` — Contact's full name
- `{{ProductName}}` — Product name from the lead request
- `{{ResponseTime}}` — Template's ResponseTimeInHours formatted (e.g., "4 hours")
- `{{BusinessName}}` — Business name from profile

### Email Infrastructure

The platform already has email sending capability via the Payment Reminders feature. Reuse:
- SMTP configuration (already in User Secrets / appsettings)
- Email sending service interface
- HTML email templating patterns

### Data Model Changes

Add columns to `[sales].[LeadResponse]`:

| Column | Type | Description |
|--------|------|-------------|
| Channel | NVARCHAR(20) NULL | "email", "phone", "whatsapp", "in_person", "other" |
| EmailSubject | NVARCHAR(200) NULL | Subject line used (if email) |
| IsEmailSent | BIT NOT NULL DEFAULT 0 | Whether an actual email was dispatched |
| SentToEmail | NVARCHAR(200) NULL | Email address the response was sent to |

### Validation

- If "Send Email" is chosen but contact has no email → show error: "This contact has no email address. Add an email to send a response."
- If email delivery fails → still record the response, but set `IsEmailSent = 0` and show a warning: "Response logged, but email delivery failed."

### Stage Transition

Same as current: if stage is "New" and a response is recorded (regardless of email sent or log-only), suggest transition to "Contacted".

### Audit

- `IsAutomated = false` for user-initiated responses
- `IsAutomated = true` for future auto-response features (out of scope for this iteration)

### UI Location

Lead Detail page → "Respond" button (replaces current "Log Response" when email feature is enabled)

### Tier Placement

Professional + Enterprise — email sending is an automation feature. Foundation users would keep "Log Only" behaviour.

### Dependencies

- SMTP configuration already exists (Payment Reminders)
- Email sending service already exists
- Template rendering already exists (PrepareResponseAsync)
- Only missing: wiring the "Send" button to the email service + recording delivery status

### Estimated Effort

Small — most infrastructure exists. Core work:
1. Wire modal to show To/Subject/Body fields
2. Call email service on "Send Email" click
3. Add Channel + IsEmailSent columns to LeadResponse
4. Handle delivery failure gracefully
