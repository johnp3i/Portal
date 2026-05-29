# Requirements Document

## Introduction

This feature enhances the purchase classification system in the Portal by introducing three new classification dimensions:

1. A new "EU Paid" origin type for purchases from EU suppliers where VAT was actually paid (distinct from EU Reverse Charge where VAT is zero).
2. An "Expense Type" property on Expense Categories to distinguish between Services and Goods.
3. A "Purchase Type" property on purchases to classify them as Asset, Stock, or Expense.

These changes apply to both the single purchase creation form and the bulk entry form.

## Glossary

- **Portal_System**: The ASP.NET Core MVC web application serving as the back-office platform
- **Purchase_Form**: The single purchase creation form at `/Purchase/Create`
- **Bulk_Entry_Form**: The bulk purchase entry form at `/Purchase/BulkEntry`
- **PurchaseOriginType**: A system-wide lookup table classifying the geographic origin of a purchase (currently: Domestic, EuReverseCharge, NonEu)
- **Purchase_List**: The purchase index page at `/Purchase` displaying a filtered, paginated table of all purchases
- **CSV_Import**: The CSV import flow at `/Purchase/CsvImport` that parses uploaded CSV files into purchase rows
- **ExpenseCategory**: A business-scoped classification for purchase entries (e.g. Office Supplies, Marketing)
- **ExpenseType**: A system-wide lookup table classifying whether an expense category relates to Services or Goods
- **PurchaseType**: A system-wide lookup table classifying whether a purchase is an Asset, Stock, or Expense
- **EU_Paid**: A new origin type representing purchases from EU suppliers where VAT was charged and paid
- **Asset**: A purchase type indicating the purchase is a capital asset (long-term value)
- **Stock**: A purchase type indicating the purchase is inventory intended for resale
- **Expense**: A purchase type indicating the purchase is a routine business expense

## Requirements

### Requirement 1: Add EU Paid Origin Type

**User Story:** As a business user, I want to classify purchases from EU suppliers where VAT was paid, so that I can distinguish them from EU Reverse Charge purchases where VAT is zero.

#### Acceptance Criteria

1. THE Portal_System SHALL provide a PurchaseOriginType entry with Id=4 and Name="EuPaid"
2. WHEN the user selects the "EU Paid" origin type, THE Purchase_Form SHALL enable the VAT Amount field for manual entry with a minimum value of 0.00
3. WHEN the user selects the "EU Paid" origin type, THE Purchase_Form SHALL display the Country field as mandatory and prevent form submission if Country is empty
4. WHEN the user selects the "EU Paid" origin type, THE Bulk_Entry_Form SHALL enable the VAT Amount column for manual entry with a minimum value of 0.00
5. WHEN the user selects the "EU Paid" origin type, THE Bulk_Entry_Form SHALL require a Country value and reject the row with a validation error if Country is empty
6. WHEN a purchase is saved with origin type "EU Paid", THE Portal_System SHALL persist the PurchaseOriginTypeId as 4
7. THE Purchase_Form SHALL display the "EU Paid" origin type with the label "EU Paid" in the origin type selection alongside Domestic, EU Reverse Charge, and Non-EU options
8. WHEN a purchase has origin type "EU Paid", THE Portal_System SHALL include its VatAmount in input VAT calculations for the associated VAT submission period
9. IF a purchase is submitted via CSV import with origin type value "EuPaid" (case-insensitive), THEN THE Portal_System SHALL resolve it to PurchaseOriginTypeId 4

### Requirement 2: Add Expense Type Property to Expense Categories

**User Story:** As a business user, I want to classify my expense categories as either Services or Goods, so that I can better categorise my spending for reporting and VAT purposes.

#### Acceptance Criteria

1. THE Portal_System SHALL provide an ExpenseType lookup table with two entries: Services (Id=1) and Goods (Id=2)
2. THE Portal_System SHALL store a nullable ExpenseTypeId foreign key on the ExpenseCategory table referencing the ExpenseType lookup, allowing NULL for expense categories created before this feature
3. WHEN creating a new expense category, THE Portal_System SHALL require the user to select an Expense Type (Services or Goods) and reject submission with a validation error if no Expense Type is selected
4. WHEN editing an existing expense category, THE Portal_System SHALL allow the user to change the Expense Type selection and persist the updated value on save
5. WHEN the user selects an expense category on the Purchase_Form, THE Purchase_Form SHALL display the Expense Type (Services or Goods) associated with that category as read-only text, or display no Expense Type indicator if the category has no ExpenseTypeId assigned
6. WHEN the user selects an expense category on the Bulk_Entry_Form, THE Bulk_Entry_Form SHALL display the Expense Type (Services or Goods) associated with that category as read-only text, or display no Expense Type indicator if the category has no ExpenseTypeId assigned
7. WHEN a user opens an expense category for editing that has no ExpenseTypeId assigned (legacy data), THE Portal_System SHALL display the Expense Type field as unset and require the user to select an Expense Type before saving, rejecting submission with a validation error if it remains unselected

### Requirement 3: Add Purchase Type Classification

**User Story:** As a business user, I want to classify each purchase as an Asset, Stock, or Expense, so that I can track whether a purchase is a capital asset, inventory for resale, or a routine expense.

#### Acceptance Criteria

