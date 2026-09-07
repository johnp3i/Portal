# Design: Digital Assistants — Scheduled-Scan Engine + Weekly Owner Digests (Group 3)

## Overview

This design builds the **scheduled-scan / digest engine** — the second structural category of
Digital Assistant (Category B) — and the first two assistants on top of it: the **Weekly
Outstanding Balance Digest** and the **Weekly Financial Snapshot**. Both are owner-facing,
reuse the Phase 1 notification outbox spine for delivery, and are the foundation later
scheduled assistants depend on.

The core idea: a new sibling hosted service (`DigitalAssistantSchedulerBackgroundService`)
wakes on an interval, finds businesses whose digest is **due this cycle** (per their
configured day/time in their time zone), **composes** the digest from existing query services,
and **enqueues** one outbox row per digest via the Phase 1 producer. The existing dispatcher
then sends it. The scheduler decides *what and when*; the dispatcher does the *sending* — a
clean separation confirmed as a product decision.

**Reuse-first.** Almost every data query the digests need already exists and is reused rather
than reimplemented:

| Need | Reused component |
|------|------------------|
| Outstanding receivables (aggregate) | `DashboardService.GetKpiDataAsync` (outstanding, overdue, paid-this-month, partially-paid) |
| Outstanding receivables (per-invoice list) | `ReceivablesQueryService.GetReceivablesAsync` |
| Upcoming payables | `DashboardService.GetUpcomingSupplierPaymentsAsync` (COALESCE(TargetPaymentDate, SupplierDueDate)) |
| Period P&L figures | `PnlService.GetSummaryAsync` with `PnlPeriodType.Custom` (7-day range) |
| Owner email | Membership DB `UserBusiness` where `IsOwner && IsActive` → `User.Email` (the `InvoiceEmailService` pattern) |
| Currency / business name | `BusinessProfile.CurrencySymbol`, `Business.Name` |
| Delivery | Phase 1 outbox (`OutboxMessage`) + `NotificationDispatcherBackgroundService` |
| Enqueue (pattern) | `NotificationProducer` Prepare/Insert split (insert-time dedup re-check) |
| Cycle dedup (NEW) | New `OutboxMessage.CycleKey` column + `ExistsForCycleAsync` (exact match) — see Review fix #5 |
| Scheduler shape | `NotificationDispatcherBackgroundService` / `PaymentReminderBackgroundService` |

## Architecture

```
DigitalAssistantSchedulerBackgroundService (Portal.Web, hosted)   ← NEW
  every N minutes, scope-per-iteration:
    for each digest assistant (weekly_outstanding_digest, weekly_financial_snapshot):
      for each business with digest enabled AND plan includes digital_assistants:
        is it due this cycle? (configured day/time in business TZ, not already enqueued)
          │ yes
          ▼
        IDigestComposer.ComposeAsync(businessId, assistant, cycleKey)   ← NEW
          ├─ resolve recipient (override ?? owner email)   [Membership UserBusiness]
          ├─ gather data (reused query services)
          ├─ render HTML (DigestEmailBuilder, Infrastructure)   ← NEW
          └─ return a prepared OutboxMessage (RelatedEntityType="Digest", RelatedEntityId=cycleKey hash)
          │
          ▼
        NotificationProducer-style enqueue  →  [notification].[OutboxMessage] (Pending)   [REUSED spine]
                                                     │
                                                     ▼
                              NotificationDispatcherBackgroundService  →  send  →  Sent/Failed   [REUSED]
```

The scheduler lives in **Portal.Web** (like the other hosted services, so it can reach
`IEmailSender`-adjacent infra and all scoped services). The **composers** and **HTML builders**
live in **Portal.Infrastructure** (no Web dependency), so the enqueue path stays self-contained
and the rendered body is stored on the outbox row exactly like Thank-You.

## Data model changes

### Extend `[notification].[BusinessAssistantSetting]`

The existing per-business setting row already holds `IsEnabled`, `IsBrandingFooterEnabled`, and
`WorkingHoursStart/End`. Scheduled digests need a schedule, a recipient, and (snapshot)
figure selection. Add nullable columns so existing rows and the customer-facing assistants are
unaffected:

