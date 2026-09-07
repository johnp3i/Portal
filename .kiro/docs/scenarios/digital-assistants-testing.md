# Testing Scenarios: Digital Assistants — Notification Spine + Thank-You (Phase 1)

Digital Assistants are opt-in background automations. Phase 1 delivers the shared
**notification spine** (transactional outbox + in-process dispatcher) and the first
assistant — **Thank-You** — which emails a customer when a payment is recorded (manual or
Stripe card), from a platform address, with an optional "Powered by 3 Inventors" footer.

## Prerequisites

- Run migrations **191–196** against the Portal database (creates the `[notification]`
  schema, outbox/settings/opt-out/timezone tables, seeds the Thank-You assistant + time
  zones, adds `Business.TimeZoneId`, and grants the `digital_assistants` module to
  Professional & Enterprise).
- Configure the **`Notifications` email account** password in **User Secrets** (the
  `notifications@3inventors.com` entry in `EmailAccounts` ships with an empty password).
  > ⚠️ **Required, not optional.** Until the secret is set, every Thank-You send will fail SMTP
  > auth, retry to `Failed`, and — once past the threshold — trigger the admin failure alert.
  > This is correct fail-safe behaviour, but it means an unconfigured environment will actively
  > raise alerts. Set the secret before enabling the dispatcher in any real environment.
- Ensure the business is on **Professional or Enterprise** (the `digital_assistants` module
  is gated). A customer with a valid email and at least one **issued** invoice with an
  outstanding balance.
- `Notifications:EnableDispatcher = true` (default). Poll interval defaults to 60s.

> The business page is at `/Assistants`. The SuperAdmin page is at `/Admin/Notifications`.

---

## Scenario 1: Digital Assistants page renders and gates by plan

1. As a Professional/Enterprise user, navigate to `/Assistants`.
2. **Expected:** The "Digital Assistants" page lists the **Thank-You Assistant** card with its persona, an on/off toggle (on by default), working-hours inputs, a "Powered by 3 Inventors" footer toggle, and a "View activity log" link.
3. As a Foundation user (or a plan without `digital_assistants`), navigate to `/Assistants`.
4. **Expected:** The plan soft-gate / "not available on your plan" behaviour applies (consistent with other Professional modules).

---

## Scenario 2: Thank-You on manual payment (happy path)

1. Ensure the Thank-You Assistant is **enabled** and the customer has an email.
2. Record a manual payment against an issued invoice (Revenue → record payment).
3. **Expected (DB):** A row appears in `[notification].[OutboxMessage]` with `Status = 'Pending'`, `AssistantTypeId` = the thank_you id, the customer's email, a subject like "Thank you for your payment — INV-xxxx", and `ScheduledForUtc` ≈ now.
4. **Expected (atomicity):** The payment row and the outbox row were committed together — both exist.
5. Within one poll interval (~60s), **Expected:** the dispatcher sends the email; the outbox row flips to `Status = 'Sent'` with `SentAtUtc` set; the customer receives the thank-you from `notifications@3inventors.com` with the branding footer.
6. On the `/Assistants` page, open the Thank-You **activity log** → the send appears with a green "Sent" status.

---

## Scenario 3: Thank-You on Stripe card payment

1. With Stripe Connect active and the Thank-You Assistant enabled, have a customer pay an invoice by card on the shared invoice page.
2. When the `checkout.session.completed` webhook records the payment, **Expected:** an outbox row is written in the same transaction as the card payment, then sent by the dispatcher — same as the manual path.

---

## Scenario 4: Assistant disabled → no email

1. On `/Assistants`, toggle the Thank-You Assistant **off** (BlockUI → status flips to "Off").
2. Record a manual payment.
3. **Expected:** **No** outbox row is written; no email is sent.
4. Toggle it back **on** and record another payment → an outbox row appears again.

---

## Scenario 5: Per-service suppression (manual)

> **Note:** There is intentionally **no customer-facing unsubscribe link**. The thank-you is a
> transactional message triggered by the customer's own payment, not marketing — so it carries
> no unsubscribe requirement, and offering one would only invite "why wasn't my confirmation
> sent?" confusion. `[notification].[AssistantOptOut]` exists as an **admin/support suppression
> list** for the rare case where a customer explicitly asks to stop receiving a specific
> assistant's emails. Rows are added manually (SQL or an admin action), not by the recipient.

