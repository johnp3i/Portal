# Design Document: Payroll Register Report

## Overview

The Payroll Register is a new, strictly additive payroll report for the 3 Inventors Portal payroll
module. A payroll user picks a **from/to month-year range** and one-or-more employees, then
downloads a single `.xlsx` workbook containing:

- a **Summary sheet** (sheet index 1): one row per selected employee aggregated across the whole
  range, one column per employer Contribution_Type, plus a bold grand-totals row whose
  per-contribution totals give "every contribution total on the first page", and
- **one detail sheet per selected employee**: the employee's month-by-month breakdown across the
  range (earnings, employee deductions, employer contributions) with one month column per
  Range_Period, a Total column, a "%" column, section totals, and a per-employee totals block.

This is the first payroll report that combines a **multi-month range**, **multi-employee
selection**, and **per-employee detail sheets** in one workbook. The existing six report pages
(PeriodSummary, EarningsBreakdown, ContributionReport, EmployeeHistory, AnnualSummary,
EmployeeStatement) are not removed, retired, or altered — this feature only adds a new controller,
new additive service methods, a workbook-builder helper, and a new view (Requirement 12).

The register is both **downloadable** and **previewable on the page** (Requirement 15). The page
offers two actions on the same selection:

- **Download** — the existing `AxGetGenerateRegister` endpoint returning the `.xlsx` workbook
  (Requirement 4), unchanged.
- **Preview** — a new `AxGetPreviewRegister` JSON endpoint that returns the per-employee data, which
  the page renders as one ledger table per eligible selected employee (earnings, employee
  deductions, employer contributions, section totals, Employee_Totals) in the section below the
  filter, without producing a file. The preview is **per-employee only** — it does not render the
  Summary sheet's cross-employee table.

Both actions read from **one shared data computation** (`GetRegisterDataAsync`, below): the same
per-employee aggregates and ordered Range_Periods the workbook is built from. A preview and a
download of the same selection therefore agree cell-for-cell and can never drift (R15.12, R12.6).

### Additive constraint (Requirement 12)

The feature reuses existing building blocks with their current signatures unchanged:

- `IPayrollReportService.GetLatestFinalisedPeriodIdAsync(businessId)` — range default (To selector).
- `IPayrollReportService.GetPeriodSummaryAsync(periodId, businessId, departmentId)` — referenced
  conceptually for the per-period number definitions (R5), but **not** used to filter by employee
  (see the design decision below).
- `IPayrollService.GetPeriodsAsync(businessId)` — reused by existing callers unchanged.
- `IPayrollRepository.GetPeriodsByBusinessAsync(businessId)` — returns
  `List<PayslipPeriod>` ordered `Year DESC, Month DESC`; the Register uses it to populate the
  From/To selectors and to **enumerate the Range_Periods** of a submitted range (R2.1, R12.4).
  `virtual`/mockable.
- `IPayrollService.GetPayslipDetailAsync(id, businessId)` — the shape (`PayslipDetailDto`) is a
  conceptual reference for a single period's figures; **not** called per employee or per period at
  generation time (N+1 avoidance, R14.3).
- `IPayrollRepository.GetFinalisedPayslipsForPeriodAsync(periodId, businessId)` — the authoritative
  finalised-payslip set for a single period (finalised = `PayslipStatusTypeId IN (3,5)`,
  tenant-scoped via the `PayslipPeriod.BusinessId` join). Called **once per Range_Period**. `virtual`.
- `ICurrentTenantService.CurrentBusinessId` — tenant scoping.

Any data operation not satisfied by the reused service operations is added as a **new** method
rather than changing an existing signature or behavior (R12.4, R12.5). Existing callers remain
untouched. The additive boundary protects exactly these operations: the six existing report pages,
`GetPeriodSummaryAsync`, `GetLatestFinalisedPeriodIdAsync`, `GetPayslipDetailAsync`,
`GetPeriodsAsync`, `GetPeriodsByBusinessAsync`, and `GetFinalisedPayslipsForPeriodAsync`.

#### New additive repository method (R12.5)

`PayrollRepository` gains ONE new method; no existing signature or behavior changes:

```csharp
// Fetches exactly the employees whose Ids are supplied, scoped to the business, in ONE batched
// query — regardless of IsActive / EndDate. Used to resolve employee names for the finalised
// payslips found across the whole range.
Task<List<Employee>> GetEmployeesByIdsAsync(int[] employeeIds, int businessId);
```

This is purely additive: it adds a new repository method and does not change the signature or
behavior of any existing method (R12.5). It exists because the only period-scoped employee query,
`GetActiveEmployeesForPeriodAsync(businessId, periodStart)`, filters
`IsActive = 1 AND (EndDate IS NULL OR EndDate >= periodStart)` and would therefore silently drop a
terminated/inactive employee who nonetheless has a finalised payslip somewhere in the range — see
the "Employee→payslip resolution" and "N+1 avoidance" sections for why that list is unsuitable for
name resolution here. It is loaded **once** over the distinct employee Ids found across the whole
range (never per period/per employee).

### New additive service methods own their own signatures (R12.5)

The two public methods below are **introduced by this feature** and have **no external callers**
outside it. They were added to `IPayrollReportService` by the Payroll Register itself. Reshaping
their parameters from a single period to a from/to range is therefore *not* a breach of the additive
constraint: R12 protects the six existing reports plus `GetPeriodSummaryAsync`,
`GetLatestFinalisedPeriodIdAsync`, `GetPayslipDetailAsync`, `GetPeriodsAsync`,
`GetPeriodsByBusinessAsync`, and `GetFinalisedPayslipsForPeriodAsync` — none of which change shape.
The Register's own methods are free to take the range parameters this feature now requires.

```csharp
// Introduced by THIS feature. Changing its shape to range parameters is allowed (no external callers).
Task<List<RegisterEmployeeOption>> GetRegisterEligibleEmployeesAsync(
    int fromYear, int fromMonth, int toYear, int toMonth, int businessId);

// Introduced by THIS feature. The SINGLE shared data computation behind both the Download_Action
// (workbook) and the Preview_Action (JSON). No callers outside the Payroll_Register, so introducing
// it is purely additive (R12.6). Returns null on any decline path (bad business id, empty selection,
// no Range_Periods, no eligible aggregates).
Task<RegisterData?> GetRegisterDataAsync(
    int fromYear, int fromMonth, int toYear, int toMonth, int businessId, int[] employeeIds);

// Introduced by THIS feature. Changing its shape to range parameters is allowed (no external callers).
// Refactored to delegate steps 1-6 to GetRegisterDataAsync (see below).
Task<byte[]?> GenerateRegisterWorkbookAsync(
    int fromYear, int fromMonth, int toYear, int toMonth, int businessId, int[] employeeIds);
```

#### The shared data method `GetRegisterDataAsync` (the key refactor for R15.12 / R12.6)

Today `GenerateRegisterWorkbookAsync` performs, in order:

1. guard `businessId <= 0` → `null`;
2. guard empty `employeeIds` → `null`;
3. `ResolveRangePeriodsAsync` → `rangePeriods`, `null` if empty;
4. `BuildEmployeePayslipMapAsync` + `LoadRegisterReferenceLookupsAsync` once;
5. loop the selected ids (dedup, preserve order), skip ineligible, `AssembleEmployeeAggregateAsync`
   → `List<RegisterEmployeeAggregate> aggregates`;
6. if `aggregates` empty → `null`;
7. build the ClosedXML workbook (Summary with contribution-type union columns + one detail sheet per
   employee) and return the bytes.

Steps 1–6 are **lifted verbatim** into a new feature-owned method `GetRegisterDataAsync`, which
returns a small result object carrying the ordered Range_Periods and the per-employee aggregates (or
`null` on any decline path). `GenerateRegisterWorkbookAsync` is then refactored to call it and run
only the workbook build (step 7) on the returned data:

```csharp
public async Task<RegisterData?> GetRegisterDataAsync(
    int fromYear, int fromMonth, int toYear, int toMonth, int businessId, int[] employeeIds)
{
    // (1) bad business id -> decline
    if (businessId <= 0) return null;
    // (2) empty selection -> decline
    if (employeeIds == null || employeeIds.Length == 0) return null;

    // (3) resolve Range_Periods (ordered oldest->newest); null/empty -> decline
    var rangePeriods = await ResolveRangePeriodsAsync(fromYear, fromMonth, toYear, toMonth, businessId);
    if (rangePeriods == null || rangePeriods.Count == 0) return null;

    // (4) load the payslip map + the four reference lookups ONCE for the whole range (N+1 avoidance)
    var payslipMap = await BuildEmployeePayslipMapAsync(rangePeriods, businessId);
    var lookups = await LoadRegisterReferenceLookupsAsync(businessId);

    // (5) build one aggregate per eligible selected employee (dedup, preserve selection order)
    var aggregates = new List<RegisterEmployeeAggregate>();
    foreach (var employeeId in employeeIds.Distinct())
    {
        if (!payslipMap.PayslipsByEmployee.TryGetValue(employeeId, out var periodPayslips)
            || periodPayslips.Count == 0)
        {
            continue; // no in-range finalised payslip -> omit (R9.3)
        }
        aggregates.Add(await AssembleEmployeeAggregateAsync(
            employeeId, periodPayslips, rangePeriods, payslipMap.NamesById, lookups));
    }

    // (6) no eligible selected employee -> decline
    if (aggregates.Count == 0) return null;

    return new RegisterData { RangePeriods = rangePeriods, Aggregates = aggregates };
}

public async Task<byte[]?> GenerateRegisterWorkbookAsync(
    int fromYear, int fromMonth, int toYear, int toMonth, int businessId, int[] employeeIds)
{
    var data = await GetRegisterDataAsync(fromYear, fromMonth, toYear, toMonth, businessId, employeeIds);
    if (data == null) return null; // same decline behavior as before

    // (7+) the EXISTING ClosedXML build, now reading data.Aggregates + data.RangePeriods
    var contributionColumns = BuildContributionTypeColumns(data.Aggregates);
    return BuildRegisterWorkbook(
        BuildRangeLabel(fromYear, fromMonth, toYear, toMonth), data.Aggregates, contributionColumns);
}
```

Properties of this refactor:

- **No external callers.** `GetRegisterDataAsync` is owned entirely by the Payroll_Register and is
  invoked only by `GenerateRegisterWorkbookAsync` (Download) and `AxGetPreviewRegister` (Preview).
  Adding it is purely additive under the R12 constraint, which protects the six existing reports plus
  `GetPeriodSummaryAsync`, `GetLatestFinalisedPeriodIdAsync`, `GetPayslipDetailAsync`,
  `GetPeriodsAsync`, `GetPeriodsByBusinessAsync`, and `GetFinalisedPayslipsForPeriodAsync` — none of
  which change (R12.6).
- **Single source of truth.** Because the workbook is built from exactly the `Aggregates` and
  `RangePeriods` the preview JSON is serialized from, every previewed figure equals the corresponding
  downloaded cell for the same selection (R15.12, R12.6) — they are literally the same objects.
- **Decline behavior preserved.** `GenerateRegisterWorkbookAsync` still returns `null` on every path
  it returned `null` on before (bad business id, empty selection, no Range_Periods, no eligible
  aggregates), so the Download controller flow is byte-for-byte identical.
- `AssembleEmployeeAggregateAsync`, `ResolveRangePeriodsAsync`, `BuildEmployeePayslipMapAsync`, and
  `LoadRegisterReferenceLookupsAsync` are **unchanged**; `GetRegisterDataAsync` only orchestrates
  them (it lifts steps 3–6 out of `GenerateRegisterWorkbookAsync`). This also satisfies R14.6: the
  Preview_Action performs exactly the round-trip set the Download_Action already performs, with no
  additional per-employee or per-period retrieval.

### Key design decision: rows are built from per-employee range aggregates, not filtered from PeriodSummaryDto

`PeriodSummaryRow` exposes `EmployeeName` but **no employee Id**, and it is single-period. Filtering
`GetPeriodSummaryAsync(...).Rows` down to the selected employees by name is fragile (two employees
with the same name collide, no reliable key to match a selection Id to a row) and cannot express a
range at all.

Therefore the Register service builds a single in-memory **`RegisterEmployeeAggregate`** per
employee (defined under Data Models) from the finalised payslips it reads once per Range_Period.
Both the Summary row and the detail sheet read from that one aggregate, so they are always
consistent and employee-Id-accurate. The range figures are exactly what `GetPeriodSummaryAsync`
would compute per employee and per period (both ultimately derive from the finalised payslip's
stored line amounts), so R5 is satisfied while the name-collision bug is avoided.

## Architecture

### Layering

The feature follows the Portal's established MVC + Service + Repository layering:

```
PayrollRegisterController  (HTTP concerns, [Authorize] + [ModuleAccess(Payroll)], AxGet endpoints)
        │
        ▼
IPayrollReportService  (new additive methods: eligible-employees + workbook generation, range-shaped)
        │
        ▼
PayrollRepository      (GetPeriodsByBusinessAsync, GetFinalisedPayslipsForPeriodAsync,
                        GetEmployeesByIdsAsync, batched reference-data lookups)  — reused + 1 additive
        │
        ▼
SQL Server (payroll schema, tenant-scoped by BusinessId)
```

A dedicated **`PayrollRegisterController`** is chosen rather than adding actions to
`PayrollReportController`. Rationale: the existing payroll report controller already follows a
loose one-controller-per-concern style and is large; a dedicated controller keeps the Register's
page + range-refresh + generate endpoints cohesive and makes the additive boundary explicit
(nothing in `PayrollReportController` is touched, directly supporting R12.1/R12.2). It carries the
same class-level attributes (`[Authorize]`, `[ModuleAccess(PortalModules.Payroll)]`) and resolves
`businessId` the same way (`_tenantService.CurrentBusinessId`).

### Request flows

