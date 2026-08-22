# Lead Email Response — Design Exploration

**Date:** August 2026  
**Status:** Exploration — NOT ready for implementation  
**Module:** Sales Pipeline  
**Tier:** Professional

---

## Summary

This feature adds the ability to send actual emails to sales contacts/leads directly from the Lead Detail page. Currently, responses are logged as records (tracking phone, WhatsApp, in-person responses), but no email is dispatched.

This would be the **first feature where the platform sends emails on behalf of the business** (from the business's identity) rather than from the platform's own address.

---

## Corner Cases & Open Questions

### 1. Email Sending Architecture

**Question:** How does the email appear to the recipient?

| Option | From Address | Pros | Cons |
|--------|-------------|------|------|
| A. Platform SMTP + Reply-To | From: "MyCompany via Portal" <noreply@3inventors.com>, Reply-To: john@mycompany.com | Simple setup, reliable delivery, no per-business SMTP config needed | "From" doesn't show business email — looks like a platform email, less personal |
| B. Business's own SMTP | From: john@mycompany.com (sent via business's own SMTP server) | Fully branded, replies go directly to business | Complex setup, deliverability varies, SMTP credentials stored per business, support burden |
| C. Platform SMTP + custom From name | From: "John Smith — MyCompany" <sales@3inventors.com>, Reply-To: john@mycompany.com | Middle ground — personal feel without SMTP complexity | "From" email is still platform, but name shows business identity |

**Current platform emails use:** Platform SMTP only (ask@3inventors.com). No per-business SMTP exists today.

**Recommendation:** Start with **Option C** (platform SMTP, custom From name + Reply-To). This delivers 90% of the value without the SMTP configuration complexity. If customers demand full branded sending later, Option B can be added as a future upgrade.

---

### 2. Rate Limiting & Abuse Protection

**Question:** How many emails can a user send?

| Limit | Value | Rationale |
|-------|-------|-----------|
| Per lead response | 1 email per response action | Each "Send Email" click = 1 email to 1 recipient |
| Per hour (per user) | 20 emails | Prevents accidental spam loops or scripted abuse |
| Per day (per business) | 100 emails | Protects platform SMTP reputation; aligns with typical SMTP provider daily limits |
| Cooldown per contact | 1 hour minimum between emails to the same contact | Prevents harassment/rapid-fire emails to same person |

**Enforcement:** Service-layer check before sending. On limit hit, return clear error: "Daily email limit reached (100). Try again tomorrow." or "Please wait before sending another email to this contact."

**Tracking:** Add `[sales].[EmailSendLog]` table (BusinessId, UserId, ContactEmail, SentAtUtc) for rate limit queries.

---

### 3. SMTP Validation & Configuration

**Question:** How do we know the sending works?

Since we're using **platform SMTP** (Option C recommendation), this is simpler:
- The platform's own SMTP is already tested and operational (payment reminders, payslips work)
- No per-business SMTP validation needed

**If Option B (business SMTP) is ever added:**
- "Test Connection" button during SMTP setup — sends a test email to the business owner
- Store credentials encrypted (not plain text)
- Health check: if delivery fails 3 times consecutively, disable and notify the owner
- Validation UI: show "Connected ✓" / "Connection Failed ✗" with last test result

---

### 4. Email Preview Before Sending

**Question:** Can the user see exactly what will be sent?

**Approach:** The modal shows a **rendered preview** of the email body (HTML formatted) before the user clicks "Send". This is already partially in place — the current "Log Response" modal shows template text.

