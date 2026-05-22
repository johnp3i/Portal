# Design Document: Purchase & Expense Tracking

## Overview

The Purchase & Expense Tracking module (Module 5) adds full expense management capabilities to the Portal platform. It enables business managers to record purchases with VAT tracking, categorise expenses by supplier and type, and handle EU Reverse Charge transactions where VAT is excluded from qualifying cross-border purchases.

The module follows the established MVC + Service + Repository architecture pattern, reusing the existing `GenericStoredProcedureRepository<T>` base class, `ServiceResult` response model, `ICurrentTenantService` for tenant isolation, and `AuditLogRepository` for change tracking. The database tables (`[purchase].[Supplier]`, `[purchase].[ExpenseCategory]`, `[purchase].[Purchase]`) and EF Core entity classes already exist with global query filters configured in `PortalDbContext`.

Key capabilities:
- **Supplier CRUD** — Manage vendor registry with soft-deactivation (never hard-delete)
- **Expense Category CRUD** — Classify purchases for reporting and VAT grouping
- **Purchase CRUD** — Record expenses with full VAT tracking and Purchase Origin Type classification (Domestic, EU Reverse Charge, Non-EU)
- **Bulk Entry** — Spreadsheet-style inline grid for rapid multi-purchase entry
- **CSV Import** — Upload historical data with validation preview before commit
- **Audit Logging** — All create/update/deactivate operations logged to `[audit].[AuditLog]`

## Architecture

The module follows the established layered architecture:

```
┌─────────────────────────────────────────────────────────────────────┐
│                        Portal.Web (MVC)                              │
│  ┌──────────────────┐ ┌────────────────────────┐ ┌───────────────┐ │
│  │SupplierController│ │ExpenseCategoryController│ │PurchaseController│
│  └────────┬─────────┘ └───────────┬────────────┘ └───────┬───────┘ │
└───────────┼───────────────────────┼───────────────────────┼─────────┘
            │                       │                       │
┌───────────┼───────────────────────┼───────────────────────┼─────────┐
│           ▼                       ▼                       ▼         │
│  Portal.Infrastructure (Services)                                   │
│  ┌──────────────────┐ ┌────────────────────────┐ ┌───────────────┐ │
│  │ SupplierService  │ │ExpenseCategoryService  │ │PurchaseService│ │
│  │(ISupplierService)│ │(IExpenseCategoryService)│ │(IPurchaseService)│
│  └────────┬─────────┘ └───────────┬────────────┘ └───────┬───────┘ │
│           │                       │                       │         │
│           ▼                       ▼                       ▼         │
│  ┌──────────────────┐ ┌────────────────────────┐ ┌───────────────┐ │
│  │SupplierRepository│ │ExpenseCategoryRepository│ │PurchaseRepository│
│  └────────┬─────────┘ └───────────┬────────────┘ └───────┬───────┘ │
│           │                       │                       │         │
│           ▼                       ▼                       ▼         │
│  ┌─────────────────────────────────────────────────────────────────┐│
│  │              PortalDbContext (EF Core + Global Query Filters)    ││
│  └─────────────────────────────────────────────────────────────────┘│
│           │                       │                       │         │
│           ▼                       ▼                       ▼         │
│  ┌─────────────────────────────────────────────────────────────────┐│
│  │  AuditLogRepository (append-only audit trail)                   ││
│  └─────────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────────┘
            │                       │                       │
            ▼                       ▼                       ▼
┌─────────────────────────────────────────────────────────────────────┐
│  SQL Server: [purchase].Supplier, [purchase].ExpenseCategory,       │
│              [purchase].Purchase, [purchase].PurchaseOriginType,     │
│              [audit].AuditLog                                        │
└─────────────────────────────────────────────────────────────────────┘
```

### Design Decisions

