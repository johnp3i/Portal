# Proposal / Quotation Structure

A reference describing exactly how a proposal (quotation) is modelled in the Portal
platform, so that a well-formed proposal can be authored and later turned into a database
seed file. Every field name, type, allowed value, and computed total below matches the
application code and database schema.

All quotation tables live in SQL schema `[quotation]`. Product tables live in `[product]`.

---

## 1. The big picture

A proposal is a **Quotation header** with:

- an ordered list of **Sections** (optional — lines can also sit in the implicit "General" group),
- an ordered list of **Lines** (the priced items), each optionally belonging to a section,
- an optional **Prepared By** contact (the person who authored it),
- optionally one special **adjustment line** representing a quotation-level ("bulk") discount,
- three **computed money totals** (Subtotal, Tax, Total) that are derived from the lines, not entered by hand.

```
Quotation (header)
├── QuotationContact        (optional "Prepared By")
├── ProposalSection[]       (optional named groups, ordered)
│     └── (lines reference a section by id, or null = General)
└── QuotationLine[]         (priced items, ordered)
      ├── normal lines      (products / services)
      └── adjustment line   (0 or 1; the bulk discount)
```

Key rule: a quotation is only editable while it is in **Draft** status.

---

## 2. Quotation header

Table: `[quotation].[Quotation]`

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| Id | int | yes (PK) | identity |
| BusinessId | int | yes | tenant owner (FK → Business) |
| CustomerId | int | yes | recipient (FK → Customer) |
| QuotationStatusTypeId | int | yes | see status table below; new = 1 (Draft) |
| Reference | string(100) | yes | human-facing quote number, format below |
| ValidUntil | date | no | expiry date |
| Subtotal | decimal(18,2) | yes | **computed** (see §7) |
| TaxAmount | decimal(18,2) | yes | **computed** (see §7) |
| TotalAmount | decimal(18,2) | yes | **computed** (see §7) |
| Notes | string | no | free text shown on the proposal |
| QuotationContactId | int | no | FK → QuotationContact ("Prepared By") |
| IsGrandTotalShown | bool | yes | default `true`; controls whether the grand-total card renders |
| LeadRequestId | int | no | optional provenance link to the originating sales lead |
| IsDeleted | bool | yes | soft-delete flag |
| CreatedAtUtc / UpdatedAtUtc | datetime | yes | default `GETUTCDATE()` |

**Reference format:** `QUO-{year}-{month:2}-{sequence:5}` — e.g. `QUO-2026-10-00042`.
The sequence is a per-business running number. (For seed data, pick a reference that
follows this pattern and does not collide with existing ones.)

**Status values** (`[quotation].[QuotationStatusType]`, fixed seeded IDs):

| Id | Name | Meaning |
|----|------|---------|
| 1 | Draft | being prepared; the only status in which the quote can be edited |
| 2 | Sent | shared with the customer |
| 3 | Accepted | customer accepted; ready to convert |
| 4 | Converted | an invoice was generated from it |
| 5 | Archived | closed without action |

---

## 3. Sections

Table: `[quotation].[ProposalSection]`

Sections group lines on the proposal (e.g. "Hardware", "Monthly Services", "Scope of Work").
They are ordered by `SortOrder`.

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| Id | int | yes (PK) | |
| QuotationId | int | yes | FK → Quotation |
| Name | string(200) | yes | section heading |
| SortOrder | int | yes | display order (first real section = 1) |
| ColumnConfiguration | string(50) | yes | `"OneTime"` (default) or `"Subscription"` |
| SectionType | string(20) | yes | `"LineItems"` (default) or `"Narrative"` |
| Description | string(2000) | no | intro text under the heading |
| Notes | string(4000) | no | extra notes |
| IsEmphasized | bool | yes | default `false`; visually highlight the section |
| AccentColor | string(20) | no | optional hex/colour token for emphasis |
| Label | string(50) | no | small badge/label on the section |
| IsTotalsTableShown | bool | yes | show a per-section totals table |
| IsHalfWidth | bool | yes | render the section at half width (two-up layout) |

**`ColumnConfiguration` — this matters for money:**
- `"OneTime"` — prices are taken as-is (quantity × unit price).
- `"Subscription"` — every line in the section is treated as a **monthly** price and
  **annualized ×12** in all totals. A €22/mo line in a Subscription section contributes
  €264 to the subtotal. The proposal shows these under a "Subscription Items (Monthly)" table.

**`SectionType`:**
- `"LineItems"` — a normal priced table of lines.
- `"Narrative"` — a text-only section (no priced lines; uses Description/Notes).

