# Requirements Document

## Introduction

The Payroll Register is a new, consolidated payroll report for the 3 Inventors Portal payroll module. It lets a payroll user select a from/to month-year range and one or more employees, then download a single Excel workbook (.xlsx) that contains a summary sheet followed by one detail sheet per selected employee.

The summary sheet contains one row per selected employee aggregated across the whole range, plus one column per employer-contribution type and a bottom grand-totals row that gives each contribution type's total across all selected employees (the "totals for all contributions on the first page"). Each employee detail sheet lays out earnings, employee deductions, and employer contributions as rows, with one month column per period in the range, a Total column summing the range, and a percentage ("%") column showing each deduction/contribution line's rate.

The distinguishing capability over the existing payroll reports is the combination of a multi-month range, multi-employee selection, and per-employee detail in one workbook: today's reports (EmployeeHistory, AnnualSummary, EmployeeStatement) operate on a single employee at a time, while PeriodSummary covers all employees in a single period without per-employee detail sheets.

The register can also be previewed on the page, per employee, rather than only downloaded: the page offers a Preview action that renders one per-employee ledger table in the section below the filter without producing a file, alongside a Download action that exports the .xlsx exactly as before. The Preview action and the Download action share one computed data source — the same per-employee aggregates and ordered Range_Periods the workbook is built from — so a preview and a download of the same selection agree cell-for-cell and can never drift.

This feature is strictly additive. The existing report pages and endpoints (PeriodSummary, EarningsBreakdown, ContributionReport, EmployeeHistory, AnnualSummary, EmployeeStatement) are not removed, retired, or altered. The feature reuses existing building blocks: `GetPeriodsByBusinessAsync(businessId)` for enumerating all periods of the business (ordered Year DESC, Month DESC) and resolving which periods fall in the selected range, `GetFinalisedPayslipsForPeriodAsync(periodId, businessId)` invoked once per Range_Period to read that period's finalised payslips (tenant-scoped via the `PayslipPeriod.BusinessId` join), `GetLatestFinalisedPeriodIdAsync` for the range defaults, `ICurrentTenantService.CurrentBusinessId` for tenant scoping, the new additive `GetEmployeesByIdsAsync(int[], int)` lookup, and the ClosedXML export pattern already used by `ExportPeriodSummaryToExcelAsync` (header fill `#0D5EA6`, white bold header font, `#,##0.00` number format, `AdjustToContents`, a bold totals row). Finalised is defined as `PayslipStatusTypeId IN (3, 5)`.

Page cleanup or consolidation of the existing reports is explicitly out of scope and deferred.

## Glossary

