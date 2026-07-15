# Implementation Plan: Payment Receipts & Signature Management

## Overview

This plan implements payment receipt generation with digital signature support. Receipts are formal documents issued when payments are received, covering single or multiple invoices. Signatures are business-level assets with permission-controlled usage.

## Tasks

- [ ] 1. Database migrations
  - [ ] 1.1 Create migration `116_CreateSignatureTable.sql`
    - Create `[portal].[Signature]` table with all columns, FK to Business, indexes
    - _Requirements: 7.1–7.6_

  - [ ] 1.2 Create migration `117_CreatePaymentReceiptTables.sql`
    - Create `[revenue].[PaymentReceipt]`, `[revenue].[PaymentReceiptLine]`, `[revenue].[PaymentReceiptShare]`
    - Add FKs, indexes on BusinessId, CustomerId, PaymentId
    - _Requirements: 1.1–1.4_

  - [ ] 1.3 Create migration `118_AddIsAutoReceiptEnabledToBusiness.sql`
    - ALTER `[portal].[Business]` ADD `IsAutoReceiptEnabled` BIT NOT NULL DEFAULT 0
    - _Requirements: 4.1_

- [ ] 2. Entity and model layer
  - [ ] 2.1 Create `Signature.cs` entity
  - [ ] 2.2 Create `PaymentReceipt.cs` entity
  - [ ] 2.3 Create `PaymentReceiptLine.cs` entity
  - [ ] 2.4 Create `PaymentReceiptShare.cs` entity
  - [ ] 2.5 Update `Business.cs` entity — add `IsAutoReceiptEnabled` property
  - [ ] 2.6 Register entities in PortalDbContext with table/schema configuration
  - [ ] 2.7 Create DTOs: `GenerateReceiptRequest`, `ReceiptViewModel`, `ReceiptLineViewModel`, `SignatureViewModel`
  - _Requirements: 1.1–1.4, 4.1, 7.1_

- [ ] 3. Checkpoint — Verify entities compile
  - Run `dotnet build`

- [ ] 4. Repository layer
  - [ ] 4.1 Create `SignatureRepository.cs` — CRUD, GetByBusinessIdAsync, GetDefaultAsync, SetDefaultAsync
  - [ ] 4.2 Create `PaymentReceiptRepository.cs` — Insert, GetById, GetByPaymentId, GetByBusinessId (paged), Void, GetNextReceiptNumber
  - [ ] 4.3 Create `PaymentReceiptLineRepository.cs` — BulkInsert, GetByReceiptId
  - [ ] 4.4 Create `PaymentReceiptShareRepository.cs` — Insert, GetByToken, Deactivate
  - _Requirements: 1.1–1.4, 2.1–2.4, 7.1_

- [ ] 5. Service layer
  - [ ] 5.1 Create `ISignatureService` / `SignatureService`
    - UploadAsync, GetAllForBusinessAsync, GetDefaultAsync, SetDefaultAsync, DeactivateAsync, ReactivateAsync
    - File storage handling (save to /uploads/signatures/{businessId}/)
    - _Requirements: 7.1–7.7_

  - [ ] 5.2 Create `IPaymentReceiptService` / `PaymentReceiptService`
    - GenerateReceiptAsync(paymentId, businessId, userId, signatureId?)
    - For per-invoice payments: single line item
    - For global payments (has children): multiple line items from child allocations
    - Compute outstanding balances at time of generation
    - Generate receipt number atomically
    - VoidReceiptAsync, GetReceiptAsync, GetReceiptsPagedAsync
    - _Requirements: 1.1–1.4, 2.1–2.4, 3.1–3.5, 5.1–5.5, 10.1–10.5, 12.1–12.5_

  - [ ] 5.3 Create `IReceiptRenderer` / `ReceiptRenderer`
    - RenderAsync(receiptId, businessId) → HTML string
    - Uses ViewRenderService with Views/Receipt/Snapshot.cshtml
    - _Requirements: 5.1–5.5_

  - [ ] 5.4 Update `PaymentService` — after recording, check IsAutoReceiptEnabled and auto-generate
    - _Requirements: 4.2–4.5_

  - [ ] 5.5 Update `PaymentService.VoidPaymentAsync` — cascade void to associated receipt
    - _Requirements: 10.1–10.2_

- [ ] 6. Checkpoint — Verify service layer compiles
  - Run `dotnet build`