**Enhanced preview flow:**
1. User clicks "Respond" → modal opens
2. Template is rendered with placeholders replaced ({{ContactName}}, {{ProductName}}, etc.)
3. User sees the **formatted HTML preview** in a "preview pane" area (read-only rendered HTML)
4. User can switch between "Edit" mode (textarea/rich editor) and "Preview" mode (rendered HTML)
5. Below the preview: "Send Email" / "Log Only" / "Cancel" buttons
6. Before sending: optional "Send Test to Myself" button (sends the email to the business owner's email for verification)

**Rich text support:** The preview should render tables, bold, links, and inline images correctly (since email templates may contain these).

---

### 5. Email Signatures & Rich Content

**Question:** Can emails contain signatures with images, logos, and formatted content?

**Option A: Template-embedded signature (simpler):**
- The email template itself includes the signature block at the bottom
- Signature includes business name, contact info, logo
- Different templates for different products/use cases can have different signature blocks
- Images in signatures use **absolute URLs** pointing to hosted assets (e.g., logo at `https://portal.3inventors.com/uploads/logos/{file}`)

**Option B: Separate signature entity (more flexible):**
- New `[sales].[EmailSignature]` table: Id, BusinessId, Name, HtmlContent, IsDefault
- User creates/manages signatures in Settings
- When composing an email, the signature is appended below the template body
- Multiple signatures per business (one per user, one per product line, etc.)

**Rich content support in either option:**
- ✅ **Bold, italic, links** — standard HTML in templates
- ✅ **Tables** — HTML tables render in all email clients
- ✅ **Images (logos)** — use absolute URLs (not base64 inline for deliverability reasons)
- ⚠️ **Inline CSS only** — email clients don't support external stylesheets
- ❌ **No JavaScript** — stripped by all email clients
- ❌ **No embedded fonts** — use system fonts or web-safe fonts only

**Recommendation:** Start with **template-embedded signatures** (Option A). The template editor already exists — just include a signature section at the bottom of each template. If separate signatures are requested later, add the entity.

---

### 6. Email Deliverability & Reputation

**Question:** How do we protect the platform's SMTP reputation?

| Risk | Mitigation |
|------|-----------|
| Spam complaints | Rate limiting (per-contact cooldown, daily cap) |
| Bounce rate | Validate email format before sending; track bounces |
| Blacklisting | Send from a subdomain (e.g., `mail.3inventors.com`) so main domain isn't affected |
| Unsubscribe requests | Include "Unsubscribe" footer link in every email (CAN-SPAM / GDPR compliance) |
| Content filtering | Avoid spam trigger words in default templates; warn if subject line is empty |

**Bounce handling:**
- If delivery fails (bounce), mark the contact's email as "undeliverable"
- Show a badge on the contact record: "⚠️ Email delivery failed"
- Don't attempt to send to contacts with failed deliveries until the email is updated

---

### 7. Legal / Compliance Considerations

| Requirement | Approach |
|-------------|----------|
| CAN-SPAM (US) | Include business name + address in footer, unsubscribe link |
| GDPR (EU) | Consent assumed for B2B sales outreach (legitimate interest basis); include opt-out option |
| Record keeping | Every sent email logged in `[sales].[LeadResponse]` with full body + recipient for audit |
| Opt-out | Per-contact "Do not email" flag — if set, "Send Email" button is disabled with explanation |

---

### 8. Failed Delivery Handling

**Question:** What happens when the email can't be delivered?

| Scenario | Behaviour |
|----------|-----------|
| SMTP connection failure | Show error immediately: "Email could not be sent. The response has been logged." → Log the response with `IsEmailSent = false` |
| Bounce (delayed) | Platform receives bounce notification → update LeadResponse with bounce info → mark contact email as problematic |
| Timeout (>10 seconds) | Abort, show timeout error, log response without email |
| Invalid recipient address | Validate format before sending → reject with "Invalid email address" |
| Rate limit hit | Block with clear message, don't log the response (user hasn't finished the action) |

---

### 9. Reply Tracking

**Question:** Can we track if the contact replies?

**Phase 1 (No):** The email sets `Reply-To: business-owner@company.com`. Replies go directly to the business owner's inbox — the platform has no visibility.

**Phase 2 (Future, if demanded):**
- Use a unique reply-to address per conversation: `reply+{token}@mail.3inventors.com`
- Platform receives the reply, links it back to the lead, stores as a new response with `Channel = "email_reply"`
- This requires mailbox monitoring infrastructure (IMAP/webhook from email provider)

**Recommendation:** Phase 1 only. Reply tracking is complex and rarely requested for initial adoption.

---

### 10. Template Editor Capabilities

**Question:** What can the user put in a template?

Current templates support plain text with `{{placeholders}}`. For email sending, we need:

| Capability | Support |
|-----------|---------|
| Plain text | ✅ |
| Bold, italic, underline | ✅ (Quill or simple toolbar) |
| Links (clickable URLs) | ✅ |
| Bullet/numbered lists | ✅ |
| Tables (pricing tables) | ✅ (HTML tables) |
| Images (inline logo/product photo) | ✅ (via URL — hosted on platform or external) |
| File attachments | ❌ Phase 1 (adds complexity — PDF proposals could be a Phase 2 addition) |
| Placeholder variables | ✅ ({{ContactName}}, {{ProductName}}, {{BusinessName}}, etc.) |
| Conditional sections | ❌ (too complex for V1) |

**Editor approach:** Use a lightweight rich-text editor (Quill.js is already in the project for some fields). The editor stores HTML. On send, the HTML is sanitized and wrapped in a branded email container.

---

### 11. Multi-recipient (CC/BCC)

**Question:** Can the user CC someone?

