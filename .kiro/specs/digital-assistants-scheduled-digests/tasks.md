# Implementation Plan: Scheduled-Scan Engine + Weekly Owner Digests (Group 3)

## Overview

Bottom-up build of the Category B **scheduled-scan engine** and the first two digests
(Weekly Outstanding Balance, Weekly Financial Snapshot). Database + seed + the outbox
`CycleKey` column first, then the `BusinessAssistantSetting` extension and EF config, the
tenant-less query + plan-check overloads the composers/runner need, the digest composers + HTML
builders (Infrastructure), the exact-`CycleKey` enqueue + dedup path, the sibling scheduler
hosted service (Web), the config/DI wiring, the `/Assistants` UI cards, and finally the
checkpoints + a manual test-scenarios doc.

> **Revised after code-grounded review.** This plan incorporates nine review fixes; the notable
> structural ones: a dedicated exact `OutboxMessage.CycleKey` column (not an int hash) for
> per-cycle dedup, tenant-less `IPlanCheckService` + composer-query overloads (the scheduler has
> no `HttpContext`), preserve-on-partial-write for the settings upsert (a toggle must not erase a
> configured schedule), and extending the existing save endpoint rather than adding a second.
> See design.md "Review revisions summary."

Reuse-first: receivables/payables/P&L queries, the outbox spine, the dispatcher, the
AssistantType seed pattern, and the `/Assistants` page all already exist. This plan mostly
**wires existing pieces onto a new scheduler** plus two new email templates.

Design decisions confirmed in `design.md`; requirements in `requirements.md`. Migrations pick
the next FREE sequential numbers (folder has known duplicate-number collisions); every SQL
script uses `USE [Portal]` per steering.

## Tasks

