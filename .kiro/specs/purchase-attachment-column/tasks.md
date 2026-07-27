# Purchase Attachment Column — Tasks

## Task 1: Add attachment count query to PurchaseController

- In the `Index` action, query `DocumentAttachment` for all Purchase entities of this business
- Group by `EntityId`, return as `Dictionary<int, int>` (purchaseId → count)
- Pass via `ViewBag.AttachmentCounts`

## Task 2: Add AJAX endpoint for attachment list

- Add `AxGetAttachments(string entityType, int entityId)` to the `AttachmentController` (or PurchaseController)
- Returns JSON with file list: id, originalFileName, fileSizeBytes, createdAtUtc
- Validate businessId for tenant isolation

## Task 3: Add "File" column to Purchase table

- Add `<th>File</th>` column header in the table
- For each row: check `AttachmentCounts[purchase.Id]` > 0
- If yes: render clickable paperclip icon with `onclick="showAttachments(@purchase.Id, '@purchase.InvoiceNumber')"` 
- If no: render `<span style="color:#8a9bab;">—</span>`

## Task 4: Add modal JS

- `showAttachments(purchaseId, reference)` function
- BlockUI → fetch attachments → BlockUI.hide()
- Render in SweetAlert2 HTML modal: file list with filename, size, date, download link
- Download link: `/Attachment/Download/{id}`

## Task 5: Test

- Purchase with attachments: icon shows, click opens modal with correct files
- Purchase without attachments: shows "—"
- Download link works
- Modal closes cleanly
