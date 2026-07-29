# Phase 3 — Development Timetable

## Overview

Phase 3 delivers **Market Expansion** and **Operational Completeness** features. This includes Multi-Currency support for international businesses, API/Integrations Layer for ecosystem connectivity, and the Payroll module — the platform's most comprehensive operational addition.

**Prerequisites:** Phase 2 must be complete (Client Portal, Document Attachments, Activity Timeline, Audit Log, Business Applications Tracker).

---

## Module 12: Multi-Currency Support

**Effort:** High | **Dependencies:** Existing invoice/payment/purchase infrastructure

- [ ] 12.1 Add Currency entity and seed ISO 4217 codes (EUR, GBP, USD, etc.)
- [ ] 12.2 Add base currency configuration per business
- [ ] 12.3 Add currency selection on invoice/quotation creation
- [ ] 12.4 Add exchange rate service (manual entry or API-fetched)
- [ ] 12.5 Update revenue KPIs to convert to base currency
- [ ] 12.6 Update P&L to handle multi-currency transactions
- [ ] 12.7 Update cash flow forecasting to handle currency conversion
- [ ] 12.8 Add currency display formatting throughout UI
- [ ] 12.9 Add plan permission gate (`multi_currency` module key — Enterprise only)
- [ ] 12.10 End-to-end testing: create invoice in USD → payment in USD → report in EUR

---

## Module 13: API / Integrations Layer

**Effort:** High | **Dependencies:** All existing modules (API exposes them)

- [ ] 13.1 Design REST API architecture (versioned, OAuth2/API key auth)
- [ ] 13.2 Create API key management UI (generate, revoke, scope per module)
- [ ] 13.3 Implement core endpoints: Customers, Invoices, Payments, Purchases
- [ ] 13.4 Implement webhook subscription management (event types, endpoints, signing)
- [ ] 13.5 Implement webhook delivery with retry logic
- [ ] 13.6 Create API documentation (Swagger/OpenAPI)
- [ ] 13.7 Add rate limiting per API key
- [ ] 13.8 Add plan permission gate (`api` and `webhooks` module keys — Enterprise only)
- [ ] 13.9 End-to-end testing: API call → data mutation → webhook fired → verified

---

## Module 14: Payroll / Payslips

**Effort:** Very High | **Dependencies:** Business entity, Employee data, P&L integration

- [ ] 14.1 Design payroll data model (`[payroll]` schema)
- [ ] 14.2 Create `Employee` table (Name, Position, SocialInsuranceNumber, StartDate, EndDate, SalaryType, BaseSalary, BankAccount, IsActive)
- [ ] 14.3 Create `DeductionType` table (Name, Code, IsPercentage, Rate, IsEmployeeContribution, IsEmployerContribution, IsActive, Country)
- [ ] 14.4 Create `PayslipPeriod` table (BusinessId, Year, Month, Status: Draft/Finalised, ProcessedAtUtc)
- [ ] 14.5 Create `Payslip` table (EmployeeId, PeriodId, GrossSalary, TotalEmployeeDeductions, NetSalary, TotalEmployerContributions)
- [ ] 14.6 Create `PayslipLine` table (PayslipId, DeductionTypeId, BaseAmount, Rate, CalculatedAmount, IsEmployeePortion, IsEmployerPortion)
- [ ] 14.7 Seed default deduction types for Cyprus (Social Insurance 8.3%/8.3%, GHS 2.65%/2.90%, Redundancy Fund 1.2%, Industrial Training 0.5%, Cohesion Fund 2%, PAYE Income Tax bands)
- [ ] 14.8 Create employee management UI (CRUD, active/terminated status, salary history)
- [ ] 14.9 Create deduction type configuration UI (business-level overrides of defaults)
- [ ] 14.10 Create payslip period management (open period, process payslips, finalise)
- [ ] 14.11 Implement payslip calculation engine (gross → apply all deduction rules → net + employer contributions)
- [ ] 14.12 Create payslip generation UI (select period → preview all employees → confirm and generate)
- [ ] 14.13 Create individual payslip view (employee sees their breakdown)
- [ ] 14.14 Create payslip PDF generation (branded, A4, print-ready)
- [ ] 14.15 Add employee payslip history view (all payslips for an employee across periods)
- [ ] 14.16 Add annual summary per employee (total gross, total deductions, total net, total employer contributions — for tax returns)
- [ ] 14.17 Add business-level payroll summary per period (total salary cost = gross + employer contributions)
- [ ] 14.18 Integrate with P&L: auto-create OpEx entries when payslips are finalised (Salary + Employer Contributions as expense categories)
- [ ] 14.19 Add employer contribution breakdown report (for Social Insurance filing — links to Business Applications module)
- [ ] 14.20 Add employee statement export (PDF showing all payslips for a date range)
- [ ] 14.21 Add "Send Payslip by Email" to employee (PDF attachment)
- [ ] 14.22 SuperAdmin: seed country-specific deduction templates (Cyprus default, with room for Malta, UK, etc.)
- [ ] 14.23 Add plan permission gate (`payroll` module key — Enterprise only)
- [ ] 14.24 Add soft-gate teaser for Professional users
- [ ] 14.25 Mobile responsive design
- [ ] 14.26 End-to-end testing: add employee → configure deductions → generate payslip → verify calculations → export PDF → verify P&L impact

---

## Build Order & Dependencies

```
Module 12 (Multi-Currency) ←── Independent, can start first
    │
Module 13 (API Layer) ←── Benefits from all modules being stable
    │
Module 14 (Payroll) ←── Most complex, benefits from P&L integration
```

**Recommended sequence:**
1. Module 12 (Multi-Currency) — foundational for international expansion
2. Module 14 (Payroll) — highest business value, longest build time (start early)
3. Module 13 (API Layer) — deliver last when all modules are stable

---

## Completion Criteria

Each module is considered complete when:
- [ ] All sub-tasks checked off
- [ ] Plan permission gating verified (Enterprise only for all Phase 3 modules)
- [ ] Soft-gate teasers visible to lower-tier users
- [ ] Mobile responsive at 375px and 810px
- [ ] No regressions in existing functionality
- [ ] Documentation updated

---

## Post-Phase 3 Milestones

- [ ] All 3 modules complete and verified
- [ ] Landing page updated with full feature set
- [ ] API documentation published
- [ ] Payroll demo-ready for Cyprus-based businesses
- [ ] International deduction templates started (Malta, UK)
- [ ] Phase 4 planning begins (Advanced Analytics / COM integration)
