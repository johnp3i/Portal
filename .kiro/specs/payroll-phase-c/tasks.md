# Implementation Plan: Payroll Phase C (Reporting & Export)

## Overview

Phase C implements the reporting and export layer for the Payroll module, replacing three Phase A stub implementations (PayslipRenderer, PayslipPdfService, PayslipEmailService) with production-ready versions, and introducing four new report views, Excel/PDF export, and email delivery. Implementation follows a strict dependency order: database schema first, then EF Core entities, DTOs, repository layer, service replacements, new report service, controller, views, and finally responsive CSS. All new tables reside in the `[payroll]` schema. PuppeteerSharp and ClosedXML are already available in the project. PayslipRenderer and PayslipPdfService implementations move to `Portal.Web/Services/` (same as InvoiceRenderer and InvoicePdfService) since they depend on web-layer services; their interfaces remain in Infrastructure.

## Tasks

- [x] 1. Database schema and EF Core setup
  - [x] 1.1 Create PayslipEmailLog table SQL migration
    - Create SQL script `Portal.Database/Seeds/Seed_PayslipEmailLog.sql`
    - USE [Portal] header
    - CREATE TABLE `[payroll].[PayslipEmailLog]` with columns: Id (INT IDENTITY PK), PayslipId (INT NOT NULL FK → [payroll].[Payslip]), SentByUserId (NVARCHAR(450) NOT NULL), SentToEmail (NVARCHAR(256) NOT NULL), SentAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE()), IsSuccess (BIT NOT NULL), FailureReason (NVARCHAR(500) NULL), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE())
    - Add FK constraint `[FK_PayslipEmailLog_Payslip]` referencing `[payroll].[Payslip]([Id])`
    - Create index `IX_PayslipEmailLog_PayslipId` on (PayslipId) INCLUDE (SentAtUtc, IsSuccess)
    - _Requirements: 10.1, 10.2, 10.6_

  - [x] 1.2 Add Payroll value to EmailDepartmentEnum
    - Locate `EmailDepartmentEnum.cs` in the email services area
    - Add `Payroll` enum value
    - _Requirements: 7.7_

  - [x] 1.3 Create PayslipEmailLog EF Core entity and DbContext configuration
    - Create `Portal.Infrastructure/Entities/PayslipEmailLog.cs` with all properties per design
    - Add DbSet<PayslipEmailLog> to PortalDbContext
    - Configure entity: schema `payroll`, PK, max lengths, defaults (SentAtUtc, CreatedAtUtc with GETUTCDATE()), FK to Payslip
    - _Requirements: 10.1, 10.2, 10.6_

- [x] 2. DTOs and View Models
  - [x] 2.1 Create PDF View Models
    - Create `Portal.Infrastructure/Models/Payroll/PayslipPdfViewModels.cs`
    - Include: PayslipPdfViewModel, AnnualSummaryPdfViewModel, EmployeeStatementPdfViewModel
    - All models per design spec with full property lists
    - _Requirements: 1.1, 1.2, 1.3, 8.3, 12.2_

  - [x] 2.2 Create Report DTOs
    - Create `Portal.Infrastructure/Models/Payroll/PayrollReportDtos.cs`
    - Include: EmployeePayslipHistoryDto, PayslipHistoryItemDto, AnnualSummaryDto, MonthlySummaryRow, DeductionSummaryRow, EarningSummaryRow, EarningsBreakdownFilter, EarningsBreakdownDto, EarningTypeSummaryRow, EarningDetailRow, PeriodSummaryDto, PeriodSummaryRow, PayslipEmailLogDto, PayslipEmailSummaryDto
    - All DTOs per design spec
    - _Requirements: 3.5, 3.6, 4.2, 4.3, 4.4, 4.5, 5.2, 5.7, 5.8, 6.2, 6.4, 10.3, 10.5_

- [ ] 3. Repository layer
  - [~] 3.1 Create PayslipEmailLogRepository
    - Create `Portal.Infrastructure/Repositories/PayslipEmailLogRepository.cs`
    - Extend `GenericStoredProcedureRepository<PayslipEmailLog>`
    - Implement `InsertAsync(PayslipEmailLog entity)`: INSERT with SqlParameters, null-safe FailureReason
    - Implement `GetByPayslipIdAsync(int payslipId)`: SELECT all logs for a payslip, ORDER BY SentAtUtc DESC
    - Implement `GetLastByPayslipIdAsync(int payslipId)`: SELECT TOP 1 successful send for duplicate detection
    - Full table names in queries, `catch (Exception ex) { throw; }` pattern
    - _Requirements: 7.9, 7.12, 10.1, 10.3, 10.4_

  - [~] 3.2 Add report query methods to PayrollRepository
    - Add `GetPayslipsByEmployeeAsync(int employeeId, int businessId, int? year)`: all payslips for an employee, optional year filter
    - Add `GetFinalisedPayslipsForEmployeeYearAsync(int employeeId, int businessId, int year)`: WHERE PayslipStatusTypeId IN (3, 5)
    - Add `GetEarningLinesForPayslipsAsync(int[] payslipIds)`: earning lines for multiple payslips
    - Add `GetDeductionLinesForPayslipsAsync(int[] payslipIds)`: deduction lines for multiple payslips
    - Add `GetEarningsBreakdownAsync(int businessId, EarningsBreakdownFilter filter)`: filtered earning lines with joins to Employee and EarningType
    - Add `GetPeriodSummaryAsync(int periodId, int businessId, int? departmentId)`: aggregated per-employee summary for a period
    - Add `GetAvailableYearsForEmployeeAsync(int employeeId, int businessId)`: SELECT DISTINCT Year from PayslipPeriod
    - Add `GetFinalisedPayslipsForPeriodAsync(int periodId, int businessId)`: all finalised payslips with detail for batch operations
    - Add `GetEmailSummaryForPeriodAsync(int periodId, int businessId)`: aggregate email log counts for period
    - All queries filter to PayslipStatusTypeId IN (3, 5) where applicable
    - _Requirements: 3.2, 3.4, 4.2, 4.8, 5.2, 5.9, 6.2, 6.5, 10.5_

