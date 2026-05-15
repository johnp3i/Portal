# Requirements Document

## Introduction

Quotation Platform (Module 2) delivers tenant-scoped quotation management with line items, lifecycle state transitions, pricing calculations, and audit logging within the Portal platform. Each business (tenant) creates quotations for their customers, manages line items with computed pricing, and transitions quotations through a defined lifecycle (Draft → Sent → Accepted → Converted → Archived).

The Quotation, QuotationLine, and QuotationStatusType entities and database tables already exist in the `[quotation]` schema. This module implements the application layer — repositories, service, controller, and Razor views — to expose quotation management to authenticated tenant users.

## Glossary

- **Quotation**: A commercial proposal document containing priced line items sent to a Customer, stored in `[quotation].[Quotation]`.
- **QuotationLine**: An individual priced item within a Quotation, stored in `[quotation].[QuotationLine]`.
- **QuotationStatusType**: A reference table defining lifecycle states: Draft (1), Sent (2), Accepted (3), Converted (4), Archived (5).
- **QuotationRepository**: A table repository extending GenericStoredProcedureRepository for Quotation CRUD operations against `[quotation].[Quotation]`.
- **QuotationLineRepository**: A table repository extending GenericStoredProcedureRepository for QuotationLine CRUD operations against `[quotation].[QuotationLine]`.
- **QuotationService**: A scoped service implementing IQuotationService that contains business logic for quotation management, lifecycle transitions, and pricing calculations.
- **QuotationController**: An MVC controller handling HTTP requests for quotation list, create, edit, detail, and status transition operations.
- **Lifecycle_State_Machine**: The defined set of valid status transitions: Draft → Sent, Sent → Accepted, Sent → Archived, Accepted → Converted, Accepted → Archived, Draft → Archived.
- **LineTotal**: A computed value per line: Quantity × UnitPrice.
- **Subtotal**: The sum of all LineTotal values across a quotation's lines.
- **TaxAmount**: The sum of all (LineTotal × VatRate / 100) values across a quotation's lines.
- **TotalAmount**: Subtotal + TaxAmount.
- **ValidUntil**: A date field indicating when a quotation expires. After this date, the quotation is considered expired.
- **AuditLog**: An append-only record in `[audit].[AuditLog]` tracking status transitions with old and new values.
- **CurrentTenantService**: A scoped service that resolves the current tenant's BusinessId from the authenticated user's claims.
- **Tenant_Isolation**: The enforcement that users can only access Quotation records belonging to their own Business, implemented via EF Core global query filters on BusinessId.

## Requirements

### Requirement 1: Quotation Repository

**User Story:** As a developer, I want a QuotationRepository with CRUD operations, so that the service layer can persist and retrieve quotation data following established repository patterns.

#### Acceptance Criteria

1. THE QuotationRepository SHALL extend GenericStoredProcedureRepository with Quotation as the type parameter.
2. THE QuotationRepository SHALL provide a method to retrieve all quotations for a given BusinessId, including the related Customer name and QuotationStatusType name.
3. THE QuotationRepository SHALL provide a method to retrieve a single quotation by Id and BusinessId.
4. WHEN a new quotation is created, THE QuotationRepository SHALL insert a record into `[quotation].[Quotation]` with BusinessId, CustomerId, QuotationStatusTypeId, Reference, ValidUntil, Subtotal, TaxAmount, TotalAmount, Notes, CreatedAtUtc, and UpdatedAtUtc.
5. WHEN a quotation is updated, THE QuotationRepository SHALL update CustomerId, Reference, ValidUntil, Subtotal, TaxAmount, TotalAmount, Notes, QuotationStatusTypeId, and UpdatedAtUtc on the matching record.
6. THE QuotationRepository SHALL use full table names in SQL queries without aliases.
7. THE QuotationRepository SHALL use null-safe SQL parameters using `?? (object)DBNull.Value` for all nullable fields.
8. THE QuotationRepository SHALL wrap all data access in try/catch with rethrow.

### Requirement 2: QuotationLine Repository

**User Story:** As a developer, I want a QuotationLineRepository with CRUD operations, so that the service layer can persist and retrieve line item data for quotations.

#### Acceptance Criteria

