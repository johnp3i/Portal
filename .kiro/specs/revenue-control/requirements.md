# Requirements Document

## Introduction

The Revenue Control module provides an operational financial control layer for the Portal platform. It enables businesses to track invoice payments, monitor outstanding balances, detect overdue invoices, and gain revenue visibility through dashboards and reporting. This module sits after the Quotation → Invoice workflow and answers: which invoices are unpaid, which customers are overdue, what amount is outstanding, and what revenue has been collected versus still open.

The existing database schema (Invoice, Payment, InvoiceLine, PaymentMethodType, InvoiceFinancialStatusType, VatSubmissionPeriod) is already in place. This module implements the application services, controllers, and UI screens that operate on these tables.

## Glossary

- **Revenue_Control_System**: The application layer (services, controllers, views) that implements payment recording, balance calculation, overdue detection, and revenue visibility
- **Payment_Service**: The service responsible for validating, creating, and voiding payments against invoices
- **Financial_Status_Engine**: The component that deterministically computes an invoice's financial status from its payment history
- **Receivables_Query_Service**: The service that provides filtered, paginated lists of invoices with their financial state
- **Dashboard_Service**: The service that computes KPI aggregates, chart data, and summary tables for the revenue dashboard
- **VAT_Integration_Service**: The service that computes Output VAT, Input VAT, and Net VAT Payable from paid invoices and purchases within VAT periods
- **Invoice**: A financial document in the `[invoice].Invoice` table representing an obligation to pay
- **Payment**: A monetary transaction in the `[revenue].Payment` table recorded against an Invoice
- **Financial_Status**: One of: Unpaid, PartiallyPaid, Paid, Overdue, WrittenOff — stored in `InvoiceFinancialStatusTypeId`
- **Outstanding_Balance**: The computed value: Invoice.TotalAmount minus the sum of valid (non-voided) payments
- **Valid_Payment**: A Payment record where IsVoided = 0
- **Overdue_Invoice**: An invoice where Outstanding_Balance > 0 AND DueDate < today's date
- **VAT_Period**: A VatSubmissionPeriod record defining a date range for VAT reporting

## Requirements

### Requirement 1: Record Payment Against Invoice

**User Story:** As a business operator, I want to record a payment against an issued invoice, so that I can track how much has been collected and update the invoice's financial status.

#### Acceptance Criteria

1. WHEN a payment is submitted, THE Payment_Service SHALL validate that the target invoice has InvoiceStatusTypeId = 2 (Issued)
2. WHEN a payment is submitted against a Draft or Cancelled invoice, THE Payment_Service SHALL reject the payment and return a descriptive error message
3. WHEN a payment is submitted, THE Payment_Service SHALL validate that the payment amount is greater than zero
4. WHEN a payment is submitted with an amount exceeding the Outstanding_Balance, THE Payment_Service SHALL reject the payment and return an error indicating the maximum allowed amount
5. WHEN a payment is submitted with valid data, THE Payment_Service SHALL create a Payment record with BusinessId, InvoiceId, PaymentMethodTypeId, PaymentDateUtc, Amount, Reference, Notes, and CreatedByUserId
6. WHEN a payment is successfully recorded, THE Financial_Status_Engine SHALL recalculate and update the invoice's InvoiceFinancialStatusTypeId

### Requirement 2: Deterministic Financial Status Calculation

**User Story:** As a business operator, I want invoice financial statuses to be computed automatically from payment data, so that I always see an accurate and consistent view of each invoice's payment state.

#### Acceptance Criteria

1. THE Financial_Status_Engine SHALL compute Outstanding_Balance as: Invoice.TotalAmount minus the sum of Payment.Amount where Payment.IsVoided = 0 for that invoice
2. WHEN Outstanding_Balance equals Invoice.TotalAmount AND DueDate is greater than or equal to today, THE Financial_Status_Engine SHALL set the status to Unpaid (Id = 1)
3. WHEN Outstanding_Balance is greater than zero AND at least one Valid_Payment exists AND DueDate is greater than or equal to today, THE Financial_Status_Engine SHALL set the status to PartiallyPaid (Id = 2)
4. WHEN Outstanding_Balance equals zero AND at least one Valid_Payment exists, THE Financial_Status_Engine SHALL set the status to Paid (Id = 3)
5. WHEN Outstanding_Balance is greater than zero AND DueDate is less than today, THE Financial_Status_Engine SHALL set the status to Overdue (Id = 4)
6. THE Financial_Status_Engine SHALL preserve WrittenOff (Id = 5) status unchanged during automatic recalculation
7. FOR ALL invoices, computing Outstanding_Balance then recalculating status then computing Outstanding_Balance again SHALL produce the same Outstanding_Balance value (idempotence property)

### Requirement 3: Void Payment

