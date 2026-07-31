# Implementation Plan: Payroll Phase A (Core Engine)

## Overview

Phase A delivers the minimum viable payslip generation capability for the Portal platform. Implementation follows a bottom-up approach: SQL schema and seed data first, then EF Core entities, DTOs, constants, repository, calculation engine, service layer, controllers, views, PDF/email services, navigation integration, and DI registration. All tables reside in the `[payroll]` schema. The module is gated to Enterprise-tier subscribers via the existing `ModuleAccess` attribute pattern.

## Tasks

- [ ] 1. SQL migrations — schema, tables, indexes, and seed data
  - [ ] 1.1 Create payroll schema and lookup tables
    - Create SQL migration file in `Portal.Database/Seeds/`
    - Create `[payroll]` schema if not exists
    - Create `[payroll].[PayslipStatusType]` lookup table (Id TINYINT PK, Name NVARCHAR(20))
    - Seed values: (1, 'Draft'), (2, 'Preview'), (3, 'Finalised')
    - Create `[payroll].[DeductionCategoryType]` lookup table (Id TINYINT PK, Name NVARCHAR(20))
    - Seed values: (1, 'Deduction'), (2, 'Contribution')
    - Create `[payroll].[SalaryType]` lookup table (Id TINYINT PK, Name NVARCHAR(50))
    - Seed values: (1, 'Full-time'), (2, 'Part-time'), (3, 'Hourly')
    - _Requirements: 5.2, 5.3, 4.1, 12.1_

  - [ ] 1.2 Create Department and Employee tables
    - Create `[payroll].[Department]` with Id, BusinessId, Name, IsActive, CreatedAtUtc
    - Add unique constraint `[UQ_Department_BusinessId_Name]` on (BusinessId, Name)
    - Create `[payroll].[Employee]` with all columns per design (Id, BusinessId, DepartmentId, Name, Position, SocialInsuranceNumber, IdNumber, Phone, Email, StartDate, EndDate, SalaryTypeId TINYINT NOT NULL, BaseSalary, HourlyRate, BankAccount, IsActive, CreatedAtUtc)
    - Add FK `[FK_Employee_SalaryType]` to SalaryType.Id
    - Add FK `[FK_Employee_Department]` to Department.Id
    - Add unique constraints for (BusinessId, SocialInsuranceNumber) and (BusinessId, IdNumber)
    - _Requirements: 1.2, 2.2, 2.6, 12.2, 12.5_

  - [ ] 1.3 Create EarningType and DeductionType tables
    - Create `[payroll].[EarningType]` with Id, Name, Code, IsActive, SortOrder, CreatedAtUtc
    - Add unique constraint `[UQ_EarningType_Code]` on Code
    - Create `[payroll].[DeductionType]` with Id, Name, Code, IsPercentage, DeductionCategoryTypeId, IsActive, BusinessId, Country, IsTemplate, CreatedAtUtc
    - Add unique constraint `[UQ_DeductionType_BusinessId_Code]` on (BusinessId, Code)
    - Add FK to DeductionCategoryType
    - Create `[payroll].[DeductionRateHistory]` with Id, DeductionTypeId, Rate, EffectiveFromUtc, EffectiveToUtc, CreatedAtUtc
    - Add FK to DeductionType
    - Add index `[IX_DeductionType_BusinessId]` on (BusinessId, IsActive) INCLUDE (Code, Name)
    - _Requirements: 3.1, 3.2, 4.2, 4.3, 4.6, 12.2_

  - [ ] 1.4 Create EmployeeDefaultEarnings table
    - Create `[payroll].[EmployeeDefaultEarnings]` with Id, EmployeeId, EarningTypeId, Description, Amount, OvertimeMultiplier, OvertimeHours, CreatedAtUtc
    - Add FK to Employee and EarningType
    - _Requirements: 13.1, 13.2, 12.2_

  - [ ] 1.5 Create PayslipPeriod, Payslip, and line tables
    - Create `[payroll].[PayslipPeriod]` with Id, BusinessId, Year, Month, PayslipStatusTypeId, ProcessedAtUtc, CreatedAtUtc
    - Add unique constraint `[UQ_PayslipPeriod_Business_YearMonth]` on (BusinessId, Year, Month)
    - Add FK to PayslipStatusType
    - Create `[payroll].[Payslip]` with Id, EmployeeId, PayslipPeriodId, TotalEarnings, TotalEmployeeDeductions, NetSalary, TotalEmployerContributions, ManagerNotes, PayslipStatusTypeId, CreatedAtUtc
    - Add FKs to Employee, PayslipPeriod, PayslipStatusType
    - Create `[payroll].[PayslipEarningLine]` with Id, PayslipId, EarningTypeId, Description, Amount, OvertimeMultiplier, OvertimeHours, CreatedAtUtc
    - Add CHECK constraint on OvertimeMultiplier (1.0–4.0)
    - Add FKs to Payslip, EarningType
    - Create `[payroll].[PayslipDeductionLine]` with Id, PayslipId, DeductionTypeId, BaseAmount, Rate, CalculatedAmount, DeductionCategoryTypeId, DeductionRateHistoryId, CreatedAtUtc
    - Add FKs to Payslip, DeductionType, DeductionRateHistory, DeductionCategoryType
    - _Requirements: 5.1, 5.2, 5.3, 6.5, 7.7, 12.2, 12.3, 12.4_

  - [ ] 1.6 Create PayslipEmailLog table and performance indexes
    - Create `[payroll].[PayslipEmailLog]` with Id, PayslipId, SentByUserId, SentToEmail, IsSignatureIncluded, SentAtUtc, CreatedAtUtc
    - Add FK to Payslip
    - Create all performance indexes per design: IX_Employee_BusinessId_IsActive, IX_Payslip_PayslipPeriodId, IX_PayslipPeriod_BusinessId_Status, IX_DeductionRateHistory_Lookup, IX_PayslipEarningLine_PayslipId, IX_PayslipDeductionLine_PayslipId
    - _Requirements: 14.8, 12.2, 12.5_

  - [ ] 1.7 Seed EarningTypes and Cyprus DeductionType templates with rate history
    - Insert 5 EarningTypes: Basic (SortOrder 1), Overtime (2), Bonus (3), Paid Holidays (4), Part-time (5)
    - Insert 7 Cyprus template DeductionTypes (BusinessId=NULL, IsTemplate=1): SI_Deduction 8.8%, GESY_Deduction 2.65%, SI_Contribution 8.8%, Redundancy 1.2%, IndustrialTraining 0.5%, SocialCohesion 2.0%, GESY_Contribution 2.9%
    - Insert DeductionRateHistory for each template with EffectiveFromUtc='2024-01-01', EffectiveToUtc=NULL
    - _Requirements: 3.1, 4.4, 4.5_