**The "General" section is NOT a real row.** Any line whose `ProposalSectionId` is `null`
belongs to the implicit "General" group, which always renders at the bottom. Do **not**
create a ProposalSection named "General" — just leave those lines' section id null.
Deleting a section moves its lines back to General (section id → null).

---

## 4. Lines

Table: `[quotation].[QuotationLine]`

Each line is one priced item. Lines are ordered by `SortOrder`.

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| Id | int | yes (PK) | |
| QuotationId | int | yes | FK → Quotation |
| Description | string(500) | yes | the item name/description |
| Quantity | decimal(18,4) | yes | must be > 0 |
| UnitPrice | decimal(18,2) | yes | must be ≥ 0 (for Subscription sections this is the **monthly** price) |
| VatRate | decimal(5,2) | yes | VAT percent 0–100 |
| Discount | decimal(5,2) | yes | default 0; line-level discount (see DiscountType) |
| DiscountType | string(10) | yes | `"Percentage"` (default) or `"Fixed"` |
| CostPrice | decimal(18,2) | no | internal cost (not shown to customer) |
| LineTotal | decimal(18,2) | yes | **computed** net-of-line-discount, excludes VAT (see §7) |
| SortOrder | int | yes | display order within its section (first = 1) |
| ReferenceUrl | string(2048) | no | absolute http/https link shown as a "ref" |
| ProposalSectionId | int | no | FK → ProposalSection, or null = General |
| Subtitle | string(1000) | no | secondary line under the description |
| ProductCode | string(50) | no | free-text product code snapshot (not a FK) |
| IsReverseCharge | bool | yes | if true, `VatRate` must be 0 |
| IsAdjustmentLine | bool | yes | marks the bulk-discount line (see §5) |
| ProductPriceTierId | int | no | FK → ProductPriceTier (chosen price tier) |
| PriceTierName | string | no | snapshot of the tier name at add time |

**Line-level discount** is baked into `LineTotal`:
- `DiscountType = "Percentage"` → `LineTotal = round(Qty × UnitPrice × (1 − Discount/100), 2)`
- `DiscountType = "Fixed"` → `LineTotal = round(Qty × UnitPrice − Discount, 2)`
- no discount → `LineTotal = round(Qty × UnitPrice, 2)`

`LineTotal` is net of the line discount and **excludes VAT**. VAT is applied on top in the
totals step.

**Reverse charge:** when `IsReverseCharge = true`, VAT must be 0% (the line contributes no VAT).

**Product linkage.** A line can reference the product catalog two ways, both optional:
- `ProductCode` — a free-text snapshot string (not a foreign key).
- `ProductPriceTierId` — a real FK to a named price tier of a product. When set, the tier
  name is snapshotted into `PriceTierName`.
A line does not have to reference a product at all; it can be fully free-text.

---

## 5. The quotation-level (bulk) discount — the "adjustment line"

A quotation may have **at most one** line with `IsAdjustmentLine = true`. It is not a product;
it represents a discount applied to the whole quotation. Its shape is fixed:

- `Quantity = 1`, `UnitPrice = 0`, `VatRate = 0`, `ProposalSectionId = null`
- `LineTotal` is **negative** and equals the discount amount
- `Discount` + `DiscountType` describe the bulk discount (`"Percentage"` or `"Fixed"`)
- `Description` is auto-generated, e.g. `"Quotation Discount (10%)"` or `"Quotation Discount (-€50.00)"`

