# Requirements Document

## Introduction

Phase C of the Payroll module delivers Reporting and Export capabilities on top of the Phase A Core Engine. This phase implements the payslip PDF generation service (replacing the Phase A stub), employee payslip history views, annual summaries for tax return preparation, earnings breakdown reports by type, business-level period summaries, email delivery of payslips with PDF attachments, and employee statement exports for date ranges. All report views are mobile responsive. The module remains gated to Enterprise-tier subscribers with the `payroll` module key. All data access uses existing tables in the `[payroll]` schema.

## Glossary

- **Payroll_System**: The payroll module within the Portal platform responsible for employee management, payslip calculation, payslip generation, reporting, PDF generation, and email delivery
- **PDF_Service**: The implementation of IPayslipPdfService that generates branded A4 PDF documents from payslip data using HTML-to-PDF rendering
- **Email_Service**: The implementation of IPayslipEmailService that sends payslip PDF attachments to employee email addresses via the platform email infrastructure
- **Payslip_Template**: A branded A4 HTML layout matching the Cyprus payslip reference structure, used as the source template for PDF rendering
- **Employee_History_View**: A filterable list of all payslips belonging to a single employee, ordered by period
- **Annual_Summary**: An aggregated report showing total gross earnings, total employee deductions, total net salary, and total employer contributions for a single employee across a calendar year
- **Earnings_Breakdown_Report**: A report grouping payslip earning lines by EarningType (overtime, bonuses, paid holidays) with filtering by period and employee
- **Period_Summary**: A business-level report showing all employees' payslip totals (gross, deductions, net, employer cost) for a single payslip period
- **Employee_Statement**: A PDF document containing all payslips for a single employee within a user-selected date range
- **PayslipEmailLog**: A record tracking each email send event including sender, recipient, timestamp, and delivery status
- **Owner**: A business owner user with permissions to manage all aspects of their business within the Portal
- **SuperAdmin**: A platform-level administrator with elevated permissions across all businesses

## Requirements

### Requirement 1: Payslip PDF Template

**User Story:** As a business owner, I want a professional, branded A4 payslip layout that matches the Cyprus payslip reference structure, so that generated PDFs are print-ready and recognisable.

#### Acceptance Criteria

1. THE Payslip_Template SHALL use A4 page dimensions (210mm x 297mm) with print-ready margins
2. THE Payslip_Template SHALL include a header section displaying: business name, business address, and business logo (when configured)
3. THE Payslip_Template SHALL include an employee details section displaying: Employee Name, Position, Social Insurance Number, and ID Number
4. THE Payslip_Template SHALL include a period section displaying: Month and Year of the payslip period
5. THE Payslip_Template SHALL include an Earnings section listing all PayslipEarningLine records with columns: Description, Hours (for overtime), Multiplier (for overtime), and Amount
6. THE Payslip_Template SHALL include an Employee Deductions section listing all employee-portion PayslipDeductionLine records with columns: Deduction Name, Rate (percentage), and Calculated Amount
7. THE Payslip_Template SHALL include an Employer Contributions section listing all employer-portion PayslipDeductionLine records with columns: Contribution Name, Rate (percentage), and Calculated Amount
8. THE Payslip_Template SHALL include a Summary section displaying: Total Earnings, Total Employee Deductions, Net Salary, Total Employer Contributions, and Total Cost to Business
9. WHEN a Payslip has ManagerNotes, THE Payslip_Template SHALL include a Notes section displaying the ManagerNotes content
10. THE Payslip_Template SHALL apply the Portal brand colours and typography (Manrope headings, Inter body text) consistently

### Requirement 2: PDF Generation Service

**User Story:** As a business owner, I want to generate a downloadable PDF from any finalised payslip, so that I can provide employees with professional documentation of their pay.

#### Acceptance Criteria

1. THE PDF_Service SHALL implement the existing IPayslipPdfService interface defined in Phase A, replacing the stub implementation
2. THE PDF_Service SHALL render the Payslip_Template to a PDF document using HTML-to-PDF conversion
3. WHEN a PDF generation is requested for a Payslip, THE PDF_Service SHALL populate the template with the payslip's earning lines, deduction lines, employee details, and period information
4. THE PDF_Service SHALL produce a valid PDF file with a filename format of "{EmployeeName}_Payslip_{MonthName}_{Year}.pdf"
5. THE Payroll_System SHALL provide a "Download PDF" action on the individual payslip detail view for payslips in Finalised or Re-finalised status
6. THE Payroll_System SHALL provide a "Download All" action on the PayslipPeriod view that generates a ZIP archive containing PDF payslips for all employees in the period
7. IF PDF generation fails for a specific payslip, THEN THE Payroll_System SHALL display an error message identifying the affected employee and the failure reason
8. THE PDF_Service SHALL generate each PDF within 5 seconds for a single payslip under normal load

