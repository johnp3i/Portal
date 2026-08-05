# Implementation Plan: Payroll Phase D (Integration)

## Overview

Phase D is the final integration layer of the Payroll module. It introduces PAYE income tax calculation using progressive tax bands (Cyprus 2024), integrates payroll data with the existing Business Applications Tracker (Compliance) module, provides an employer contribution breakdown report, and seeds country-specific deduction templates for multi-country expansion. The existing `PayslipCalculationEngine` is NOT modified — a new orchestrator wraps it. PAYE calculation is a pure function (Singleton, no DB access). Compliance integration is non-blocking. Country templates are SuperAdmin-only.

**Key Design Decisions (from review):**
- `IsPayeDeductible` flag on `DeductionType` identifies which deductions reduce the PAYE base (instead of Code matching)
- `PayslipDeductionLine.DeductionRateHistoryId` is nullable — PAYE lines use NULL
- Compliance filing lookup uses 1-month offset (July payroll → August DueDate)
- Country code mapping via static dictionary (BusinessProfile.Country → ISO code)
- Both `ConfirmBatchGenerationAsync` AND `SaveEarningLinesAsync` use the orchestrator
- PAYE line `Rate` = top marginal rate, not blended effective rate

## Tasks

- [ ] 1. Database schema
  - [ ] 1.1 Create PayeTaxBand table SQL migration
    - Create SQL script `Portal.Database/Seeds/Seed_PayeTaxBand.sql`
    - USE [Portal] header
    - CREATE TABLE `[payroll].[PayeTaxBand]` with columns: Id (INT IDENTITY PK), CountryCode (NVARCHAR(3) NOT NULL), LowerBound (DECIMAL(18,2) NOT NULL), UpperBound (DECIMAL(18,2) NULL for top band), Rate (DECIMAL(5,4) NOT NULL), EffectiveFromYear (INT NOT NULL), EffectiveToYear (INT NULL for current), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE())
    - Add CHECK constraint `[CK_PayeTaxBand_Rate]`: Rate >= 0 AND Rate <= 1
    - Add CHECK constraint `[CK_PayeTaxBand_Bounds]`: UpperBound IS NULL OR LowerBound < UpperBound
    - Create index `IX_PayeTaxBand_Country_Year` on (CountryCode, EffectiveFromYear) INCLUDE (LowerBound, UpperBound, Rate, EffectiveToYear)
    - _Requirements: 1.7, 8.1, 8.2, 8.7_

  - [ ] 1.2 Create CountryDeductionTemplate table SQL migration
    - Create SQL script `Portal.Database/Seeds/Seed_CountryDeductionTemplate.sql`
    - USE [Portal] header
    - CREATE TABLE `[payroll].[CountryDeductionTemplate]` with columns: Id (INT IDENTITY PK), CountryCode (NVARCHAR(3) NOT NULL), DeductionName (NVARCHAR(100) NOT NULL), Code (NVARCHAR(50) NOT NULL), IsPercentage (BIT NOT NULL DEFAULT 1), DeductionCategoryTypeId (TINYINT NOT NULL FK), DefaultRate (DECIMAL(5,4) NOT NULL), IsPayeDeductible (BIT NOT NULL DEFAULT 0), SortOrder (INT NOT NULL DEFAULT 0), IsActive (BIT NOT NULL DEFAULT 1), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE())
    - Add FK constraint `[FK_CountryDeductionTemplate_Category]` referencing `[payroll].[DeductionCategoryType]([Id])`
    - Create index `IX_CountryDeductionTemplate_Country` on (CountryCode, IsActive) INCLUDE (DeductionName, Code, DefaultRate, SortOrder)
    - _Requirements: 7.1, 7.4, 8.7_

  - [ ] 1.3 Create PayslipPeriodComplianceFiling table SQL migration
    - Create SQL script `Portal.Database/Seeds/Seed_PayslipPeriodComplianceFiling.sql`
    - USE [Portal] header
    - CREATE TABLE `[payroll].[PayslipPeriodComplianceFiling]` with columns: Id (INT IDENTITY PK), PayslipPeriodId (INT NOT NULL FK), ComplianceFilingId (INT NOT NULL FK), ContributionTotal (DECIMAL(18,2) NOT NULL), UpdatedAtUtc (DATETIME NOT NULL), UpdatedByUserId (NVARCHAR(450) NOT NULL), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE())
    - Add FK `[FK_PayslipPeriodCF_Period]` referencing `[payroll].[PayslipPeriod]([Id])`
    - Add FK `[FK_PayslipPeriodCF_Filing]` referencing `[compliance].[BusinessApplication]([Id])`
    - Create index `IX_PayslipPeriodCF_Period` on (PayslipPeriodId) INCLUDE (ComplianceFilingId, ContributionTotal, UpdatedAtUtc)
    - _Requirements: 9.1, 9.2, 9.3, 9.5, 8.7_

  - [ ] 1.4 Add IsPayeApplicable column to Employee table
    - Create SQL script `Portal.Database/Seeds/Seed_Employee_IsPayeApplicable.sql`
    - USE [Portal] header
    - ALTER TABLE `[payroll].[Employee]` ADD [IsPayeApplicable] BIT NOT NULL DEFAULT 0
    - _Requirements: 2.1, 8.5_

  - [ ] 1.5 Add IsPayeDeductible column to DeductionType table
    - Create SQL script `Portal.Database/Seeds/Seed_DeductionType_IsPayeDeductible.sql`
    - USE [Portal] header
    - ALTER TABLE `[payroll].[DeductionType]` ADD [IsPayeDeductible] BIT NOT NULL DEFAULT 0
    - UPDATE existing SI_Deduction and GESY_Deduction records: SET IsPayeDeductible = 1 WHERE Code IN ('SI_Deduction', 'GESY_Deduction') AND DeductionCategoryTypeId = 1
    - This flag marks deductions that reduce the PAYE taxable base (replaces hard-coded Code matching)
    - _Requirements: 1.3, 3.2, 3.6_

  - [ ] 1.6 Make DeductionRateHistoryId nullable on PayslipDeductionLine
    - Create SQL script `Portal.Database/Seeds/Seed_PayslipDeductionLine_NullableRateHistory.sql`
    - USE [Portal] header
    - ALTER TABLE `[payroll].[PayslipDeductionLine]` ALTER COLUMN [DeductionRateHistoryId] INT NULL
    - PAYE lines will use NULL for this column since PAYE uses progressive bands, not rate history
    - _Requirements: 3.7_