| Column | Type | Notes |
|--------|------|-------|
| `SendDayOfWeek` | `TINYINT NULL` | 0=Sunday … 6=Saturday. NULL → default (Monday=1). Used by scheduled digests only. |
| `SendTimeLocal` | `TIME NULL` | Send time in business-local time. NULL → default 08:00. |
| `RecipientOverride` | `NVARCHAR(1000) NULL` | Delimited email list; NULL → owner. |
| `IsRecipientOwnerIncluded` | `BIT NOT NULL DEFAULT 1` | When an override is set, also send to owner? |
| `IncludedFiguresCsv` | `NVARCHAR(400) NULL` | Snapshot figure keys (CSV). NULL → default set. |

All columns are additive and nullable/defaulted, so the Thank-You assistant's rows and code are
untouched. The entity `BusinessAssistantSetting` gains the matching properties; EF config adds
them with defaults.

> **Why reuse `BusinessAssistantSetting` rather than a new table?** It already models
> "one row per (business, assistant)" with an `IsEnabled` toggle and the absence-of-row =
> defaults semantics. Digests are just assistants with a different scheduling shape. A separate
> table would fork the settings model unnecessarily.

### Outbox schema change — `CycleKey` (revised, Review fix #5 & #8)

Add to `[notification].[OutboxMessage]`:

| Column | Type | Notes |
|--------|------|-------|
| `CycleKey` | `NVARCHAR(80) NULL` | Exact cycle identifier for cycle-based producers (e.g. `weekly_financial_snapshot:2026-W35`). NULL for event-driven producers (Thank-You). |

Plus a filtered index supporting the exact-match dedup lookup (Review fix #8 — none of the
Phase 1 indexes cover this predicate):

```sql
CREATE INDEX IX_OutboxMessage_Cycle
    ON [notification].[OutboxMessage] ([BusinessId], [AssistantTypeId], [CycleKey])
    WHERE [CycleKey] IS NOT NULL;
```

The dedup for cycle-based assistants becomes an exact match on
`(BusinessId, AssistantTypeId, CycleKey)` excluding Failed rows — a new repository method
`ExistsForCycleAsync(businessId, assistantTypeId, cycleKey)` alongside the existing
`ExistsForRelatedEntityAsync`. Thank-You's dedup path is untouched.

### Seed two `[notification].[AssistantType]` rows (revised, Review fix #6)

Mirror migration 193's explicit-Id idempotent `MERGE` seed. Id 1 = thank_you (existing). The
digests take **Ids 2 and 3** — we do **not** pre-reserve an id for Quotation Follow-Up, because
the corrected catalog sequences Quotation Follow-Up *after* Group 3, so it will take the next
free id (4) when it is built. Ids are explicit (not identity):

| Id | Key | Name | RecipientKind | IsCustomerFacing |
|----|-----|------|---------------|------------------|
| 2 | `weekly_outstanding_digest` | "Weekly Outstanding Balance Digest" | `PortalUser` | 0 |
| 3 | `weekly_financial_snapshot` | "Weekly Financial Snapshot" | `PortalUser` | 0 |

(The `MERGE` is keyed on `Id`; if any of these ids are ever found already taken at migration
time, the migration fails loudly rather than silently colliding — assign the next free ids and
update the code constants. No "shift accordingly" ambiguity: the seed is authoritative.)

### No new status tables

Digests reuse `OutboxMessageStatusType` (Pending/Sent/Failed) unchanged.

## Components and interfaces

### 1. `DigitalAssistantSchedulerBackgroundService` (Portal.Web) — NEW

`BackgroundService`, registered via `AddHostedService`, modelled directly on
`NotificationDispatcherBackgroundService`:

- Config gate `Notifications:EnableScheduler` (default true); returns immediately when off.
- `Task.Delay(interval)` loop; interval from `Notifications:SchedulerPollIntervalMinutes`
  (default e.g. 15 min — digests are day/time-grained, so sub-hour polling is plenty).
- `IServiceScopeFactory` scope per iteration.
- Per-business, per-assistant **try/catch** so one business's failure doesn't stop the run;
  fatal wrapper try/catch so the service survives to the next interval (Req 1.4).
