# Payroll / Payslips — Phased Development Timetable

## Overview

The Payroll module is the platform's most comprehensive operational addition. It enables businesses to manage employees, configure deductions with historical rate tracking, generate payslips with multiple earning types, and integrate with both the P&L and Compliance modules.

The module is scoped into 4 sequential phases, each independently deliverable. Phases build on each other — Phase A must complete before B, etc.

**Subscription Tier:** Enterprise only (`payroll` module key)

---

## Key Design Decisions

| # | Decision | Detail |
|---|----------|--------|
| 1 | Phased sub-scoping | A → B → C → D, each deliverable independently |
| 2 | PAYE included | As an option per employee (not all employees have PAYE — depends on annual salary threshold). Cyprus progressive bands. |
| 3 | All salary types | Basic, Overtime, Bonus, Paid Holidays, Part-time supplement — each a separate earning line type |
| 4 | Earnings summary | Break down extra payments by type (holidays, bonuses, overtime) with period totals |
| 5 | Preview/Finalise/Unlock | Draft → Preview → Finalised → Unlocked (edit) → Re-finalised. Full audit on unlock. |
| 6 | Rate history | `DeductionRateHistory` table with EffectiveFromUtc. Engine picks the rate that was active on the payslip's period date. |
| 7 | Batch payslip generation | Select period → all active employees → generate all → preview → confirm |
| 8 | Compliance integration | Monthly Social Insurance total feeds into the compliance filing's EstimatedAmount |
| 9 | P&L integration | Finalised payslips auto-create expense entries (Salaries + Employer Contributions as distinct categories) |
| 10 | Audit trail on edits | Unlocking a finalised payslip records: who, when, what changed (old → new values). Warning that P&L is affected. |

---

## Reference Data

### Cyprus Payslip Structure (from reference CSV)

**Employee Deductions (taken from gross salary):**
| Deduction | Rate | Type |
|-----------|------|------|
| Social Insurance | 8.8% | Employee |
| GESY (General Healthcare System) | 2.65% | Employee |

**Employer Contributions (additional cost to business):**
| Contribution | Rate | Type |
|-------------|------|------|
| Social Insurance | 8.8% | Employer |
| Redundancy Fund | 1.2% | Employer |
| Industrial Training Fund | 0.5% | Employer |
| Social Cohesion Fund | 2.0% | Employer |
| GESY | 2.9% | Employer |

**Calculation Example (€1,000 gross):**
- Total Employee Deductions: €114.50 (8.8% + 2.65% = 11.45%)
- Net Salary: €885.50
- Total Employer Contributions: €154.00 (8.8% + 1.2% + 0.5% + 2.0% + 2.9% = 15.4%)
- Total Cost to Business: €1,154.00

### PAYE Income Tax Bands (Cyprus 2024)

| Taxable Income (Annual) | Rate |
|--------------------------|------|
| €0 – €19,500 | 0% |
| €19,501 – €28,000 | 20% |
| €28,001 – €36,300 | 25% |
| €36,301 – €60,000 | 30% |
| Over €60,000 | 35% |

PAYE is optional per employee — only applies when the employee's projected annual income exceeds €19,500.

---

## Phase A: Core Engine (Minimum Viable Payslip)

**Effort:** Very High | **Dependencies:** None (standalone within Portal)

### Scope

- Employee management (CRUD with full profile)
- Earning types configuration (Basic, Overtime, Bonus, Paid Holidays, Part-time)
- Deduction types with **rate history** (effective dates, employee vs employer portion)
- Payslip period management (Year/Month, Draft → Preview → Finalised)
- Calculation engine: Sum earnings by type → apply deductions at historical rate → compute Net + Employer contributions
- Batch payslip generation (select period → all active employees → generate → preview → confirm)
- Individual payslip view with full breakdown by earning type and deduction

### Data Model (Phase A)

**Schema:** `[payroll]`

| Table | Purpose |
|-------|---------|
| `Department` | Id, BusinessId, Name, IsActive, CreatedAtUtc — Groups employees for reporting |
| `Employee` | Id, BusinessId, DepartmentId (FK, nullable), Name, Position, SocialInsuranceNumber, IdNumber, Phone, Email, StartDate, EndDate, SalaryType, BaseSalary, HourlyRate (nullable, for overtime calc), BankAccount, IsActive, CreatedAtUtc |
| `EarningType` | Id, Name, Code, IsActive, SortOrder (e.g., Basic, Overtime, Bonus, PaidHolidays, PartTime). System-seeded. |
| `DeductionType` | Id, Name, Code, IsPercentage, IsEmployeeContribution, IsEmployerContribution, IsActive, Country, CreatedAtUtc |
| `DeductionRateHistory` | Id, DeductionTypeId (FK), Rate, EffectiveFromUtc, EffectiveToUtc (NULL = current), CreatedAtUtc |
| `PayslipPeriod` | Id, BusinessId, Year, Month, Status (Draft/Preview/Finalised/Unlocked), ProcessedAtUtc, CreatedAtUtc |
| `Payslip` | Id, EmployeeId (FK), PayslipPeriodId (FK), TotalEarnings, TotalEmployeeDeductions, NetSalary, TotalEmployerContributions, ManagerNotes (NVARCHAR(2000), nullable — for comments like "€40 travelling expenses"), Status, CreatedAtUtc |
| `PayslipEarningLine` | Id, PayslipId (FK), EarningTypeId (FK), Description, Amount, OvertimeMultiplier (DECIMAL(4,2), nullable — e.g., 1.5, 4.0), OvertimeHours (DECIMAL(6,2), nullable), CreatedAtUtc |
| `PayslipDeductionLine` | Id, PayslipId (FK), DeductionTypeId (FK), BaseAmount, Rate, CalculatedAmount, IsEmployeePortion, IsEmployerPortion, DeductionRateHistoryId (FK), CreatedAtUtc |

