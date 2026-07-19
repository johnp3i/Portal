# Revenue Ingestion — Feature Brief

**Date:** 17 July 2026
**Status:** Concept — Approved for development
**Module:** Revenue / External Sales
**Schema:** [dbo]

## Problem Statement

The Portal currently assumes businesses issue invoices from the platform. However, many businesses generate hundreds of daily transactions through their own POS system. They need Portal to receive and consolidate their external sales data for VAT reporting, revenue tracking, and financial intelligence.

Industries affected: Hospitality, Retail, Services with POS, any business using a till system.

Immediate demand: Two potential cafe clients use 3 Inventors POS software and need VAT-compliant revenue recording.

## Solution: Three Mechanisms

1. Z-Report Manual Entry (Foundation tier) - Single Z-report entry via form
2. Z-Report Bulk Import (Professional tier) - Upload CSV/Excel of multiple Z-reports
3. Sales Invoice Import (Professional tier) - Transaction-level POS data with optional CustomerId

## Mechanism A: Z-Report Manual Entry

Supports multiple VAT rates per entry. User selects Revenue Source, enters period dates, Z-Report number, export date, adds VAT lines (one per rate), and optionally enters discount and notes. Totals auto-computed from lines.

## Mechanism B: Z-Report Bulk Import

CSV format (one row per VAT-rate-line per Z-report):

Date From,Date To,Z-Number,VAT Rate,Net Sales,VAT Amount,Discount,Export Date
01/11/2021,01/11/2021,78390,5,640.00,32.00,20.00,02/11/2021 08:15
01/11/2021,01/11/2021,78390,9,40.50,3.42,2.10,02/11/2021 08:15

Import engine groups rows by Date From + Date To + Z-Number into one RevenueSummary with multiple RevenueSummaryLines.

## Mechanism C: Sales Invoice Import

Transaction-level POS data. Customer ID only (FK to existing Customer table). Enables behaviour analytics.

## Entity Design

### RevenueSource
Business-scoped lookup (POS devices/registers). Forward-compatible with future API.
Columns: Id, BusinessId, Name, Description, IsActive, CreatedAtUtc

### RevenueSummary (Z-Report Header)
Columns: Id, BusinessId, RevenueSourceId, SummaryDate, PeriodEndDate, ZReportNumber, TotalNet, TotalVat, TotalGross, TotalDiscount, TransactionCount, Reference, Notes, ExportedAtUtc, VatSubmissionPeriodId, ImportSessionId, IsActive, CreatedAtUtc

### RevenueSummaryLine (VAT Breakdown)
Columns: Id, RevenueSummaryId, VatRate (DECIMAL 5,2), NetAmount, VatAmount, TotalAmount, DiscountAmount, Description, CreatedAtUtc

### ExternalSalesRecord (Transaction-Level)
Columns: Id, BusinessId, RevenueSourceId, TransactionDate, InvoiceNumber, CustomerId, NetAmount, VatAmount, TotalAmount, Description, PaymentMethod, ImportSessionId, VatSubmissionPeriodId, IsActive, CreatedAtUtc

## Real-World Example (Kennedy's Cafe - 3 Inventors POS)

Z-Report Number 78419, Period 01/11/2021-30/11/2021, Export Date 08/01/2022 12:40
Net Sales: 20561.36, VAT: 1070.72, Total: 21632.08, Discount: 786.72
VAT 5%: Net 19495.03, VAT 974.75
VAT 9%: Net 1066.33, VAT 95.97

## Subscription Tier Placement

Revenue Source + Z-Report Manual Entry: Foundation (all tiers)
Z-Report Bulk Import: Professional
Sales Invoice Import: Professional
Customer Behaviour Analytics: Enterprise

## Phasing

Phase 1: Revenue Source + Z-Report Manual Entry (Foundation)
Phase 2: Z-Report Bulk Import (Professional)
Phase 3: Sales Invoice Import (Professional)
Phase 4: Revenue Ingestion API (Future)
Phase 5: Customer Intelligence (Enterprise)

## Related Documents

- Purchase Import Automation - Import engine to reuse
- Customer Intelligence Brief - Analytics powered by this data
- Subscription Tier Model - Tier placement


---

## Detailed Entity Design (SQL DDL)

### [dbo].[RevenueSource]

```sql
CREATE TABLE [dbo].[RevenueSource] (
    [Id]            INT IDENTITY(1,1) NOT NULL,
    [BusinessId]    INT NOT NULL,
    [Name]          NVARCHAR(200) NOT NULL,
    [Description]   NVARCHAR(500) NULL,
    [IsActive]      BIT NOT NULL DEFAULT 1,
    [CreatedAtUtc]  DATETIME NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_RevenueSource] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RevenueSource_Business] FOREIGN KEY ([BusinessId]) REFERENCES [dbo].[Business]([Id])
);
```

