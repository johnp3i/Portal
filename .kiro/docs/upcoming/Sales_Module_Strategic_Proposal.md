# Portal CRM / Sales Module

## Strategic Proposal for Kiro Portal Agent

### Vision

Portal should evolve into the **Core Operational Platform** of the 3
Inventors ecosystem. Rather than integrating a third-party CRM,
implement a native **Sales** module that shares Identity, Businesses,
Subscriptions and Operational Intelligence with every product.

## Why a Native Sales Module?

-   Unified customer lifecycle
-   Native integration with subscriptions and billing
-   Shared permissions and identity
-   Product-aware sales journeys
-   Operational Intelligence over the complete commercial process
-   No duplicated customer records

## Module Name

Use **Sales** instead of CRM.

## Phase 1 -- MVP

Entities: - Leads - Companies - Contacts - Demo Requests - Meetings -
Tasks - Opportunities - Proposals - Customers

Workflow:

Website Form → Lead → Demo Request → Follow-up Task → Meeting → Proposal
→ Won/Lost → Customer

## Phase 2 -- Automation

-   Website forms
-   Demo request ingestion
-   Automatic acknowledgement email
-   Sales assignment
-   Follow-up reminders
-   Activity timeline
-   Calendar integration

## Phase 3 -- Product Integration

Lead sources: - WorkforcePi - MyChair - Guardian - EOMFA - Chaplin -
CampaignPi - Portal

Every product feeds the same commercial pipeline.

## Phase 4 -- Subscription Integration

Proposal Accepted → Customer → Business → Subscription → Billing →
Onboarding

## Phase 5 -- Operational Intelligence

Dashboards: - New leads - Response time - Demo conversion - Proposal
conversion - Revenue by product - Revenue by source - Sales pipeline
health - Forecast revenue

## Suggested Navigation

Portal - Dashboard - Sales - Leads - Companies - Contacts - Demo
Requests - Meetings - Tasks - Opportunities - Proposals - Customers -
Billing - Subscriptions - Products - Reports - Administration

## Architecture Principles

-   Modular
-   Shared Identity
-   Shared Business entity
-   Shared Subscription model
-   Shared Notification engine
-   Shared Activity Timeline
-   Shared Audit Log

Sales is a Portal module, not a separate application.

## Long-Term Vision

CampaignPi → Landing Page → Lead → Sales → Proposal → Customer →
Subscription

The Portal becomes the commercial and operational backbone of the entire
3 Inventors ecosystem.

## Recommendation

Build a **Sales** module, not a generic CRM.

The objective is to create a commercial platform tightly integrated with
Portal, enabling future Operational Intelligence across the complete
customer lifecycle.

I would start thinking of it as

The Core Platform

Everything else plugs into it.
                Portal Core
                     │
     ┌───────────────┼───────────────┐
     │               │               │
   Sales         Billing        Identity
     │               │               │
  Customers     Subscriptions   Permissions
     │
     ├──────── WorkforcePi
     ├──────── MyChair
     ├──────── Guardian
     ├──────── Chaplin
     ├──────── EOMFA
     ├──────── CampaignPi
     └──────── Future Products