- [~] 4. Build checkpoint
  - Ensure the project compiles with all new entities, DTOs, repository methods
  - Verify DbContext configuration compiles and new DbSet is registered
  - Verify no missing references or type errors
  - Ask the user if questions arise

- [ ] 5. PayslipRenderer — replace stub with full implementation
  - [~] 5.1 Replace PayslipRenderer stub with Razor-based implementation
    - Delete the existing stub at `Portal.Infrastructure/Services/PayslipRenderer.cs`
    - Create the implementation at `Portal.Web/Services/PayslipRenderer.cs` (same location as InvoiceRenderer)
    - Inject `IViewRenderService` and `ILogoService` via constructor
    - Implement `RenderPayslipHtmlAsync`: build `PayslipPdfViewModel` from parameters, call `_viewRenderService.RenderViewToStringAsync("~/Views/Payroll/PdfTemplates/Payslip.cshtml", model)`
    - `catch (Exception ex) { throw; }` pattern
    - Ensure DI registration resolves correctly (interface `IPayslipRenderer` stays in Infrastructure)
    - _Requirements: 2.1, 2.2, 2.3_

  - [~] 5.2 Create Payslip PDF Razor template
    - Create `Portal.Web/Views/Payroll/PdfTemplates/Payslip.cshtml`
    - `@model PayslipPdfViewModel` directive
    - Self-contained HTML with inline CSS for PDF compatibility
    - A4 dimensions (210mm x 297mm), 48px/52px internal padding
    - Sections: Header (logo + business name/address), Employee Details (Name, Position, SIN, ID Number), Period (Month/Year), Earnings table (Description, Hours, Multiplier, Amount), Employee Deductions table (Name, Rate %, Amount), Net Salary highlight box, Employer Contributions table (Name, Rate %, Amount), Total Cost summary, Manager Notes (conditional on model), Footer
    - Brand colours: #0D5EA6 primary, Manrope headings, Inter body
    - Match approved mockup at `.kiro/docs/mockups/payroll-phase-c-payslip-pdf.html`
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 1.9, 1.10_

- [ ] 6. PayslipPdfService — replace stub with PuppeteerSharp implementation
  - [~] 6.1 Replace PayslipPdfService stub with PuppeteerSharp implementation
    - Delete the existing stub at `Portal.Infrastructure/Services/PayslipPdfService.cs`
    - Create the implementation at `Portal.Web/Services/PayslipPdfService.cs` (same location as InvoicePdfService)
    - Update the `IPayslipPdfService` interface (in Portal.Infrastructure) to add `CancellationToken` parameter and `GenerateBatchPdfAsync` method:
      - `Task<byte[]> GeneratePdfAsync(string html, CancellationToken cancellationToken = default)`
      - `Task<List<byte[]>> GenerateBatchPdfAsync(List<string> htmlDocuments, CancellationToken cancellationToken = default)`
    - Inject `IWebHostEnvironment`, `ILogoService`, `ICurrentTenantService` via constructor
    - Implement `GeneratePdfAsync`: embed logo as base64 (same pattern as InvoicePdfService), then generate PDF via PuppeteerSharp
    - Implement `GenerateBatchPdfAsync`: launch browser once, iterate documents generating PDFs with page reuse, respect CancellationToken
    - PuppeteerSharp config: Headless = true, `--no-sandbox` args, A4 format, PrintBackground = true, zero margins (template defines internal padding)
    - WaitUntil = Networkidle0
    - `catch (Exception ex) { throw; }` pattern
    - Verify DI registration resolves correctly (interface `IPayslipPdfService` stays in Infrastructure)
    - _Requirements: 2.1, 2.2, 2.8_