- **Payroll_Register**: The new report feature described in this document, comprising a selection page and an Excel generation endpoint.
- **Portal**: The 3 Inventors Portal ASP.NET Core MVC application that hosts the Payroll_Register.
- **Register_Controller**: The MVC controller action set that serves the Payroll_Register page and the Excel generation endpoint. Follows the existing `PayrollReportController` conventions, including `[Authorize]` and `[ModuleAccess(PortalModules.Payroll)]`.
- **Register_Service**: The service method(s) that assemble Payroll_Register data and produce the Excel workbook, consistent with `IPayrollReportService`.
- **Business**: A tenant in the multi-tenant Portal, identified by the `businessId` resolved from `ICurrentTenantService.CurrentBusinessId`.
- **Current_Business**: The Business of the authenticated user as resolved by `ICurrentTenantService.CurrentBusinessId`.
- **Payroll_Period**: A payslip period (year + month) belonging to a Business, represented by `PayslipPeriodDto`, which exposes Year, Month, and Status and has no start-date field.
- **Period_Range**: An inclusive selection expressed as a From month-year and a To month-year within the Current_Business, where From is not later than To. Range membership is evaluated on the (Year, Month) pair only.
- **Range_Period**: A Payroll_Period of the Current_Business whose (Year, Month) falls within the Period_Range, inclusive of both endpoints.
- **Finalised_Period**: A Payroll_Period that contains at least one Finalised_Payslip, matching the existing definition used by `GetLatestFinalisedPeriodIdAsync`.
- **Finalised_Payslip**: A payslip whose `PayslipStatusTypeId` is in (3, 5), returned by `GetFinalisedPayslipsForPeriodAsync` for a given period and Current_Business.
- **Contribution_Type**: An employer-contribution `DeductionType` (deduction category 2) that appears on a Finalised_Payslip within the Period_Range.
- **Month_Column**: A column on an Employee_Detail_Sheet representing one Range_Period, holding that line item's amount for that single period.
- **Line_Percentage**: The rate displayed in the "%" column for a deduction or employer-contribution line, sourced from `DeductionLineDto.Rate`; when the line's rate changes within the Period_Range, the Line_Percentage is the latest in-range rate.
- **Selected_Employees**: The set of one or more employees chosen by the user for inclusion in the Payroll_Register.
- **Register_Workbook**: The generated Excel (.xlsx) workbook returned to the user.
- **Summary_Sheet**: The first worksheet of the Register_Workbook, containing one summary row per Selected_Employee aggregated across the Period_Range, one column per Contribution_Type, and a grand-totals row.
- **Employee_Detail_Sheet**: A worksheet, one per Selected_Employee, containing that employee's breakdown across the Period_Range with one Month_Column per Range_Period, a Total column, and a "%" column.
- **Grand_Totals_Row**: The final row of the Summary_Sheet aggregating every column across all Selected_Employees, including a grand total per Contribution_Type.
- **Employee_Totals**: The per-employee totals aggregated across the Period_Range (total earnings, total employee deductions, net salary, total employer contributions, total cost).
- **Preview_Action**: The on-page, per-employee render of the Payroll_Register. Preview_Action requests the Register_Preview_Data and renders one Employee_Detail_Sheet-equivalent ledger table per eligible Selected_Employee in the section below the filter, without producing or downloading any file.
- **Download_Action**: The existing Excel export of the Payroll_Register. Download_Action invokes the `AxGet`-prefixed generation endpoint and returns the Register_Workbook (.xlsx) as a file download response, with the behavior specified in Requirement 4.
- **Register_Preview_Data**: The per-employee aggregates and the ordered list of Range_Periods returned as JSON for the Preview_Action. Register_Preview_Data is produced from the same internal computation the Register_Workbook is built from (the per-employee aggregate list and the chronological Range_Period ordering), so Register_Preview_Data and the Register_Workbook always contain the same figures for the same selection.

## Requirements

### Requirement 1: Access the Payroll Register page

**User Story:** As a payroll user, I want to open a Payroll Register page within the payroll module, so that I can configure and generate a consolidated multi-employee register.

#### Acceptance Criteria

1. IF the Payroll_Register page is requested by a user who is not authenticated, THEN THE Register_Controller SHALL deny access and redirect to the sign-in flow without rendering the selection view, consistent with the `[Authorize]` attribute on the existing payroll report actions.
2. IF the Payroll_Register page is requested by an authenticated user who lacks payroll module access, THEN THE Register_Controller SHALL deny access and return an authorization-failure response without rendering the selection view, consistent with the `[ModuleAccess(PortalModules.Payroll)]` attribute on the existing payroll report actions.
3. WHEN the Payroll_Register page is requested by an authenticated user with payroll module access, THE Register_Controller SHALL resolve the Current_Business identifier from `ICurrentTenantService.CurrentBusinessId`.
4. IF `ICurrentTenantService.CurrentBusinessId` cannot be resolved to a valid Current_Business when the Payroll_Register page is requested, THEN THE Register_Controller SHALL return the shared error view without rendering the selection view.
5. WHEN the Payroll_Register page is requested and the Current_Business is resolved, THE Register_Controller SHALL return the selection view containing a From month-year selector, a To month-year selector, and an employee selector populated only with employees belonging to the resolved Current_Business.
6. WHILE the employee selector is populated, THE Register_Controller SHALL exclude every employee not belonging to the resolved Current_Business from the selection view.
7. IF an unhandled error occurs while loading the Payroll_Register page, THEN THE Register_Controller SHALL return the shared error view and preserve the request state without persisting any partial register data, consistent with the existing payroll report page actions.

### Requirement 2: Select a from/to month range

**User Story:** As a payroll user, I want to choose a from/to month-year range the register covers, so that the register spans exactly the months I need.