- [ ] 2. EF Core entities and DbContext configuration
  - [ ] 2.1 Create PayeTaxBand entity and configuration
    - Create `Portal.Infrastructure/Entities/PayeTaxBand.cs` with properties: Id, CountryCode, LowerBound, UpperBound (nullable), Rate, EffectiveFromYear, EffectiveToYear (nullable), CreatedAtUtc
    - Add DbSet<PayeTaxBand> to PortalDbContext
    - Configure entity in OnModelCreating: schema "payroll", PK, max lengths (CountryCode = 3), check constraints, default CreatedAtUtc
    - _Requirements: 1.7, 8.1, 8.2_

  - [ ] 2.2 Create CountryDeductionTemplate entity and configuration
    - Create `Portal.Infrastructure/Entities/CountryDeductionTemplate.cs` with properties: Id, CountryCode, DeductionName, Code, IsPercentage, DeductionCategoryTypeId, DefaultRate, IsPayeDeductible, SortOrder, IsActive, CreatedAtUtc
    - Add DbSet<CountryDeductionTemplate> to PortalDbContext
    - Configure entity: schema "payroll", PK, max lengths (CountryCode=3, DeductionName=100, Code=50), FK to DeductionCategoryType, defaults (IsPercentage=1, IsPayeDeductible=0, IsActive=1, SortOrder=0, CreatedAtUtc)
    - _Requirements: 7.1, 7.4_

  - [ ] 2.3 Create PayslipPeriodComplianceFiling entity and configuration
    - Create `Portal.Infrastructure/Entities/PayslipPeriodComplianceFiling.cs` with properties: Id, PayslipPeriodId, ComplianceFilingId, ContributionTotal, UpdatedAtUtc, UpdatedByUserId, CreatedAtUtc
    - Add DbSet<PayslipPeriodComplianceFiling> to PortalDbContext
    - Configure entity: schema "payroll", PK, max length (UpdatedByUserId=450), FK to PayslipPeriod, FK to BusinessApplication, default CreatedAtUtc
    - _Requirements: 9.1_

  - [ ] 2.4 Add IsPayeApplicable property to existing Employee entity
    - Locate existing `Employee.cs` entity in `Portal.Infrastructure/Entities/`
    - Add `public bool IsPayeApplicable { get; set; }` property
    - Update EF Core configuration for Employee entity: add `.HasDefaultValue(false)` for IsPayeApplicable
    - _Requirements: 2.1, 8.5_

  - [ ] 2.5 Add IsPayeDeductible property to existing DeductionType entity
    - Locate existing `DeductionType.cs` entity in `Portal.Infrastructure/Entities/`
    - Add `public bool IsPayeDeductible { get; set; }` property
    - Update EF Core configuration for DeductionType entity: add `.HasDefaultValue(false)` for IsPayeDeductible
    - _Requirements: 1.3, 3.2, 3.6_

  - [ ] 2.6 Make DeductionRateHistoryId nullable on PayslipDeductionLine entity
    - Locate existing `PayslipDeductionLine.cs` entity in `Portal.Infrastructure/Entities/`
    - Change `public int DeductionRateHistoryId { get; set; }` to `public int? DeductionRateHistoryId { get; set; }`
    - Update EF Core configuration: change FK relationship to optional (`.IsRequired(false)`)
    - Update any existing INSERT queries in PayrollRepository that reference DeductionRateHistoryId to handle NULL values
    - _Requirements: 3.7_

- [ ] 3. Seed data
  - [ ] 3.1 Seed Cyprus PAYE tax bands (2024)
    - Create SQL script `Portal.Database/Seeds/Seed_CyprusPAYETaxBands2024.sql`
    - USE [Portal] header, IF NOT EXISTS guard
    - INSERT 5 bands: €0–€19,500 at 0%, €19,500.01–€28,000 at 20%, €28,000.01–€36,300 at 25%, €36,300.01–€60,000 at 30%, €60,000.01–NULL at 35%
    - CountryCode = 'CY', EffectiveFromYear = 2024, EffectiveToYear = NULL
    - _Requirements: 1.1, 7.7_

  - [ ] 3.2 Seed Cyprus country deduction templates
    - Create SQL script `Portal.Database/Seeds/Seed_CyprusDeductionTemplates.sql`
    - USE [Portal] header, IF NOT EXISTS guard
    - INSERT 7 templates with IsPayeDeductible flag:
      - SI Employee (0.0880, Cat 1, IsPayeDeductible=1)
      - GESY Employee (0.0265, Cat 1, IsPayeDeductible=1)
      - SI Employer (0.0880, Cat 2, IsPayeDeductible=0)
      - Redundancy (0.0120, Cat 2, IsPayeDeductible=0)
      - Industrial Training (0.0050, Cat 2, IsPayeDeductible=0)
      - Social Cohesion (0.0200, Cat 2, IsPayeDeductible=0)
      - GESY Employer (0.0290, Cat 2, IsPayeDeductible=0)
    - All with CountryCode = 'CY', IsPercentage = 1, IsActive = 1
    - _Requirements: 7.2, 7.4_