- [ ] 2. EF Core entities and DbContext configuration
  - [ ] 2.1 Create entity classes for all payroll tables
    - Create entity files in `Portal.Infrastructure/Entities/`: Department.cs, Employee.cs, EmployeeDefaultEarnings.cs, EarningType.cs, DeductionType.cs, DeductionRateHistory.cs, PayslipPeriod.cs, Payslip.cs, PayslipEarningLine.cs, PayslipDeductionLine.cs, PayslipStatusType.cs, DeductionCategoryType.cs, PayslipEmailLog.cs, SalaryType.cs
    - All properties per design including correct data types (decimal, byte for TINYINT, DateTime, etc.)
    - _Requirements: 12.2, 12.3, 12.4, 12.5_

  - [ ] 2.2 Add DbContext configuration for payroll entities
    - Register all 13 entity types in DbContext with `[payroll]` schema mapping
    - Configure SalaryType entity (TINYINT PK, no CreatedAtUtc — lookup table exempt per convention)
    - Configure primary keys, foreign keys, unique constraints, default values, check constraints
    - Configure `CreatedAtUtc` with `.HasDefaultValueSql("GETUTCDATE()")` on all entities
    - Configure `DECIMAL(18,2)` precision for monetary columns, `DECIMAL(4,2)` for OvertimeMultiplier, `DECIMAL(6,2)` for OvertimeHours and Rate
    - _Requirements: 12.2, 12.3, 12.4, 12.5_