**Page load (R1, R2, R3):** `GET Index` resolves `businessId`, loads periods
(`GetPeriodsByBusinessAsync`), computes the default range (To = latest finalised period via
`GetLatestFinalisedPeriodIdAsync`; From = January of that period's year), and loads the
eligible-employee list for the default range (new service method). Returns a `RegisterPageViewModel`.

**Range change (R3.4):** `AxGetEmployeesForPeriod(fromYear, fromMonth, toYear, toMonth)` returns the
eligible-employee list as JSON for the newly selected range; the client clears the prior selection
and repopulates the selector. (Fires when either the From or the To selector changes.)

**Download (R4):** `AxGetGenerateRegister(fromYear, fromMonth, toYear, toMonth, employeeIds[])`
validates parameters (including From ≤ To), calls `GenerateRegisterWorkbookAsync` (which delegates to
the shared `GetRegisterDataAsync`), and returns either a `File(...)` (`.xlsx`) on success or
`Json(new { success = false, message })` on validation/empty/error outcomes.

**Preview (R15):** `AxGetPreviewRegister(fromYear, fromMonth, toYear, toMonth, employeeIds[])` applies
the **same** validation (From ≤ To, non-empty selection) before the service, calls the shared
`GetRegisterDataAsync`, and returns `Json(new { success = true, data })` with the per-employee preview
DTO on success or `Json(new { success = false, message })` on validation/empty/no-data outcomes. The
client renders the returned per-employee data as ledger tables below the filter (no file).

### Range enumeration + aggregation (core new algorithm)

This is the heart of the reshaped feature. The ordinal value of a `(Year, Month)` pair is
`Year * 12 + Month`; range membership compares ordinals so month arithmetic across year boundaries
is trivial.

1. **Resolve Range_Periods.** Call `GetPeriodsByBusinessAsync(businessId)` **once**. Filter to the
   periods whose ordinal `Year*12+Month` is in `[fromYear*12+fromMonth .. toYear*12+toMonth]`, then
   order **chronologically oldest→newest**. This ordered list is the `Month_Columns` set — its
   length is the number of month columns every detail sheet will have.
2. **Read finalised payslips per period.** For each Range_Period in that ordered list, call
   `GetFinalisedPayslipsForPeriodAsync(periodId, businessId)` **once**. Build a structure keyed by
   `employeeId → (periodId → Payslip)`, and fetch each payslip's line rows
   (`GetEarningLinesByPayslipAsync` / `GetDeductionLinesByPayslipAsync`).
3. **Load reference lookups once for the whole range** (not per period, not per employee):
   `GetAllEarningTypesAsync`, `GetDeductionTypesByBusinessAsync(businessId)`,
   `GetDepartmentsByBusinessAsync(businessId)`, `GetStatusNamesAsync`.
4. **Pivot per employee.** For each selected employee that has at least one finalised payslip in the
   range, build a month-pivot aggregate. For each section (Earnings / Employee Deductions / Employer
   Contributions) hold a dictionary `lineKey → per-month amount` where `lineKey` is the stable
   identity of the line (`EarningTypeId` for earnings, `DeductionTypeId` for deductions/contributions),
   plus that row's `Total` (sum across months) and its `Line_Percentage`.
   - **Line_Percentage** = the latest in-range `Rate` for that line: take the `Rate` from the line in
     the **newest** Range_Period in which that line appears. Earnings have no rate → blank/"-" (R6.6,
     R13.1, R13.2, R13.4).
   - **Employee vs employer split** is performed on the **raw `PayslipDeductionLine`**, filtering its
     `DeductionCategoryTypeId` (1 = employee deduction, 2 = employer contribution) **before** mapping —
     see the AssembleBreakdown note; never on the mapped DTO.
5. **Compute across-range Employee_Totals** on each aggregate: `TotalEarnings`,
   `TotalEmployeeDeductions`, `NetSalary`, `TotalEmployerContributions`, `TotalCost`
   (`TotalEarnings + TotalEmployerContributions`) — each summed across every Range_Period. All these
   sums are computed from per-period line amounts **independently of the displayed `%`**, so a
   mid-range rate change never alters a summed value (R13.3).
6. **Build the Contribution_Type column set for the Summary.** Take the **union** of
   employer-contribution `DeductionTypeId`s (category 2) that appear on any selected employee's
   in-range payslips, ordered by the deduction type's sort order then name (R5.3). Each employee's
   Summary cell for a contribution type = that employee's across-range total for that type, or
   `0.00` if the employee has no amount for it (R5.6).

The Summary sheet and every detail sheet both read from this single set of aggregates, keeping them
consistent (same principle as the pre-range design).

### Download sequence

```mermaid
sequenceDiagram
    participant View as Index.cshtml (fetch + BlockUI)
    participant Ctrl as PayrollRegisterController
    participant Svc as PayrollReportService
    participant Repo as PayrollRepository

    View->>View: BlockUI.show("Generating...")
    View->>Ctrl: GET AxGetGenerateRegister(fromYear,fromMonth,toYear,toMonth, employeeIds[])
    Ctrl->>Ctrl: resolve businessId; validate From<=To and employeeIds not empty
    alt invalid params (From>To or empty selection)
        Ctrl-->>View: Json { success=false, message }
    else valid
        Ctrl->>Svc: GenerateRegisterWorkbookAsync(from.., to.., businessId, employeeIds)
        Svc->>Svc: GetRegisterDataAsync (steps 1-6) -> RegisterData? 
        Svc->>Repo: GetPeriodsByBusinessAsync(businessId)  (once)
        Svc->>Svc: filter to Range_Periods, order oldest->newest (Month_Columns)
        loop for each Range_Period (oldest -> newest)
            Svc->>Repo: GetFinalisedPayslipsForPeriodAsync(periodId, businessId)
            Svc->>Repo: per payslip: GetEarningLinesByPayslipAsync / GetDeductionLinesByPayslipAsync
        end
        Svc->>Repo: reference lookups ONCE (earning types, deduction types, departments, statuses)
        Svc->>Repo: GetEmployeesByIdsAsync(distinct employee Ids in range, businessId)  (once)
        Svc->>Svc: build per-employee RegisterEmployeeAggregate (month-pivot + totals + %)
        alt no eligible selected employees
            Svc-->>Ctrl: null / empty
            Ctrl-->>View: Json { success=false, message }
        else
            Svc->>Svc: build Summary sheet (dynamic contribution columns) + one sheet per employee
            Svc-->>Ctrl: byte[] xlsx
            Ctrl-->>View: File(bytes, xlsx mime, "PayrollRegister_{fromYear}{fromMonth}-{toYear}{toMonth}.xlsx")
        end
    end
    View->>View: BlockUI.hide(); download blob OR SweetAlert2 on JSON failure
```

### Preview sequence (R15)

The Preview_Action shares the same validation and the same data computation; it differs only in that
it returns JSON (never a file) and the client renders per-employee ledger tables instead of
triggering a download.

```mermaid
sequenceDiagram
    participant View as Index.cshtml (fetch + BlockUI)
    participant Ctrl as PayrollRegisterController
    participant Svc as PayrollReportService

    View->>View: client guard From<=To and >=1 employee; BlockUI.show("Loading preview...")
    View->>Ctrl: GET AxGetPreviewRegister(fromYear,fromMonth,toYear,toMonth, employeeIds[])
    Ctrl->>Ctrl: resolve businessId; validate From<=To and employeeIds not empty
    alt invalid params (From>To or empty selection)
        Ctrl-->>View: Json { success=false, message }   (before service, R15.4)
    else valid
        Ctrl->>Svc: GetRegisterDataAsync(from.., to.., businessId, employeeIds)
        alt null (no Range_Period / no eligible selected / no data)
            Svc-->>Ctrl: null
            Ctrl-->>View: Json { success=false, message }   (R15.9, R15.10)
        else RegisterData (RangePeriods + Aggregates)
            Svc-->>Ctrl: RegisterData
            Ctrl->>Ctrl: project to per-employee preview DTO (camelCase, no summary)
            Ctrl-->>View: Json { success=true, data = { employees[] } }   (R15.2)
        end
    end
    View->>View: BlockUI.hide(); on success render one ledger table per employee (R15.5-R15.8),<br/>else SweetAlert2 message and clear preview area (R15.10)
```

### N+1 avoidance over the range (R14.3, R14.6)

`PayrollService.GetPayslipDetailAsync` re-queries reference data (earning types, deduction types,
departments) and the status-name map on **every** call. Calling it per employee or per period would
scale data round-trips with the selection and the range length — violating R14.3.

The Register service instead reads finalised payslips **once per Range_Period** and loads each
reference lookup **once for the whole range**, then assembles every per-employee aggregate in
memory. The explicit round-trip budget is:

```
1   × GetPeriodsByBusinessAsync(businessId)
N   × GetFinalisedPayslipsForPeriodAsync(periodId, businessId)   where N = number of Range_Periods
1   × GetEmployeesByIdsAsync(distinctEmployeeIds, businessId)
4   × reference loads (GetAllEarningTypesAsync, GetDeductionTypesByBusinessAsync,
                       GetDepartmentsByBusinessAsync, GetStatusNamesAsync)
2   × line fetches PER finalised payslip (GetEarningLinesByPayslipAsync + GetDeductionLinesByPayslipAsync)
```

Reference loads are **not** multiplied by period or by employee; employee-name resolution is a
**single** batched `GetEmployeesByIdsAsync`. The per-period finalised-payslip call is bounded by the
number of months in the range (inherent to a multi-month report, not by the number of selected
employees). The per-payslip line fetches are inherent to the data model (one set of lines per
payslip); a future batched line-fetch could be added additively but is not required by R14.3, whose
constraint is on the fixed set of batched *reference* lookups not growing with the selection.

Employee names are resolved via `GetEmployeesByIdsAsync(distinctEmployeeIds, businessId)` over the
distinct employee Ids that have a finalised payslip somewhere in the range — **not** via
`GetActiveEmployeesForPeriodAsync`, which filters `IsActive = 1 AND (EndDate IS NULL OR EndDate >=
periodStart)` and would drop a terminated/inactive employee who still has an in-range finalised
payslip (breaking R3.1 and R8.5).

Per R12.5 this is delivered as a **new** service method, leaving `GetPayslipDetailAsync` and its
callers unchanged.

Because the Preview_Action and the Download_Action both go through the single `GetRegisterDataAsync`,
the preview incurs **exactly** the round-trip budget above and **no** additional per-employee or
per-period retrieval (R14.6): a preview of a selection costs the same data trips as a download of the
same selection. Only the final step differs — the workbook build (ClosedXML) for Download versus a
JSON projection for Preview — neither of which adds data round-trips.

## Components and Interfaces

### 1. New controller: `PayrollRegisterController`

`Portal.Web/Controllers/PayrollRegisterController.cs`

```csharp
[Authorize]
[ModuleAccess(PortalModules.Payroll)]
public class PayrollRegisterController : Controller
{
    private readonly IPayrollReportService _reportService;
    private readonly IPayrollService _payrollService;      // existing callers unaffected
    private readonly ICurrentTenantService _tenantService;

    // GET page (R1, R2, R3). Returns the selection view with a RegisterPageViewModel,
    // or View("Error") on failure to resolve the business / unhandled error (R1.4, R1.7).
    [HttpGet]
    public async Task<IActionResult> Index();

    // GET JSON (R3.4). Returns the eligible-employee options for the given range so the
    // client can refresh the employee selector when either From or To changes.
    // Returns Json { success = true, data = RegisterEmployeeOption[] } or { success = false, message }.
    [HttpGet]
    public async Task<IActionResult> AxGetEmployeesForPeriod(int fromYear, int fromMonth, int toYear, int toMonth);

    // GET file download (R4.1-R4.5). Produces the single .xlsx workbook for the range.
    // On success: File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    //             "PayrollRegister_{fromYear}{fromMonth}-{toYear}{toMonth}.xlsx").
    // On From>To, empty selection, no-eligible selection, or unhandled error:
    //             Json { success = false, message } (R4.3, R9, R10).
    [HttpGet]
    public async Task<IActionResult> AxGetGenerateRegister(
        int fromYear, int fromMonth, int toYear, int toMonth, int[] employeeIds);

    // GET JSON preview (R15.2-R15.4, R15.9-R15.12). The Preview_Action. Validates From<=To (ordinal)
    // and employeeIds non-empty BEFORE the service — identical to the Download_Action — returning a
    // JSON failure otherwise. Calls the shared GetRegisterDataAsync; null -> Json { success=false,
    // message } (no eligible / empty range / no data). On success -> Json { success=true, data } where
    // data is the per-employee preview DTO. Never returns a file.
    [HttpGet]
    public async Task<IActionResult> AxGetPreviewRegister(
        int fromYear, int fromMonth, int toYear, int toMonth, int[] employeeIds);
}
```

Controller behavior notes:

- `Index` and both `AxGet` endpoints resolve `businessId = _tenantService.CurrentBusinessId`.
- `Index` wraps logic in `try/catch (Exception ex)` and returns `View("Error")` on failure
  (mirrors `PayrollReportController` page actions; R1.4, R1.7).
- `AxGetGenerateRegister` validates required parameters **before** invoking generation: the range
  must satisfy From `(Year,Month)` ≤ To `(Year,Month)` (compared by ordinal) and `employeeIds` must
  be non-empty; otherwise return a JSON failure naming the invalid condition (From later than To, or
  no employees) without calling the service (R4.3, R10.5).
- `AxGetPreviewRegister` applies the **same** pre-service validation as `AxGetGenerateRegister`:
  From `(Year,Month)` ≤ To `(Year,Month)` (ordinal) and `employeeIds` non-empty; otherwise it returns
  `Json { success = false, message }` naming the invalid condition **without** calling the service
  (R15.4). On valid input it calls `GetRegisterDataAsync`; a `null` result returns
  `Json { success = false, message }` ("no data available for the current selection" / empty range /
  no eligible employees — R15.9, R15.10). On a non-null result it returns
  `Json { success = true, data = <preview DTO> }` (R15.2). It **never** returns a `File` — the
  Preview_Action is always a JSON response.
- On a rethrown service exception, the controller (both the Download and Preview actions) logs the
  error (via the existing `SystemLoggerExtensions` logging path) and returns a generic JSON failure
  that does not leak stack traces (R10.1, R10.4).
- Every catch uses `catch (Exception ex)` with the variable, per the coding golden rules. Both AJAX
  endpoints follow the no-logger JSON-failure pattern (catch, return `Json(new { success = false,
  message })`).

The preview endpoint shapes the shared `RegisterData` into a lightweight, JSON-friendly preview DTO
(or serializes the aggregates directly with camelCase). Per employee, in selection order:

```jsonc
{
  "success": true,
  "data": {
    "employees": [
      {
        "employeeId": 0,
        "employeeName": "",
        "rangePeriods": [ { "periodId": 0, "year": 0, "month": 0 } ],  // oldest -> newest
        "earnings":              [ { "name": "", "amountByPeriodId": { "<periodId>": 0.0 }, "total": 0.0, "percentage": null } ],
        "employeeDeductions":    [ { "name": "", "amountByPeriodId": { "<periodId>": 0.0 }, "total": 0.0, "percentage": 0.0 } ],
        "employerContributions": [ { "name": "", "amountByPeriodId": { "<periodId>": 0.0 }, "total": 0.0, "percentage": 0.0 } ],
        "totalEarnings": 0.0,
        "totalEmployeeDeductions": 0.0,
        "netSalary": 0.0,
        "totalEmployerContributions": 0.0,
        "totalCost": 0.0
      }
    ]
  }
}
```

The DTO is a one-to-one projection of `RegisterData.Aggregates` (and each aggregate's
`RangePeriods`) — it adds no computation, so the previewed figures are exactly the aggregate figures
the workbook is built from (R15.12). There is deliberately **no** summary/grand-totals block in the
preview payload: the preview is per-employee only (R15.5).

### 2. New additive service methods on `IPayrollReportService` / `PayrollReportService`

```csharp
/// <summary>
/// Returns the employees eligible for the Payroll Register over the given range: only employees
/// that have a Finalised_Payslip (PayslipStatusTypeId IN (3,5)) in ANY Range_Period for the
/// business, projected to a lightweight option (EmployeeId + EmployeeName) and sorted alphabetically
/// by name. Enumerates Range_Periods via GetPeriodsByBusinessAsync then reads
/// GetFinalisedPayslipsForPeriodAsync once per Range_Period. Additive: does not alter the protected
/// operations. (R3.1, R3.6, R8.5)
/// </summary>
Task<List<RegisterEmployeeOption>> GetRegisterEligibleEmployeesAsync(
    int fromYear, int fromMonth, int toYear, int toMonth, int businessId);

/// <summary>
/// Builds the single Payroll Register .xlsx workbook for the selected range and employees, scoped to
/// the business. Enumerates Range_Periods once, reads finalised payslips once per Range_Period, loads
/// reference lookups once for the whole range (N+1 avoidance, R14.3), includes only selected
/// employees that have a Finalised_Payslip in at least one Range_Period (R9.3), builds the Summary
/// sheet (index 1, dynamic Contribution_Type columns) and one detail sheet per eligible selected
/// employee. Returns null (or empty byte[]) when no selected employee has an in-range finalised
/// payslip, so the controller can decline with a JSON message (R4.7, R9.5). Rethrows on data-access
/// errors (R10.3).
/// </summary>
Task<byte[]?> GenerateRegisterWorkbookAsync(
    int fromYear, int fromMonth, int toYear, int toMonth, int businessId, int[] employeeIds);
```

Internal (private) helpers in `PayrollReportService` — revised for the range:

- `ResolveRangePeriodsAsync(fromYear, fromMonth, toYear, toMonth, businessId)` → loads
  `GetPeriodsByBusinessAsync` once, filters by ordinal `Year*12+Month` within `[from..to]`, returns
  the Range_Periods ordered **oldest→newest** (defines `Month_Columns`).
- `BuildEmployeePayslipMapAsync(rangePeriods, businessId)` → revised from single-period: loops the
  Range_Periods, calling `GetFinalisedPayslipsForPeriodAsync` once per period, and returns a nested
  `Dictionary<int employeeId, Dictionary<int periodId, Payslip>>` plus the batched employee-name
  lookup obtained via `GetEmployeesByIdsAsync` over the distinct in-range employee Ids (so
  terminated/inactive employees with an in-range finalised payslip are still named — never via
  `GetActiveEmployeesForPeriodAsync`).
- `LoadRegisterReferenceLookupsAsync(businessId)` → revised to load the four reference lookups
  **once for the whole range** (earning types, deduction types, departments, status names); it is
  now called once per generation, not once per period.
- `AssembleBreakdownAsync(employeeId, periodPayslips, rangePeriods, lookups)` → revised to produce a
  `RegisterEmployeeAggregate` (below) by pivoting each section's lines across the Range_Periods.
  **The employee-vs-employer deduction split MUST be performed on the raw `PayslipDeductionLine`
  entity, filtering on its `DeductionCategoryTypeId` (1 = employee deduction → EmployeeDeductions,
  2 = employer contribution → EmployerContributions) BEFORE mapping to `DeductionLineDto`.** This
  mirrors the existing `GetPayslipDetailAsync`, where the split happens on the raw entity and the
  mapped `DeductionLineDto` intentionally leaves `DeductionCategoryTypeId` unset (0). Never filter on
  the DTO's `DeductionCategoryTypeId` — it is unset (0), so doing so would misclassify every
  deduction into a single bucket. This is a correctness requirement, not a style note.
- `BuildRegisterWorkbook(rangeLabel, orderedAggregates, contributionTypeColumns)` → the ClosedXML
  builder (below).
- `BuildEmployeeDetailSheet(workbook, aggregate, rangePeriods)` → revised to emit month columns +
  Total + "%" (below).
- `SanitizeWorksheetName(name)` → unchanged in intent (sanitise + truncate ≤ 31 + uniqueness).

The service constructor is unchanged; the new methods use the already-injected `PayrollRepository`.

### 3. Workbook-builder helper (private, in `PayrollReportService`)

Produces the Summary sheet + one sheet per eligible selected employee, reusing the exact styling from
`ExportPeriodSummaryToExcelAsync`:

- header fill `ClosedXML.Excel.XLColor.FromHtml("#0D5EA6")`, font white + bold;
- monetary cells number format `"#,##0.00"`;
- `worksheet.Columns().AdjustToContents()`;
- bold totals rows;
- `workbook.SaveAs(stream)` → `stream.ToArray()`.

It also owns worksheet-name sanitation/truncation/uniqueness (R7, detailed below).

### 4. Employee→payslip resolution over the range (R6, R9.3, R14.1/2)

1. Resolve the Range_Periods (ordered oldest→newest).
2. For each Range_Period call `GetFinalisedPayslipsForPeriodAsync` **once**.
3. Group into `Dictionary<int employeeId, Dictionary<int periodId, Payslip>>`.
4. For each selected `employeeId`, in the order supplied (R6.1, R7.5):
   - if the employee has **no** finalised payslip in **any** Range_Period → omit entirely (no row,
     no sheet) (R9.3);
   - otherwise build the full `RegisterEmployeeAggregate` + detail sheet. An employee with a
     finalised payslip for some months but not others is included; the absent months render `0.00`
     (R9.4). Where a single Range_Period unexpectedly yields more than one finalised payslip for the
     same employee, their per-period amount for that month is the sum of those payslips' lines (the
     pivot naturally accumulates), so the month-column and totals stay well-defined.