- [ ] 4. DTOs and request models
  - [ ] 4.1 Create Phase D DTOs
    - Create `Portal.Infrastructure/Models/Payroll/PhaseDDtos.cs`
    - Include: PayeTaxBandDto, CountryDeductionTemplateDto, ContributionReportDto, ContributionTypeSummary, EmployeeContributionDetail, ContributionLineItem, ComplianceFilingLinkDto, PayslipPeriodComplianceFilingDto
    - All DTOs per design spec with full property lists
    - _Requirements: 5.2, 5.4, 5.5, 6.1, 6.3, 9.4_

  - [ ] 4.2 Create Phase D request models
    - Create `Portal.Infrastructure/Models/Payroll/PhaseDRequests.cs`
    - Include: CreateCountryTemplateRequest, UpdateCountryTemplateRequest, CreateTaxBandRequest, UpdateTaxBandRequest
    - All request models per design spec
    - _Requirements: 7.3, 7.8_

  - [ ] 4.3 Create PayeCalculationResult and PayeBandBreakdown models
    - Create `Portal.Infrastructure/Models/Payroll/PayeCalculationResult.cs`
    - Include: PayeCalculationResult (IsValid, ValidationError, AnnualProjectedIncome, AnnualTax, MonthlyPaye, EffectiveRate, TopMarginalRate, BandBreakdowns list), PayeBandBreakdown (LowerBound, UpperBound, Rate, TaxableAmountInBand, TaxForBand)
    - _Requirements: 1.1, 1.4, 1.5, 1.6_

- [ ] 5. Repository layer
  - [ ] 5.1 Add PAYE tax band repository methods to PayrollRepository
    - Add `GetTaxBandsAsync(string countryCode, int year)`: SELECT from PayeTaxBand WHERE CountryCode = @countryCode AND EffectiveFromYear <= @year AND (EffectiveToYear IS NULL OR EffectiveToYear >= @year), ORDER BY LowerBound ASC
    - Add `GetTaxBandByIdAsync(int id)`: SELECT single band
    - Add `InsertTaxBandAsync(PayeTaxBand band)`: INSERT with SqlParameters
    - Add `UpdateTaxBandAsync(PayeTaxBand band)`: UPDATE LowerBound, UpperBound, Rate, EffectiveFromYear, EffectiveToYear WHERE Id = @Id
    - Full table names in queries, `catch (Exception ex) { throw; }` pattern
    - _Requirements: 1.7, 1.8, 7.8, 8.1_

  - [ ] 5.2 Add country template repository methods to PayrollRepository
    - Add `GetTemplatesByCountryAsync(string countryCode)`: SELECT from CountryDeductionTemplate WHERE CountryCode = @countryCode AND IsActive = 1 ORDER BY SortOrder
    - Add `GetTemplateByIdAsync(int id)`: SELECT single template
    - Add `InsertTemplateAsync(CountryDeductionTemplate template)`: INSERT with SqlParameters
    - Add `UpdateTemplateAsync(CountryDeductionTemplate template)`: UPDATE DeductionName, DefaultRate, SortOrder WHERE Id = @Id
    - Add `DeactivateTemplateAsync(int id)`: UPDATE IsActive = 0 WHERE Id = @Id
    - Full table names in queries, `catch (Exception ex) { throw; }` pattern
    - _Requirements: 7.1, 7.3, 7.5_

  - [ ] 5.3 Add compliance cross-reference repository methods
    - Add `GetComplianceFilingsByPeriodAsync(int periodId)`: SELECT from PayslipPeriodComplianceFiling WHERE PayslipPeriodId = @periodId ORDER BY CreatedAtUtc DESC
    - Add `InsertComplianceFilingLinkAsync(PayslipPeriodComplianceFiling link)`: INSERT with SqlParameters
    - Full table names in queries, `catch (Exception ex) { throw; }` pattern
    - _Requirements: 9.1, 9.2, 9.3, 9.5_

  - [ ] 5.4 Add contribution report repository methods
    - Add `GetEmployerContributionsForPeriodAsync(int periodId, int businessId)`: SELECT deduction lines WHERE DeductionCategoryTypeId = 2 (employer) from finalised payslips in the period, JOIN to Payslip, PayslipPeriod, Employee, DeductionType
    - Add `GetPayeDeductionTypeIdForBusinessAsync(int businessId)`: SELECT Id FROM DeductionType WHERE BusinessId = @businessId AND Code = 'PAYE'
    - Add `UpdateEmployeePayeStatusAsync(int employeeId, int businessId, bool isPayeApplicable)`: UPDATE Employee SET IsPayeApplicable = @isPayeApplicable WHERE Id = @employeeId AND BusinessId = @businessId
    - Full table names in queries, `catch (Exception ex) { throw; }` pattern
    - _Requirements: 2.1, 5.2, 5.4, 5.6_

  - [ ] 5.5 Add compliance repository helper methods
    - Locate existing compliance repository or create a new method in an appropriate repository
    - Add `FindSocialInsuranceFilingAsync(int businessId, int year, int month)`: SELECT from [compliance].[BusinessApplication] WHERE BusinessId = @businessId AND ApplicationTypeId = (Social Insurance type) AND YEAR(DueDate) = @dueYear AND MONTH(DueDate) = @dueMonth. Apply 1-month offset: if month < 12, dueMonth = month + 1, dueYear = year; if month = 12, dueMonth = 1, dueYear = year + 1 (filing for July is due in August; filing for December is due in January of next year)
    - Add `UpdateEstimatedAmountAsync(int filingId, decimal amount)`: UPDATE [compliance].[BusinessApplication] SET EstimatedAmount = @amount WHERE Id = @filingId
    - Full table names in queries, `catch (Exception ex) { throw; }` pattern
    - _Requirements: 4.2, 4.3_

- [ ] 6. Build checkpoint
  - Ensure the project compiles with all new entities, DTOs, repository methods
  - Verify DbContext configuration compiles and new DbSets are registered
  - Verify no missing references or type errors
  - Ask the user if questions arise

