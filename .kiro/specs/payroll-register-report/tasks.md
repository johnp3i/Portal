# Implementation Plan: Payroll Register Report — Phase 3 (On-Page Preview)

## Overview

Phases 1 (single-period) and 2 (range reshape) are **fully implemented and shipped** — every Phase 1
and Phase 2 task is complete. The register already resolves a FROM/TO month-year range, lists
eligible employees, and produces a single `.xlsx` workbook (Summary sheet with per-contribution
columns + grand-totals row, one detail sheet per employee with month columns + Total + "%") via the
`AxGetGenerateRegister` Download endpoint.

This Phase 3 plan adds an **on-page, per-employee Preview** alongside the existing Download. It is a
small set of *change* tasks over the shipped code — not a fresh build. The user gains a second action
on the same selection: a **Preview** button that renders one per-employee ledger table in the section
below the filter (earnings, employee deductions, employer contributions, section totals,
Employee_Totals), without producing a file. The existing **Download** keeps its exact current
behavior.

The central refactor makes Preview and Download read from **one shared data computation**:

- A new result model `RegisterData { RangePeriods; Aggregates }` is added next to the other register
  models.
- A new feature-owned `GetRegisterDataAsync(fromYear, fromMonth, toYear, toMonth, businessId,
  employeeIds)` lifts the current steps 1–6 of `GenerateRegisterWorkbookAsync` (the guards, range
  resolution, payslip-map + reference-lookup loads, per-employee aggregate loop, and decline paths)
  verbatim, returning the ordered Range_Periods and the per-employee aggregates (or `null` on any
  decline path).
- `GenerateRegisterWorkbookAsync` is refactored to call `GetRegisterDataAsync` and then run only the
  **existing** ClosedXML workbook build (step 7+) on `data.Aggregates` + `data.RangePeriods`. Its
  decline and success behavior must be **byte-for-byte identical** on every path.

Because the workbook is built from exactly the aggregates the preview JSON is projected from, a
preview and a download of the same selection agree cell-for-cell and can never drift (R15.12, R12.6).
The Preview_Action therefore incurs **no** additional per-employee or per-period round trips beyond
what the Download_Action already performs (R14.6).

The four private helpers that own the heavy lifting — `AssembleEmployeeAggregateAsync`,
`ResolveRangePeriodsAsync`, `BuildEmployeePayslipMapAsync`, `LoadRegisterReferenceLookupsAsync` — are
**not changed**; `GetRegisterDataAsync` only orchestrates them. The six existing report pages and the
protected operation signatures remain untouched. Each step builds on the previous and ends by wiring
the piece in: the shared data method + model first, then the preview controller endpoint, then the
view (buttons + CSS + preview JS), then the final checkpoint.

## Tasks