1. **Repository pattern with raw SQL** — Consistent with existing `CustomerRepository` pattern. Uses `ExecuteSqlRawAsync` for writes and `FromSqlRaw` for reads via `GenericStoredProcedureRepository<T>`.
2. **Service layer returns `ServiceResult`** — Validation failures return `ServiceResult.Fail(message)` rather than throwing exceptions, enabling clean JSON error responses from controllers.
3. **Soft-deactivation** — Suppliers and ExpenseCategories are never hard-deleted because existing Purchase records reference them. Setting `IsActive = false` hides them from dropdowns.
4. **Global query filters** — Already configured in `PortalDbContext.ApplyGlobalQueryFilters()` for Supplier, ExpenseCategory, and Purchase entities. Tenant isolation is enforced at the EF Core level.
5. **Bulk operations use transactions** — Both bulk entry and CSV import wrap batch inserts in a database transaction for atomicity (all-or-nothing).
6. **Purchase Origin Type replaces boolean flag** — Instead of a single `IsEuReverseCharge` BIT column, a `PurchaseOriginTypeId` INT column references `[purchase].[PurchaseOriginType]` (Domestic=1, EuReverseCharge=2, NonEu=3). This enables geographic origin tracking for statistics and reporting while maintaining the EU RC VAT enforcement logic. The `PurchaseService` enforces the RC invariant (VatAmount = 0, TotalAmount = AmountExcludingVat) when origin is EuReverseCharge, and requires Country for both EuReverseCharge and NonEu origins.

## Components and Interfaces

### Repository Layer

```csharp
// Portal.Infrastructure/Repositories/SupplierRepository.cs
public class SupplierRepository : GenericStoredProcedureRepository<Supplier>
{
    Task<List<Supplier>> GetAllByBusinessIdAsync(int businessId);
    Task<Supplier?> GetByIdAndBusinessIdAsync(int id, int businessId);
    Task InsertAsync(Supplier entity);
    Task UpdateAsync(Supplier entity);
    Task DeactivateAsync(int id, int businessId);
}

// Portal.Infrastructure/Repositories/ExpenseCategoryRepository.cs
public class ExpenseCategoryRepository : GenericStoredProcedureRepository<ExpenseCategory>
{
    Task<List<ExpenseCategory>> GetAllByBusinessIdAsync(int businessId);
    Task<ExpenseCategory?> GetByIdAndBusinessIdAsync(int id, int businessId);
    Task InsertAsync(ExpenseCategory entity);
    Task UpdateAsync(ExpenseCategory entity);
    Task DeactivateAsync(int id, int businessId);
}

// Portal.Infrastructure/Repositories/PurchaseRepository.cs
public class PurchaseRepository : GenericStoredProcedureRepository<Purchase>
{
    Task<List<Purchase>> GetAllByBusinessIdAsync(int businessId);
    Task<Purchase?> GetByIdAndBusinessIdAsync(int id, int businessId);
    Task InsertAsync(Purchase entity);
    Task UpdateAsync(Purchase entity);
    Task<List<Purchase>> GetFilteredAsync(int businessId, int? supplierId, int? expenseCategoryId, DateOnly? dateFrom, DateOnly? dateTo);
}
```

### Service Layer

```csharp
// Portal.Infrastructure/Services/ISupplierService.cs
public interface ISupplierService
{
    Task<List<Supplier>> GetSuppliersAsync();
    Task<List<Supplier>> GetActiveSuppliersAsync();
    Task<Supplier?> GetSupplierByIdAsync(int id);
    Task<ServiceResult> CreateSupplierAsync(Supplier supplier);
    Task<ServiceResult> UpdateSupplierAsync(Supplier supplier);
    Task<ServiceResult> DeactivateSupplierAsync(int id);
}

// Portal.Infrastructure/Services/IExpenseCategoryService.cs
public interface IExpenseCategoryService
{
    Task<List<ExpenseCategory>> GetExpenseCategoriesAsync();
    Task<List<ExpenseCategory>> GetActiveExpenseCategoriesAsync();
    Task<ExpenseCategory?> GetExpenseCategoryByIdAsync(int id);
    Task<ServiceResult> CreateExpenseCategoryAsync(ExpenseCategory category);
    Task<ServiceResult> UpdateExpenseCategoryAsync(ExpenseCategory category);
    Task<ServiceResult> DeactivateExpenseCategoryAsync(int id);
}

// Portal.Infrastructure/Services/IPurchaseService.cs
public interface IPurchaseService
{
    Task<List<Purchase>> GetPurchasesAsync();
    Task<List<Purchase>> GetFilteredPurchasesAsync(int? supplierId, int? expenseCategoryId, DateOnly? dateFrom, DateOnly? dateTo);
    Task<Purchase?> GetPurchaseByIdAsync(int id);
    Task<ServiceResult> CreatePurchaseAsync(Purchase purchase);
    Task<ServiceResult> UpdatePurchaseAsync(Purchase purchase);
    Task<ServiceResult> BulkCreatePurchasesAsync(List<Purchase> purchases);
}
```