- [ ] 7. PayeCalculationService — pure progressive tax calculation
  - [ ] 7.1 Create IPayeCalculationService interface and implementation
    - Create `Portal.Infrastructure/Services/IPayeCalculationService.cs` with method: `PayeCalculationResult CalculateMonthlyPaye(decimal monthlyTaxableIncome, List<PayeTaxBand> bands)`
    - Create `Portal.Infrastructure/Services/PayeCalculationService.cs` implementing the interface
    - Register as Singleton (pure calculation, no I/O, no state)
    - Algorithm:
      1. If bands is null or empty, return IsValid = false with ValidationError "No tax bands configured"
      2. If monthlyTaxableIncome <= 0, return IsValid = true with MonthlyPaye = 0
      3. Compute `annualProjected = monthlyTaxableIncome * 12`
      4. For each band ordered by LowerBound ascending: compute bandWidth, taxableInBand, taxForBand, accumulate annualTax, populate BandBreakdowns, track topMarginalRate (last band rate where taxableInBand > 0)
      5. Compute `monthlyPaye = Math.Round(annualTax / 12, 2, MidpointRounding.AwayFromZero)`
      6. Compute `effectiveRate = annualProjected > 0 ? annualTax / annualProjected : 0`
      7. Set `topMarginalRate` = rate of the highest band where income falls
      8. Return populated PayeCalculationResult
    - `catch (Exception ex) { throw; }` pattern
    - _Requirements: 1.1, 1.2, 1.4, 1.5, 1.6, 1.9_

  - [ ]* 7.2 Write property tests for PayeCalculationService
    - Create `Portal.Tests/Unit/Payroll/PhaseD/PayeCalculationServicePropertyTests.cs`
    - Use FsCheck.Xunit with minimum 100 iterations per property
    - **Property 1: Progressive Tax Band Calculation** — For any monthly taxable income >= 0 and valid non-overlapping tax bands, total annual tax equals sum of individual band taxes, each band tax = min(income_in_band, band_width) × rate, monthly PAYE has exactly 2 decimal places, monthly PAYE = Round(annual_tax / 12, 2, AwayFromZero)
    - **Validates: Requirements 1.1, 1.4, 1.5, 1.6**
    - **Property 2: Taxable Income Equals Gross Minus PAYE-Deductible Amounts** — For any payslip input with IsPayeApplicable = true, the PAYE base amount equals TotalEarnings minus the sum of all employee deductions where IsPayeDeductible = 1
    - **Validates: Requirements 1.3, 3.2, 3.6**
    - Tag format: `Feature: payroll-phase-d, Property N: {title}`
    - _Requirements: 11.2_

  - [ ]* 7.3 Write unit tests for PayeCalculationService boundary cases
    - Create `Portal.Tests/Unit/Payroll/PhaseD/PayeCalculationServiceTests.cs`
    - Test boundary cases with Cyprus 2024 bands:
      - Monthly €1,625 (annual €19,500) → PAYE = €0.00
      - Monthly €1,625.08 (annual €19,501) → PAYE = €0.02
      - Monthly €2,333.33 (annual €28,000) → PAYE = €141.67
      - Monthly €3,025 (annual €36,300) → PAYE = €347.92
      - Monthly €5,000 (annual €60,000) → PAYE = €660.42
      - Monthly €6,250 (annual €75,000) → PAYE = €1,098.33
    - Test edge cases: zero income → €0, negative income → €0, no bands → IsValid = false
    - _Requirements: 11.2_

- [ ] 8. PayslipCalculationOrchestrator — wraps existing engine + PAYE
  - [ ] 8.1 Create IPayslipCalculationOrchestrator interface and implementation
    - Create `Portal.Infrastructure/Services/IPayslipCalculationOrchestrator.cs` with method: `Task<PayslipCalculationResult> CalculateWithPayeAsync(PayslipCalculationInput input, bool isPayeApplicable)`
    - Create `Portal.Infrastructure/Services/PayslipCalculationOrchestrator.cs` implementing the interface
    - Register as Scoped (needs repository access for tax bands)
    - Inject: `IPayslipCalculationEngine`, `IPayeCalculationService`, `PayrollRepository`, `ICurrentTenantService`, `IBusinessService`
    - Include static `CountryCodeMapping` dictionary: `{ "Cyprus" → "CY", "Malta" → "MT", "United Kingdom" → "GB" }` for mapping BusinessProfile.Country to ISO code
    - Flow:
      1. Call `_calculationEngine.Calculate(input)` — existing pure engine
      2. If `!isPayeApplicable` or result is invalid, return as-is
      3. Identify PAYE-deductible deductions: from `input.ApplicableDeductions`, build a set of DeductionTypeIds where `IsPayeDeductible = 1 AND DeductionCategoryTypeId = 1`. Sum `CalculatedAmount` from result's `DeductionLines` where DeductionTypeId is in that set.
      4. Compute `taxableIncome = result.TotalEarnings - payeDeductibleTotal`
      5. Get business profile → map Country to ISO code via CountryCodeMapping → load tax bands from repository for that code and period year
      6. Call `_payeService.CalculateMonthlyPaye(taxableIncome, bands)`
      7. If PAYE result is invalid, return validation error
      8. Append PAYE ComputedDeductionLine: DeductionTypeId = PAYE type (from repo), BaseAmount = taxableIncome, Rate = top marginal rate (highest band rate that applies), CalculatedAmount = monthlyPaye, DeductionCategoryTypeId = 1, DeductionRateHistoryId = NULL (nullable — PAYE uses bands, not rate history)
      9. Recalculate TotalEmployeeDeductions and NetSalary
      10. Return updated result
    - `catch (Exception ex) { throw; }` pattern
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8_

  - [ ]* 8.2 Write property tests for PayslipCalculationOrchestrator
    - Create `Portal.Tests/Unit/Payroll/PhaseD/PayslipCalculationOrchestratorPropertyTests.cs`
    - Use FsCheck.Xunit with minimum 100 iterations per property
    - **Property 3: PAYE Skipped When Disabled** — For any payslip input where IsPayeApplicable is false, the orchestrator returns a result with zero PAYE deduction lines
    - **Validates: Requirements 2.3**
    - **Property 4: PAYE Line Included When Enabled** — For any payslip input where IsPayeApplicable is true and valid tax bands exist, the result contains exactly one PAYE deduction line with DeductionCategoryTypeId = 1 and non-negative CalculatedAmount
    - **Validates: Requirements 3.1, 3.7**
    - **Property 5: Net Salary Invariant** — For any valid payslip calculation result, NetSalary equals TotalEarnings - TotalEmployeeDeductions where TotalEmployeeDeductions is the sum of all lines with DeductionCategoryTypeId = 1
    - **Validates: Requirements 3.3**
    - Mock IPayslipCalculationEngine and repository, use FsCheck generators for PayslipCalculationInput
    - Tag format: `Feature: payroll-phase-d, Property N: {title}`
    - _Requirements: 11.2, 11.5, 11.6_

