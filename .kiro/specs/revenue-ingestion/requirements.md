# Requirements Document

## Introduction

This document defines Phase 1 requirements for the Revenue Ingestion feature — enabling businesses that use external POS systems to manually record Z-Report summaries in the Portal for VAT reporting and revenue consolidation. Many businesses (hospitality, retail, services with tills) generate hundreds of daily transactions through their own POS and need Portal to receive consolidated revenue data for VAT compliance and financial intelligence.

Phase 1 delivers: a feature toggle on MyBusiness settings, Revenue Source configuration (POS devices/registers), Z-Report manual entry with multiple VAT rate lines, VAT period assignment, document attachments for audit evidence, VAT report integration (Output VAT contribution + new Z-Reports section), and Revenue Dashboard aggregation.

Phase 2+ (excluded from this document): Z-Report Bulk Import, Sales Invoice Import, Revenue Ingestion API, Customer Behaviour Analytics, and ExternalSalesRecord table.

## Glossary

- **Portal**: The ASP.NET Core MVC 8 web application providing multi-tenant back-office operations
- **Business**: A registered organization on the Portal with users, subscriptions, and data
- **Business_Profile**: The settings record for a Business containing operational preferences and feature toggles
- **Revenue_Source**: A configurable entity representing a POS device, register, or sales channel belonging to a Business (e.g., "Main POS", "Bar Register")
- **Revenue_Summary**: A Z-Report header record representing a consolidated sales summary from a POS device for a given date or period, with computed totals
- **Revenue_Summary_Line**: A child record of Revenue_Summary representing the breakdown at a specific VAT rate (net, VAT, total, discount amounts)
- **Z_Report**: An official end-of-day (or end-of-period) sales summary printed from a POS system, containing net sales, VAT totals, and discount information grouped by VAT rate
- **VAT_Submission_Period**: A defined time period (bi-monthly) for which VAT returns are prepared and submitted
- **Output_VAT**: The total VAT collected on sales that a business must report to tax authorities — contributed by Portal invoices and Revenue Summaries
- **Feature_Toggle**: The IsZReportEnabled column on Business_Profile that controls visibility of all Z-Report related functionality
- **Document_Attachment**: A file (PDF/image) attached to a Revenue_Summary via the existing Document Attachments feature using EntityType = "RevenueSummary"

## Requirements

### Requirement 1: Z-Report Feature Toggle

**User Story:** As a business owner, I want to enable or disable Z-Report functionality from my business settings, so that the feature is only visible when my business uses POS systems.

#### Acceptance Criteria

1. THE Business_Profile table SHALL include an IsZReportEnabled column of type BIT NOT NULL with a default value of 0 (disabled).
2. THE MyBusiness settings page SHALL display a toggle control for "Z-Report / External Revenue" within the business settings section.
3. WHEN the user enables the toggle, THE Portal SHALL set IsZReportEnabled to 1 for the current Business_Profile.
4. WHEN the user disables the toggle, THE Portal SHALL set IsZReportEnabled to 0 for the current Business_Profile.
5. WHILE IsZReportEnabled is false, THE Portal SHALL hide the "Z-Reports" navigation item from the Revenue menu in the sidebar.
6. WHILE IsZReportEnabled is false, THE Portal SHALL exclude Z-Report sections from the VAT Period Report and VAT Detail pages.
7. WHILE IsZReportEnabled is false, THE Revenue Dashboard SHALL aggregate only Portal-issued invoices.
8. WHILE IsZReportEnabled is true, THE Portal SHALL display the "Z-Reports" navigation item under Revenue in the sidebar.
9. WHILE IsZReportEnabled is true AND no Revenue Sources exist, THE Portal SHALL prompt the user to create their first Revenue Source when navigating to the Z-Reports page.

### Requirement 2: Revenue Source Data Model

**User Story:** As a system architect, I want Revenue Source metadata stored in a dedicated database table, so that each POS device or register is identifiable and selectable when recording Z-Reports.

#### Acceptance Criteria

1. THE Portal database SHALL contain a RevenueSource table in the `[dbo]` schema with columns: Id (INT IDENTITY NOT NULL), BusinessId (INT NOT NULL), Name (NVARCHAR(200) NOT NULL), Description (NVARCHAR(500) NULL), IsActive (BIT NOT NULL DEFAULT 1), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE()).
2. THE RevenueSource table SHALL have a primary key constraint on Id.
3. THE RevenueSource table SHALL have a foreign key from BusinessId to the Business table.
4. ALL queries against the RevenueSource table SHALL filter by the authenticated user's BusinessId to enforce tenant isolation.