### Requirement 3: Employee Payslip History View

**User Story:** As a business owner, I want to view all payslips for a specific employee in one place, filterable by year, so that I can quickly access historical pay records.

#### Acceptance Criteria

1. THE Payroll_System SHALL provide an Employee_History_View accessible from the employee detail page
2. THE Employee_History_View SHALL display all payslips belonging to the selected employee, ordered by period (newest first)
3. THE Employee_History_View SHALL provide a year filter dropdown allowing the user to select a specific calendar year
4. WHEN a year filter is applied, THE Employee_History_View SHALL display only payslips with a PayslipPeriod matching the selected year
5. EACH payslip entry in the history view SHALL display: Period (Month/Year), Total Earnings, Net Salary, Status, and action links (View, Download PDF)
6. THE Employee_History_View SHALL display a summary row at the bottom showing: total gross, total net, and number of payslips for the filtered period
7. WHEN no payslips exist for the selected year, THE Employee_History_View SHALL display an empty state message: "No payslips found for {Year}"

### Requirement 4: Annual Summary Per Employee

**User Story:** As a business owner, I want an annual aggregated view of an employee's payroll totals, so that I can prepare tax return documentation efficiently.

#### Acceptance Criteria

1. THE Payroll_System SHALL provide an Annual_Summary view accessible from the employee detail page and from the Employee_History_View
2. THE Annual_Summary SHALL aggregate the following values across all finalised (or re-finalised) payslips for the employee in the selected year: Total Gross Earnings, Total Employee Deductions, Total Net Salary, and Total Employer Contributions
3. THE Annual_Summary SHALL display a breakdown of deductions by DeductionType showing: Deduction Name, Total Amount deducted for the year, and the number of months the deduction was applied
4. THE Annual_Summary SHALL display a breakdown of employer contributions by DeductionType showing: Contribution Name, Total Amount contributed for the year, and the number of months the contribution was applied
5. THE Annual_Summary SHALL display a breakdown of earnings by EarningType showing: Earning Type Name and Total Amount earned for the year
6. THE Payroll_System SHALL provide a year selector allowing the user to switch between available years (years where at least one payslip exists)
7. THE Annual_Summary SHALL include a "Download PDF" action that generates a formatted PDF of the annual summary suitable for tax filing attachments
8. THE Annual_Summary SHALL exclude payslips with Draft or Preview status from all aggregated calculations

### Requirement 5: Earnings Breakdown Report

**User Story:** As a business owner, I want to see a breakdown of extra payments by type (overtime, bonuses, paid holidays) across employees and periods, so that I can analyse compensation patterns and costs.

#### Acceptance Criteria

1. THE Payroll_System SHALL provide an Earnings_Breakdown_Report view accessible from the payroll reports section
2. THE Earnings_Breakdown_Report SHALL group PayslipEarningLine records by EarningType and display the total amount per type
3. THE Earnings_Breakdown_Report SHALL provide a period filter allowing selection of a date range (from month/year to month/year)
4. THE Earnings_Breakdown_Report SHALL provide an employee filter allowing selection of a specific employee or all employees
5. THE Earnings_Breakdown_Report SHALL provide an earning type filter allowing selection of one or more earning types (Overtime, Bonus, Paid Holidays, Part-time, Basic)
6. WHEN filters are applied, THE Earnings_Breakdown_Report SHALL recalculate totals based on the filtered dataset
7. THE Earnings_Breakdown_Report SHALL display a detail table showing: Employee Name, Period, Earning Type, Description, and Amount for each matching earning line
8. THE Earnings_Breakdown_Report SHALL display summary totals per earning type at the top of the view
9. THE Earnings_Breakdown_Report SHALL include only earning lines from payslips in Finalised or Re-finalised status
10. THE Earnings_Breakdown_Report SHALL provide an "Export to Excel" action that downloads the filtered data as an XLSX file

### Requirement 6: Business-Level Period Summary

**User Story:** As a business owner, I want to see all employees' payroll totals in one consolidated view per period, so that I can understand the total payroll cost for each month.

#### Acceptance Criteria