The eligible-employee **selector** list (page + range-refresh) is derived from this same in-range
finalised set joined to batched employee names fetched via `GetEmployeesByIdsAsync` (ONE call over
the distinct in-range employee Ids), so only employees with an in-range finalised payslip are
listable (R3.1), sorted alphabetically by name. The names are deliberately **not** sourced from
`GetActiveEmployeesForPeriodAsync` (R3.1, R8.5).

### 5. View: `Views/PayrollRegister/Index.cshtml`

MyChair design system + layout-standards:

- Topbar with eyebrow label, 42px heading, muted description.
- A filter card `<section class="glass card-pad" style="margin-bottom:22px;">` containing:
  - a **From** month-year `<select>` and a **To** month-year `<select>`, both populated from the
    Current_Business periods (R2.1: sorted by `(Year, Month)` descending — newest first; each option
    labelled with its year + month and its finalised/unfinalised state **derived from the `Status`
    string** — `PayslipPeriodDto` has no `StartDate` and no boolean finalised flag). Defaults: To =
    latest finalised period; From = January of that period's year (R2.2);
  - a multi-select employee control with a **Select All** toggle (R3.3);
  - two action buttons (both disabled when no eligible employees — R3.6, R15.1):
    - a **Preview** button (`btn-secondary`) → triggers the Preview_Action;
    - a **Download** button (`btn-primary`) → triggers the Download_Action (the exact current
      generate/blob-or-JSON behavior).