### Requirement 3: Revenue Source CRUD Operations

**User Story:** As a business user, I want to create, view, edit, and deactivate Revenue Sources, so that I can manage my POS devices and registers within the platform.

#### Acceptance Criteria

1. THE Revenue Source management page SHALL display a list of all Revenue Sources for the current Business, showing Name, Description, Status (Active/Inactive), and Created date.
2. WHEN the user submits the Create form with a valid Name, THE Portal SHALL insert a new RevenueSource record with the provided Name and Description, scoped to the current BusinessId.
3. WHEN the user submits the Edit form with updated fields, THE Portal SHALL update the Name and Description on the existing RevenueSource record.
4. WHEN the user deactivates a Revenue Source, THE Portal SHALL set IsActive to 0 on the RevenueSource record.
5. WHEN the user reactivates a Revenue Source, THE Portal SHALL set IsActive to 1 on the RevenueSource record.
6. THE Revenue Source dropdown on the Z-Report entry form SHALL display only active Revenue Sources (IsActive = 1) for the current Business.
7. IF the user attempts to deactivate a Revenue Source that has associated Revenue Summaries, THEN THE Portal SHALL allow deactivation but display an advisory message that existing Z-Reports linked to this source will retain their association.
8. THE Create and Edit forms SHALL validate that Name is not empty and does not exceed 200 characters.

### Requirement 4: Revenue Summary Data Model

**User Story:** As a system architect, I want Revenue Summary (Z-Report header) data stored in a structured table, so that the system can track consolidated POS revenue per period with full audit trail.

#### Acceptance Criteria

1. THE Portal database SHALL contain a RevenueSummary table in the `[dbo]` schema with columns: Id (INT IDENTITY NOT NULL), BusinessId (INT NOT NULL), RevenueSourceId (INT NOT NULL), SummaryDate (DATE NOT NULL), PeriodEndDate (DATE NULL), ZReportNumber (NVARCHAR(50) NULL), TotalNet (DECIMAL(18,2) NOT NULL), TotalVat (DECIMAL(18,2) NOT NULL), TotalGross (DECIMAL(18,2) NOT NULL), TotalDiscount (DECIMAL(18,2) NULL), TransactionCount (INT NULL), Reference (NVARCHAR(200) NULL), Notes (NVARCHAR(MAX) NULL), ExportedAtUtc (DATETIME NULL), VatSubmissionPeriodId (INT NULL), ImportSessionId (INT NULL), IsActive (BIT NOT NULL DEFAULT 1), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE()).
2. THE RevenueSummary table SHALL have a primary key constraint on Id.
3. THE RevenueSummary table SHALL have foreign keys: BusinessId to Business, RevenueSourceId to RevenueSource, VatSubmissionPeriodId to VatSubmissionPeriod.
4. THE ImportSessionId column SHALL be NULL for manually entered Revenue Summaries (used only for Phase 2 bulk import).
5. ALL queries against the RevenueSummary table SHALL filter by the authenticated user's BusinessId to enforce tenant isolation.

### Requirement 5: Revenue Summary Line Data Model

**User Story:** As a system architect, I want VAT rate breakdowns stored as child records of each Revenue Summary, so that the system supports multiple VAT rates per Z-Report for accurate tax reporting.

#### Acceptance Criteria

1. THE Portal database SHALL contain a RevenueSummaryLine table in the `[dbo]` schema with columns: Id (INT IDENTITY NOT NULL), RevenueSummaryId (INT NOT NULL), VatRate (DECIMAL(5,2) NOT NULL), NetAmount (DECIMAL(18,2) NOT NULL), VatAmount (DECIMAL(18,2) NOT NULL), TotalAmount (DECIMAL(18,2) NOT NULL), DiscountAmount (DECIMAL(18,2) NULL), Description (NVARCHAR(200) NULL), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE()).
2. THE RevenueSummaryLine table SHALL have a primary key constraint on Id.
3. THE RevenueSummaryLine table SHALL have a foreign key from RevenueSummaryId to RevenueSummary with cascading enforcement.
4. EACH RevenueSummary SHALL have one or more RevenueSummaryLine records — the system SHALL require at least one line to save a Revenue Summary.

### Requirement 6: Z-Report Manual Entry — Create

**User Story:** As a business user, I want to manually enter a Z-Report from my POS system, so that my external revenue is recorded in the Portal for VAT reporting and financial consolidation.

#### Acceptance Criteria

