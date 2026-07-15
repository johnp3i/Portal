# Requirements: Purchase Import — Improvements & Nice-to-Haves

## Introduction

Follow-up improvements to the Purchase Import Automation feature based on real-world usage. These address UX friction points and robustness gaps identified during testing with Meta and DatabaseMart supplier files.

## Requirements

### Requirement 1: Auto-Detection Fallback on Template Failure

**User Story:** As a business user, when my parser template produces no results, I want the system to attempt auto-detection as a fallback so I can still see partial results instead of a hard failure.

#### Acceptance Criteria

1. WHEN a configured template produces 0 parsed rows, THE system SHALL attempt auto-detection on the same file as a fallback.
2. IF auto-detection produces results, THE system SHALL display them with an informational message: "Template '{name}' did not match. Results shown using auto-detection."
3. IF both template and auto-detection fail, THE system SHALL display the enhanced error message (already implemented).

---

### Requirement 2: Multi-Section File Support (Stop-at-Section)

**User Story:** As a business user importing Meta-style CSVs with multiple payment sections, I want the parser to stop reading when it encounters a repeated header row or section break, so that only the intended section is parsed.

#### Acceptance Criteria

1. THE ColumnMapper SHALL detect when a data row exactly matches the header row (same column values) and stop parsing at that point.
2. THE system SHALL treat a row matching the header as a section boundary, not a data row.
3. THIS behaviour SHALL apply automatically — no template configuration needed.

---

### Requirement 3: Send to Bulk Entry

**User Story:** As a business user, when imported rows need extensive per-row editing (different suppliers, different categories per row), I want to send parsed rows to the Bulk Entry form where I have full editing power.

#### Acceptance Criteria

1. THE Preview page SHALL include a "Send to Bulk Entry" button alongside Confirm Import.
2. WHEN clicked, THE system SHALL transfer all non-removed parsed rows to the Purchase Bulk Entry view with fields pre-populated.
3. THE transfer SHALL include: InvoiceDate, InvoiceNumber, Description, AmountExcludingVat, VatAmount, TotalAmount, Country, Notes.
4. THE import session SHALL be deleted after transfer (same as confirmation).
5. THE Bulk Entry view SHALL accept pre-populated data via a temporary mechanism (session storage, TempData, or query parameter with session ID).

---

### Requirement 4: Import History Log

**User Story:** As a business user, I want to see a history of past imports (file name, date, row count, who imported) so I can track what was imported and when.

#### Acceptance Criteria

1. THE Import Purchases page SHALL include an "Import History" section (or tab) showing past successful imports.
2. EACH history entry SHALL display: file name, import date, row count, total amount, imported by.
3. THE history SHALL be read-only — no re-import or undo functionality.
4. THE history data SHALL come from the existing AuditLog entries (Action = 'PurchaseImport').

---

### Requirement 5: Template Preview/Test

**User Story:** As a business user creating a new parser template, I want to test it against a file before saving, so I can verify the mappings are correct without creating a full import session.

#### Acceptance Criteria

1. THE Template creation/edit modal SHALL include a "Test with File" button.
2. WHEN clicked, THE system SHALL accept a file upload and parse the first 5 rows using the unsaved template configuration.
3. THE result SHALL display in a mini-preview table showing the parsed values (or parse errors).
4. THE test SHALL NOT create an import session or persist any data.
5. IF the test produces 0 rows, THE system SHALL show a diagnostic message indicating which header row was tried and what was found.

---

### Requirement 6: Expense Category Auto-Create on Import

**User Story:** As a business user, when my CSV contains a category name that doesn't exist yet, I want the option to auto-create it during import rather than having to create it separately first.

#### Acceptance Criteria

1. WHEN a category name is not found during validation, THE system SHALL flag it as a warning (current behaviour).
2. THE Preview page SHALL offer a "Create missing categories" action that creates all unresolved category names as new ExpenseCategory records.
3. AFTER creation, THE system SHALL re-validate affected rows (they should now resolve to the newly created IDs).
4. THE auto-create SHALL only work for the current business's categories.
