# Sales Module — Entity Relationship Model

**Last revised:** 14 July 2026  
**Status:** Draft — Brainstorming  
**Schema:** `[sales]`

---

## Overview

The Sales module adds a native commercial pipeline to the Portal platform. It is a multi-tenant module available within subscription tiers (not 3 Inventors-specific). The module tracks the complete lifecycle: Lead → Contact → Follow-Up → Meeting → Proposal → Won/Lost → Customer.

All entities belong to a Business (tenant). The module shares Identity, Permissions, Activity Timeline, Audit Log, and Notification infrastructure with the rest of Portal.

---

## Entity Relationship Diagram

```
[LeadSourceType] ──┐
[LeadSourceReferenceType] ──┤
[Product] ──────────────────┼──→ [LeadRequest] ──→ [Contact]
                            │         │
                            │         ├──→ [LeadResponse]
                            │         ├──→ [LeadFollowUpSchedule] ──→ [LeadFollowUpScheduleTask]
                            │         └──→ [LeadStatusType] (FK on LeadRequest)
                            │
[LeadResponseType] ─────────┼──→ [LeadResponse]
[LeadResponseTemplate] ─────┘         │
                                      ▼
                              [Meeting] ──→ [MeetingType]
                                  │
                                  ├──→ [MeetingProductRequest] ──→ [Product]
                                  └──→ [MeetingOpportunity]
                                            │
                                            ▼
                                      [Proposal] (links to existing Quotation)
                                            │
                                            ▼
                                      Won / Lost (Pipeline Stage)
```

---

## Lookup / Reference Tables

### 1. [sales].[Product]

The products offered by the business. For 3 Inventors: WorkforcePi, Chaplin Pro, EOMFA, Guardian, CampaignPi, Portal, etc. For other tenants: their own product catalogue.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INT IDENTITY | NOT NULL | PK |
| BusinessId | INT | NOT NULL | FK → [dbo].[Business] |
| Name | NVARCHAR(200) | NOT NULL | Product name |
| Description | NVARCHAR(500) | NULL | Optional description |
| IsActive | BIT | NOT NULL | DEFAULT 1 |
| CreatedAtUtc | DATETIME | NOT NULL | DEFAULT GETUTCDATE() |

---

### 2. [sales].[LeadSourceType]

How the lead arrived. Industry-agnostic.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INT IDENTITY | NOT NULL | PK |
| Name | NVARCHAR(100) | NOT NULL | e.g., Website, Referral, Event, Cold Call, Partner |
| IsActive | BIT | NOT NULL | DEFAULT 1 |

**Seed values:** Website, Referral, Event, Cold Call, Partner, Social Media, Other

---

### 3. [sales].[LeadSourceReferenceType]

Complementary detail to LeadSourceType — the specific channel or platform.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INT IDENTITY | NOT NULL | PK |
| Name | NVARCHAR(100) | NOT NULL | e.g., Facebook, Instagram, LinkedIn, Google Ads, Direct |
| IsActive | BIT | NOT NULL | DEFAULT 1 |

**Seed values:** Facebook, Instagram, LinkedIn, Google Ads, Twitter/X, Email Campaign, Direct, Other

---

### 4. [sales].[LeadStatusType]

Pipeline stages for tracking lead progression.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INT IDENTITY | NOT NULL | PK |
| Name | NVARCHAR(50) | NOT NULL | Stage name |
| DisplayOrder | INT | NOT NULL | For UI ordering |
| Colour | NVARCHAR(7) | NULL | Hex colour for pipeline view |
| IsTerminal | BIT | NOT NULL | DEFAULT 0 — marks Won/Lost/Inactive as end states |

**Seed values:**

| Id | Name | DisplayOrder | Colour | IsTerminal |
|----|------|-------------|--------|-----------|
| 1 | New | 1 | #57B8E8 | 0 |
| 2 | Contacted | 2 | #0D5EA6 | 0 |
| 3 | Follow-Up | 3 | #C8912E | 0 |
| 4 | Meeting Scheduled | 4 | #6B5CE7 | 0 |
| 5 | Proposal Sent | 5 | #0D5EA6 | 0 |
| 6 | Won | 6 | #129867 | 1 |
| 7 | Lost | 7 | #C24A4A | 1 |
| 8 | Inactive | 8 | #8a9bab | 1 |