### Sub-tasks (Phase A)

- [ ] A.1 Design and create `[payroll]` schema with all Phase A tables
- [ ] A.2 Seed default earning types (Basic, Overtime, Bonus, Paid Holidays, Part-time)
- [ ] A.3 Seed default Cyprus deduction types with rate history (Social Insurance, GESY, Redundancy Fund, Industrial Training, Social Cohesion)
- [ ] A.4 Create EF Core entities and DbContext configuration
- [ ] A.5 Create Department + Employee CRUD (service + repository + controller + views)
  - Department CRUD: simple table with Name + IsActive per business
  - Employee CRUD: full profile with department assignment, salary type, base salary, hourly rate
- [ ] A.6 Create Deduction Type management UI (view rates, add rate history entry when rate changes)
- [ ] A.7 Create Payslip Period management (open period, close period, status transitions)
- [ ] A.8 Implement calculation engine:
  - For each earning line: if Overtime type → Amount = OvertimeHours × Employee.HourlyRate × OvertimeMultiplier; otherwise use the entered Amount
  - TotalEarnings = SUM of all earning line amounts
  - For each deduction: BaseAmount = TotalEarnings (deductions apply to full gross)
  - CalculatedAmount = BaseAmount × historical rate (for percentage deductions) or fixed amount
  - Historical rate = rate where EffectiveFromUtc <= period date AND (EffectiveToUtc is NULL OR EffectiveToUtc > period date)
  - TotalEmployeeDeductions = SUM of employee-portion calculated amounts
  - NetSalary = TotalEarnings - TotalEmployeeDeductions
  - TotalEmployerContributions = SUM of employer-portion calculated amounts
  - Total Cost to Business = TotalEarnings + TotalEmployerContributions
- [ ] A.9 Create batch payslip generation UI (select period → preview all employees → confirm and generate)
- [ ] A.10 Create individual payslip view (full breakdown: earnings by type, deductions, net, employer contributions)
- [ ] A.11 Support multiple earning lines per payslip (e.g., part-time €600 + holiday pay €150 on same payslip)
- [ ] A.12 Add plan permission gate (`payroll` module key — Enterprise only)
- [ ] A.13 Build verification checkpoint

---

## Phase B: Audit, Unlock, and P&L Integration

**Effort:** High | **Dependencies:** Phase A complete

### Scope

- Unlock/Edit after finalisation with full audit trail
- Audit log: who changed what, when, old value → new value
- Warning system when editing affects P&L
- P&L integration: auto-create expense entries on finalisation (Salaries + Employer Contributions)
- P&L reverse/adjust when a finalised payslip is edited
- Period status: Draft → Preview → Finalised → Unlocked → Re-finalised

### Data Model (Phase B additions)

| Table | Purpose |
|-------|---------|
| `PayslipAuditLog` | PayslipId, UserId, Action (Unlocked/Edited/ReFinalised), FieldName, OldValue, NewValue, Timestamp |

### Sub-tasks (Phase B)

- [ ] B.1 Add `PayslipAuditLog` table and entity
- [ ] B.2 Implement period unlock mechanism (Finalised → Unlocked, requires owner/SuperAdmin)
- [ ] B.3 Implement payslip edit (after unlock) with field-level change tracking
- [ ] B.4 Record audit entries for every field change (old → new)
- [ ] B.5 Implement re-finalisation (Unlocked → Re-finalised) with updated totals
- [ ] B.6 Add P&L integration: on Finalise, create expense records (Salary Cost + Employer Contributions)
- [ ] B.7 Add P&L adjustment: on edit of finalised payslip, reverse old entries + create new entries
- [ ] B.8 Add warning dialog when user attempts to unlock: "Editing will affect P&L for {period}"
- [ ] B.9 Create audit history view per payslip (timeline of changes)
- [ ] B.10 Build verification checkpoint

---

## Phase C: Reporting & Export