Purpose: Configurable per business. Each entry represents a POS device, register, or sales channel. Examples: "Main POS", "Bar Register", "Terrace POS". Forward-compatible with future Sales Ingestion API where each RevenueSource gets its own API key for automated posting.

### [dbo].[RevenueSummary]

```sql
CREATE TABLE [dbo].[RevenueSummary] (
    [Id]                    INT IDENTITY(1,1) NOT NULL,
    [BusinessId]            INT NOT NULL,
    [RevenueSourceId]       INT NOT NULL,
    [SummaryDate]           DATE NOT NULL,
    [PeriodEndDate]         DATE NULL,
    [ZReportNumber]         NVARCHAR(50) NULL,
    [TotalNet]              DECIMAL(18,2) NOT NULL,
    [TotalVat]              DECIMAL(18,2) NOT NULL,
    [TotalGross]            DECIMAL(18,2) NOT NULL,
    [TotalDiscount]         DECIMAL(18,2) NULL,
    [TransactionCount]      INT NULL,
    [Reference]             NVARCHAR(200) NULL,
    [Notes]                 NVARCHAR(MAX) NULL,
    [ExportedAtUtc]         DATETIME NULL,
    [VatSubmissionPeriodId] INT NULL,
    [ImportSessionId]       INT NULL,
    [IsActive]              BIT NOT NULL DEFAULT 1,
    [CreatedAtUtc]          DATETIME NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_RevenueSummary] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RevenueSummary_Business] FOREIGN KEY ([BusinessId]) REFERENCES [dbo].[Business]([Id]),
    CONSTRAINT [FK_RevenueSummary_RevenueSource] FOREIGN KEY ([RevenueSourceId]) REFERENCES [dbo].[RevenueSource]([Id]),
    CONSTRAINT [FK_RevenueSummary_VatPeriod] FOREIGN KEY ([VatSubmissionPeriodId]) REFERENCES [dbo].[VatSubmissionPeriod]([Id])
);
```

Column notes:
- SummaryDate: Period start (e.g., 2021-11-01)
- PeriodEndDate: Period end (NULL = same as SummaryDate for daily reports)
- ZReportNumber: Official sequential number from the POS (e.g., "78419")
- TotalNet/TotalVat/TotalGross: Computed from child lines, stored for query performance
- TotalDiscount: Total discount from the Z-report
- ExportedAtUtc: When the POS generated the Z-report (audit trail)
- ImportSessionId: NULL if manually entered, set if bulk imported

### [dbo].[RevenueSummaryLine]

```sql
CREATE TABLE [dbo].[RevenueSummaryLine] (
    [Id]                INT IDENTITY(1,1) NOT NULL,
    [RevenueSummaryId]  INT NOT NULL,
    [VatRate]           DECIMAL(5,2) NOT NULL,
    [NetAmount]         DECIMAL(18,2) NOT NULL,
    [VatAmount]         DECIMAL(18,2) NOT NULL,
    [TotalAmount]       DECIMAL(18,2) NOT NULL,
    [DiscountAmount]    DECIMAL(18,2) NULL,
    [Description]       NVARCHAR(200) NULL,
    [CreatedAtUtc]      DATETIME NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_RevenueSummaryLine] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RevenueSummaryLine_Summary] FOREIGN KEY ([RevenueSummaryId]) REFERENCES [dbo].[RevenueSummary]([Id])
);
```

Column notes:
- VatRate: The VAT percentage (e.g., 5.00, 9.00, 19.00)
- NetAmount: Net sales at this VAT rate
- VatAmount: VAT collected at this rate
- TotalAmount: NetAmount + VatAmount (stored for convenience)
- DiscountAmount: Discount allocated to this VAT rate line (optional)
- Description: Optional label (e.g., "Food & Beverage", "Alcohol")

### [dbo].[ExternalSalesRecord]