---

### 5. [sales].[LeadResponseType]

How the response was delivered.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INT IDENTITY | NOT NULL | PK |
| Name | NVARCHAR(50) | NOT NULL | e.g., Email, Telephone, SMS, WhatsApp |

**Seed values:** Email, Telephone, SMS, WhatsApp, In Person

---

### 6. [sales].[MeetingType]

Format of the meeting.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INT IDENTITY | NOT NULL | PK |
| Name | NVARCHAR(50) | NOT NULL | e.g., Online, On-Site, Phone Call |

**Seed values:** Online, On-Site, Phone Call, Video Call

---

## Core Entities

### 7. [sales].[Contact]

A person who has expressed interest or been recorded manually. Unique by email or phone within a business.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INT IDENTITY | NOT NULL | PK |
| BusinessId | INT | NOT NULL | FK → [dbo].[Business] |
| FirstName | NVARCHAR(100) | NOT NULL | |
| LastName | NVARCHAR(100) | NULL | |
| Email | NVARCHAR(320) | NULL | Unique within BusinessId (when not null) |
| PhoneNumber | NVARCHAR(30) | NULL | Unique within BusinessId (when not null) |
| CompanyName | NVARCHAR(200) | NULL | Free text — the company they represent |
| JobTitle | NVARCHAR(100) | NULL | |
| Country | NVARCHAR(100) | NULL | |
| Notes | NVARCHAR(MAX) | NULL | Free-text notes |
| IsActive | BIT | NOT NULL | DEFAULT 1 |
| CreatedAtUtc | DATETIME | NOT NULL | DEFAULT GETUTCDATE() |

**Uniqueness:** Partial unique index on (BusinessId, Email) WHERE Email IS NOT NULL. Partial unique index on (BusinessId, PhoneNumber) WHERE PhoneNumber IS NOT NULL.

---

### 8. [sales].[LeadRequest]

A specific enquiry or interest expression from a Contact about a Product. One Contact can have many LeadRequests (interest history).

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INT IDENTITY | NOT NULL | PK |
| BusinessId | INT | NOT NULL | FK → [dbo].[Business] |
| ContactId | INT | NOT NULL | FK → [sales].[Contact] |
| ProductId | INT | NULL | FK → [sales].[Product] — may be a general enquiry |
| LeadSourceTypeId | INT | NOT NULL | FK → [sales].[LeadSourceType] |
| LeadSourceReferenceTypeId | INT | NULL | FK → [sales].[LeadSourceReferenceType] |
| LeadStatusTypeId | INT | NOT NULL | FK → [sales].[LeadStatusType] — DEFAULT 1 (New) |
| SourceUrl | NVARCHAR(500) | NULL | e.g., product website URL, Facebook post URL |
| RequestText | NVARCHAR(MAX) | NULL | Contact form message or notes |
| AssignedToUserId | NVARCHAR(450) | NULL | FK → AspNetUsers — who owns this lead |
| IsCancelled | BIT | NOT NULL | DEFAULT 0 |
| CreatedAtUtc | DATETIME | NOT NULL | DEFAULT GETUTCDATE() |

---

### 9. [sales].[LeadResponse]

A response action taken on a LeadRequest — either automated or manual.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INT IDENTITY | NOT NULL | PK |
| LeadRequestId | INT | NOT NULL | FK → [sales].[LeadRequest] |
| LeadResponseTypeId | INT | NOT NULL | FK → [sales].[LeadResponseType] |
| LeadResponseTemplateId | INT | NULL | FK → [sales].[LeadResponseTemplate] — if automated |
| RespondedByUserId | NVARCHAR(450) | NULL | FK → AspNetUsers — NULL if automated |
| ResponseText | NVARCHAR(MAX) | NULL | Actual message sent or summary |
| IsAutomated | BIT | NOT NULL | DEFAULT 0 |
| SentAtUtc | DATETIME | NOT NULL | When the response was sent |
| CreatedAtUtc | DATETIME | NOT NULL | DEFAULT GETUTCDATE() |

---

### 10. [sales].[LeadResponseTemplate]