#### Acceptance Criteria

1. WHEN the Payroll_Register page loads, THE Register_Controller SHALL populate the From and To month-year selectors from the Payroll_Periods of the Current_Business obtained from `GetPeriodsByBusinessAsync`, ordered from newest to oldest by (Year, Month), where each option displays the period label and its finalised/unfinalised state.
2. WHEN the Payroll_Register page loads and no range has been chosen by the user, THE Register_Controller SHALL default the To selector to the latest Finalised_Period obtained from `GetLatestFinalisedPeriodIdAsync` and SHALL default the From selector to January of that latest finalised period's year.
3. IF the Current_Business has no Finalised_Period, THEN THE Register_Controller SHALL present the From and To selectors with no period pre-selected and SHALL display a message indicating that no finalised period is available, while retaining any unfinalised periods as selectable options.
4. WHEN the user submits a range whose From (Year, Month) is later than its To (Year, Month), THE Register_Controller SHALL decline to generate the Register_Workbook and SHALL display an inline validation error indicating that From must not be later than To, without altering the current selection.
5. WHEN the user selects a valid Period_Range, THE Register_Service SHALL include in the Payroll_Register only data belonging to the Range_Periods of the Current_Business, and SHALL exclude all data belonging to any Payroll_Period outside the Period_Range or to any other business.
6. IF the submitted range resolves to no Range_Period belonging to the Current_Business, THEN THE Register_Service SHALL return an empty Payroll_Register and SHALL display a message indicating that the selected range contains no periods for the current business, without altering the current selection.

### Requirement 3: Select one or more employees

**User Story:** As a payroll user, I want to select one, several, or all employees for the chosen range, so that I can produce a register for exactly the employees I need.

#### Acceptance Criteria

1. WHEN a Period_Range is selected, THE Register_Controller SHALL present an employee selector listing only employees who have a Finalised_Payslip in any Range_Period for the Current_Business, sorted alphabetically by employee name.
2. THE employee selector SHALL allow the user to select between 1 and the total number of listed employees, inclusive, for the selected Period_Range.
3. WHERE the user chooses to select all listed employees, THE employee selector SHALL mark every employee currently listed for the selected Period_Range as selected.
4. WHEN either the From selection or the To selection changes, THE Register_Controller SHALL refresh the employee selector to list only employees with a Finalised_Payslip in any Range_Period of the newly selected Period_Range and SHALL clear any previous selection so that no employee is selected until the user selects again.
5. IF the user requests generation with no employee selected, THEN THE Register_Controller SHALL decline to generate the Register_Workbook, SHALL retain the current Period_Range and employee selector state, and SHALL return an error message indicating that at least one employee must be selected.
6. IF the selected Period_Range has no employees with a Finalised_Payslip for the Current_Business, THEN THE Register_Controller SHALL present an empty employee selector, SHALL disable the generation action, and SHALL return a message indicating that no eligible employees exist for the selected Period_Range.

### Requirement 4: Generate the register workbook (Download action)

**User Story:** As a payroll user, I want to download a single Excel workbook for my selection via the Download action, so that I can review and share the consolidated register.

#### Acceptance Criteria

1. WHEN the user triggers the Download_Action for a valid Period_Range and one or more Selected_Employees, THE Register_Controller SHALL produce a single Excel workbook in .xlsx format and return it as a file download response, such that the `AxGet`-prefixed generation endpoint is the Download_Action.
2. THE Register_Controller SHALL expose the Excel generation as an HTTP GET endpoint named with the `AxGet` prefix, accepting `fromYear`, `fromMonth`, `toYear`, `toMonth`, and `employeeIds[]` parameters, consistent with the existing export endpoints.
3. IF the request has From (Year, Month) later than To (Year, Month), or has zero Selected_Employees, THEN THE Register_Controller SHALL reject the request before invoking the Register_Service, return no workbook, and provide a JSON failure indication stating that a valid range and at least one employee are required.
4. WHEN the Register_Workbook is returned, THE Register_Controller SHALL set the response content type to `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`.
5. WHEN the Register_Workbook is returned, THE Register_Controller SHALL set a download file name that identifies the report as the Payroll Register and includes the selected range in the form `PayrollRegister_{fromYear}{fromMonth}-{toYear}{toMonth}.xlsx`.
6. THE Register_Service SHALL restrict all data in the Register_Workbook to Finalised_Payslips of the Range_Periods for the Current_Business, excluding any payslip that is not in a Finalised state.
7. IF no Finalised_Payslips exist for the selected Period_Range and Selected_Employees within the Current_Business, THEN THE Register_Controller SHALL return no workbook and provide a JSON failure indication stating that no finalised payslip data is available for the selection.