1. THE QuotationLineRepository SHALL extend GenericStoredProcedureRepository with QuotationLine as the type parameter.
2. THE QuotationLineRepository SHALL provide a method to retrieve all lines for a given QuotationId ordered by SortOrder.
3. WHEN a new line is created, THE QuotationLineRepository SHALL insert a record into `[quotation].[QuotationLine]` with QuotationId, Description, Quantity, UnitPrice, LineTotal, and SortOrder.
4. WHEN a line is updated, THE QuotationLineRepository SHALL update Description, Quantity, UnitPrice, LineTotal, and SortOrder on the matching record.
5. WHEN a line is removed, THE QuotationLineRepository SHALL delete the record from `[quotation].[QuotationLine]` by Id.
6. THE QuotationLineRepository SHALL provide a method to delete all lines for a given QuotationId.
7. THE QuotationLineRepository SHALL use full table names in SQL queries without aliases.
8. THE QuotationLineRepository SHALL use null-safe SQL parameters using `?? (object)DBNull.Value` for all nullable fields.
9. THE QuotationLineRepository SHALL wrap all data access in try/catch with rethrow.

### Requirement 3: Quotation Service

**User Story:** As a developer, I want an IQuotationService interface and implementation, so that business logic for quotation management is encapsulated in a testable service layer.

#### Acceptance Criteria

1. THE QuotationService SHALL implement the IQuotationService interface.
2. THE QuotationService SHALL be registered as a scoped service in the DI container.
3. WHEN retrieving quotations, THE QuotationService SHALL return only quotations belonging to the current tenant's BusinessId.
4. WHEN creating a quotation, THE QuotationService SHALL set BusinessId from the current tenant, set QuotationStatusTypeId to 1 (Draft), and set CreatedAtUtc and UpdatedAtUtc to the current UTC time.
5. WHEN creating a quotation, THE QuotationService SHALL generate a unique Reference value for the quotation.
6. WHEN updating a quotation, THE QuotationService SHALL set UpdatedAtUtc to the current UTC time.
7. THE QuotationService SHALL validate that CustomerId references a valid customer belonging to the current tenant before creating or updating a quotation.
8. IF validation fails, THEN THE QuotationService SHALL throw an ArgumentException with a descriptive message.

### Requirement 4: Quotation Lifecycle State Machine

**User Story:** As a business user, I want quotations to follow a defined lifecycle, so that the status of each proposal is tracked and only valid transitions are permitted.

#### Acceptance Criteria

1. THE QuotationService SHALL enforce the following valid status transitions: Draft → Sent, Sent → Accepted, Sent → Archived, Accepted → Converted, Accepted → Archived, Draft → Archived.
2. IF a requested status transition is not in the set of valid transitions, THEN THE QuotationService SHALL throw an InvalidOperationException with a message indicating the transition is not allowed.
3. WHEN a status transition is performed, THE QuotationService SHALL update QuotationStatusTypeId and UpdatedAtUtc on the quotation record.
4. WHILE a quotation is in Draft status, THE QuotationService SHALL allow editing of quotation fields and line items.
5. WHILE a quotation is not in Draft status, THE QuotationService SHALL reject any attempt to edit quotation fields or line items with an InvalidOperationException.
6. WHEN a quotation is transitioned to Sent status, THE QuotationService SHALL validate that the quotation has at least one line item.
7. WHEN a quotation is transitioned to Converted status, THE QuotationService SHALL record the transition for downstream invoice conversion processing.

### Requirement 5: Line Item Management

**User Story:** As a business user, I want to add, edit, remove, and reorder line items on a quotation, so that I can build accurate proposals with multiple priced items.

#### Acceptance Criteria

1. WHEN a line item is added to a quotation, THE QuotationService SHALL compute LineTotal as Quantity × UnitPrice and assign the next SortOrder value.
2. WHEN a line item is edited, THE QuotationService SHALL recompute LineTotal as Quantity × UnitPrice.
3. WHEN a line item is removed, THE QuotationService SHALL remove the record and recalculate the quotation totals.
4. WHEN line items are reordered, THE QuotationService SHALL update the SortOrder values to reflect the new order.
5. THE QuotationService SHALL validate that Description is not null or whitespace for each line item.
6. THE QuotationService SHALL validate that Quantity is greater than zero for each line item.
7. THE QuotationService SHALL validate that UnitPrice is zero or greater for each line item.
8. IF line item validation fails, THEN THE QuotationService SHALL throw an ArgumentException with a descriptive message.

### Requirement 6: Pricing Calculation

**User Story:** As a business user, I want quotation totals to be automatically computed from line items, so that financial values are always accurate and consistent.

#### Acceptance Criteria