- [ ] 7. Controller layer
  - [ ] 7.1 Create `ReceiptController.cs`
    - Index (list), Detail (view), AxPostGenerate, AxGetDownloadPdf, AxPostVoid, AxPostShare
    - _Requirements: 3.5, 6.1–6.6, 11.1–11.5_

  - [ ] 7.2 Create `SignatureController.cs` (or add to MyBusinessController)
    - Upload, List, SetDefault, Deactivate, Reactivate, GetImage
    - Permission checks: signature_manage for CRUD, signature_use for selection
    - _Requirements: 7.1–7.7, 8.1–8.6_

  - [ ] 7.3 Create `ReceiptViewController.cs` (public, AllowAnonymous)
    - Token-based receipt viewing and PDF download for shared receipts
    - _Requirements: 6.1–6.6_

  - [ ] 7.4 Update `RevenueController` — add "Generate Receipt" action link in payment history
    - _Requirements: 3.5_

- [ ] 8. Checkpoint — Verify controllers compile
  - Run `dotnet build`

- [ ] 9. Permission setup
  - [ ] 9.1 Add `signature_manage` and `signature_use` permission keys
    - Add to PortalModules or a new PermissionKeys constant class
    - Add to UserBusinessPermission seeding for owners
    - _Requirements: 8.1–8.6_

- [ ] 10. Views
  - [ ] 10.1 Create `Views/Receipt/Index.cshtml` — receipt list with filters
  - [ ] 10.2 Create `Views/Receipt/Detail.cshtml` — receipt detail with actions
  - [ ] 10.3 Create `Views/Receipt/Snapshot.cshtml` — printable receipt layout (for PDF and sharing)
  - [ ] 10.4 Create receipt share page `Views/ReceiptView/Index.cshtml` (public)
  - [ ] 10.5 Create `Views/MyBusiness/Signatures.cshtml` (or partial) — signature management UI
  - [ ] 10.6 Add "Generate Receipt" button to Invoice Detail payment history rows
  - [ ] 10.7 Add "Generate Receipt" option to Revenue Dashboard recent payments
  - [ ] 10.8 Add "Record Payment" flow on Statement with receipt generation option
  - [ ] 10.9 Add "Auto-Receipt" toggle to My Business settings page
  - _Requirements: 3.5, 4.4, 5.1–5.5, 6.1–6.6, 7.7, 9.1–9.5, 11.1–11.5_

- [ ] 11. Navigation
  - [ ] 11.1 Add "Receipts" navigation item under Finance/Revenue section
  - [ ] 11.2 Add "Signatures" to My Business or Account section
  - _Requirements: 11.5_

- [ ] 12. Checkpoint — Full integration test
  - Record a payment → verify receipt auto-generated (if enabled)
  - Manual generate → verify receipt created with correct line items
  - Share receipt → verify public access works
  - Void payment → verify receipt voided
  - Upload signature → apply to receipt → verify embedded in PDF

- [ ] 13. Property-based tests
  - [ ]* 13.1 Receipt number uniqueness and sequentiality
  - [ ]* 13.2 Receipt total = sum of line amounts
  - [ ]* 13.3 Void cascade completeness (payment void → receipt void)
  - [ ]* 13.4 Signature permission enforcement
  - [ ]* 13.5 Multi-invoice receipt line consistency with global payment children

- [ ] 14. Final checkpoint
  - Run `dotnet test` and `dotnet build`

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"], "description": "Database migrations" },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3", "2.4", "2.5", "2.6", "2.7"], "description": "Entities and models" },
    { "id": 2, "tasks": ["3"], "description": "Checkpoint: compile" },
    { "id": 3, "tasks": ["4.1", "4.2", "4.3", "4.4"], "description": "Repository layer" },
    { "id": 4, "tasks": ["5.1", "5.2", "5.3", "5.4", "5.5"], "description": "Service layer" },
    { "id": 5, "tasks": ["6"], "description": "Checkpoint: compile" },
    { "id": 6, "tasks": ["7.1", "7.2", "7.3", "7.4", "9.1"], "description": "Controllers + permissions" },
    { "id": 7, "tasks": ["8"], "description": "Checkpoint: compile" },
    { "id": 8, "tasks": ["10.1", "10.2", "10.3", "10.4", "10.5", "10.6", "10.7", "10.8", "10.9", "11.1", "11.2"], "description": "Views + navigation" },
    { "id": 9, "tasks": ["12"], "description": "Checkpoint: integration" },
    { "id": 10, "tasks": ["13.1", "13.2", "13.3", "13.4", "13.5"], "description": "Property tests" },
    { "id": 11, "tasks": ["14"], "description": "Final checkpoint" }
  ]
}
```

## Notes

- Receipt numbering uses the same pattern as invoices (REC-{BusinessId}-{Sequence})
- Signature files stored under /uploads/signatures/{businessId}/ — never served directly, always through a controller that validates ownership
- Auto-receipt integrates into existing PaymentService after successful recording
- The PaymentReceiptShare follows the exact same pattern as InvoiceShare (token, snapshot HTML, expiry)
- Void cascade: Payment void → receipt void → share link deactivation
- Foundation tier — no PlanFeature gating needed
- Permission keys are NOT module keys — they're fine-grained permissions within the existing UserBusinessPermission system