1. THE Z-Report Create form SHALL include fields for: Revenue Source (required dropdown), Summary Date (required date picker), Period End Date (optional date picker), Z-Report Number (optional text), Transaction Count (optional integer), Reference (optional text), Notes (optional text area), and Exported At (optional datetime).
2. THE Z-Report Create form SHALL include a dynamic VAT Lines section where the user adds one or more lines with: VAT Rate (required percentage), Net Amount (required decimal), VAT Amount (required decimal), Discount Amount (optional decimal), and Description (optional text).
3. WHEN the user adds or modifies VAT Lines, THE form SHALL auto-compute TotalAmount per line as NetAmount + VatAmount.
4. WHEN the user adds or modifies VAT Lines, THE form SHALL auto-compute header totals: TotalNet (sum of all line NetAmounts), TotalVat (sum of all line VatAmounts), TotalGross (sum of all line TotalAmounts), TotalDiscount (sum of all line DiscountAmounts).
5. WHEN the user submits the form with valid data, THE Portal SHALL insert one RevenueSummary record and one or more RevenueSummaryLine records in a single database transaction.
6. IF the form submission fails validation, THEN THE Portal SHALL display specific error messages without losing the entered data.
7. THE form SHALL validate that at least one VAT Line exists before submission.
8. THE form SHALL validate that all monetary amounts are non-negative.
9. THE ImportSessionId SHALL be set to NULL for manually created Revenue Summaries.

### Requirement 7: Z-Report Manual Entry — Edit

**User Story:** As a business user, I want to edit a previously entered Z-Report, so that I can correct errors or add missing information.

#### Acceptance Criteria

1. THE Z-Report Edit form SHALL pre-populate all fields from the existing RevenueSummary and its RevenueSummaryLine records.
2. THE user SHALL be able to add new VAT Lines, modify existing VAT Lines, and remove VAT Lines.
3. WHEN the user saves changes, THE Portal SHALL update the RevenueSummary record, delete removed lines, update modified lines, and insert new lines in a single database transaction.
4. THE header totals (TotalNet, TotalVat, TotalGross, TotalDiscount) SHALL be recomputed from the updated lines on save.
5. IF the Revenue Summary is assigned to a submitted VAT period, THEN THE Portal SHALL prevent editing and display a message: "Locked — assigned to a submitted VAT period."
6. THE form SHALL enforce the same validation rules as the Create form (at least one line, non-negative amounts).

### Requirement 8: Z-Report Manual Entry — View and List

**User Story:** As a business user, I want to view a list of all my Z-Reports and see the details of each entry, so that I can review and manage my recorded external revenue.

#### Acceptance Criteria

1. THE Z-Reports list page SHALL display all Revenue Summaries for the current Business where IsActive = 1, ordered by SummaryDate descending.
2. THE list SHALL display columns: Summary Date, Period End Date, Revenue Source Name, Z-Report Number, Total Net, Total VAT, Total Gross, VAT Period (assigned period label or "Unassigned").
3. THE list SHALL support filtering by: Revenue Source, VAT Period, and date range (Summary Date from/to).
4. THE list SHALL support pagination with standard page sizes.
5. WHEN the user clicks a Z-Report row, THE Portal SHALL navigate to a detail/edit view showing the full Revenue Summary with all VAT Lines.
6. THE list page SHALL include a "New Z-Report" button to navigate to the Create form.

### Requirement 9: Z-Report Soft Delete

**User Story:** As a business user, I want to delete a Z-Report that was entered in error, so that incorrect data does not affect my VAT calculations.

#### Acceptance Criteria

1. WHEN the user requests deletion of a Revenue Summary, THE Portal SHALL display a SweetAlert2 confirmation dialog with the message: "Are you sure you want to delete this Z-Report? This will remove it from VAT calculations."
2. WHEN the user confirms deletion, THE Portal SHALL set IsActive to 0 on the RevenueSummary record (soft delete).
3. IF the Revenue Summary is assigned to a submitted VAT period, THEN THE Portal SHALL prevent deletion and display a message: "Cannot delete — assigned to a submitted VAT period."
4. AFTER soft deletion, THE Revenue Summary SHALL NOT appear in the Z-Reports list, SHALL NOT contribute to Output VAT calculations, and SHALL NOT appear in Revenue Dashboard aggregations.

### Requirement 10: VAT Period Assignment for Z-Reports

**User Story:** As a business user, I want to assign Z-Reports to a VAT submission period, so that my external revenue VAT is included in the correct filing period.

#### Acceptance Criteria