- A main content card `<section class="glass card-pad">` for status/help text and the no-finalised /
  no-eligible / no-range-periods messages (R2.3, R2.6, R3.6), and — below those — a **preview
  container** into which the per-employee ledger tables are rendered (empty until a Preview_Action
  succeeds).

Client JS — vanilla `fetch`, `BlockUI`, SweetAlert2, **no jQuery, no native dialogs** (R11):

- On **either** From or To change → **client-side guard**: if From `(Year,Month)` > To
  `(Year,Month)`, show an inline validation error that From must not be later than To and do **not**
  refresh/fire (R2.4); otherwise `BlockUI.show`, `fetch AxGetEmployeesForPeriod` with the four range
  params, `BlockUI.hide`, repopulate the selector and **clear prior selection** (R3.4); on JSON
  failure show a SweetAlert2 error.
- On **Download** (unchanged current behavior):
  1. client-side guards: if From > To, SweetAlert2/inline warning, do not fire (R2.4); if no employee
     selected, SweetAlert2 warning, do not fire (R3.5, R4.3);
  2. `BlockUI.show('Generating register...')` (R11.1, R14.5 — continuous);
  3. `fetch AxGetGenerateRegister` with query string `fromYear`/`fromMonth`/`toYear`/`toMonth` +
     repeated `employeeIds`;
  4. inspect the response: if `response.ok` **and** content-type is the spreadsheet mime → read
     `blob()` and trigger a browser download (anchor + object URL; the filename is parsed from the
     `Content-Disposition` header as the existing exports already do); if content-type is JSON
     (failure) → parse and show a SweetAlert2 **error** with the returned message (R11.5);
  5. `BlockUI.hide()` in both success and error/catch paths (R11.3, R11.4); on network/non-JSON
     failure show a generic SweetAlert2 error (R11.6).

  This blob-or-JSON handling is required because the download endpoint returns a file on success but
  a JSON body on failure, while BlockUI must wrap the whole call.

- On **Preview** (R15):
  1. client-side guards **identical to Download**: if From > To, warning, do not fire (R2.4, R15.4);
     if no employee selected, warning, do not fire (R3.5, R15.4);
  2. `BlockUI.show('Loading preview...')` (R15.11);
  3. `fetch AxGetPreviewRegister` with the same query string (`fromYear`/`fromMonth`/`toYear`/`toMonth`
     + repeated `employeeIds`); this endpoint always returns JSON;
  4. `BlockUI.hide()` (R15.11); then:
     - on `success === true` → render the preview (below) into the preview container (R15.5);
     - on `success === false` → **clear** the preview container and show a SweetAlert2 message
       describing the outcome (invalid range / no eligible employees / no data — R15.10);
     - on network/non-JSON failure → clear the preview container and show a generic SweetAlert2 error
       (R11.6). No native dialogs anywhere (R11.7).

#### Preview rendering (client, from the JSON `employees[]`)

For each employee object in the returned order (R15.5), build **one ledger table** in the preview
container:

- a heading showing the employee name (R15.5);
- the three sections — **Earnings**, **Employee Deductions**, **Employer Contributions** — each as a
  block of rows (R15.5). Columns, left to right: line-item **name**; one column **per Range_Period**
  (header = abbreviated month + year, built in JS from that employee's `rangePeriods`, oldest→newest
  — R15.6); a **Total** column; and a **"%"** column (R15.6). Each month cell reads
  `amountByPeriodId[periodId]`, defaulting to **`0.00`** when the period key is absent (R15.8). The
  "%" cell shows `percentage` for deduction/contribution lines and is **blank** for earnings
  (`percentage == null`) (R15.6);
- a **section total row** per section (R15.7);
- a per-employee **Employee_Totals** block: Total Earnings, Total Employee Deductions, Net Salary,
  Total Employer Contributions, Total Cost (R15.7).
- styling matches the MyChair look — a small CSS block in the view reuses the `#0D5EA6` header fill
  with white bold text for the table headers (consistent with the workbook/export styling), monetary
  cells right-aligned and formatted to two decimals.
- each table is wrapped in a **horizontally scrollable container** (e.g. `overflow-x:auto`) because a
  full-year range produces 12 month columns + Total + "%" and would otherwise break the page layout
  (R14.7).

