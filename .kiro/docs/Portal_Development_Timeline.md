# Portal Development Timeline

## Status: Phase 1 — Core Platform
Last Updated: 2026-05-26

---

## Completed

| # | Milestone | Completed |
|---|-----------|-----------|
| ✅ | Database Schema — 8 schemas, 18 tables, EF Core layer, FullSchema_Deploy.sql | 2026-05-11 |
| ✅ | Platform Foundation — MVC project, DI, Identity, invitation flow, layout, logging, repositories, admin screens | 2026-05-11 |
| ✅ | Customer Registry — Repository, service, controller, views, validation, search/filter | 2026-05-11 |
| ✅ | Quotation Platform — Repositories, service, controller, views, lifecycle state machine, pricing, audit logging | 2026-05-11 |
| ✅ | Invoicing — Quotation-to-invoice conversion, standalone creation, sections, lifecycle, audit logging, views | 2026-05-14 |
| ✅ | Document Soft Delete — IsDeleted/DeletedAtUtc columns, soft-delete service, two-step confirmation UI, listing filters | 2026-05-18 |
| ✅ | Purchase & Expense Tracking — Suppliers, categories, purchases with Origin Type (Domestic/EU RC/Non-EU), bulk entry with autocomplete+inline create, CSV import, bidirectional amount calculation, property-based tests | 2026-05-20 |
| ✅ | VAT Submissions — Period generation from registration config, output/input/net VAT computation, submission tracking, mark-as-submitted workflow, audit logging | 2025-07-17 |
| ✅ | Supplier Dashboard — Analytics page with KPIs, spend share donut chart, monthly/period bar charts, paginated purchases table, period filter, sidebar navigation | 2025-07-22 |
| ✅ | Revenue Control — Payment recording, financial status engine, receivables tracking, revenue dashboard with KPIs/charts, VAT integration, property-based tests, integration tests | 2025-07-22 |
| ✅ | Product Catalog — Product schema, CRUD, autocomplete, auto-population, price history, management UI with KPIs/charts, integration into Invoice/Quotation forms, property-based tests (21 properties) | 2025-07-25 |
| ✅ | Customer Statement of Account — Statement generation, PDF export, email with attachment, email history, customer registry pagination, audit logging, navigation integration | 2025-07-25 |
| ✅ | Serilog Structured Logging — Dedicated Portal.Logging database, MSSqlServer sink, LoggingEnrichmentMiddleware (UserId/BusinessId), SelfLog, migration script, property-based tests | 2026-05-26 |
| ✅ | Audit & System Administration — EF Core SaveChangesInterceptor, audit log viewer with filtered/paginated search, super admin user management, per-module permission controls, property-based tests (13 properties) | 2026-07-14 |
| ✅ | System Logs Viewer — Dedicated LoggingDbContext for Portal.Logging DB, SystemLogQueryRepository, filtered/paginated search by level/date/user/correlation ID, expandable detail rows with exception/stack trace, navigation integration | 2025-07-28 |
| ✅ | Credit Notes — Credit note creation against source invoices, lifecycle (Draft → Issued → Applied → Voided), line items, amount computation, number generation, VAT period assignment, application to invoices, void with reversal, property-based tests | 2026-05-28 |
| ✅ | Invoice Line Product Type & Reverse Charge — ProductType lookup table (Services/Goods), ProductTypeId on Product with derivation on quotation lines, immutable snapshot on invoice lines, IsReverseCharge flag with VatRate=0% enforcement, quotation-to-invoice conversion preservation, property-based tests (7 properties) | 2025-07-28 |

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
| [x] | 4.1 | Create PaymentRepository | Module 0 | 2025-07-22 |
| [x] | 4.2 | Create IInvoiceFinancialStatusService (compute TotalPaid, Outstanding, FinancialStatus) | 4.1, Module 3 | 2025-07-22 |
| [x] | 4.3 | Create IPaymentApplicationService (validate, create, void payments) | 4.1, 4.2 | 2025-07-22 |
| [x] | 4.4 | Implement payment validation rules (amount > 0, invoice issued, not exceeding balance) | 4.3 | 2025-07-22 |
| [x] | 4.5 | Implement financial status recalculation after payment entry/void | 4.2 | 2025-07-22 |
| [x] | 4.6 | Implement overdue detection (DueDate < Today AND Outstanding > 0) | 4.2 | 2025-07-22 |
| [x] | 4.7 | Create IReceivablesQueryService (dashboard summaries, overdue list, outstanding list) | 4.2 | 2025-07-22 |
| [x] | 4.8 | Create RevenueController (dashboard, receivables, payment entry) | 4.3, 4.7 | 2025-07-22 |
| [x] | 4.9 | Build Revenue Dashboard UI (outstanding total, overdue amount, paid this month, KPI cards) | 4.8 | 2025-07-22 |
| [x] | 4.10 | Build Receivables list UI (filterable: unpaid, partial, overdue, paid) | 4.8 | 2025-07-22 |
| [x] | 4.11 | Build Invoice detail with payment history table | 4.8, 3.11 | 2025-07-22 |
| [x] | 4.12 | Build Add Payment modal/screen | 4.8 | 2025-07-22 |
| [x] | 4.13 | Implement payment void (soft-delete via IsVoided flag, recalculate status) | 4.3 | 2025-07-22 |
| [x] | 4.14 | Add audit logging for payment events | 4.3 | 2025-07-22 |

