# 3 Inventors Portal — Platform Description

## Document Purpose

This document provides a complete description of the 3 Inventors Portal platform — its features, architecture, design system, subscription model, and technical conventions. It serves as a reference for:
- New team members onboarding to the project
- AI agents working on the codebase
- Stakeholders evaluating the platform's capabilities
- Future integration partners

---

## 1. Platform Identity

| Property | Value |
|----------|-------|
| **Product Name** | 3 Inventors Portal |
| **URL** | portal.3inventors.com |
| **Type** | Multi-tenant SaaS back-office platform |
| **Organization** | 3 Inventors Limited (Operational Intelligence Company) |
| **Location** | Limassol, Cyprus |
| **Target Market** | Small-to-medium businesses needing structured financial operations |

### Vision

The Portal is a modular operational intelligence platform designed for businesses that need structured financial visibility — quotations, invoicing, revenue tracking, expense management, and VAT compliance — without the complexity of enterprise ERP systems.

It is not a generic SaaS tool. It reflects operational seriousness, financial awareness, and long-term reliability. The platform is designed for subscription by any business, not tightly bound to 3 Inventors' internal operations.

---

## 2. Current Features (Phase 1 — Complete)

### 2.1 Customer Registry
- Full CRUD for customer records (name, email, phone, address, city, country)
- Active/inactive status management
- Search and filter capabilities
- Tenant-isolated (each business sees only their customers)

### 2.2 Quotation Platform
- Create quotations with multiple line items and sections
- Lifecycle state machine: Draft → Sent → Accepted → Converted → Archived
- Proposal sections with narrative and line-item types
- Product autocomplete from catalog
- Pricing calculation (subtotal, VAT, discount, total)
- Quotation contacts management
- Proposal sharing via unique links
- Document duplication
- Reverse charge support (forces VAT to 0%)
- Product type display (Services/Goods badge derived from product catalog)
- Audit logging for all status transitions

### 2.3 Invoicing
- Quotation-to-invoice conversion (deterministic, transactional, immutable snapshot)
- Standalone invoice creation (without quotation source)
- Invoice sections (mirroring quotation sections)
- Lifecycle: Draft → Issued → Cancelled
- Invoice number generation (sequential per business)
- Line item management with reverse charge support
- Product type snapshot on invoice lines
- VAT period auto-assignment
- Document soft delete (two-step confirmation)
- Document duplication
- Invoice sharing via unique links
- CSV and PDF export (filter-aware)
- Audit logging

### 2.4 Revenue Control
- Payment recording against invoices
- Financial status engine: Unpaid → Partially Paid → Paid → Overdue → Written Off
- Receivables tracking with overdue detection
- Revenue dashboard with KPI cards and charts
- Payment void with status recalculation
- Integration with VAT submissions

### 2.5 Credit Notes
- Credit note creation against source invoices
- Lifecycle: Draft → Issued → Applied → Voided
- Line items with amount computation
- Credit note number generation
- VAT period assignment
- Application to invoices (reduces outstanding balance)
- Void with reversal
- Validation: credited amount cannot exceed invoice amount

### 2.6 Purchase & Expense Tracking
- Supplier management (CRUD, active/inactive, dashboard)
- Expense category management
- Purchase recording with origin type classification:
  - Domestic
  - EU Reverse Charge
  - Non-EU
  - EU Paid
- Purchase type classification (Asset, Stock, Expense)
- Bidirectional amount calculation (Net ↔ Gross with VAT)
- Bulk entry with autocomplete and inline supplier/category creation
- CSV import (upload, preview, confirm)
- VAT period assignment
- CSV and PDF export (filter-aware)
- Supplier dashboard with KPIs, spend share charts, monthly trends

### 2.7 VAT Submissions
- Automatic period generation from VAT registration date and period length
- Output VAT computation (from issued invoices in period)
- Input VAT computation (from purchases in period, excluding EU reverse charge)
- Net VAT payable calculation
- Mark-as-submitted workflow
- Full Period Report with monthly breakdowns:
  - Sales by month (Net/VAT/Gross)
  - Purchases by month (Net/VAT/Gross)
  - Purchases by origin per month
  - Period totals by origin
- PDF report generation

### 2.8 Product Catalog
- Product master records (code, description, selling price, cost price, VAT rate)
- Product type classification (Services / Goods)
- Supplier association
- Autocomplete integration in quotation and invoice forms
- Auto-population from line items
- Price history tracking
- Management UI with KPIs and usage charts
- Active/inactive status

### 2.9 Customer Statements
- Statement generation for a customer within a selected period
- Opening balance from prior periods
- Invoice debits and payment credits interleaved chronologically
- Running balance calculation
- PDF export
- Email with PDF attachment
- Email history tracking

### 2.10 Administration & Audit
- Super admin user management (invite, approve, deactivate)
- Per-module permission controls (Full / Read-Only access per user)
- EF Core SaveChangesInterceptor for automatic audit logging
- Audit log viewer with filtered/paginated search
- System logs viewer (from dedicated Portal.Logging database)
- Structured logging with Serilog (UserId/BusinessId enrichment)

