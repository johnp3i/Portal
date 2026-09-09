# Design: Digital Assistants — Owner Alerts, Phase 4a

## Overview

Phase 4a adds two owner-facing assistants that build directly on existing infrastructure:

- **Daily Brief** — a *scheduled* (Category B) assistant reusing the Group 3 scheduled engine,
  generalized from weekly-only to also support a **daily** cadence, and reusing the attention-
  item logic already written for the Weekly Financial Snapshot (extracted into a shared service).
- **New Payment Received** — an *event* (Category A) producer mirroring the Thank-You assistant,
  but owner-facing, hooked into all three payment-recording paths.

The guiding principle is maximum reuse: the outbox spine, dispatcher, scheduled runner, per-
business settings, plan gating, timezone resolution, and all the attention-item query helpers
already exist. This phase mostly *generalizes* the engine (weekly → weekly+daily), *extracts* a
shared builder, and *mirrors* one producer.

## Part A — New Payment Received (event producer)

### Producer

Add to `INotificationProducer` / `NotificationProducer`, mirroring `PrepareThankYouAsync`:

```csharp
Task<OutboxMessage?> PrepareNewPaymentAsync(
    int businessId,
    string? invoiceNumber,   // null for global/unallocated payments
    string? customerName,    // best-effort context (owner already knows their customers)
    decimal amount);
```

Gating pipeline mirrors Thank-You:
1. Resolve the assistant by a new key `new_payment_received` (const `NewPaymentKey`); null → skip.
2. Per-business enabled? (`BusinessAssistantSettingRepository.GetAsync`; absence = enabled) → skip if off.
3. **Resolve the owner email** via the Membership DB (`UserBusiness.IsOwner && IsActive`). This
   is the key difference from Thank-You (which uses the customer email). If no owner email →
   return null (Req 5.5). **Decision:** introduce a shared `IOwnerEmailResolver.ResolveAsync(businessId)`
   in Infrastructure and have BOTH the producer and `DigestRecipientResolver` use it — single
   source of truth for "the owner email."
4. Render subject + owner-facing body (no branded footer) via a new
   `AssistantEmailBuilder.BuildNewPaymentHtml(...)`; `ScheduledForUtc = now`.
5. Return the `OutboxMessage` (Pending, owner `RecipientEmail`).