---

### Module 5: Purchase & Expense Tracking

**Goal:** Record business expenses with VAT tracking, categorised by supplier and expense type, with Purchase Origin Type classification (Domestic, EU Reverse Charge, Non-EU).

| Done | # | Task | Dependencies | Completed |
|------|---|------|-------------|-----------|
| [x] | 5.1 | Create SupplierRepository and ExpenseCategoryRepository | Module 0 | 2026-05-20 |
| [x] | 5.2 | Create PurchaseRepository | 5.1 | 2026-05-20 |
| [x] | 5.3 | Create ISupplierService and IExpenseCategoryService | 5.1 | 2026-05-20 |
| [x] | 5.4 | Create IPurchaseService (CRUD, VAT calculation, bulk operations) | 5.2 | 2026-05-20 |
| [x] | 5.5 | Implement Purchase Origin Type handling (Domestic/EU RC/Non-EU with PurchaseOriginType lookup table) | 5.4 | 2026-05-20 |
| [x] | 5.6 | Create PurchaseController, SupplierController, ExpenseCategoryController | 5.3, 5.4 | 2026-05-20 |
| [x] | 5.7 | Build Supplier management UI (list, create, edit, deactivate) | 5.6 | 2026-05-20 |
| [x] | 5.8 | Build ExpenseCategory management UI | 5.6 | 2026-05-20 |
| [x] | 5.9 | Build Purchase list UI (filterable by supplier, category, origin type, date range) | 5.6 | 2026-05-20 |
| [x] | 5.10 | Build Purchase create/edit form (with origin type selector, bidirectional amount calculation) | 5.6 | 2026-05-20 |
| [x] | 5.11 | Build Bulk Entry view (spreadsheet grid with autocomplete + inline create for suppliers/categories) | 5.6 | 2026-05-20 |
| [x] | 5.12 | Build CSV Import view (upload, preview, confirm) | 5.6 | 2026-05-20 |
| [x] | 5.13 | Create CsvImportService (parsing, name matching, validation) | 5.4 | 2026-05-20 |
| [x] | 5.14 | Add audit logging for purchase entries | 5.4 | 2026-05-20 |
| [x] | 5.15 | DI registration and module access wiring | 5.6 | 2026-05-20 |
| [x] | 5.16 | Property-based tests (11 properties: VAT logic, validation, tenant isolation, filtering, batch atomicity, CSV round-trip, case-insensitive matching) | 5.4 | 2026-05-20 |