### 2.11 Cross-Cutting Features
- Multi-tenant isolation (BusinessId claim, scoped services)
- Document soft delete with two-step confirmation
- Document duplication service
- Consistent table action buttons (Theme C: Soft Filled)
- VAT period filtering on Invoice and Purchase tables
- Searchable dropdowns for large datasets

---

## 3. Subscription Tiers (Upcoming — Module 10)

The platform is transitioning from invitation-only to self-service subscription via Stripe.

### Tier Structure

| Tier | Monthly Price | Target Audience |
|------|--------------|-----------------|
| **Starter** | €29/mo | Solo operators, freelancers |
| **Business** | €59/mo | Growing businesses with expense tracking |
| **Enterprise** | €149/mo | Manufacturing, wholesale, multi-location |

### Module Access by Tier

| Module | Starter | Business | Enterprise |
|--------|---------|----------|------------|
| Quotations & Proposals | ✓ | ✓ | ✓ |
| Invoicing & Credit Notes | ✓ | ✓ | ✓ |
| Revenue Control & Payments | ✓ | ✓ | ✓ |
| Customer Registry | ✓ | ✓ | ✓ |
| Product Catalog | ✓ | ✓ | ✓ |
| VAT Submissions | ✓ | ✓ | ✓ |
| Purchases & Expenses | — | ✓ | ✓ |
| Supplier Management | — | ✓ | ✓ |
| Customer Statements | — | ✓ | ✓ |
| Audit Logs | — | ✓ | ✓ |
| CSV Import | — | ✓ | ✓ |
| Inventory Management | — | — | ✓ |
| Bill of Materials (BOM) | — | — | ✓ |
| Production Planning | — | — | ✓ |
| Advanced Analytics | — | — | ✓ |
| API Access | — | — | ✓ |

### User Limits

| Tier | Users Included |
|------|---------------|
| Starter | 1 user |
| Business | Up to 5 users |
| Enterprise | Unlimited |

### Self-Service Onboarding Flow

```
Landing Page → Select Plan → Stripe Checkout → Account Created →
Business Profile Setup → Start Working
```

- Landing page at `portal.3inventors.com/` (unauthenticated visitors)
- Existing login at `/Account/Login` (unchanged)
- Authenticated users redirect to `/Dashboard`
- Stripe handles payment, webhooks provision the tenant
- Existing invitation flow preserved for adding users within a tenant

---

## 4. Technical Architecture

### Stack

| Layer | Technology |
|-------|-----------|
| Web Framework | ASP.NET Core MVC 8 |
| ORM | Entity Framework Core (Database-First) |
| Database | SQL Server (Portal DB + Membership DB + Logging DB) |
| Authentication | ASP.NET Core Identity |
| Logging | Serilog (structured, MSSqlServer sink) |
| PDF Generation | PuppeteerSharp (headless Chromium) |
| Message Bus | MassTransit + RabbitMQ (future) |
| Real-time | SignalR (future) |
| Payments | Stripe (upcoming) |

### Architecture Pattern

```
Controller → Service → Repository → Database
     ↓
  View (Razor)
```

- **Controllers** handle HTTP concerns only — delegate to services
- **Services** contain business logic and orchestration
- **Repositories** handle all data access (raw SQL via `ExecuteSqlRawAsync` / `FromSqlRaw`)
- **Entities** map to database tables
- **ViewModels** shape data for views

### Database Schema

8 schemas organizing 60+ tables:

| Schema | Purpose |
|--------|---------|
| `[dbo]` | Core business tables, membership |
| `[customer]` | Customer registry |
| `[quotation]` | Quotations, lines, sections, contacts |
| `[invoice]` | Invoices, lines, sections, payments |
| `[purchase]` | Purchases, suppliers, expense categories |
| `[product]` | Product catalog, price history, product types |
| `[vat]` | VAT submission periods, submissions |
| `[creditnote]` | Credit notes, lines |

### Key Conventions

- **Tenant isolation**: `ICurrentTenantService` provides `CurrentBusinessId` from the authenticated user's claims
- **Repository pattern**: All repos extend `GenericStoredProcedureRepository<T>` with try/catch rethrow
- **Null safety**: SQL parameters use `?? (object)DBNull.Value` for nullable fields
- **Full table names**: No short aliases in SQL queries (e.g. `[invoice].[Invoice].[Id]` not `i.Id`)
- **BIT columns**: Always prefixed with `Is` or `Has`
- **Foreign keys**: Follow `<TableName>Id` convention
- **Audit timestamps**: Every table has `CreatedAtUtc` (DATETIME, NOT NULL, DEFAULT GETUTCDATE())
- **Idempotent migrations**: All schema changes wrapped in `IF NOT EXISTS` checks

### Property-Based Testing

The platform uses FsCheck for property-based testing — formal correctness properties validated with 100+ random iterations per property. Current coverage: 50+ properties across modules covering:
- Financial computation invariants
- Validation boundary conditions
- Tenant isolation guarantees
- State machine transition rules
- Data integrity constraints