### Controller Layer

```csharp
// Portal.Web/Controllers/SupplierController.cs
[Authorize]
[ModuleAccess(PortalModules.Purchase)]
public class SupplierController : Controller
{
    // GET /Supplier — List view
    // POST /Supplier/Create — JSON response (AJAX modal)
    // POST /Supplier/Edit — JSON response (AJAX modal)
    // POST /Supplier/Deactivate — JSON response (AJAX confirm)
}

// Portal.Web/Controllers/ExpenseCategoryController.cs
[Authorize]
[ModuleAccess(PortalModules.Purchase)]
public class ExpenseCategoryController : Controller
{
    // GET /ExpenseCategory — List view
    // POST /ExpenseCategory/Create — JSON response (AJAX modal)
    // POST /ExpenseCategory/Edit — JSON response (AJAX modal)
    // POST /ExpenseCategory/Deactivate — JSON response (AJAX confirm)
}

// Portal.Web/Controllers/PurchaseController.cs
[Authorize]
[ModuleAccess(PortalModules.Purchase)]
public class PurchaseController : Controller
{
    // GET /Purchase — List view with filter params
    // GET /Purchase/Create — Form view
    // POST /Purchase/Create — Form submit, redirect
    // GET /Purchase/Edit/{id} — Form view pre-populated
    // POST /Purchase/Edit/{id} — Form submit, redirect
    // GET /Purchase/BulkEntry — Bulk entry grid view
    // POST /Purchase/BulkCreate — JSON batch save (AJAX)
    // GET /Purchase/CsvImport — CSV upload view
    // POST /Purchase/CsvImport — Parse & preview (AJAX)
    // POST /Purchase/CsvConfirm — Commit import (AJAX)
}
```

### View Layer

| View | Type | Description |
|------|------|-------------|
| `Views/Supplier/Index.cshtml` | List + Modal | Table with inline create/edit modal, deactivate via SweetAlert2 |
| `Views/ExpenseCategory/Index.cshtml` | List + Modal | Table with inline create/edit modal, deactivate via SweetAlert2 |
| `Views/Purchase/Index.cshtml` | List + Filter | Filterable table with supplier/category/date filters |
| `Views/Purchase/Create.cshtml` | Form | Single purchase form with Origin Type selector (Domestic/EU RC/Non-EU) |
| `Views/Purchase/Edit.cshtml` | Form | Pre-populated edit form with Origin Type selector |
| `Views/Purchase/BulkEntry.cshtml` | Grid | Spreadsheet-style inline editable grid |
| `Views/Purchase/CsvImport.cshtml` | Upload + Preview | File upload with validation preview grid |

## Data Models

### Existing Entities (already scaffolded)

```csharp
// [purchase].[Supplier]
public class Supplier
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string Name { get; set; } = null!;       // max 200
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    // Navigation
    public Business Business { get; set; } = null!;
    public ICollection<Purchase> Purchases { get; set; }
}

// [purchase].[ExpenseCategory]
public class ExpenseCategory
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string Name { get; set; } = null!;       // max 100
    public bool IsActive { get; set; }
    // Navigation
    public Business Business { get; set; } = null!;
    public ICollection<Purchase> Purchases { get; set; }
}

// [purchase].[Purchase]
public class Purchase
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public int SupplierId { get; set; }
    public int ExpenseCategoryId { get; set; }
    public int PurchaseOriginTypeId { get; set; }    // 1=Domestic, 2=EuReverseCharge, 3=NonEu
    public string? InvoiceNumber { get; set; }      // max 100
    public DateOnly InvoiceDate { get; set; }
    public string Description { get; set; } = null!; // max 500
    public decimal AmountExcludingVat { get; set; }  // precision(18,2)
    public decimal VatAmount { get; set; }           // precision(18,2)
    public decimal TotalAmount { get; set; }         // precision(18,2)
    public string? Country { get; set; }             // max 100, required when origin is EuReverseCharge or NonEu
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    // Navigation
    public Business Business { get; set; } = null!;
    public Supplier Supplier { get; set; } = null!;
    public ExpenseCategory ExpenseCategory { get; set; } = null!;
    public PurchaseOriginType PurchaseOriginType { get; set; } = null!;
}

// [purchase].[PurchaseOriginType] — Lookup table
public class PurchaseOriginType
{
    public int Id { get; set; }                     // 1=Domestic, 2=EuReverseCharge, 3=NonEu
    public string Name { get; set; } = null!;       // max 50
}
```