- [ ] 9. ComplianceIntegrationService
  - [ ] 9.1 Create IComplianceIntegrationService interface and implementation
    - Create `Portal.Infrastructure/Services/IComplianceIntegrationService.cs` with method: `Task<ServiceResult> UpdateComplianceFilingFromPayrollAsync(int periodId, int businessId, string userId)`
    - Create `Portal.Infrastructure/Services/ComplianceIntegrationService.cs` implementing the interface
    - Register as Scoped
    - Inject: `PayrollRepository`, compliance repository (or direct DbContext access), `ILogger<ComplianceIntegrationService>`
    - Flow:
      1. Load all finalised payslips for the period
      2. Sum employer SI contribution lines (Code = "SI_Contribution", DeductionCategoryTypeId = 2)
      3. Find matching BusinessApplication with 1-month offset: filing for July's payroll has DueDate in August. Query: YEAR(DueDate) = @payrollYear AND MONTH(DueDate) = @payrollMonth + 1 (with December wraparound to January of next year: if payrollMonth = 12, look for MONTH(DueDate) = 1 AND YEAR(DueDate) = payrollYear + 1)
      4. If no matching filing found: log warning via Serilog, return success (non-blocking)
      5. Update BusinessApplication.EstimatedAmount with the calculated total
      6. Create new PayslipPeriodComplianceFiling record (always insert, never update — preserves history)
      7. Return success
    - `catch (Exception ex) { throw; }` pattern
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 9.2, 9.3, 9.5_

  - [ ]* 9.2 Write property test for ComplianceIntegrationService
    - Create `Portal.Tests/Unit/Payroll/PhaseD/ComplianceIntegrationServicePropertyTests.cs`
    - Use FsCheck.Xunit with minimum 100 iterations per property
    - **Property 6: Compliance Sum Filters to SI Employer Only** — For any set of finalised payslip deduction lines with multiple employer contribution types (SI, Redundancy, Industrial Training, Social Cohesion, GESY), the compliance service computes a total that includes ONLY lines with Code "SI_Contribution" (DeductionCategoryTypeId = 2)
    - **Validates: Requirements 4.6**
    - Mock repository returning generated payslip data with FsCheck generators
    - Tag format: `Feature: payroll-phase-d, Property 6: Compliance Sum Filters to SI Employer Only`
    - _Requirements: 4.6, 11.3_

- [ ] 10. CountryTemplateService
  - [ ] 10.1 Create ICountryTemplateService interface and implementation
    - Create `Portal.Infrastructure/Services/ICountryTemplateService.cs` with all method signatures per design
    - Create `Portal.Infrastructure/Services/CountryTemplateService.cs` implementing the interface
    - Register as Scoped
    - Inject: `PayrollRepository`, `ILogger<CountryTemplateService>`
    - Implement CRUD methods: GetTemplatesByCountryAsync, CreateTemplateAsync, UpdateTemplateAsync, DeactivateTemplateAsync, GetTaxBandsAsync, CreateTaxBandAsync, UpdateTaxBandAsync
    - Implement `ImportCountryTemplatesForBusinessAsync`:
      1. Load active templates for the country
      2. Check if business already has deduction types with matching codes — return warning if duplicates found
      3. For each template: create DeductionType record (scoped to business) with `IsPayeDeductible` flag propagated from the template, and create DeductionRateHistory entry with `DefaultRate * 100` (converting from decimal to percentage format)
      4. Create PAYE DeductionType (Code: "PAYE", IsPercentage: false, DeductionCategoryTypeId: 1, IsPayeDeductible: 0) if not already present
      5. Return success with count of imported templates
    - `catch (Exception ex) { throw; }` pattern
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.8, 8.3, 8.4_

- [ ] 11. Modify PayrollService — integration points
  - [ ] 11.1 Integrate orchestrator into batch payslip generation and SaveEarningLinesAsync
    - In the existing `ConfirmBatchGenerationAsync` method, replace direct `_calculationEngine.Calculate(input)` call with `await _orchestrator.CalculateWithPayeAsync(input, employee.IsPayeApplicable)`
    - In the existing `SaveEarningLinesAsync` method, ALSO replace direct `_calculationEngine.Calculate(input)` call with `await _orchestrator.CalculateWithPayeAsync(input, employee.IsPayeApplicable)` — ensures PAYE is recalculated when earnings change
    - Inject `IPayslipCalculationOrchestrator` into PayrollService constructor
    - When `IsPayeApplicable` is false, orchestrator returns same result as original engine (transparent)
    - _Requirements: 3.1, 3.2, 3.3_

  - [ ] 11.2 Add PAYE toggle endpoint to PayrollService
    - Add `UpdateEmployeePayeStatusAsync(int businessId, int employeeId, bool isPayeApplicable)` to IPayrollService
    - Implementation: validate employee belongs to business, call repository `UpdateEmployeePayeStatusAsync`, return ServiceResult
    - _Requirements: 2.1, 2.2, 2.3_

  - [ ] 11.3 Add contribution report methods to PayrollService
    - Add `GetContributionReportAsync(int periodId, int businessId)` to IPayrollService
    - Implementation: load employer contributions for period, group by DeductionType, build per-employee detail, check for compliance filing link, return ContributionReportDto
    - Add `ExportContributionReportToExcelAsync(int periodId, int businessId)`: use ClosedXML, branded header, columns (Employee Name, SI, Redundancy, Industrial Training, Social Cohesion, GESY, Total), footer totals
    - Add `GenerateContributionReportPdfAsync(int periodId, int businessId)`: render via IViewRenderService, generate PDF via IPayslipPdfService
    - Add `GetComplianceFilingHistoryAsync(int periodId, int businessId)`: load cross-reference records, join to AspNetUsers for UpdatedByUserName
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 5.9, 5.10, 9.4_