> **No entity-dedup for New Payment (revised — Review fix #1).** The original design keyed dedup
> on `paymentId`. That was wrong on two counts, confirmed against the code:
> - **It doesn't prevent the real duplicate.** A `paymentId` generated inside *this* transaction
>   can never collide with another transaction's row, so `InsertAsync`'s re-check is dead weight.
>   The actual duplicate risk — a retried Stripe webhook — would create a *new* payment with a
>   *new* id, which a paymentId-keyed dedup would NOT catch.
> - **The real risk is already handled upstream.** The Stripe path is idempotent twice over:
>   `WebhookProcessingService.ExistsByEventIdAsync(eventId)` short-circuits a re-delivered event,
>   and `StripeConnectService.HandleCheckoutCompletedAsync` returns "Already processed" when
>   `checkoutSession.Status == "completed"` — **before any payment is inserted**. So a retried
>   webhook never reaches the payment insert, and the alert (enqueued in the same transaction)
>   is protected by inheritance. The manual and global paths are deliberate, non-retried user
>   actions.
>
> Therefore New Payment sets **no** `RelatedEntityType`/`RelatedEntityId` and does no
> entity-dedup. `PrepareNewPaymentAsync` runs entirely before the transaction (nothing needs the
> post-insert id), and `InsertAsync` simply appends the row — no id-stamping-inside-txn trick.
> This also removes the fragile ordering wrinkle from the original design.

> **Intentional working-hours bypass (Review fix #4).** Unlike every other producer, New Payment
> sets `ScheduledForUtc = now` and does **not** call `ScheduleResolver.ResolveScheduledForUtc`.
> This is deliberate — it's an internal owner alert that should arrive promptly, not be deferred
> to business hours. Documented here so a future maintainer does not "restore" the resolver.

`InsertAsync` is reused unchanged (with no `RelatedEntityId`, its dedup re-check is simply a
no-op for these rows).

### Hook sites — all three payment paths

Each mirrors the existing Thank-You wiring, but simpler (no id to stamp): prepare fully before
the txn, insert inside it.

1. **`PaymentService.RecordPaymentAsync`** — after the existing `PrepareThankYouAsync(...)`,
   also `PrepareNewPaymentAsync(businessId, invoice.InvoiceNumber, customerName, dto.Amount)`;
   inside the txn, `InsertAsync(newPaymentMsg)` alongside the Thank-You insert.
2. **`StripeConnectService.HandleCheckoutCompletedAsync`** — same pattern alongside its Thank-You.
3. **`RecordGlobalPaymentAsync`** — new hook (no template today). This path inserts **one parent
   payment** (`parentId`) and the allocation engine then creates N child rows per invoice.
   **Exactly ONE alert fires, for the parent** (Review fix #2): prepare with
   `invoiceNumber = null`, `customerName` from `dto.CustomerId`, `amount = dto.Amount`, and
   insert once inside the existing transaction **after** the parent insert (the children are an
   allocation detail, not separate "payments received" from the owner's perspective). Never one
   alert per allocation.

Because all three already open a transaction around the payment insert, the alert insert joins
that transaction (transactional outbox). Per Req 6.5, `PrepareNewPaymentAsync` and the insert
are best-effort relative to the payment: a failure preparing/inserting the alert must NOT roll
back the payment (guard it so the payment still commits).

### AssistantType seed

New seed row (migration), explicit-Id `MERGE` like 193/198:

| Id | Key | Name | RecipientKind | IsCustomerFacing |
|----|-----|------|---------------|------------------|
| 4 | `new_payment_received` | "New Payment Received" | `PortalUser` | 0 |

(Id 4 — 1=thank_you, 2=weekly_outstanding_digest, 3=weekly_financial_snapshot. The Daily Brief
takes id 5.)

> **Seed must be verified to land (Review fix #5).** The idempotent `MERGE ... ON target.Id =
> source.Id WHEN NOT MATCHED THEN INSERT` does NOT "fail loudly" if id 4 or 5 is already taken —
> `WHEN NOT MATCHED` simply inserts nothing (a silent no-op), and the assistant would never
> appear. (A different row with the same *Key* would instead fail on the unique Key constraint.)
> So the migration SHALL, after the MERGE, `SELECT`/`PRINT` a count of the expected keys and the
> script SHALL be run/checked so a no-op is visible, rather than assuming the seed landed.
> Confirm ids 4 and 5 are free against the current `[notification].[AssistantType]` before
> running.

## Part B — Daily Brief (scheduled owner assistant)

### Engine generalization: weekly + daily cadence

The `ScheduledDigestRunner` is generalized minimally:

1. **Cadence per assistant.** Introduce a small notion of cadence keyed by assistant. Simplest:
   a static set / lookup in the runner marking which assistant keys are **daily** vs **weekly**
   (the Daily Brief key is daily; the two digests are weekly). Keep it explicit rather than a new
   DB column, since cadence is intrinsic to the assistant, not per-business.
2. **`DigestCycleKey.Daily(assistantKey, businessLocalNow)`** → `"{assistantKey}:{yyyy-MM-dd}"`
   (business-local date). Add alongside the existing `Weekly`.
3. **Due calculation.** Generalize `IsDue` to branch on cadence:
   - Weekly (unchanged): due when business-local ≥ this week's `SendDayOfWeek` + `SendTimeLocal`.
   - Daily (new): due when business-local time-of-day ≥ `SendTimeLocal` (default 08:00),
     ignoring day-of-week.
4. **Runner key list.** The runner's hard-coded `digestKeys` array gains the Daily Brief key (or
   derive the set from the registered `_composersByKey`). The rest of the loop — plan gate,
   timezone resolution, per-assistant setting, cycle-key dedup, compose, enqueue — is unchanged;
   it just calls `DigestCycleKey.Daily(...)` and the daily `IsDue` branch for the daily assistant.

No change to the outbox `CycleKey` column (the daily key is just a different string), no change
to the dedup mechanism (`ExistsForCycleAsync` works for any cycle-key string), and the weekly
digests are untouched.

### Shared attention-item builder (extraction)

Extract `FinancialSnapshotComposer.BuildAttentionItemsAsync` into a shared, injectable service:

```csharp
public interface IAttentionItemBuilder
{
    Task<List<AttentionItem>> BuildAsync(int businessId, string currencySymbol, DateOnly today);
}
```

- It depends only on `IDashboardService` (`GetKpiDataAsync`, `GetOldestOverdueInvoiceAsync`,
  `GetUpcomingSupplierPaymentsAsync`), `QuotationRepository.CountAwaitingResponseAsync`,
  `VatSubmissionPeriodRepository.GetCoveringUnsubmittedPeriodAsync`, and `NotificationOptions`
  (`VatFilingOffsetDays`, `VatDeadlineNoticeDays`) — all tenant-less.
- `FinancialSnapshotComposer` is refactored to call `IAttentionItemBuilder.BuildAsync(...)`
  instead of its private method — **no behavioural change to the weekly Snapshot** (Req 2.2).
- The `DailyBriefComposer` calls the same builder.

> **KPI double-query — resolved to an overload (Review fix #6).** The Snapshot already calls
> `GetKpiDataAsync` for its figures and passed the DTO into its private attention method. If the
> extracted builder *always* re-fetches the KPI, the Snapshot would run **two** KPI queries per
> send. To avoid that regression, `IAttentionItemBuilder` takes the KPI as an **optional
> parameter**:
> ```csharp
> Task<List<AttentionItem>> BuildAsync(int businessId, string currencySymbol, DateOnly today,
>     DashboardKpiDto? kpi = null);   // when null, the builder fetches it itself
> ```
> The Snapshot passes the KPI it already loaded (no extra query — preserves current behaviour and
> query count, satisfying Req 2.2). The Daily Brief passes null and lets the builder fetch it
> (one query, since the brief has no separate figures section needing it). Clean for both.

### `DailyBriefComposer`

`DailyBriefComposer : DigestComposerBase, IDigestComposer`:
- `AssistantKey => DigestAssistantKeys.DailyBrief` (new const).
- `ComposeAsync`:
  1. Resolve recipient (reuse `IDigestRecipientResolver`); null → return null (skip).
  2. `today = business-local date` (the runner passes business-local now; derive the date).
  3. `items = await _attentionBuilder.BuildAsync(businessId, currencySymbol, today)`.
  4. **If `items` is empty → return null** (Req 3.2 "skip when empty" — no email that day). This
     is the key behavioural difference from the digests.
  5. Otherwise render an owner-facing "Your daily brief" email via a new
     `DigestEmailBuilder.BuildDailyBriefHtml(businessName, currencySymbol, items)` — reusing the
     same attention-item rendering (urgent = red) already in the Snapshot, with a today-focused
     heading and no figures/glance block.
  6. Return the `OutboxMessage` (Pending, `CycleKey` = the daily key, owner recipient).

> **Disambiguating the two "skip" reasons (revised — Review fix #3).** The runner currently
> treats a null composer result as skip and logs a **warning**: "Digest not enqueued (no
> resolvable recipient)." For the Daily Brief, null would mean two very different things — a real
> problem ("no owner email") vs the normal, expected quiet day ("nothing to report"). Logging
> every quiet day as an unresolvable-recipient warning is misleading and noisy. **Fix:** the
> composer distinguishes the two internally and logs its own message —
> - No recipient resolved → the composer logs a **warning** ("Daily Brief: no recipient for
>   BusinessId=X") and returns null.
> - Empty attention items → the composer logs an **information** line ("Daily Brief: nothing to
>   report for BusinessId=X today") and returns null.
> and the runner's own null-path log is **downgraded to Debug/Information** (or the composer
> having already logged, the runner stays silent). Net: quiet days produce no warning noise; a
> genuinely missing recipient still surfaces. This is a small change to the runner's null-branch
> logging plus the composer owning its own reason-specific logs.

### AssistantType seed + settings

New seed row: id 5, key `daily_brief`, "Daily Brief", `PortalUser`, not customer-facing.
Reuses the existing `BusinessAssistantSetting` fields — `SendTimeLocal` drives the daily send
moment; `SendDayOfWeek` is ignored for a daily assistant; `RecipientOverride` /
`IsRecipientOwnerIncluded` work as for the digests. No new settings column.

## UI (`/Assistants`)

Both assistants appear as cards. Two card variants already exist (customer-facing working-hours,
scheduled-digest day+time+recipient+figures). Add handling so:
- **Daily Brief** renders like a digest card but with **send time only** (no day-of-week
  dropdown, no figures checklist) + recipient + owner-CC toggle. This is a small view branch:
  a scheduled assistant that is *daily* shows time-only.
- **New Payment Received** is an event assistant with no schedule — its card shows just the
  on/off toggle, recipient (owner, with optional override), and the activity-log expander (like
  a simplified Thank-You card without working hours/footer).

Controller: extend the `Index` projection to mark cadence (event / daily / weekly) so the view
picks the right control set; the save endpoint (`AxPostSaveAssistantSettings`) already branches
on assistant kind — extend it to accept the daily (time-only) and event (recipient-only) shapes,
preserving the preserve-on-partial-write rule already in place.

## Configuration + DI

- No new `NotificationOptions` keys required (daily cadence reuses `SendTimeLocal`; VAT config
  already exists). `SchedulerPollIntervalMinutes` already governs how promptly a daily send
  fires after its send time — 15 min default is fine.
- Register in Program.cs (under the Digital Assistants block): `IAttentionItemBuilder` →
  `AttentionItemBuilder`; `IOwnerEmailResolver` → `OwnerEmailResolver`; `IDigestComposer` →
  `DailyBriefComposer` (joins the existing composer set the runner picks up).
- `INotificationProducer` is already registered; the new `PrepareNewPaymentAsync` needs the
  producer to reach the owner resolver (inject `IOwnerEmailResolver`).

## Error handling

- Daily Brief: per-business try/catch in the runner (already there); composer returns null to
  skip cleanly on empty or unresolved-recipient.
- New Payment Received: prepare is best-effort relative to the payment (Req 6.5) — a failure to
  prepare/insert the alert must not roll back the payment. Match the existing Thank-You posture
  (the prepared message is inserted in-txn, but the prepare itself is guarded).
- Delivery failures/retries/alerting: handled by the existing dispatcher — no new alerting.

## Testing strategy

Unit:
- Daily `IsDue`: fires at/after send time, once per day, no back-fill; weekly unaffected.
- `DigestCycleKey.Daily`: stable per business-local day; dedup blocks a second same-day send.
- `IAttentionItemBuilder`: same output as the Snapshot's previous private method for a fixture
  (regression), and each item omitted when nothing to report.
- Daily Brief composer: empty items → null (no enqueue); non-empty → owner message with items.
- New Payment producer: owner recipient resolved; dedup on paymentId; disabled → null; no owner
  email → null.

Manual (scenarios doc): daily brief on a business with overdue items sends once/day and skips on
a clean day; new-payment alert fires for manual, Stripe, and global payments; disabling each
suppresses it; plan gating; preserve-on-partial-write on the new cards.

## Deliberate decisions

1. **Cadence is intrinsic to the assistant, not a per-business DB column** — a static
   daily/weekly lookup in the runner keeps the schema unchanged.
2. **New Payment does NO entity-dedup** — the Stripe path is already idempotent upstream
   (webhook event id + `checkoutSession.Status == "completed"`, both before the payment insert),
   and the manual/global paths are deliberate user actions. A `paymentId`-keyed dedup would have
   been dead weight and wouldn't catch the real (retried-webhook) risk anyway.
3. **One alert per global payment (the parent), never per allocation** — the child allocation
   rows are a detail, not separate "payments received."
4. **Extract the attention-item builder** — one source of truth for Snapshot + Daily Brief;
   avoids drift. KPI passed in as an optional param so the Snapshot doesn't double-query.
5. **Daily Brief skips empty days via composer null** — reuses the runner's null-skip, with the
   composer logging reason-specific messages so quiet days aren't logged as recipient errors.
6. **`IOwnerEmailResolver` shared by producer + digest resolver** — single definition of "the
   owner email."
7. **New Payment sends immediately** (intentional working-hours-resolver bypass) — internal
   owner alert; revisit only if owners ask for quiet hours.

## Review revisions summary

This design was revised after a code-grounded review. The seven findings and resolutions:

1. **New Payment dedup** — removed the `paymentId` entity-dedup entirely. Confirmed the Stripe
   path is idempotent upstream (event-id + session-status checks before the payment insert), so
   the alert inherits that protection; manual/global are deliberate actions. This also removed
   the fragile "stamp id inside the txn" ordering trick.
2. **Global payment** — explicitly one alert for the **parent** payment (`dto.Amount`), fired
   after the parent insert, never one per allocation child.
3. **Daily Brief empty vs no-recipient** — the composer logs reason-specific messages (info for
   quiet day, warning for missing recipient) and the runner's null-branch warning is downgraded,
   so quiet days don't generate false recipient warnings.
4. **Working-hours bypass** — documented that New Payment intentionally skips
   `ResolveScheduledForUtc` (`ScheduledForUtc = now`).
5. **Seed landing** — the MERGE no-ops silently on a taken id; the migration verifies the seed
   landed (post-MERGE count/PRINT) rather than assuming.
6. **Attention-builder KPI double-query** — the extracted builder takes the KPI as an optional
   param; the Snapshot passes its already-loaded KPI (no second query), the Daily Brief passes
   null.
7. **"Today-focused" wording** — dropped; the Daily Brief uses the **same** attention-item set
   as the Snapshot (via the shared builder), just daily. Consistency between the two emails is
   more valuable than a bespoke daily filter (requirements reworded to match).

## Cross-references

- Catalog: `.kiro/docs/features/digital-assistants-catalog.md` (Group 4).
- Spine: `.kiro/specs/digital-assistants-notifications/`.
- Scheduled engine + digests: `.kiro/specs/digital-assistants-scheduled-digests/`.
- Requirements: `./requirements.md`.