**User Story:** As a business operator, I want to void a previously recorded payment, so that I can correct mistakes without losing the audit trail.

#### Acceptance Criteria

1. WHEN a void action is requested, THE Payment_Service SHALL set Payment.IsVoided = 1 on the target payment record
2. THE Payment_Service SHALL NOT physically delete any Payment record from the database
3. WHEN a payment is voided, THE Financial_Status_Engine SHALL recalculate and update the parent invoice's InvoiceFinancialStatusTypeId
4. WHEN a payment that is already voided is targeted for voiding, THE Payment_Service SHALL return an informational message indicating the payment is already voided

### Requirement 4: Revenue Dashboard KPI Cards

**User Story:** As a business operator, I want to see key financial metrics at a glance on the revenue dashboard, so that I can quickly assess the state of my receivables.

#### Acceptance Criteria

1. THE Dashboard_Service SHALL compute Outstanding Receivables as the sum of Outstanding_Balance across all non-deleted invoices with InvoiceStatusTypeId = 2 (Issued) and InvoiceFinancialStatusTypeId in (1, 2, 4) for the current business tenant
2. THE Dashboard_Service SHALL compute Overdue Amount as the sum of Outstanding_Balance across all invoices where DueDate is less than today and Outstanding_Balance is greater than zero for the current business tenant
3. THE Dashboard_Service SHALL compute Paid This Month as the sum of Payment.Amount where Payment.IsVoided = 0 and Payment.PaymentDateUtc falls within the current calendar month for the current business tenant
4. THE Dashboard_Service SHALL compute Partially Paid amount as the sum of Outstanding_Balance across all invoices with InvoiceFinancialStatusTypeId = 2 (PartiallyPaid) for the current business tenant

### Requirement 5: Revenue Dashboard Charts

**User Story:** As a business operator, I want to see revenue trends over time, so that I can understand collection patterns and forecast cash flow.

#### Acceptance Criteria

1. THE Dashboard_Service SHALL provide Revenue Collected data as monthly totals of valid payments for the last 12 months for the current business tenant
2. THE Dashboard_Service SHALL provide Invoiced vs Collected data as paired monthly totals of invoice TotalAmount (by InvoiceDate) and valid payment Amount (by PaymentDateUtc) for the last 12 months for the current business tenant
3. THE Dashboard_Service SHALL compute Collection Rate as the percentage of total invoiced amount that has been collected within 30 days of invoice date, for invoices issued in the last 12 months for the current business tenant

### Requirement 6: VAT Integration on Dashboard

**User Story:** As a business operator, I want to see VAT liability information on the revenue dashboard, so that I can understand my tax obligations without navigating to the VAT module.

#### Acceptance Criteria

1. THE VAT_Integration_Service SHALL compute Output VAT Collected as the sum of Invoice.TaxAmount for invoices that are fully paid (InvoiceFinancialStatusTypeId = 3) and whose InvoiceDate falls within the current VAT period for the current business tenant
2. THE VAT_Integration_Service SHALL compute Input VAT as the sum of Purchase.VatAmount for purchases whose InvoiceDate falls within the current VAT period for the current business tenant
3. THE VAT_Integration_Service SHALL compute Net VAT Payable as Output VAT Collected minus Input VAT
4. THE VAT_Integration_Service SHALL provide VAT Liability by Period data as Net VAT Payable values for the last 6 VAT periods for the current business tenant
5. THE VAT_Integration_Service SHALL compute Output/Input VAT Ratio as Output VAT Collected divided by Input VAT for the current VAT period, returning zero when Input VAT is zero

### Requirement 7: Overdue Invoices Table on Dashboard

**User Story:** As a business operator, I want to see a list of overdue invoices on the dashboard, so that I can prioritize collection efforts.

#### Acceptance Criteria

1. THE Dashboard_Service SHALL return overdue invoices sorted by days overdue in descending order for the current business tenant
2. THE Dashboard_Service SHALL include for each overdue invoice: InvoiceNumber, Customer name, DueDate, days overdue (today minus DueDate), and Outstanding_Balance
3. WHEN a search term is provided, THE Dashboard_Service SHALL filter overdue invoices by InvoiceNumber or Customer name containing the search term (case-insensitive)
4. THE Dashboard_Service SHALL support pagination for the overdue invoices list with configurable page size

### Requirement 8: Recent Payments Table on Dashboard

**User Story:** As a business operator, I want to see recent payment activity on the dashboard, so that I can confirm incoming funds and spot anomalies.

#### Acceptance Criteria

