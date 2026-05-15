# Portal Development Timeline

## Status: Phase 1 — Core Platform
Last Updated: 2026-05-11

---

## Completed

| # | Milestone | Completed |
|---|-----------|-----------|
| ✅ | Database Schema — 8 schemas, 18 tables, EF Core layer, FullSchema_Deploy.sql | 2026-05-11 |
| ✅ | Platform Foundation — MVC project, DI, Identity, invitation flow, layout, logging, repositories, admin screens | 2026-05-11 |
| ✅ | Customer Registry — Repository, service, controller, views, validation, search/filter | 2026-05-11 |
| ✅ | Quotation Platform — Repositories, service, controller, views, lifecycle state machine, pricing, audit logging | 2026-05-11 |
| ✅ | Invoicing — Quotation-to-invoice conversion, standalone creation, sections, lifecycle, audit logging, views | 2026-05-14 |

---

## Phase 1 — Modules

### Module 0: Platform Foundation

**Goal:** Wire up the ASP.NET Core MVC project, DI, authentication, and shared infrastructure.

| Done | # | Task | Dependencies | Completed |
|------|---|------|-------------|-----------|
| [x] | 0.1 | Create ASP.NET Core MVC 8 web project (`Portal.Web`) | None | 2026-05-11 |
| [x] | 0.2 | Register PortalDbContext in DI with SQL Server connection string | Database deployed | 2026-05-11 |
| [x] | 0.3 | Register ICurrentTenantService as scoped service | 0.2 | 2026-05-11 |
| [x] | 0.4 | Configure ASP.NET Core Identity with Membership DB | 0.1 | 2026-05-11 |
| [x] | 0.5 | Implement invitation-only registration flow (super admin invites) | 0.4 | 2026-05-11 |
| [x] | 0.6 | Add BusinessId claim to user authentication token on login | 0.4 | 2026-05-11 |
| [x] | 0.7 | Create shared layout (sidebar + topbar) per MyChair design system | 0.1 | 2026-05-11 |
| [x] | 0.8 | Configure Serilog structured logging | 0.1 | 2026-05-11 |
| [x] | 0.9 | Create GenericStoredProcedureRepository base class | 0.2 | 2026-05-11 |
| [x] | 0.10 | Add Business and BusinessProfile CRUD (admin screens) | 0.2, 0.7 | 2026-05-11 |

---

### Module 1: Customer Registry

**Goal:** Maintain a registry of customers per tenant for use in quotations and invoices.

| Done | # | Task | Dependencies | Completed |
|------|---|------|-------------|-----------|
| [x] | 1.1 | Create CustomerRepository (CRUD operations) | Module 0 | 2026-05-11 |
| [x] | 1.2 | Create ICustomerService interface and implementation | 1.1 | 2026-05-11 |
| [x] | 1.3 | Create CustomerController (list, create, edit, deactivate) | 1.2 | 2026-05-11 |
| [x] | 1.4 | Build Customer list UI (searchable, filterable by IsActive) | 1.3 | 2026-05-11 |
| [x] | 1.5 | Build Customer create/edit form UI | 1.3 | 2026-05-11 |
| [x] | 1.6 | Validation: Name required, email format, tenant isolation | 1.2 | 2026-05-11 |

---

### Module 2: Quotation Platform

**Goal:** Create quotations with line items, manage lifecycle states, and send proposals to customers.

| Done | # | Task | Dependencies | Completed |
|------|---|------|-------------|-----------|
| [x] | 2.1 | Create QuotationRepository and QuotationLineRepository | Module 0 | 2026-05-11 |
| [x] | 2.2 | Create IQuotationService interface and implementation | 2.1 | 2026-05-11 |
| [x] | 2.3 | Implement quotation lifecycle state machine (Draft → Sent → Accepted → Converted → Archived) | 2.2 | 2026-05-11 |
| [x] | 2.4 | Implement line item management (add, edit, remove, reorder) | 2.2 | 2026-05-11 |
| [x] | 2.5 | Implement pricing calculation (Subtotal, TaxAmount, TotalAmount from lines) | 2.4 | 2026-05-11 |
| [x] | 2.6 | Create QuotationController (list, create, edit, status transitions) | 2.2 | 2026-05-11 |
| [x] | 2.7 | Build Quotation list UI (filterable by status, customer, date) | 2.6 | 2026-05-11 |
| [x] | 2.8 | Build Quotation create/edit form with dynamic line items | 2.6 | 2026-05-11 |
| [x] | 2.9 | Build Quotation detail/preview screen | 2.6 | 2026-05-11 |
| [x] | 2.10 | Implement ValidUntil expiry logic | 2.2 | 2026-05-11 |
| [x] | 2.11 | Add audit logging for status transitions | 2.3 | 2026-05-11 |