---

### Module 6: VAT Submissions

**Goal:** Calculate VAT periods from registration config, track submissions per period.

| Done | # | Task | Dependencies | Completed |
|------|---|------|-------------|-----------|
| [x] | 6.1 | Create VatSubmissionPeriodRepository and VatSubmissionRepository | Module 0 | 2025-07-17 |
| [x] | 6.2 | Create IVatPeriodGenerationService (derive periods from VatRegistrationDate + VatPeriodLengthInMonths) | 6.1 | 2025-07-17 |
| [x] | 6.3 | Implement period generation algorithm (contiguous, non-overlapping, correct duration) | 6.2 | 2025-07-17 |
| [x] | 6.4 | Create IVatSubmissionService (create submission, compute totals from invoices + purchases in period) | 6.1, Module 3, Module 5 | 2025-07-17 |
| [x] | 6.5 | Compute TotalOutputVat (from issued invoices in period) | 6.4 | 2025-07-17 |
| [x] | 6.6 | Compute TotalInputVat (from purchases in period, excluding EU reverse charge) | 6.4 | 2025-07-17 |
| [x] | 6.7 | Compute NetVatPayable (Output - Input) | 6.4 | 2025-07-17 |
| [x] | 6.8 | Create VatController (periods list, submission detail, mark as submitted) | 6.4 | 2025-07-17 |
| [x] | 6.9 | Build VAT periods list UI (showing period ranges, submission status) | 6.8 | 2025-07-17 |
| [x] | 6.10 | Build VAT submission detail screen (output/input/net breakdown) | 6.8 | 2025-07-17 |
| [x] | 6.11 | Add audit logging for VAT submissions | 6.4 | 2025-07-17 |

---

### Module 7: Audit & System Administration

**Goal:** Provide audit trail visibility and system-level admin tools.

| Done | # | Task | Dependencies | Completed |
|------|---|------|-------------|-----------|
| [x] | 7.1 | Implement automatic audit logging interceptor (EF Core SaveChanges override or interceptor) | Module 0 | 2026-07-14 |
| [x] | 7.2 | Create IAuditLogQueryService (search by table, action, user, date range) | 7.1 | 2026-07-14 |
| [x] | 7.3 | Create AuditController (admin-only access) | 7.2 | 2026-07-14 |
| [x] | 7.4 | Build Audit log viewer UI (searchable, filterable, paginated) | 7.3 | 2026-07-14 |
| [x] | 7.5 | Implement super admin module access management (grant/revoke module access per user) | Module 0 | 2026-07-14 |
| [x] | 7.6 | Build admin user management screen | 7.5 | 2026-07-14 |

---

### Module 8: Customer Statement of Account

**Goal:** Generate a per-customer statement for a selected period showing all invoices issued, payments received, and running balance — available as an on-screen view and a downloadable PDF.

| Done | # | Task | Dependencies | Completed |
|------|---|------|-------------|-----------|
| [x] | 8.1 | Create ICustomerStatementService (query invoices + payments for a customer within a selected period) | Module 4 | 2025-07-25 |
| [x] | 8.2 | Build StatementLineDto model (date, type [Invoice/Payment], reference, description, debit, credit, running balance) | 8.1 | 2025-07-25 |
| [x] | 8.3 | Implement statement generation logic (opening balance from prior periods, invoice debits, payment credits with method/reference, closing balance) | 8.1, 8.2 | 2025-07-25 |
| [x] | 8.4 | Create CustomerStatementController (Index with customer/period filters, Generate action) | 8.3 | 2025-07-25 |
| [x] | 8.5 | Build Customer Statement UI — filter panel (customer dropdown, period selector or date from/to) | 8.4 | 2025-07-25 |
| [x] | 8.6 | Build Customer Statement UI — statement table (date, type, reference, description, debit, credit, balance columns) with invoice and payment rows interleaved chronologically | 8.4 | 2025-07-25 |
| [x] | 8.7 | Build statement header section (customer details, statement period, opening balance, total invoiced, total paid, closing balance summary) | 8.6 | 2025-07-25 |
| [x] | 8.8 | Implement PDF export (HTML-to-PDF using existing rendering pattern or DinkToPdf) | 8.6 | 2025-07-25 |
| [x] | 8.9 | Add "Download PDF" and "Email Statement" action buttons | 8.8 | 2025-07-25 |
| [x] | 8.10 | Add statement access from Customer detail and Revenue Control screens | 8.4 | 2025-07-25 |
| [x] | 8.11 | Add audit logging for statement generation events | 8.4 | 2025-07-25 |