- [x] 1. Add the shared data computation and refactor workbook generation to use it
  - [x] 1.1 Add the `RegisterData` result model and the `GetRegisterDataAsync` method
    - In `Portal.Infrastructure/Models/Payroll/RegisterDtos.cs`, add the result model
      `RegisterData { List<PayslipPeriod> RangePeriods = new(); List<RegisterEmployeeAggregate>
      Aggregates = new(); }` next to the existing register models (`PayslipPeriod` is an entity
      already referenced there; `RangePeriods` is ordered oldest→newest, `Aggregates` is in selection
      order, eligible only).
    - Add `Task<RegisterData?> GetRegisterDataAsync(int fromYear, int fromMonth, int toYear, int
      toMonth, int businessId, int[] employeeIds)` to the interface `IPayrollReportService` and to the
      implementation `PayrollReportService`. It performs the **current steps 1–6** of
      `GenerateRegisterWorkbookAsync`: guard `businessId <= 0` → `null`; guard null/empty
      `employeeIds` → `null`; `ResolveRangePeriodsAsync` → `null` if no Range_Period;
      `BuildEmployeePayslipMapAsync` + `LoadRegisterReferenceLookupsAsync` **once** for the whole
      range; loop `employeeIds.Distinct()` preserving selection order, skip any employee with no
      in-range finalised payslip (R9.3), call `AssembleEmployeeAggregateAsync` to accumulate
      aggregates; if `aggregates` is empty → `null`; otherwise return
      `new RegisterData { RangePeriods = rangePeriods, Aggregates = aggregates }`.
    - This is feature-owned with **no external callers**, so adding it is purely additive (R12.6).
      Do **not** change `AssembleEmployeeAggregateAsync`, `ResolveRangePeriodsAsync`,
      `BuildEmployeePayslipMapAsync`, or `LoadRegisterReferenceLookupsAsync`. `try/catch (Exception
      ex) { throw; }` on data-access paths, consistent with the existing method. Leave every protected
      signature unchanged.
    - _Requirements: 12.6, 15.12, 14.6, 8.1, 8.2, 9.3, 2.6_
  - [x] 1.2 Refactor `GenerateRegisterWorkbookAsync` to call `GetRegisterDataAsync`
    - Reshape the body of `GenerateRegisterWorkbookAsync(int fromYear, int fromMonth, int toYear, int
      toMonth, int businessId, int[] employeeIds)` so it calls `GetRegisterDataAsync` with the same
      arguments; if the result is `null`, return `null` (same decline behavior as before); otherwise
      run the **existing** ClosedXML workbook build (the current step 7+) on `data.Aggregates` and
      `data.RangePeriods` — the Summary sheet with the dynamic Contribution_Type union columns and the
      grand-totals row, plus one detail sheet per eligible employee — and return the bytes.
    - Behavior must be **byte-for-byte identical** on every path (bad business id, empty selection, no
      Range_Periods, no eligible aggregates, and success). This is purely a lift-and-delegate of
      steps 1–6 into `GetRegisterDataAsync`; the workbook-build code (step 7+) and its helpers
      (`BuildContributionTypeColumns`, `BuildRegisterWorkbook`, `BuildEmployeeDetailSheet`,
      `SanitizeWorksheetName`, range-label) are unchanged. `try/catch (Exception ex) { throw; }`.
    - _Requirements: 4.1, 4.6, 4.7, 9.5, 12.6, 15.12_
  - [ ]* 1.3 Write a service test anchoring the shared-data guarantee
    - Mirror `PayrollReportServiceTests`
      (`Mock<PayrollRepository>(MockBehavior.Loose, new object[] { null! })`).
    - Assert: `GetRegisterDataAsync` returns aggregates whose **count** and **per-employee totals**
      (Total Earnings, Total Employee Deductions, Net Salary, Total Employer Contributions, Total
      Cost) match what the workbook is built from — build the workbook from the same `RegisterData`,
      re-open the bytes with `new XLWorkbook(new MemoryStream(bytes))`, and assert the detail-sheet
      count equals `data.Aggregates.Count` and each employee's sheet totals match the aggregate's
      totals; and that the eligible set matches an equivalent `GenerateRegisterWorkbookAsync` call.
      Also assert `GenerateRegisterWorkbookAsync` returns `null` on every path `GetRegisterDataAsync`
      returns `null` (bad business id, empty selection, no Range_Periods, no eligible aggregates).
    - _Requirements: 15.12, 12.6_

