# Purchase Attachment Column — Design

## Table Layout Change

```
| DATE | INVOICE # | SUPPLIER | CATEGORY | FILE | AMT EXCL. VAT | VAT | TOTAL | ORIGIN | ACTIONS |
|------|-----------|----------|----------|------|---------------|-----|-------|--------|---------|
| ...  | INV-001   | Acme     | Services | 📎   | €100.00       | €19 | €119  | EU     | ...     |
| ...  | INV-002   | Beta     | Goods    | —    | €50.00        | €9  | €59   | Dom    | ...     |
```

## Data Flow

```
PurchaseController.Index()
    → Query attachments: SELECT EntityId, COUNT(*) FROM [document].[DocumentAttachment]
                         WHERE EntityType = 'Purchase' AND BusinessId = @BusinessId AND IsDeleted = 0
                         GROUP BY EntityId
    → Pass as Dictionary<int, int> via ViewBag.AttachmentCounts
    
View renders:
    → For each purchase row, check if AttachmentCounts[purchase.Id] > 0
    → If yes: render clickable paperclip icon with data-purchase-id attribute
    → If no: render "—"

Click handler:
    → AJAX GET /Attachment/GetByEntity?entityType=Purchase&entityId={id}
    → Returns JSON array of { id, fileName, fileSizeBytes, createdAtUtc }
    → Render in SweetAlert2 HTML modal with download links
```

## Modal Design

```
┌─────────────────────────────────────────┐
│ 📎 Attachments — INV-001 (Acme Ltd)     │
├─────────────────────────────────────────┤
│ ┌─────────────────────────────────────┐ │
│ │ 📄 invoice_scan.pdf                 │ │
│ │    128 KB · Uploaded 15 Jul 2026    │ │
│ │                        [Download]   │ │
│ └─────────────────────────────────────┘ │
│ ┌─────────────────────────────────────┐ │
│ │ 📄 receipt_photo.jpg                │ │
│ │    45 KB · Uploaded 15 Jul 2026     │ │
│ │                        [Download]   │ │
│ └─────────────────────────────────────┘ │
├─────────────────────────────────────────┤
│                              [Close]    │
└─────────────────────────────────────────┘
```

## Icon SVG

```html
<svg width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
    <path d="M21.44 11.05l-9.19 9.19a6 6 0 01-8.49-8.49l9.19-9.19a4 4 0 015.66 5.66l-9.2 9.19a2 2 0 01-2.83-2.83l8.49-8.48"/>
</svg>
```

## AJAX Endpoint

Either reuse existing `/Attachment/GetByEntity` or add a lightweight endpoint:

```csharp
[HttpGet]
public async Task<IActionResult> AxGetAttachments(string entityType, int entityId)
{
    var attachments = await _attachmentRepository.GetByEntityAsync(entityType, entityId, businessId);
    return Json(new { success = true, data = attachments.Select(a => new {
        a.Id, a.OriginalFileName, a.FileSizeBytes, a.CreatedAtUtc
    })});
}
```