### Requirement 5: Summary sheet content

**User Story:** As a payroll user, I want a summary sheet listing each selected employee with per-contribution totals and grand totals, so that I can see the consolidated figures and every contribution total at a glance.

#### Acceptance Criteria

1. THE Register_Service SHALL place the Summary_Sheet as the first worksheet (sheet index 1) of the Register_Workbook.
2. WHEN the Register_Service generates the Summary_Sheet, THE Summary_Sheet SHALL present, in order, an Employee Name column, a Total Earnings column, a Total Employee Deductions column, a Net Salary column, one column per Contribution_Type, a Total Employer Contributions column, and a Total Cost column.
3. THE set of Contribution_Type columns SHALL be the union of all employer-contribution types (deduction category 2) appearing on the Finalised_Payslips of the Selected_Employees across the Range_Periods, ordered consistently by the contribution type's sort order and then by name.
4. WHEN the Register_Service generates the Summary_Sheet, THE Summary_Sheet SHALL contain exactly one row per Selected_Employee, each row showing that employee's amounts summed across all Range_Periods for Total Earnings, Total Employee Deductions, Net Salary, each Contribution_Type column, Total Employer Contributions, and Total Cost.
5. IF a Selected_Employee has no Finalised_Payslip in any Range_Period after filtering, THEN THE Register_Service SHALL omit that employee's row rather than rendering a zero-valued row.
6. WHERE a Selected_Employee has no amount for a given Contribution_Type within the Period_Range, THE Register_Service SHALL render 0.00 in that employee's cell for that Contribution_Type column.
7. THE Summary_Sheet SHALL contain a Grand_Totals_Row whose value in each monetary column (Total Earnings, Total Employee Deductions, Net Salary, each Contribution_Type column, Total Employer Contributions, and Total Cost) equals the arithmetic sum of the corresponding column values across all Selected_Employee rows, such that each Contribution_Type column's total is that contribution's grand total across all Selected_Employees.
8. THE Summary_Sheet SHALL render the header row with background fill `#0D5EA6` and white bold font, SHALL format every monetary value in the employee rows and the Grand_Totals_Row with the `#,##0.00` number format, and SHALL render the Grand_Totals_Row with bold font, consistent with the existing `ExportPeriodSummaryToExcelAsync` styling.

### Requirement 6: Per-employee detail sheet content

**User Story:** As a payroll user, I want one sheet per selected employee with a month-by-month breakdown and line percentages, so that I can review every earning, deduction, and contribution across the range for each person.

#### Acceptance Criteria

1. THE Register_Service SHALL add exactly one Employee_Detail_Sheet per Selected_Employee that has a Finalised_Payslip in at least one Range_Period, positioned after the Summary_Sheet, ordered by the sequence in which employees appear in the Selected_Employee list.
2. WHEN the Register_Service generates an Employee_Detail_Sheet, THE Employee_Detail_Sheet SHALL organise rows into an Earnings section, an Employee Deductions section, and an Employer Contributions section, each listing one row per distinct line item that appears for that employee across the Range_Periods.
3. WHEN the Register_Service generates an Employee_Detail_Sheet, THE Employee_Detail_Sheet SHALL include one Month_Column per Range_Period ordered chronologically from oldest to newest, a Total column summing each row across the Range_Periods, and a "%" column.
4. WHERE a line item has no amount for a given Range_Period, THE Employee_Detail_Sheet SHALL render that line's Month_Column cell as 0.00 for that period rather than treating the missing month as an error.
5. WHEN the Register_Service renders a deduction line or an employer-contribution line, THE Employee_Detail_Sheet SHALL display the Line_Percentage in that line's "%" column.
6. WHERE a row is an earnings line, THE Employee_Detail_Sheet SHALL render that line's "%" cell as blank or "-", because earnings have no rate.
7. THE Employee_Detail_Sheet SHALL render a section total row for each of the Earnings, Employee Deductions, and Employer Contributions sections, summing each Month_Column and the Total column for the section.
8. THE Employee_Detail_Sheet SHALL display an Employee_Totals block for Total Earnings, Total Employee Deductions, Net Salary, Total Employer Contributions, and Total Cost, each summed across the Range_Periods.
9. WHERE a section (Earnings, Employee Deductions, or Employer Contributions) has no line items for the employee across the Range_Periods, THE Employee_Detail_Sheet SHALL render that section's header row followed by a single row indicating that no items exist for that section, and SHALL set that section's total to 0.00.
10. THE Employee_Detail_Sheet SHALL format every monetary value with the `#,##0.00` number format and SHALL render every header row with background fill `#0D5EA6` and white bold font, consistent with the existing export styling.