- Delegates the actual "is it due? compose? enqueue?" to an injected
  `IScheduledDigestRunner` (below), keeping the hosted service thin (the same way the
  dispatcher delegates to the repo/sender).

### 2. `IScheduledDigestRunner` / `ScheduledDigestRunner` (Portal.Infrastructure) — NEW

The orchestrator invoked each poll. Responsibilities:

- Enumerate the two digest assistant types.
- For each, load the set of businesses that (a) have the `digital_assistants` module and
  (b) have the digest enabled (setting `IsEnabled ?? true`).
- For each business, compute **is it due this cycle** (see "Due calculation").
- If due, call the appropriate `IDigestComposer`, then enqueue via the producer path with the
  cycle-key dedup guard.

> **Tenant-less plan gating (revised — Review fix #1).** Requirement 10.2 says the scheduler
> only enqueues for businesses whose plan includes `digital_assistants`. The existing
> entitlement services — `IPlanCheckService.GetPlanModulesAsync()` /
> `ISubscriptionPlanService.GetAccessAsync()` — resolve the business from
> `ICurrentTenantService`/`HttpContext` and cache per-request. **The scheduler has no
> `HttpContext`**, so neither can be called as-is; doing so would read the wrong tenant or
> throw. This design therefore **adds tenant-less overloads** to `IPlanCheckService`:
> `Task<List<string>> GetPlanModulesAsync(int businessId)` and
> `Task<bool> IsModuleInPlanAsync(int businessId, string module)`. These reuse the *exact same
> query* the current method runs — `BusinessPlans.Where(bp => bp.BusinessId == id && bp.IsActive)
> .SelectMany(bp => bp.Plan.PlanFeatures).Where(pf => pf.IsIncluded).Select(pf => pf.ModuleName)`
> — but take `businessId` explicitly instead of from the tenant service, and skip the
> HttpContext cache (harmless when null). The existing HttpContext-based methods delegate to
> the new overloads for a single source of truth. The runner enumerates active businesses and
> filters with `IsModuleInPlanAsync(businessId, PortalModules.DigitalAssistants)`.
>
> **How the runner gets the business list:** there is no tenant-less "all businesses with an
> active plan" query today. The runner obtains candidate businesses from
> `PortalDbContext.BusinessPlans.Where(bp => bp.IsActive)` (active-plan businesses), then
> applies the module filter per business. This keeps the scan bounded to paying businesses.

```csharp
public interface IScheduledDigestRunner
{
    Task RunAsync(CancellationToken ct);
}
```

### 3. Due calculation + cycle key

- **Cycle key (weekly):** `"{assistantKey}:{ISOYear}-W{ISOWeek}"` derived from the current
  date in the business's time zone. Deterministic and stable within a week (Req 6.1). Stored
  verbatim in the new `OutboxMessage.CycleKey` column (no hashing — Review fix #5).
- **Due test:** the business is due this cycle when the current business-local time is at or
  past the configured `SendDayOfWeek` + `SendTimeLocal` for the current cycle, AND no
  non-failed outbox row already exists for `(business, assistant, cycleKey)` via
  `ExistsForCycleAsync`.
- **Timezone:** resolve the business's Windows time zone id from `NotificationTimeZones` via
  `Business.TimeZoneId` (same lookup the Phase 1 producer uses), fall back to
  `Notifications:DefaultTimeZoneWindowsId` when null (Req 2.2).
- **No back-fill:** if a business is newly enabled after this cycle's send moment already
  passed, the cycle key for the current week will simply never trigger (the due test is "at or
  past the send moment" but the guard doesn't back-fill missed prior weeks — each poll only
  considers the current cycle) (Req 2.5).

**Idempotency via a dedicated `CycleKey` column (revised — see Review fix #5):** the Phase 1
dedup key is `(BusinessId, AssistantTypeId, RelatedEntityType, RelatedEntityId:int)`, which
works for Thank-You because an invoice already *has* an int id. A cycle has no natural int id,
and hashing the week string into a 32-bit int is **not** collision-safe at the relevant scope:
the dedup is scoped to `(businessId, assistantId, RelatedEntityId)`, so a collision only needs
two *weeks of the same digest for the same business* to hash equal — by the birthday bound
that is plausible within a few years per business, and the consequence is a **silently dropped
weekly digest**. That directly contradicts the "always-send heartbeat" principle (a missing
digest is supposed to signal breakage, not be caused by our own hash collision).

**Decision:** add a dedicated nullable **`CycleKey NVARCHAR(80) NULL`** column to
`[notification].[OutboxMessage]` plus an idempotency-scoped index (see "Outbox schema
change"). Cycle-based producers set `CycleKey` to the exact string
`"{assistantKey}:{ISOYear}-W{ISOWeek}"`; dedup is an **exact string match**, not a hash.
Event-driven producers (Thank-You) leave `CycleKey` NULL and keep using the existing
`RelatedEntityType/Id` dedup unchanged. This makes the dedup exact, is the first cycle-based
producer's responsibility to establish, and is directly reused by every later cycle-based
assistant (Recurring Invoices, Payroll). The final check happens at insert time inside the
enqueue (mirroring `NotificationProducer.InsertAsync`) to close the concurrent-pass race
(Req 6.2, 6.3). A failed cycle does not block re-enqueue (Req 6.4).

### 4. `IDigestComposer` implementations (Portal.Infrastructure) — NEW

One composer per digest, each returning a prepared `OutboxMessage` (or null if it cannot send,
e.g. no recipient — Req 7.3):

```csharp
public interface IDigestComposer
{
    string AssistantKey { get; }
    Task<OutboxMessage?> ComposeAsync(int businessId, int assistantTypeId, string cycleKey, CancellationToken ct);
}
```

**`OutstandingBalanceDigestComposer`:**
- Receivables aggregate via `DashboardService.GetKpiDataAsync(businessId)` (outstanding total +
  count, overdue total + count, partially-paid).
- Top-N outstanding invoices via `ReceivablesQueryService.GetReceivablesAsync(businessId,
  page:1, pageSize:N)` (ordered by due date / amount).
- Payables via `DashboardService.GetUpcomingSupplierPaymentsAsync(businessId)` (already
  computes effective due date + overdue/due_soon/upcoming status). If the TOP-5 cap in that
  method is too tight for a digest, add a parameterised variant (default preserved).
- If receivables total == 0 AND payables list empty → mark as "all clear" (Req 3.5 / 5.2).

**`FinancialSnapshotComposer`:**
- Period figures via `PnlService.GetSummaryAsync` with `PnlPeriodType.Custom` and an explicit
  7-day range (this week) — revenue collected, COGS, OpEx, net.
- To-date outstanding via `GetKpiDataAsync`.
- Filter to the business's `IncludedFiguresCsv` (or default set) (Req 4.3).
- Always composed, even when quiet (Req 4.4 / 5.1).

> **Tenancy audit — not a rubber-stamp (revised — Review fix #2).** The scheduler has **no
> ambient tenant**; every query a composer calls must take `businessId` explicitly AND must not
> reach for `ICurrentTenantService`/`HttpContext` internally (directly or via a dependency).
> Taking a `businessId` parameter is necessary but NOT sufficient — a method could accept
> `businessId` and still read the current tenant for a secondary lookup (currency, Z-report
> profile flag, etc.). Therefore, before reuse, **each composer dependency must be audited by
> reading its implementation**, not assumed safe:
> - `DashboardService.GetKpiDataAsync(int businessId)` — takes businessId; **audit** the
>   currency/Z-report profile lookups it performs to confirm they too are keyed by the passed
>   businessId and not the tenant service.
> - `ReceivablesQueryService.GetReceivablesAsync(int businessId, ...)` — takes businessId;
>   **audit** for any ambient-tenant use.
> - `PnlService.GetSummaryAsync` — **known** to read the current tenant via its request;
>   **requires** an explicit-businessId overload/helper (e.g.
>   `ComputeSnapshotAsync(int businessId, DateOnly from, DateOnly to)`) reusing the private
>   compute methods.
> - `DashboardService.GetUpcomingSupplierPaymentsAsync(int businessId)` — takes businessId;
>   **audit**, and (Req 3.3) add a parameterised variant if the internal TOP-5 cap is too tight
>   for a digest.
>
> The audit is task 3.3 and is a genuine code check with a fallback: **any dependency found to
> touch ambient tenant/HttpContext gets the same explicit-businessId overload treatment as
> `PnlService`.** The composers must never be wired to a tenant-scoped method.

### 5. Recipient resolution (Portal.Infrastructure) — NEW helper

`IDigestRecipientResolver.ResolveAsync(int businessId, BusinessAssistantSetting? setting)`:
- If `RecipientOverride` set → parse the delimited list (reuse the
  `';', ',', '\n', '\r'` split + trim + distinct + basic email-format validation pattern from
  `NotificationAdminAlertService`), and include the owner too when `IsRecipientOwnerIncluded`.
- Else → owner email via `MembershipDbContext.UserBusinesses.Where(ub => ub.BusinessId == id &&
  ub.IsOwner && ub.IsActive).Select(ub => ub.User.Email)` (Req 7.1).
- Returns the resolved address(es); empty → composer returns null, runner logs a warning and
  does not enqueue (Req 7.3).

> **Recipient cardinality — decided (revised — Review fix #7).** The outbox row has a single
> `RecipientEmail`, and the cycle dedup is scoped `(business, assistant, CycleKey)` — one row
> per cycle. To avoid a dedup-vs-fan-out conflict, **v1 sends to a single primary recipient**:
> the override's first address if an override is set, else the owner. When an override is set
> AND "also send to owner" is on, the owner is added as a **CC/second address on the same
> outbox row** (if the sender supports multiple To/CC) rather than a second row — keeping
> exactly one row per cycle so the dedup and the heartbeat semantics stay clean. **True
> multi-row fan-out (one outbox row per address) is explicitly deferred**; if it is ever added,
> the cycle dedup must incorporate a recipient discriminator. Requirement 7 has been reworded
> to match this v1 decision (single primary + optional owner CC), so the spec is internally
> consistent.

### 6. `DigestEmailBuilder` (Portal.Infrastructure) — NEW

Static, self-contained HTML builders mirroring `AssistantEmailBuilder` (Thank-You). Produce
owner-facing, MyChair-styled emails with **no customer branded footer** (Req 10.4). Methods:

- `BuildOutstandingBalanceHtml(...)` — receivables summary + top-N table + payables table +
  all-clear variant.
- `BuildFinancialSnapshotHtml(...)` — selected figure tiles + all-clear/quiet variant + a
  small "this week at a glance" line (Req 5.2).
- Subjects: e.g. "Your weekly outstanding balance — {business}", "Your weekly financial
  snapshot — {business}".

### 7. Enqueue path

The runner enqueues the composed `OutboxMessage` through a small dedicated `IDigestEnqueuer`
that applies the same insert-time re-check pattern as `NotificationProducer.InsertAsync`, but
against the **cycle dedup** (`ExistsForCycleAsync(businessId, assistantId, cycleKey)`). The row
is `Pending`, `ScheduledForUtc = now` (digests send immediately once due — the *scheduling*
already happened in the due calc, so no working-hours deferral), `MaxRetries` from
`NotificationOptions`, `AssistantTypeId` = the digest's id, and `CycleKey` = the exact cycle
string (Review fix #5). `RelatedEntityType/Id` are left NULL for digests (they use `CycleKey`
for dedup, not the entity dedup).

## UI (Portal.Web) — `/Assistants` page

The digests appear as cards alongside Thank-You. Because they are scheduled + owner-facing, the
card differs from the customer-facing card:

- **Instead of** the working-hours "send only between" control → a **send day-of-week + time**
  control.
- **Recipient** field (default: "Business owner"; free-text override with validation) + an
  "also send to owner" toggle when an override is present.
- **Financial Snapshot only:** a checklist of **included figures**.
- Same on/off toggle, and the same activity-log expander (reusing
  `AxGetAssistantLog` — the outbox-backed log already keys off assistant id).

Controller actions on `AssistantsController` (all `AxPost`/`AxGet` per steering,
`catch (Exception ex)`, `Json(new { success, message })`):

- **Save (revised — Review fix #3).** The controller **already has** `AxPostSaveAssistantSettings`
  taking `SaveAssistantSettingsRequest` (working hours + footer). Rather than add a confusing
  second near-identical save endpoint, **extend `SaveAssistantSettingsRequest`** with the
  optional digest fields (`SendDayOfWeek`, `SendTimeLocal`, `RecipientOverride`,
  `IsRecipientOwnerIncluded`, `IncludedFiguresCsv`) and branch inside `AxPostSaveAssistantSettings`
  on the assistant's kind (customer-facing → validate/persist working hours + footer;
  scheduled-owner → validate/persist day/time/recipient/figures). One save endpoint, one JS
  contract, kind-aware handling.
- Reuse `AxPostToggleAssistant` for enable/disable (quick op → reload).
- Reuse `AxGetAssistantLog` for the activity log.

> **Preserve-on-partial-write (revised — Review fix #4).** Both `AxPostToggleAssistant` and the
> save path construct a fresh `BusinessAssistantSetting` and call `UpsertAsync`, whose `MERGE`
> overwrites **every** mapped column. Once the five new columns exist, the current toggle path —
> which carries only `IsEnabled` + footer + working hours — would **write NULL over a
> configured digest schedule/recipient/figures**, silently destroying the user's settings on a
> simple off/on toggle. This is a data-loss bug. The fix: **every writer must read the existing
> row first and carry forward the columns it isn't changing** (the controller already reads
> `existing` before building the setting — it must copy the new columns too), OR `UpsertAsync`
> must be split so partial updates only touch the columns they own. The design mandates the
> read-existing-then-merge approach across `AxPostToggleAssistant` and the save path, and this
> is an explicit acceptance criterion (Req 8.4) and task (11.1 / 12.3 / 12.4).

The card JS follows the workspace UI rules: BlockUI.show → fetch → BlockUI.hide → SweetAlert2,
antiforgery token, no native alerts.

## Configuration + DI

`NotificationOptions` gains: `EnableScheduler` (bool, default true),
`SchedulerPollIntervalMinutes` (int, default 15), and reuses the existing
`DefaultTimeZoneWindowsId`. Program.cs adds, under the `// --- Digital Assistants ---` block:

- `AddHostedService<DigitalAssistantSchedulerBackgroundService>()`
- factory-lambda registrations for `IScheduledDigestRunner`, the two `IDigestComposer`s,
  `IDigestRecipientResolver`, and `IDigestEnqueuer` (matching the existing registration
  convention).

## Error handling

- Scheduler: per-business + per-assistant try/catch (`catch (Exception ex)`), fatal wrapper
  try/catch; failures logged, never thrown out of the loop (Req 1.4).
- Composers/queries: `catch (Exception ex)` and rethrow (repository convention) — the runner
  catches at the per-business boundary.
- Recipient unresolved → log warning, skip enqueue, no crash (Req 7.3).
- Delivery failure/retry/permanent-failure → handled entirely by the **existing dispatcher**
  and the existing admin failure-alert threshold (no new alerting needed).

## Testing strategy

Unit tests (mirroring `Portal.Tests/Unit/...` patterns):
- Due calculation: day/time/timezone boundaries; newly-enabled-mid-week no back-fill; DST edge.
- Cycle-key stability + dedup: same cycle twice → one enqueue; failed cycle → re-enqueue
  allowed; concurrent-pass insert-time guard.
- Composer outputs: receivables/payables numbers match the reused query services for a fixture;
  all-clear variant when zero; figure filtering for the snapshot.
- Recipient resolution: override list parsing/validation; owner fallback; empty → null.

Manual verification (produce a scenarios doc like the Phase 1 one):
- Enable a digest, set day/time to "in 1 minute" business-local, confirm one outbox row with
  the right recipient/subject/body, then the dispatcher sends it and the activity log shows it.
- Quiet business → all-clear email still sent.
- Disabled digest → no row. Override recipient → sent to the accountant. Non-Professional plan
  → nothing enqueued.

## Deliberate decisions

1. **Sibling scheduler, not dispatcher extension** — confirmed product decision; keeps
   "decide what to enqueue" separate from "drain the outbox."
2. **Reuse `BusinessAssistantSetting`** for schedule/recipient/figures rather than a new table
   — digests are assistants with a scheduling shape, not a new domain.
3. **Cycle-key hash onto the existing int `RelatedEntityId`** for one-per-cycle dedup — reuses
   the proven Phase 1 dedup with no schema change; a dedicated `CycleKey` column is a clean
   future refinement if exactness is ever required.
4. **Composers/builders in Infrastructure** — the rendered body is stored self-contained on the
   outbox row (dispatcher stays dumb; the log shows exactly what was sent), exactly like
   Thank-You.
5. **Always-send heartbeat** — both digests send every cycle; empty → all-clear. The email
   doubles as proof the delivery chain is healthy. **Honest blind spot (Review fix #9):** a
   received digest proves the *dispatcher + SMTP* half is working, but if the **scheduler
   itself** is down, nothing is enqueued and nothing alerts — so a missing digest could mean
   either the scheduler failed or the whole chain failed. The heartbeat does NOT self-monitor
   the scheduler. A lightweight scheduler-liveness signal (e.g. a heartbeat timestamp the
   scheduler writes each successful pass, surfaced to admins) is a sensible follow-up but is
   **out of scope** here; we simply do not overclaim what the digest heartbeat proves.
6. **Payables framed as "coming due," not "unpaid"** — because `Purchase` has no paid-state;
   the wording is honest about what the data supports.
7. **Explicit-businessId query overloads** — the scheduler has no ambient tenant, so the
   composers must call explicit-businessId methods; audited per dependency, with an overload
   added wherever a method reaches for ambient tenant (`PnlService` at minimum).
8. **Tenant-less plan check** — new `IPlanCheckService` businessId overloads reusing the
   existing plan-modules query, so gating works without an `HttpContext`.
9. **Exact `CycleKey` dedup, not an int hash** — a dedicated outbox column + filtered index for
   exact, collision-free one-per-cycle dedup; the foundation every later cycle-based assistant
   reuses.
10. **One outbox row per cycle (single primary recipient + optional owner CC)** — keeps dedup
    and heartbeat clean; true multi-row fan-out deferred.

## Review revisions summary

This design was revised after a code-grounded review. The nine findings and their resolutions:

1. **Tenant-less plan gating** — added `IPlanCheckService.GetPlanModulesAsync(int businessId)` /
   `IsModuleInPlanAsync(int businessId, string)` overloads (the HttpContext-based methods can't
   run in a background service). Runner enumerates `BusinessPlans.IsActive` + filters by module.
2. **Tenancy audit made real** — task 3.3 is a genuine per-dependency code audit (not a
   rubber-stamp); any ambient-tenant use gets an explicit-businessId overload.
3. **Save endpoint reconciled** — extend the existing `AxPostSaveAssistantSettings` /
   `SaveAssistantSettingsRequest` and branch on assistant kind, rather than adding a second
   near-identical endpoint.
4. **Preserve-on-partial-write** — toggle and save paths must read-existing-then-merge so a
   toggle never NULLs a configured schedule/recipient/figures (data-loss bug); explicit AC + task.
5. **Exact `CycleKey` dedup** — replaced the int-hash-of-week (collision-prone, self-defeating
   for the heartbeat) with a dedicated `NVARCHAR` column + `ExistsForCycleAsync`.
6. **AssistantType ids** — digests take ids 2 and 3; no stale "reserved for Quotation Follow-Up"
   id; MERGE is authoritative and fails loudly on collision.
7. **Recipient cardinality decided** — v1 single primary + optional owner CC on one row;
   multi-row fan-out deferred; Requirement 7 reworded to match.
8. **Dedup index** — added filtered `IX_OutboxMessage_Cycle` for the new lookup.
9. **Heartbeat blind spot** — stated honestly (digest proves dispatcher/SMTP, not the
   scheduler); scheduler-liveness monitoring noted as out-of-scope follow-up.

## Cross-references

- Catalog & category model: `.kiro/docs/features/digital-assistants-catalog.md` (Group 3).
- Phase 1 spine: `.kiro/specs/digital-assistants-notifications/` (requirements/design/tasks).
- Requirements for this spec: `./requirements.md`.