```sql
CREATE TABLE [dbo].[ExternalSalesRecord] (
    [Id]                    INT IDENTITY(1,1) NOT NULL,
    [BusinessId]            INT NOT NULL,
    [RevenueSourceId]       INT NULL,
    [TransactionDate]       DATE NOT NULL,
    [InvoiceNumber]         NVARCHAR(100) NULL,
    [CustomerId]            INT NULL,
    [NetAmount]             DECIMAL(18,2) NOT NULL,
    [VatAmount]             DECIMAL(18,2) NOT NULL,
    [TotalAmount]           DECIMAL(18,2) NOT NULL,
    [Description]           NVARCHAR(500) NULL,
    [PaymentMethod]         NVARCHAR(50) NULL,
    [ImportSessionId]       INT NULL,
    [VatSubmissionPeriodId] INT NULL,
    [IsActive]              BIT NOT NULL DEFAULT 1,
    [CreatedAtUtc]          DATETIME NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_ExternalSalesRecord] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ExternalSalesRecord_Business] FOREIGN KEY ([BusinessId]) REFERENCES [dbo].[Business]([Id]),
    CONSTRAINT [FK_ExternalSalesRecord_RevenueSource] FOREIGN KEY ([RevenueSourceId]) REFERENCES [dbo].[RevenueSource]([Id]),
    CONSTRAINT [FK_ExternalSalesRecord_Customer] FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Customer]([Id]),
    CONSTRAINT [FK_ExternalSalesRecord_VatPeriod] FOREIGN KEY ([VatSubmissionPeriodId]) REFERENCES [dbo].[VatSubmissionPeriod]([Id])
);
```

---

## Detailed Mechanism B: Z-Report Bulk Import

### CSV Column Mapping Table

| CSV Column | Target Field | Required | Notes |
|------------|-------------|----------|-------|
| Date From | RevenueSummary.SummaryDate | Yes | Period start date |
| Date To | RevenueSummary.PeriodEndDate | Yes | Period end (equals Date From for daily) |
| Z-Number | RevenueSummary.ZReportNumber | Yes | Used for row grouping |
| VAT Rate | RevenueSummaryLine.VatRate | Yes | Percentage value (5, 9, 19) |
| Net Sales | RevenueSummaryLine.NetAmount | Yes | Net amount for this VAT rate |
| VAT Amount | RevenueSummaryLine.VatAmount | Yes | VAT for this rate |
| Discount | RevenueSummaryLine.DiscountAmount | No | Discount for this line |
| Export Date | RevenueSummary.ExportedAtUtc | No | POS generation timestamp |

### Grouping Logic Detail

Rows are grouped by the composite key: Date From + Date To + Z-Number.

Example input (6 CSV rows):
```
01/11/2021,01/11/2021,78390,5,640.00,32.00,20.00,02/11/2021 08:15
01/11/2021,01/11/2021,78390,9,40.50,3.42,2.10,02/11/2021 08:15
02/11/2021,02/11/2021,78391,5,700.00,35.00,17.00,03/11/2021 08:20
02/11/2021,02/11/2021,78391,9,23.80,2.68,1.50,03/11/2021 08:20
30/11/2021,30/11/2021,78419,5,19495.03,974.75,750.00,08/01/2022 12:40
30/11/2021,30/11/2021,78419,9,1066.33,95.97,36.72,08/01/2022 12:40
```

Result: 3 RevenueSummary records, each with 2 RevenueSummaryLine records.

Group 1 (Z-78390): TotalNet=680.50, TotalVat=35.42, TotalGross=715.92, TotalDiscount=22.10
Group 2 (Z-78391): TotalNet=723.80, TotalVat=37.68, TotalGross=761.48, TotalDiscount=18.50
Group 3 (Z-78419): TotalNet=20561.36, TotalVat=1070.72, TotalGross=21632.08, TotalDiscount=786.72

### Import Flow Steps

1. User selects Revenue Source from dropdown
2. User selects or creates a Parser Template
3. User uploads CSV/Excel file
4. Import engine parses rows using template column mappings
5. Engine groups rows by Date From + Date To + Z-Number
6. Preview grid shows grouped Z-reports (expandable to see VAT lines)
7. User reviews, corrects errors, confirms
8. Bulk insert: one RevenueSummary + N RevenueSummaryLines per group (single transaction)
9. Duplicate detection: Z-Number + RevenueSourceId + BusinessId

### Duplicate Detection

A Z-report is considered a duplicate if a RevenueSummary already exists with the same:
- BusinessId AND
- RevenueSourceId AND
- ZReportNumber

Duplicates are flagged as warnings in the preview grid (advisory, not blocking).

---

## Detailed Real-World Example

### Source: Kennedy's Cafe Z-Report (3 Inventors POS Software)

```
Kennedy's Cafe
46A Kennedy Str, Nicosia
Telephone 22250722
VAT Number: 10348073Y
Official Z-Report, Number 78419

Sales Report:
  Net Sales:  €20,561.36
  VAT:        €1,070.72
  Total:      €21,632.08

VAT Report:
  VAT 5%  — Net Sales: €19,495.03  — VAT: €974.75
  VAT 9%  — Net Sales: €1,066.33   — VAT: €95.97
  Total VAT: €1,070.72

Discount Report:
  Discount: €786.72

Period: 01/11/2021 – 30/11/2021
Export Date: 08/01/2022 12:40

Software by 3Inventors
```

### Entity Mapping