1. THE Portal_System SHALL provide a PurchaseType lookup table with three entries: Asset (Id=1), Stock (Id=2), Expense (Id=3)
2. THE Portal_System SHALL store a PurchaseTypeId column (NOT NULL) on the Purchase table referencing the PurchaseType lookup
3. WHEN creating a purchase via the Purchase_Form, THE Portal_System SHALL require the user to select a Purchase Type (Asset, Stock, or Expense) with Expense pre-selected as the default
4. WHEN creating a purchase via the Bulk_Entry_Form, THE Portal_System SHALL require the user to select a Purchase Type for each row with Expense pre-selected as the default
5. THE Purchase_Form SHALL present the Purchase Type options as a radio button group
6. THE Bulk_Entry_Form SHALL present the Purchase Type options as a dropdown select column
7. IF a purchase is submitted without a PurchaseTypeId, THEN THE Portal_System SHALL reject the submission, prevent persistence, and display a validation error indicating that Purchase Type selection is required
8. THE Portal_System SHALL default the PurchaseTypeId to Expense (Id=3) for existing purchases that predate this feature (data migration)
9. WHEN editing an existing purchase, THE Portal_System SHALL display the current Purchase Type selection and allow the user to change it

### Requirement 4: Form Consistency Between Single and Bulk Entry

**User Story:** As a business user, I want both the single purchase form and the bulk entry form to offer the same classification options, so that my data is consistent regardless of which entry method I use.

#### Acceptance Criteria

1. THE Purchase_Form SHALL display all four origin type options in the following order: Domestic, EU Reverse Charge, EU Paid, and Non-EU
2. THE Bulk_Entry_Form SHALL display all four origin type options in the Origin Type dropdown in the following order: Domestic, EU RC, EU Paid, and Non-EU
3. THE Purchase_Form SHALL include a Purchase Type selection field presenting three options: Asset, Stock, and Expense
4. THE Bulk_Entry_Form SHALL include a Purchase Type dropdown column presenting three options: Asset, Stock, and Expense
5. WHEN the same purchase data is entered via the Purchase_Form or the Bulk_Entry_Form, THE Portal_System SHALL produce database records with matching values for all user-entered fields: InvoiceDate, InvoiceNumber, SupplierId, ExpenseCategoryId, Description, AmountExcludingVat, VatAmount, TotalAmount, PurchaseOriginTypeId, PurchaseTypeId, and Country
6. THE Purchase_Form and THE Bulk_Entry_Form SHALL both display the Expense Type associated with the selected expense category as read-only context information

### Requirement 5: Purchase List Display and Filtering

**User Story:** As a business user, I want to see the new classification fields in the purchase list and filter by them, so that I can quickly find and review purchases by type and origin.

#### Acceptance Criteria

1. THE Purchase_List SHALL display a pill badge for "EU Paid" origin type purchases (styled distinctly from EU RC and Non-EU badges)
2. THE Purchase_List origin type filter dropdown SHALL include the "EU Paid" option alongside Domestic, EU Reverse Charge, and Non-EU
3. THE Purchase_List SHALL display the Purchase Type (Asset, Stock, or Expense) as a column or badge for each purchase row
4. THE Purchase_List SHALL provide a Purchase Type filter dropdown allowing users to filter by Asset, Stock, or Expense
5. THE Purchase_List batch summary section SHALL include an "EU Paid" count alongside the existing Domestic, EU RC, and Non-EU counts in the Bulk_Entry_Form summary

### Requirement 6: Service Layer and Validation Updates

**User Story:** As a platform operator, I want the service layer to correctly handle the new origin type and purchase type, so that data integrity is maintained across all entry methods.

#### Acceptance Criteria

1. THE Portal_System SHALL accept PurchaseOriginTypeId values 1 through 4 (Domestic, EuReverseCharge, NonEu, EuPaid) during purchase validation
2. WHEN a purchase has origin type "EU Paid" (Id=4), THE Portal_System SHALL compute TotalAmount as AmountExcludingVat + VatAmount (same as Domestic and Non-EU)
3. WHEN a purchase has origin type "EU Paid" (Id=4), THE Portal_System SHALL require a non-empty Country value and reject the submission if Country is missing
4. THE Portal_System SHALL validate that PurchaseTypeId is a value between 1 and 3 (Asset, Stock, Expense) on every purchase creation and update
5. THE CSV_Import origin type resolver SHALL accept "EuPaid", "eu paid", and "eupaid" (case-insensitive) and map them to PurchaseOriginTypeId 4
6. THE CSV_Import SHALL support a PurchaseType column (column 11) accepting values "Asset", "Stock", or "Expense" (case-insensitive), defaulting to "Expense" if the column is absent or empty

### Requirement 7: Expense Category Management UI

**User Story:** As a business user, I want to assign an Expense Type when creating expense categories inline from the purchase form, so that all new categories are properly classified.

#### Acceptance Criteria

1. WHEN creating an expense category inline from the Purchase_Form autocomplete, THE Portal_System SHALL prompt the user to select an Expense Type (Services or Goods) as part of the inline creation flow
2. WHEN creating an expense category inline from the Bulk_Entry_Form autocomplete, THE Portal_System SHALL prompt the user to select an Expense Type (Services or Goods) as part of the inline creation flow
3. THE Portal_System SHALL reject inline expense category creation if no Expense Type is selected and display a validation error

