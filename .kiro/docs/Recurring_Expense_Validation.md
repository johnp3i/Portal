# Recurring Expense Validation

## Overview

A feature that allows business users to define recurring expense expectations per supplier, and validates that those expenses have been recorded during a VAT period. This prevents forgotten or unrecorded purchases from slipping through during VAT submission.

## Problem Statement

Many business expenses are predictable and recurring — telecommunications (monthly), electricity (bimonthly), hosting (monthly). Because most invoices are digital, they are easily lost, forgotten, or left unrecorded. During VAT submission, the user currently has to mentally verify that all expected expenses are present. This is time-consuming and error-prone.

## Solution

Allow the user to configure recurring expense rules per supplier. The system validates purchases against these rules and reports any missing or incomplete entries — both within the VAT submission flow and as a standalone validation view.

---

## Design Decisions

### 1. Rule Scope: Supplier + Category (Category Optional)

Rules are tied to a **Supplier** and optionally to an **Expense Category**.

- **Supplier only**: "I expect a purchase from Cyta every month" — any category counts.
- **Supplier + Category**: "I expect a purchase from DatabaseMart in the Hosting category every month for $225" — distinguishes hosting from SSL/domain purchases with the same supplier.

When a category is specified, only purchases matching both the supplier AND category satisfy the rule. When no category is specified, any purchase from that supplier counts.

### 2. Availability: VAT Submission + Standalone View

The validation is available in **two places**:

1. **VAT Submission view** — An integrated validation panel that runs automatically (or on-demand) before the user submits a VAT period. Shows pass/warn/fail per rule.
2. **Standalone menu option** — An independent navigation item (under the Purchases module) that allows running the validation at any time, independent of VAT submission. This is useful for mid-period checks.

> Note: The name "Health Check" was considered but not confirmed. A more appropriate name will be decided during implementation (e.g., "Expense Monitor", "Recurring Validation", or "Expected Purchases").

### 3. Grace Period

Rules support an optional **grace period** (in days). This accommodates suppliers whose billing date falls near the boundary between VAT periods (e.g., an invoice dated on the 28th may land in the next period depending on processing).

- Default grace period: 0 days (strict matching within the period)
- Configurable per rule (e.g., 5 days, 10 days)
- When a grace period is set, the system extends the lookup window by that many days beyond the period boundaries

---

## Validation Levels

### Level 1 — Frequency-Based (Core)

"I expect at least N purchases from this supplier every X months."

| Field | Description |
|-------|-------------|
| Supplier | Which supplier to monitor |
| Category | (Optional) Which expense category to match |
| Frequency | Every 1 month, 2 months, 3 months, etc. |
| Description | User label, e.g., "Monthly phone bill" |
| Grace Period | Days of tolerance for period boundaries |

**Validation logic:**
- For a 3-month VAT period with a monthly rule (frequency = 1): expects 3 purchases
- For a 3-month VAT period with a bimonthly rule (frequency = 2): expects at least 1 purchase (period covers 1.5 cycles, rounded down to 1)
- For a 6-month period with a bimonthly rule: expects 3 purchases

**Formula:** `expectedCount = floor(periodMonths / frequencyMonths)`  
Minimum expected count is always 1 if the period is >= the frequency.

### Level 2 — Amount-Anchored (Advanced)

"I expect a purchase from this supplier of approximately X amount."

| Field | Description |
|-------|-------------|
| Expected Amount | The amount to look for (e.g., 225.00) |
| Tolerance % | Acceptable variance (e.g., 5% allows 213.75–236.25) |

**Validation logic:**
- In addition to frequency checks, verifies that at least one purchase per expected occurrence matches the amount within tolerance
- Useful for fixed-cost services (hosting, subscriptions, retainers)

---

## Data Model

