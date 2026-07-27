# Purchase Attachment Column — Requirements

## Overview

Add a "File" column to the Purchases table that shows a paperclip/staple icon when a purchase has attached documents. Clicking the icon opens a modal listing the files with download links — same pattern as the Z-Reports attachment viewer.

## Requirements

### R1: File Column in Purchases Table

- Add a "File" column to the purchases list table (between existing columns — suggest after "Category" or before "Actions")
- Column header: "File" or a paperclip icon header
- Cell content:
  - If purchase has attachment(s): show a paperclip icon (clickable)
  - If no attachments: show "—" or leave empty

### R2: Attachment Count Data

- The PurchaseController Index action must load attachment counts per purchase
- Query `[document].[DocumentAttachment]` for `EntityType = 'Purchase'` grouped by `EntityId`
- Pass as a dictionary (purchaseId → count) to the view via ViewBag or model

### R3: Click → Modal with File List

- Clicking the paperclip icon opens a modal (SweetAlert2 or custom overlay)
- Modal shows:
  - Title: "Attachments — {Purchase Description or Invoice #}"
  - List of files with: filename, file size, upload date, download link
  - Close button
- Download link: `/Attachment/Download/{attachmentId}`

### R4: Visual Consistency

- Use the same paperclip SVG icon used elsewhere in the app (e.g., the Attachments sidebar nav icon)
- Icon should be muted when present but not distracting — subtle grey, becomes blue on hover
- Match the Z-Report attachment viewer pattern for the modal content

## Files to Modify

| File | Change |
|------|--------|
| `Portal.Web/Controllers/PurchaseController.cs` | Load attachment counts in Index action |
| `Portal.Web/Views/Purchase/Index.cshtml` | Add "File" column header + cell with icon + modal JS |
| `Portal.Infrastructure/Repositories/DocumentAttachmentRepository.cs` | Add `GetCountsByEntityTypeAsync(string entityType, int businessId)` if not exists |

## Reference Implementation

- Z-Report attachment viewer in `Portal.Web/Views/ZReport/Index.cshtml`
- Existing `AttachmentPanelViewComponent` for how attachments are loaded and displayed

## Out of Scope

- Uploading attachments from the list page (upload is done on the Purchase Edit/Detail page)
- Inline preview (PDF viewer, image preview) — just download links