1. THE Z-Report Create and Edit forms SHALL include an optional "VAT Period" dropdown field.
2. THE dropdown SHALL display all VAT periods for the business that are NOT yet submitted, ordered by most recent first, plus a "— Not assigned —" default option.
3. WHEN the user selects a period, THE RevenueSummary SHALL be saved with VatSubmissionPeriodId set to the selected period's Id.
4. WHEN the user leaves the dropdown on "— Not assigned —", THE RevenueSummary SHALL be saved with VatSubmissionPeriodId = NULL.
5. IF the user does not select a period explicitly, THE Portal SHALL attempt date-range fallback: assign to the period whose date range (PeriodStartDate to PeriodEndDate) contains the RevenueSummary.SummaryDate, provided that period is not submitted.
6. IF no unsubmitted period covers the SummaryDate, THE VatSubmissionPeriodId SHALL remain NULL.
7. WHEN a Revenue Summary is assigned to a submitted period, THE VAT Period dropdown SHALL be disabled with the message: "Locked — assigned to a submitted period."
8. THE Z-Reports list page SHALL display the assigned VAT Period label (or "Unassigned") per row.

### Requirement 11: Document Attachment for Z-Reports

**User Story:** As a business user, I want to attach the original Z-Report printout (PDF or image) to my Z-Report entry, so that I have audit evidence for VAT compliance.

#### Acceptance Criteria

1. THE Z-Report detail/edit page SHALL include the existing Document Attachment panel (reusable partial view).
2. THE attachment panel SHALL use EntityType = "RevenueSummary" and EntityId = the Revenue Summary's Id.
3. THE attachment panel SHALL support the same file types and size limits as the existing Document Attachments feature (PDF, PNG, JPG, JPEG, WEBP; max 5 MB; max 5 files per entity).
4. WHEN a file is uploaded against a Revenue Summary, THE DocumentAttachment record SHALL be created with EntityType = "RevenueSummary" and the correct EntityId and BusinessId.
5. THE attachment panel SHALL only be available after the Revenue Summary has been saved (not on the initial Create form before first save).

### Requirement 12: VAT Integration — Output VAT Contribution

**User Story:** As a business user, I want my Z-Report VAT totals included in the Output VAT calculation for each period, so that my VAT returns accurately reflect all revenue sources.

#### Acceptance Criteria