- [x] 2. Add the `AxGetPreviewRegister` JSON preview endpoint to the controller
  - [x] 2.1 Add the `AxGetPreviewRegister` GET action
    - In `Portal.Web/Controllers/PayrollRegisterController.cs`, add
      `[HttpGet] public async Task<IActionResult> AxGetPreviewRegister(int fromYear, int fromMonth,
      int toYear, int toMonth, int[] employeeIds)`. Resolve `businessId =
      _tenantService.CurrentBusinessId`.
    - **Validate BEFORE calling the service — identical to `AxGetGenerateRegister`:** From ≤ To by
      ordinal compare (`fromYear*12+fromMonth <= toYear*12+toMonth`) and `employeeIds` non-empty; if
      From > To or the selection is empty, return `Json(new { success = false, message })` naming the
      invalid condition **without invoking the service** (R15.4).
    - On valid input call the shared `GetRegisterDataAsync`; a `null` result → `Json(new { success =
      false, message = "No finalised payslip data is available for the selected range and employees."
      })` (R15.9, R15.10). On a non-null result → `Json(new { success = true, data = <per-employee
      preview projection> })` (R15.2). **Never** return a `File`.
    - Project each aggregate (in returned order) to a camelCase-friendly anonymous/projected object:
      `employeeId`, `employeeName`, `rangePeriods` (`[{ periodId = Id, year, month }]` oldest→newest),
      `earnings` / `employeeDeductions` / `employerContributions` (each `[{ name, amountByPeriodId,
      total, percentage }]`), `totalEarnings`, `totalEmployeeDeductions`, `netSalary`,
      `totalEmployerContributions`, `totalCost`. Rely on ASP.NET Core's default camelCase JSON
      serialization via `Json(...)` — no computation is added in the projection (preserving R15.12).
    - `catch (Exception ex)` → log via `SystemLoggerExtensions`, return a generic `Json` failure with
      no stack trace. Every catch keeps the `ex` variable.
    - _Requirements: 15.2, 15.3, 15.4, 15.9, 15.10, 15.12, 10.1, 10.4_
  - [ ]* 2.2 Write controller unit tests for the preview endpoint
    - Mirror the existing `PayrollRegisterController` tests. Assert: `AxGetPreviewRegister` returns a
      `JsonResult` (**not** a `FileResult`) with `success = true` and a non-null `data` payload for
      valid input (stub `GetRegisterDataAsync` to return a `RegisterData`); returns a `Json` failure
      for From>To and for empty selection **without calling the service** (verify the mocked
      `GetRegisterDataAsync` is never invoked via Moq `Times.Never`); returns a `Json` failure when
      `GetRegisterDataAsync` returns `null`.
    - _Requirements: 15.4, 15.2, 15.9_

- [x] 3. Add the Preview/Download buttons, preview CSS, and preview JS to the view
  - [x] 3.1 Replace the single Generate button with Preview + Download buttons
    - In `Portal.Web/Views/PayrollRegister/Index.cshtml`, replace the single Generate button with
      **two** buttons inside the existing filter card: a **Preview** button (`btn btn-secondary`,
      `id="previewBtn"`) and a **Download** button (`btn btn-primary`, `id="generateBtn"`) that keeps
      the exact current download behavior. Both buttons keep the existing disabled-when-no-eligible
      logic (R3.6, R15.1). Add an empty **preview container** below the status/content card into which
      the per-employee ledger tables render (empty until a Preview_Action succeeds).
    - _Requirements: 15.1, 3.6_
  - [x] 3.2 Add the preview-table CSS block to the existing view `<style>`
    - In the existing view `<style>` block, add CSS for the preview ledger tables matching the MyChair
      look: header cells with `#0D5EA6` fill and white bold font (consistent with the workbook/export
      styling); money cells right-aligned, two-decimal (`#,##0.00`-style) presentation; and a
      per-table **horizontally scrollable wrapper** (`overflow-x: auto`) so a full-year range (12 month
      columns + Total + "%") stays usable without breaking page layout (R14.7).
    - _Requirements: 14.7, 15.6_
  - [x] 3.3 Add the `onPreview()` fetch handler and `renderPreview(data)` renderer
    - Add an `onPreview()` handler wired to `#previewBtn` that mirrors `onGenerate()`'s client-side
      guards: if From `(Year,Month)` > To `(Year,Month)` → SweetAlert2 warning and do **not** fire;
      if no employee is selected → SweetAlert2 warning and do **not** fire (R15.4). Otherwise
      `BlockUI.show('Loading preview...')` (R15.11); `fetch AxGetPreviewRegister` with the four range
      params (`fromYear`/`fromMonth`/`toYear`/`toMonth`) + repeated `employeeIds`; `BlockUI.hide()` in
      **all** paths (R15.11). On JSON `success === true` → `renderPreview(data)` into the preview
      container (R15.5); on `success === false` or empty → **clear** the preview container and show a
      SweetAlert2 error/inline message describing the outcome (R15.10); network/non-JSON → clear the
      preview container and show a generic SweetAlert2 error (R11.6). No native dialogs (R11.7).
    - Add `renderPreview(data)` that builds **one ledger table per employee** (in returned order) into
      the preview container: a heading with `employeeName`; three sections — **Earnings**, **Employee
      Deductions**, **Employer Contributions** — each a header row then one row per line, with columns
      = line name, one column per `rangePeriods` entry (header = abbreviated month + year, e.g.
      `new Date(year, month-1).toLocaleString('en', { month: 'short' }) + ' ' + year`) reading
      `amountByPeriodId[periodId]` (**`0.00`** when absent — R15.8), a **Total** column, and a **"%"**
      column (showing `percentage` formatted e.g. `"8.80%"` for deductions/contributions, blank for
      earnings — R15.6); a **section total row** per section (R15.7); then a per-employee
      **Employee_Totals** block (Total Earnings, Total Employee Deductions, Net Salary, Total Employer
      Contributions, Total Cost — R15.7). Wrap each table in the scrollable container (R14.7). Clear
      the preview container before rendering, and clear it when a new range/selection invalidates a
      prior preview (e.g. on the existing From/To-change and selection-change handlers).
    - Keep the existing **Download** (`onGenerate`) blob-or-JSON path and the range/employee-widget
      logic **untouched**.
    - _Requirements: 15.5, 15.6, 15.7, 15.8, 15.9, 15.10, 15.11, 14.7, 11.3, 11.4, 11.6, 11.7_