---

## 5. Design System (MyChair)

### Color Palette

| Token | Hex | Usage |
|-------|-----|-------|
| Primary Blue | `#0D5EA6` | Primary actions, links, headings |
| Accent Cyan | `#57B8E8` | Gradients, highlights |
| Success Green | `#129867` | Active states, positive values |
| Warning Amber | `#C8912E` | Warnings, reverse charge badges |
| Danger Red | `#C24A4A` | Destructive actions, overdue, errors |
| Text | `#0B1B28` | Primary text |
| Muted | `#5a6a7a` | Secondary text, labels |
| Background | `#F7FAFC` | Page background |
| Surface | `#EEF4F8` | Card backgrounds, table headers |

### Typography

| Element | Font | Weight | Size |
|---------|------|--------|------|
| Page headings | Manrope | 800 | 42px |
| Section headings | Manrope | 700 | 18-24px |
| Body text | Inter | 400-600 | 14px |
| Labels | Inter | 600-700 | 11-13px (uppercase) |
| Table data | Inter | 400 | 13-14px |

### Layout Structure

Every page follows:
1. **Topbar** — eyebrow label, heading, subtitle, action buttons
2. **Filter section** (optional) — `.glass.card-pad` with `margin-bottom:22px`
3. **Main content** — `.glass.card-pad` containing tables or primary content
4. **Secondary sections** — additional cards with `margin-top:24px`

### Component Patterns

| Component | Class | Description |
|-----------|-------|-------------|
| Content card | `.glass.card-pad` | 20-30px border-radius, soft shadow |
| Primary button | `.btn.btn-primary` | Blue gradient, white text, shadow |
| Secondary button | `.btn.btn-secondary` | White/transparent, subtle border |
| Table action (primary) | `.tbl-action.tbl-action--primary` | Blue text, soft blue background |
| Table action (danger) | `.tbl-action.tbl-action--danger` | Red text, soft red background |
| Status pill | `.pill.pill-green` | Rounded badge for status display |
| Filter field | `.field` with `min-width:180px` | Label + input/select |

### Interaction Patterns

- **AJAX calls**: BlockUI.show() → fetch() → BlockUI.hide() → Swal.fire()
- **Confirmations**: SweetAlert2 for all user confirmations (never native `alert()`)
- **Destructive actions**: Red confirm button (`confirmButtonColor: '#C24A4A'`)
- **Form validation**: Client-side first, server-side always enforced
- **Empty states**: Centered message within the main content card

### Tone

Operational, calm, structured. No startup flashiness. The UI communicates:
- Reliability and professionalism
- Financial seriousness
- Structured execution
- Quiet confidence

---

## 6. Roadmap

### Phase 2 — Subscription Maturity & Advanced Modules

| Module | Description |
|--------|-------------|
| Inventory Management | Stock tracking, reorder points, warehouse locations |
| Bill of Materials (BOM) | Product composition, cost roll-up, multi-level BOM |
| Production Planning | Work orders, scheduling, completion tracking |
| Advanced Analytics | KPI cards, trend signals, margin analysis |
| Seat-Based Pricing | Per-user billing within plans |
| Usage Limits & Metering | Document count limits per plan |
| Bank Feed Integration | Automated payment matching |
| Reminder Workflow | Automated overdue notifications |

### Phase 3 — Platform Scale & Ecosystem

| Module | Description |
|--------|-------------|
| ERP Integration | Export to Xero, QuickBooks, SAP |
| API Access & Webhooks | RESTful API for external integrations |
| Multi-Currency Support | Per-invoice currency, exchange rates |
| White-Label / Custom Branding | Per-tenant branding, custom domains |
| Marketplace & Add-Ons | Third-party integrations |
| Mobile Responsive Layout | Touch-optimized, collapsible sidebar |

---

## 7. Security Model

| Aspect | Implementation |
|--------|---------------|
| Authentication | ASP.NET Core Identity with email/password |
| Authorization | Per-module access (Full / ReadOnly) via `[ModuleAccess]` attribute |
| Tenant isolation | BusinessId claim on JWT, enforced at repository level |
| Registration | Invitation-only (super admin invites) + self-service via Stripe (upcoming) |
| Input validation | Server-side always; parameterized queries (SqlParameter) |
| Secrets | User Secrets in development, never in appsettings.json |
| CSRF | Antiforgery tokens on all POST requests |
| Audit trail | Automatic logging of all data changes via EF Core interceptor |

---

## 8. Deployment & Infrastructure

| Aspect | Detail |
|--------|--------|
| Hosting | Windows Server / IIS (or Azure App Service) |
| Database | SQL Server (3 databases: Portal, Membership, Logging) |
| Domain | portal.3inventors.com |
| SSL | HTTPS enforced |
| Migrations | Sequential numbered SQL scripts (idempotent) |
| CI/CD | GitHub Actions (planned) |

---

*Last updated: 2026-05-29*