### New View Models (Portal.Web/Models)

```csharp
// Purchase form view model
public class PurchaseFormViewModel
{
    [Required] public int SupplierId { get; set; }
    [Required] public int ExpenseCategoryId { get; set; }
    [Required] public int PurchaseOriginTypeId { get; set; } = 1; // Default: Domestic
    public string? InvoiceNumber { get; set; }
    [Required] public DateOnly InvoiceDate { get; set; }
    [Required] public string Description { get; set; } = null!;
    [Required] public decimal AmountExcludingVat { get; set; }
    public decimal VatAmount { get; set; }
    public string? Country { get; set; }
    public string? Notes { get; set; }
    // For dropdowns
    public List<Supplier> Suppliers { get; set; } = new();
    public List<ExpenseCategory> ExpenseCategories { get; set; } = new();
    public List<PurchaseOriginType> OriginTypes { get; set; } = new();
}

// Purchase list view model
public class PurchaseListViewModel
{
    public List<Purchase> Purchases { get; set; } = new();
    public List<Supplier> Suppliers { get; set; } = new();
    public List<ExpenseCategory> ExpenseCategories { get; set; } = new();
    public List<PurchaseOriginType> OriginTypes { get; set; } = new();
    // Filter state
    public int? SupplierId { get; set; }
    public int? ExpenseCategoryId { get; set; }
    public int? PurchaseOriginTypeId { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
}

// Bulk entry row model (JSON payload)
public class BulkPurchaseRowDto
{
    public DateOnly InvoiceDate { get; set; }
    public string? InvoiceNumber { get; set; }
    public int SupplierId { get; set; }
    public int ExpenseCategoryId { get; set; }
    public string Description { get; set; } = null!;
    public decimal AmountExcludingVat { get; set; }
    public decimal VatAmount { get; set; }
    public int PurchaseOriginTypeId { get; set; } = 1; // Default: Domestic
    public string? Country { get; set; }
}

// CSV import row (parsed from file)
public class CsvPurchaseRowDto
{
    public int RowNumber { get; set; }
    public DateOnly InvoiceDate { get; set; }
    public string? InvoiceNumber { get; set; }
    public string SupplierName { get; set; } = null!;
    public string ExpenseCategoryName { get; set; } = null!;
    public string Description { get; set; } = null!;
    public decimal AmountExcludingVat { get; set; }
    public decimal VatAmount { get; set; }
    public string PurchaseOriginType { get; set; } = "Domestic"; // Domestic/EuReverseCharge/NonEu
    public string? Country { get; set; }
    public string? Notes { get; set; }
    // Resolved IDs (after matching)
    public int? ResolvedSupplierId { get; set; }
    public int? ResolvedExpenseCategoryId { get; set; }
    public int? ResolvedPurchaseOriginTypeId { get; set; }
    // Validation
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}
```

### Database Schema (existing migrations)

