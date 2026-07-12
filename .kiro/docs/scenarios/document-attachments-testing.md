# Document Attachments — Testing Scenarios

## Prerequisites

1. Run migration `115_CreateDocumentAttachmentTable.sql` against your Portal database
2. Run seed `Seed_PlanFeature_Attachments.sql` to grant the module to Professional/Enterprise plans
3. Ensure the `wwwroot/uploads/` directory is writable
4. Log in as a Professional or Enterprise plan user

---

## Scenario 1: Upload (Happy Path)

1. Navigate to any detail page (e.g., Purchase Edit, Invoice Detail)
2. The attachment panel should appear with "No attachments yet" empty state and "0 of 5" badge
3. Click "browse" or drag a valid PDF (< 5 MB) into the drop zone
4. **Expected:** BlockUI spinner → success SweetAlert → page reloads → file appears in list with PDF icon, filename, size, date, your display name
5. Repeat with a PNG image — expect thumbnail preview instead of icon

---

## Scenario 2: Upload Rejections

| Action | Expected Error |
|--------|---------------|
| Upload a .exe file | "File type not allowed. Accepted: PDF, PNG, JPG, WEBP." |
| Upload a 6 MB file | "File size exceeds the maximum of 5 MB." |
| Rename a .txt file to .pdf and upload | "File content does not match the declared file type." (magic-byte check) |
| Upload when 5 files already attached | "Maximum of 5 attachments per record reached." |

Each rejection should show BlockUI → unblock → SweetAlert2 error with the specific message.

---

## Scenario 3: Download

1. Click the download button (arrow icon) on an existing attachment
2. **Expected:** Browser downloads the file with its original filename
3. Open the downloaded file — it should be intact and readable

---

## Scenario 4: Delete (Owner)

1. Upload a file yourself
2. Click the delete button (trash icon) on that file
3. **Expected:** SweetAlert2 confirmation dialog ("Delete attachment? This action cannot be undone.")
4. Click "Yes, delete it"
5. **Expected:** BlockUI → success SweetAlert → page reloads → file gone from list → count badge decremented
6. Verify the physical file still exists on disk (`wwwroot/uploads/{businessId}/...`) — soft-delete only removes metadata visibility

---

## Scenario 5: Delete (Authorization)

1. Log in as User A, upload a file on an Invoice
2. Log in as User B (same business, not Owner role)
3. Navigate to the same Invoice — User B should see the file but NOT see a delete button (since `IsOwnedByCurrentUser` is false)
4. Log in as the business Owner — they should see the delete button on all attachments regardless of uploader

---

## Scenario 6: Tenant Isolation

1. Log in as Business A user, upload a file on Purchase #10
2. Log in as Business B user
3. Navigate to their own Purchase list — no paperclip badge from Business A's files
4. Manually craft a URL to `/Attachment/AxGetDownload?id={Business A's attachment ID}`
5. **Expected:** 404 response (not found, no file served)

---

## Scenario 7: Lightbox (Image Preview)

1. Upload a PNG or JPG image
2. Click the thumbnail in the attachment list
3. **Expected:** Full-screen dark overlay with the image centered, close button (top-right), download button
4. On mobile/touch: pinch-to-zoom should work
5. Press Escape or click the close button — overlay dismisses
6. Click outside the image — overlay dismisses

---

## Scenario 8: Plan Gating (Soft Gate)

1. Log in as a Starter plan user (no `attachments` module in their PlanFeature records)
2. Navigate to any detail page (e.g., Invoice Detail)
3. **Expected:** Instead of the full attachment panel, see the soft-gate teaser: "Document Attachments — Available on the Professional plan and above" with a "View Plans" link
4. The upload zone and attachment list should NOT render

---

## Scenario 9: List View Count Indicators

1. Upload 2 files to Purchase #5 and 3 files to Purchase #8
2. Navigate to Purchase Index list
3. **Expected:** Purchase #5 row shows a paperclip badge with "2", Purchase #8 shows "3"
4. Other purchases with no attachments show nothing (no badge at all)
5. Delete one file from Purchase #5 → refresh list → badge now shows "1"

---

## Scenario 10: Multiple Entity Types

1. Upload files to a Purchase, an Invoice, a Supplier, and a Customer
2. Navigate to each detail page — each should show only its own attachments
3. Verify the 5-attachment limit is independent per entity (filling up a Supplier doesn't affect its linked Purchases)

---

## Scenario 11: Mobile Upload

1. Open a detail page on a mobile device (or Chrome DevTools mobile emulation)
2. Tap the upload area — device should offer camera capture option (due to `capture="environment"` attribute)
3. Take a photo or pick from gallery
4. Upload should succeed as normal
5. Attachment cards should stack vertically at < 576px viewport

---

## Quick Smoke Test (5 minutes)

If you only have time for one pass:

1. Navigate to Purchase Edit → verify panel renders
2. Upload a PDF → success
3. Upload a PNG → success with thumbnail
4. Download the PDF → file intact
5. Delete the PNG → confirmation → removed
6. Check Purchase list → paperclip badge shows "1"
