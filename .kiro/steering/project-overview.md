---
inclusion: always
---

# Portal Project Overview

## Project Identity

- **Repository**: Portal
- **Organization**: 3 Inventors (Operational Intelligence Company)
- **Type**: ASP.NET Core MVC 8 Web Application
- **Database Approach**: Database-First (EF Core scaffolding)
- **Product Vision**: Multi-tenant back-office operational platform (not tightly bound to 3 Inventors — designed for subscription by other businesses)

## Company Philosophy

3 Inventors is positioned as an **Operational Intelligence Company**, not a software vendor or SaaS startup. The platform reflects:
- Operational seriousness and structured execution
- Financial awareness and visibility
- Long-term reliability and partnership
- Intelligence readiness (future COM — Canonical Operational Model)

## Platform Modules

The Portal is a modular back-office platform. Users access modules via invitation-only registration with super admin approval.

### Current Modules (Phase 1)
1. **Quotation Platform** — Device/setup selection, pricing logic, proposal generation
2. **Invoicing** — Quotation-to-invoice conversion (deterministic, transactional)
3. **Revenue Control** — Payment tracking, outstanding balances, receivables visibility

### Future Modules
- Insights (operational analytics)
- JDS (production orchestration)
- ERP integration
- COM ingestion pipeline

## Architecture

### Pattern: MVC + Service Layer

```
Controller → Service → Repository → Database
                ↕
         Message Bus (MassTransit/RabbitMQ)
                ↕
         Real-time (SignalR)
```

### Technology Stack

| Layer | Technology |
|-------|-----------|
| Web Framework | ASP.NET Core MVC 8 |
| ORM | Entity Framework Core (Database-First) |
| Message Bus | MassTransit + RabbitMQ |
| Real-time | SignalR |
| Logging | Serilog |
| Authentication | ASP.NET Core Identity |
| Databases | Portal DB + Membership DB (SQL Server) |

### Critical Invariants
1. Financial values are computed, not manually edited
2. Outbox pattern guarantees message delivery
3. Domain logic stays outside UI layer
4. Modules remain loosely coupled
5. Naming consistency across DB and code

## Key Conventions

### Code Organization
- Controllers handle HTTP concerns only — delegate logic to services
- Services contain business logic and orchestration
- Repositories handle all data access (see repository-standards global steering)
- Models map to database entities via EF Core scaffolding
- No UI-driven logic in domain layer

### Database
- SQL Server with `[dbo]` schema
- Two databases: Portal (business data) and Membership (identity/auth)
- Naming follows the SQL schema design steering (global)
- Full table names in queries — no short aliases
- BIT columns prefixed with `Is` or `Has`
- Foreign keys follow `<TableName>Id` convention

### Error Handling
- Repositories: try/catch with rethrow (`throw;`)
- Controllers: catch, log via `SystemLoggerExtensions`, return appropriate response
- Never swallow exceptions silently

### Security
- Invitation-only registration (super admin invites users)
- Super admin grants module access after registration confirmation
- Input validation on all endpoints
- Parameterized queries (SqlParameter) — never string concatenation
- ASP.NET Core Identity for auth
- Credentials in User Secrets, not appsettings.json

## UI Design System

All UI follows the MyChair Design System:
- **Colors**: Primary Blue #0D5EA6, Accent Cyan #57B8E8, Success #129867, Warning #C8912E, Danger #C24A4A
- **Background**: Base #F7FAFC, Secondary #EEF4F8
- **Typography**: Headings = Manrope (bold, tight), Body = Inter
- **Layout**: Sidebar + Topbar + Content grid, cards with 20-30px border radius, soft shadows
- **Tone**: Operational, calm, structured — no startup flashiness
- **Alerts & Confirmations**: All confirmation dialogs and user-facing alert messages must use SweetAlert2 (`Swal.fire`). Never use native `alert()`, `confirm()`, or `prompt()`. Use `confirmButtonColor: '#C24A4A'` for destructive actions and `confirmButtonColor: '#0D5EA6'` for informational ones.
- **BlockUI**: All AJAX calls must use `BlockUI.show()` before the request and `BlockUI.hide()` after completion (in both success and error paths). This prevents user interaction during processing. The utility is loaded globally from `/js/block-ui.js`.

### AJAX Call Pattern

All AJAX interactions must follow this standard flow:

1. **BlockUI.show('message')** — Block the UI before the request
2. **fetch()** — Make the AJAX call
3. **BlockUI.hide()** — Unblock the UI after response (success or error)
4. **Swal.fire()** — Show SweetAlert2 with the result

```javascript
// Standard AJAX pattern
async function doAction() {
    BlockUI.show('Processing...');
    try {
        var response = await fetch('/Controller/Action', {
            method: 'POST',
            headers: { 'RequestVerificationToken': getAntiForgeryToken() },
            body: formData
        });
        var data = await response.json();
        BlockUI.hide();

        if (data.success) {
            Swal.fire({ title: 'Done!', text: 'Operation completed.', icon: 'success', confirmButtonColor: '#0D5EA6' });
        } else {
            Swal.fire({ title: 'Error', text: data.message, icon: 'error', confirmButtonColor: '#0D5EA6' });
        }
    } catch (e) {
        BlockUI.hide();
        Swal.fire({ title: 'Error', text: 'An unexpected error occurred.', icon: 'error', confirmButtonColor: '#0D5EA6' });
    }
}
```

Key rules:
- Never leave BlockUI visible after an error — always call `BlockUI.hide()` in both success and catch paths
- Use vanilla `fetch` API — no jQuery
- Controllers return `Json(new { success, message })` for AJAX endpoints
- Include antiforgery token in POST requests

## Reference Documentation

Detailed specs live in `.kiro/docs/QuotationPlatform/`:
- `KIRO_Execution_Index.md` — Execution phases and validation checkpoints
- `Architecture_State.md` — Full architecture state and bounded contexts
- `mychair_quotation_to_invoice_kiro.md` — Quotation→Invoice conversion spec
- `mychair_revenue_control_layer_kiro.md` — Revenue control layer spec
- `mychair_ui_design_system.md` — UI design system
- `quotation_platform_production_pack/` — HTML mockups for all screens