The month-column headers are derived in JS from each employee's own `rangePeriods` list (month
abbreviation + year), so the table width tracks the actual range. The preview renders **per-employee
only** — it never renders the Summary/grand-totals table (R15.5).

## Data Models

Reuse existing DTOs wherever possible (`EarningLineDto`, `DeductionLineDto`, `PayslipDetailDto` as a
per-period conceptual shape). The range pivot needs one new in-memory aggregate.

### New: `RegisterEmployeeOption` (lightweight selector option)

`Portal.Infrastructure/Models/Payroll/RegisterDtos.cs`

```csharp
public class RegisterEmployeeOption
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
}
```

Used by the selector (page + AJAX refresh). `EmployeeId` is the reliable key the summary-row-by-name
collision motivated — it is what the generate endpoint receives as `employeeIds[]`.

### New: `RegisterEmployeeAggregate` (in-memory range aggregate — the single source for both sheets)

`Portal.Infrastructure/Models/Payroll/RegisterDtos.cs`

```csharp
// One pivoted row within a section: a line item with its per-period amounts, its range total,
// and the latest in-range rate ("%" — null/blank for earnings).
public class RegisterLineRow
{
    public int LineKey { get; set; }                 // EarningTypeId or DeductionTypeId
    public string Name { get; set; } = string.Empty; // display name (earning/deduction type)
    public Dictionary<int, decimal> AmountByPeriodId { get; set; } = new(); // periodId -> amount
    public decimal Total { get; set; }               // sum across Range_Periods
    public decimal? Percentage { get; set; }         // latest in-range Rate; null for earnings
}

public class RegisterEmployeeAggregate
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;

    // The ordered Range_Periods define the Month_Columns (oldest -> newest).
    public List<PayslipPeriod> RangePeriods { get; set; } = new();

    public List<RegisterLineRow> Earnings { get; set; } = new();            // % blank
    public List<RegisterLineRow> EmployeeDeductions { get; set; } = new();  // category 1
    public List<RegisterLineRow> EmployerContributions { get; set; } = new(); // category 2

    // Across-range Employee_Totals (independent of displayed %):
    public decimal TotalEarnings { get; set; }
    public decimal TotalEmployeeDeductions { get; set; }
    public decimal NetSalary { get; set; }
    public decimal TotalEmployerContributions { get; set; }
    public decimal TotalCost { get; set; }           // TotalEarnings + TotalEmployerContributions
}
```

Both the Summary row and the detail sheet read exclusively from this aggregate (consistency). The
Summary's per-contribution cell for employee E and contribution type T = the `Total` of E's
`EmployerContributions` row whose `LineKey == T`, or `0.00` when absent (R5.6).

### New: `RegisterData` (the shared computation result — single source for Download and Preview)

`Portal.Infrastructure/Models/Payroll/RegisterDtos.cs`

```csharp
// The result of GetRegisterDataAsync: the ordered Range_Periods and the per-employee aggregates.
// The workbook (Download) is built from these; the preview JSON (Preview) is projected from these.
// Both actions therefore agree cell-for-cell (R15.12, R12.6).
public class RegisterData
{
    public List<PayslipPeriod> RangePeriods { get; set; } = new();           // oldest -> newest
    public List<RegisterEmployeeAggregate> Aggregates { get; set; } = new(); // selection order, eligible only
}
```

`GetRegisterDataAsync` returns `null` on any decline path (bad business id, empty selection, no
Range_Periods, no eligible aggregates); a non-null `RegisterData` always carries at least one
aggregate.

### Preview JSON projection (Preview_Action payload)

The Preview_Action projects `RegisterData.Aggregates` to a JSON-friendly, camelCase shape (shown in
the controller section): per employee `{ employeeId, employeeName, rangePeriods: [{ periodId, year,
month }], earnings / employeeDeductions / employerContributions: [{ name, amountByPeriodId, total,
percentage }], totalEarnings, totalEmployeeDeductions, netSalary, totalEmployerContributions,
totalCost }`. `RegisterEmployeeAggregate`, `RegisterLineRow`, and `PayslipPeriod` are already
JSON-serializable (`PayslipPeriod` exposes `Id`, `Year`, `Month`), so the aggregates MAY be
serialized directly with camelCase settings; a thin dedicated DTO is equivalent. The projection adds
no computation — every field is copied straight from the aggregate — preserving the cell-for-cell
guarantee (R15.12).

### New: `RegisterPageViewModel` (page model)

`Portal.Web/Models/Payroll/RegisterPageViewModel.cs`

```csharp
public class RegisterPageViewModel
{
    public List<PayslipPeriodDto> Periods { get; set; } = new();  // GetPeriodsByBusinessAsync, (Year,Month) desc
    public int? DefaultToYear { get; set; }                       // latest finalised period year
    public int? DefaultToMonth { get; set; }                      // latest finalised period month
    public int? DefaultFromYear { get; set; }                     // = DefaultToYear
    public int DefaultFromMonth { get; set; } = 1;                // January of that year (R2.2)
    public List<RegisterEmployeeOption> EligibleEmployees { get; set; } = new(); // for the default range
    public bool HasFinalisedPeriod { get; set; }                  // drives the R2.3 message
}
```

Existing reused models: `PayslipPeriodDto`, `PayslipDetailDto`, `EarningLineDto`, `DeductionLineDto`,
`PeriodSummaryDto`/`PeriodSummaryRow` (conceptual reference only).

## Worksheet Layout

### Summary sheet — name `"Summary {FromMon}-{ToMon} {Year}"` (truncated ≤ 31 chars), sheet index 1 (R5.1, R7.1)

The Summary sheet has a **dynamic column count**: fixed columns on either side of a variable block of
one column per Contribution_Type.

Header row (fill `#0D5EA6`, white bold), in order (R5.2):

| Col               | Header                        |
|-------------------|-------------------------------|
| 1                 | Employee Name                 |
| 2                 | Total Earnings                |
| 3                 | Total Employee Deductions     |
| 4                 | Net Salary                    |
| 5 .. (5+K−1)      | one column per Contribution_Type (K columns) |
| 5+K               | Total Employer Contributions  |
| 6+K               | Total Cost                    |

- **Contribution_Type columns** = the **union** of employer-contribution `DeductionType`s
  (category 2) appearing on any selected employee's in-range finalised payslips, ordered by the
  deduction type's sort order then by name (R5.3). `K` is the size of that union.
- One data row per eligible selected employee; the monetary columns are formatted `"#,##0.00"`
  (R5.4, R5.8). Each contribution-type cell = that employee's across-range total for that type, or
  **`0.00`** when the employee has no amount for it (R5.6). `Net Salary`, `Total Employer
  Contributions`, and `Total Cost` are the aggregate's across-range totals.
- **Grand_Totals_Row**: label "TOTALS" in column 1 (bold); every monetary column (Total Earnings,
  Total Employee Deductions, Net Salary, **each** Contribution_Type column, Total Employer
  Contributions, Total Cost) = the arithmetic sum of that column across the employee rows, so each
  Contribution_Type column's total is that contribution's grand total across all selected employees
  "on the first page" (R5.7). Whole row bold, monetary cells `"#,##0.00"`.
- Only selected (and eligible) employees appear; others excluded from rows and totals (R5.5, R9.3).
- `Columns().AdjustToContents()`.

### Per-employee detail sheet — one per eligible selected employee, after Summary (R6.1, R7.5)

The detail sheet is laid out as a ledger: line items are rows, Range_Periods are columns. Its width
grows with the range — a full year is **12 month columns + Total + "%"**. Columns, left to right:

| Col            | Header                                   |
|----------------|------------------------------------------|
| 1              | line item name (Earning / Deduction / Contribution) |
| 2 .. (2+N−1)   | one Month_Column per Range_Period, oldest→newest (N columns) |
| 2+N            | Total (sum of the row's Month_Columns)   |
| 3+N            | % (Line_Percentage)                      |

Each section header row uses fill `#0D5EA6`, white bold; all monetary cells `"#,##0.00"` (R6.10):

1. **Identity block** — employee name, position, department, and the range label.
2. **Earnings section** — header row, then one row per distinct earning line item that appears for
   the employee across the range (R6.2). Each Month_Column holds that line's amount for that period
   (`0.00` when the line is absent that month — R6.4). Total = sum of the row's months. The "%" cell
   is **blank or "-"** because earnings have no rate (R6.6, R13.4). If the section has no line items
   → header row + a single "No earnings" row + a section total of 0.00 (R6.9).
3. **Employee Deductions section** — one row per distinct employee-deduction (category 1) line item
   (R6.2). "%" = the line's `Line_Percentage` (latest in-range `Rate`) (R6.5, R13.1, R13.2). Missing
   month = `0.00` (R6.4). Empty section → header + "No employee deductions" + 0.00 (R6.9).
4. **Employer Contributions section** — one row per distinct employer-contribution (category 2) line
   item (R6.2). "%" = `Line_Percentage` (R6.5). Missing month = `0.00` (R6.4). Empty section →
   header + "No employer contributions" + 0.00 (R6.9).

   The `EmployeeDeductions` and `EmployerContributions` rows populating sections 3 and 4 are split in
   `AssembleBreakdownAsync` on the **raw `PayslipDeductionLine.DeductionCategoryTypeId`** (1 vs 2)
   before mapping — see the helper note above — never on the mapped DTO's unset
   `DeductionCategoryTypeId`.

5. **Section total rows** — each of the three sections ends with a total row that sums **each
   Month_Column** and the **Total** column for that section (R6.7), bold.
6. **Employee_Totals block** — Total Earnings, Total Employee Deductions, Net Salary, Total Employer
   Contributions, Total Cost, each summed across the Range_Periods (R6.8), each `"#,##0.00"`.
7. `Columns().AdjustToContents()`.

Every summed value (row Total, section total, Employee_Totals, and the Summary figures derived from
them) is computed from the per-period line amounts **independently of the displayed `%`** (R13.3).

### Worksheet naming (R7)

- Summary: `"Summary {FromMon}-{ToMon} {Year}"`, truncated so the whole name ≤ 31 chars (R7.1).
- Detail: the employee name, sanitized and truncated to ≤ 31 chars (R7.2, R7.4):
  - remove/replace the .xlsx-prohibited characters: backslash `\`, forward slash `/`, question mark
    `?`, asterisk `*`, open/close square brackets `[` `]`, colon `:`;
  - truncate to ≤ 31 chars.
- Uniqueness: if two sanitized names collide, append a numeric suffix starting at `2`, incrementing,
  trimming the base so the suffixed name stays ≤ 31 chars (R7.3).
- A sheet is always retained rather than failing generation (R7.4).

## Error Handling

| Condition | Where | Behavior | Reqs |
|-----------|-------|----------|------|
| Unauthenticated / no module access | attributes | `[Authorize]` redirect / authorization failure; view not rendered | R1.1, R1.2 |
| `CurrentBusinessId` unresolved on page load | `Index` | `View("Error")`, no selection view | R1.4 |
| Unhandled error on page load | `Index` | `catch (Exception ex)` → `View("Error")` | R1.7 |
| No finalised period for business | `Index`/VM | selectors rendered, no pre-selection, R2.3 message, unfinalised periods still selectable | R2.3 |
| From (Year,Month) later than To (Year,Month) | view + `AxGetGenerateRegister` | client-side inline validation blocks firing; server also rejects before service with JSON `success=false` "From must not be later than To" | R2.4, R4.3 |
| Submitted range resolves to no Range_Period for the business | service | empty result → JSON `success=false` "selected range contains no periods for the current business"; selection unchanged | R2.6, R8.3 |
| No employee selected | `AxGetGenerateRegister` | validate before service; JSON `success=false` "at least one employee is required"; no generation | R3.5, R4.3, R10.5 |
| No eligible employees for range | selector/VM + generate | empty selector, Generate disabled; if forced, JSON `success=false` "no eligible employees for the selected range" | R3.6, R9.1 |
| Zero finalised payslips across all Range_Periods | generate | service returns null/empty → JSON `success=false` identifying the range | R4.7, R9.1 |
| Selected employee not owned / no in-range finalised payslip | service | omitted from workbook; generation continues | R8.4, R9.3, R14.2 |
| After filtering, zero selected have an in-range finalised payslip | generate | JSON `success=false` "no data available for the current selection" | R9.5, R8.6 |
| `CurrentBusinessId` null/empty in service | service | abort, no data, surface business-not-determined error | R8.2 |
| Generation exceeds 120s | service | terminate, error response "generation exceeded the time limit", no partial workbook | R14.4 |
| Data-access exception in service | service | `catch (Exception ex) { throw; }` rethrow, type/stack preserved | R10.3 |
| Rethrown exception reaches controller | controller | log via SystemLoggerExtensions, then JSON `success=false` generic message, no stack trace | R10.1, R10.4 |
| Success (Download) | controller | `File(bytes, xlsx mime, "PayrollRegister_{fromYear}{fromMonth}-{toYear}{toMonth}.xlsx")` — not JSON | R4.1, R4.4, R4.5, R10.2 |
| JSON failure reaches client | view | BlockUI.hide + SweetAlert2 error with message | R11.5 |
| Network / non-JSON failure | view | BlockUI.hide + SweetAlert2 generic error | R11.6 |
| Preview: From>To or empty selection | view + `AxGetPreviewRegister` | client-side guard blocks firing; server rejects before service with JSON `success=false` "a valid range and at least one employee are required" | R15.4 |
| Preview: no Range_Period / no eligible selected / no data | `AxGetPreviewRegister` | `GetRegisterDataAsync` returns null → JSON `success=false` message; client clears preview area + SweetAlert2 | R15.9, R15.10 |
| Preview: success | controller | `Json { success=true, data = { employees[] } }` — never a file | R15.2, R15.12 |
| Preview: render-time nothing to show | view | client renders no ledger table and shows a SweetAlert2 message describing the outcome | R15.10 |
| Preview: unhandled service exception | controller | log via SystemLoggerExtensions, JSON `success=false` generic message, no stack trace | R10.1, R10.4 |

No partial workbook is persisted or transmitted on any decline/failure path (R9.2, R14.4).

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a
system — a formal statement about what the system should do. Properties serve as the bridge between
human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Sheet-count invariant

*For any* selected range and any set of selected employee Ids, the number of worksheets in the
generated workbook equals 1 (the Summary sheet) plus the number of selected employees that have at
least one finalised payslip in at least one Range_Period for the business.

**Validates: Requirements 6.1, 7.5, 14.1, 14.2, 9.3**

### Property 2: Grand totals equal the sum of summary rows (every column, including each contribution)

*For any* generated workbook, each monetary column of the Grand_Totals_Row — Total Earnings, Total
Employee Deductions, Net Salary, **each Contribution_Type column**, Total Employer Contributions, and
Total Cost — equals the arithmetic sum of that column across all Summary employee rows.

**Validates: Requirements 5.7, 5.3**

### Property 3: Tenant isolation

*For any* generated workbook, every employee row on the Summary sheet and every detail sheet
corresponds to an employee of the Current_Business and to a Range_Period of the Current_Business; no
record of any other business, or of any period outside the Period_Range, appears.

**Validates: Requirements 8.1, 8.4, 8.5, 2.5**

### Property 4: Ineligible selections are omitted

*For any* set of selected employee Ids, any selected employee that has no finalised payslip in any
Range_Period (including employees not belonging to the business) is absent from both the Summary
rows/totals and the detail sheets, and generation still completes for the remaining employees.

**Validates: Requirements 9.3, 14.2, 8.4**

### Property 5: Worksheet names are valid and unique

*For any* generated workbook, every worksheet name is at most 31 characters, contains none of the
.xlsx-prohibited characters (`\ / ? * [ ] :`), and is unique within the workbook.

**Validates: Requirements 7.1, 7.2, 7.3, 7.4**

### Property 6: Summary row figures match the employee's detail figures

*For any* eligible selected employee, that employee's Summary row values (Total Earnings, Total
Employee Deductions, Net Salary, each Contribution_Type cell, Total Employer Contributions, Total
Cost) equal the corresponding across-range totals rendered on that employee's detail sheet.

**Validates: Requirements 5.4, 6.8**

### Property 7: Each detail row Total equals the sum of its Month_Columns

*For any* detail sheet and any line-item row in any section, the value in that row's Total column
equals the arithmetic sum of that row's Month_Column values across all Range_Periods (with absent
months counted as 0.00).

**Validates: Requirements 6.3, 6.4, 9.4**

### Property 8: The Contribution_Type column set is the union across selected employees

*For any* generated workbook, the set of Contribution_Type columns on the Summary sheet equals
exactly the union of employer-contribution (category 2) deduction types appearing on the selected
employees' in-range finalised payslips, with no duplicates and none missing.

**Validates: Requirements 5.2, 5.3**

### Property 9: A mid-range rate change does not alter any summed amount

*For any* employee whose deduction or employer-contribution line's rate changes within the
Period_Range, altering the rate in intermediate periods changes only the displayed "%"
(Line_Percentage, which equals the latest in-range rate) and leaves every summed amount — each row
Total, each section total, each Employee_Totals figure, and every Grand_Totals_Row value — unchanged.

**Validates: Requirements 13.2, 13.3, 13.1**

### Property 10: Preview and Download share one data source

*For any* valid selection (range + employee Ids), the per-employee figures returned in the Preview
JSON — each employee's ordered Range_Periods, every section line's per-period amounts and Total, each
line's "%", and the five Employee_Totals — equal the corresponding per-employee figures the workbook
is built from, because both the Preview_Action and the Download_Action read the identical
`RegisterData` (the same ordered Range_Periods and the same `RegisterEmployeeAggregate` list) produced
by the single `GetRegisterDataAsync`. The two outputs can therefore never drift for the same
selection.

**Validates: Requirements 15.12, 12.6**

## Testing Strategy

### Why property-based testing applies

The workbook generation is effectively a pure transformation: given a per-Range_Period
finalised-payslip set and reference lookups (all mockable), it produces deterministic bytes whose
structure (sheet count, names, month columns, contribution columns, totals, "%") is a function of the
input. The invariants above ("for all selections/ranges…") are naturally property-shaped, so PBT is
appropriate for the service. The view/JS layer (BlockUI/SweetAlert2/blob download) and the
attribute-based auth are **not** PBT targets — they are example/integration concerns.

### Unit tests (xUnit + Moq), mirroring `PayrollReportServiceTests`

Use `Mock<PayrollRepository>(MockBehavior.Loose, new object[] { null! })` (the repository methods
used here — `GetPeriodsByBusinessAsync`, `GetFinalisedPayslipsForPeriodAsync`,
`GetEmployeesByIdsAsync`, `GetEarningLinesByPayslipAsync`, `GetDeductionLinesByPayslipAsync`,
`GetAllEarningTypesAsync`, `GetDeductionTypesByBusinessAsync`, `GetDepartmentsByBusinessAsync`,
`GetStatusNamesAsync` — are `virtual`, so they are mockable without a DbContext). ClosedXML can
re-open the produced `byte[]` (`new XLWorkbook(new MemoryStream(bytes))`) to assert sheet names,
month columns, contribution columns, the "%" column, and cell values. No `PortalDbContext` is needed.

Example-based and edge-case tests:

- workbook bytes non-empty for a valid single-employee, multi-month range;
- sheet count = 1 + (eligible selected employees) for 1, several, and all-selected cases;
- grand totals (including each contribution column) = sum of rows;
- a selected employee present in some months but not others → included, absent months render 0.00;
- a selected employee with no in-range finalised payslip → omitted and generation still succeeds;
- empty range (no Range_Periods) → service returns null/empty (controller declines, R2.6);
- no-eligible-employees → `GetRegisterEligibleEmployeesAsync` returns empty;
- tenant filtering → payslips of another business (different `BusinessId`) never appear;
- empty earning/deduction/contribution section → section header + "No …" row + 0.00 total;
- worksheet-name sanitation (prohibited chars removed) and uniqueness suffixing (duplicate names →
  `name`, `name 2`, `name 3`);
- "%" = latest in-range rate when a rate changes mid-range; blank for earnings;
- a mid-range rate change leaves summed totals unchanged (anchors Property 9).

### Property-based tests

Use **FsCheck.Xunit** (FsCheck 2.16.6 — `FsCheck.dll`, `FsCheck.Xunit.dll` are in `build_check`),
following the codebase's established property-test pattern (e.g.
`CreditNoteAmountComputationPropertyTests`): each property is a `[Property(MaxTest = 100)]`-attributed
method that returns an `FsCheck.Property` built with `Prop.ForAll(<Gen>.ToArbitrary(), body)`, where
the generators are composed from `Gen.Choose` / `Gen.Elements` / `Gen.ListOf`. New generators needed
for the range:

- a **multi-period range** generator: a random `(fromYear,fromMonth)..(toYear,toMonth)` ordered pair
  plus the matching set of `PayslipPeriod`s (so Month_Columns vary from 1 to 12+);
- a **per-month amount** generator: random earning/deduction/contribution lines per period, including
  lines that appear in only some months (to exercise 0.00 filling) and lines whose `Rate` changes
  across periods (to exercise latest-in-range "%" and Property 9);
- random eligible/ineligible employee sets and random duplicate/long/prohibited-char names.

`MaxTest = 100` expresses the minimum **100 iterations** the house way. The body builds the workbook
via the mocked repository, re-opens the bytes with ClosedXML, and asserts the invariant — including
asserting the month-column count, the contribution-column set, and the "%" column where relevant. Tag
each test with a comment referencing the design property, e.g.:

```csharp
// Feature: payroll-register-report, Property 1: Sheet count = 1 + eligible selected employees
[Property(MaxTest = 100)]
public Property SheetCountEqualsOnePlusEligibleSelected()
    => Prop.ForAll(RegisterInputGen.ToArbitrary(), input => { /* build + re-open + assert */ });
```

Map one property-based test per correctness property (Properties 1–10). Properties 2 (grand totals,
including each contribution column), 7 (row Total = sum of months), 8 (contribution-column union),
9 (rate-change invariance), and 10 (preview/download share one data source) are the highest-value
generators.

**Property 10 (preview/download share one data source)** is tested at the service level: for a random
valid selection, call `GetRegisterDataAsync` once; build the workbook from that same `RegisterData`
(the refactored `GenerateRegisterWorkbookAsync` path); project the preview DTO from that same
`RegisterData`; then assert the previewed per-employee figures (aggregate count, each employee's
Total Earnings / Total Employee Deductions / Net Salary / Total Employer Contributions / Total Cost,
and each line's Total and "%") equal the figures the workbook was built from. Because both read the
identical object, the assertion holds by construction — the test guards against a future refactor that
re-derives the data on either path.

### Controller / view

- Controller (Download): example-based tests that `AxGetGenerateRegister` returns a `JsonResult` for
  From>To and empty-selection params (without calling the service) and a `FileResult` on a stubbed
  successful service call; verify the download file name
  (`PayrollRegister_{fromYear}{fromMonth}-{toYear}{toMonth}.xlsx`) and content type.
- Controller (Preview): example-based tests that `AxGetPreviewRegister`:
  - returns a `JsonResult` (**not** a file) with `success = true` and a non-null `data.employees`
    payload for valid input (stubbing `GetRegisterDataAsync` to return a `RegisterData`) — R15.2;
  - returns a `JsonResult` with `success = false` for From>To and for empty selection **without
    calling the service** (verify the mocked `GetRegisterDataAsync` is never invoked) — R15.4;
  - returns a `JsonResult` with `success = false` when `GetRegisterDataAsync` returns `null`
    (no eligible / empty range / no data) — R15.9, R15.10.
- Service (shared data): a test that `GetRegisterDataAsync` returns the same aggregates the workbook
  is built from — e.g. build a `RegisterData`, then the workbook from it, re-open the bytes with
  ClosedXML and assert the detail-sheet count equals `data.Aggregates.Count` and each employee's
  per-employee totals on the sheet match the aggregate's totals (anchors Property 10). Also assert
  `GenerateRegisterWorkbookAsync` returns `null` on every path `GetRegisterDataAsync` returns `null`
  (bad business id, empty selection, no Range_Periods, no eligible aggregates) — the preserved
  decline behavior of the refactor.
- View/JS: not unit-tested here; the From/To selectors, the client-side From≤To guard, the
  employee-refresh on either selector change, the blob-or-JSON handling (Download), the preview
  ledger-table rendering + horizontal scroll container (R14.7), and the BlockUI/SweetAlert2 flow are
  verified by manual/integration check against R2.4, R3.4, R11, and R15.5–R15.11.
