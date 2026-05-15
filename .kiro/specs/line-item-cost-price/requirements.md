# Requirements Document

## Introduction

This feature adds a CostPrice column to quotation line items for internal profit and margin tracking. The CostPrice represents the actual cost or purchase price of an item (e.g., a domain costs €39 but is charged at €60 to the customer). This field is strictly internal — it is never exposed in proposals, shared views, or invoices. It enables business statistics and profit analysis on hardware and items that are not direct services.

## Glossary

- **Portal**: The ASP.NET Core MVC 8 web application serving as the back-office platform.
- **QuotationLine**: An individual priced item within a Quotation, stored in the `[quotation].[QuotationLine]` table.
- **CostPrice**: A nullable decimal field representing the actual cost/purchase price of a line item, used for internal margin calculations.
- **UnitPrice**: The price charged to the customer per unit of the line item.
- **LineTotal**: The computed total for a line item (UnitPrice × Quantity, adjusted for discounts).
- **Margin**: The profit on a line item, calculated as UnitPrice minus CostPrice per unit, or LineTotal minus (CostPrice × Quantity) for the line total.
- **Quotation_Edit_Form**: The internal form used by business users to add or edit quotation line items.
- **Proposal_View**: The customer-facing rendered view of a quotation (snapshot, shared link, PDF).
- **Invoice_View**: The rendered invoice document generated from a converted quotation.

## Requirements

### Requirement 1: Database Schema Extension

**User Story:** As a developer, I want the QuotationLine table to include a CostPrice column, so that cost data can be persisted per line item.

#### Acceptance Criteria

1. THE Portal SHALL store a CostPrice column of type DECIMAL(18,2) NULL on the `[quotation].[QuotationLine]` table.
2. WHEN a QuotationLine record is inserted without a CostPrice value, THE Portal SHALL persist NULL for the CostPrice column.
3. WHEN a QuotationLine record is inserted with a CostPrice value, THE Portal SHALL persist the provided decimal value for the CostPrice column.

### Requirement 2: Entity and Repository Support

**User Story:** As a developer, I want the QuotationLine entity and repository to support CostPrice, so that the application layer can read and write cost data.

#### Acceptance Criteria

1. THE Portal SHALL include a nullable decimal CostPrice property on the QuotationLine entity.
2. WHEN a QuotationLine is retrieved from the database, THE QuotationLineRepository SHALL include the CostPrice column in the SELECT statement.
3. WHEN a QuotationLine is inserted, THE QuotationLineRepository SHALL include the CostPrice value in the INSERT statement using NULL-safe parameter handling.
4. WHEN a QuotationLine is updated, THE QuotationLineRepository SHALL include the CostPrice value in the UPDATE statement using NULL-safe parameter handling.

### Requirement 3: Service Layer Support

**User Story:** As a developer, I want the QuotationService to accept and pass through CostPrice values, so that the business logic layer supports cost tracking.

#### Acceptance Criteria

1. THE QuotationService AddLineAsync method SHALL accept an optional CostPrice parameter of type decimal?.
2. THE QuotationService UpdateLineAsync method SHALL accept an optional CostPrice parameter of type decimal?.
3. WHEN a CostPrice value is provided, THE QuotationService SHALL pass the value through to the QuotationLineRepository without modifying it.
4. WHEN a CostPrice value is provided, THE QuotationService SHALL validate that the value is zero or greater.
5. IF a negative CostPrice value is provided, THEN THE QuotationService SHALL reject the input with a descriptive error message.

### Requirement 4: Form Model and Edit View

**User Story:** As a business user, I want to optionally enter a cost price when adding or editing a quotation line item, so that I can track my purchase costs internally.

#### Acceptance Criteria

1. THE QuotationLineFormViewModel SHALL include a nullable CostPrice property.
2. WHEN the CostPrice field is displayed in the Quotation_Edit_Form, THE Portal SHALL render it as an optional input field.
3. WHEN a CostPrice value is submitted, THE Portal SHALL validate that the value is zero or greater.
4. WHEN the CostPrice field is left empty, THE Portal SHALL treat the value as NULL.

### Requirement 5: Internal Visibility Constraint

**User Story:** As a business owner, I want the cost price to remain strictly internal, so that customers never see my purchase costs.

#### Acceptance Criteria

1. THE Proposal_View SHALL exclude the CostPrice value from all rendered output.
2. THE Invoice_View SHALL exclude the CostPrice value from all rendered output.
3. WHEN a proposal is shared via a public link, THE Portal SHALL exclude the CostPrice value from the response.
4. THE Portal SHALL exclude the CostPrice value from any customer-facing API response or rendered page.

### Requirement 6: Margin Calculation

**User Story:** As a business user, I want to see profit margins per line item, so that I can understand my profitability on hardware and purchased items.

#### Acceptance Criteria

1. WHEN a QuotationLine has a non-null CostPrice, THE Portal SHALL calculate the unit margin as UnitPrice minus CostPrice.
2. WHEN a QuotationLine has a non-null CostPrice, THE Portal SHALL calculate the line margin as LineTotal minus (CostPrice multiplied by Quantity).
3. WHEN a QuotationLine has a null CostPrice, THE Portal SHALL not display any margin value for that line.
4. THE Portal SHALL display margin values only in the Quotation_Edit_Form and internal reporting views.
5. THE Portal SHALL exclude margin values from the Proposal_View and Invoice_View.