- [ ] 12. Modify finalisation hooks — compliance integration call
  - [ ] 12.1 Add compliance integration to FinalisePeriodAsync and RefinalisePeriodAsync
    - Locate existing `FinalisePeriodAsync` and `RefinalisePeriodAsync` in PayslipPeriodStatusService (or PayrollService)
    - After successful status transition to Finalised/ReFinalised, add: `await _complianceIntegrationService.UpdateComplianceFilingFromPayrollAsync(periodId, businessId, userId)`
    - Inject `IComplianceIntegrationService` into the service constructor
    - Non-blocking: if compliance integration fails, log error but do not fail the finalisation
    - Wrap compliance call in try/catch — on failure log warning and continue
    - _Requirements: 4.1, 4.4, 4.5, 9.2_

- [ ] 13. Build checkpoint
  - Ensure the project compiles with all service implementations (PayeCalculationService, PayslipCalculationOrchestrator, ComplianceIntegrationService, CountryTemplateService, PayrollService modifications)
  - Verify constructor injection resolves correctly
  - Verify orchestrator integration in ConfirmBatchGenerationAsync compiles
  - Ask the user if questions arise

- [ ] 14. Controllers
  - [ ] 14.1 Create PayrollComplianceController
    - Create `Portal.Web/Controllers/PayrollComplianceController.cs`
    - Apply `[Authorize]` and `[ModuleAccess(PortalModules.Payroll)]` attributes
    - Inject: `IPayrollService`, `ICurrentTenantService`
    - Page action: `ContributionReport(int? year, int? month)` — load available periods, default to latest, pass model to view
    - AJAX: `AxGetContributionReportData(int periodId)` — call GetContributionReportAsync, return Json
    - AJAX: `AxGetDownloadContributionReportExcel(int periodId)` — call ExportContributionReportToExcelAsync, return File
    - AJAX: `AxGetDownloadContributionReportPdf(int periodId)` — call GenerateContributionReportPdfAsync, return File
    - All endpoints pass `_tenantService.CurrentBusinessId` for tenant isolation
    - `catch (Exception ex)` → Json error response pattern
    - _Requirements: 5.1, 5.8, 5.9, 10.1, 10.2_

  - [ ] 14.2 Create PayrollTemplateController (SuperAdmin only)
    - Create `Portal.Web/Controllers/PayrollTemplateController.cs`
    - Apply `[Authorize(Roles = "SuperAdmin")]` attribute
    - Inject: `ICountryTemplateService`
    - Page actions: `Index()` — list templates grouped by country; `TaxBands(string countryCode)` — list bands for country
    - AJAX endpoints:
      - `AxPostCreateTemplate(CreateCountryTemplateRequest request)` — validate, call service, return Json
      - `AxPostUpdateTemplate(UpdateCountryTemplateRequest request)` — validate, call service, return Json
      - `AxPostDeactivateTemplate(int id)` — call service, return Json
      - `AxPostCreateTaxBand(CreateTaxBandRequest request)` — validate, call service, return Json
      - `AxPostUpdateTaxBand(UpdateTaxBandRequest request)` — validate, call service, return Json
    - ValidateAntiForgeryToken on all POST actions
    - `catch (Exception ex)` → Json error response pattern
    - _Requirements: 7.3, 7.8, 10.3_

  - [ ] 14.3 Add PAYE toggle endpoint to PayrollController
    - Add to existing `PayrollController`:
      - `[HttpPost] [ValidateAntiForgeryToken] AxPostToggleEmployeePaye(int employeeId, bool isPayeApplicable)` — call PayrollService.UpdateEmployeePayeStatusAsync, return Json success/fail
    - Check Owner or SuperAdmin role before allowing toggle
    - If employee projected income <= €19,500 and isPayeApplicable = true, return Json with `warning` flag (client will show SweetAlert2 warning but still allow)
    - `catch (Exception ex)` → Json error response pattern
    - _Requirements: 2.1, 2.2, 2.6, 10.4_

- [ ] 15. Views — PAYE toggle on Employee form
  - [ ] 15.1 Add PAYE toggle to Employee profile edit view
    - Locate existing Employee edit view
    - Add PAYE section with checkbox toggle: `<input type="checkbox" id="isPayeApplicable" />`
    - Label: "Subject to PAYE Income Tax"
    - Informational note: "PAYE applies when projected annual income exceeds €19,500"
    - JavaScript:
      - On checkbox change: BlockUI.show('Updating...') → fetch POST to AxPostToggleEmployeePaye → BlockUI.hide()
      - If response has `warning` flag: Swal.fire warning "This employee's projected annual income (€{amount}) does not exceed the PAYE threshold (€19,500). PAYE calculation will result in €0." with Confirm/Cancel
      - On confirm: proceed with toggle; on cancel: revert checkbox
      - On success: Swal.fire success
      - On error: Swal.fire error, revert checkbox
    - Include antiforgery token in POST header
    - _Requirements: 2.4, 2.5, 2.6_

- [ ] 16. Views — Contribution Report
  - [ ] 16.1 Create ContributionReport view
    - Create `Portal.Web/Views/PayrollCompliance/ContributionReport.cshtml`
    - Topbar: eyebrow "Payroll", heading "Employer Contribution Report", muted description
    - Filter card (`.glass.card-pad`, margin-bottom:22px): Year dropdown, Month dropdown, Filter button, Clear button
    - Summary card (`.glass.card-pad`): one row per contribution type (SI, Redundancy, Industrial Training, Social Cohesion, GESY) with total amount, styled as summary boxes
    - Detail card (`.glass.card-pad`): table with columns: Employee Name, SI, Redundancy, Industrial Training, Social Cohesion, GESY, Total
    - Footer row: grand total of all employer contributions
    - Compliance link section: if filing exists, show status badge (Filed/Pending) linking to the filing detail
    - Action buttons: "Export to Excel" (calls AxGetDownloadContributionReportExcel), "Download PDF" (calls AxGetDownloadContributionReportPdf)
    - Empty state: "No finalised payslips for {Month Name} {Year}"
    - JavaScript: filter submits → BlockUI → fetch AxGetContributionReportData → BlockUI.hide → render table; download buttons trigger file downloads
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 5.9, 5.10_

  - [ ] 16.2 Add Contribution Report navigation link to payroll sidebar
    - Locate the payroll sidebar navigation partial view (same file that contains "Earnings Breakdown" and "Period Summary" links from Phase C)
    - Add "Contribution Report" navigation link: `/PayrollCompliance/ContributionReport`
    - Place in the same section as other payroll report links
    - Use consistent icon and styling with existing sidebar items
    - _Requirements: 10.1, 10.2_