- [ ] 3. DTOs and request models
  - [ ] 3.1 Create all DTO and request model classes
    - Create `Portal.Infrastructure/Models/Payroll/` directory
    - Create all DTOs per design: DepartmentDto, EmployeeDto, EmployeeDetailDto, EarningTypeDto, DeductionTypeDto, DeductionRateHistoryDto, PayslipPeriodDto, PayslipPeriodDetailDto, PayslipSummaryDto, PayslipDetailDto, EarningLineDto, DeductionLineDto, BatchGenerationPreview, PayslipPreviewDto, BatchValidationError, EmployeeDefaultEarningsDto
    - Create all request models: CreateDepartmentRequest, UpdateDepartmentRequest, CreateEmployeeRequest, UpdateEmployeeRequest, CreateEarningTypeRequest, CreateDeductionTypeRequest, AddRateHistoryRequest, CreatePeriodRequest, SaveEarningLinesRequest, SaveManagerNotesRequest, EmployeeDefaultEarningInput
    - Create calculation engine I/O models: PayslipCalculationInput, EarningLineInput, DeductionTypeWithHistory, PayslipCalculationResult, ComputedEarningLine, ComputedDeductionLine
    - _Requirements: 1.1, 2.1, 3.2, 4.2, 5.1, 6.5, 7.7, 8.2, 8.6, 9.1, 9.5, 13.2_

- [ ] 4. Constants and PlanFeature seed
  - [ ] 4.1 Add PortalModules.Payroll constant and PlanFeature seed
    - Add `public const string Payroll = "payroll";` to `Portal.Infrastructure/Constants/PortalModules.cs`
    - Add "payroll" to the `All` array in PortalModules
    - Add `[PortalModules.Payroll] = new[] { "Payroll", "AdminPayroll" }` to `ModuleControllerMap`
    - Create SQL seed script to insert `payroll` PlanFeature record for Enterprise tier (following existing pattern)
    - _Requirements: 11.1, 11.3_

- [ ] 5. Build checkpoint
  - Ensure the project compiles successfully with all new entities, DTOs, and constants registered
  - Verify no build errors from DbContext configuration or missing references
  - Ask the user if questions arise

- [ ] 6. Repository layer
  - [ ] 6.1 Create PayrollRepository — Department and Employee methods
    - Create `Portal.Infrastructure/Repositories/PayrollRepository.cs` extending `GenericStoredProcedureRepository<PayslipPeriod>`
    - Implement Department methods: GetDepartmentsByBusinessAsync, GetDepartmentByIdAsync, InsertDepartmentAsync, UpdateDepartmentAsync, DepartmentNameExistsAsync, DepartmentHasActiveEmployeesAsync
    - Implement Employee methods: GetEmployeesAsync (paged with search/filter), GetEmployeeByIdAsync, InsertEmployeeAsync, UpdateEmployeeAsync, SocialInsuranceNumberExistsAsync, IdNumberExistsAsync, GetActiveEmployeesForPeriodAsync
    - Use full table names in all queries, `catch (Exception ex) { throw; }`, null-safe SqlParameters
    - _Requirements: 1.1, 1.4, 1.5, 2.1, 2.6, 2.7_

  - [ ] 6.2 Add EarningType, DeductionType, and Rate History methods to PayrollRepository
    - Implement EarningType methods: GetAllEarningTypesAsync, InsertEarningTypeAsync, ToggleEarningTypeAsync
    - Implement DeductionType methods: GetAllDeductionTypesAsync, InsertDeductionTypeAsync, ToggleDeductionTypeAsync, GetActiveDeductionsWithRatesAsync
    - Implement DeductionRateHistory methods: GetRateHistoryAsync, InsertRateHistoryAsync, CloseCurrentRateAsync
    - Implement template methods: GetTemplatesByCountryAsync, InsertDeductionTypeWithRatesAsync
    - _Requirements: 3.1, 3.3, 4.3, 4.4, 4.6, 4.7, 4.9, 4.10_

  - [ ] 6.3 Add PayslipPeriod, Payslip, and line methods to PayrollRepository
    - Implement Period methods: GetPeriodsByBusinessAsync, GetPeriodByIdAsync, InsertPeriodAsync, UpdatePeriodStatusAsync, PeriodExistsAsync
    - Implement Payslip methods: InsertPayslipAsync, GetPayslipsByPeriodAsync, GetPayslipDetailAsync, UpdatePayslipTotalsAsync, UpdateManagerNotesAsync
    - Implement EarningLine methods: InsertEarningLineAsync, DeleteEarningLinesByPayslipAsync, GetEarningLinesByPayslipAsync
    - Implement DeductionLine methods: InsertDeductionLineAsync, DeleteDeductionLinesByPayslipAsync, GetDeductionLinesByPayslipAsync
    - Implement EmployeeDefaultEarnings methods: GetDefaultEarningsByEmployeeAsync, InsertDefaultEarningAsync, UpdateDefaultEarningAsync, DeleteDefaultEarningAsync
    - Implement EmailLog methods: InsertEmailLogAsync, GetEmailLogsByPayslipAsync
    - Ensure tenant isolation on Payslip access via PayslipPeriod.BusinessId join
    - _Requirements: 5.1, 5.5, 5.6, 5.7, 5.8, 6.5, 7.7, 8.1, 10.1, 13.1, 14.8_

