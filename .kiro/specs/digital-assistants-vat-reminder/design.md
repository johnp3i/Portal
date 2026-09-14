# Design: VAT Period Due Reminder Assistant (Group 4)

## Overview

A Category B (scheduled scan) owner-facing assistant that reuses the Group 3 engine end-to-end.
The only genuinely new pieces are: a **composer** (`VatReminderComposer`), a **period-scoped cycle
key**, a **public per-period VAT figure accessor**, a **per-business notice lead-time** column +
UI card shape, and an **AssistantType seed** (Id 6). Deadline detection reuses the exact logic
already in `AttentionItemBuilder`.

This design is grounded in the current code (verified): `ScheduledDigestRunner`, `IDigestComposer`
/ `DigestComposerBase`, `IDigestRecipientResolver` / `IOwnerEmailResolver`, `DigestCycleKey`,
`NotificationOutboxRepository.ExistsForCycleAsync`, `VatSubmissionPeriodRepository`,
`VatSubmissionService`, `BusinessAssistantSetting`, `AssistantsController`, and the 198/199/201
migration patterns.

---

## Architecture

```
ScheduledDigestRunner (existing, per poll)
   │  per plan+module-entitled business, business-local now
   │  assistant key ∈ { weekly_outstanding, weekly_snapshot, daily_brief, vat_period_due_reminder }  ← ADD KEY
   ▼
ProcessBusinessAssistantAsync
   │  enabled? IsDue(send-time)?  (daily-style: SendDayOfWeek ignored)
   │  cycleKey = VatReminderComposer decides period → "vat_period_due_reminder:period-{periodId}"  ← PERIOD-SCOPED
   │  ExistsForCycleAsync(business, assistant, cycleKey)?  → once-per-period dedup
   ▼
VatReminderComposer.ComposeAsync   (NEW, mirrors DailyBriefComposer)
   │  1. find due unsubmitted period (deadline in notice window) — null ⇒ skip (info log)
   │  2. resolve recipient (owner/override) — null ⇒ skip (warning log)
   │  3. compute approx net VAT (persisted-then-compute)
   │  4. build subject + owner HTML (deadline + period + estimate)
   ▼
OutboxMessage (Pending, CycleKey = period-scoped)  →  existing dispatcher  →  Sent/Retry/Failed
```

> **Cycle-key wrinkle (important):** the runner computes `cycleKey` *before* calling the composer
> (it needs the key for the pre-check `ExistsForCycleAsync`). But the period-scoped key depends on
> *which period* is due, which only the composer's scan knows. See "Cycle-key resolution" below —
> the resolution needs **no runner change**: the runner's pre-check simply checks a key that can
> never match for this assistant (a harmless no-op), and dedup is owned entirely by the composer's
> own in-compose `ExistsForCycleAsync` + the enqueuer.

> **⚠️ Enqueuer race (verified in code, applies to ALL scheduled assistants):**
> `DigestEnqueuer.EnqueueAsync` does a **check-then-insert** (`ExistsForCycleAsync` then
> `InsertAsync` as two separate statements), and the supporting index `IX_OutboxMessage_Cycle`
> (migration 200) is **NON-unique**. So two overlapping scheduler passes *could* both pass the check
> and double-insert. This is a **pre-existing** latent gap in the Group 3 engine, not introduced
> here, and low-risk for VAT (a single message, and the poll interval greatly exceeds a compose).
> **Recommended hardening (shared, do once):** make the cycle index **UNIQUE** filtered
> (`CREATE UNIQUE INDEX ... WHERE [CycleKey] IS NOT NULL`) so a duplicate insert fails at the DB and
> `EnqueueAsync` treats the unique-violation as "already enqueued" (return false). Tracked as a
> shared task; the VAT assistant does not depend on it but benefits from it.

---

## Components

### 1. AssistantType seed (Id 6) — migration

New migration (next free number, e.g. `202_SeedVatReminderAssistantType.sql`), `USE [Portal]`,
idempotent explicit-Id MERGE into `[notification].[AssistantType]`:

- Id `6`, Key `vat_period_due_reminder`, Name `VAT Period Due Reminder`,
  Description (owner-facing, e.g. "Reminds you before each VAT filing deadline, with an estimate of
  what you'll owe."), `RecipientKind = 'PortalUser'`, `IsCustomerFacing = 0`.
- Post-merge verification `PRINT` (1/1), matching 201's guard against a silent no-op on a taken Id.

Add the key constant: `DigestAssistantKeys.VatPeriodDueReminder = "vat_period_due_reminder"`.

### 2. Per-business notice lead time — column + migration

New additive migration (e.g. `203_AddVatNoticeLeadDaysToAssistantSetting.sql`), `USE [Portal]`:

- `ALTER TABLE [notification].[BusinessAssistantSetting] ADD [VatNoticeLeadDays] INT NULL;`
  (nullable — NULL means "fall back to global default"). Matches migration 199's additive/nullable
  convention.

Entity: add `public int? VatNoticeLeadDays { get; set; }` to `BusinessAssistantSetting`. Thread it
through `BusinessAssistantSettingRepository.UpsertAsync` (and its SELECTs) exactly like the other
nullable columns, and through the toggle/save preserve-forward logic in `AssistantsController` so a
partial write never nulls it.

> **Reuse vs. new column:** `IncludedFiguresCsv` could technically stash the lead time, but that's
> a hack — a typed `INT?` column is clearer, validates cleanly, and matches the additive-migration
> pattern. Prefer the column.

### 3. Period-scoped cycle key — `DigestCycleKey`

Add:
```csharp
/// <summary>
/// Period-scoped idempotency key for once-per-period reminders (e.g. VAT). Unlike Daily/Weekly,
/// this does NOT vary by date, so the reminder de-dupes across the whole notice window — at most
/// one non-Failed send per (business, assistant, period).
/// </summary>
public static string Period(string assistantKey, int periodId)
    => $"{assistantKey}:period-{periodId}";
```
`ExistsForCycleAsync` already excludes Failed rows, so a genuine retry after a delivery failure is
still allowed — matching Requirement 4.3.

### 4. Public per-period VAT figure accessor — `VatSubmissionService`

The authoritative calc `ComputeSubmissionFiguresAsync(int businessId, VatSubmissionPeriod period)`
is **private** and the service is tenant-scoped. Add a tenant-less, side-effect-free public method
to `IVatSubmissionService`:

```csharp
/// <summary>
/// Read-only approximate net VAT for a period the caller already holds, without tenant context
/// or persistence. Prefers a persisted (unsubmitted) VatSubmission.NetVatPayable; else computes
/// in-memory. Mirrors the persisted-then-compute pattern of the pre-submission checklist.
/// </summary>
Task<decimal> GetApproxNetVatPayableAsync(int businessId, VatSubmissionPeriod period);
```

**Takes the `VatSubmissionPeriod` the composer already found (V1 fix)** — no redundant re-load by
id, and "which period" is decided in exactly one place (the composer's scan). Implementation:
1. `var persisted = await _vatSubmissionRepository.GetByPeriodIdAndBusinessIdAsync(period.Id, businessId);`
   — if it exists and is **unsubmitted**, return `persisted.NetVatPayable`.
   (If it exists and IS submitted, the composer won't have selected this period — but return its
   value harmlessly if asked.)
2. Else `return (await ComputeSubmissionFiguresAsync(businessId, period)).NetVatPayable;`

**No behavioural change** to existing callers: `ComputeSubmissionFiguresAsync` stays private and
untouched; we only add a new public wrapper that takes an explicit `businessId` (so it's safe for
the tenant-less background scan). Existing VAT screens keep using their current paths.

> Optionally expose the full `VatFigures` (output/input/net) if the email wants a breakdown; the
> requirement only mandates the net, so a single `decimal` keeps the surface minimal. Return the
> net for now; widen later if the template wants the split.

### 5. Deadline detection — reuse `AttentionItemBuilder`'s logic

The scan must find an **unsubmitted period whose derived deadline is in the notice window**, and
must catch periods that have **ended** but whose deadline is imminent (Requirement 2.5). The
existing `AttentionItemBuilder` uses `GetCoveringUnsubmittedPeriodAsync(businessId, today)`, which
only finds the period covering *today* — insufficient here.

Use `VatSubmissionPeriodRepository.GetUnsubmittedPeriodsFromAsync(businessId, fromDate)` and select
per-period by derived deadline:

```csharp
// fromDate: far enough back that a just-ended period is still returned. PeriodEndDate can be
// up to VatFilingOffsetDays before the deadline, so start the scan window generously, e.g.
// today.AddDays(-(VatFilingOffsetDays + maxPeriodLenDays)). Simplest robust choice: scan all
// unsubmitted periods from an early bound and filter in-memory by deadline (period counts are
// tiny per business).
var periods = await _vatPeriodRepository.GetUnsubmittedPeriodsFromAsync(businessId, earlyBound);
var leadDays = setting?.VatNoticeLeadDays ?? _options.VatDeadlineNoticeDays;
var inWindow = periods
    .Select(p => new { p, deadline = VatDeadline.For(p, _options) })   // shared helper (§ DRY)
    .Where(x => { var d = x.deadline.DayNumber - today.DayNumber; return d >= 0 && d <= leadDays; })
    .OrderBy(x => x.deadline)         // most urgent first
    .ToList();

// V2 fix — do NOT just take the earliest: once reminded, its cycle key persists for the whole
// window, so a naive FirstOrDefault would keep returning it and STARVE a second in-window period.
// Pick the most-urgent period that has NOT already been reminded (no non-Failed cycle row yet).
foreach (var candidate in inWindow)
{
    var key = DigestCycleKey.Period(AssistantKey, candidate.p.Id);
    if (!await _outbox.ExistsForCycleAsync(businessId, assistantTypeId, key))
        return candidate;   // this run reminds this one; a still-due sibling is picked next run
}
return null; // all in-window periods already reminded → quiet skip
```

Emit for the **most-urgent not-yet-reminded** period (one email per run). A second in-window period
is picked on the next run once the first has its cycle row — no starvation. `VatFilingOffsetDays`
stays the single source for the offset (via the shared `VatDeadline.For` helper), so the deadline
math never diverges from `AttentionItemBuilder`.

> **DRY (adopted):** extract a tiny shared helper `VatDeadline.For(period, options)` =
> `period.PeriodEndDate.AddDays(options.VatFilingOffsetDays)` and use it in both
> `AttentionItemBuilder` and the composer, so there is exactly one definition of "derived deadline"
> and the two can never drift. The scan above already uses it.

### 6. `VatReminderComposer` (new) — `IDigestComposer`

Mirror `DailyBriefComposer` (owner-facing, returns null to skip). Deps: `PortalDbContext` (base),
`IDigestRecipientResolver`, `VatSubmissionPeriodRepository`, `IVatSubmissionService`,
`NotificationOutboxRepository` (for the §5 per-period `ExistsForCycleAsync` dedup),
`NotificationOptions`, `ILogger<VatReminderComposer>`.

```csharp
public string AssistantKey => DigestAssistantKeys.VatPeriodDueReminder;

public async Task<OutboxMessage?> ComposeAsync(
    int businessId, int assistantTypeId, BusinessAssistantSetting? setting, string cycleKey, CancellationToken ct)
{
    try
    {
        var today = /* business-local date — see cycle-key resolution */;

        // 1. Find the most-urgent, not-yet-reminded unsubmitted period in the lead window
        //    (§5 — the scan itself does the per-period ExistsForCycleAsync check, so this both
        //    selects the period AND is the once-per-period dedup short-circuit).
        var due = /* section 5 scan → { p, deadline } or null */;
        if (due == null)
        {
            _logger.LogInformation(
                "VAT reminder: no un-reminded unsubmitted period within notice window for BusinessId={BusinessId}.", businessId);
            return null; // expected quiet skip (nothing due, or all in-window periods already reminded)
        }

        // 2. Recipient.
        var recipients = await _recipientResolver.ResolveAsync(businessId, setting);
        if (recipients == null || string.IsNullOrWhiteSpace(recipients.PrimaryEmail))
        {
            _logger.LogWarning(
                "VAT reminder: no resolvable recipient for BusinessId={BusinessId}.", businessId);
            return null; // real problem
        }

        // 3. Approx net VAT (persisted-then-compute) — pass the period we already hold (V1).
        var (businessName, currencySymbol) = await LoadBusinessBrandingAsync(businessId);
        var netVat = await _vatSubmissionService.GetApproxNetVatPayableAsync(businessId, due.p);

        // 4. Build email.
        var deadline  = due.deadline;
        var daysUntil = deadline.DayNumber - today.DayNumber;
        var subject   = DigestEmailBuilder.VatReminderSubject(businessName, due.p, deadline);
        var body      = DigestEmailBuilder.BuildVatReminderHtml(
                            businessName, currencySymbol, due.p, deadline, daysUntil, netVat);

        return new OutboxMessage
        {
            BusinessId = businessId,
            AssistantTypeId = assistantTypeId,
            RecipientEmail = recipients.PrimaryEmail,
            Subject = subject,
            BodyHtml = body,
            OutboxMessageStatusTypeId = OutboxMessageStatusTypes.Pending,
            MaxRetries = _options.DefaultMaxRetries,
            ScheduledForUtc = DateTime.UtcNow,
            CycleKey = DigestCycleKey.Period(AssistantKey, due.p.Id),   // period-scoped
            CreatedAtUtc = DateTime.UtcNow
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "VAT reminder compose failed for BusinessId={BusinessId}.", businessId);
        return null; // fail safe per business (runner also guards)
    }
}
```

Email content (`DigestEmailBuilder.BuildVatReminderHtml`): owner-facing MyChair style, **no**
customer footer. States period label, derived deadline date, days remaining, and the **estimate**
line per the sign convention:
- `net > 0` → "Estimated VAT to pay: {sym}{net:N2} (approximate, as it stands today)."
- `net < 0` → "Estimated VAT refund: {sym}{|net|:N2} (approximate, as it stands today)."
- `net == 0` → "No VAT payment expected for this period (approximate, as it stands today)."

### 7. Cycle-key resolution (reconciling runner vs. composer) — NO runner change

The runner computes a `cycleKey` before calling `ComposeAsync` and pre-checks `ExistsForCycleAsync`
with it. For this assistant that pre-computed key (a daily date key, since the assistant is in
`DailyCadenceKeys`) can **never** match a period-scoped key (`…:period-{id}`), so the runner's
pre-check is a harmless no-op that never short-circuits — and that's fine, because the runner's
pre-check is only an optimisation, never the correctness authority. **No runner change is required
or desirable.**

Dedup is owned entirely by the composer:
- The §5 scan calls `ExistsForCycleAsync(businessId, assistantTypeId, Period(p.Id))` per candidate
  and only returns a period with **no** existing cycle row — this is the once-per-period guard.
- The returned `OutboxMessage.CycleKey` is the period key; `DigestEnqueuer.EnqueueAsync` re-checks
  it at insert time (narrowing, though not fully closing, the concurrent-pass race — see the
  enqueuer-race note in Overview; the shared UNIQUE-index hardening closes it).

So: inject `NotificationOutboxRepository` into the composer (for the scan's `ExistsForCycleAsync`).
`IDigestComposer` is unchanged; the existing date-based assistants are unaffected.

> Rejected alternative: adding `ResolveCycleKeyAsync` to `IDigestComposer` so the runner could
> pre-resolve the key. Cleaner in theory but touches the interface and every composer — overkill for
> one assistant, and unnecessary given the pre-check is only an optimisation.

> `today` inside the composer: `ComposeAsync` doesn't receive `businessLocalNow`. Reuse the same
> derivation the runner uses, or (simpler) `DateOnly.FromDateTime(DateTime.UtcNow)` — deadlines are
> whole-day boundaries measured in days, so a few hours' timezone skew doesn't change `daysUntil`
> materially. **Decision:** use `DateOnly.FromDateTime(DateTime.UtcNow)` for the deadline-day math
> (consistent with how `AttentionItemBuilder` uses `today`), while send-time-of-day is still gated
> by the runner's timezone-aware `IsDue`.

### 8. Runner registration

- Add `DigestAssistantKeys.VatPeriodDueReminder` to the `digestKeys` array in
  `ScheduledDigestRunner.RunAsync`.
- Add it to `DailyCadenceKeys` (it's evaluated daily; the period-scoped cycle key — not the daily
  date key — provides once-per-period semantics).
- Register `builder.Services.AddScoped<IDigestComposer, VatReminderComposer>();` in Program.cs
  alongside the others.

### 9. UI — new "deadline reminder" card shape

`AssistantsController.Index`: add `IsVatReminder = a.Key == DigestAssistantKeys.VatPeriodDueReminder`
and surface `VatNoticeLeadDays` on the card VM (default shown = global `VatDeadlineNoticeDays`).
`isScheduled` stays true (owner-facing, not event), so it flows through the scheduled branch, but
the view shows **send-time + lead-days + recipient**, no day-of-week, no figures.

`AxPostSaveAssistantSettings`: in the scheduled/owner branch, when `IsVatReminder`:
- null `SendDayOfWeek` (like the daily brief),
- parse + validate `VatNoticeLeadDays` (1..90; empty ⇒ null ⇒ global default),
- preserve-forward everything else.

View (`Assistants/Index.cshtml`): add a branch for the VAT card (send-time input + a "Remind me __
days before the deadline" number input + recipient controls + activity-log expander). The client
save payload includes the lead-days value.

---

## Data Model Summary

| Change | Type | Migration |
|--------|------|-----------|
| `AssistantType` Id 6 `vat_period_due_reminder` | seed row | `202_…` (MERGE + verify) |
| `BusinessAssistantSetting.VatNoticeLeadDays` | `INT NULL` | `203_…` (additive) |
| `IX_OutboxMessage_Cycle` → **UNIQUE** filtered (shared hardening) | index change | shared, e.g. `20x_…` |

> **Migration ranges (X1):** VAT owns **202–203**. The Task & Meeting spec owns **204–206**. The
> shared UNIQUE-index hardening is a single migration shared by both — whichever spec builds first
> creates it; the other reuses it (guarded `IF NOT EXISTS`, so it's idempotent either way).

No change to `VatSubmissionPeriod` / `VatSubmission` schema. Deadline stays derived. Migration
numbers are reserved above, but **reconfirm the next free number before creating** — this session
did not re-scan the migrations folder.

---

## Error Handling & Reliability

- Composer wrapped in `try/catch (Exception ex)` → log + return null (fail safe per business; the
  runner also guards per business/assistant).
- New VAT accessor + any repository use: async, typed, `try/catch (Exception ex)` rethrow, explicit
  `businessId` (tenant-less).
- Reuses the existing dispatcher for send/retry/failure — no new send path.
- Once-per-period via period cycle key + `ExistsForCycleAsync` (Failed excluded → retriable).

---

## Testing Strategy

Unit/integration (mirror the digests testing approach; the runner's compute logic is testable):
1. **Due-in-window** unsubmitted period → one reminder, correct deadline + net figure + estimate
   wording (each sign: owed / refund / none).
2. **Ended period, deadline imminent** (period window in the past, deadline within lead days) → still
   reminded (Requirement 2.5).
3. **Submitted period** (`IsSubmitted = 1`) → no reminder.
4. **Outside window** (deadline > leadDays away, or deadline passed) → no reminder (info skip).
5. **Once-per-period:** re-evaluate next day within window → no duplicate (period cycle key).
6. **Failed then retry:** a Failed row for the period → re-produced (ExistsForCycleAsync excludes
   Failed).
7. **Per-business lead time:** business with `VatNoticeLeadDays = 5` not reminded at 10 days out;
   business with null falls back to 21.
8. **No recipient** → null + warning (distinct from the info-level quiet skip).
9. **Disabled** / **non-entitled plan** → nothing.
10. **Persisted-then-compute:** unsubmitted `VatSubmission` present → uses persisted net; absent →
    in-memory compute matches `ComputeSubmissionFiguresAsync`.
11. **Regression:** weekly digests + Daily Brief unchanged; `AttentionItemBuilder` VAT line
    unchanged; existing VAT screens unchanged.

Manual: seed a business with an unsubmitted period whose deadline is ~N days out, set lead time,
confirm one email with the right estimate; confirm no second email next day; mark submitted →
confirm silence.

---

## Review Revisions Summary

- **Deadline scan uses `GetUnsubmittedPeriodsFromAsync` + per-period deadline filter**, not
  `GetCoveringUnsubmittedPeriodAsync(today)`, so recently-ended-but-not-yet-filed periods are caught
  (Requirement 2.5).
- **Period-scoped cycle key** (`:period-{id}`) added to `DigestCycleKey` for once-per-period
  semantics — the daily date key would have re-sent every day in the window.
- **Cycle-key reconciliation needs NO runner change** (V3): the composer's §5 scan does the
  per-period `ExistsForCycleAsync` dedup and owns its key; the runner's pre-check is a harmless
  no-op for this assistant. No `IDigestComposer` change.
- **V1:** `GetApproxNetVatPayableAsync(businessId, VatSubmissionPeriod period)` takes the period the
  composer already found — no redundant re-load, one place decides "which period."
- **V2:** the scan picks the most-urgent **not-yet-reminded** in-window period (skips those with an
  existing cycle row), so a second in-window period is never starved by an already-reminded earlier
  one. Corrects the earlier "the other gets its own reminder on a later poll" claim, which was false
  for a naive `FirstOrDefault`.
- **Enqueuer race (verified):** `DigestEnqueuer` is check-then-insert and `IX_OutboxMessage_Cycle`
  is non-unique — a pre-existing latent double-send window. Shared hardening: make the index UNIQUE
  filtered + treat the unique violation as "already enqueued." Low-risk for single-message VAT;
  important for fan-out (Task/Meeting spec).
- **Migration ranges reserved:** VAT 202–203; shared index-hardening migration idempotent.
- **Net VAT via a new tenant-less public `GetApproxNetVatPayableAsync(businessId, periodId)`** using
  persisted-then-compute; the private `ComputeSubmissionFiguresAsync` stays untouched (no regression
  to VAT screens). Corrects the earlier catalog note that wrongly cited a non-existent
  `IVatIntegrationService.GetCurrentPeriodSummaryAsync`.
- **Per-business lead time = new typed `INT?` column** (`VatNoticeLeadDays`), not an overloaded CSV
  field; falls back to the global default.
- **`today` for deadline-day math = UTC date** (whole-day granularity), while send-time-of-day stays
  timezone-aware via the runner's `IsDue`.
- **Optional DRY:** extract `VatDeadline.For(period, options)` shared by builder + composer.