1. WHEN computing Output VAT for a VAT period, THE Portal SHALL include the sum of RevenueSummary.TotalVat for all active Revenue Summaries (IsActive = 1) assigned to that period (VatSubmissionPeriodId = period's Id).
2. THE Output VAT formula SHALL be: SUM(Invoice.TaxAmount for issued invoices) + SUM(RevenueSummary.TotalVat for active assigned summaries) - SUM(CreditNote.TaxAmount for issued/applied credit notes).
3. WHILE IsZReportEnabled is false for a Business, THE Output VAT calculation SHALL NOT include Revenue Summary contributions (maintains backward compatibility).

### Requirement 13: VAT Integration — Period Report Section

**User Story:** As a business user, I want to see a dedicated "Z-Reports" section in the VAT Period Report, so that I can verify which external revenue is included in each filing.

#### Acceptance Criteria

1. WHILE IsZReportEnabled is true, THE VAT Period Report page SHALL display a "External Revenue (Z-Reports)" section between the "Sales Invoices" section and the "Purchases" section.
2. THE section SHALL display a table with columns: Revenue Source Name, Z-Report Number, Period (SummaryDate — PeriodEndDate), Net Amount, VAT Amount, Total, Discount.
3. EACH row SHALL represent one RevenueSummary record assigned to the current period with IsActive = 1.
4. THE section SHALL display a Period Total row summing Net Amount, VAT Amount, Total, and Discount across all Z-Reports in the period.
5. WHILE IsZReportEnabled is false, THE "External Revenue (Z-Reports)" section SHALL NOT be rendered on the page.
6. WHEN no Revenue Summaries are assigned to the current period, THE section SHALL display: "No Z-Reports assigned to this period."

### Requirement 14: VAT Integration — VAT Detail Page Section

**User Story:** As a business user, I want Z-Report entries visible on the VAT Detail page for each period, so that I can review all revenue sources contributing to my filing.

#### Acceptance Criteria

1. WHILE IsZReportEnabled is true, THE VAT Detail page SHALL display a "External Revenue (Z-Reports)" section between the "Sales Invoices" section and the "Purchases" section.
2. THE section SHALL display each RevenueSummary record assigned to the current period (IsActive = 1) with: Revenue Source Name, Z-Report Number, Period dates, Total VAT, and Assignment status (Explicit or Date Range).
3. THE section SHALL display the total VAT contributed by Z-Reports as a subtotal.
4. WHILE IsZReportEnabled is false, THE "External Revenue (Z-Reports)" section SHALL NOT be rendered on the VAT Detail page.

### Requirement 15: Revenue Dashboard Integration

**User Story:** As a business user, I want the Revenue Dashboard KPIs to include both Portal invoices and Z-Report revenue, so that I have a complete view of my business revenue.

#### Acceptance Criteria

1. WHILE IsZReportEnabled is true, THE Revenue Dashboard SHALL aggregate revenue from Portal-issued invoices AND active Revenue Summaries (IsActive = 1) when computing total revenue KPIs.
2. THE revenue aggregation SHALL include RevenueSummary.TotalGross for Revenue Summaries within the selected dashboard date range (based on SummaryDate).
3. THE Revenue Dashboard SHALL clearly indicate when revenue includes external sources (e.g., a label or tooltip: "Includes POS revenue").
4. WHILE IsZReportEnabled is false, THE Revenue Dashboard SHALL aggregate only Portal-issued invoices (no change to existing behaviour).

### Requirement 16: Tenant Isolation

**User Story:** As a business user, I want all Revenue Ingestion data scoped to my business, so that my data remains private and secure.

#### Acceptance Criteria

1. ALL queries against RevenueSource, RevenueSummary, and RevenueSummaryLine tables SHALL filter by the authenticated user's BusinessId.
2. THE Revenue Source CRUD operations SHALL validate that the RevenueSourceId belongs to the current Business before any read, update, or deactivate operation.
3. THE Z-Report Create endpoint SHALL set BusinessId from the authenticated user's session — the client SHALL NOT provide BusinessId.
4. THE Z-Report Edit and Delete endpoints SHALL verify the RevenueSummary.BusinessId matches the authenticated user's BusinessId before processing.
5. THE VAT Period dropdown SHALL only show periods belonging to the current Business.
6. THE Revenue Source dropdown SHALL only show sources belonging to the current Business.

### Requirement 17: Navigation — Z-Reports Menu Item

**User Story:** As a business user, I want to access Z-Reports from the sidebar navigation under Revenue, so that I can quickly find and manage my external revenue entries.

#### Acceptance Criteria

1. WHILE IsZReportEnabled is true, THE sidebar navigation SHALL display a "Z-Reports" sub-item under the "Revenue" section.
2. WHILE IsZReportEnabled is false, THE sidebar navigation SHALL NOT display the "Z-Reports" sub-item.
3. THE "Z-Reports" navigation item SHALL link to the Z-Reports list page.
4. THE "Z-Reports" navigation item SHALL be positioned after existing Revenue sub-items (e.g., after Invoices/Payments).
5. THE Revenue Source management page SHALL be accessible from the Z-Reports page (e.g., a "Manage Sources" link or settings icon) rather than as a separate sidebar item.

### Requirement 18: Auto-Computation of Totals

**User Story:** As a business user, I want Z-Report totals computed automatically from the VAT lines I enter, so that I don't have to calculate sums manually and risk arithmetic errors.

#### Acceptance Criteria

1. WHEN the user adds, modifies, or removes VAT Lines in the form, THE Portal SHALL immediately recompute and display: TotalNet (sum of all line NetAmounts), TotalVat (sum of all line VatAmounts), TotalGross (sum of all line TotalAmounts), TotalDiscount (sum of all line DiscountAmounts where not null).
2. THE auto-computation SHALL occur client-side in real-time as values are entered (no server round-trip required for preview).
3. WHEN saving the Revenue Summary, THE Portal SHALL recompute totals server-side from the submitted lines to prevent client-side manipulation.
4. THE server-computed totals SHALL be the values stored in the RevenueSummary record — client-displayed totals are for preview only.

### Requirement 19: Subscription Tier Gating

**User Story:** As a platform operator, I want Revenue Source and Z-Report Manual Entry available to all subscription tiers (Foundation and above), so that all paying businesses can record their POS revenue.

#### Acceptance Criteria

1. THE Revenue Source CRUD and Z-Report Manual Entry features SHALL be available to all active subscription tiers (Foundation, Professional, Enterprise).
2. THE IsZReportEnabled toggle on MyBusiness settings SHALL be available to all active subscription tiers.
3. IF a business does not have an active subscription, THEN THE Z-Report functionality SHALL NOT be accessible regardless of the IsZReportEnabled setting.