Defines an automated response rule: for a given product, send a specific type of response after X hours.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INT IDENTITY | NOT NULL | PK |
| BusinessId | INT | NOT NULL | FK → [dbo].[Business] |
| ProductId | INT | NULL | FK → [sales].[Product] — NULL = applies to all products |
| LeadResponseTypeId | INT | NOT NULL | FK → [sales].[LeadResponseType] |
| Name | NVARCHAR(200) | NOT NULL | Template name for display |
| Subject | NVARCHAR(300) | NULL | Email subject (for email type) |
| BodyTemplate | NVARCHAR(MAX) | NOT NULL | Message body with placeholders |
| ResponseTimeInHours | INT | NOT NULL | Hours to wait before sending |
| IsActive | BIT | NOT NULL | DEFAULT 1 |
| CreatedAtUtc | DATETIME | NOT NULL | DEFAULT GETUTCDATE() |

**Placeholders:** `{ContactFirstName}`, `{ProductName}`, `{BusinessName}`, `{MeetingBookingLink}`

---

### 11. [sales].[LeadFollowUpSchedule]

A reusable follow-up plan that can be applied to a lead. Defines a sequence of tasks to execute over time.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INT IDENTITY | NOT NULL | PK |
| BusinessId | INT | NOT NULL | FK → [dbo].[Business] |
| Name | NVARCHAR(200) | NOT NULL | e.g., "Standard 14-Day Follow-Up" |
| Description | NVARCHAR(500) | NULL | |
| ProductId | INT | NULL | FK → [sales].[Product] — NULL = generic |
| IsActive | BIT | NOT NULL | DEFAULT 1 |
| CreatedAtUtc | DATETIME | NOT NULL | DEFAULT GETUTCDATE() |

---

### 12. [sales].[LeadFollowUpScheduleTask]

Individual steps within a follow-up schedule (the template definition).

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INT IDENTITY | NOT NULL | PK |
| LeadFollowUpScheduleId | INT | NOT NULL | FK → [sales].[LeadFollowUpSchedule] |
| StepOrder | INT | NOT NULL | Sequence position (1, 2, 3...) |
| Name | NVARCHAR(200) | NOT NULL | Task description |
| DelayFromPreviousInHours | INT | NOT NULL | Hours after previous step (or after lead creation for step 1) |
| IsAutomated | BIT | NOT NULL | DEFAULT 0 — if true, system executes automatically |
| LeadResponseTemplateId | INT | NULL | FK → [sales].[LeadResponseTemplate] — for automated steps |
| CreatedAtUtc | DATETIME | NOT NULL | DEFAULT GETUTCDATE() |

---

### 13. [sales].[LeadFollowUpTask]

An *instance* of a follow-up task generated for a specific LeadRequest from a schedule.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INT IDENTITY | NOT NULL | PK |
| LeadRequestId | INT | NOT NULL | FK → [sales].[LeadRequest] |
| LeadFollowUpScheduleTaskId | INT | NULL | FK → [sales].[LeadFollowUpScheduleTask] — NULL if manually created |
| AssignedToUserId | NVARCHAR(450) | NULL | FK → AspNetUsers |
| Name | NVARCHAR(200) | NOT NULL | Task description |
| DueAtUtc | DATETIME | NOT NULL | When this task should be completed |
| CompletedAtUtc | DATETIME | NULL | When it was actually completed |
| IsCompleted | BIT | NOT NULL | DEFAULT 0 |
| IsAutomated | BIT | NOT NULL | DEFAULT 0 |
| Notes | NVARCHAR(MAX) | NULL | |
| CreatedAtUtc | DATETIME | NOT NULL | DEFAULT GETUTCDATE() |

---

### 14. [sales].[Meeting]

A scheduled meeting related to a lead.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INT IDENTITY | NOT NULL | PK |
| BusinessId | INT | NOT NULL | FK → [dbo].[Business] |
| LeadRequestId | INT | NULL | FK → [sales].[LeadRequest] — NULL if meeting is not lead-related |
| ContactId | INT | NOT NULL | FK → [sales].[Contact] |
| MeetingTypeId | INT | NOT NULL | FK → [sales].[MeetingType] |
| Subject | NVARCHAR(300) | NOT NULL | Meeting subject/title |
| ScheduledAtUtc | DATETIME | NOT NULL | Start time |
| DurationMinutes | INT | NOT NULL | DEFAULT 60 |
| Location | NVARCHAR(300) | NULL | Physical address or video link |
| Notes | NVARCHAR(MAX) | NULL | Pre-meeting notes or agenda |
| Outcome | NVARCHAR(MAX) | NULL | Post-meeting summary |
| IsCancelled | BIT | NOT NULL | DEFAULT 0 |
| CreatedByUserId | NVARCHAR(450) | NOT NULL | FK → AspNetUsers |
| CreatedAtUtc | DATETIME | NOT NULL | DEFAULT GETUTCDATE() |