- [ ] 17. Views — SuperAdmin Template Management
  - [ ] 17.1 Create PayrollTemplate Index view (country templates)
    - Create `Portal.Web/Views/PayrollTemplate/Index.cshtml`
    - Topbar: eyebrow "Platform Admin", heading "Country Deduction Templates"
    - Country filter: dropdown or tabs for country codes (CY, MT, UK, etc.)
    - Template table (`.glass.card-pad`): columns — Name, Code, Category (Employee/Employer), Rate %, Sort Order, Active (badge), Actions (Edit, Deactivate)
    - "Add Template" button opens SweetAlert2 form modal with fields: DeductionName, Code, IsPercentage, Category dropdown, DefaultRate, SortOrder
    - Edit action: SweetAlert2 form pre-populated with current values (DeductionName, DefaultRate, SortOrder editable)
    - Deactivate action: SweetAlert2 confirmation dialog with warning text
    - All AJAX actions follow BlockUI → fetch → BlockUI.hide → Swal pattern
    - Include antiforgery token in POST headers
    - _Requirements: 7.3, 7.8, 10.3_

  - [ ] 17.2 Create PayrollTemplate TaxBands view
    - Create `Portal.Web/Views/PayrollTemplate/TaxBands.cshtml`
    - Topbar: eyebrow "Platform Admin", heading "PAYE Tax Bands — {CountryName}"
    - Tax band table (`.glass.card-pad`): columns — Lower Bound (€), Upper Bound (€ or "No limit"), Rate %, From Year, To Year (or "Current"), Actions (Edit)
    - "Add Tax Band" button: SweetAlert2 form with fields: LowerBound, UpperBound (optional), Rate, EffectiveFromYear, EffectiveToYear (optional)
    - Edit action: SweetAlert2 form pre-populated
    - All AJAX actions follow BlockUI → fetch → BlockUI.hide → Swal pattern
    - Include antiforgery token in POST headers
    - _Requirements: 7.8, 8.1_

- [ ] 18. Views — Compliance Filing Detail Enhancement
  - [ ] 18.1 Extend compliance filing detail view with payroll source indicator
    - Locate existing compliance filing detail view (Business Applications Tracker)
    - Add payroll source section when EstimatedAmount was populated from payroll:
      - Source label: "Auto-calculated from Payroll — {Month Name} {Year}"
      - Per-employee breakdown expandable section showing individual SI contributions
      - Manual override input: allow owner to override amount, show difference indicator "Manual override — differs from payroll calculation by €{difference}"
    - Add "Payroll Cross-Reference" section showing PayslipPeriodComplianceFiling history (date, amount, updated by)
    - Display only when PayslipPeriodComplianceFiling records exist for this filing
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6_

- [ ] 19. DI registration
  - [ ] 19.1 Register all Phase D services in DI container
    - Register `IPayeCalculationService` / `PayeCalculationService` as Singleton
    - Register `IPayslipCalculationOrchestrator` / `PayslipCalculationOrchestrator` as Scoped
    - Register `IComplianceIntegrationService` / `ComplianceIntegrationService` as Scoped
    - Register `ICountryTemplateService` / `CountryTemplateService` as Scoped
    - Verify existing `IPayrollService` registration picks up new injected dependencies (IPayslipCalculationOrchestrator)
    - Verify existing `PayslipPeriodStatusService` (or equivalent) registration picks up IComplianceIntegrationService injection
    - No new NuGet packages needed — FsCheck, ClosedXML, PuppeteerSharp already in project
    - _Requirements: 10.1_

- [ ] 20. Build checkpoint
  - Ensure the entire Phase D compiles: all services, controllers, views, DI registrations
  - Verify controller routes are accessible
  - Verify new view paths resolve correctly
  - Verify orchestrator integration doesn't break existing payslip generation flow
  - Ask the user if questions arise

- [ ] 21. Unit tests
  - [ ]* 21.1 Write unit tests for ComplianceIntegrationService
    - Create `Portal.Tests/Unit/Payroll/PhaseD/ComplianceIntegrationServiceTests.cs`
    - Test: no matching filing → log warning, return success (non-blocking)
    - Test: matching filing exists → updates EstimatedAmount with correct SI total
    - Test: only SI_Contribution lines included (not Redundancy, Industrial Training, etc.)
    - Test: creates PayslipPeriodComplianceFiling record with correct ContributionTotal and UpdatedByUserId
    - Test: re-finalisation creates new cross-reference record (preserves history)
    - Use Moq for repository dependencies
    - _Requirements: 4.1, 4.4, 4.6, 9.2, 9.5, 11.3_

  - [ ]* 21.2 Write unit tests for CountryTemplateService
    - Create `Portal.Tests/Unit/Payroll/PhaseD/CountryTemplateServiceTests.cs`
    - Test: ImportCountryTemplatesForBusinessAsync creates correct DeductionType records
    - Test: rate conversion from decimal (0.0880) to percentage (8.80) during import
    - Test: duplicate detection returns warning when templates already imported
    - Test: PAYE DeductionType created with IsPercentage = false
    - Test: deactivation succeeds without affecting existing business records
    - Use Moq for repository dependencies
    - _Requirements: 7.5, 7.6, 8.3, 8.4_

  - [ ]* 21.3 Write unit tests for PayslipCalculationOrchestrator
    - Create `Portal.Tests/Unit/Payroll/PhaseD/PayslipCalculationOrchestratorTests.cs`
    - Test: IsPayeApplicable = false → returns engine result unchanged, no PAYE line
    - Test: IsPayeApplicable = true → appends PAYE line, recalculates NetSalary
    - Test: PAYE base amount = TotalEarnings minus sum of all IsPayeDeductible=1 employee deductions
    - Test: no tax bands → returns validation error
    - Test: PAYE = €0 when income below threshold → still includes PAYE line with CalculatedAmount = 0
    - Test: NetSalary = TotalEarnings - TotalEmployeeDeductions (including PAYE)
    - Test: PAYE deduction line has DeductionRateHistoryId = NULL
    - Test: PAYE deduction line Rate = top marginal rate (not effective rate)
    - Use Moq for IPayslipCalculationEngine, IPayeCalculationService, PayrollRepository
    - _Requirements: 2.3, 3.1, 3.3, 3.7, 11.5, 11.6_