```sql
-- [purchase].[Supplier] (Migration 014)
CREATE TABLE [purchase].[Supplier] (
    [Id]           INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [BusinessId]   INT NOT NULL REFERENCES [portal].[Business]([Id]),
    [Name]         NVARCHAR(200) NOT NULL,
    [IsActive]     BIT NOT NULL DEFAULT 1,
    [CreatedAtUtc] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

-- [purchase].[ExpenseCategory] (Migration 015)
CREATE TABLE [purchase].[ExpenseCategory] (
    [Id]         INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [BusinessId] INT NOT NULL REFERENCES [portal].[Business]([Id]),
    [Name]       NVARCHAR(100) NOT NULL,
    [IsActive]   BIT NOT NULL DEFAULT 1
);

-- [purchase].[PurchaseOriginType] — New lookup table (new migration required)
CREATE TABLE [purchase].[PurchaseOriginType] (
    [Id]   INT NOT NULL PRIMARY KEY,
    [Name] NVARCHAR(50) NOT NULL
);
INSERT INTO [purchase].[PurchaseOriginType] ([Id], [Name]) VALUES
    (1, 'Domestic'),
    (2, 'EuReverseCharge'),
    (3, 'NonEu');

-- [purchase].[Purchase] (Migration 016 + new migration to replace IsEuReverseCharge with PurchaseOriginTypeId)
CREATE TABLE [purchase].[Purchase] (
    [Id]                    INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [BusinessId]            INT NOT NULL REFERENCES [portal].[Business]([Id]),
    [SupplierId]            INT NOT NULL REFERENCES [purchase].[Supplier]([Id]),
    [ExpenseCategoryId]     INT NOT NULL REFERENCES [purchase].[ExpenseCategory]([Id]),
    [PurchaseOriginTypeId]  INT NOT NULL DEFAULT 1 REFERENCES [purchase].[PurchaseOriginType]([Id]),
    [InvoiceNumber]         NVARCHAR(100) NULL,
    [InvoiceDate]           DATE NOT NULL,
    [Description]           NVARCHAR(500) NOT NULL,
    [AmountExcludingVat]    DECIMAL(18,2) NOT NULL,
    [VatAmount]             DECIMAL(18,2) NOT NULL,
    [TotalAmount]           DECIMAL(18,2) NOT NULL,
    [Country]               NVARCHAR(100) NULL,
    [Notes]                 NVARCHAR(MAX) NULL,
    [CreatedAtUtc]          DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAtUtc]          DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
```


## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: TotalAmount equals AmountExcludingVat plus VatAmount

*For any* purchase with `PurchaseOriginTypeId` equal to 1 (Domestic) or 3 (NonEu), the computed `TotalAmount` SHALL equal `AmountExcludingVat + VatAmount` (after the service processes the purchase).

**Validates: Requirements 6.6, 7.5, 7.6**

### Property 2: EU Reverse Charge forces VatAmount to zero

*For any* purchase where `PurchaseOriginTypeId = 2` (EuReverseCharge) and *for any* user-provided `VatAmount` value (including positive values), the `PurchaseService` SHALL set `VatAmount` to `0.00` and `TotalAmount` to `AmountExcludingVat`.

**Validates: Requirements 7.2, 7.3**

### Property 3: EU Reverse Charge and Non-EU require non-whitespace Country

*For any* purchase where `PurchaseOriginTypeId` is 2 (EuReverseCharge) or 3 (NonEu), and *for any* `Country` value that is null or composed entirely of whitespace characters, the `PurchaseService` SHALL reject the purchase with a validation error.

**Validates: Requirements 7.4**

### Property 4: Domestic and Non-EU purchases preserve user-provided VatAmount

*For any* purchase where `PurchaseOriginTypeId` is 1 (Domestic) or 3 (NonEu), and *for any* valid `VatAmount` (>= 0), the `PurchaseService` SHALL preserve the user-provided `VatAmount` unchanged in the resulting purchase record.

**Validates: Requirements 7.5, 7.6**

### Property 12: Domestic purchases do not require Country

*For any* purchase where `PurchaseOriginTypeId = 1` (Domestic) and *for any* Country value (including null or whitespace), the `PurchaseService` SHALL NOT reject the purchase based on the Country field.

**Validates: Requirements 7.5**

### Property 5: Whitespace rejection for required text fields

*For any* string that is null or composed entirely of whitespace characters, the respective service SHALL reject it when used as a Supplier `Name`, ExpenseCategory `Name`, or Purchase `Description`, returning `ServiceResult.Fail` with a descriptive error message.

**Validates: Requirements 4.7, 5.7, 6.11**

### Property 6: Numeric validation bounds

*For any* `AmountExcludingVat` value that is less than or equal to zero, or *for any* `VatAmount` value that is less than zero, the `PurchaseService` SHALL reject the purchase with a validation error.

**Validates: Requirements 6.7, 6.8**

### Property 7: Tenant BusinessId assignment

*For any* Supplier, ExpenseCategory, or Purchase creation — regardless of what `BusinessId` value the caller provides in the input entity — the respective service SHALL overwrite `BusinessId` with the value from `ICurrentTenantService.CurrentBusinessId`.

**Validates: Requirements 4.4, 5.4, 6.4, 8.4**

### Property 8: Purchase filter correctness

