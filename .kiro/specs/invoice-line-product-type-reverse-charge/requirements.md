# Requirements Document

## Introduction

This feature adds two classification properties to the sales pipeline (quotations and invoices):

1. **Product Type** — A lookup property (Services/Goods) stored on the Product master record and displayed read-only on quotation and invoice lines. This mirrors the ExpenseType concept on ExpenseCategory in the purchase module.
2. **Reverse Charge Flag** — A boolean flag on quotation and invoice lines indicating that the reverse charge mechanism applies, forcing the VAT rate to 0% on that line. This covers B2B EU cross-border services and other scenarios where the buyer accounts for VAT.

Both properties flow through the quotation-to-invoice conversion process and are persisted as immutable snapshots on invoice lines.

## Glossary

- **Portal_System**: The ASP.NET Core MVC web application serving as the back-office platform
- **Product**: A master catalog record representing a sellable item or service, scoped to a business tenant (schema: [product].Product)
- **ProductType**: A system-wide lookup table classifying whether a product is a Service or Goods (Services=1, Goods=2)
- **QuotationLine**: An individual priced item within a Quotation (schema: [quotation].QuotationLine)
- **InvoiceLine**: An individual priced item within an Invoice (schema: [invoice].InvoiceLine)
- **Reverse_Charge**: A VAT mechanism where the buyer (rather than the seller) accounts for VAT, resulting in 0% VAT on the seller's invoice
- **Product_Form**: The product creation and editing form in the Portal
- **Quotation_Form**: The quotation creation and editing form displaying quotation lines
- **Invoice_View**: The invoice detail view displaying invoice lines
- **Conversion_Service**: The service responsible for converting a Quotation into an Invoice, copying lines as immutable snapshots

## Requirements

### Requirement 1: Product Type Lookup Table

**User Story:** As a platform operator, I want a system-wide lookup table for product types, so that products can be consistently classified as Services or Goods.

#### Acceptance Criteria

1. THE Portal_System SHALL provide a ProductType lookup table in the [product] schema as [product].[ProductType] with columns: Id (INT, NOT NULL, Primary Key) and Name (NVARCHAR(50), NOT NULL), seeded with exactly two entries: Services (Id=1) and Goods (Id=2)
2. THE Portal_System SHALL enforce that the ProductType table accepts only manually seeded Id values (no IDENTITY) and rejects duplicate Id or Name values through unique constraints
3. IF a request attempts to insert, update, or delete rows in the [product].[ProductType] table outside of the seed migration, THEN THE Portal_System SHALL reject the operation (the table is a static reference lookup not modifiable at runtime)

### Requirement 2: Product Type Property on Product

**User Story:** As a business user, I want to assign a product type (Services or Goods) to each product, so that the classification flows automatically to quotation and invoice lines.

#### Acceptance Criteria

1. THE Portal_System SHALL store a nullable ProductTypeId foreign key on the Product table referencing the ProductType lookup, allowing NULL for products created before this feature
2. WHEN creating a new product, THE Product_Form SHALL display a Product Type dropdown populated with the values from the ProductType lookup (Services, Goods), require the user to select a value, and reject submission with a validation error indicating that Product Type is required if no value is selected
3. WHEN editing an existing product that has a ProductTypeId assigned, THE Product_Form SHALL pre-select the current Product Type value in the dropdown and allow the user to change the selection, persisting the updated value on save
4. WHEN a user opens a product for editing that has no ProductTypeId assigned (legacy data), THE Product_Form SHALL display the Product Type field as unset and allow saving without selecting a Product Type
5. WHEN a product's ProductTypeId is changed, THE Portal_System SHALL apply the updated Product Type to quotation lines referencing that product on next retrieval, without modifying already-persisted invoice lines

### Requirement 3: Product Type Display on Quotation Lines

**User Story:** As a business user, I want to see the product type on each quotation line, so that I know whether the line item is a service or goods without navigating to the product record.

#### Acceptance Criteria

1. WHEN a product is selected for a quotation line, THE Quotation_Form SHALL display the Product Type (Services or Goods) associated with that product as read-only text on the line within 1 second of the product selection
2. WHEN a quotation line has no ProductCode assigned (manual line item without a linked product), THE Quotation_Form SHALL display no Product Type indicator for that line
3. WHEN a quotation line references a product with no ProductTypeId assigned, THE Quotation_Form SHALL display no Product Type indicator for that line
4. WHEN the user changes the product selection on an existing quotation line, THE Quotation_Form SHALL update the displayed Product Type to reflect the newly selected product's current ProductTypeId
5. THE Quotation_Form SHALL NOT allow the user to edit the Product Type on the quotation line directly (the value is derived from the linked product record)

### Requirement 4: Product Type Display on Invoice Lines

**User Story:** As a business user, I want to see the product type on each invoice line, so that I can verify the classification of items on finalised invoices.

#### Acceptance Criteria

