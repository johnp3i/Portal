# Sales Module — Development Brief

**Date:** 15 July 2026  
**Status:** Approved for Phase 1 development  
**Module Name:** Sales  
**Schema:** `[sales]`

---

## Strategic Summary

The Sales module is a native commercial pipeline built into the Portal platform. It replaces the need for a third-party CRM by providing unified lead tracking, follow-up management, meeting scheduling, and proposal generation — all sharing the platform's Identity, Permissions, Activity Timeline, and Audit infrastructure.

The module is multi-tenant and industry-agnostic. It is available within subscription tiers (Foundation for basic tracking, Professional for automation and API, Enterprise for analytics).

---

## Phasing

### Phase 1 — Core Pipeline (Manual + Suggested Actions)

**Goal:** A fully functional lead-to-customer pipeline with manual operations and smart suggestions.

**Scope:**
- Contacts — manual creation, unique by email/phone per business
- LeadRequests — record enquiries, track interest history per contact
- Products — business product catalogue for pipeline tracking
- Pipeline stages — New → Contacted → Follow-Up → Meeting Scheduled → Proposal Sent → Won → Lost → Inactive
- Meetings — scheduling, types (Online/On-Site/Phone), outcome recording
- MeetingProductRequests — products discussed during meetings
- MeetingOpportunities — broader opportunities not tied to a product
- Lead response email — not automated, but auto-suggested (system prepares a template, user reviews and clicks "Send")
- Proposals linked to leads — existing Quotation table with new LeadRequestId FK
- Invoice linked to leads — existing Invoice table with new LeadRequestId FK
- Contact → Customer conversion — manual "Mark as Won" action, Customer gets ContactId FK
- ICS file download — generate .ics for meetings
- Deduplication — on contact creation, match by email or phone to prevent duplicates
- Pipeline view — visual representation of leads by stage

**Deliverable:** Usable lead tracking for the 3 demo requests already received.

---

### Phase 2 — LeadIngestionApi + Notifications

**Goal:** External websites feed leads into the pipeline automatically. Users are notified of events.

**Scope:**
- Public API endpoint (`POST /api/sales/leads/ingest`) with API key authentication
- Products identified by ID in API payload
- Rate limiting on the ingestion endpoint
- Embeddable integration code snippets (JavaScript, PHP, C#)
- Integration settings page — API key display/regeneration, product list, code snippets, test endpoint
- NotificationSubscriptions — business manager registers users for specific events (NewLead, TaskDue, MeetingReminder, etc.)
- Email delivery tracking — opens, bounces, delivery status on sent responses
- Lead deduplication window — same person + same product within X minutes = single LeadRequest
- Product mapping — link `[sales].[Product]` to quotation line items (nullable FK or mapping table)

**Deliverable:** Self-service lead capture from any website with notification routing.

---

### Phase 3 — Automation

**Goal:** The system acts autonomously — sends responses, generates tasks, reminds users.

**Scope:**
- Automated responses — LeadResponseTemplate with configurable ResponseTimeInHours
- Follow-up schedules — reusable task sequences auto-generated per lead
- Follow-up task instances — generated from schedule templates, assigned to users
- Meeting reminders — SMS/Email before scheduled meetings
- Background service — dedicated `IHostedService` with Outbox pattern (NOT MassTransit/RabbitMQ)
- Auto status transitions — pipeline stage advances automatically based on events (response sent → Contacted, meeting created → Meeting Scheduled, etc.)
- Overridable — all automated transitions can be manually overridden

**Deliverable:** Hands-off lead nurturing with guaranteed event processing.

---

## Architecture Decisions

### 1. Background Service over Message Bus

For Phase 3 automation, the module uses a self-contained `IHostedService` with an Outbox pattern instead of MassTransit/RabbitMQ.

**Rationale:**
- Lower complexity and maintenance burden
- No external infrastructure dependency (RabbitMQ)
- Sufficient for sales event volume (low-throughput compared to real-time systems)
- Outbox table (`[sales].[OutboxMessage]`) guarantees at-least-once processing
- Events written transactionally with the business operation
- Background service polls and processes

### 2. Contacts are Sales-Only

`[sales].[Contact]` represents prospects in the commercial pipeline. They are NOT general-purpose contacts. The existing `[dbo].[Customer]` entity remains the billing/invoicing entity. A Contact is linked to a Customer upon conversion (Won).

### 3. Cancellation vs Soft-Delete Pattern

All customer-facing action entities have two mechanisms:
- **IsCancelled** + `CancellationTimestamp` + `CancellationDescription` — customer/lead initiated the cancellation
- **IsActive** — internal soft-delete by the business user (cleanup, duplicates)

Both can coexist. Queries filter on `IsActive = 1`. IsCancelled is a visual indicator.

### 4. Product Catalogue Separation

`[sales].[Product]` is for pipeline tracking (what you sell). Quotation line items are for pricing (what you charge). They remain separate for now. A mapping or FK reference will be introduced in Phase 2 when the relationship becomes clearer through usage.

### 5. Calendar Integration

- **Phase 1:** ICS file downloads (manual import to any calendar)
- **Future:** Bidirectional calendar sync (Google/Outlook) as a separate module when demand justifies it

### 6. Lead Scoring (Deferred to Phase 5)

Experience-based scoring profile per lead:
- Interest level (1-5)
- Existing competing product
- Relationship strength
- Budget indication
- Timeline urgency

Separate `[sales].[LeadScore]` table when implemented. No entity changes needed now.

---

## Missing Items to Address

| Item | Phase | Priority |
|------|-------|----------|
| Contact deduplication on creation | 1 | High |
| API rate limiting | 2 | High |
| Email delivery tracking (opens/bounces) | 2 | Medium |
| Lead deduplication window (API) | 2 | High |
| Product → Quotation line item mapping | 2 | Medium |
| Outbox table and background service | 3 | High |

---

## Subscription Tier Placement

| Feature | Foundation | Professional | Enterprise |
|---------|-----------|-------------|-----------|
| Contacts (manual) | ✓ | ✓ | ✓ |
| Lead Requests (manual) | ✓ | ✓ | ✓ |
| Pipeline View | ✓ | ✓ | ✓ |
| Meetings + ICS | ✓ | ✓ | ✓ |
| Suggested Responses | ✓ | ✓ | ✓ |
| Follow-Up Schedules | — | ✓ | ✓ |
| Response Templates | — | ✓ | ✓ |
| LeadIngestionApi | — | ✓ | ✓ |
| Automated Responses | — | ✓ | ✓ |
| Notifications | — | ✓ | ✓ |
| Proposals (from lead) | — | ✓ | ✓ |
| Dashboards & Analytics | — | — | ✓ |
| Multi-product Pipeline | — | — | ✓ |

---

## Related Documents

- [Sales Module Entity Model](./Sales_Module_Entity_Model.md) — Full entity definitions and relationships
- [Portal Product Overview](../Portal_Product_Overview_2026-07-10.md) — Platform context
- [Subscription Tier Model](../Subscription_Tier_Model.md) — Pricing and tier features
- [Sales Module Strategic Proposal](./Sales_Module_Strategic_Proposal.md) — Original vision document