**Phase 1:** No. Single recipient only (the contact's email). The business owner receives a copy via BCC automatically (so they have a record in their own inbox).

**Phase 2:** Optional CC field for including colleagues or additional stakeholders.

---

### 12. Relationship to Existing Email Features

| Feature | Sends From | Purpose | Template Source |
|---------|-----------|---------|----------------|
| Payment Reminders | Platform (ask@3inventors.com) | Chase overdue invoices | Pre-configured escalation templates |
| Payslip Delivery | Platform (ask@3inventors.com) | Deliver payslip PDFs | Fixed template |
| Quotation/Invoice Sharing | Platform (ask@3inventors.com) | Share document links | Fixed template |
| **Lead Email Response** | Platform + Reply-To (business email) | **Sales communication** | **User-editable templates per product** |

The Lead Email Response is unique: it's **conversational** (not transactional), it's **editable per send** (not fire-and-forget), and it represents the **business's voice** (not the platform's).

---

## Recommended Phase 1 Scope

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Sending mechanism | Platform SMTP + Reply-To (Option C) | Simplest, most reliable |
| Rate limits | 20/hour per user, 100/day per business, 1hr/contact cooldown | Protects reputation |
| Preview | Rendered HTML preview in modal before send | Prevents mistakes |
| Signatures | Template-embedded (include in template body) | Simplest, already supported |
| Rich content | Bold, italic, links, tables, images (via URL) | Covers 95% of use cases |
| Editor | Quill.js (already in project) | No new dependencies |
| Reply tracking | None (Reply-To goes to business inbox) | Phase 2 if demanded |
| Attachments | None | Phase 2 |
| CC/BCC | Auto-BCC to business owner only | Phase 2 for explicit CC |
| Bounce handling | Mark contact email as problematic | Prevents repeated failures |
| Legal footer | Auto-appended to every email (business name + unsubscribe link) | CAN-SPAM/GDPR compliance |
| Validation | SMTP test not needed (platform SMTP); email format validated | Minimal friction |

---

## Data Model Changes (Phase 1)

### Modify `[sales].[LeadResponse]`

| Column | Type | Description |
|--------|------|-------------|
| Channel | NVARCHAR(20) NULL | "email", "phone", "whatsapp", "in_person", "other" |
| EmailSubject | NVARCHAR(200) NULL | Subject line used (if email) |
| IsEmailSent | BIT NOT NULL DEFAULT 0 | Whether email was dispatched |
| SentToEmail | NVARCHAR(256) NULL | Recipient email |
| EmailDeliveryStatus | NVARCHAR(20) NULL | "sent", "failed", "bounced" |
| EmailFailureReason | NVARCHAR(500) NULL | Error message if failed |

### New `[sales].[EmailSendLog]` (for rate limiting)

| Column | Type | Description |
|--------|------|-------------|
| Id | INT IDENTITY PK | |
| BusinessId | INT NOT NULL | |
| UserId | NVARCHAR(450) NOT NULL | |
| ContactEmail | NVARCHAR(256) NOT NULL | |
| SentAtUtc | DATETIME NOT NULL | |

### Modify `[sales].[LeadResponseTemplate]`

| Column | Type | Description |
|--------|------|-------------|
| BodyHtml | NVARCHAR(MAX) NULL | Rich HTML version of the body (for email sending) |
| Subject | NVARCHAR(200) NULL | Email subject line (already exists — confirm) |

---

## UI Flow (Phase 1)

```
Lead Detail → Click "Respond" →
Modal opens with:
  1. Recipient (read-only): contact@example.com
  2. Subject: pre-filled from template (editable)
  3. Body: Quill editor with rendered template + signature (editable)
  4. Preview toggle: switch between Edit/Preview modes
  
Actions:
  [Send Email]  → validates → rate limit check → sends → logs response
  [Log Only]    → logs response without sending (current behaviour)
  [Cancel]      → closes modal, no action
```

---

## Questions for You Before Proceeding

1. **From name format:** What should the "From" name look like? Options:
   - "John Smith — MyCompany"
   - "MyCompany"
   - "MyCompany via 3 Inventors Portal"

2. **Auto-BCC:** Should every email automatically BCC the business owner (so they have a copy in their inbox)?

3. **Template editor:** Is the existing plain-text template sufficient for Phase 1, or do you want the Quill rich-text editor from day one?

4. **Unsubscribe:** Should the unsubscribe link in the footer simply mark the contact as "do not email" in the Portal, or should it link to an external page?

5. **"Send Test to Myself":** Should the preview include a "Send test email to me" button so the business owner can check rendering in their actual inbox before sending to the contact?

---

## Next Steps

Once these questions are resolved:
1. Create formal spec (requirements → design → tasks)
2. Implement Phase 1
3. Gather feedback
4. Plan Phase 2 (reply tracking, attachments, CC, business SMTP)