- [ ] 7. Calculation Engine
  - [ ] 7.1 Create IPayslipCalculationEngine interface and PayslipCalculationEngine implementation
    - Create `Portal.Infrastructure/Services/IPayslipCalculationEngine.cs` with `Calculate(PayslipCalculationInput input)` method
    - Create `Portal.Infrastructure/Services/PayslipCalculationEngine.cs` implementing the full algorithm:
      - Resolve overtime earning lines: OvertimeHours × Employee.HourlyRate × OvertimeMultiplier (default 1.5)
      - Validate OvertimeMultiplier range (1.0–4.0), validate HourlyRate not null for overtime
      - Non-overtime lines use manually entered Amount
      - Compute TotalEarnings as sum of all earning line amounts
      - For each applicable deduction: find effective rate via date-range lookup (EffectiveFromUtc ≤ PeriodDate AND EffectiveToUtc IS NULL or > PeriodDate)
      - Compute CalculatedAmount: if IsPercentage then ROUND(TotalEarnings × Rate / 100, 2, MidpointRounding.AwayFromZero), else use fixed Rate
      - Separate into employee deductions (CategoryTypeId=1) and employer contributions (CategoryTypeId=2)
      - NetSalary = TotalEarnings − TotalEmployeeDeductions
      - Return PayslipCalculationResult with all computed values or validation error
    - Pure logic, no I/O — suitable for Singleton registration
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7, 7.8, 7.9, 7.10_

  - [ ] 7.2 Write unit tests for PayslipCalculationEngine
    - Create test class in `Portal.Tests/Unit/Payroll/PayslipCalculationEngineTests.cs`
    - Test basic salary only: €1,000 basic → Net = €885.50, Employee deductions = €114.50, Employer = €154.00
    - Test overtime calculation: 10hrs × €10/hr × 1.5 = €150.00
    - Test overtime max multiplier: 8hrs × €12/hr × 4.0 = €384.00
    - Test default multiplier: 5hrs × €10/hr, no multiplier → €75.00 (uses 1.5 default)
    - Test multiple earning lines: Basic €600 + Holiday €150 → TotalEarnings = €750, deductions applied on €750
    - Test missing rate → IsValid=false with ValidationError
    - Test multiplier out of range (5.0) → IsValid=false
    - Test missing HourlyRate for overtime → IsValid=false
    - Test per-line rounding (MidpointRounding.AwayFromZero)
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.6, 7.1, 7.3, 7.8, 7.9_