---

### 15. [sales].[MeetingProductRequest]

Specific products discussed or requested during a meeting. A meeting about Product A might reveal interest in B and C.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INT IDENTITY | NOT NULL | PK |
| MeetingId | INT | NOT NULL | FK → [sales].[Meeting] |
| ProductId | INT | NOT NULL | FK → [sales].[Product] |
| RequestText | NVARCHAR(MAX) | NULL | What was discussed about this product |
| IsActive | BIT | NOT NULL | DEFAULT 1 |
| IsCancelled | BIT | NOT NULL | DEFAULT 0 |
| CreatedAtUtc | DATETIME | NOT NULL | DEFAULT GETUTCDATE() |

---

### 16. [sales].[MeetingOpportunity]

Broader business opportunities discovered during a meeting — not tied to a specific product.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INT IDENTITY | NOT NULL | PK |
| MeetingId | INT | NOT NULL | FK → [sales].[Meeting] |
| Title | NVARCHAR(300) | NOT NULL | e.g., "Partnership opportunity", "Consulting engagement" |
| Description | NVARCHAR(MAX) | NULL | Details |
| EstimatedValue | DECIMAL(18,2) | NULL | Potential revenue |
| IsActive | BIT | NOT NULL | DEFAULT 1 |
| CreatedAtUtc | DATETIME | NOT NULL | DEFAULT GETUTCDATE() |

---

### 17. [sales].[Proposal]

This is NOT a new table — it references the existing `[dbo].[Quotation]` table. The link is established by adding a nullable FK on the Quotation table:

```sql
ALTER TABLE [dbo].[Quotation]
ADD [LeadRequestId] INT NULL
    CONSTRAINT [FK_Quotation_LeadRequest] FOREIGN KEY REFERENCES [sales].[LeadRequest]([Id]);
```

This connects the existing quotation/proposal workflow to the sales pipeline. When a proposal is created from a lead, the LeadRequestId is set.

---

## Automation Entities

### 18. [sales].[NotificationSubscription]

Defines who gets notified for what event. The business manager registers users for specific notification types.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INT IDENTITY | NOT NULL | PK |
| BusinessId | INT | NOT NULL | FK → [dbo].[Business] |
| UserId | NVARCHAR(450) | NOT NULL | FK → AspNetUsers |
| EventType | NVARCHAR(50) | NOT NULL | e.g., "NewLead", "TaskDue", "MeetingReminder" |
| Channel | NVARCHAR(20) | NOT NULL | e.g., "Email", "SMS", "Push" |
| IsActive | BIT | NOT NULL | DEFAULT 1 |
| CreatedAtUtc | DATETIME | NOT NULL | DEFAULT GETUTCDATE() |

**Event types:** NewLead, LeadAssigned, TaskDue, TaskOverdue, MeetingReminder, MeetingTomorrow, ProposalAccepted, LeadWon

---

## API Entity

### LeadIngestionApi

Not a database table — this is an API endpoint that external websites call:

**Endpoint:** `POST /api/sales/leads/ingest`

**Payload:**
```json
{
    "businessApiKey": "abc123",
    "productCode": "workforcepi",
    "source": "website",
    "sourceReference": "facebook",
    "sourceUrl": "https://workforcepi.com/contact",
    "firstName": "John",
    "lastName": "Doe",
    "email": "john@example.com",
    "phone": "+35799123456",
    "companyName": "Acme Ltd",
    "message": "I'd like a demo of WorkforcePi for 50 users."
}
```

**Behaviour:**
1. Validate API key → resolve BusinessId
2. Find or create Contact (match by email or phone within business)
3. Create LeadRequest with status "New"
4. Trigger LeadResponseTemplate automation (if configured for product)
5. Trigger NotificationSubscription events ("NewLead")
6. Return 201 Created with LeadRequestId