**Effort:** Medium | **Dependencies:** Phase A complete (Phase B recommended but not required)

### Scope

- Payslip PDF generation (branded, A4, print-ready)
- Employee payslip history view (all payslips for an employee)
- Annual summary per employee (total gross, deductions, net, employer contributions — for tax returns)
- **Earnings summary by type** (total overtime, bonuses, holiday pay — across employees and periods)
- Business-level period summary (total salary cost per month)
- Send payslip by email (PDF attachment)
- Employee statement export (PDF for date range)

### Sub-tasks (Phase C)

- [ ] C.1 Create payslip PDF template (branded, A4 layout matching reference CSV structure)
- [ ] C.2 Implement PDF generation service (using existing PDF infrastructure)
- [ ] C.3 Create employee payslip history view (filterable by year)
- [ ] C.4 Create annual summary per employee (aggregated totals for tax return preparation)
- [ ] C.5 Create earnings breakdown report (by type: overtime, bonuses, holidays — filterable by period/employee)
- [ ] C.6 Create business-level period summary (all employees: total gross, deductions, net, employer cost)
- [ ] C.7 Implement "Send Payslip by Email" with PDF attachment
- [ ] C.8 Create employee statement export (PDF showing all payslips for a selected date range)
- [ ] C.9 Mobile responsive design for all report views
- [ ] C.10 Build verification checkpoint

---

## Phase D: Integration

**Effort:** Medium | **Dependencies:** Phase A + Business Applications Tracker (Compliance module)

### Scope

- **Compliance module link**: Auto-populate monthly Social Insurance filing's `EstimatedAmount` from payroll employer contributions total
- PAYE Income Tax calculation (Cyprus progressive bands, optional per employee)
- Employer contribution breakdown report (for Social Insurance filing — links to Compliance module)

### Sub-tasks (Phase D)

- [ ] D.1 Implement PAYE calculation engine (progressive tax bands, annual projection from monthly salary)
- [ ] D.2 Add PAYE as an optional employee-level deduction (flag: IsPayeApplicable, with annual salary threshold check)
- [ ] D.3 Add PAYE line to payslip calculation (applied after Social Insurance deductions, before Net)
- [ ] D.4 Create Compliance integration: on payslip finalisation, update the corresponding month's Social Insurance filing EstimatedAmount with total employer Social Insurance contribution
- [ ] D.5 Create employer contribution breakdown report (grouped by deduction type, linked to Compliance filings)
- [ ] D.6 Show expected amount on Compliance filing detail: "Based on {N} employees × {rate}% = €{amount}"
- [ ] D.7 SuperAdmin: seed country-specific deduction templates (Cyprus default, structure for Malta/UK expansion)
- [ ] D.8 Build verification checkpoint

---

## Build Order & Dependencies

```
Phase A (Core Engine) — Must complete first
    │
    ├── Phase B (Audit + P&L) — Requires A.7, A.8, A.9
    │
    ├── Phase C (Reporting) — Requires A.5, A.10
    │
    └── Phase D (Integration) — Requires A.8 + Compliance module
```

**Recommended sequence:** A → B → C → D (sequential, each phase a separate spec)

---

## Resolved Design Questions

| # | Question | Answer |
|---|----------|--------|
| 1 | Part-time + holidays | **Yes.** Multiple earning lines per payslip (e.g., part-time €600 + holiday pay €150 as separate lines). |
| 2 | Overtime calculation | **Multiplier-based with configurable rate.** Default multiplier is 1.5×, but the user can change it per payslip line (e.g., 4× for Christmas/New Year's Eve). The overtime amount = hours × hourly rate × multiplier. |
| 3 | Deduction base amount | **All earnings (total gross).** Deductions apply to the SUM of all earning lines. Additionally, the manager should be able to add **comments/notes per payslip** (e.g., "€40 travelling expenses") — these are informational and do not affect calculations. |
| 4 | Employee grouping | **Yes — Department grouping.** Add a `Department` table (BusinessId, Name). Each employee belongs to a department. Enables cost-per-department reporting in later phases. |
| 5 | Multi-currency | **EUR only for Phase A.** Multi-currency deferred to a future phase. |

---

## Estimated Effort

| Phase | Tasks | Estimated Effort |
|-------|-------|-----------------|
| Phase A | 13 major tasks | ~3–4 weeks |
| Phase B | 10 tasks | ~1.5–2 weeks |
| Phase C | 10 tasks | ~1.5–2 weeks |
| Phase D | 8 tasks | ~1–1.5 weeks |
| **Total** | **41 tasks** | **~7–9 weeks** |

---

## Completion Criteria (Per Phase)

- [ ] All sub-tasks checked off
- [ ] Plan permission gating verified (Enterprise only)
- [ ] Mobile responsive at 375px and 810px
- [ ] No regressions in existing functionality
- [ ] Build passes with zero errors
- [ ] Documentation updated (this file + relevant design docs)