1. THE Dashboard_Service SHALL return recent payments sorted by PaymentDateUtc in descending order for the current business tenant
2. THE Dashboard_Service SHALL include for each payment: PaymentDateUtc, InvoiceNumber, Customer name, PaymentMethodType name, Amount, and a label indicating Full Payment or Partial based on whether the invoice became fully paid
3. WHEN a search term is provided, THE Dashboard_Service SHALL filter recent payments by InvoiceNumber or Customer name containing the search term (case-insensitive)
4. THE Dashboard_Service SHALL support pagination for the recent payments list with configurable page size
5. THE Dashboard_Service SHALL exclude voided payments from the recent payments list

### Requirement 9: Receivables List with Filtering

**User Story:** As a business operator, I want to view all issued invoices with their payment status and filter by various criteria, so that I can manage my accounts receivable effectively.

#### Acceptance Criteria

1. THE Receivables_Query_Service SHALL return all non-deleted invoices with InvoiceStatusTypeId = 2 (Issued) for the current business tenant
2. THE Receivables_Query_Service SHALL include for each invoice: InvoiceNumber, Customer name, InvoiceDate, DueDate, TotalAmount, total paid (sum of valid payments), Outstanding_Balance, and Financial_Status name
3. WHEN a search term is provided, THE Receivables_Query_Service SHALL filter by InvoiceNumber or Customer name containing the search term (case-insensitive)
4. WHEN a financial status filter is provided, THE Receivables_Query_Service SHALL return only invoices matching the specified InvoiceFinancialStatusTypeId
5. WHEN a customer filter is provided, THE Receivables_Query_Service SHALL return only invoices for the specified CustomerId
6. WHEN a date range filter is provided (due from and/or due to), THE Receivables_Query_Service SHALL return only invoices whose DueDate falls within the specified range
7. THE Receivables_Query_Service SHALL support pagination with configurable page size and return total count for pagination controls
8. THE Receivables_Query_Service SHALL display a "Pay" action link on invoices where Outstanding_Balance is greater than zero

### Requirement 10: Invoice Detail with Payment History

**User Story:** As a business operator, I want to view an invoice's full financial detail including all payments made against it, so that I can understand the payment timeline and remaining balance.

#### Acceptance Criteria

1. THE Revenue_Control_System SHALL display financial summary KPI cards showing: Invoice Total (TotalAmount), Total Paid (sum of valid payments), Outstanding_Balance, and DueDate with an overdue indicator when DueDate is less than today and Outstanding_Balance is greater than zero
2. THE Revenue_Control_System SHALL display invoice line items in a table showing: Description, Quantity, UnitPrice, LineTotal, and SortOrder
3. THE Revenue_Control_System SHALL display payment history in a table showing: PaymentDateUtc, Amount, PaymentMethodType name, Reference, Notes, and a Void action button for non-voided payments
4. THE Revenue_Control_System SHALL display a payment progress bar showing the percentage of TotalAmount that has been paid versus the outstanding percentage
5. THE Revenue_Control_System SHALL display a "Record Payment" button that opens the Add Payment modal pre-populated with the invoice context
6. WHEN a payment is voided from the invoice detail view, THE Revenue_Control_System SHALL visually mark the voided payment and recalculate displayed totals

### Requirement 11: Add Payment Modal

**User Story:** As a business operator, I want a modal form to record a payment with proper validation and context, so that I can quickly and accurately enter payment information.

#### Acceptance Criteria

1. THE Revenue_Control_System SHALL display an invoice context bar in the modal showing: InvoiceNumber, Customer name, and remaining balance (Outstanding_Balance)
2. THE Revenue_Control_System SHALL provide input fields for: Payment Date (required), Amount (required, numeric), Payment Method (required, dropdown from active PaymentMethodType records), Reference (optional), and Notes (optional)
3. WHEN the Amount field value exceeds the Outstanding_Balance, THE Revenue_Control_System SHALL display a client-side validation error and prevent form submission
4. WHEN the Amount field value is zero or negative, THE Revenue_Control_System SHALL display a client-side validation error and prevent form submission
5. WHEN the form is submitted with valid data, THE Revenue_Control_System SHALL call the Payment_Service, display a loading state (BlockUI), and show a success confirmation (SweetAlert2) upon completion
6. IF the Payment_Service returns an error, THEN THE Revenue_Control_System SHALL display the error message via SweetAlert2 and allow the user to correct and resubmit

### Requirement 12: Tenant Isolation

**User Story:** As a platform operator, I want all revenue control data to be scoped to the authenticated business tenant, so that businesses cannot see or modify each other's financial data.

#### Acceptance Criteria

1. THE Revenue_Control_System SHALL filter all invoice queries by the authenticated user's BusinessId
2. THE Revenue_Control_System SHALL filter all payment queries by the authenticated user's BusinessId
3. WHEN a payment is created, THE Payment_Service SHALL set Payment.BusinessId to the authenticated user's BusinessId
4. THE Revenue_Control_System SHALL reject any request that attempts to access an invoice or payment belonging to a different BusinessId

