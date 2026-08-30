# External Platform Sales Export — Canonical Import Contract

**Audience:** Engineering teams (and Kiro agents) of platforms owned/operated by 3 Inventors Limited, or any third party that needs to feed sales into the Portal for VAT consolidation.

**Purpose:** Define the exact file your platform's export service must produce so the Portal can import your sales line-by-line into the correct VAT submission period. Build one export service that emits this file; the Portal operator imports it.

---

## 1. What you are producing

A single **CSV file** listing your sales invoices for a date range. **One row = one invoice.** The Portal imports each row as a line-level sales record, tags it to your platform, and assigns it to the VAT period that covers its invoice date.

Do **not** send summaries or period totals — send the individual invoices. Summaries are derived on the Portal side.

---

## 2. File format

| Property | Requirement |
|---|---|
| File type | CSV (`.csv`) |
| Encoding | UTF-8 (no BOM preferred; BOM tolerated) |
| Delimiter | Comma `,` (semicolon `;` tolerated) |
| First row | Header row with column names |
| Max size | 5 MB per file |
| Max rows | 1000 data rows per file (split larger ranges into multiple files) |
| Decimal separator | Dot `.` (e.g. `1234.56`) — do **not** use thousands separators |
| Date format | ISO `yyyy-MM-dd` (e.g. `2026-08-27`) |

---

## 3. Columns

### Required (file is rejected if any header is missing)

| Column | Type | Rules |
|---|---|---|
| `InvoiceNumber` | string | Must follow `{PlatformCode}-INV-{yyyy}-{NNNN}` (see §4) |
| `InvoiceDate` | date | `yyyy-MM-dd`. The Portal uses this to pick the VAT period |
| `NetAmount` | decimal | Amount excluding VAT. Must be ≥ 0 |
| `VatAmount` | decimal | VAT portion. Must be ≥ 0 |
| `TotalAmount` | decimal | Must equal `NetAmount + VatAmount`. If omitted or ≤ 0, the Portal recomputes it as `NetAmount + VatAmount` |

### Optional

| Column | Type | Rules |
|---|---|---|
| `VatRate` | decimal | The VAT rate applied, e.g. `19` or `0`. Informational |
| `CustomerName` | string | Free text. Not linked to Portal customers |
| `Description` | string | ≤ 500 chars |
| `PaymentMethod` | string | ≤ 50 chars (e.g. `card`, `bank_transfer`) |
| `Currency` | string | ISO 4217 (e.g. `EUR`). Informational this phase — send amounts in your reporting currency |

Column **order does not matter**. Header names are matched case-insensitively.

---

## 4. Invoice number format

Every invoice number must follow the same format the Portal itself uses:

```
{PlatformCode}-INV-{yyyy}-{NNNN}
```

- `{PlatformCode}` — your platform's short code, 1–10 alphanumeric characters (e.g. `GRD`, `MYC`). It must match the platform registered in the Portal. If it doesn't match, the Portal still imports the row but flags a **prefix mismatch warning** so the operator can review.
- `{yyyy}` — 4-digit year.
- `{NNNN}` — sequence number, zero-padded to at least 4 digits.

Examples: `GRD-INV-2026-0042`, `MYC-INV-2026-0007`.

---

## 5. VAT semantics

- `NetAmount + VatAmount = TotalAmount`. Keep these consistent per row; the Portal treats `VatAmount` as the Output VAT contribution for that invoice.
- **Zero-VAT / exempt / reverse-charge** invoices: send `VatAmount = 0` (and `VatRate = 0` if you include the column). Still include the row — it belongs in the sales figures even when no VAT is due.
- Send amounts in your platform's reporting currency. Multi-currency conversion is not handled yet; use one consistent currency.
- **Credit notes / refunds:** for this phase, send only positive sales. Do not send negative rows (they are treated as invalid). Handling of refunds/credit notes will be defined in a later revision of this contract.

---

## 6. De-duplication and idempotency

The Portal detects duplicates using **`InvoiceNumber` + `InvoiceDate`** (scoped to your platform). This means:

- Re-importing a file that overlaps a previously imported date range is **safe** — already-imported invoices are skipped.
- Your export **must be stable**: the same invoice must always export with the same `InvoiceNumber` and `InvoiceDate`. Do not renumber invoices between exports.
- Prefer exporting **finalized** invoices only. Avoid exporting drafts that might change number or date later.

---

## 7. Worked example

`guardian-sales-2026-08.csv`:

```csv
InvoiceNumber,InvoiceDate,NetAmount,VatAmount,TotalAmount,VatRate,CustomerName,PaymentMethod,Currency
GRD-INV-2026-0040,2026-08-01,100.00,19.00,119.00,19,Acme Ltd,card,EUR
GRD-INV-2026-0041,2026-08-03,250.00,47.50,297.50,19,Beta Kiosk,bank_transfer,EUR
GRD-INV-2026-0042,2026-08-05,80.00,0.00,80.00,0,Gamma NGO,card,EUR
GRD-INV-2026-0043,2026-08-19,540.00,102.60,642.60,19,Delta Group,card,EUR
```

Notes on the example:
- Row 3 is a zero-VAT sale — included with `VatAmount = 0`.
- All `TotalAmount` values equal `NetAmount + VatAmount`.
- All invoice numbers share the `GRD` platform code and ISO dates.

---

## 8. Operator-side steps (for reference — done in the Portal, not by you)

1. The operator registers your platform in the Portal (Name + `PlatformCode`).
2. The operator uploads your CSV and reviews a preview: valid rows, duplicates skipped, any prefix-mismatch warnings, and the VAT period each row will land in.
3. On confirm, each row becomes a line-level sales record tagged to your platform and assigned to the covering, **unsubmitted** VAT period. Rows whose date falls in an already-submitted period are imported as "Unassigned" and never alter a filed return.

---

## 9. Checklist for your export service

- [ ] Output is UTF-8 CSV with a header row.
- [ ] All five required columns present: `InvoiceNumber, InvoiceDate, NetAmount, VatAmount, TotalAmount`.
- [ ] Dates are `yyyy-MM-dd`; decimals use `.`; no thousands separators.
- [ ] Invoice numbers follow `{PlatformCode}-INV-{yyyy}-{NNNN}` with your registered code.
- [ ] `NetAmount + VatAmount = TotalAmount` on every row; amounts ≥ 0.
- [ ] Zero-VAT rows included with `VatAmount = 0`.
- [ ] Export is stable and idempotent (same invoice → same number + date every time).
- [ ] Files ≤ 5 MB and ≤ 1000 rows (split by date range if needed).
- [ ] Only finalized invoices exported; no negative/refund rows this phase.

---

*Contract version 1.0. Related Portal spec: `.kiro/specs/external-platform-sales-import/`. Changes to this contract will be versioned; coordinate with the Portal operator before changing column names or formats.*