### Table: `[Billing].[SupplierRecurringRule]`

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| Id | INT IDENTITY | NOT NULL | Primary key |
| BusinessId | INT | NOT NULL | FK to Business |
| SupplierId | INT | NOT NULL | FK to Supplier |
| ExpenseCategoryId | INT | NULL | FK to ExpenseCategory (optional) |
| FrequencyMonths | INT | NOT NULL | Billing frequency (1=monthly, 2=bimonthly, 3=quarterly) |
| ExpectedAmount | DECIMAL(18,2) | NULL | Expected purchase amount (optional) |
| AmountTolerancePercent | DECIMAL(5,2) | NULL | Tolerance for amount matching (default 5%) |
| GracePeriodDays | INT | NOT NULL | Days of tolerance at period boundaries (default 0) |
| Description | NVARCHAR(200) | NOT NULL | User-facing label |
| IsActive | BIT | NOT NULL | Whether this rule is active |
| CreatedAtUtc | DATETIME | NOT NULL | Audit timestamp |

### Relationships

- `SupplierId` → `[dbo].[Suppliers].Id`
- `ExpenseCategoryId` → `[dbo].[ExpenseCategories].Id`
- `BusinessId` → `[dbo].[Businesses].Id`

---

## Validation Output

Each rule produces one of three statuses:

| Status | Icon | Meaning |
|--------|------|---------|
| Pass | ✅ | All expected purchases found (and amount matches if configured) |
| Warning | ⚠️ | Partially fulfilled (e.g., 2 of 3 expected months have a purchase) |
| Fail | ❌ | No matching purchases found for this rule in the period |

### Example Output

```
✅ Cyta (Telecommunications)     — 3/3 months covered
⚠️ EAC (Electricity)            — 1/2 expected billings recorded
❌ DatabaseMart (Hosting, $225)  — hosting fee not found this period
✅ DatabaseMart (SSL/Domain)     — 1/1 purchase recorded
```

---

## UX Placement

### 1. Supplier Configuration

On the Supplier detail/edit page, add a section **"Recurring Expense Rules"** where the user can:
- Add new rules
- Edit existing rules
- Activate/deactivate rules
- View rule history

### 2. VAT Submission View (Integrated)

On the VAT period submission page, add a collapsible validation panel:
- Runs validation automatically when the page loads (or on button click)
- Shows the pass/warn/fail list
- Non-blocking — the user can still submit even with warnings/failures
- Option to dismiss individual warnings

### 3. Standalone View (Independent Navigation)

A dedicated page accessible from the Purchases module navigation:
- Allows selecting a date range or VAT period to validate against
- Shows the full validation report
- Useful for mid-period checks without navigating to VAT submission

---

## Behaviour Rules

1. Rules are **advisory only** — they never block VAT submission
2. Rules are **per-business** — each business configures its own recurring expectations
3. A supplier can have **multiple rules** (e.g., DatabaseMart has one for hosting and one for SSL)
4. When a rule is deactivated, it is excluded from validation but retained for history
5. Amount tolerance applies as: `expectedAmount * (1 - tolerance/100)` to `expectedAmount * (1 + tolerance/100)`
6. Grace period extends the lookup window: if a period ends on 31 Aug and grace is 5 days, purchases up to 5 Sep also count for that period

---

## Future Enhancements

- **Dashboard widget**: "Overdue Recurring Expenses" alert showing rules that are overdue mid-period
- **Notifications**: Email/in-app alert when an expected expense hasn't been recorded by a configurable day of the month
- **Auto-suggest**: When creating a purchase, suggest the recurring rule description if supplier + category matches a rule
- **Trend detection**: Flag when a supplier's actual billing deviates from the expected amount over time (price increases)

---

## Open Questions

- [ ] Final name for the standalone validation view (candidates: "Expense Monitor", "Recurring Validation", "Expected Purchases")
- [ ] Should the standalone view be a sub-item under Purchases, or a top-level navigation item?
- [ ] Should deactivated rules be soft-deleted or retained indefinitely?