---

## Suggested Execution Order

```
Module 0 (Foundation) → Module 1 (Customers) → Module 2 (Quotations) →
Module 3 (Invoicing) → Module 4 (Revenue Control) → Module 5 (Purchases) →
Module 6 (VAT) → Module 7 (Audit/Admin) → Module 8 (Customer Statements)
```

Modules 5 and 6 can run in parallel with Module 4 since they share no direct dependencies beyond the foundation.

---

### Module 9: System Logs Viewer

**Goal:** Provide SuperAdmin visibility into application-level logs (errors, warnings, request activity) from the Portal.Logging database, enabling operational monitoring and debugging without direct database access.

| Done | # | Task | Dependencies | Completed |
|------|---|------|-------------|-----------|
| [x] | 9.1 | Create SystemLogQueryRepository (query [dbo].[Logs] from Portal.Logging DB) | Module 0 | 2025-07-28 |
| [x] | 9.2 | Create ISystemLogQueryService (filtered, paginated access by level, date, user, correlation ID) | 9.1 | 2025-07-28 |
| [x] | 9.3 | Create SystemLogsController (SuperAdmin-only, /Admin/SystemLogs) | 9.2 | 2025-07-28 |
| [x] | 9.4 | Build System Logs viewer UI (filterable by level, date range, user, correlation ID; paginated; expandable detail with exception/stack trace) | 9.3 | 2025-07-28 |
| [x] | 9.5 | Add System Logs link to Administration sidebar section | 9.4 | 2025-07-28 |
| [x] | 9.6 | DI registration for SystemLogQueryRepository and ISystemLogQueryService | 9.2 | 2025-07-28 |

---

### Module 10: Subscription & Self-Service Onboarding

**Goal:** Enable new tenants to self-register via a public landing page, select a subscription plan, pay via Stripe, and start working immediately — replacing the invitation-only model for initial tenant creation while preserving it for adding users within a tenant.

| Done | # | Task | Dependencies | Completed |
|------|---|------|-------------|-----------|
| [ ] | 10.1 | Design and build public landing page (hero, features, pricing cards, CTA) | Module 0 | |
| [ ] | 10.2 | Create subscription schema: `[subscription].[Plan]`, `[subscription].[Subscription]`, `[subscription].[PlanModule]` | Module 0 | |
| [ ] | 10.3 | Seed Plan data (Starter, Business, Enterprise) with module mappings | 10.2 | |
| [ ] | 10.4 | Integrate Stripe: create Checkout Session on plan selection | 10.2 | |
| [ ] | 10.5 | Implement Stripe webhook handler (`checkout.session.completed`, `invoice.paid`, `invoice.payment_failed`, `customer.subscription.updated`, `customer.subscription.deleted`) | 10.4 | |
| [ ] | 10.6 | Auto-provision tenant on successful payment: create Business, first User (owner), assign modules per plan | 10.5 | |
| [ ] | 10.7 | Build post-signup setup wizard (business name, VAT number, address, logo, currency) | 10.6 | |
| [ ] | 10.8 | Update module access middleware: check subscription plan includes module before checking user-level permission | 10.2, Module 7 | |
| [ ] | 10.9 | Build subscription status indicator in sidebar/topbar (plan name, days remaining) | 10.8 | |
| [ ] | 10.10 | Implement grace period and "billing required" lockout screen for lapsed subscriptions | 10.5 | |
| [ ] | 10.11 | Integrate Stripe Customer Portal for self-service billing management (plan changes, payment methods, invoice history) | 10.4 | |
| [ ] | 10.12 | Build super admin "Subscriptions" view (all tenants, plans, payment status, MRR) | 10.2 | |
| [ ] | 10.13 | Routing: unauthenticated `/` → landing page; authenticated `/` → redirect to Dashboard | 10.1 | |
| [ ] | 10.14 | Property-based tests (plan-module mapping, provisioning atomicity, webhook idempotency, access gating) | 10.8 | |