1. WHEN an invoice line has a ProductTypeId value stored from the quotation-to-invoice conversion, THE Invoice_View SHALL display the corresponding Product Type name (Services or Goods) as read-only text on that line
2. WHEN an invoice line has no ProductTypeId value stored (NULL), THE Invoice_View SHALL render no Product Type text for that line, leaving the product type position empty
3. THE Invoice_View SHALL NOT allow the user to edit the Product Type on the invoice line (the value is an immutable snapshot from conversion)
4. WHEN an invoice line does not reference a product (manual or free-text line), THE Invoice_View SHALL render no Product Type text for that line

### Requirement 5: Reverse Charge Flag on Quotation Lines

**User Story:** As a business user, I want to mark a quotation line as reverse charge, so that the VAT rate is forced to 0% for B2B EU cross-border services or other reverse charge scenarios.

#### Acceptance Criteria

1. THE Portal_System SHALL store an IsReverseCharge column (BIT, NOT NULL, DEFAULT 0) on the QuotationLine table
2. THE Quotation_Form SHALL display a Reverse Charge checkbox on each quotation line
3. WHEN the user enables the Reverse Charge flag on a quotation line, THE Quotation_Form SHALL immediately set the VatRate field to 0% on that line in the UI and persist IsReverseCharge as 1 when the quotation is saved
4. WHEN the user disables the Reverse Charge flag on a quotation line, THE Quotation_Form SHALL restore the VatRate to the line's previously held VatRate value before reverse charge was enabled (i.e., the product's DefaultVatRate if the line was populated from a product, or 0% if no prior rate existed) and persist IsReverseCharge as 0 when the quotation is saved
5. WHILE IsReverseCharge is enabled on a quotation line, THE Quotation_Form SHALL display the VatRate field as read-only with value 0%
6. WHILE IsReverseCharge is enabled on a quotation line, THE Portal_System SHALL reject any request that attempts to set a VatRate greater than 0% on that line
7. IF a quotation line has no associated product and the user disables the Reverse Charge flag, THEN THE Quotation_Form SHALL restore the VatRate to 0% for that line

### Requirement 6: Reverse Charge Flag on Invoice Lines

**User Story:** As a business user, I want the reverse charge flag to appear on invoice lines, so that finalised invoices correctly reflect the 0% VAT treatment.

#### Acceptance Criteria

1. THE Portal_System SHALL store an IsReverseCharge column (BIT, NOT NULL, DEFAULT 0) on the InvoiceLine table
2. WHEN an invoice line has IsReverseCharge set to 1, THE Invoice_View SHALL display a visible "Reverse Charge" label on that line
3. WHEN an invoice line has IsReverseCharge set to 0, THE Invoice_View SHALL NOT display a reverse charge label on that line
4. WHEN an invoice line has IsReverseCharge set to 1, THE Portal_System SHALL reject any update that sets the VatRate to a value greater than 0% and return a validation error indicating that reverse charge lines require 0% VAT
5. THE Invoice_View SHALL display the IsReverseCharge flag as read-only on invoice lines (the value is set during quotation-to-invoice conversion and cannot be modified)

### Requirement 7: Quotation to Invoice Conversion Preserves Reverse Charge

**User Story:** As a business user, I want the reverse charge flag to carry over when a quotation is converted to an invoice, so that the VAT treatment is preserved without manual re-entry.

#### Acceptance Criteria

1. WHEN a quotation is converted to an invoice, THE Conversion_Service SHALL copy the IsReverseCharge value from each QuotationLine to the corresponding InvoiceLine, producing one InvoiceLine for every QuotationLine in the source quotation
2. WHEN a quotation line has IsReverseCharge set to 1, THE Conversion_Service SHALL set the VatRate to 0% on the resulting invoice line regardless of the VatRate stored on the source quotation line
3. WHEN a quotation line has IsReverseCharge set to 0, THE Conversion_Service SHALL copy the VatRate from the quotation line to the invoice line without modification
4. IF the conversion fails after partially copying lines, THEN THE Conversion_Service SHALL roll back all changes so that neither the invoice nor any invoice lines are persisted

### Requirement 8: Data Integrity and Validation

**User Story:** As a platform operator, I want the system to enforce data integrity for the new classification properties, so that invalid combinations cannot be persisted.

#### Acceptance Criteria

1. IF a quotation line is submitted with IsReverseCharge set to 1 and a VatRate greater than 0, THEN THE Portal_System SHALL reject the submission within the same transaction, prevent persistence of the line, and return a validation error indicating that reverse charge lines require 0% VAT
2. IF an invoice line is submitted with IsReverseCharge set to 1 and a VatRate greater than 0, THEN THE Portal_System SHALL reject the submission within the same transaction, prevent persistence of the line, and return a validation error indicating that reverse charge lines require 0% VAT
3. THE Portal_System SHALL accept ProductTypeId values of NULL, 1, or 2 on the Product table and reject any other value with a foreign key constraint violation at the database level
4. THE Portal_System SHALL default IsReverseCharge to 0 for all existing quotation lines and invoice lines present at the time the migration executes, and the migration SHALL be idempotent (safe to run multiple times without altering already-migrated rows)
5. THE Portal_System SHALL enforce the IsReverseCharge and VatRate validation defined in criteria 1 and 2 at the server-side service layer before persisting data, ensuring the constraint is applied regardless of the entry point (form submission or quotation-to-invoice conversion)
