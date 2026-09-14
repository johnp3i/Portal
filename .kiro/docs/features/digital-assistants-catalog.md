# Digital Assistants — Categories & Catalog

> **Status:** Living roadmap document. Phase 1 (the notification spine + Thank-You Assistant)
> is implemented, deployed, and tested. Everything else below is planned/design-captured
> and grouped for phasing. This document defines the two structural **categories** every
> assistant falls into, then lists the full **assistant catalog** organised into delivery
> groups.

---

## 1. What a Digital Assistant is

A Digital Assistant is a named, opt-in automation that runs on a business's behalf and whose
activity is visible in a log. Each is framed as a helper with a job (e.g. "the Thank-You
Assistant sends a courteous payment confirmation the moment a customer pays"), not as
infrastructure. Every assistant shares one common ending: **it writes a durable outbox row,
and the in-process dispatcher sends the email, retries on failure, and records the result.**

Because the delivery backbone (the "spine") is already built and proven in Phase 1, the only
thing that distinguishes one assistant from another is:

1. **What wakes it up** (its trigger), and
2. **What it composes** (the query + the email body).

That first point — the trigger — is what splits every assistant into exactly one of two
categories.

---

## 2. The two assistant categories

### Category A — Event / State-Driven Assistants

**Trigger:** a business event happens (a payment is recorded, a quotation is sent, an invoice
is issued). A **producer** reacts to that event and, if the relevant assistant is enabled,
writes an outbox row **inside the same database transaction as the event**. This guarantees
the notification is never lost and never sent for an event that rolled back.

**Architecture:** producer only. These assistants reuse the Phase 1 spine **as-is** — no new
infrastructure. They are the cheapest to build.

**Shape of the flow:**

```
Business event (in a txn)
        │
        ▼
  Producer (guarded: assistant enabled? recipient valid? not suppressed?)
        │  writes outbox row in the SAME txn
        ▼
  [notification].[OutboxMessage]  (Pending)
        │
        ▼
  Dispatcher  →  send email  →  Sent / Retry / Failed
```

**Examples:** Thank-You (built), Quotation Follow-Up, Invoice Issued, Quotation
Accepted/Rejected owner alert, New Payment Received owner alert.

**Key traits:**

- Reacts in near-real-time to something a user or customer just did.
- Transactionally atomic with the triggering event.
- No clock, no scan — the event *is* the trigger.
- Idempotency comes from the single enqueue point inside the transaction
  (`RelatedEntityType` + `RelatedEntityId` allow dedupe if ever needed).

---

### Category B — Scheduled Scan / Digest Assistants

**Trigger:** a **clock**, not an event. On a cadence (e.g. every Monday 08:00 in the
business's time zone, or daily), a scheduled pass wakes up, **scans** the platform data for
"due" things, **composes** one message per business (or per due item), and enqueues the
outbox row(s). The dispatcher then sends them exactly as in Category A.

**Architecture:** requires a piece the spine does **not** have yet — a generic
**scheduled-scan / digest engine**. The Phase 1 dispatcher only *sends* rows that a producer
already wrote; it has no component that *decides on a schedule* what to generate. This engine
is the single most reusable addition to the platform: once built, every scheduled assistant
becomes "a scan query + an HTML builder."

**Shape of the flow:**

```
Clock tick (scheduler pass, per cadence)
        │
        ▼
  Scheduled scanner (per enabled business, respecting timezone + send day/time)
        │  runs a scan/aggregate query
        │  composes a digest / per-item message
        │  idempotency guard: (assistant, business, period) — never double-send
        ▼
  [notification].[OutboxMessage]  (Pending)  × N
        │
        ▼
  Dispatcher  →  send email  →  Sent / Retry / Failed
```

**Examples:** Weekly Outstanding Balance Digest, Weekly Financial Snapshot, Daily
"what needs attention" brief, Payment Reminder (overdue scan), VAT period due reminder,
Recurring Invoices generation (a scheduled *document-generation* variant that also writes an
owner notification through the spine).

**Key traits:**

- Fires on time, independent of any single event.
- Composes aggregate or multi-record content (a digest), or scans for a set of due records.
- Needs an **idempotency period key** so a re-run within the same window doesn't resend.
- Needs a per-business **schedule preference** (send day/time) layered on the existing
  working-hours + `Business.TimeZoneId` model.

---

### Category comparison at a glance

| Aspect | Category A — Event/State-Driven | Category B — Scheduled Scan/Digest |
|--------|--------------------------------|-------------------------------------|
| Trigger | A business event | A clock / cadence |
| New infrastructure | None (producer only) | Scheduled-scan engine (build once) |
| Atomicity | Same txn as the event | Not tied to a single txn |
| Content | About one record | Aggregate/digest or a set of due records |
| Idempotency | Single enqueue point in txn | `(assistant, business, period)` guard |
| Timing control | Working hours (defer to window) | Send day/time + working hours |
| Relative build cost | Low | Medium (first one pays for the engine) |
| Built today? | Yes — Thank-You | No — engine not yet built |

---

## 3. Assistant catalog — grouped for delivery

The catalog below organises every identified assistant into groups. Group ordering reflects a
suggested delivery sequence: build the reusable engine early, ship high-value wins on top of
it, and defer the heaviest (document generation) until the engine is proven.

Legend — **Cat**: A = Event/State-driven, B = Scheduled scan/digest ·
**Recipient**: Customer (customer-facing, branded footer) / Owner (`PortalUser`, internal) ·
**Status**: ✅ done · 🟡 designed · ⚪ idea/roadmap.

---

### Group 1 — Shipped (Phase 1)

| Assistant | Cat | Recipient | Trigger | Status |
|-----------|-----|-----------|---------|--------|
| **Thank-You Assistant** | A | Customer | Payment recorded (manual or Stripe) | ✅ |

The proof of the spine. Everything else reuses what this validated.

---

### Group 2 — Quotation lifecycle (mixed A/B)

| Assistant | Cat | Recipient | Trigger | Status |
|-----------|-----|-----------|---------|--------|
| **Quotation Follow-Up** | **B** | Customer | A sent quotation is still unanswered after N days | 🟡 |
| **Quotation Sent acknowledgement** *(optional pair)* | A | Customer | Quotation sent | ⚪ |
| **Quotation Accepted owner alert** *(optional pair)* | A | Owner | Customer accepts a quote | ⚪ |

> **Note:** "Quotation Follow-Up" and "Quotation Follow-Up Reminder" are the **same
> assistant** — a follow-up nudge on an unanswered quote. They are not two separate items.

> **⚠️ Category correction (verified against code):** Quotation Follow-Up is **Category B
> (scheduled scan)**, NOT Category A. "A quote stayed unanswered for N days" is the *absence*
> of an event over time — nothing fires when a quote simply *stays* open — so it can only be
> detected by a scheduled scan (find quotations at status Sent whose share is older than N
> days with no acceptance). It therefore **depends on the Group 3 scheduled-scan engine** and
> is **not** spine-only. It should be built **after** Group 3, as a consumer of that engine.
>
> Additional code-verified gaps the Quotation Follow-Up spec must address:
> - **No `SentAtUtc` / `AnsweredAtUtc` on `Quotation`.** "Sent" is derived from
>   `ProposalShare.CreatedAtUtc` + status = Sent(2); "answered" from a `ProposalAcceptance`
>   existing (or status ≥ Accepted). There is **no Rejected/Expired status** — expiry is
>   computed at runtime from `Quotation.ValidUntil` (`DateOnly?`).
> - **No follow-up tracking, and the outbox dedup (`ExistsForRelatedEntityAsync`) is binary**
>   (a non-failed row exists → skip). "Nudge once" works out of the box; a repeating "every N
>   days up to M times" cadence needs a new tracking column or a per-cycle related-entity key.
> - **Two opt-out signals exist:** the assistant-level `AssistantOptOut` (used by Thank-You)
>   and `Customer.IsReminderOptedOut`. The follow-up should likely honour both.

The event-driven items in this group (Quotation Sent acknowledgement, Accepted owner alert)
**are** genuine Category A / spine-only quick wins; only the Follow-Up itself is Category B.

---

### Group 3 — Scheduled-scan foundation + Weekly owner digests (Category B)

This group **builds the scheduled-scan / digest engine** (Section 2, Category B) and ships the
two weekly owner-facing digests on top of it. The engine is the reusable payoff — Groups 4
and 5 depend on it.

| Assistant | Cat | Recipient | Cadence | Status |
|-----------|-----|-----------|---------|--------|
| **Scheduled-scan / digest engine** *(infrastructure, not an assistant)* | — | — | — | ⚪ |
| **Weekly Outstanding Balance Digest** | B | Owner | Weekly | ⚪ |
| **Weekly Financial Snapshot** | B | Owner | Weekly | ⚪ |

- **Weekly Outstanding Balance Digest** — a weekly summary of receivables: who owes what,
  totals outstanding, oldest/overdue balances. Answers "what am I owed this week?"
- **Weekly Financial Snapshot** — a weekly summary of the business's financial position
  (e.g. invoiced, paid, outstanding, key movement) for the owner's at-a-glance review.

Both go to the **owner** (`PortalUser`): no branded footer, no customer suppression concern;
instead a per-business "send me this digest" toggle plus a send day/time preference.

---

### Group 4 — Owner attention & alerts (mixed A/B, depend on Group 3 engine for the scheduled ones)

| Assistant | Cat | Recipient | Trigger | Status |
|-----------|-----|-----------|---------|--------|
| **Daily "what needs attention" brief** | B | Owner | Daily (overdue + quotes awaiting + tasks due) | ✅ |
| **New Payment Received alert** | A | Owner | Payment recorded (esp. Stripe) | ✅ |
| **VAT period due reminder** | B | Owner | Filing deadline approaching | ✅ |
| **Compliance filing due reminder** | B | Owner | Business Applications tracker due date | ⚪ |
| **Task & Meeting Reminder** *(was "Lead-Task Reminder")* | B | Team member (assignee) | Daily agenda of upcoming/overdue tasks + meetings | ✅ |

A daily brief is effectively a super-set of several signals in one email — a strong owner-value
item once the engine exists.

> **📋 Task & Meeting Reminder — spec written (`.kiro/specs/digital-assistants-task-meeting-reminder/`).**
> Scope widened from the original "Lead-Task Reminder": it now covers **both tasks and meetings**, is
> a **daily morning agenda** (not per-item countdown), and is **fan-out to the assigned team member**
> (N emails per business), NOT owner-only — so a person who never opens the portal still learns what's
> assigned to them. Key design decisions: tasks are 1-to-1 (existing `FollowUpTask.TeamMemberId`, needs
> wiring); meetings are 1-to-many via a **new `[sales].[MeetingTeamMember]` mapping table** + attendee
> UI; recipient = `TeamMember.Email` → linked portal-user email → **owner** (unassigned/no-email
> fallback); include overdue + per-business look-ahead (default 2 days); NO backfill (legacy/unassigned
> → owner); separate from the always-on Daily Brief. Needs a new **`IFanOutDigestComposer`** contract
> (the existing `IDigestComposer` returns a single message). Spans a Sales-module change (Part 1:
> assignment model) + the assistant (Part 2). AssistantType Id 7.

> **✅ Phase 4a shipped:** the **Daily Brief** (daily-cadence scheduled) and **New Payment
> Received** (event) assistants are implemented, building, and documented
> (`.kiro/specs/digital-assistants-owner-alerts/`, scenarios in
> `.kiro/docs/scenarios/digital-assistants-owner-alerts-testing.md`). The three remaining Group 4
> items (VAT, Compliance, Lead-Task) are the outstanding work.

> **📌 VAT period due reminder — content note (code-verified, for the future spec):** the email
> must include the **approximate net VAT payable** for the due period, not just the deadline.
>
> **The calc exists but is NOT cleanly exposed** (an earlier note in this doc wrongly cited
> `IVatIntegrationService.GetCurrentPeriodSummaryAsync` / `VatSummaryDto` — **those types do not
> exist**). The authoritative figure is `VatSubmissionService.ComputeSubmissionFiguresAsync(businessId,
> period)` → `VatFigures(TotalOutputVat, TotalInputVat, NetVatPayable)`, which computes in-memory and
> is **credit-note-aware** — but it is **private** and the service is **tenant-scoped** (uses
> `CurrentBusinessId`), so a tenant-less background scan cannot call it as-is. The public read-only
> surface is `GetPreSubmissionChecklistAsync`, which uses the right pattern: prefer the persisted
> `VatSubmission.NetVatPayable` when a (still-unsubmitted) row exists, else compute in-memory.
>
> **Spec must add a small accessor:** either make `ComputeSubmissionFiguresAsync` public / add
> `IVatSubmissionService.ComputeFiguresForPeriodAsync(int businessId, int periodId)` (tenant-less,
> explicit businessId), or replicate the persisted-then-compute fallback inside the composer.
>
> Label it an **estimate** ("approx., as it stands today") — late invoices/purchases/credit notes
> still move it pre-filing. Sign convention (matches `VatSubmissionService`): `> 0` → "tax owed",
> `< 0` → "refund due" (abs value), `= 0` → "no payment expected".

---

### Group 5 — Customer-facing receivables (Category B, depend on Group 3 engine)

| Assistant | Cat | Recipient | Trigger | Status |
|-----------|-----|-----------|---------|--------|
| **Payment Reminder (overdue invoice)** | B | Customer | Issued invoice past due, unpaid | ⚪ |
| **Payment link waiting reminder** | B | Customer | Issued invoice with an unused Stripe link after N days | ⚪ |
| **Invoice Issued / "here's your invoice"** | A | Customer | Invoice transitions to issued | ⚪ |

> **Note on Payment Reminders:** a Payment Reminders module already exists and is intentionally
> **untouched** by Phase 1. Migrating it onto the spine (so it becomes a Category B assistant
> with a unified log and delivery) is a deliberate, separately-scoped effort — not an
> automatic consequence of building the engine.

---

### Group 6 — Scheduled document generation (Category B + generation engine — heaviest)

| Assistant | Cat | Recipient | Trigger | Status |
|-----------|-----|-----------|---------|--------|
| **Recurring Invoices Assistant** | B | Owner (+ Customer via normal issue path) | Scheduled cadence per recurring definition | 🟡 |

Distinct from every other assistant: its scheduled run does not primarily *send* an email — it
**creates a real invoice** (header, lines, sequence number, VAT, totals) on a cadence,
optionally auto-issues it, optionally attaches a payment link, then writes an **owner
notification** through the spine ("N recurring invoices generated for {month}"). It reuses the
notification half of the spine and the Group 3 scheduled engine, but adds its own schedule
model and a generation engine. Fully designed in
`.kiro/specs/digital-assistants-notifications/design.md` → "Phase 2 — Recurring Invoices
Assistant". Heaviest because of document generation, so it is deliberately last.

---

### Group 7 — Payroll Assistant (Category B + scheduled document generation — independent)

| Assistant | Cat | Recipient | Trigger | Status |
|-----------|-----|-----------|---------|--------|
| **Payroll Assistant** | B | Employee (payslips) + Owner (status alerts) | Monthly cadence per business (e.g. 28th at 10:00 local) | 🟡 |

The Payroll Assistant is **independent, like Recurring Invoices** — a scheduled
4. **Group 6 — Recurring Invoices** — adds document generation on top of the scheduled engine.
5. **Group 7 — Payroll Assistant** — independent scheduled document-generation assistant. Its
   **email-only** modes are near-term feasible (the payroll generation, finalisation, and
   payslip-email paths already exist); the **SMS + secure-link** delivery channel is a
   separate, heavier, GDPR-sensitive sub-effort to phase on its own. Enterprise-gated.

The single most important architectural decision this document captures: **the
`IPayslipRenderer` + `IPayslipPdfService` via PuppeteerSharp, `PayrollHub` progress). The
assistant is essentially a **scheduler + orchestrator** on top of that surface, enqueuing
delivery through the notification spine.

> **Plan gating note:** the Payroll module is **Enterprise-only** (`payroll` module key),
> whereas Digital Assistants is Professional+. The Payroll Assistant therefore requires
> **both** `payroll` AND `digital_assistants` — effectively Enterprise. Confirm the gating
- **Recurring Invoices (Group 6) design:** `design.md` → "Phase 2 — Recurring Invoices
  Assistant".
- **Payroll module (Group 7 reuses this):** `.kiro/docs/Payslip_Phases_Timetable.md`
  (Phases A–D — the fully-built generation, finalisation, PDF, and payslip-email surface the
  Payroll Assistant schedules). Key services: `IPayrollService`, `IPayslipEmailService`,
  `IPayslipRenderer` + `IPayslipPdfService`.
- **Testing scenarios (Phase 1):** `.kiro/docs/scenarios/digital-assistants-testing.md`.
- **Plan gating / pricing:** `.kiro/docs/Subscription_Tier_Model.md`
  (`digital_assistants` module — Professional+; the Payroll Assistant additionally requires
  the Enterprise-only `payroll` module).
On the configured day/time, the assistant opens the month's `PayslipPeriod` (guarded by the
existing `(BusinessId, Year, Month)` uniqueness + `PeriodExistsAsync`), runs batch generation
(`GeneratePayslipsPreviewAsync` → `ConfirmBatchGenerationAsync`), and then follows the
owner's configured sub-option:
- **Generate only** → leave payslips in Preview, notify the owner "payroll for {month}
  generated and awaiting your review."