- [ ] 8. Service layer
  - [ ] 8.1 Create IPayrollService interface
    - Create `Portal.Infrastructure/Services/IPayrollService.cs` with all method signatures per design
    - Department, Employee, EarningType, DeductionType, DeductionTemplate, EmployeeDefaultEarnings, Period, Payslip Generation, Payslip PDF & Email method groups
    - _Requirements: 1.1, 2.1, 3.1, 4.3, 4.4, 5.1, 8.1, 13.1, 14.3, 14.4, 14.7_

  - [ ] 8.2 Implement PayrollService — Department and Employee management
    - Create `Portal.Infrastructure/Services/PayrollService.cs` implementing IPayrollService
    - Department methods: validate duplicate names, prevent deactivation with active employees, CRUD operations
    - Employee methods: validate unique SIN and IdNumber, require HourlyRate for overtime, filter by search/department/status, toggle active status
    - All validation returns `ServiceResult.Fail(message)` — never throws for business rule violations
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8_

  - [ ] 8.3 Implement PayrollService — EarningTypes, DeductionTypes, Rate History, and Templates
    - EarningType methods: list all, create new, toggle active (admin only)
    - DeductionType methods: list all, create new with initial rate history, toggle active (admin only)
    - Rate history: list for deduction, add new rate (close current rate first), validate no overlap
    - Template import: list templates by country, import into business (copy type + rates, set BusinessId)
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8, 4.9, 4.10, 4.11_

  - [ ] 8.4 Implement PayrollService — Period Management and Batch Generation
    - Period methods: list periods, get detail with payslip summaries, create period (validate no duplicates), finalise (validate Preview status, set ProcessedAtUtc)
    - GeneratePayslipsPreviewAsync: verify Draft status, load active employees (exclude EndDate before period), load default earnings (fallback to BaseSalary as Basic), run calculation engine per employee, collect errors per employee, return BatchGenerationPreview
    - ConfirmBatchGenerationAsync: insert payslips + earning lines + deduction lines, update period to Preview
    - _Requirements: 5.1, 5.2, 5.4, 5.5, 5.6, 5.7, 5.8, 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 13.3, 13.5_

  - [ ] 8.5 Implement PayrollService — Payslip Detail, Earning Lines Save, and Manager Notes
    - GetPayslipDetailAsync: load payslip with earning/deduction lines, enforce tenant isolation via PayslipPeriod.BusinessId
    - SaveEarningLinesAsync: validate payslip not finalised, delete existing earning lines → insert new → load deductions → recalculate via engine → delete existing deduction lines → insert new → update payslip totals
    - SaveManagerNotesAsync: validate 2000 char limit, validate not finalised, update notes
    - EmployeeDefaultEarnings: get/save/delete defaults per employee
    - _Requirements: 5.6, 5.8, 6.5, 6.6, 7.1, 9.1, 9.2, 9.3, 9.4, 9.5, 10.1, 10.2, 10.3, 13.1, 13.2, 13.4, 13.6_

  - [ ] 8.6 Implement PayrollService — PDF Generation and Email
    - GeneratePayslipPdfAsync: load payslip detail → load business profile → render HTML via IPayslipRenderer → convert to PDF via IPayslipPdfService → return bytes
    - SendPayslipEmailAsync: validate employee has email, generate PDF → compose email → send via IEmailService → log to PayslipEmailLog
    - SendAllPayslipEmailsAsync: iterate payslips in period, send to each employee with valid email, log each send
    - _Requirements: 14.1, 14.2, 14.3, 14.4, 14.5, 14.6, 14.7, 14.8_

- [ ] 9. Build checkpoint
  - Ensure the project compiles with repository, calculation engine, and service layer complete
  - Verify all service methods reference correct repository methods
  - Ask the user if questions arise