- [ ] 7. PayslipEmailService — replace stub with production implementation
  - [~] 7.1 Add SendPayslipEmailAsync method to IEmailService
    - Extend `IEmailService` interface with: `Task SendPayslipEmailAsync(string toEmail, string employeeName, string businessName, string monthName, int year, byte[] pdfBytes, string filename)`
    - Implement in `PortalEmailService`: use existing `SendEmailWithAttachmentAsync` infrastructure
    - Subject: "Your Payslip - {MonthName} {Year}"
    - Body: branded HTML message "Please find attached your payslip for {MonthName} {Year}. If you have any questions, please contact your manager."
    - Department: `EmailDepartmentEnum.Payroll`
    - _Requirements: 7.7, 7.8_

  - [~] 7.2 Replace PayslipEmailService stub with full implementation
    - **Note:** Read the existing implementation first. If Phase A created a working version, extend/complete it rather than blindly overwriting. The existing code may already have some of the logic in place.
    - Rewrite `Portal.Infrastructure/Services/PayslipEmailService.cs`
    - Inject: `IPayslipPdfService`, `IPayslipRenderer`, `IEmailService`, `PayslipEmailLogRepository`, `PayrollRepository`, `IBusinessService`, `ICurrentTenantService`
    - Implement `SendPayslipAsync(int payslipId, int businessId, string userId, bool includeSignature)`:
      1. Load payslip detail from repository
      2. Validate employee has email (return Fail if not)
      3. Load business info (name, address)
      4. Render HTML via PayslipRenderer → Generate PDF via PayslipPdfService
      5. Build filename: "{EmployeeName}_Payslip_{MonthName}_{Year}.pdf"
      6. Send via IEmailService.SendPayslipEmailAsync
      7. Log success to PayslipEmailLog
      8. On failure: log with IsSuccess=false and FailureReason, rethrow
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.9, 7.10_

  - [~] 7.3 Implement SendAllPayslipsAsync for batch email sending
    - **Note:** Read the existing implementation first. If Phase A created a working version, extend/complete it rather than blindly overwriting. The existing code may already have some of the logic in place.
    - In `PayslipEmailService`, implement `SendAllPayslipsAsync(int periodId, int businessId, string userId, bool includeSignature)`:
      1. Get all finalised payslips for period (PayslipStatusTypeId IN (3, 5))
      2. Filter to employees with valid email addresses
      3. Respect `BatchEmailMaxSize` from PayrollSettings — return error if batch exceeds maximum
      4. Add delay of `BatchEmailDelayBetweenSendsMs` between sends
      5. Iterate sequentially: for each payslip, call SendPayslipAsync
      6. Track counts: sent, failed, skipped (no email)
      7. Broadcast SignalR progress after each send via `IHubContext<PayrollHub>`
      8. On per-payslip failure: log to PayslipEmailLog, increment failed count, continue
      9. Return ServiceResult with summary message: "{sent} sent, {failed} failed, {skipped} skipped"
    - _Requirements: 7.5, 7.10, 7.11_

  - [ ] 7.4 Add batch email configuration and SignalR progress notifications
    - Create `Portal.Infrastructure/Models/PayrollSettings.cs` with `BatchEmailMaxSize` (default 50) and `BatchEmailDelayBetweenSendsMs` (default 500)
    - Add `"Payroll"` section to `appsettings.json` with the two settings
    - Register `IOptions<PayrollSettings>` in Program.cs via `builder.Services.Configure<PayrollSettings>(builder.Configuration.GetSection("Payroll"))`
    - Inject `IOptions<PayrollSettings>` and `IHubContext<PayrollHub>` into PayslipEmailService
    - In SendAllPayslipsAsync: check batch size against BatchEmailMaxSize (return error if exceeded), add delay between sends, broadcast SignalR progress after each send
    - Create `Portal.Web/Hubs/PayrollHub.cs` (empty hub class with `[Authorize]` — used for grouping payroll real-time notifications)
    - Map hub endpoint in Program.cs: `app.MapHub<PayrollHub>("/hubs/payroll")`
    - _Requirements: 7.5, 7.10, 7.11_