A percentage bulk discount re-tracks the subtotal automatically; a fixed one is a flat amount.
There are therefore **two discount layers**: per-line discounts (inside each line's LineTotal)
and the single quotation-level adjustment line.

---

## 6. Prepared By (QuotationContact)

Table: `[quotation].[QuotationContact]` — a reusable "author/preparer" record per business,
referenced by `Quotation.QuotationContactId`. Optional.

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| Id | int | yes (PK) | |
| BusinessId | int | yes | FK → Business |
| Name | string(200) | yes | |
| Email | string(200) | no | |
| TelephoneNumber | string(30) | no | |
| UserId | string(450) | no | optional link to an identity user |
| IsActive | bool | yes | default true |

---

## 7. How the totals are computed

The three money fields on the header are **always derived from the lines** — never entered
directly. Compute them like this:

1. Separate lines into **normal lines** (`IsAdjustmentLine = false`) and the single
   **adjustment line** (if any).
2. For each normal line, pick a **multiplier**: `12` if the line's section is a
   `"Subscription"` section, otherwise `1`.
3. Accumulate:
   - `Subtotal = Σ (LineTotal × multiplier)` — rounded to 2 dp. (net of line discounts, excludes VAT)
   - `TaxAmount = Σ (LineTotal × multiplier × VatRate / 100)` — rounded to 2 dp.
4. `adjustmentAmount` = the adjustment line's `LineTotal` (negative) or 0.
5. `TotalAmount = Subtotal + adjustmentAmount + TaxAmount`.

Optional presentation breakdown (not stored, shown in the UI):
- `GrossSubtotal = Σ (Qty × UnitPrice × multiplier)` — before any discount
- `LineDiscounts = GrossSubtotal − Subtotal`
- `InvoiceDiscount = |adjustment line total|`
- `NetAmount = Subtotal − InvoiceDiscount`
- `Total = NetAmount + Tax`

Currency is EUR (€) across the platform today.

**Worked example (mixed one-time + subscription):**

- Section "Hardware" (OneTime): 1 × Label Printer @ €120.00, 19% VAT, no discount
  → LineTotal 120.00, multiplier 1
- Section "Services" (Subscription): 1 × Support @ €22.00/mo, 19% VAT, no discount
  → LineTotal 22.00, multiplier 12 → contributes 264.00
- Subtotal = 120.00 + 264.00 = **384.00**
- Tax = 120.00×0.19 + 264.00×0.19 = 22.80 + 50.16 = **72.96**
- No bulk discount → Total = 384.00 + 0 + 72.96 = **456.96**

---

## 8. Allowed values cheat-sheet

| Thing | Allowed values |
|-------|----------------|
| Quotation status | 1 Draft, 2 Sent, 3 Accepted, 4 Converted, 5 Archived |
| Section `ColumnConfiguration` | `"OneTime"`, `"Subscription"` |
| Section `SectionType` | `"LineItems"`, `"Narrative"` |
| Line / discount `DiscountType` | `"Percentage"`, `"Fixed"` |
| `VatRate` | decimal 0–100 (per line; no VAT lookup table) |
| Reverse charge | if `IsReverseCharge = true` then `VatRate = 0` |

---

## 9. What a proposal spec should contain (output template for the GPT agent)

To define a proposal, provide:

1. **Header**
   - Customer (name — we map to CustomerId at seed time)
   - Reference (or let it be generated as `QUO-YYYY-MM-#####`)
   - ValidUntil (optional)
   - Notes (optional)
   - Prepared By name (optional)
   - IsGrandTotalShown (default true)

2. **Sections** (optional, ordered). For each:
   - Name, SortOrder
   - ColumnConfiguration (`OneTime` or `Subscription`)
   - SectionType (`LineItems` or `Narrative`)
   - Description / Notes (optional), IsEmphasized / AccentColor / Label / IsTotalsTableShown / IsHalfWidth (optional)
   - (Do not define a "General" section — unsectioned lines go there automatically.)

3. **Lines** (ordered). For each:
   - Which section it belongs to (section name, or "General")
   - Description, Subtitle (optional)
   - Quantity, UnitPrice (monthly price if the section is Subscription), VatRate
   - Discount + DiscountType (optional)
   - ProductCode (optional), and whether it maps to a catalog Product / price tier (optional)
   - ReferenceUrl (optional)
   - IsReverseCharge (optional; forces VAT 0)

4. **Bulk discount** (optional): type (`Percentage`/`Fixed`) and value.

5. **Expected totals**: Subtotal, Tax, Total computed per §7, so we can verify the seed.

---

## 10. Notes for the seed file (what Kiro will need afterwards)

When the proposal spec comes back, these IDs/values must be resolved before writing the seed:

- **BusinessId** — the tenant that owns the quotation.
- **CustomerId** — resolve the customer by name, or seed the customer too.
- **QuotationContactId** — if a "Prepared By" is named, resolve or seed it.
- **ProductPriceTierId / Product** — only if any line links to a catalog product/tier.
  For each such line we need the `Product.Id`, its `ProductCode`, and the `ProductPriceTier.Id`
  (which must be active). If lines are free-text (just Description + prices), no product IDs
  are needed.
- **Reference** — must be unique per business and follow `QUO-YYYY-MM-#####`.
- **Computed totals** — the seed must set the header Subtotal/TaxAmount/TotalAmount to the
  values from §7 (including ×12 for Subscription-section lines), and each line's `LineTotal`
  to the §4 formula, so the stored data is internally consistent.

So: product IDs are only required if the proposal references catalog products or price tiers.
A fully free-text proposal (description + quantity + price + VAT per line) needs no product IDs —
just a Business and a Customer.
