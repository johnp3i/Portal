# Payroll Module — Review Backlog

**Date:** 2026-02 (review session)
**Scope:** Full payroll module — calculation engine, PAYE, orchestrator, P&L integration,
compliance integration, period lifecycle, batch generation, save/recalc, repository data access.
**Status legend:** OPEN = confirmed, not yet fixed · FIXED = resolved this session ·
VERIFIED-OK = investigated, not an issue · IMPROVEMENT = non-bug enhancement.

> This backlog records every finding from the review, including items being fixed immediately
> and items deferred. Items fixed this session are marked FIXED with a note.

---

## Correctness issues

### C1 — Re-finalisation does not recalculate payslip totals  [FIXED]
`PayslipPeriodStatusService.RefinalisePeriodAsync` flips statuses, writes audit entries, and
triggers compliance, but never recalculates payslip totals. Phase B Requirement 5.1 requires the
Calculation_Engine to recalculate all payslip totals on re-finalisation. Today totals stay current
only because edits during the Unlocked window go through `SaveEarningLinesAsync` (which recalcs per
save). If any input changes outside that path, re-finalise locks in stale totals.
**Risk:** medium. **Action:** recalculate all payslips in the period during re-finalisation.

### C2 — Re-finalisation does not adjust P&L entries  [FIXED]
`FinalisePeriodAsync` creates P&L entries, but `RefinalisePeriodAsync` never calls
`IPayrollPnlService.AdjustPnlEntriesAsync` (which exists and is correct: cancels old entries,
creates new). Phase B Requirement 7 requires P&L reversal + recreation on re-finalisation.
**Consequence:** unlock → change a salary → re-finalise leaves the P&L showing the ORIGINAL
figures. Payslips correct, P&L silently wrong. Real money-reporting bug.
**Risk:** HIGH. **Action:** call `AdjustPnlEntriesAsync` inside re-finalisation, transactionally.

### C3 — P&L creation lacks idempotency / zero-guard  [OPEN — backlog]
`PayrollPnlService.CreatePnlEntriesAsync` always inserts two Purchase rows from whatever payslips
exist, with no "already has entries for this period" check and no guard for zero/no payslips.
A retry of finalisation could create duplicate P&L entries; a zero-payslip period would create €0
entries. The finalise transaction protects a single call but not re-entry.
**Risk:** low-medium. **Action:** add an idempotency guard (skip/replace if active entries exist
for the period) and a no-payslip guard.

### C4 — Social Insurance filing month-offset  [VERIFIED-OK]
Investigated: `FindSocialInsuranceFilingAsync` applies the +1 month offset internally (with
December→January wraparound). The service passing the raw payroll month is correct. No issue.

---

## Robustness / money-safety

### R1 — PAYE top-band uses decimal.MaxValue sentinel  [OPEN — backlog]
`PayeCalculationService` sets `bandUpper = band.UpperBound ?? decimal.MaxValue` then
`Math.Min(annualProjected, bandUpper) - bandLower`. Safe today because annualProjected is always
the min, but a future refactor subtracting from bandUpper directly would overflow.
**Risk:** low (latent). **Action:** add a guard/comment, or special-case the open-ended top band.

### R2 — No defensive validation of PAYE band contiguity at calc time  [OPEN — backlog]
The engine trusts seeded bands are ordered, gapless, non-overlapping. If bad band data is seeded,
income in a gap is silently untaxed. Schema is supposed to enforce (Phase D Req 8.2) but the
calculation does not defensively check.
**Risk:** low (depends on seed integrity). **Action:** cheap sanity check that bands cover
[0, ∞) contiguously; fail-closed on misconfiguration.

### R3 — ConfirmBatchGenerationAsync is not transactional  [FIXED]
Batch confirm inserts payslip + earning lines + deduction lines across many separate repository
calls with no transaction (unlike `FinalisePeriodAsync`). A mid-way failure leaves a half-generated
period.
**Risk:** medium (data integrity). **Action:** wrap the whole confirm in a DB transaction.

### R4 — Audit trail attributes actions to "system" instead of the real user  [FIXED (SaveEarningLines + Finalise; SaveManagerNotes still "system" — see note)]
`FinalisePeriodAsync` passes "system" as the compliance userId; `SaveEarningLinesAsync` audit uses
"system" with a "future refactor" comment. The audit feature exists for accountability (Phase B);
attributing edits/finalisation to "system" undermines it. The controller has the real userId.
**Risk:** medium (compliance/accountability). **Action:** thread the authenticated userId through
`SaveEarningLinesAsync` and `FinalisePeriodAsync` → compliance/audit.

---

## Improvements (non-bugs)

### I1 — Name-based lookup pattern still present for EARNING types  [OPEN — backlog]
`ConfirmBatchGenerationAsync` resolves earning types by Name
(`earningTypes.FirstOrDefault(e => e.Name == el.EarningTypeName)`). Earning names are currently
unique, so it is not biting, but it is the same fragile pattern that caused the deduction
category bug (now fixed). **Action:** resolve by Id/Code for consistency and future-proofing.

### I2 — Duplicated deduction-loading boilerplate + N+1 rate-history queries  [OPEN — backlog]
The `GetActiveDeductionsWithRatesAsync` → build `DeductionTypeWithHistory` loop (one
`GetRateHistoryAsync` per type) is duplicated in 4 places (preview, confirm, save-earning-lines,
recalculate) and is an N+1 pattern. **Action:** extract a single helper; load rate histories in
one query.

### I3 — Hard-coded "CY" country default in the orchestrator  [OPEN — backlog]
`PayslipCalculationOrchestrator` maps business country to ISO code via an in-code dictionary and
defaults unmapped countries to "CY" — unmapped jurisdictions silently calculate as Cyprus.
(Also flagged in the standalone architecture audit.) **Action:** data-drive the mapping and
fail-closed on unmapped countries.

### I4 — Test suite cannot run; payroll paths lack coverage  [OPEN — backlog]
`Portal.Tests` has pre-existing compile errors (Invoice/Proposal controller tests out of sync
with newer constructors) that block the whole assembly, so the new
`PayrollServiceConfirmBatchTests` cannot execute. The confirm and re-finalise paths need real
coverage, especially given C1/C2. **Action:** fix the 4 unrelated controller-test constructors,
then run/extend payroll tests.

---

## Fix order (this session)
1. C2 — P&L adjustment on re-finalisation (highest money impact)
2. C1 — recalculate payslips on re-finalisation
3. R3 — transaction around batch confirm
4. R4 — real userId in audit/compliance
(Then backlog items C3, R1, R2, I1, I2, I3, I4 as separate work.)


## Fixed this session (summary)
- **C1 + C2** — PayrollService.RefinalisePeriodAsync rewritten: recalculates every payslip, transitions Unlocked->Re-finalised with audit, and calls AdjustPnlEntriesAsync, all in one transaction; compliance post-commit. (Replaces the thin pass-through to the status service.)
- **R3** — ConfirmBatchGenerationAsync insert loop + status transition wrapped in a DB transaction (rollback on failure).
- **R4** — FinalisePeriodAsync and SaveEarningLinesAsync now accept an optional userId; controllers pass the authenticated user; audit/compliance attribute the real user instead of "system". NOTE: SaveManagerNotesAsync still logs "system" (same trivial pattern) — added to backlog as C3-adjacent cleanup.
- Verified: both projects build with 0 errors.