### Requirement 7: Worksheet naming and ordering

**User Story:** As a payroll user, I want sheets that are clearly named and ordered, so that I can navigate the workbook without confusion.

#### Acceptance Criteria

1. THE Register_Service SHALL name the Summary_Sheet using a label that reflects the selected range (for example "Summary {FromMon}-{ToMon} {Year}"), truncated so the total worksheet name does not exceed 31 characters.
2. WHEN generating an Employee_Detail_Sheet, THE Register_Service SHALL name the worksheet using the employee's name, truncated to a maximum of 31 characters.
3. IF two or more Selected_Employees would produce the same Employee_Detail_Sheet name, THEN THE Register_Service SHALL append a numeric suffix (starting at "2" for the second occurrence and incrementing by 1 for each subsequent occurrence) such that each worksheet name is unique within the Register_Workbook, keeping the total length at or below 31 characters.
4. IF an employee's name exceeds 31 characters or contains any of the characters not permitted in an .xlsx worksheet name (backslash, forward slash, question mark, asterisk, square brackets, colon), THEN THE Register_Service SHALL remove or replace the prohibited characters and truncate the result to at most 31 characters so the produced worksheet name satisfies the .xlsx worksheet-naming constraints, and SHALL retain the sheet in the workbook rather than failing generation.
5. THE Register_Service SHALL order the worksheets with the Summary_Sheet at position 1, followed by the Employee_Detail_Sheets in the same sequence as the Selected_Employees were provided.

### Requirement 8: Tenant isolation

**User Story:** As a business owner, I want the register to include only my business's data, so that no other business's payroll information is ever exposed.

#### Acceptance Criteria

1. THE Register_Service SHALL scope every data access by the `businessId` resolved from `ICurrentTenantService.CurrentBusinessId`, including each per-period call to `GetFinalisedPayslipsForPeriodAsync`.
2. IF `ICurrentTenantService.CurrentBusinessId` is null, empty, or cannot be resolved, THEN THE Register_Service SHALL abort the operation, SHALL return no payroll data, and SHALL surface an error indication that the current business could not be determined.
3. IF a requested Range_Period does not belong to the Current_Business, THEN THE Register_Controller SHALL exclude that period's data and SHALL return a message indicating that no data is available for the selection.
4. IF a requested employee does not belong to the Current_Business, THEN THE Register_Service SHALL exclude that employee from the Register_Workbook.
5. THE Register_Workbook SHALL contain records only for employees of the Current_Business and only for Range_Periods belonging to the Current_Business, such that every row corresponds to the Current_Business.
6. WHEN no employee of the Current_Business has data for the requested Period_Range, THE Register_Service SHALL NOT produce a Register_Workbook and SHALL return a message indicating that no data is available for the selection.

### Requirement 9: Empty and missing-data handling

**User Story:** As a payroll user, I want clear feedback when there is nothing to generate, so that I am not handed an empty or misleading file.

#### Acceptance Criteria

