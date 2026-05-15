# Design Document: Line Item Cost Price

## Overview

This feature adds a nullable `CostPrice` column to the `QuotationLine` entity, enabling internal profit/margin tracking per line item. The cost price represents the actual purchase cost of an item (e.g., a domain costs €39 but is billed at €60). This value is strictly internal and must never appear in customer-facing views (proposals, invoices, shared links).

The implementation is a straightforward additive change across all layers: database schema → entity → repository → service → form model → edit view. No existing behaviour changes; the column is nullable and optional throughout.

## Architecture

The change follows the existing vertical slice through the MVC + Service + Repository stack:

```
[Database] → DECIMAL(18,2) NULL column on [quotation].[QuotationLine]
     ↓
[Entity] → decimal? CostPrice property on QuotationLine
     ↓
[Repository] → CostPrice added to SELECT/INSERT/UPDATE SQL statements
     ↓
[Service] → AddLineAsync/UpdateLineAsync accept decimal? costPrice parameter with validation (≥ 0)
     ↓
[Interface] → IQuotationService updated signatures
     ↓
[FormModel] → QuotationLineFormViewModel gets nullable CostPrice with [Range(0, ...)] validation
     ↓
[Controller] → Passes CostPrice from form to service
     ↓
[View] → Optional input field in _SectionCards.cshtml edit form
     ↓
[Margin Display] → Computed in view/viewmodel: UnitMargin = UnitPrice - CostPrice, LineMargin = LineTotal - (CostPrice × Quantity)
```

No changes to Proposal Snapshot, Invoice views, or any customer-facing endpoint.

## Components and Interfaces

### Database Migration (033_AddCostPriceToQuotationLine.sql)

Idempotent `ALTER TABLE` adding `[CostPrice] DECIMAL(18,2) NULL`.

### Entity: QuotationLine.cs

Add property:
```csharp
public decimal? CostPrice { get; set; }
```

### DbContext: ConfigureQuotationLine

Add fluent configuration:
```csharp
entity.Property(e => e.CostPrice)
    .HasPrecision(18, 2);
```

### Repository: QuotationLineRepository.cs

- Add `[CostPrice]` to all SELECT queries
- Add `@CostPrice` parameter to INSERT and UPDATE with `?? (object)DBNull.Value` null-safety

### Service: IQuotationService / QuotationService

Updated method signatures:
```csharp
Task<QuotationLine> AddLineAsync(..., decimal? costPrice = null);
Task UpdateLineAsync(..., decimal? costPrice = null);
```

Validation in service: if `costPrice.HasValue && costPrice.Value < 0` → throw `ArgumentException`.

### Form Model: QuotationLineFormViewModel

```csharp
[Range(0, double.MaxValue, ErrorMessage = "Cost price must be zero or greater")]
public decimal? CostPrice { get; set; }
```

### Controller: QuotationController

Pass `model.CostPrice` to service `AddLineAsync` / `UpdateLineAsync` calls.

### View: _SectionCards.cshtml

Add an optional `CostPrice` input field in the line item form grid. Display computed margin (UnitPrice − CostPrice) when CostPrice is populated.

### Exclusion Verification

- `Proposal/Snapshot.cshtml` — no CostPrice rendered
- `ProposalShare` endpoint — no CostPrice in snapshot HTML
- Invoice views — no CostPrice in output

## Data Models

### Database Column

| Column | Type | Nullable | Default | Constraint |
|--------|------|----------|---------|------------|
| CostPrice | DECIMAL(18,2) | YES | NULL | None (CHECK ≥ 0 enforced at service layer) |

### Entity Property

```csharp
public decimal? CostPrice { get; set; }
```

### Margin Calculation (computed, not persisted)

```
UnitMargin = UnitPrice - CostPrice           (when CostPrice is not null)
LineMargin = LineTotal - (CostPrice × Quantity) (when CostPrice is not null)
```

Margins are display-only values computed in the view model or view. They are not stored in the database.


## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: CostPrice persistence round-trip

*For any* QuotationLine with a CostPrice value (either null or a non-negative decimal), inserting or updating the line and then retrieving it should return the exact same CostPrice value that was provided.

**Validates: Requirements 1.2, 1.3, 2.4**

### Property 2: CostPrice validation rejects negative values

*For any* decimal value, the QuotationService shall accept the CostPrice if and only if the value is null or greater than or equal to zero. Any negative value must result in an ArgumentException.

**Validates: Requirements 3.4, 3.5**

### Property 3: CostPrice excluded from customer-facing output

*For any* QuotationLine with a non-null CostPrice, the rendered Proposal and Invoice HTML output shall not contain the CostPrice numeric value anywhere in the response body.

**Validates: Requirements 5.1, 5.2**

### Property 4: Margin calculation correctness

*For any* QuotationLine where CostPrice is not null, the unit margin shall equal `UnitPrice - CostPrice` and the line margin shall equal `LineTotal - (CostPrice × Quantity)`.

**Validates: Requirements 6.1, 6.2**

### Property 5: Null CostPrice produces no margin

*For any* QuotationLine where CostPrice is null, the margin calculation function shall return null (no margin value displayed).

**Validates: Requirements 6.3**

## Error Handling

| Scenario | Layer | Behaviour |
|----------|-------|-----------|
| Negative CostPrice submitted via form | Controller/Model Validation | `[Range(0, ...)]` rejects with validation error, form re-displayed |
| Negative CostPrice passed to service | Service | `ArgumentException("Cost price must be zero or greater")` |
| NULL CostPrice in database | Repository/Entity | Mapped to `decimal? = null` — no error |
| SQL parameter null handling | Repository | `entity.CostPrice ?? (object)DBNull.Value` pattern |
| Database column overflow | Database | DECIMAL(18,2) supports values up to 9,999,999,999,999,999.99 — no practical overflow risk |

No new exception types are introduced. The existing `try/catch { throw; }` pattern in the repository is maintained.

## Testing Strategy

### Unit Tests

- Verify `ArgumentException` is thrown when `costPrice = -1` is passed to `AddLineAsync`
- Verify `ArgumentException` is thrown when `costPrice = -0.01m` is passed to `UpdateLineAsync`
- Verify no exception when `costPrice = 0` or `costPrice = null`
- Verify margin calculation helper returns correct values for known inputs
- Verify margin calculation returns null when CostPrice is null

### Property-Based Tests

Library: **FsCheck.Xunit** (existing .NET property-based testing library)

Each property test runs a minimum of 100 iterations with randomly generated inputs.

- **Test 1** — Feature: line-item-cost-price, Property 1: CostPrice persistence round-trip
  - Generate random non-negative decimals (and null). Insert via service, retrieve via repository, assert equality.

- **Test 2** — Feature: line-item-cost-price, Property 2: CostPrice validation rejects negative values
  - Generate random negative decimals. Call AddLineAsync/UpdateLineAsync, assert ArgumentException thrown.
  - Generate random non-negative decimals (including zero). Call AddLineAsync/UpdateLineAsync, assert no exception.

- **Test 3** — Feature: line-item-cost-price, Property 3: CostPrice excluded from customer-facing output
  - Generate random lines with non-null CostPrice values. Render proposal/invoice HTML. Assert CostPrice value string not present in output.

- **Test 4** — Feature: line-item-cost-price, Property 4: Margin calculation correctness
  - Generate random UnitPrice, CostPrice (non-null, non-negative), Quantity, Discount. Compute expected margins. Assert calculation function matches.

- **Test 5** — Feature: line-item-cost-price, Property 5: Null CostPrice produces no margin
  - Generate random lines with null CostPrice. Assert margin function returns null.

### Integration Tests

- End-to-end: Add line with CostPrice via controller → verify persisted in DB → verify excluded from proposal snapshot render.
- Migration test: Run migration on test database, verify column exists with correct type and nullability.