RevenueSummary record:
- RevenueSourceId: (FK to "Main POS" record)
- SummaryDate: 2021-11-01
- PeriodEndDate: 2021-11-30
- ZReportNumber: "78419"
- TotalNet: 20561.36
- TotalVat: 1070.72
- TotalGross: 21632.08
- TotalDiscount: 786.72
- ExportedAtUtc: 2022-01-08 12:40:00
- VatSubmissionPeriodId: (assigned to Nov 2021 period)
- ImportSessionId: NULL (manually entered)

RevenueSummaryLine record 1:
- VatRate: 5.00
- NetAmount: 19495.03
- VatAmount: 974.75
- TotalAmount: 20469.78
- DiscountAmount: NULL (not split by rate in this report)
- Description: NULL

RevenueSummaryLine record 2:
- VatRate: 9.00
- NetAmount: 1066.33
- VatAmount: 95.97
- TotalAmount: 1162.30
- DiscountAmount: NULL
- Description: NULL

---

## VAT Integration Detail

### Output VAT Calculation (Updated Formula)

```
Total Output VAT for Period = 
    SUM(Invoice.TaxAmount where InvoiceStatusTypeId=2 and !IsDeleted)
  + SUM(RevenueSummary.TotalVat)
  + SUM(ExternalSalesRecord.VatAmount)
  - SUM(CreditNote.TaxAmount where status=Issued or Applied)
```

### VAT Period Report: New Section

The Period Report (Vat/PeriodReport) gains a new section between "Sales Invoices" and "Purchases":

Section title: "External Revenue (Z-Reports)"
Subtitle: "Revenue summaries from POS systems for this period"

Table columns: Source, Z-Report #, Period, Net Amount, VAT Amount, Total, Discount

Each row is one RevenueSummary. Expandable to show VAT rate breakdown (RevenueSummaryLines).

Period Total row at the bottom sums all Z-reports.

### VAT Detail Page: New Section

The Detail page (Vat/Detail) gains a new section between "Sales Invoices" and "Purchases":

Similar to the invoices section but showing RevenueSummary records with:
- Revenue Source name
- Z-Report number
- Period dates
- VAT total
- Assignment status (Explicit / Date Range)

With its own "Export CSV" button.

---

## Revenue Source: Future API Compatibility

The RevenueSource entity is designed to be API-ready. In Phase 4, the table gains:

```sql
ALTER TABLE [dbo].[RevenueSource]
ADD [ApiKey] NVARCHAR(64) NULL,
    [IsApiEnabled] BIT NOT NULL DEFAULT 0;
```

Each POS device that supports API posting will have its own RevenueSource with an API key. The ingestion endpoint authenticates by: Business API Key (identifies the business) + Revenue Source API Key (identifies the device).

This means the same POS that generates Kennedy's Cafe Z-reports could eventually post them directly to Portal without any manual entry or import.

---

## Open Questions (Remaining)

1. VAT Rate on ExternalSalesRecord: Should individual sales records track VAT rate? Or just total VAT amount? (For now: just total. Can add VatRate column later if needed.)
2. Discount allocation: When importing Z-reports, should discount be split proportionally across VAT lines? Or kept as a single total on the header? (For now: single total on header, optional per-line.)
3. Z-Report numbering validation: Should we enforce sequential Z-Report numbers? (Probably not — some POS systems reset numbering. Just use it for dedup, not validation.)


---

## Additional Requirements (from review)

### 1. Z-Report Feature Toggle on MyBusiness Page

A toggle on the MyBusiness settings page enables/disables Z-Report functionality for the business.

New column on BusinessProfile:
```sql
ALTER TABLE [dbo].[BusinessProfile]
ADD [IsZReportEnabled] BIT NOT NULL DEFAULT 0;
```

Behaviour when IsZReportEnabled = false:
- "Z-Reports" nav item is hidden from the Revenue menu
- Revenue dashboard only shows Portal-issued invoices
- VAT reports do not include Z-Report/External Revenue sections (no empty sections)
- Revenue Source management is hidden

Behaviour when IsZReportEnabled = true:
- "Z-Reports" nav item appears under Revenue
- Revenue dashboard aggregates Portal invoices + Z-Reports
- VAT reports include the "External Revenue (Z-Reports)" section
- Revenue Source management is accessible
- If no Revenue Sources exist, prompt user to create their first one

This prevents clutter for businesses that don't use POS systems while making the feature discoverable for those that do.

### 2. Document Attachments on Z-Report Entries

Each RevenueSummary record supports file attachments (PDF, image) via the existing Document Attachments feature. The attachment links via EntityType = "RevenueSummary" and EntityId = RevenueSummary.Id.

Purpose: Store the original Z-report printout as audit evidence for VAT compliance. The accountant or tax auditor can view the source document directly from the platform.