1. IF the selected Period_Range contains zero Finalised_Payslip records across all Range_Periods for the Current_Business, THEN THE Register_Controller SHALL decline to produce the Register_Workbook and SHALL return a user-facing message identifying the selected range and indicating that no finalised payslips exist for it.
2. WHEN the Register_Controller declines to produce the Register_Workbook, THE Register_Controller SHALL leave the selection inputs unchanged and SHALL NOT transmit or persist any workbook file.
3. IF a Selected_Employee has zero Finalised_Payslip records across all Range_Periods, THEN THE Register_Service SHALL omit that employee's Employee_Detail_Sheet and SHALL exclude that employee from the Summary_Sheet rows and from all Summary_Sheet totals.
4. WHERE a Selected_Employee has a Finalised_Payslip for some Range_Periods but not others, THE Register_Service SHALL render 0.00 month cells for the absent periods and SHALL include the employee rather than treating the absent months as an error.
5. IF, after applying tenant and finalised-payslip filtering, zero Selected_Employee records have a Finalised_Payslip in any Range_Period, THEN THE Register_Controller SHALL decline to produce the Register_Workbook and SHALL return a user-facing message indicating that no data is available for the current selection.

### Requirement 10: Error handling

**User Story:** As a payroll user, I want consistent error feedback when generation fails, so that I understand the result and can retry.

#### Acceptance Criteria

1. IF an unhandled error occurs during Register_Workbook generation, THEN THE Register_Controller SHALL return a JSON response with a success flag set to false and a human-readable message indicating that register generation failed, without exposing stack traces or internal exception details, consistent with the existing export endpoints.
2. WHEN Register_Workbook generation succeeds, THE Register_Controller SHALL return the file download response containing the generated workbook rather than a JSON response, consistent with the existing export endpoints.
3. WHERE a data-access operation within the Register_Service encounters an exception, THE Register_Service SHALL rethrow the exception without modifying its type or stack trace so that the Register_Controller handles it, consistent with the repository and service error-handling conventions.
4. WHEN the Register_Controller receives a rethrown exception from the Register_Service, THE Register_Controller SHALL log the error before returning the JSON failure response.
5. IF the Register_Controller receives a request with missing or invalid required parameters, THEN THE Register_Controller SHALL return a JSON response with a success flag set to false and a message identifying the invalid parameter, without invoking Register_Workbook generation.

### Requirement 11: Client interaction feedback

**User Story:** As a payroll user, I want visible progress and result feedback while the workbook is being generated, so that I know the system is working and whether it succeeded.

#### Acceptance Criteria

1. WHEN the user initiates Register_Workbook generation, THE Payroll_Register page SHALL display a BlockUI overlay before the generation request is sent, and the overlay SHALL present a progress message indicating that generation is in progress.
2. WHILE the BlockUI overlay is displayed, THE Payroll_Register page SHALL block all user interaction with underlying page controls until the overlay is removed.
3. WHEN Register_Workbook generation completes with a success outcome, THE Payroll_Register page SHALL remove the BlockUI overlay.
4. WHEN Register_Workbook generation completes with a failure outcome, THE Payroll_Register page SHALL remove the BlockUI overlay.
5. IF the Excel generation endpoint returns a JSON failure response, THEN THE Payroll_Register page SHALL remove the BlockUI overlay and SHALL display a SweetAlert2 error dialog containing the message returned in the response.
6. IF the Excel generation request fails without a JSON failure response (for example, a network failure or a non-JSON response), THEN THE Payroll_Register page SHALL remove the BlockUI overlay and SHALL display a SweetAlert2 error dialog containing an error message indicating that an unexpected error occurred.
7. THE Payroll_Register page SHALL use SweetAlert2 for all user-facing messages and SHALL NOT use native browser alert, confirm, or prompt dialogs.

### Requirement 12: Additive, non-destructive delivery

**User Story:** As a payroll administrator, I want the existing reports untouched, so that current workflows keep working while the new register is added.

#### Acceptance Criteria