- [ ] 10. Controllers
  - [ ] 10.1 Create PayrollController (business-facing)
    - Create `Portal.Web/Controllers/PayrollController.cs` with `[Authorize]` and `[ModuleAccess(PortalModules.Payroll)]` attributes
    - Implement page actions: Departments, DepartmentForm, Employees, EmployeeForm, Periods, PeriodDetail, PayslipDetail, BatchGenerate
    - Implement AJAX endpoints (AxPost/AxGet prefix): AxPostCreateDepartment, AxPostUpdateDepartment, AxPostToggleDepartment, AxPostCreateEmployee, AxPostUpdateEmployee, AxPostToggleEmployee, AxPostCreatePeriod, AxPostGeneratePayslips, AxPostConfirmBatch, AxPostFinalisePeriod, AxPostSaveEarningLines, AxPostSaveManagerNotes, AxGetDownloadPayslipPdf, AxPostSendPayslipEmail, AxPostSendAllPayslipEmails
    - AJAX endpoints return `Json(new { success, message })` or `Json(new { success, data })`
    - Controller-level try/catch returns error JSON for AJAX, Error view for page actions
    - _Requirements: 1.1, 2.1, 5.1, 8.1, 9.1, 10.1, 11.3, 11.4, 13.6, 14.3, 14.4, 14.7_

  - [ ] 10.2 Create AdminPayrollController (SuperAdmin)
    - Create `Portal.Web/Controllers/AdminPayrollController.cs` with `[Authorize(Roles = "SuperAdmin")]`
    - Implement page actions: EarningTypes, DeductionTypes, DeductionRateHistory
    - Implement AJAX endpoints: AxPostCreateEarningType, AxPostToggleEarningType, AxPostCreateDeductionType, AxPostToggleDeductionType, AxPostAddRateHistory
    - _Requirements: 3.1, 3.3, 3.4, 4.3, 4.6, 4.7, 4.9_

- [ ] 11. Views — Business-facing
  - [ ] 11.1 Create Departments view and DepartmentForm partial
    - Create `Portal.Web/Views/Payroll/Departments.cshtml` — list departments with name, employee count, active status, create/edit/toggle actions
    - Create `Portal.Web/Views/Payroll/DepartmentForm.cshtml` — form with Name field, save button
    - AJAX calls follow BlockUI → fetch → Unblock → SweetAlert2 pattern
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

  - [ ] 11.2 Create Employees view and EmployeeForm view
    - Create `Portal.Web/Views/Payroll/Employees.cshtml` — paginated list with search, department filter, active filter; columns: Name, Position, Department, SalaryType, BaseSalary, Status
    - Create `Portal.Web/Views/Payroll/EmployeeForm.cshtml` — full employee form with all fields (Name, Position, SIN, IdNumber, Phone, Email, StartDate, EndDate, SalaryType dropdown, BaseSalary, HourlyRate, BankAccount, Department dropdown)
    - Include EmployeeDefaultEarnings management section on the employee form (add/edit/remove recurring earning lines)
    - Conditional HourlyRate field visibility based on overtime eligibility
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 13.1, 13.2, 13.4, 13.6_

  - [ ] 11.3 Create Periods view and PeriodDetail view
    - Create `Portal.Web/Views/Payroll/Periods.cshtml` — list periods with Year/Month, Status badge, PayslipCount, TotalNetSalary, ProcessedAtUtc; Create Period button
    - Create `Portal.Web/Views/Payroll/PeriodDetail.cshtml` — period header with status, list of payslip summaries (employee name, department, earnings, deductions, net), action buttons (Generate/Confirm/Finalise/Send All Emails based on status)
    - _Requirements: 5.1, 5.2, 5.4, 5.5, 5.6, 5.7, 5.8, 8.2, 8.6, 14.7_

  - [ ] 11.4 Create BatchGenerate preview view
    - Create `Portal.Web/Views/Payroll/BatchGenerate.cshtml` — preview page showing: summary (total employees, total payroll cost, total employer contributions, excluded count), table of individual payslip previews with earnings/deductions breakdown, validation errors section per excluded employee, Confirm button
    - _Requirements: 4.11, 8.1, 8.2, 8.3, 8.4, 8.5, 8.6_

  - [ ] 11.5 Create PayslipDetail view
    - Create `Portal.Web/Views/Payroll/PayslipDetail.cshtml` — full payslip breakdown:
      - Employee info header (name, position, department, period)
      - Earning lines section (type, description, amount, overtime details)
      - Employee Deductions section (type, base amount, rate, calculated amount)
      - Employer Contributions section (same columns, clearly separated)
      - Totals: TotalEarnings, TotalEmployeeDeductions, NetSalary, TotalEmployerContributions, TotalCostToBusiness
      - Manager Notes section (editable if not Finalised)
      - Edit Earning Lines button (if Draft or Preview status)
      - Download PDF and Send Email action buttons (visible only when period status is Finalised)
    - _Requirements: 4.11, 9.1, 9.2, 9.3, 9.4, 9.5, 10.1, 10.2, 10.3, 14.3, 14.4_

  - [ ] 11.6 Create DeductionConfig view for business owners
    - Create `Portal.Web/Views/Payroll/DeductionConfig.cshtml` — business-specific deduction/contribution type management:
      - Two sections: "Employee Deductions" and "Employer Contributions" clearly separated
      - Each type shows: Name, Code, IsPercentage, CurrentRate, IsActive, toggle/edit actions
      - "Import Templates" button: opens import flow (Country dropdown → show available templates grouped by category → select/deselect → confirm import → BlockUI → AJAX AxPostImportDeductionTemplates → SweetAlert2 success → reload)
      - "Add Custom" button to create new business-specific types
      - Rate history view per deduction type (expandable or modal)
    - _Requirements: 4.3, 4.4, 4.5, 4.8, 4.10, 4.11_

  - [ ] 11.7 Create PayslipEarningLinesEdit partial/modal
    - Create `Portal.Web/Views/Payroll/_EarningLinesEdit.cshtml` (or inline section on PayslipDetail)
    - Dynamic form: add/remove earning line rows, each with EarningType dropdown, Description input, Amount input
    - Overtime-specific fields: OvertimeHours + OvertimeMultiplier (shown conditionally when Overtime type selected, default multiplier 1.5)
    - Client-side validation: at least 1 earning line, non-negative amounts, multiplier 1.0–4.0
    - Save button: BlockUI → fetch AxPostSaveEarningLines with JSON body → Unblock → SweetAlert2 → page reload (triggers server-side recalculation)
    - _Requirements: 5.8, 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 13.4_