---

### Module 3: Invoicing (Quotation → Invoice Conversion)

**Goal:** Convert accepted quotations into invoices deterministically, or create standalone invoices.

| Done | # | Task | Dependencies | Completed |
|------|---|------|-------------|-----------|
| [x] | 3.1 | Create InvoiceRepository and InvoiceLineRepository | Module 0 | 2026-05-14 |
| [x] | 3.2 | Create IInvoiceService interface and implementation | 3.1 | 2026-05-14 |
| [x] | 3.3 | Implement Quotation → Invoice conversion service (transactional) | 3.2, Module 2 | 2026-05-14 |
| [x] | 3.4 | Enforce conversion preconditions (status = Accepted, lines exist, no duplicate) | 3.3 | 2026-05-14 |
| [x] | 3.5 | Copy QuotationLines → InvoiceLines as immutable snapshot | 3.3 | 2026-05-14 |
| [x] | 3.6 | Implement idempotency protection (filtered unique index on QuotationId) | 3.3 | 2026-05-14 |
| [x] | 3.7 | Implement standalone invoice creation (no quotation source) | 3.2 | 2026-05-14 |
| [x] | 3.8 | Implement invoice number generation strategy | 3.2 | 2026-05-14 |
| [x] | 3.9 | Create InvoiceController (list, create, convert, detail) | 3.2 | 2026-05-14 |
| [x] | 3.10 | Build Invoice list UI (filterable by status, financial status, customer) | 3.9 | 2026-05-14 |
| [x] | 3.11 | Build Invoice detail screen (line items, totals, source quotation link) | 3.9 | 2026-05-14 |
| [x] | 3.12 | Build "Convert to Invoice" button on Quotation detail (only when Accepted) | 3.3, 2.9 | 2026-05-14 |
| [x] | 3.13 | Add audit logging for invoice creation and status changes | 3.2 | 2026-05-14 |

---

### Module 4: Revenue Control

**Goal:** Track payments against invoices, compute outstanding balances, and provide receivables visibility.

| Done | # | Task | Dependencies | Completed |
|------|---|------|-------------|-----------|
| [ ] | 4.1 | Create PaymentRepository | Module 0 | |
| [ ] | 4.2 | Create IInvoiceFinancialStatusService (compute TotalPaid, Outstanding, FinancialStatus) | 4.1, Module 3 | |
| [ ] | 4.3 | Create IPaymentApplicationService (validate, create, void payments) | 4.1, 4.2 | |
| [ ] | 4.4 | Implement payment validation rules (amount > 0, invoice issued, not exceeding balance) | 4.3 | |
| [ ] | 4.5 | Implement financial status recalculation after payment entry/void | 4.2 | |
| [ ] | 4.6 | Implement overdue detection (DueDate < Today AND Outstanding > 0) | 4.2 | |
| [ ] | 4.7 | Create IReceivablesQueryService (dashboard summaries, overdue list, outstanding list) | 4.2 | |
| [ ] | 4.8 | Create RevenueController (dashboard, receivables, payment entry) | 4.3, 4.7 | |
| [ ] | 4.9 | Build Revenue Dashboard UI (outstanding total, overdue amount, paid this month, KPI cards) | 4.8 | |
| [ ] | 4.10 | Build Receivables list UI (filterable: unpaid, partial, overdue, paid) | 4.8 | |
| [ ] | 4.11 | Build Invoice detail with payment history table | 4.8, 3.11 | |
| [ ] | 4.12 | Build Add Payment modal/screen | 4.8 | |
| [ ] | 4.13 | Implement payment void (soft-delete via IsVoided flag, recalculate status) | 4.3 | |
| [ ] | 4.14 | Add audit logging for payment events | 4.3 | |

---

### Module 5: Purchase & Expense Tracking

**Goal:** Record business expenses with VAT tracking, categorised by supplier and expense type.

| Done | # | Task | Dependencies | Completed |
|------|---|------|-------------|-----------|
| [ ] | 5.1 | Create SupplierRepository and ExpenseCategoryRepository | Module 0 | |
| [ ] | 5.2 | Create PurchaseRepository | 5.1 | |
| [ ] | 5.3 | Create ISupplierService and IExpenseCategoryService | 5.1 | |
| [ ] | 5.4 | Create IPurchaseService (CRUD, VAT calculation support) | 5.2 | |
| [ ] | 5.5 | Implement EU Reverse Charge handling (IsEuReverseCharge = 1, VatAmount = 0) | 5.4 | |
| [ ] | 5.6 | Create PurchaseController, SupplierController, ExpenseCategoryController | 5.3, 5.4 | |
| [ ] | 5.7 | Build Supplier management UI (list, create, edit, deactivate) | 5.6 | |
| [ ] | 5.8 | Build ExpenseCategory management UI | 5.6 | |
| [ ] | 5.9 | Build Purchase list UI (filterable by supplier, category, date range) | 5.6 | |
| [ ] | 5.10 | Build Purchase create/edit form (with EU reverse charge toggle) | 5.6 | |
| [ ] | 5.11 | Add audit logging for purchase entries | 5.4 | |

