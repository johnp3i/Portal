# Phase 2 — Development Timetable

## Overview

Phase 2 delivers the **Enterprise tier** features — operational completeness through client self-service, document management, and team awareness. These features make the platform a complete operational hub for growing businesses.

**Prerequisites:** Phase 1 must be complete (permission infrastructure, P&L, Expense Insights, Payment Reminders, Stripe Connect, Cash Flow Forecasting).

---

## Module 7: Client Portal (Customer Self-Service)

**Effort:** High | **Dependencies:** Existing invoice/payment data, Stripe payment links (Phase 1)

- [ ] 7.1 Design client portal architecture (magic link access per customer, no login required)
- [ ] 7.2 Create `CustomerPortalToken` table (customer, token, expiry, business)
- [ ] 7.3 Create portal token generation service (similar to Demo Invitations pattern)
- [ ] 7.4 Create public portal controller (AllowAnonymous, token-based access)
- [ ] 7.5 Create portal landing page (customer sees their business relationship summary)
- [ ] 7.6 Create invoice list view (all invoices for this customer, status, amounts)
- [ ] 7.7 Create invoice detail view (line items, totals, payment history)
- [ ] 7.8 Add "Pay Now" button integration (Stripe payment link from Phase 1)
- [ ] 7.9 Create outstanding balance summary view
- [ ] 7.10 Create payment history view (all payments made by this customer)
- [ ] 7.11 Add statement download (PDF generation for any period)
- [ ] 7.12 Add business branding (logo, colours from business profile)
- [ ] 7.13 Create "Invite Customer to Portal" action on Customer Detail page
- [ ] 7.14 Create portal link email template (branded, with magic link)
- [ ] 7.15 Add portal activity logging (which customers accessed, when)
- [ ] 7.16 Add plan permission gate (`client_portal` module key — Enterprise only)
- [ ] 7.17 Add soft-gate teaser for Professional users
- [ ] 7.18 Mobile responsive design (phone + tablet)
- [ ] 7.19 Security: token expiry management, rate limiting, no data leakage between customers
- [ ] 7.20 End-to-end testing: invite → access → view invoices → pay → verify

---

## Module 8: Document Attachments

**Effort:** Medium | **Dependencies:** File storage infrastructure

- [ ] 8.1 Create `DocumentAttachment` table (entity type, entity ID, file name, content type, storage path, size, uploaded by)
- [ ] 8.2 Choose and configure storage backend (local filesystem for dev, Azure Blob for production)
- [ ] 8.3 Create file upload service (validation, storage, metadata persistence)
- [ ] 8.4 Add attachment upload UI on Purchase Detail/Edit (supplier invoice scan)
- [ ] 8.5 Add attachment upload UI on Invoice Detail (signed copy, supporting docs)
- [ ] 8.6 Add attachment upload UI on Quotation Detail (supporting docs, contracts)
- [ ] 8.7 Create attachment list/preview component (thumbnail for images, icon for PDFs)
- [ ] 8.8 Add download endpoint (secure, business-scoped, no cross-tenant access)
- [ ] 8.9 Add delete attachment capability (owner/admin only)
- [ ] 8.10 Enforce limits: max 5 attachments per record, max 5MB per file
- [ ] 8.11 Supported types validation: PDF, PNG, JPG, WEBP only
- [ ] 8.12 Add attachment count indicator on list views (e.g., paperclip icon with count)
- [ ] 8.13 Add plan permission gate (`attachments` module key — Professional+)
- [ ] 8.14 Add soft-gate teaser for Starter users on detail pages
- [ ] 8.15 Mobile responsive: upload via camera/files, preview in lightbox
- [ ] 8.16 Security: virus scan consideration, content-type validation, no executable uploads
- [ ] 8.17 End-to-end testing: upload → view → download → delete

---

## Module 9: Activity Timeline & Notifications

**Effort:** High | **Dependencies:** SignalR (existing in stack), MassTransit (existing)

- [ ] 9.1 Create `ActivityEvent` table (event type, entity type, entity ID, description, user ID, business ID, timestamp)
- [ ] 9.2 Create `NotificationPreference` table (user, event type, channel: in-app/email/both)
- [ ] 9.3 Define event taxonomy (invoice.issued, payment.received, quotation.accepted, customer.created, vat.submitted, etc.)
- [ ] 9.4 Create activity event publisher service (called from existing services when events occur)
- [ ] 9.5 Instrument existing services: Invoice, Payment, Quotation, Customer, Purchase, VAT (emit events)
- [ ] 9.6 Create activity timeline controller and view (centralised feed, filterable)
- [ ] 9.7 Create SignalR hub for real-time activity updates
- [ ] 9.8 Add real-time notification indicator in the app header (bell icon with count)
- [ ] 9.9 Create notification dropdown/panel (recent events, mark as read)
- [ ] 9.10 Create email digest service (daily or weekly summary of activity)
- [ ] 9.11 Create notification preference UI (Settings → Notifications)
- [ ] 9.12 Add per-user event filtering (choose which events to be notified about)
- [ ] 9.13 Add plan permission gate (`activity_timeline` module key — Enterprise only)
- [ ] 9.14 Add soft-gate teaser for Professional users
- [ ] 9.15 Mobile responsive: timeline card layout, notification panel
- [ ] 9.16 Performance: pagination on timeline, event pruning (archive after 90 days)
- [ ] 9.17 End-to-end testing: action → event emitted → timeline updated → notification shown

