# Implementation Plan: Digital Assistants — Owner Alerts, Phase 4a

## Overview

Two owner-facing assistants built on existing infrastructure: **New Payment Received** (event
producer mirroring Thank-You) and **Daily Brief** (scheduled assistant reusing the Group 3
engine, generalized to a daily cadence, reusing an extracted attention-item builder).

Reuse-first: the outbox spine, dispatcher, scheduled runner, per-business settings, plan gating,
timezone resolution, and every attention-item query helper already exist. This phase generalizes
the engine (weekly → weekly+daily), extracts a shared builder, mirrors one producer, and hooks
three payment paths.

Design in `design.md`; requirements in `requirements.md`. Migrations pick the next FREE
sequential numbers; `USE [Portal]` header; idempotent guarded DDL.

> **Revised after code-grounded review.** Key corrections baked into this plan: New Payment does
> **no entity-dedup** (the Stripe path is already idempotent upstream — verified — so a retried
> webhook never reaches the payment insert); the global payment fires **one** alert for the
> parent, never per allocation; the Daily Brief distinguishes "quiet day" (info log) from "no
> recipient" (warning); the extracted attention-builder takes the KPI as an optional param so the
> Snapshot doesn't double-query; and the seed migration verifies it landed (MERGE no-ops on a
> taken id). See design.md "Review revisions summary."

## Tasks