1. WHEN the Payroll_Register is deployed, THE Payroll_Register SHALL be added as new functionality while the PeriodSummary, EarningsBreakdown, ContributionReport, EmployeeHistory, AnnualSummary, and EmployeeStatement capabilities each remain invocable and return results identical to their pre-deployment outputs for the same inputs.
2. WHEN a request is made to any of the existing PeriodSummary, EarningsBreakdown, ContributionReport, EmployeeHistory, AnnualSummary, or EmployeeStatement endpoints after the Payroll_Register is deployed, THE Payroll_Register SHALL cause no change to that endpoint's response content, response structure, or success/error outcome compared to the same request issued before deployment.
3. THE Payroll_Register SHALL obtain its data by invoking the existing `GetPeriodSummaryAsync`, `GetLatestFinalisedPeriodIdAsync`, `GetPayslipDetailAsync`, and `GetPeriodsAsync` operations with their current method signatures unchanged, such that every existing caller of these operations continues to compile and returns results identical to pre-deployment outputs for the same inputs.
4. WHEN the Payroll_Register enumerates the Period_Range, THE Payroll_Register SHALL reuse `GetPeriodsByBusinessAsync` and SHALL invoke `GetFinalisedPayslipsForPeriodAsync` once per Range_Period without changing either operation's signature or behavior, such that existing callers of those operations remain unaffected.
5. IF the Payroll_Register requires a data operation not satisfied by the existing `GetPeriodSummaryAsync`, `GetLatestFinalisedPeriodIdAsync`, `GetPayslipDetailAsync`, `GetPeriodsAsync`, `GetPeriodsByBusinessAsync`, or `GetFinalisedPayslipsForPeriodAsync` operations, THEN THE Payroll_Register SHALL add a new operation (for example the additive `GetEmployeesByIdsAsync(int[], int)`) rather than modify the signature or existing behavior of any existing operation, and existing callers SHALL remain unaffected.
6. WHERE the Preview_Action needs the same per-employee aggregates and ordered Range_Periods the Register_Workbook is built from, THE Payroll_Register SHALL extract that computation into a shared, feature-owned data method invoked by both the Download_Action workbook generation and the Preview_Action, introduced additively and having no callers outside the Payroll_Register, such that the existing six reports and all existing shared operations remain unchanged.

### Requirement 13: Percentage and in-range rate-change handling

**User Story:** As a payroll user, I want each deduction and contribution line to show its rate, so that I can see the percentage behind each amount even when a rate changes during the range.

#### Acceptance Criteria

1. WHEN the Register_Service renders a deduction or employer-contribution line, THE Register_Service SHALL source the Line_Percentage from `DeductionLineDto.Rate` for that line.
2. WHERE a deduction or employer-contribution line's rate changes within the Period_Range, THE Register_Service SHALL display the latest in-range rate as the Line_Percentage for that line.
3. THE Register_Service SHALL compute every summed monetary amount from the per-period line amounts independently of the displayed Line_Percentage, such that a mid-range rate change does not alter any summed Total column, section total, or Grand_Totals_Row value.
4. WHERE a line is an earnings line, THE Register_Service SHALL leave the Line_Percentage blank or "-", because earnings have no rate.

### Requirement 14: Large-selection performance expectation

**User Story:** As a payroll user, I want the register to generate reliably even for a large employee selection and a long range, so that selecting many employees or many months does not break the download.

#### Acceptance Criteria

1. WHEN the Selected_Employees set contains between 1 and 5,000 entries, THE Register_Service SHALL generate exactly one Employee_Detail_Sheet per Selected_Employee that has a Finalised_Payslip in at least one Range_Period.
2. IF a Selected_Employee has no Finalised_Payslip in any Range_Period, THEN THE Register_Service SHALL exclude that Selected_Employee from the Register_Workbook and SHALL complete generation for the remaining Selected_Employees without failing.
3. WHILE assembling the Register_Workbook, THE Register_Service SHALL enumerate the Range_Periods once, invoke `GetFinalisedPayslipsForPeriodAsync` once per Range_Period, and load reference data once for the whole range, such that the number of data-retrieval round trips does not increase in proportion to the number of Selected_Employees and reference data is not re-loaded per period or per employee, consistent with the N+1-avoidance approach used by the existing report services.
4. IF Register_Workbook generation does not complete within 120 seconds for the Selected_Employees set and Period_Range, THEN THE Register_Service SHALL terminate the generation request, SHALL return an error response indicating that generation exceeded the time limit, and SHALL leave no partial Register_Workbook available for download.
5. WHERE Register_Workbook generation is an in-progress operation, THE Payroll_Register page SHALL keep the BlockUI overlay visible continuously from the moment the generation request is issued until a success or error response is received.
6. WHEN the Preview_Action computes Register_Preview_Data, THE Register_Service SHALL compute the per-employee aggregates once over the Period_Range, enumerate the Range_Periods once, invoke `GetFinalisedPayslipsForPeriodAsync` once per Range_Period, and load reference data once for the whole range, such that the Preview_Action introduces no additional per-employee or per-period data-retrieval round trips beyond those the Download_Action already performs.
7. WHERE a Preview_Action covers a large combination of Range_Periods and Selected_Employees that is visually wide, THE Payroll_Register page SHALL render the per-employee ledger tables inside a horizontally scrollable container so that wide month-column layouts remain usable without breaking page layout.