---

## Module 10: Audit Log Access

**Effort:** Low | **Dependencies:** Existing audit log infrastructure

- [ ] 10.1 Review existing audit log implementation (already capturing events)
- [ ] 10.2 Add plan permission gate (`audit_log` module key — Enterprise only)
- [ ] 10.3 Add soft-gate teaser for Professional users ("Full audit trail available on Enterprise")
- [ ] 10.4 Verify audit log view works correctly with new permission system
- [ ] 10.5 Add export capability (CSV/PDF) for audit log
- [ ] 10.6 Visual QA and mobile responsiveness check

---

## Module 11: Business Applications Tracker (Compliance Filings)

**Effort:** Medium | **Dependencies:** None (standalone module)

- [ ] 11.1 Design compliance filings data model (`ApplicationType` templates, `BusinessApplication` per-business instances)
- [ ] 11.2 Create `[compliance]` schema with tables: `ApplicationType`, `ApplicationCategory`, `BusinessApplication`, `ApplicationAttachment`
- [ ] 11.3 Create `ApplicationType` entity (Name, Description, Country, Category FK, Frequency, DefaultDueMonth, DefaultDueDay, IsTemplate, CreatedByAdmin)
- [ ] 11.4 Create `ApplicationCategory` entity (Name, Description — e.g., "Tax", "Employee", "Regulatory", "Business Registration")
- [ ] 11.5 Create `BusinessApplication` entity (BusinessId, ApplicationTypeId, DueDate, Status, ReferenceNumber, Notes, SubmittedAtUtc, ApprovedAtUtc)
- [ ] 11.6 Create `ApplicationAttachment` entity (ApplicationId, FileName, FilePath, ContentType, UploadedAtUtc)
- [ ] 11.7 Seed default application types for Cyprus (IR7 Annual Tax Return, Social Insurance Monthly, VAT Return, Annual Levy, Employer's Declaration)
- [ ] 11.8 Create SuperAdmin template management UI (CRUD for ApplicationType + ApplicationCategory)
- [ ] 11.9 Create business-facing "Import from Templates" flow (select country/category → pick relevant applications → import to business)
- [ ] 11.10 Create business applications list view (filterable by category, status, due date)
- [ ] 11.11 Create business application detail/edit view (status updates, notes, attachments, reference number)
- [ ] 11.12 Implement status workflow: Pending → In Progress → Submitted → Approved / Rejected
- [ ] 11.13 Create dashboard widget: "Upcoming Filings" — applications due in next 30/60/90 days
- [ ] 11.14 Add notification/warning for applications approaching due date (7 days, 3 days, overdue)
- [ ] 11.15 Add calendar year view showing all filing deadlines
- [ ] 11.16 Add attachment upload per application (PDF evidence of submission)
- [ ] 11.17 Add plan permission gate (`compliance` module key — Professional+)
- [ ] 11.18 Add soft-gate teaser for Foundation users
- [ ] 11.19 Mobile responsive design
- [ ] 11.20 End-to-end testing: import template → create application → update status → attach evidence

---

## Build Order & Dependencies

```
Module 8 (Document Attachments) ←── Independent, can start immediately
    │
Module 10 (Audit Log Access) ←── Quick win, mostly permission gating
    │
Module 11 (Business Applications) ←── Standalone, no dependencies
    │
Module 7 (Client Portal) ←── Benefits from Stripe integration (Phase 1)
    │
Module 9 (Activity Timeline) ←── Most complex, benefits from all other modules being complete
```

**Recommended sequence:**
1. Module 10 (Audit Log) — quick win, mostly permission gating on existing feature
2. Module 8 (Attachments) — independent, medium effort
3. Module 11 (Business Applications) — medium effort, standalone, high compliance value
4. Module 7 (Client Portal) — high effort, high value
5. Module 9 (Activity Timeline) — highest effort, benefits from all modules emitting events

---

## Completion Criteria

Each module is considered complete when:
- [ ] All sub-tasks checked off
- [ ] Plan permission gating verified (Professional blocked, Enterprise allowed)
- [ ] Soft-gate teasers visible to Professional users
- [ ] Mobile responsive at 375px and 810px
- [ ] No regressions in existing functionality
- [ ] Documentation updated

---

## Post-Phase 2 Milestones

- [ ] All 5 modules complete and verified
- [ ] Landing page updated with Enterprise tier features
- [ ] Enterprise tier available for purchase/activation
- [ ] Client Portal demo-ready for prospect presentations
- [ ] Business Applications Tracker demo-ready for compliance-focused businesses
- [ ] Phase 3 planning begins (Multi-Currency, API/Integrations Layer, Payroll)

---

## Next Phase

When Phase 2 is complete, proceed to:

**→ Phase 3: Market Expansion** (`.kiro/docs/Phase3_Development_Timetable.md`)
- Multi-Currency Support
- API / Integrations Layer (REST API, Webhooks, Stripe advanced, Bank Feeds)
- Payroll / Payslips (employee management, payslip generation, deductions, P&L integration)

Phase 3 positions the Portal for international markets, third-party ecosystem connectivity, and operational completeness.