*For any* set of purchases belonging to a tenant and *for any* combination of filter parameters (SupplierId, ExpenseCategoryId, DateFrom, DateTo), every purchase returned by the filter method SHALL satisfy all specified filter criteria, and no purchase satisfying all criteria SHALL be excluded from the results.

**Validates: Requirements 3.6**

### Property 9: Batch save atomicity

*For any* batch of purchase rows submitted to `BulkCreatePurchasesAsync`, if any single row fails validation then zero rows SHALL be persisted. If all rows pass validation then all rows SHALL be persisted.

**Validates: Requirements 17.7**

### Property 10: CSV parse round-trip

*For any* valid set of purchase data (with valid dates, numbers, and strings), serializing to the expected CSV format and then parsing back through the CSV import parser SHALL produce row data equivalent to the original input (field values preserved, correct column mapping).

**Validates: Requirements 18.2**

### Property 11: Case-insensitive name matching

*For any* existing active Supplier or ExpenseCategory name and *for any* case variation of that name (uppercase, lowercase, mixed), the CSV import name-matching logic SHALL resolve to the same record.

**Validates: Requirements 18.4**

## Error Handling

### Repository Layer

All repository methods follow the established pattern:
- Wrap data access in `try/catch`
- Rethrow exceptions (`throw;`) to preserve stack trace
- Never log directly — logging is handled by the controller/service layer

```csharp
try
{
    // SQL execution
}
catch (Exception)
{
    throw;
}
```

### Service Layer

Services return `ServiceResult` for validation failures (predictable business rule violations):

| Scenario | Response |
|----------|----------|
| Name is null/whitespace | `ServiceResult.Fail("Supplier name is required.")` |
| AmountExcludingVat <= 0 | `ServiceResult.Fail("Amount excluding VAT must be greater than zero.")` |
| VatAmount < 0 | `ServiceResult.Fail("VAT amount cannot be negative.")` |
| Description is null/whitespace | `ServiceResult.Fail("Description is required.")` |
| EU RC without Country | `ServiceResult.Fail("Country is required for EU Reverse Charge transactions.")` |
| Non-EU without Country | `ServiceResult.Fail("Country is required for Non-EU purchases.")` |
| Invalid PurchaseOriginTypeId | `ServiceResult.Fail("Invalid purchase origin type.")` |
| SupplierId not found/inactive | `ServiceResult.Fail("Selected supplier is not active or does not exist.")` |
| ExpenseCategoryId not found/inactive | `ServiceResult.Fail("Selected expense category is not active or does not exist.")` |
| Batch has invalid rows | `ServiceResult.Fail("Batch contains validation errors. No records were saved.")` |
| CSV exceeds 500 rows | `ServiceResult.Fail("CSV file exceeds the maximum of 500 rows.")` |

For unexpected exceptions (database failures, connection issues), services let exceptions propagate to the controller layer.

### Controller Layer

Controllers handle errors differently based on the endpoint type:

**Form-based endpoints (Purchase Create/Edit):**
```csharp
var result = await _purchaseService.CreatePurchaseAsync(purchase);
if (!result.Success)
{
    ModelState.AddModelError(string.Empty, result.Message!);
    return View(model); // Re-display form with error
}
return RedirectToAction(nameof(Index));
```

**AJAX endpoints (Supplier/ExpenseCategory CRUD, Bulk Entry, CSV Import):**
```csharp
var result = await _supplierService.CreateSupplierAsync(supplier);
return Json(new { success = result.Success, message = result.Message });
```

### Bulk Entry Error Handling

The bulk save endpoint validates all rows before persisting any:
1. Validate each row individually
2. Collect all validation errors with row numbers
3. If any errors exist, return the full error list without saving
4. If all valid, wrap inserts in a transaction

```csharp
// Response for partial validation failure
{
    "success": false,
    "message": "3 rows have validation errors.",
    "errors": [
        { "row": 2, "field": "Description", "message": "Description is required." },
        { "row": 4, "field": "AmountExcludingVat", "message": "Amount must be greater than zero." },
        { "row": 5, "field": "Country", "message": "Country is required for EU RC." }
    ]
}
```

### CSV Import Error Handling

CSV parsing errors are collected per-row and returned in the preview:
- Malformed CSV (wrong column count) → reject entire file
- Individual row validation errors → flag rows, allow user to fix or exclude
- Name matching failures → flag with "Supplier 'X' not found" message
- File size limit (500 rows) → reject before parsing