---

### Module 6: VAT Submissions

**Goal:** Calculate VAT periods from registration config, track submissions per period.

| Done | # | Task | Dependencies | Completed |
|------|---|------|-------------|-----------|
| [ ] | 6.1 | Create VatSubmissionPeriodRepository and VatSubmissionRepository | Module 0 | |
| [ ] | 6.2 | Create IVatPeriodGenerationService (derive periods from VatRegistrationDate + VatPeriodLengthInMonths) | 6.1 | |
| [ ] | 6.3 | Implement period generation algorithm (contiguous, non-overlapping, correct duration) | 6.2 | |
| [ ] | 6.4 | Create IVatSubmissionService (create submission, compute totals from invoices + purchases in period) | 6.1, Module 3, Module 5 | |
| [ ] | 6.5 | Compute TotalOutputVat (from issued invoices in period) | 6.4 | |
| [ ] | 6.6 | Compute TotalInputVat (from purchases in period, excluding EU reverse charge) | 6.4 | |
| [ ] | 6.7 | Compute NetVatPayable (Output - Input) | 6.4 | |
| [ ] | 6.8 | Create VatController (periods list, submission detail, mark as submitted) | 6.4 | |
| [ ] | 6.9 | Build VAT periods list UI (showing period ranges, submission status) | 6.8 | |
| [ ] | 6.10 | Build VAT submission detail screen (output/input/net breakdown) | 6.8 | |
| [ ] | 6.11 | Add audit logging for VAT submissions | 6.4 | |

---

### Module 7: Audit & System Administration

**Goal:** Provide audit trail visibility and system-level admin tools.

| Done | # | Task | Dependencies | Completed |
|------|---|------|-------------|-----------|
| [ ] | 7.1 | Implement automatic audit logging interceptor (EF Core SaveChanges override or interceptor) | Module 0 | |
| [ ] | 7.2 | Create IAuditLogQueryService (search by table, action, user, date range) | 7.1 | |
| [ ] | 7.3 | Create AuditController (admin-only access) | 7.2 | |
| [ ] | 7.4 | Build Audit log viewer UI (searchable, filterable, paginated) | 7.3 | |
| [ ] | 7.5 | Implement super admin module access management (grant/revoke module access per user) | Module 0 | |
| [ ] | 7.6 | Build admin user management screen | 7.5 | |

---

## Suggested Execution Order

```
Module 0 (Foundation) → Module 1 (Customers) → Module 2 (Quotations) →
Module 3 (Invoicing) → Module 4 (Revenue Control) → Module 5 (Purchases) →
Module 6 (VAT) → Module 7 (Audit/Admin)
```

Modules 5 and 6 can run in parallel with Module 4 since they share no direct dependencies beyond the foundation.

---

## Phase 2 — Future Modules (Not Phase 1)

| Done | Module | Description | Prerequisite | Completed |
|------|--------|-------------|-------------|-----------|
| [ ] | Insights | Operational analytics, KPI cards, trend signals, story engine | Modules 2-4 complete | |
| [ ] | JDS Integration | Production orchestration connection | Architecture review | |
| [ ] | ERP Integration | Export to external accounting systems | Modules 3-6 complete | |
| [ ] | COM Pipeline | Canonical Operational Model ingestion | All Phase 1 modules | |
| [ ] | Credit Notes | Issue credit against invoices | Module 4 | |
| [ ] | Customer Statements | Periodic account summaries | Module 4 | |
| [ ] | Bank Feed Integration | Automated payment matching | Module 4 | |
| [ ] | Reminder Workflow | Automated overdue notifications | Module 4 | |

---

## Architecture Constraints (All Modules)

- Controllers handle HTTP only — delegate to services
- Services contain business logic — no direct DB access
- Repositories handle all data access (try/catch with rethrow)
- Financial values are computed, never manually edited
- Tenant isolation enforced via EF Core global query filters
- UI follows MyChair Design System (Manrope headings, Inter body, operational tone)
- No coupling between modules (loose coupling via shared entities only)
- Audit trail for all significant data changes