- [x] 1. Database — seed the two new AssistantType rows
  - [x] 1.1 Seed `new_payment_received` (id 4) and `daily_brief` (id 5) via idempotent explicit-Id `MERGE`
    - Both `RecipientKind = 'PortalUser'`, `IsCustomerFacing = 0`. Confirm ids 4/5 free (1=thank_you, 2=weekly_outstanding_digest, 3=weekly_financial_snapshot).
    - **Verify the seed landed (Review fix #5):** the MERGE no-ops silently if an id is already taken, so after it, `SELECT`/`PRINT` a count of the two keys and check the output rather than assuming.
    - _Requirements: 4.1, 6.1_
  - [x] 1.2 Confirm `digital_assistants` plan gating covers both (same controller/module — no new key)
    - _Requirements: 4.4, 6.1_

- [x] 2. Shared owner-email resolver (Portal.Infrastructure)
  - [x] 2.1 `IOwnerEmailResolver` / `OwnerEmailResolver` — tenant-less `ResolveAsync(int businessId)` returning the owner email (`UserBusiness.IsOwner && IsActive` via MembershipDbContext)
    - _Requirements: 5.5_
  - [x] 2.2 Refactor `DigestRecipientResolver` to use `IOwnerEmailResolver` for its owner lookup (single source of truth; no behavioural change)
    - _Requirements: 5.5, 7.4_
  - [x] 2.3 Register `IOwnerEmailResolver` in Program.cs
    - _Requirements: 5.5_

- [x] 3. Shared attention-item builder (Portal.Infrastructure)
  - [x] 3.1 `IAttentionItemBuilder` / `AttentionItemBuilder` — `BuildAsync(businessId, currencySymbol, today, DashboardKpiDto? kpi = null)` returning `List<AttentionItem>`
    - Move the logic from `FinancialSnapshotComposer.BuildAttentionItemsAsync`. **KPI is an optional param (Review fix #6):** when null the builder fetches it; when passed, it's reused. This lets the Snapshot pass its already-loaded KPI (no double-query). Depends on IDashboardService, QuotationRepository, VatSubmissionPeriodRepository, NotificationOptions.
    - _Requirements: 2.1, 2.3_
  - [x] 3.2 Refactor `FinancialSnapshotComposer` to use `IAttentionItemBuilder`, passing its already-loaded KPI (no double-query); no change to the weekly email output
    - _Requirements: 2.2_
  - [x] 3.3 Register `IAttentionItemBuilder` in Program.cs
    - _Requirements: 2.1_

- [x] 4. New Payment Received producer (Portal.Infrastructure)
  - [x] 4.1 Add `PrepareNewPaymentAsync(int businessId, string? invoiceNumber, string? customerName, decimal amount)` to `INotificationProducer` + `NotificationProducer`
    - Gating mirrors Thank-You: resolve `new_payment_received` by key; per-business enabled; resolve OWNER email via `IOwnerEmailResolver` (null → return null); render owner body (no footer); `ScheduledForUtc = now` (intentional working-hours bypass). **No entity-dedup** (Review fix #1) — no `RelatedEntityType`/`RelatedEntityId`; runs fully before the txn (no post-insert id needed).
    - Inject `IOwnerEmailResolver` into `NotificationProducer`.
    - _Requirements: 5.4, 5.5, 6.2, 6.3_
  - [x] 4.2 `AssistantEmailBuilder.BuildNewPaymentHtml(...)` + subject — owner-facing, no branded footer, MyChair style
    - _Requirements: 5.4, 7.3_

- [x] 5. Hook New Payment Received into the three payment paths (Portal.Infrastructure / Portal.Web)
  - [x] 5.1 `PaymentService.RecordPaymentAsync` — prepare the owner alert before the txn; `InsertAsync` inside the txn (alongside Thank-You). No id-stamping (dedup removed).
    - Best-effort: a prepare/insert failure must NOT roll back the payment (Req 6.6).
    - _Requirements: 5.1, 6.5, 6.6_
  - [x] 5.2 `StripeConnectService.HandleCheckoutCompletedAsync` — same alongside its Thank-You (retried webhooks already short-circuit before the payment insert — no dedup needed)
    - _Requirements: 5.2, 6.5, 6.6_
  - [x] 5.3 `PaymentService.RecordGlobalPaymentAsync` — new hook; `invoiceNumber` null, `customerName` from `dto.CustomerId`, `amount = dto.Amount`; prepare before txn, `InsertAsync` inside the txn **after the parent insert** — exactly ONE alert for the parent, never per allocation child (Review fix #2)
    - _Requirements: 5.3, 6.4, 6.6_

- [x] 6. Daily cadence in the scheduled engine (Portal.Infrastructure)
  - [x] 6.1 `DigestCycleKey.Daily(assistantKey, businessLocalNow)` → `"{assistantKey}:{yyyy-MM-dd}"`
    - _Requirements: 1.2_
  - [x] 6.2 Generalize `ScheduledDigestRunner`: cadence lookup (daily vs weekly by assistant key); daily `IsDue` branch (time-of-day ≥ SendTimeLocal, ignore day-of-week, no back-fill); select `DigestCycleKey.Daily` vs `Weekly` by cadence; add the daily-brief key to the runner's assistant set
    - **Downgrade the null-branch log (Review fix #3):** the runner currently logs a *warning* ("no resolvable recipient") when a composer returns null. Change it to Debug/Information (or stay silent) so the Daily Brief's expected quiet-day null isn't logged as a false recipient warning — the composer now logs its own reason-specific message (task 7.2).
    - Weekly digests unaffected.
    - _Requirements: 1.1, 1.3, 1.4, 1.5_

- [x] 7. Daily Brief composer + email (Portal.Infrastructure)
  - [x] 7.1 `DigestAssistantKeys.DailyBrief` const
    - _Requirements: 4.1_
  - [x] 7.2 `DailyBriefComposer : DigestComposerBase, IDigestComposer` — resolve recipient; build attention items via `IAttentionItemBuilder` (pass null KPI); render owner body when there are items
    - **Reason-specific logging (Review fix #3):** no recipient → log **warning** + return null; empty attention items → log **information** ("nothing to report today") + return null. Both return null (runner skips), but the logs distinguish a real problem from a normal quiet day.
    - _Requirements: 3.1, 3.2, 3.3, 3.4_
  - [x] 7.3 `DigestEmailBuilder.BuildDailyBriefHtml(businessName, currencySymbol, items)` + subject — owner-facing, today-focused heading, reuse the attention-item rendering (urgent = red), no figures/glance block
    - _Requirements: 3.1, 3.5, 7.3_
  - [x] 7.4 Register `DailyBriefComposer` as `IDigestComposer` in Program.cs
    - _Requirements: 1.1_

- [x] 8. Checkpoint — backend build
  - Build Infrastructure + Web; 0 errors. Verify the extraction didn't change Snapshot behaviour.

- [x] 9. UI — cards for both assistants (Portal.Web)
  - [x] 9.1 Extend the `Index` projection with a cadence marker (event / daily / weekly) so the view picks the right control set
    - _Requirements: 4.1, 6.1_
  - [x] 9.2 Daily Brief card: send-time-only (no day-of-week, no figures) + recipient + owner-CC toggle + activity-log expander
    - _Requirements: 4.3, 4.5_
  - [x] 9.3 New Payment Received card: on/off toggle + recipient (owner + optional override) + activity-log expander; no schedule
    - _Requirements: 6.1_
  - [x] 9.4 Extend `AxPostSaveAssistantSettings` for the daily (time-only) and event (recipient-only) shapes; keep preserve-on-partial-write
    - _Requirements: 4.3, 6.1_

- [ ] 10. Final checkpoint — build + manual verification
  - Build, 0 errors.
  - Verify: Daily Brief on a business with overdue items sends once/day and **skips on a clean day** (quiet day logs an info line, NOT a recipient warning); a second poll the same day → no duplicate (daily cycle-key dedup). New Payment alert fires **once** for manual, Stripe, and global payments (owner receives it) — the global payment produces exactly one alert for the parent, not one per allocation; a retried Stripe webhook produces no second alert (short-circuits before the payment insert). Disabling each suppresses it; non-Professional plan → nothing; preserve-on-partial-write on the new cards; weekly Snapshot output unchanged after the builder extraction (regression, incl. no extra KPI query); Thank-You unchanged.
  - Add scenarios to `.kiro/docs/scenarios/` (extend the digests testing doc or a new owner-alerts doc).

## Notes

- Reuse the outbox spine, dispatcher, `ScheduledDigestRunner`, `DigestComposerBase`,
  `IDigestRecipientResolver`, plan gating, and timezone resolution.
- `catch (Exception ex)` everywhere; repositories rethrow; runner fails safe per business.
- New Payment dedup keys on `paymentId` (not invoice) so the global path works and retried
  webhooks don't duplicate.
- Daily Brief "skip when empty" = composer returns null (runner already treats null as skip).
- Attention items come from ONE shared builder (Snapshot + Daily Brief) — no duplication/drift.
- Migrations: next FREE sequential numbers; `USE [Portal]`; idempotent MERGE for seeds.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1","1.2"] },
    { "id": 1, "tasks": ["2.1","2.2","2.3","3.1","3.2","3.3"] },
    { "id": 2, "tasks": ["4.1","4.2","6.1","6.2","7.1"] },
    { "id": 3, "tasks": ["5.1","5.2","5.3","7.2","7.3","7.4"] },
    { "id": 4, "tasks": ["8"] },
    { "id": 5, "tasks": ["9.1","9.2","9.3","9.4"] },
    { "id": 6, "tasks": ["10"] }
  ]
}
```