- [ ] 22. What's New announcement
  - [ ] 22.1 Create What's New announcement seed SQL for Phase D
    - Create `Portal.Database/Seeds/Seed_WhatsNew_PayrollPhaseD.sql`
    - USE [Portal] header, IF NOT EXISTS guard on Title
    - Title: "PAYE Tax & Compliance Integration"
    - Summary: Brief description of PAYE income tax calculation, compliance auto-population, and employer contribution reporting
    - DetailHtml: Bullet list covering — Automatic PAYE income tax calculation with Cyprus 2024 progressive bands, Per-employee PAYE opt-in toggle, Auto-population of Social Insurance Compliance filing amounts on payslip finalisation, Employer contribution breakdown report with Excel/PDF export, Cross-reference audit trail between payroll and compliance, Country-specific deduction templates for multi-country expansion
    - ModuleKey: 'payroll'
    - CtaLabel: 'Open Contribution Report', CtaUrl: '/PayrollCompliance/ContributionReport'
    - IsActive: 1, PublishedAtUtc: GETUTCDATE()
    - _Requirements: N/A (user-facing announcement)_

- [ ] 23. Final checkpoint
  - Ensure all Phase D code compiles end-to-end
  - Verify PAYE calculation produces correct results for boundary test cases
  - Verify compliance integration hook fires on finalisation without blocking
  - Verify orchestrator returns identical results to existing engine when IsPayeApplicable = false (no regression)
  - Verify all existing Phase A/B/C tests still pass
  - Ask the user if questions arise

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP delivery
- The existing `PayslipCalculationEngine` (Singleton, pure) is NOT modified — the orchestrator wraps it transparently
- `PayeCalculationService` is registered as Singleton because it is a pure function with no I/O or state
- `PayslipCalculationOrchestrator` is Scoped because it needs repository access to load tax bands
- Compliance integration is NON-BLOCKING: if no filing is found or the update fails, log a warning and continue — never fail payslip finalisation
- Country templates are SuperAdmin-only; importing creates business-scoped copies (DeductionType + DeductionRateHistory)
- Rate format conversion: `CountryDeductionTemplate.DefaultRate` stores decimals (0.0880 = 8.80%), but `DeductionRateHistory.Rate` stores percentages (8.80). Import must multiply by 100.
- The `ComplianceFilingId` FK references the existing `[compliance].[BusinessApplication]` table
- PAYE DeductionType has `IsPercentage = false` because PAYE uses progressive bands, not a flat rate
- When `IsPayeApplicable = false`, the orchestrator returns the exact same result as calling the engine directly — zero regression risk
- All download/export endpoints must validate tenant isolation via `_tenantService.CurrentBusinessId`
- Property-based testing IS applicable for Phase D (PAYE calculation is a pure function with universal properties)
- FsCheck.Xunit is already in the project — no new NuGet packages needed
- **Issue 1 (IsPayeDeductible):** The orchestrator identifies PAYE-deductible deductions via the `IsPayeDeductible` flag on `DeductionType` — NOT by matching Code strings. The `CountryDeductionTemplate` also includes `IsPayeDeductible` so the flag propagates during import.
- **Issue 3 (1-month offset):** Filing for July's payroll has DueDate in August. The compliance integration applies `+1 month` offset (with December → January wraparound).
- **Issue 4 (Country code mapping):** The orchestrator uses a static `CountryCodeMapping` dictionary to map `BusinessProfile.Country` (e.g., "Cyprus") to ISO code (e.g., "CY") for PayeTaxBand lookup.
- **Issue 5 (SaveEarningLinesAsync):** Both `ConfirmBatchGenerationAsync` AND `SaveEarningLinesAsync` use the orchestrator. PAYE is recalculated whenever earnings change.
- **Issue 6 (Nullable DeductionRateHistoryId):** `PayslipDeductionLine.DeductionRateHistoryId` is nullable (INT NULL). PAYE lines use NULL. Existing code must be checked to handle NULL values.
- **Issue 9 (Rate field):** PAYE deduction line `Rate` = top marginal rate (highest applicable band), not blended effective rate. Clearer for display.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "1.4", "1.5", "1.6"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3", "2.4", "2.5", "2.6"] },
    { "id": 2, "tasks": ["3.1", "3.2", "4.1", "4.2", "4.3"] },
    { "id": 3, "tasks": ["5.1", "5.2", "5.3", "5.4", "5.5"] },
    { "id": 4, "tasks": ["7.1"] },
    { "id": 5, "tasks": ["7.2", "7.3", "8.1"] },
    { "id": 6, "tasks": ["8.2", "9.1", "10.1"] },
    { "id": 7, "tasks": ["9.2", "11.1", "11.2", "11.3"] },
    { "id": 8, "tasks": ["12.1"] },
    { "id": 9, "tasks": ["14.1", "14.2", "14.3", "19.1"] },
    { "id": 10, "tasks": ["15.1", "16.1", "16.2", "17.1", "17.2"] },
    { "id": 11, "tasks": ["18.1"] },
    { "id": 12, "tasks": ["21.1", "21.2", "21.3"] },
    { "id": 13, "tasks": ["22.1"] }
  ]
}
```