## Testing Strategy

### Unit Tests (Example-Based)

Unit tests cover specific scenarios, edge cases, and integration points:

| Area | Tests |
|------|-------|
| SupplierService | Create with valid name, create with empty name, deactivate existing, deactivate non-existent |
| ExpenseCategoryService | Create with valid name, create with whitespace name, deactivate |
| PurchaseService | Create with valid data, create with EU RC, create with Non-EU, toggle origin type, invalid supplier reference |
| Purchase Origin Type | EU RC sets VAT=0, Domestic preserves VAT, Non-EU preserves VAT, EU RC requires Country, Non-EU requires Country, Domestic allows null Country |
| Bulk Entry | All valid rows saved, one invalid row blocks all, empty batch |
| CSV Import | Valid file parsed correctly, unmatched supplier flagged, 501 rows rejected |
| Controllers | Correct HTTP responses, redirect after success, JSON error on failure |

### Property-Based Tests

Property-based tests verify universal properties across randomized inputs. The project will use **FsCheck** (via `FsCheck.Xunit`) for property-based testing in the .NET ecosystem.

**Configuration:**
- Minimum 100 iterations per property test
- Each test references its design document property via tag comment

**Tag format:** `// Feature: purchase-expense-tracking, Property {number}: {title}`

**Properties to implement:**

| # | Property | Generator Strategy |
|---|----------|-------------------|
| 1 | TotalAmount computation | Random decimal AmountExcludingVat (0.01–999999.99), random VatAmount (0–999999.99), PurchaseOriginTypeId in {1,3} |
| 2 | EU RC forces VatAmount to zero | Random VatAmount (including large values), PurchaseOriginTypeId=2 |
| 3 | EU RC/Non-EU requires Country | Random whitespace strings (empty, spaces, tabs, newlines) with PurchaseOriginTypeId in {2,3} |
| 4 | Domestic/Non-EU preserves VatAmount | Random valid VatAmount with PurchaseOriginTypeId in {1,3} |
| 12 | Domestic allows null Country | Random null/whitespace Country with PurchaseOriginTypeId=1 |
| 5 | Whitespace rejection | Random whitespace strings for Name/Description fields |
| 6 | Numeric validation bounds | Random non-positive AmountExcludingVat, random negative VatAmount |
| 7 | Tenant BusinessId assignment | Random BusinessId values in input, verify overwritten |
| 8 | Filter correctness | Random purchase sets + random filter combinations (including origin type filter) |
| 9 | Batch atomicity | Random batches with 0–N invalid rows |
| 10 | CSV round-trip | Random purchase data → CSV string → parse → compare |
| 11 | Case-insensitive matching | Random case transformations of known names |

### Integration Tests

Integration tests verify database operations and cross-layer behavior:
- Repository CRUD operations against a test database
- Global query filter enforcement (multi-tenant isolation)
- Audit log entry creation after service operations
- End-to-end controller tests with authentication

### Test Project Structure

```
Portal.Tests/
├── Unit/
│   ├── Services/
│   │   ├── SupplierServiceTests.cs
│   │   ├── ExpenseCategoryServiceTests.cs
│   │   └── PurchaseServiceTests.cs
│   └── Properties/
│       ├── PurchaseVatPropertyTests.cs      (Properties 1-4, 6)
│       ├── ValidationPropertyTests.cs       (Property 5)
│       ├── TenantIsolationPropertyTests.cs  (Property 7)
│       ├── FilterPropertyTests.cs           (Property 8)
│       ├── BulkEntryPropertyTests.cs        (Property 9)
│       └── CsvImportPropertyTests.cs        (Properties 10-11)
├── Integration/
│   ├── Repositories/
│   │   ├── SupplierRepositoryTests.cs
│   │   ├── ExpenseCategoryRepositoryTests.cs
│   │   └── PurchaseRepositoryTests.cs
│   └── Controllers/
│       ├── SupplierControllerTests.cs
│       ├── ExpenseCategoryControllerTests.cs
│       └── PurchaseControllerTests.cs
└── Helpers/
    ├── TestDbContextFactory.cs
    └── Generators/
        ├── PurchaseGenerators.cs
        └── CsvGenerators.cs
```