- [ ] 12. Views — Admin (SuperAdmin)
  - [ ] 12.1 Create AdminPayroll EarningTypes view
    - Create `Portal.Web/Views/AdminPayroll/EarningTypes.cshtml` — list all earning types with Name, Code, SortOrder, IsActive; create/toggle actions
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

  - [ ] 12.2 Create AdminPayroll DeductionTypes and RateHistory views
    - Create `Portal.Web/Views/AdminPayroll/DeductionTypes.cshtml` — list all template deduction types with Name, Code, Category, IsPercentage, Country, IsActive; create/toggle actions
    - Create `Portal.Web/Views/AdminPayroll/DeductionRateHistory.cshtml` — rate history for a deduction type with EffectiveFrom, EffectiveTo, Rate, IsCurrent indicator; add new rate action
    - _Requirements: 4.3, 4.4, 4.5, 4.6, 4.7, 4.9_

- [ ] 13. PDF and Email services
  - [ ] 13.1 Create IPayslipRenderer and PayslipRenderer
    - Create `Portal.Infrastructure/Services/IPayslipRenderer.cs` with `RenderPayslipHtmlAsync(PayslipDetailDto, BusinessProfile, bool includeSignature)` method
    - Create `Portal.Infrastructure/Services/PayslipRenderer.cs` — renders branded A4 HTML payslip with: business name/address, employee details, period, earning lines, deduction lines (separated by category), contribution lines, NetSalary, TotalCostToBusiness, optional signature
    - Follow existing IInvoiceRenderer pattern
    - _Requirements: 14.1, 14.2, 14.6_

  - [ ] 13.2 Create IPayslipPdfService and PayslipPdfService
    - Create `Portal.Infrastructure/Services/IPayslipPdfService.cs` with `GeneratePdfAsync(string html)` method
    - Create `Portal.Infrastructure/Services/PayslipPdfService.cs` — converts HTML to PDF byte array using existing PDF infrastructure
    - _Requirements: 14.1, 14.3_

  - [ ] 13.3 Create IPayslipEmailService and PayslipEmailService
    - Create `Portal.Infrastructure/Services/IPayslipEmailService.cs` with `SendPayslipAsync` and `SendAllPayslipsAsync` methods
    - Create `Portal.Infrastructure/Services/PayslipEmailService.cs` — compose email with PDF attachment, send via existing IEmailService, log to PayslipEmailLog
    - Validate employee has non-empty email before sending
    - Batch send iterates all payslips in period, skips employees without email
    - _Requirements: 14.4, 14.5, 14.6, 14.7, 14.8_