1. WHEN line items are added, edited, or removed, THE QuotationService SHALL recalculate Subtotal as the sum of all LineTotal values for the quotation.
2. WHEN line items are added, edited, or removed, THE QuotationService SHALL recalculate TaxAmount as the sum of all (LineTotal × VatRate / 100) values for the quotation.
3. WHEN line items are added, edited, or removed, THE QuotationService SHALL recalculate TotalAmount as Subtotal + TaxAmount.
4. THE QuotationService SHALL persist the recalculated Subtotal, TaxAmount, and TotalAmount on the Quotation record after any line item change.
5. THE QuotationService SHALL use decimal precision (18,2) for all financial calculations to prevent rounding errors.
6. THE QuotationService SHALL reject any attempt to manually set Subtotal, TaxAmount, or TotalAmount directly — these values are computed only.

### Requirement 7: Tenant Isolation

**User Story:** As a business user, I want to see only my own business's quotations, so that quotation data remains private between tenants.

#### Acceptance Criteria

1. THE PortalDbContext SHALL apply a global query filter on Quotation ensuring that only records matching the current tenant's BusinessId are returned.
2. WHEN creating a quotation, THE QuotationService SHALL assign the BusinessId from ICurrentTenantService, preventing users from creating quotations under a different tenant.
3. WHEN retrieving a quotation by Id, THE QuotationService SHALL verify the quotation belongs to the current tenant's BusinessId before returning the record.
4. IF a user attempts to access a quotation belonging to a different Business, THEN THE QuotationController SHALL return a NotFound response.

### Requirement 8: ValidUntil Expiry Logic

**User Story:** As a business user, I want quotations to have an expiry date, so that I can track which proposals are still valid and which have expired.

#### Acceptance Criteria