---

## Phase 2 — Subscription Maturity & Advanced Modules

**Goal:** Differentiate subscription tiers with advanced features, add usage-based controls, and build the modules that justify higher-tier plans.

| Done | Module | Description | Prerequisite | Completed |
|------|--------|-------------|-------------|-----------|
| [ ] | Inventory Management | Stock tracking from purchases, stock levels, reorder points, warehouse locations | Module 5, Module 10 | |
| [ ] | Bill of Materials (BOM) | Product composition, cost roll-up, multi-level BOM, production cost estimation | Inventory, Product Catalog | |
| [ ] | Production Planning | Work orders, BOM-to-production flow, scheduling, completion tracking | BOM, Inventory | |
| [ ] | Advanced Analytics (Insights) | Operational KPI cards, trend signals, margin analysis, story engine | Modules 2-5 complete | |
| [ ] | Seat-Based Pricing | Per-user billing within plans, user count limits per tier, overage handling | Module 10 | |
| [ ] | Usage Limits & Metering | Document count limits per plan (invoices/month, quotations/month), upgrade prompts | Module 10 | |
| [ ] | Plan Upgrade/Downgrade Flow | In-app plan switching with prorated billing via Stripe, module access adjustment | Module 10 | |
| [ ] | Customer Account Credits | Standalone credit notes (no invoice reference), running credit balance, apply to future invoices | Module 4, Credit Notes | |
| [ ] | Bank Feed Integration | Automated payment matching from bank statements | Module 4 | |
| [ ] | Reminder Workflow | Automated overdue notifications via email, configurable schedules | Module 4 | |

---

## Phase 3 — Platform Scale & Ecosystem

**Goal:** Enterprise-grade features, external integrations, and platform extensibility for large-scale operations.

| Done | Module | Description | Prerequisite | Completed |
|------|--------|-------------|-------------|-----------|
| [ ] | ERP Integration | Export to external accounting systems (Xero, QuickBooks, SAP) | Modules 3-6 | |
| [ ] | JDS Integration | Production orchestration connection (3 Inventors internal) | Architecture review | |
| [ ] | COM Pipeline | Canonical Operational Model ingestion | All Phase 2 modules | |
| [ ] | API Access & Webhooks | RESTful API for external integrations, webhook notifications for events | Module 10 (Enterprise tier) | |
| [ ] | Multi-Currency Support | Per-invoice currency, exchange rate management, multi-currency reporting | Modules 3-4 | |
| [ ] | Mobile Responsive Layout | Hamburger menu, collapsible sidebar, mobile-friendly topbar, touch-optimized tables | Module 0 | |
| [ ] | White-Label / Custom Branding | Per-tenant branding (logo, colors, custom domain CNAME) | Module 10 (Enterprise tier) | |
| [ ] | Marketplace & Add-Ons | Optional paid add-on modules, third-party integrations marketplace | Module 10 | |
| [ ] | Custom Integrations | Tenant-specific integration configurations, custom field mapping | API Access | |

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