1. THE Payroll_System SHALL provide a Period_Summary view accessible from the payroll reports section
2. THE Period_Summary SHALL display one row per employee with columns: Employee Name, Department, Total Earnings, Total Employee Deductions, Net Salary, Total Employer Contributions, and Total Cost to Business
3. THE Period_Summary SHALL display a period selector allowing the user to choose a specific PayslipPeriod (Year/Month)
4. THE Period_Summary SHALL display a footer row with aggregate totals across all employees: Total Gross, Total Deductions, Total Net, Total Employer Contributions, and Total Cost to Business
5. THE Period_Summary SHALL include only payslips in Finalised or Re-finalised status for the selected period
6. WHEN no finalised payslips exist for the selected period, THE Period_Summary SHALL display an empty state message: "No finalised payslips for {Month Name} {Year}"
7. THE Period_Summary SHALL provide a "Download PDF" action that generates an A4 PDF of the period summary table
8. THE Period_Summary SHALL provide an "Export to Excel" action that downloads the period summary data as an XLSX file
9. THE Period_Summary SHALL allow optional filtering by Department when departments are configured

### Requirement 7: Send Payslip by Email

**User Story:** As a business owner, I want to send individual or batch payslips by email with the PDF attached, so that employees receive their payslips electronically.

#### Acceptance Criteria

1. THE Email_Service SHALL implement the existing IPayslipEmailService interface defined in Phase A, replacing the stub implementation
2. THE Payroll_System SHALL provide a "Send by Email" action on the individual payslip detail view for payslips in Finalised or Re-finalised status
3. WHEN the "Send by Email" action is initiated, THE Email_Service SHALL generate the payslip PDF and send it as an attachment to the employee's configured email address
4. WHEN an employee has no email address configured, THE Payroll_System SHALL disable the "Send by Email" action and display a tooltip: "Employee email address not configured"
5. THE Payroll_System SHALL provide a "Send All by Email" action on the PayslipPeriod view that sends payslip PDFs to all employees in the period who have valid email addresses
6. WHEN batch email sending is initiated, THE Payroll_System SHALL display a confirmation dialog showing: the number of employees who will receive emails, and the number of employees skipped due to missing email addresses
7. THE email subject line SHALL follow the format: "Your Payslip - {Month Name} {Year}" (e.g., "Your Payslip - July 2027")
8. THE email body SHALL include a brief message: "Please find attached your payslip for {Month Name} {Year}. If you have any questions, please contact your manager."
9. THE Payroll_System SHALL create a PayslipEmailLog record for each email send attempt, storing: PayslipId, SentByUserId, SentToEmail, SentAtUtc, and IsSuccess
10. IF email sending fails for a specific employee, THEN THE Payroll_System SHALL log the failure in PayslipEmailLog with IsSuccess = false and continue sending to remaining employees without interrupting the batch
11. THE Payroll_System SHALL display a summary after batch sending showing: emails sent successfully, emails failed, and employees skipped (no email)
12. THE Payroll_System SHALL prevent duplicate email sending for the same payslip by displaying a warning: "This payslip was already emailed on {date}. Send again?" with confirmation required

### Requirement 8: Employee Statement Export

**User Story:** As a business owner, I want to generate a PDF statement showing all payslips for an employee within a selected date range, so that I can provide comprehensive pay documentation for loan applications, audits, or employee requests.

#### Acceptance Criteria

1. THE Payroll_System SHALL provide an "Export Statement" action on the employee detail page and from the Employee_History_View
2. WHEN the "Export Statement" action is initiated, THE Payroll_System SHALL display a date range selector allowing the user to choose a start month/year and end month/year
3. THE Employee_Statement PDF SHALL include a cover section with: Employee Name, Position, Social Insurance Number, ID Number, and the statement period (from date to date)
4. THE Employee_Statement PDF SHALL include a summary section showing: Total Gross Earnings, Total Employee Deductions, Total Net Salary, and Total Employer Contributions across the selected range
5. THE Employee_Statement PDF SHALL include individual payslip details for each period within the range, showing: Period (Month/Year), earning lines, deduction lines, and net salary per payslip
6. THE Employee_Statement PDF SHALL include only payslips in Finalised or Re-finalised status within the selected date range
7. WHEN no finalised payslips exist within the selected date range, THE Payroll_System SHALL display a validation message: "No finalised payslips found between {Start Period} and {End Period}"
8. THE Employee_Statement PDF SHALL use the same branded template styling as the individual payslip PDF (business logo, brand colours, A4 layout)
9. THE PDF_Service SHALL generate the employee statement with a filename format of "{EmployeeName}_Statement_{StartMonth}{StartYear}_to_{EndMonth}{EndYear}.pdf"