- [ ] 1. Database — seed digest assistants + extend settings + add outbox CycleKey
  - [ ] 1.1 Seed two `[notification].[AssistantType]` rows via idempotent explicit-Id `MERGE`
    - `(2, 'weekly_outstanding_digest', 'Weekly Outstanding Balance Digest', <persona>, 'PortalUser', 0)`
    - `(3, 'weekly_financial_snapshot', 'Weekly Financial Snapshot', <persona>, 'PortalUser', 0)`
    - Ids 2 and 3 (Review fix #6 — no pre-reserved Quotation Follow-Up id; it takes the next free id later). `MERGE` keyed on `Id` is authoritative; if an id is already taken the migration fails loudly — reassign + update code constants.
    - _Requirements: 9.1, 10.4_
  - [ ] 1.2 Extend `[notification].[BusinessAssistantSetting]` with nullable/defaulted columns
    - `SendDayOfWeek TINYINT NULL`, `SendTimeLocal TIME NULL`, `RecipientOverride NVARCHAR(1000) NULL`, `IsRecipientOwnerIncluded BIT NOT NULL DEFAULT 1`, `IncludedFiguresCsv NVARCHAR(400) NULL`
    - Guarded `IF COL_LENGTH(...) IS NULL` adds; additive only — Thank-You rows unaffected.
    - _Requirements: 2.1, 4.3, 7.2, 8.3_
  - [ ] 1.3 Add `CycleKey NVARCHAR(80) NULL` to `[notification].[OutboxMessage]` + filtered index (Review fix #5, #8)
    - Guarded `IF COL_LENGTH(...) IS NULL` add; then `CREATE INDEX IX_OutboxMessage_Cycle ON [notification].[OutboxMessage] ([BusinessId], [AssistantTypeId], [CycleKey]) WHERE [CycleKey] IS NOT NULL;`
    - Additive/nullable — Thank-You rows leave it NULL and are unaffected.
    - _Requirements: 6.1, 6.2_
  - [ ] 1.4 Verify `digital_assistants` plan gating already covers these assistants
    - No new module key — reuse `digital_assistants` (Professional+). Confirm no CHECK/seed change needed.
    - _Requirements: 10.1, 10.2_

- [ ] 2. Entities + EF Core configuration (Portal.Infrastructure)
  - [ ] 2.1 Add the five new properties to `BusinessAssistantSetting` entity
    - `byte? SendDayOfWeek`, `TimeOnly? SendTimeLocal`, `string? RecipientOverride`, `bool IsRecipientOwnerIncluded`, `string? IncludedFiguresCsv`
    - _Requirements: 2.1, 4.3, 7.2, 8.3_
  - [ ] 2.2 EF config for the new columns in `PortalDbContext` (types, defaults, nullability)
    - `IsRecipientOwnerIncluded` default value; keep existing config intact.
    - _Requirements: 8.3_
  - [ ] 2.3 Add `DigestAssistantKeys` constants (`weekly_outstanding_digest`, `weekly_financial_snapshot`) and the seeded ids where needed
    - Mirror `NotificationProducer.ThankYouKey` (resolve by Key at runtime).
    - _Requirements: 9.1_
  - [ ] 2.4 Add `CycleKey` property to `OutboxMessage` entity + EF config; update `NotificationOutboxRepository.InsertAsync` to persist it (Review fix #5)
    - `string? CycleKey`; null-safe param; existing Thank-You inserts pass NULL (unchanged behaviour).
    - _Requirements: 6.1_
  - [ ] 2.5 Add `NotificationOutboxRepository.ExistsForCycleAsync(businessId, assistantTypeId, cycleKey)` (Review fix #5)
    - Exact match on `(BusinessId, AssistantTypeId, CycleKey)` excluding Failed rows; honours ambient transaction like `ExistsForRelatedEntityAsync`.
    - _Requirements: 6.2, 6.3, 6.4_

- [ ] 3. Tenant-less query overloads (Portal.Infrastructure) — enable the tenant-less scheduler
  - [ ] 3.1 Add `PnlService` explicit-businessId snapshot computation
    - New overload/helper `ComputeSnapshotAsync(int businessId, DateOnly from, DateOnly to)` (or `GetSummaryAsync(int businessId, PnlPeriodRequest)`) reusing the private compute methods; no reliance on `ICurrentTenantService`.
    - _Requirements: 4.2, 10.3_
  - [ ] 3.2 Add a parameterised upcoming-payables query if the TOP-5 cap is too tight
    - Variant of `DashboardService.GetUpcomingSupplierPaymentsAsync(int businessId, int? take, int windowDays)`; preserve existing behaviour/defaults.
    - _Requirements: 3.3_
  - [ ] 3.3 **AUDIT** the composer dependencies for hidden ambient-tenant use (Review fix #2) — genuine code check, not a rubber-stamp
    - Read the implementations of `DashboardService.GetKpiDataAsync(int businessId)`, `ReceivablesQueryService.GetReceivablesAsync(int businessId, ...)`, and `GetUpcomingSupplierPaymentsAsync(int businessId)`. Confirm NONE reach for `ICurrentTenantService`/`HttpContext` internally (currency, Z-report flag, secondary lookups).
    - **Fallback:** any method found using ambient tenant gets the same explicit-businessId overload treatment as `PnlService` (3.1). Do not wire a composer to a tenant-scoped method.
    - _Requirements: 3.1, 3.2, 4.2, 10.3_
  - [ ] 3.4 Add tenant-less plan-check overloads to `IPlanCheckService` (Review fix #1)
    - `Task<List<string>> GetPlanModulesAsync(int businessId)` + `Task<bool> IsModuleInPlanAsync(int businessId, string module)`, reusing the existing `BusinessPlans → PlanFeatures` query but keyed by the passed `businessId` and skipping the HttpContext cache. Existing HttpContext-based methods delegate to the overloads (single source of truth).
    - _Requirements: 10.1, 10.2_

- [ ] 4. Recipient resolution (Portal.Infrastructure)
  - [ ] 4.1 `IDigestRecipientResolver` / `DigestRecipientResolver`
    - Override list → split (`; , \n \r`) + trim + distinct + basic email validation (mirror `NotificationAdminAlertService`); include owner when `IsRecipientOwnerIncluded`.
    - Owner fallback via `MembershipDbContext.UserBusinesses` where `IsOwner && IsActive` → `User.Email`.
    - Empty result → return none (runner skips + warns).
    - **Decision:** default single primary recipient; multi-recipient fan-out gated behind explicit config (confirm at build).
    - _Requirements: 7.1, 7.2, 7.3, 7.4_

- [ ] 5. Digest composers + HTML builders (Portal.Infrastructure)
  - [ ] 5.1 `DigestEmailBuilder` — static, self-contained HTML (no customer footer), MyChair-styled
    - `BuildOutstandingBalanceHtml(...)`, `BuildFinancialSnapshotHtml(...)`, subject helpers, all-clear + "this week at a glance" variants.
    - _Requirements: 3.1, 3.3, 4.1, 5.2, 10.4_
  - [ ] 5.2 `IDigestComposer` + `OutstandingBalanceDigestComposer`
    - Receivables aggregate (`GetKpiDataAsync`) + top-N invoices (`GetReceivablesAsync`) + payables (`GetUpcomingSupplierPayments...`); all-clear when zero+empty; currency from `BusinessProfile`.
    - Payables wording = "supplier payments coming due" (not "unpaid").
    - Returns prepared `OutboxMessage?` (null if no recipient).
    - _Requirements: 3.1–3.6, 5.1, 5.2_
  - [ ] 5.3 `FinancialSnapshotComposer`
    - 7-day `Custom` P&L (task 3.1) + to-date outstanding (`GetKpiDataAsync`); filter by `IncludedFiguresCsv` or default set; always composed.
    - _Requirements: 4.1–4.5, 5.1, 5.2_

- [ ] 6. Cycle key, dedup, and enqueue (Portal.Infrastructure)
  - [ ] 6.1 Cycle-key derivation → verbatim `OutboxMessage.CycleKey` (Review fix #5 — no hashing)
    - `"{assistantKey}:{ISOYear}-W{ISOWeek}"` computed in business-local time; stored exactly on `CycleKey`. `RelatedEntityType/Id` left NULL for digests.
    - _Requirements: 6.1_
  - [ ] 6.2 `IDigestEnqueuer` with insert-time cycle dedup (Review fix #5)
    - Guard `ExistsForCycleAsync(businessId, assistantId, cycleKey)` (exact match); final re-check inside insert (mirrors `NotificationProducer.InsertAsync`); failed cycles may re-enqueue.
    - Sets `Pending`, `ScheduledForUtc = now`, `MaxRetries` from options, `CycleKey` set.
    - _Requirements: 6.2, 6.3, 6.4_

- [ ] 7. `IScheduledDigestRunner` / `ScheduledDigestRunner` (Portal.Infrastructure)
  - [ ] 7.1 Enumerate digest assistants; load candidate businesses; filter by tenant-less plan check (Review fix #1)
    - Candidates = `PortalDbContext.BusinessPlans.Where(bp => bp.IsActive)`; filter each via `IPlanCheckService.IsModuleInPlanAsync(businessId, PortalModules.DigitalAssistants)` (task 3.4 — NO HttpContext).
    - Enabled = setting `IsEnabled ?? true` (absence = enabled).
    - _Requirements: 1.6, 8.1, 8.2, 10.2_
  - [ ] 7.2 Due calculation (day/time in business TZ, no back-fill)
    - Resolve Windows TZ via `NotificationTimeZones` from `Business.TimeZoneId`, fallback to `DefaultTimeZoneWindowsId`; default Monday 08:00; "at or past" the cycle's send moment; no missed-cycle back-fill.
    - _Requirements: 2.2, 2.3, 2.5_
  - [ ] 7.3 For each due business: resolve recipient → compose → enqueue (per-business try/catch)
    - One `OutboxMessage` per cycle (per recipient row if multi-recipient enabled).
    - _Requirements: 1.3, 2.4, 6.2_

- [ ] 8. `DigitalAssistantSchedulerBackgroundService` (Portal.Web)
  - [ ] 8.1 `BackgroundService` mirroring `NotificationDispatcherBackgroundService`
    - `Notifications:EnableScheduler` gate; `Task.Delay(SchedulerPollIntervalMinutes)` loop; scope-per-iteration; fatal wrapper try/catch; delegates to `IScheduledDigestRunner`.
    - _Requirements: 1.1, 1.4, 1.5_

- [ ] 9. Configuration + DI wiring (Portal.Web)
  - [ ] 9.1 Extend `NotificationOptions`: `EnableScheduler` (default true), `SchedulerPollIntervalMinutes` (default 15); reuse `DefaultTimeZoneWindowsId`
    - _Requirements: 1.1, 1.5, 2.2_
  - [ ] 9.2 Register runner, composers, recipient resolver, enqueuer (factory-lambda), and `AddHostedService<DigitalAssistantSchedulerBackgroundService>()` under `// --- Digital Assistants ---`
    - _Requirements: 1.1_

- [ ] 10. Checkpoint — backend build
  - Build solution (Infrastructure + Web); ensure the seed, entity/EF changes, query overloads, composers, builders, runner, scheduler, and DI compile with 0 errors.

- [ ] 11. Repository support for digest settings (Portal.Infrastructure)
  - [ ] 11.1 Extend `BusinessAssistantSettingRepository` `GetAsync`/`GetAllForBusinessAsync`/`UpsertAsync` for the five new columns
    - Add the columns to the SELECT lists AND the `MERGE` UPDATE + INSERT clauses; null-safe params; full table names; `catch (Exception ex)` rethrow.
    - _Requirements: 8.3_
  - [ ] 11.2 **Preserve-on-partial-write guard (Review fix #4)** — update EVERY writer to read-existing-then-merge
    - `AxPostToggleAssistant` and the save path already read `existing`; they MUST now also copy forward `SendDayOfWeek`, `SendTimeLocal`, `RecipientOverride`, `IsRecipientOwnerIncluded`, `IncludedFiguresCsv` (and existing working-hours/footer) so a toggle never NULLs a configured digest schedule/recipient/figures.
    - _Requirements: 8.4_

- [ ] 12. Business-facing UI — digest cards on `/Assistants` (Portal.Web)
  - [ ] 12.1 Extend `AssistantsIndexViewModel` + `AssistantsController.Index` to surface digest fields
    - Send day/time, recipient(s), owner-include, included figures, per-assistant kind (customer-facing vs scheduled-owner).
    - _Requirements: 9.2_
  - [ ] 12.2 Digest card markup (day-of-week + time control, recipient field, figures checklist for snapshot) matching the design system + layout standards
    - Reuse the existing card shell; swap working-hours control for day/time; reuse activity-log expander.
    - _Requirements: 9.2, 9.4_
  - [ ] 12.3 Extend the EXISTING `AxPostSaveAssistantSettings` + `SaveAssistantSettingsRequest` (Review fix #3 — do NOT add a second save endpoint)
    - Add optional digest fields (`SendDayOfWeek`, `SendTimeLocal`, `RecipientOverride`, `IsRecipientOwnerIncluded`, `IncludedFiguresCsv`) to the request; branch on assistant kind (customer-facing → working hours + footer; scheduled-owner → day/time/recipient/figures). Validate day/time + recipient email format. Preserve-on-partial-write (task 11.2). BlockUI → request → BlockUI hide → SweetAlert2; `Json(new { success, message })`.
    - _Requirements: 9.3, 8.4_
  - [ ] 12.4 Reuse `AxPostToggleAssistant` (quick op → reload) and `AxGetAssistantLog` (activity log) for digests
    - `AxPostToggleAssistant` must preserve the new columns (task 11.2).
    - _Requirements: 9.3, 9.4, 8.4_

- [ ] 13. Final checkpoint — build + manual verification
  - Build, verify 0 errors.
  - Verify: enabling a digest with a near-future business-local day/time enqueues exactly one outbox row (right recipient/subject/body, `CycleKey` set); the dispatcher sends it; the activity log shows it. Quiet business → all-clear still sent. Disabled → no row. Override recipient → sent to the accountant; owner-include → owner CC'd on the same row. Idempotency: second poll in the same week → no duplicate (exact `CycleKey` match); failed cycle → re-enqueue allowed. Non-Professional plan → nothing enqueued (tenant-less plan check). Payables section reads "coming due," receivables numbers match Revenue Control.
  - **Regression (Review fix #4):** configure a digest schedule/recipient, then toggle it off and on → the schedule/recipient/figures are PRESERVED (not NULLed).
  - **Regression (Thank-You):** Thank-You still enqueues/sends normally; its outbox rows have `CycleKey` NULL and use the unchanged `RelatedEntityType/Id` dedup.
  - Produce a manual test-scenarios doc (like `digital-assistants-testing.md`) under `.kiro/docs/scenarios/`.

## Notes

- Reuse `NotificationDispatcherBackgroundService` / `PaymentReminderBackgroundService` as the
  scheduler template (BackgroundService + Task.Delay + scope-per-iteration + resilient
  try/catch). No new alerting — the existing dispatcher + admin failure threshold cover
  delivery failures.
- Composers + `DigestEmailBuilder` live in Infrastructure so the rendered body is stored
  self-contained on the outbox row (dispatcher stays dumb; the log shows exactly what was sent).
- `catch (Exception ex)` everywhere; repositories rethrow; scheduler fails safe per business.
- Payables are "coming due," never "unpaid" — `Purchase` has no paid-state (only `IsCancelled`).
- Migrations: next FREE sequential numbers; `USE [Portal]` header; idempotent guarded DDL.
- Digests default ENABLED via the absence-of-row / `IsEnabled ?? true` convention.
- **Dedup is exact via `OutboxMessage.CycleKey`** (Review fix #5) — NOT an int hash. Thank-You's
  `RelatedEntityType/Id` dedup path is untouched.
- **Preserve-on-partial-write** (Review fix #4): every setting writer reads-existing-then-merges
  so a toggle can't erase a configured schedule/recipient.
- **Tenant-less everything** (Review fix #1, #2): the scheduler has no `HttpContext` — plan check
  and every composer query take explicit `businessId`; audited, not assumed.

## Resolved review findings (was: open decision)

- **Recipient cardinality — DECIDED:** v1 sends to a single primary recipient (first override
  address, else owner), with the owner added as a **CC on the same outbox row** when "include
  owner" is set — exactly one row per cycle. True multi-row fan-out is deferred (would require a
  recipient discriminator in the cycle dedup). Requirements reworded to match; no longer open.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1","1.2","1.3","1.4"] },
    { "id": 1, "tasks": ["2.1","2.2","2.3","2.4","2.5"] },
    { "id": 2, "tasks": ["3.1","3.2","3.3","3.4","4.1"] },
    { "id": 3, "tasks": ["5.1","5.2","5.3","6.1","6.2"] },
    { "id": 4, "tasks": ["7.1","7.2","7.3"] },
    { "id": 5, "tasks": ["8.1","9.1","9.2"] },
    { "id": 6, "tasks": ["10"] },
    { "id": 7, "tasks": ["11.1","11.2","12.1","12.2","12.3","12.4"] },
    { "id": 8, "tasks": ["13"] }
  ]
}
```