---

## Relationships Summary

| From | To | Relationship | FK Column |
|------|----|-------------|-----------|
| LeadRequest | Business | Many-to-One | BusinessId |
| LeadRequest | Contact | Many-to-One | ContactId |
| LeadRequest | Product | Many-to-One | ProductId |
| LeadRequest | LeadSourceType | Many-to-One | LeadSourceTypeId |
| LeadRequest | LeadSourceReferenceType | Many-to-One | LeadSourceReferenceTypeId |
| LeadRequest | LeadStatusType | Many-to-One | LeadStatusTypeId |
| LeadResponse | LeadRequest | Many-to-One | LeadRequestId |
| LeadResponse | LeadResponseType | Many-to-One | LeadResponseTypeId |
| LeadResponse | LeadResponseTemplate | Many-to-One | LeadResponseTemplateId |
| LeadFollowUpSchedule | Business | Many-to-One | BusinessId |
| LeadFollowUpSchedule | Product | Many-to-One | ProductId |
| LeadFollowUpScheduleTask | LeadFollowUpSchedule | Many-to-One | LeadFollowUpScheduleId |
| LeadFollowUpTask | LeadRequest | Many-to-One | LeadRequestId |
| LeadFollowUpTask | LeadFollowUpScheduleTask | Many-to-One | LeadFollowUpScheduleTaskId |
| Meeting | Business | Many-to-One | BusinessId |
| Meeting | LeadRequest | Many-to-One | LeadRequestId |
| Meeting | Contact | Many-to-One | ContactId |
| Meeting | MeetingType | Many-to-One | MeetingTypeId |
| MeetingProductRequest | Meeting | Many-to-One | MeetingId |
| MeetingProductRequest | Product | Many-to-One | ProductId |
| MeetingOpportunity | Meeting | Many-to-One | MeetingId |
| Quotation | LeadRequest | Many-to-One | LeadRequestId (new FK) |
| NotificationSubscription | Business | Many-to-One | BusinessId |

---

## Pipeline Status Transitions

```
New → Contacted          (when first LeadResponse is created)
Contacted → Follow-Up    (when LeadFollowUpSchedule is applied)
Follow-Up → Meeting Scheduled  (when Meeting is created for this lead)
Meeting Scheduled → Proposal Sent  (when Quotation with LeadRequestId is created)
Proposal Sent → Won      (manual, or automated when invoice is paid)
Proposal Sent → Lost     (manual)
Any non-terminal → Inactive  (manual — lead went cold)
```

**Automation:** Status transitions can be automated based on events, but always overridable manually.

---

## Subscription Tier Placement

| Feature | Foundation | Professional | Enterprise |
|---------|-----------|-------------|-----------|
| Contacts (manual) | ✓ | ✓ | ✓ |
| Lead Requests (manual) | ✓ | ✓ | ✓ |
| Pipeline View | ✓ | ✓ | ✓ |
| Meetings | ✓ | ✓ | ✓ |
| Follow-Up Schedules | — | ✓ | ✓ |
| Response Templates | — | ✓ | ✓ |
| LeadIngestionApi | — | ✓ | ✓ |
| Automated Responses | — | ✓ | ✓ |
| Notifications | — | ✓ | ✓ |
| Proposals (from lead) | — | ✓ | ✓ |
| Dashboards & Analytics | — | — | ✓ |
| Multi-product Pipeline | — | — | ✓ |

---

## Open Questions

1. **Contact → Customer conversion:** When a lead is Won and the contact becomes a Customer (existing entity), should we create a link? (e.g., `CustomerId` on Contact, or `ContactId` on Customer?)
2. **Calendar integration (Phase 2):** Google Calendar / Outlook sync for meetings — API choice?
3. **ICS file download:** Should Meeting entity generate .ics files for download?
4. **Lead scoring (Phase 5):** Do we want a numeric score on LeadRequest based on engagement?
5. **Product codes for API:** How should products be identified in the ingestion API — slug, code, or ID?

---

## Next Steps

1. Review and refine this entity model
2. Decide on open questions
3. Create formal spec (requirements → design → tasks)
4. Begin implementation with migrations and entities