- **Generate + send** → additionally deliver payslips (see delivery channels) on a configured
  date/time.

> **⚠️ Finalisation decision (must resolve at spec time):** today payslips only *send* when
> **Finalised** (status 3/5), and finalisation creates **P&L entries** and feeds the
> **compliance filing**. Auto-finalising without a human check risks pushing wrong figures
> into P&L. So Mode 1 needs an explicit decision: (a) auto-finalise then send (fully
> autonomous, higher risk), or (b) generate-to-Preview and **require owner approval** before
> finalise+send (safer, recommended default). This is the single biggest product decision for
> this assistant.

**Mode 2 — Send generated payslips only:**
On the configured day/time, if the period is **finalised**, batch-send via the existing
`SendAllPayslipsAsync` (reused as-is: email-with-PDF, skip-no-email, throttled, logged).
If the period is **not** finalised, send **no** payslips and instead notify the owner
"payroll for {month} is still pending — generate and finalise it to enable sending."

#### Delivery channels (per-business config)

- **Email (exists today):** reuse `IPayslipEmailService.SendAllPayslipsAsync` — HTML→PDF
  attachment, per-send log (`PayslipEmailLog`), throttling. Lowest effort.
- **SMS + secure payslip link (NEW, heaviest, GDPR-sensitive):** an optional channel where
  instead of (or alongside) the emailed PDF, the employee receives an **SMS** (via an SMS
  service to be **replicated from another project** — none exists here today) prompting them
  to visit a **tokenised, expiring, password-protected** download link for their payslip.
  This introduces genuinely new infrastructure the platform does not have:
  - **Persistent payslip storage / retrievable artifact** — today PDFs are generated
    **in-memory only** (`byte[]`), never stored. A secure link needs the PDF (or a
    regenerate-on-demand token) to be retrievable after the run.
  - **A public, tokenised download endpoint** with expiration + password (GDPR: minimise
    exposure, short TTL, no PII in the URL, access-logged).
  - **An SMS sender** integrated as a delivery channel (mirroring the email path; the
    outbox/spine may need a channel concept, or SMS gets its own small sender).
  - **Employee contact field** — `Employee.Phone` exists (single column; no separate Mobile),
    reusable for SMS.