1. WHEN a quotation is created or edited, THE QuotationService SHALL accept an optional ValidUntil date.
2. THE QuotationService SHALL expose a method or property to determine whether a quotation has expired (ValidUntil is not null and ValidUntil < today's date).
3. WHILE a quotation has expired (ValidUntil < today), THE Quotation list view SHALL visually indicate the expired state.
4. THE QuotationService SHALL still allow status transitions on expired quotations (expiry is informational, not blocking).

### Requirement 9: Audit Logging for Status Transitions

**User Story:** As a business user, I want all quotation status changes to be recorded in an audit trail, so that I can track who changed what and when.

#### Acceptance Criteria

1. WHEN a quotation status transition is performed, THE QuotationService SHALL insert a record into `[audit].[AuditLog]` with the BusinessId, UserId, Action set to "StatusTransition", TableName set to "quotation.Quotation", RecordId set to the quotation Id, OldValues containing the previous status name, and NewValues containing the new status name.
2. THE QuotationService SHALL record the Timestamp as the current UTC time for each audit log entry.
3. THE AuditLog record SHALL be created within the same logical operation as the status transition to maintain consistency.
4. THE QuotationService SHALL never update or delete existing AuditLog records.

### Requirement 10: Quotation Controller

**User Story:** As a business user, I want to list, create, edit, view details, and transition quotation statuses through the web interface, so that I can manage my quotations.

#### Acceptance Criteria

1. THE QuotationController SHALL require authentication via the Authorize attribute.
2. THE QuotationController SHALL delegate all business logic to IQuotationService.
3. WHEN a user navigates to the quotation list, THE QuotationController SHALL return a view displaying all quotations for the current tenant.
4. WHEN a user submits a valid quotation creation form, THE QuotationController SHALL create the quotation and redirect to the edit view for adding line items.
5. WHEN a user submits a valid quotation edit form, THE QuotationController SHALL update the quotation and redirect to the quotation detail view.
6. WHEN a user requests a status transition, THE QuotationController SHALL perform the transition and redirect to the quotation detail view.
7. IF model validation fails on create or edit, THEN THE QuotationController SHALL redisplay the form with validation error messages.
8. IF a business rule violation occurs, THEN THE QuotationController SHALL redisplay the form with the error message from the service layer.
9. THE QuotationController SHALL use ValidateAntiForgeryToken on all POST actions.
10. WHEN a user navigates to the quotation detail, THE QuotationController SHALL return a view displaying the quotation with all line items and available status transitions.

### Requirement 11: Quotation List UI

**User Story:** As a business user, I want a quotation list screen following the MyChair Design System, so that I can view and manage all my quotations with filtering capabilities.

#### Acceptance Criteria

1. THE Quotation list view SHALL display quotations in a table layout following the MyChair Design System (Primary Blue #0D5EA6, Manrope headings, Inter body, cards with 20-30px border radius).
2. THE Quotation list view SHALL display Reference, Customer name, Status, TotalAmount, ValidUntil, and CreatedAtUtc for each quotation.
3. THE Quotation list view SHALL provide filter controls for status, customer, and date range above the quotation list.
4. WHEN a status filter is selected, THE Quotation list view SHALL display only quotations matching the selected status.
5. WHEN a customer filter is selected, THE Quotation list view SHALL display only quotations for the selected customer.
6. THE Quotation list view SHALL provide action links to view details and edit each quotation.
7. THE Quotation list view SHALL provide a button to create a new quotation.
8. THE Quotation list view SHALL visually distinguish quotations by status using colour-coded badges.
9. THE Quotation list view SHALL visually indicate expired quotations (where ValidUntil < today).

### Requirement 12: Quotation Create/Edit Form UI

**User Story:** As a business user, I want a quotation create/edit form with dynamic line items following the MyChair Design System, so that I can build proposals with accurate pricing.

#### Acceptance Criteria

1. THE Quotation form view SHALL display input fields for: Customer (dropdown), ValidUntil (date picker), and Notes (textarea).
2. THE Quotation form view SHALL display a dynamic line items section where users can add, edit, and remove lines.
3. WHEN a line item is added, THE Quotation form view SHALL display fields for Description, Quantity, UnitPrice, and a computed LineTotal.
4. THE Quotation form view SHALL display computed Subtotal, TaxAmount, and TotalAmount that update as line items change.
5. THE Quotation form view SHALL mark Customer as required with a visual indicator.
6. WHEN validation errors exist, THE Quotation form view SHALL display error messages adjacent to the relevant fields.
7. THE Quotation form view SHALL pre-populate all fields with existing values when editing a quotation.
8. THE Quotation form view SHALL only be accessible for quotations in Draft status.
9. THE Quotation form view SHALL follow the MyChair Design System styling (input fields, buttons, spacing, typography).

### Requirement 13: Quotation Detail/Preview Screen

**User Story:** As a business user, I want a quotation detail screen showing the full proposal with line items and available actions, so that I can review and manage individual quotations.

#### Acceptance Criteria

1. THE Quotation detail view SHALL display the quotation Reference, Customer name, Status, ValidUntil, Notes, and creation date.
2. THE Quotation detail view SHALL display all line items in a table with Description, Quantity, UnitPrice, and LineTotal columns.
3. THE Quotation detail view SHALL display the computed Subtotal, TaxAmount, and TotalAmount.
4. THE Quotation detail view SHALL display action buttons for valid status transitions based on the current status.
5. WHILE the quotation is in Draft status, THE Quotation detail view SHALL display an Edit button.
6. WHILE the quotation is in Draft status, THE Quotation detail view SHALL display Send and Archive buttons.
7. WHILE the quotation is in Sent status, THE Quotation detail view SHALL display Accept and Archive buttons.
8. WHILE the quotation is in Accepted status, THE Quotation detail view SHALL display Convert to Invoice and Archive buttons.
9. THE Quotation detail view SHALL follow the MyChair Design System styling.
10. THE Quotation detail view SHALL visually indicate if the quotation has expired.

### Requirement 14: Search and Filtering

**User Story:** As a business user, I want to filter quotations by status, customer, and date, so that I can quickly find specific quotations.

#### Acceptance Criteria

1. WHEN a status filter is provided, THE QuotationService SHALL return only quotations matching the specified QuotationStatusTypeId.
2. WHEN a customer filter is provided, THE QuotationService SHALL return only quotations matching the specified CustomerId.
3. WHEN a date range filter is provided, THE QuotationService SHALL return only quotations with CreatedAtUtc within the specified range.
4. WHEN multiple filters are provided, THE QuotationService SHALL apply all filters simultaneously.
5. WHEN no filters are provided, THE QuotationService SHALL return all quotations for the current tenant.

### Requirement 15: Integration Points

**User Story:** As a developer, I want the Quotation module to support downstream conversion to invoices, so that accepted quotations can be deterministically converted.

#### Acceptance Criteria

1. THE Quotation entity SHALL maintain a navigation property to the Invoice collection for use by the downstream Invoice module.
2. WHEN a quotation is transitioned to Converted status, THE QuotationService SHALL ensure the quotation Id is available for the Invoice conversion service to reference.
3. THE QuotationService SHALL not perform the actual invoice creation — this responsibility belongs to the Invoice module (Module 3).
4. THE QuotationLine entity SHALL maintain all fields required for copying to InvoiceLine during conversion (Description, Quantity, UnitPrice, LineTotal, SortOrder).