### Requirement 15: On-page per-employee preview

**User Story:** As a payroll user, I want to preview the register on the page per employee before downloading, so that I can review the figures without opening a file and trust that the preview matches the download.

#### Acceptance Criteria

1. THE Payroll_Register page SHALL present both a Preview button that triggers the Preview_Action and a Download button that triggers the Download_Action, where the Download button preserves the existing Excel export behavior specified in Requirement 4.
2. WHEN the user triggers the Preview_Action with a valid Period_Range and one or more Selected_Employees, THE Register_Controller SHALL return the Register_Preview_Data as a JSON success response containing the data rather than returning a file.
3. THE Register_Controller SHALL expose the Preview_Action as an HTTP GET endpoint named with the `AxGet` prefix, accepting `fromYear`, `fromMonth`, `toYear`, `toMonth`, and `employeeIds[]` parameters, consistent with the Download_Action endpoint.
4. IF a Preview_Action request has From (Year, Month) later than To (Year, Month), or has zero Selected_Employees, THEN THE Register_Controller SHALL reject the request before invoking the Register_Service and SHALL return a JSON failure indication stating that a valid range and at least one employee are required, applying the same validation as the Download_Action.
5. WHEN Register_Preview_Data is returned, THE Payroll_Register page SHALL render, in the section below the filter, exactly one ledger table per eligible Selected_Employee in the order the employees were selected, each table presenting a heading showing the employee name, an Earnings section, an Employee Deductions section, and an Employer Contributions section.
6. WHEN the Payroll_Register page renders a per-employee ledger table, THE Payroll_Register page SHALL display one row per distinct line item in each section, one Month_Column per Range_Period ordered chronologically from oldest to newest, a Total column summing each row across the Range_Periods, and a "%" column holding each deduction or employer-contribution line's latest-in-range rate and left blank for earnings lines.
7. WHEN the Payroll_Register page renders a per-employee ledger table, THE Payroll_Register page SHALL include a section total row for each of the Earnings, Employee Deductions, and Employer Contributions sections and SHALL include the Employee_Totals block for Total Earnings, Total Employee Deductions, Net Salary, Total Employer Contributions, and Total Cost.
8. WHERE a line item has no amount for a given Range_Period, THE Payroll_Register page SHALL render that line's Month_Column cell as 0.00 for that period, consistent with the Employee_Detail_Sheet missing-month rule.
9. IF a Selected_Employee has no Finalised_Payslip in any Range_Period, THEN THE Preview_Action SHALL omit that employee from the Register_Preview_Data and THE Payroll_Register page SHALL render no ledger table for that employee, applying the same eligibility as the Download_Action.
10. IF a Preview_Action resolves to no eligible Selected_Employee, no Range_Period, or an invalid range, THEN THE Payroll_Register page SHALL render no ledger table and SHALL display a SweetAlert2 message describing the outcome, consistent with the client-feedback behavior in Requirement 11.
11. WHEN the user triggers the Preview_Action, THE Payroll_Register page SHALL display a BlockUI overlay before the preview request is sent and SHALL remove the overlay after the request completes on both success and failure, consistent with Requirement 11.
12. THE Register_Preview_Data returned for a Preview_Action SHALL be derived from the same computed per-employee aggregates and ordered Range_Periods as the Register_Workbook produced by the Download_Action for the identical selection, such that each previewed cell equals the corresponding Download_Action cell for that selection.