- [x] 4. Final checkpoint - build and confirm no regression
  - Run `dotnet build Portal.Web` and confirm 0 errors; run the payroll test suite (the reshaped
    service + controller tests, including the Phase 3 shared-data and preview tests); confirm no
    regression and that the **Download** behavior is unchanged (same file name, content type, and
    decline paths as before). Ask the user if questions arise.

## Notes

- This is **Phase 3 — On-Page Preview**: every task *modifies* the already-shipped Phase 1 + Phase 2
  implementation. Phases 1 and 2 are complete and are not re-listed. Checkboxes are reset to
  unchecked because this preview work is new work to execute.
- Tasks marked with `*` are optional (unit tests) and can be skipped for a faster MVP; all core
  implementation tasks are required and must be implemented.
- The design has **no new Correctness Properties section for Phase 3** that needs fresh property
  tests here — the shared-data guarantee (design Property 10, R15.12/R12.6) is anchored by the
  optional service test in task 1.3, which asserts the preview aggregates equal the figures the
  workbook is built from. The existing Phase 2 property tests continue to cover the workbook
  invariants unchanged.
- The refactor is **behavior-preserving for Download**: `GenerateRegisterWorkbookAsync` must return
  `null` on every path it returned `null` on before and produce byte-for-byte identical workbooks on
  success. `AssembleEmployeeAggregateAsync`, `ResolveRangePeriodsAsync`, `BuildEmployeePayslipMapAsync`,
  and `LoadRegisterReferenceLookupsAsync` are not changed — `GetRegisterDataAsync` only orchestrates
  them (R12.6).
- `GetRegisterDataAsync` is feature-owned with no external callers, so adding it is purely additive;
  the six existing report pages and the protected signatures (`GetPeriodSummaryAsync`,
  `GetLatestFinalisedPeriodIdAsync`, `GetPayslipDetailAsync`, `GetPeriodsAsync`,
  `GetPeriodsByBusinessAsync`, `GetFinalisedPayslipsForPeriodAsync`) are unchanged (R12.6).
- The Preview_Action and Download_Action share the single `GetRegisterDataAsync`, so the preview adds
  no per-employee or per-period round trips beyond those the download already performs (R14.6); the
  preview tables render inside a horizontally scrollable container for wide ranges (R14.7).
- The preview is **per-employee only** — it never renders the Summary/grand-totals cross-employee
  table (R15.5).
- Every `try/catch` keeps the `catch (Exception ex)` variable (coding golden rule); the preview Ajax
  endpoint uses the `AxGet` prefix; the view uses vanilla `fetch` + BlockUI + SweetAlert2 with no
  native dialogs and no jQuery.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2"] },
    { "id": 2, "tasks": ["1.3", "2.1"] },
    { "id": 3, "tasks": ["2.2", "3.1"] },
    { "id": 4, "tasks": ["3.2"] },
    { "id": 5, "tasks": ["3.3"] }
  ]
}
```