- [ ] 14. Navigation integration
  - [ ] 14.1 Add payroll sidebar links and module access wiring
    - Add "Payroll" section to the sidebar navigation with links: Departments, Employees, Deduction Config, Periods
    - Show sidebar section only when business has `payroll` module access (Enterprise tier)
    - Add AdminPayroll links to admin sidebar: Earning Types, Deduction Types
    - _Requirements: 11.1, 11.2, 11.3_

- [ ] 15. DI registration
  - [ ] 15.1 Register all payroll services in DI container
    - Register `PayrollRepository` as Scoped
    - Register `IPayrollService` / `PayrollService` as Scoped
    - Register `IPayslipCalculationEngine` / `PayslipCalculationEngine` as Singleton (pure, no I/O)
    - Register `IPayslipRenderer` / `PayslipRenderer` as Scoped
    - Register `IPayslipPdfService` / `PayslipPdfService` as Scoped
    - Register `IPayslipEmailService` / `PayslipEmailService` as Scoped
    - _Requirements: 11.1, 11.3_

- [ ] 16. Final build checkpoint
  - Ensure all tests pass and the full project compiles with no errors
  - Verify end-to-end: controller → service → repository → engine path is wired correctly
  - Ensure all DI registrations resolve without runtime errors
  - Ask the user if questions arise

## Notes

- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- No property-based tests — the calculation engine uses finite, rule-based inputs best validated with example-based unit tests using Cyprus reference data
- Unit tests use xUnit + Moq (existing project standard)
- The calculation engine is registered as Singleton (pure logic, no state, no I/O)
- All AJAX calls follow the BlockUI → fetch → Unblock → SweetAlert2 pattern (except quick toggle operations which use BlockUI → fetch → Unblock → Reload)
- Repository uses full table names in queries (no aliases), `catch (Exception ex) { throw; }` pattern
- Tenant isolation enforced: Payslip access always validated via PayslipPeriod.BusinessId join
- SalaryType is a TINYINT lookup table (1=Full-time, 2=Part-time, 3=Hourly), referenced by Employee.SalaryTypeId FK
- Calculation engine unit tests (task 7.2) are REQUIRED — not optional — due to financial correctness criticality
- DeductionType is business-scoped (BusinessId column) — templates have BusinessId=NULL, IsTemplate=1
- Period date for rate lookups is always the 1st of the period month (e.g., 2027-07-01 for July 2027)

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "1.4", "1.5", "1.6", "1.7"] },
    { "id": 1, "tasks": ["2.1", "2.2"] },
    { "id": 2, "tasks": ["3.1", "4.1"] },
    { "id": 3, "tasks": ["5"] },
    { "id": 4, "tasks": ["6.1", "6.2", "6.3"] },
    { "id": 5, "tasks": ["7.1", "7.2"] },
    { "id": 6, "tasks": ["8.1"] },
    { "id": 7, "tasks": ["8.2", "8.3", "8.4", "8.5", "8.6"] },
    { "id": 8, "tasks": ["9"] },
    { "id": 9, "tasks": ["10.1", "10.2"] },
    { "id": 10, "tasks": ["11.1", "11.2", "11.3", "11.4", "11.5", "11.6", "11.7"] },
    { "id": 11, "tasks": ["12.1", "12.2"] },
    { "id": 12, "tasks": ["13.1", "13.2", "13.3"] },
    { "id": 13, "tasks": ["14.1", "15.1"] },
    { "id": 14, "tasks": ["16"] }
  ]
}
```