1. Manually insert an `[notification].[AssistantOptOut]` row for (business, thank_you, the customer's email):
   ```sql
   USE [Portal]
   GO
   INSERT INTO [notification].[AssistantOptOut]
       ([BusinessId], [AssistantTypeId], [CustomerId], [RecipientEmail], [OptedOutAtUtc], [CreatedAtUtc])
   VALUES
       (1, 1, NULL, N'customer@example.com', GETUTCDATE(), GETUTCDATE());
   ```
   (`AssistantTypeId = 1` = thank_you — verify against your seeded `[notification].[AssistantType]` rows.)
2. Record a payment for that customer.
3. **Expected:** No outbox row (the recipient is suppressed for this assistant specifically).
4. A **different** customer still receives the thank-you (suppression is per recipient, per assistant).

---

## Scenario 6: No email → no send

1. Use an invoice whose customer has **no email address**.
2. Record a payment.
3. **Expected:** No outbox row is written (guard: missing recipient).

---

## Scenario 7: Working-hours scheduling (business time zone)

1. Set the business's `TimeZoneId` (e.g. Cyprus).
2. On `/Assistants`, set the Thank-You working hours to a window that **excludes now** (e.g. 08:00–20:00 when it is currently 21:00 local), and Save.
3. Record a payment.
4. **Expected:** The outbox row's `ScheduledForUtc` is set to the **next window start** converted to UTC (e.g. tomorrow 08:00 local → UTC), so the dispatcher does **not** send it immediately.
5. Clear the working hours (leave blank) and record another payment → `ScheduledForUtc` ≈ now (sends on the next poll).
6. With no `TimeZoneId` set on the business, **Expected:** scheduling falls back to the configured default (`DefaultTimeZoneWindowsId`) / UTC without error.

---

## Scenario 8: Branding footer toggle

1. With the footer toggle **on**, trigger a thank-you → the received email contains the "Sent via 3 Inventors Business Portal — like this? Discover it →" footer.
2. Turn the footer toggle **off**, Save, trigger another → the email has **no** footer.

---

## Scenario 9: Retry then permanent failure

1. Temporarily misconfigure the `Notifications` SMTP account (e.g. wrong password) so sends fail.
2. Trigger a thank-you.
3. **Expected:** On each poll the outbox row's `RetryCount` increments and `LastError` is recorded, while `Status` stays `Pending` — until `RetryCount` reaches `MaxRetries` (default 5), at which point `Status` becomes `Failed` with `FailedAtUtc` set.
4. Fix the SMTP config → previously-`Pending` rows send on the next poll; already-`Failed` rows do **not** retry.

---

## Scenario 10: Admin failure-threshold alert

1. Configure recipients at `/Admin/Notifications` (SuperAdmin) — enter one or more valid emails, Save. **Expected:** invalid addresses are rejected with a clear message; valid ones are normalised to a semicolon list in `PlatformConfig`.
2. Drive `Failed` messages above `Notifications:FailureAlertThreshold` (e.g. lower the threshold for testing).
3. **Expected:** The admin recipients receive **one** summary alert email (not one per failure), and a high-severity entry appears in the SystemLogs viewer. A second batch within the window does **not** re-alert (throttle).

---

## Scenario 11: Dispatcher disable switch

1. Set `Notifications:EnableDispatcher = false` and restart.
2. Record a payment → an outbox row is written (Pending) but **not** sent (the dispatcher is off).
3. Re-enable and restart → pending rows send on the next poll.

---

## Scenario 12: SuperAdmin recipients access control

1. As a non-SuperAdmin, navigate to `/Admin/Notifications`.
2. **Expected:** Access denied (the controller is `[Authorize(Roles = "SuperAdmin")]`).

---

## Notes

- The dispatcher runs **across all businesses** with no tenant context — the outbox table has no tenant query filter by design; all reads/writes are scoped by explicit `BusinessId`.
- Notification emails send from a **platform account** (`notifications@3inventors.com`), not the business mailbox, to protect deliverability. The **reply-to** is set to the business's own contact email (`BusinessProfile.Email`) so customer replies reach the business.
- The payment ↔ outbox write is **atomic** (single transaction). A thank-you can never exist without its payment, and vice versa.
- The email body is rendered and stored on the outbox row at write time (self-contained), so the activity log shows exactly what was sent and the dispatcher needs no template/tenant context.
- Real-time SignalR admin toast for failures is a deferred enhancement; Phase 1 surfaces admin alerts via email + the system log.
- Recurring Invoices, Quotation Follow-Up, and Lead-Task reminder assistants are designed but **not** implemented in Phase 1.