### Requirement 9: Mobile Responsive Design

**User Story:** As a business owner accessing the system from a mobile device, I want all payroll report views to be usable on small screens, so that I can review payroll information on the go.

#### Acceptance Criteria

1. THE Payroll_System SHALL render all Phase C report views (Employee_History_View, Annual_Summary, Earnings_Breakdown_Report, Period_Summary) responsively at viewport widths of 375px and 810px
2. WHEN the viewport width is below 810px, THE Payroll_System SHALL convert wide data tables into card-based layouts or horizontally scrollable containers
3. WHEN the viewport width is below 810px, THE Payroll_System SHALL stack filter controls vertically instead of inline
4. THE Payroll_System SHALL ensure all action buttons (Download PDF, Send Email, Export) remain accessible and tappable (minimum touch target 44px x 44px) on mobile viewports
5. THE Payroll_System SHALL maintain readable font sizes (minimum 14px body text) on mobile viewports without requiring pinch-to-zoom
6. THE Payroll_System SHALL ensure pagination controls are usable on mobile with adequate spacing between page number buttons

### Requirement 10: Email Audit and Logging

**User Story:** As a business owner, I want a record of all payslip emails sent, so that I can verify delivery and track communication history.

#### Acceptance Criteria

1. THE Payroll_System SHALL store PayslipEmailLog records in the `[payroll]` schema with columns: Id (INT PK IDENTITY), PayslipId (INT FK), SentByUserId (NVARCHAR(450) FK), SentToEmail (NVARCHAR(256) NOT NULL), SentAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE()), IsSuccess (BIT NOT NULL), FailureReason (NVARCHAR(500) nullable), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE())
2. EACH PayslipEmailLog entry SHALL reference the specific Payslip via PayslipId foreign key
3. THE Payroll_System SHALL display email send history on the individual payslip detail view showing: date sent, sent by (user name), sent to (email address), and status (success/failed)
4. WHEN an email send fails, THE PayslipEmailLog SHALL record the FailureReason with a descriptive error message
5. THE Payroll_System SHALL provide a period-level email status summary showing: total emails sent, total successful, total failed for the selected PayslipPeriod
6. THE PayslipEmailLog table SHALL enforce referential integrity: PayslipEmailLog.PayslipId → Payslip.Id

### Requirement 11: Permission and Access Control

**User Story:** As a platform operator, I want all Phase C reporting and export features restricted to authorised users with the payroll module key, so that only Enterprise-tier subscribers can access payroll reports.

#### Acceptance Criteria

1. THE Payroll_System SHALL enforce the `payroll` module key (Enterprise plan) for all Phase C controllers and actions
2. THE Payroll_System SHALL apply the standard [Authorize] attribute on all Phase C controller actions
3. THE Payroll_System SHALL restrict "Send by Email" and "Send All by Email" actions to users with Owner or SuperAdmin roles
4. THE Payroll_System SHALL allow standard payroll users (non-Owner, non-SuperAdmin) to view reports and download PDFs but not send emails
5. WHEN a user without Owner or SuperAdmin role attempts to send payslip emails, THE Payroll_System SHALL deny the action and display an authorisation error
6. THE Payroll_System SHALL hide email-sending buttons from the UI for users without the required role

### Requirement 12: Annual Summary PDF Generation

**User Story:** As a business owner, I want to generate a PDF of the annual summary for an employee, so that I can provide it to accountants or attach it to tax filing documentation.

#### Acceptance Criteria

1. THE PDF_Service SHALL generate an A4 branded PDF for the Annual_Summary containing all aggregated data (gross, deductions by type, contributions by type, net totals)
2. THE Annual_Summary PDF SHALL include a header section with: Business Name, Employee Name, Employee SIN, and the calendar year
3. THE Annual_Summary PDF SHALL include a monthly breakdown table showing: Month, Gross Earnings, Employee Deductions, Net Salary, and Employer Contributions for each month with a finalised payslip
4. THE Annual_Summary PDF SHALL include a totals row summarising all monthly values
5. THE Annual_Summary PDF SHALL include a deductions breakdown section grouped by DeductionType with annual totals
6. THE Annual_Summary PDF SHALL include an employer contributions breakdown section grouped by DeductionType with annual totals
7. THE PDF_Service SHALL generate the annual summary PDF with a filename format of "{EmployeeName}_AnnualSummary_{Year}.pdf"