#### What it reuses vs. what is new

| Reuses (already built) | New (this assistant introduces) |
|------------------------|----------------------------------|
| `IPayrollService` generation/finalisation | A monthly **scheduler** (mirror `PaymentReminderBackgroundService`) |
| `IPayslipEmailService` (single + batch email) | Per-business **assistant config** (mode, day/time, channels, auto-finalise choice) |
| `IPayslipRenderer` + `IPayslipPdfService` (PDF) | **Idempotency guard** per (business, Year, Month) so a monthly re-run never double-generates/sends |
| Notification spine (owner alerts) | **SMS channel** + **secure payslip link** (storage + tokenised endpoint) — optional, heaviest |
| `PayslipStatusType` lifecycle + guards | Owner status notifications ("generated / awaiting review / still pending") |
| `Employee.Email` / `Employee.Phone` | A `payroll_assistant` AssistantType seed + Digital Assistants card |

#### Positioning

Like Recurring Invoices (Group 6), this is a scheduled document-generation assistant, so it
belongs with the heavy, independent group. Its **email-only** modes are near-term feasible
because the payroll email path already exists; the **SMS + secure-link** channel is a
separate, heavier sub-effort with real GDPR surface area and should be phased on its own.

---

## 4. Suggested delivery sequence

> **Correction:** an earlier version of this sequence put Quotation Follow-Up first as a
> "spine-only quick win." That was wrong — Quotation Follow-Up is Category B and depends on the
> scheduled-scan engine (see the Group 2 category-correction note). The engine is therefore
> built first, in Group 3.