- [ ] 8. PayrollReportService — new report orchestration service
  - [~] 8.1 Create IPayrollReportService interface and basic class structure
    - Create `Portal.Infrastructure/Services/IPayrollReportService.cs` with all method signatures per design
    - Create `Portal.Infrastructure/Services/PayrollReportService.cs` implementing the interface
    - Inject: `PayrollRepository`, `IPayslipPdfService`, `IPayslipRenderer`, `IBusinessService`, `PayslipEmailLogRepository`, `IViewRenderService`, `ILogoService`
    - _Requirements: 3.1, 4.1, 5.1, 6.1_

  - [~] 8.2 Implement GetEmployeeHistoryAsync
    - Call `_payrollRepository.GetPayslipsByEmployeeAsync(employeeId, businessId, year)`
    - Call `_payrollRepository.GetAvailableYearsForEmployeeAsync(employeeId, businessId)`
    - Map to `EmployeePayslipHistoryDto` with summary totals (TotalGross, TotalNet, Count)
    - Return DTO with ordered payslip list (newest first)
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

  - [~] 8.3 Implement GetAnnualSummaryAsync and GenerateAnnualSummaryPdfAsync
    - `GetAnnualSummaryAsync`: load finalised payslips for year, load earning + deduction lines, aggregate by month/type, return `AnnualSummaryDto`
    - Monthly breakdown: Gross, Deductions, Net, Contributions per month
    - Deduction breakdown: group by DeductionType, sum amounts, count months applied
    - Contribution breakdown: group employer-portion deductions by type
    - Earnings breakdown: group by EarningType, sum amounts
    - `GenerateAnnualSummaryPdfAsync`: build `AnnualSummaryPdfViewModel`, render via `IViewRenderService` ("~/Views/Payroll/PdfTemplates/AnnualSummary.cshtml"), generate PDF via `IPayslipPdfService`
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8, 12.1, 12.2, 12.3, 12.4, 12.5, 12.6, 12.7_

  - [~] 8.4 Implement GetEarningsBreakdownAsync and ExportEarningsBreakdownToExcelAsync
    - `GetEarningsBreakdownAsync`: call repository with filter, aggregate totals per earning type, return `EarningsBreakdownDto`
    - `ExportEarningsBreakdownToExcelAsync`: use ClosedXML to create XLSX workbook
      - Header row with branded styling (#0D5EA6 background, white text, bold)
      - Columns: Employee Name, Period, Earning Type, Description, Amount
      - Auto-adjust column widths
      - Amount column formatted as `#,##0.00`
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 5.9, 5.10_

  - [~] 8.5 Implement GetPeriodSummaryAsync with PDF and Excel export
    - `GetPeriodSummaryAsync`: call repository, map to `PeriodSummaryDto` with aggregate totals footer
    - `GeneratePeriodSummaryPdfAsync`: build model, render via `IViewRenderService` ("~/Views/Payroll/PdfTemplates/PeriodSummary.cshtml"), generate PDF
    - `ExportPeriodSummaryToExcelAsync`: ClosedXML workbook with columns: Employee Name, Department, Gross, Deductions, Net, Employer Contributions, Total Cost. Branded header row. Footer totals row.
    - Optional department filter
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.7, 6.8, 6.9_

  - [~] 8.6 Implement GenerateEmployeeStatementPdfAsync
    - Load all finalised payslips for employee within date range (startYear/Month to endYear/Month)
    - If no payslips found, return empty result (controller will handle validation message)
    - Build `EmployeeStatementPdfViewModel` with cover section (name, position, SIN, ID, period), summary totals, and individual payslip details
    - Render via `IViewRenderService` ("~/Views/Payroll/PdfTemplates/EmployeeStatement.cshtml")
    - Generate PDF with filename: "{EmployeeName}_Statement_{StartMonth}{StartYear}_to_{EndMonth}{EndYear}.pdf"
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.8, 8.9_

  - [~] 8.7 Implement GenerateAllPayslipsPdfZipAsync
    - Load all finalised payslips for period
    - Render all HTML documents first via PayslipRenderer
    - Use `IPayslipPdfService.GenerateBatchPdfAsync(htmlDocuments)` to generate all PDFs with browser reuse (single browser instance)
    - Package results into ZipArchive with filename "{EmployeeName}_Payslip_{MonthName}_{Year}.pdf" per entry
    - Use `System.IO.Compression.ZipArchive` with `CompressionLevel.Optimal`
    - Return ZIP bytes
    - _Requirements: 2.6_

  - [~] 8.8 Implement email log query methods
    - `GetEmailLogForPayslipAsync(int payslipId)`: call repository, map to `PayslipEmailLogDto` list (join to AspNetUsers for SentByUserName)
    - `GetLastEmailForPayslipAsync(int payslipId)`: call repository for last successful send (for duplicate detection)
    - `GetEmailSummaryForPeriodAsync(int periodId, int businessId)`: call repository for aggregate counts
    - _Requirements: 7.12, 10.3, 10.5_

- [~] 9. Build checkpoint
  - Ensure the project compiles with all service implementations (PayslipRenderer, PayslipPdfService, PayslipEmailService, PayrollReportService)
  - Verify constructor injection resolves correctly for replaced stubs
  - Verify IEmailService extension compiles with existing implementations
  - Ask the user if questions arise

- [ ] 10. PDF Razor templates (Annual Summary, Period Summary, Employee Statement)
  - [~] 10.1 Create Annual Summary PDF Razor template
    - Create `Portal.Web/Views/Payroll/PdfTemplates/AnnualSummary.cshtml`
    - `@model AnnualSummaryPdfViewModel`
    - Self-contained HTML with inline CSS, A4 dimensions
    - Sections: Header (Business Name, Employee Name, SIN, Year), Monthly Breakdown table (Month, Gross, Deductions, Net, Contributions, with Totals row), Deductions Breakdown table (Name, Total, Months Applied), Employer Contributions Breakdown table (Name, Total, Months Applied), Footer
    - Brand styling matching Payslip template
    - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.5, 12.6_

  - [~] 10.2 Create Period Summary PDF Razor template
    - Create `Portal.Web/Views/Payroll/PdfTemplates/PeriodSummary.cshtml`
    - `@model PeriodSummaryDto`
    - Self-contained HTML with inline CSS, A4 dimensions (landscape for wide table)
    - Sections: Header (Business Name, Period Month/Year, Department filter if applied), Employee table (Name, Department, Gross, Deductions, Net, Employer Contributions, Total Cost), Footer totals row
    - Brand styling
    - _Requirements: 6.7_

  - [~] 10.3 Create Employee Statement PDF Razor template
    - Create `Portal.Web/Views/Payroll/PdfTemplates/EmployeeStatement.cshtml`
    - `@model EmployeeStatementPdfViewModel`
    - Self-contained HTML with inline CSS, A4 dimensions
    - Sections: Cover (Employee Name, Position, SIN, ID Number, Period From/To, Business Name/Address), Summary totals (Total Gross, Deductions, Net, Contributions), Individual payslip detail sections (one per payslip with earning/deduction tables)
    - Same branded styling as individual payslip PDF
    - _Requirements: 8.3, 8.4, 8.5, 8.8_

- [ ] 11. PayrollReportController — new controller with page actions and endpoints
  - [~] 11.1 Create PayrollReportController with page actions
    - Create `Portal.Web/Controllers/PayrollReportController.cs`
    - Apply `[Authorize]` and `[ModuleAccess(PortalModules.Payroll)]` attributes
    - Inject: `IPayrollReportService`, `IPayrollService`, `ICurrentTenantService`, `IPayslipEmailService`
    - Page actions:
      - `EmployeeHistory(int employeeId, int? year)` → calls service, returns View
      - `AnnualSummary(int employeeId, int? year)` → calls service, returns View
      - `EarningsBreakdown(EarningsBreakdownFilter filter)` → calls service, returns View
      - `PeriodSummary(int periodId, int? departmentId)` → calls service, returns View
    - Set ViewBag.CanSendEmail flag based on user claims (Owner/SuperAdmin)
    - _Requirements: 3.1, 4.1, 5.1, 6.1, 11.1, 11.2_

  - [~] 11.2 Add download and export AJAX endpoints
    - **Important:** All download/export endpoints MUST pass `_tenantService.CurrentBusinessId` to every service call. This ensures tenant isolation — a user cannot download payslips from another business by manipulating query parameters. The repository queries already filter by businessId, so passing the correct business ID is sufficient.
    - `AxGetDownloadPayslipPdf(int payslipId)`: generate single payslip PDF, return File result with content-type "application/pdf"
    - `AxGetDownloadAllPayslipsPdf(int periodId)`: generate ZIP of all payslips, return File with "application/zip" and filename "Payslips_{MonthName}_{Year}.zip"
    - `AxGetDownloadAnnualSummaryPdf(int employeeId, int year)`: generate annual summary PDF, return File
    - `AxGetDownloadPeriodSummaryPdf(int periodId, int? departmentId)`: generate period summary PDF, return File
    - `AxGetDownloadEmployeeStatement(int employeeId, int startYear, int startMonth, int endYear, int endMonth)`: generate statement PDF, return File
    - `AxGetExportEarningsBreakdown(EarningsBreakdownFilter filter)`: generate XLSX, return File with "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    - `AxGetExportPeriodSummary(int periodId, int? departmentId)`: generate XLSX, return File
    - All endpoints: try/catch with Json error on failure, validate business ownership
    - _Requirements: 2.4, 2.5, 2.6, 4.7, 5.10, 6.7, 6.8, 8.1, 8.9, 12.7_

  - [~] 11.3 Add email send and log AJAX endpoints
    - `AxPostSendPayslipEmail(int payslipId, bool confirmResend = false)`:
      - Check Owner/SuperAdmin role, return 403 if unauthorised
      - If not confirmResend: check for existing successful send, return `{ success: false, alreadySent: true, sentDate }` if found
      - Call `PayslipEmailService.SendPayslipAsync`
      - Return Json success/fail
    - `AxPostSendAllPayslipEmails(int periodId)`:
      - Check Owner/SuperAdmin role
      - Call `PayslipEmailService.SendAllPayslipsAsync`
      - Return Json with summary (sent, failed, skipped counts)
    - `AxGetEmailLog(int payslipId)`: return Json list of email log entries
    - `AxGetPeriodEmailSummary(int periodId)`: return Json with total/successful/failed counts
    - _Requirements: 7.2, 7.5, 7.9, 7.11, 7.12, 10.3, 10.5, 11.3, 11.4, 11.5_

- [ ] 12. DI registration
  - [~] 12.1 Register Phase C services in DI container
    - Register `IPayrollReportService` / `PayrollReportService` as Scoped
    - Register `PayslipEmailLogRepository` as Scoped (resolve PortalDbContext)
    - Register `IOptions<PayrollSettings>` via `builder.Services.Configure<PayrollSettings>(builder.Configuration.GetSection("Payroll"))`
    - Map `PayrollHub` endpoint: `app.MapHub<PayrollHub>("/hubs/payroll")`
    - Existing registrations for IPayslipPdfService, IPayslipEmailService, IPayslipRenderer remain unchanged (implementations are replaced, not registrations) — ensure `using` directives reference `Portal.Web.Services` for PayslipPdfService and PayslipRenderer
    - Verify constructor injection auto-resolves new dependencies for replaced stubs (IViewRenderService, ILogoService, IWebHostEnvironment, IOptions<PayrollSettings>, IHubContext<PayrollHub>, etc.)
    - _Requirements: 11.1_

- [~] 13. Build checkpoint
  - Ensure the project compiles with PayrollReportController, all service registrations, and PDF templates
  - Verify route resolution for new controller
  - Verify no missing view paths or model type mismatches
  - Ask the user if questions arise

- [ ] 14. Report views — Employee History and Annual Summary
  - [~] 14.1 Create Employee History view
    - Create `Portal.Web/Views/PayrollReport/EmployeeHistory.cshtml`
    - `@model EmployeePayslipHistoryDto`
    - Topbar: eyebrow "Payroll Reports", heading "Employee History — {EmployeeName}", muted description
    - Filter card (`.glass.card-pad`, margin-bottom:22px): year dropdown populated from `Model.AvailableYears`, Filter button, Clear button
    - Data table card (`.glass.card-pad`): columns — Period (Month/Year), Total Earnings (formatted currency), Net Salary, Status badge, Actions (View link, Download PDF link)
    - Summary row below table: Total Gross, Total Net, Payslip Count
    - Empty state: "No payslips found for {Year}" when list is empty
    - JavaScript: year filter triggers page reload with query param, Download PDF calls `AxGetDownloadPayslipPdf`
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7_

  - [~] 14.2 Create Annual Summary view
    - Create `Portal.Web/Views/PayrollReport/AnnualSummary.cshtml`
    - `@model AnnualSummaryDto`
    - Topbar: eyebrow "Payroll Reports", heading "Annual Summary — {EmployeeName}", year in description
    - Filter card: year selector from `Model.AvailableYears`, Filter/Clear buttons
    - Main card (`.glass.card-pad`): 
      - Summary boxes (Total Gross, Total Deductions, Total Net, Total Contributions)
      - Monthly breakdown table (Month, Gross, Deductions, Net, Contributions) with totals row
      - Earnings by type table (Type Name, Total Amount)
      - Deductions by type table (Name, Total, Months Applied)
      - Contributions by type table (Name, Total, Months Applied)
    - Action buttons: "Download PDF" (calls `AxGetDownloadAnnualSummaryPdf`)
    - Empty state: "No finalised payslips for {Year}" when no data
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8_

- [ ] 15. Report views — Earnings Breakdown and Period Summary
  - [~] 15.1 Create Earnings Breakdown view
    - Create `Portal.Web/Views/PayrollReport/EarningsBreakdown.cshtml`
    - `@model EarningsBreakdownDto`
    - Topbar: eyebrow "Payroll Reports", heading "Earnings Breakdown"
    - Filter card: From (month/year selects), To (month/year selects), Employee dropdown (optional), Earning Type multi-select, Filter/Clear buttons
    - Summary section: cards showing total per earning type (EarningTypeName + TotalAmount)
    - Detail table (`.glass.card-pad`): columns — Employee Name, Period, Earning Type, Description, Amount
    - Action buttons: "Export to Excel" (calls `AxGetExportEarningsBreakdown`)
    - JavaScript: filter form submits as GET with query params, Export triggers file download
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 5.9, 5.10_

  - [~] 15.2 Create Period Summary view
    - Create `Portal.Web/Views/PayrollReport/PeriodSummary.cshtml`
    - `@model PeriodSummaryDto`
    - Topbar: eyebrow "Payroll Reports", heading "Period Summary — {MonthName} {Year}"
    - Filter card: Period selector (year/month dropdowns), Department dropdown (optional), Filter/Clear buttons
    - Data table (`.glass.card-pad`): columns — Employee Name, Department, Total Earnings, Total Deductions, Net Salary, Employer Contributions, Total Cost
    - Footer row: aggregate totals for all columns
    - Action buttons: "Download PDF" (calls `AxGetDownloadPeriodSummaryPdf`), "Export to Excel" (calls `AxGetExportPeriodSummary`)
    - Empty state: "No finalised payslips for {Month Name} {Year}"
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 6.8, 6.9_

- [ ] 16. Email UI integration (payslip detail and period views)
  - [~] 16.1 Add email send buttons and duplicate detection to existing views
    - On PayslipDetail view: add "Send by Email" button (visible only to Owner/SuperAdmin via ViewBag.CanSendEmail)
    - Disable button with tooltip "Employee email address not configured" when employee has no email
    - On PeriodDetail view: add "Send All by Email" button (visible only to Owner/SuperAdmin)
    - On PayslipDetail view: add "Email History" section showing last send date/status (fetched via `AxGetEmailLog`)
    - On PeriodDetail view: add email summary badge (total sent/failed) fetched via `AxGetPeriodEmailSummary`
    - _Requirements: 7.2, 7.4, 7.5, 10.3, 10.5, 11.3, 11.4, 11.6_

  - [~] 16.2 Implement email send JavaScript with SweetAlert2 flows
    - `sendPayslipEmail(payslipId)` function:
      - BlockUI.show('Sending payslip...')
      - POST to AxPostSendPayslipEmail
      - BlockUI.hide()
      - If response `alreadySent`: SweetAlert2 warning "This payslip was already emailed on {date}. Send again?" with confirm/cancel
      - On confirm resend: repeat call with `confirmResend=true`
      - On success: SweetAlert2 success "Payslip emailed successfully"
      - On error: SweetAlert2 error with message
    - `sendAllPayslipEmails(periodId)` function:
      - SweetAlert2 confirmation dialog showing employee count and skipped count
      - On confirm: connect to `/hubs/payroll` SignalR hub, listen for `BatchEmailProgress` events, display real-time progress modal showing current/total and last employee processed
      - BlockUI.show('Sending payslips...') → POST → update progress modal with each SignalR event → BlockUI.hide() on completion → SweetAlert2 summary
    - Include antiforgery token in POST headers
    - _Requirements: 7.2, 7.5, 7.6, 7.11, 7.12, 11.5_

  - [~] 16.3 Add Download PDF and Download All buttons to existing payslip views
    - On PayslipDetail view: add "Download PDF" button for Finalised/Re-finalised payslips
    - JavaScript: `downloadPayslipPdf(payslipId)` triggers file download via `window.location.href` to `AxGetDownloadPayslipPdf`
    - On PeriodDetail view: add "Download All (ZIP)" button for Finalised/Re-finalised periods
    - JavaScript: `downloadAllPayslips(periodId)` triggers file download to `AxGetDownloadAllPayslipsPdf`
    - _Requirements: 2.5, 2.6_

  - [~] 16.4 Add Export Statement action to employee views
    - On Employee detail and EmployeeHistory views: add "Export Statement" button
    - JavaScript: SweetAlert2 modal with date range picker (start month/year, end month/year selects)
    - On confirm: trigger file download to `AxGetDownloadEmployeeStatement` with selected range
    - Validate start is before end before submitting
    - _Requirements: 8.1, 8.2, 8.7_

  - [ ] 16.5 Add Payroll Reports navigation links
    - In the payroll navigation section (sidebar or submenu), add links to:
      - "Earnings Breakdown" → /PayrollReport/EarningsBreakdown
      - "Period Summary" → /PayrollReport/PeriodSummary
    - Employee History and Annual Summary are accessed from the employee detail page (no sidebar link needed)
    - Add links from PeriodDetail view: "View Period Report" → /PayrollReport/PeriodSummary?periodId={id}
    - Add links from Employee detail page: "View History" → /PayrollReport/EmployeeHistory?employeeId={id}, "Annual Summary" → /PayrollReport/AnnualSummary?employeeId={id}
    - _Requirements: 3.1, 4.1, 5.1, 6.1_

- [ ] 17. Mobile responsive CSS
  - [~] 17.1 Add responsive CSS for Phase C report views
    - Add CSS rules (in site.css or a payroll-specific stylesheet) for breakpoints at 810px and 375px
    - At ≤810px: stack filter controls vertically (flex-direction: column, 100% width), convert wide data tables to horizontally scrollable containers (overflow-x: auto), ensure action buttons meet 44px minimum touch target
    - At ≤375px: reduce topbar heading to 28px, ensure body text minimum 14px
    - Ensure pagination controls have adequate spacing between buttons on mobile
    - Apply to all four report views (EmployeeHistory, AnnualSummary, EarningsBreakdown, PeriodSummary)
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6_

- [~] 18. Build and integration checkpoint
  - Ensure the entire Phase C compiles: all services, repository methods, controller, views, templates, CSS
  - Verify replaced stubs resolve correctly at runtime (DI)
  - Verify PDF template view paths resolve (no missing cshtml files)
  - Verify controller routes are accessible
  - Ask the user if questions arise

- [ ] 19. Unit tests
  - [ ]* 19.1 Write unit tests for PayslipEmailService
    - Create `Portal.Tests/Unit/Payroll/PhaseC/PayslipEmailServiceTests.cs`
    - Test: payslip not found → ServiceResult.Fail
    - Test: employee has no email → ServiceResult.Fail with "Employee email address not configured"
    - Test: successful send → IEmailService.SendPayslipEmailAsync called with correct params, PayslipEmailLog created with IsSuccess=true
    - Test: send failure → PayslipEmailLog created with IsSuccess=false and FailureReason
    - Test: SendAllPayslipsAsync skips employees without email, tracks counts correctly
    - Test: batch continues after individual failure
    - Use Moq for all dependencies
    - _Requirements: 7.1, 7.3, 7.4, 7.9, 7.10, 7.11_

  - [ ]* 19.2 Write unit tests for PayrollReportService
    - Create `Portal.Tests/Unit/Payroll/PhaseC/PayrollReportServiceTests.cs`
    - Test: GetEmployeeHistoryAsync returns correctly ordered list (newest first)
    - Test: GetAnnualSummaryAsync aggregates monthly totals correctly with known test data
    - Test: GetAnnualSummaryAsync excludes Draft/Preview payslips
    - Test: GetEarningsBreakdownAsync applies all filter parameters correctly
    - Test: GetPeriodSummaryAsync applies department filter when provided
    - Test: GenerateAllPayslipsPdfZipAsync includes correct number of entries
    - Test: GenerateEmployeeStatementPdfAsync returns empty when no payslips in range
    - Use Moq for repository and service mocks
    - _Requirements: 3.2, 3.4, 4.2, 4.8, 5.9, 6.5, 8.6_

  - [ ]* 19.3 Write unit tests for PayrollReportController
    - Create `Portal.Tests/Unit/Payroll/PhaseC/PayrollReportControllerTests.cs`
    - Test: AxPostSendPayslipEmail by non-Owner/non-SuperAdmin → returns 403 Json response
    - Test: AxPostSendPayslipEmail with alreadySent payslip and confirmResend=false → returns alreadySent warning
    - Test: AxPostSendAllPayslipEmails by Owner → calls service and returns summary
    - Test: AxGetDownloadPayslipPdf returns FileResult with correct content type
    - Test: page actions pass correct parameters to service
    - Use Moq for all service dependencies
    - _Requirements: 11.3, 11.5, 7.12, 2.5_

  - [ ]* 19.4 Write unit tests for PayslipRenderer
    - Create `Portal.Tests/Unit/Payroll/PhaseC/PayslipRendererTests.cs`
    - Test: correct view path passed to IViewRenderService
    - Test: model correctly populated from parameters (business name, address, include signature flag)
    - Use Moq for IViewRenderService and ILogoService
    - _Requirements: 2.1, 2.2, 2.3_

- [ ] 20. What's New announcement
  - [~] 20.1 Create What's New announcement seed SQL for Phase C
    - Create `Portal.Database/Seeds/Seed_WhatsNew_PayrollPhaseC.sql`
    - USE [Portal] header, IF NOT EXISTS guard on Title
    - Title: "Payroll Reports & PDF Export"
    - Summary: Brief description of reporting, PDF generation, email delivery, and Excel export capabilities
    - DetailHtml: Bullet list covering — Download payslip PDFs, Employee history & annual summary, Earnings breakdown with Excel export, Period summary reports, Email payslips to employees, Employee statement generation
    - ModuleKey: 'payroll'
    - CtaLabel: 'Open Payroll Reports', CtaUrl: '/PayrollReport/PeriodSummary'
    - IsActive: 1, PublishedAtUtc: GETUTCDATE()
    - _Requirements: N/A (user-facing announcement)_

- [~] 21. Final checkpoint
  - Ensure all Phase C code compiles end-to-end
  - Verify all replaced stubs have correct constructor dependencies resolved by DI
  - Verify all PDF templates render without missing model properties
  - Verify email flow compiles: controller → service → renderer → PDF → email → log
  - Ask the user if questions arise

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP delivery
- Three Phase A stubs are being REPLACED (PayslipRenderer, PayslipPdfService, PayslipEmailService) — these are complete rewrites of existing files, not new classes
- **PayslipRenderer** and **PayslipPdfService** are moved from `Portal.Infrastructure/Services/` to `Portal.Web/Services/` (same location as InvoiceRenderer and InvoicePdfService) because they depend on web-layer services. Their interfaces remain in Infrastructure.
- PuppeteerSharp is already in the project (used by InvoicePdfService) — no new NuGet package needed
- ClosedXML is already in the project (used by ExcelParser) — no new NuGet package needed
- IViewRenderService already exists (used by InvoiceRenderer) — same rendering pattern applies
- All report queries filter to `PayslipStatusTypeId IN (3, 5)` — Finalised and Re-finalised only
- Email send actions are role-restricted to Owner/SuperAdmin only; report viewing is open to all payroll users
- PDF templates use self-contained inline CSS (no external stylesheets) for PuppeteerSharp compatibility
- Excel exports use branded header styling (#0D5EA6 background, white bold text)
- ZIP generation uses System.IO.Compression (built-in .NET) with `GenerateBatchPdfAsync` for browser reuse
- Batch email sending is capped by `PayrollSettings.BatchEmailMaxSize` (default 50) with real-time SignalR progress notifications
- All download/export endpoints must validate tenant isolation via `_tenantService.CurrentBusinessId`
- Property-based testing was assessed and determined NOT applicable for Phase C (see design document Testing Strategy section)

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["1.3", "2.1", "2.2"] },
    { "id": 2, "tasks": ["3.1", "3.2"] },
    { "id": 3, "tasks": ["5.1", "6.1", "7.1"] },
    { "id": 4, "tasks": ["5.2", "7.2", "7.3"] },
    { "id": 5, "tasks": ["7.4", "8.1"] },
    { "id": 6, "tasks": ["8.2", "8.3", "8.4", "8.5", "8.6", "8.7", "8.8"] },
    { "id": 7, "tasks": ["10.1", "10.2", "10.3", "12.1"] },
    { "id": 8, "tasks": ["11.1", "11.2", "11.3"] },
    { "id": 9, "tasks": ["14.1", "14.2", "15.1", "15.2"] },
    { "id": 10, "tasks": ["16.1", "16.2", "16.3", "16.4", "16.5"] },
    { "id": 11, "tasks": ["17.1"] },
    { "id": 12, "tasks": ["19.1", "19.2", "19.3", "19.4"] },
    { "id": 13, "tasks": ["20.1"] }
  ]
}
```
