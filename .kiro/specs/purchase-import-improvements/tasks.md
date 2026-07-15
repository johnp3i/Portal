# Implementation Plan: Purchase Import — Improvements & Nice-to-Haves

## Overview

Follow-up improvements to the Purchase Import feature based on production usage. Covers auto-detection fallback, multi-section file handling, Send to Bulk Entry integration, import history, template testing, and category auto-creation.

## Tasks

- [x] 1. Auto-detection fallback on template failure
  - [x] 1.1 In ImportEngineService, when template parsing produces 0 rows, attempt auto-detection as fallback
  - [x] 1.2 Return results with an informational warning message if fallback was used
  - [x] 1.3 Display the warning in the Preview page header (e.g., amber banner)

- [x] 2. Multi-section file support (stop-at-section)
  - [x] 2.1 In ColumnMapper, after parsing each row, check if the row exactly matches the header row values
  - [x] 2.2 If a header-duplicate row is encountered, stop parsing (break the loop)
  - [x] 2.3 Test with the Meta CSV which has two identical "Date,Transaction ID,Amount,Currency" header rows

- [x] 3. Send to Bulk Entry
  - [x] 3.1 Add "Send to Bulk Entry" button to Preview page (styled as secondary action)
  - [x] 3.2 Create AxPostSendToBulkEntry endpoint that serializes session rows to TempData
  - [x] 3.3 Update Purchase BulkEntry controller to accept pre-populated rows from TempData
  - [x] 3.4 Delete the import session after transfer
  - [x] 3.5 Pre-populate the Bulk Entry form fields from transferred data

- [x] 4. Import history log
  - [x] 4.1 Add "Import History" section to the Import Purchases Index page
  - [x] 4.2 Query AuditLog for Action = 'PurchaseImport' entries scoped to business
  - [x] 4.3 Display: file name (from Details), date, row count, user name
  - [x] 4.4 Style as a compact table below the upload form

- [x] 5. Template preview/test
  - [x] 5.1 Add "Test with File" button to the template create/edit modal
  - [x] 5.2 Create AxPostTestTemplate endpoint that parses first 5 rows without persisting
  - [x] 5.3 Return parsed results as JSON
  - [x] 5.4 Display mini-preview table in the modal showing parsed field values
  - [x] 5.5 Show diagnostic info if 0 rows produced (header row found at position X, etc.)

- [x] 6. Expense category auto-create
  - [x] 6.1 Add "Create missing categories" button to Preview page (shown only when unresolved categories exist)
  - [x] 6.2 Create AxPostCreateMissingCategories endpoint that inserts new ExpenseCategory records
  - [x] 6.3 Collect unique unresolved category names from session rows
  - [x] 6.4 After creation, re-validate all affected rows and update session
  - [x] 6.5 Refresh the page to reflect resolved categories

## Notes

- These are independent improvements — each can be implemented and deployed separately
- Priority order: 2 (multi-section) > 1 (fallback) > 6 (auto-create) > 4 (history) > 3 (bulk entry) > 5 (template test)
- Multi-section support directly addresses the Meta CSV parsing issue where the second "Ad credit" section leaks rows
- Send to Bulk Entry is lower priority since the Preview page inline editing already covers most use cases
- Template test is a developer productivity feature — useful but not blocking users

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1"] },
    { "id": 1, "tasks": ["1.2", "1.3", "2.2", "2.3"] },
    { "id": 2, "tasks": ["4.1", "4.2", "4.3", "4.4"] },
    { "id": 3, "tasks": ["6.1", "6.2", "6.3", "6.4", "6.5"] },
    { "id": 4, "tasks": ["3.1", "3.2", "3.3", "3.4", "3.5"] },
    { "id": 5, "tasks": ["5.1", "5.2", "5.3", "5.4", "5.5"] }
  ]
}
```