1. **Group 3 — Scheduled-scan engine + the two Weekly digests** — builds the reusable Category
   B engine and ships two high-value owner digests on top of it. **Do this first** — it is the
   foundation the other scheduled assistants (including Quotation Follow-Up) depend on.
2. **Group 2 — Quotation Follow-Up** — now a straightforward consumer of the Group 3 engine
   (a scan query + an email template + the follow-up-tracking decision). The event-driven
   items in Group 2 (Sent acknowledgement, Accepted owner alert) are independent spine-only
   quick wins that can slot in any time.
3. **Group 4 / Group 5** — layer additional scheduled and event assistants onto the now-proven
   engine (daily brief, VAT/compliance reminders, payment reminder migration, invoice-issued).
4. **Group 6 — Recurring Invoices** — adds document generation on top of the scheduled engine.
5. **Group 7 — Payroll Assistant** — independent scheduled document-generation assistant;
   email-only modes near-term, SMS + secure-link phased separately. Enterprise-gated.

The single most important architectural decision this document captures: **the
scheduled-scan / digest engine (Category B infrastructure) should be built once, early
(Group 3),** because the two Weekly digests, the Recurring Invoices assistant, the payment
reminder migration, and the VAT/compliance reminders all ride on it.

---

## 5. Where this fits with existing docs

- **Spine (Phase 1) design & requirements:** `.kiro/specs/digital-assistants-notifications/`
  (`requirements.md`, `design.md`, `tasks.md`).
- **Recurring Invoices (Group 6) design:** `design.md` → "Phase 2 — Recurring Invoices
  Assistant".
- **Testing scenarios (Phase 1):** `.kiro/docs/scenarios/digital-assistants-testing.md`.
- **Plan gating / pricing:** `.kiro/docs/Subscription_Tier_Model.md`
  (`digital_assistants` module — Professional+).

Each group, when picked up, should get its own spec (requirements → design → tasks) rather
than being built directly from this catalog. This document is the map, not the build plan.
