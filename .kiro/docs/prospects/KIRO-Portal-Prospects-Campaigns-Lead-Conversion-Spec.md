# KIRO Specification — Portal Prospects & Lead Conversion Module

**Product:** 3 Inventors Portal  
**Area:** CRM / Sales / Opportunities  
**Purpose:** Add a structured prospecting layer before the existing Lead/Opportunity workflow and use it immediately for the Chaplin Pro founder-led sales campaign.

---

## 1. Business Objective

The Portal already supports creating a **Lead**, including Contact, Product, Source, Source Reference, Source URL and Request Details.

We now need a stage **before Lead**.

### Core distinction

> **Prospect = a person or organisation we intend to approach.**  
> **Lead = a person or organisation with whom a meaningful commercial interaction has started.**  
> **Opportunity = a qualified commercial possibility.**  
> **Customer = a converted commercial relationship.**

Target lifecycle:

**Prospect → Lead → Opportunity → Customer**

A prospect must not automatically become a Lead merely because a cold call was attempted.

Examples:
- Researched company, not contacted → Prospect
- Called, no answer → Prospect
- Receptionist asks for an introductory email → Prospect + Activity
- Relevant decision-maker engages / requests demo / expresses potential interest → Convert to Lead
- Qualified need/commercial possibility established → Opportunity

This keeps the sales database clean while preserving the complete acquisition history.

---

## 2. V1 Scope

Build only what is required to operate the real 3 Inventors prospecting workflow:

1. **Prospects**
2. **Prospecting Campaigns**
3. **Campaign membership / qualification**
4. **Prospect Activities**
5. **Prospect → Lead conversion**
6. Basic list/filter/dashboard functionality
7. Excel import compatible with the current Chaplin Pro prospect workbook

Do **not** add in V1:
- AI lead scoring
- automated email sequences
- scraping
- marketing automation
- complex workflow automation
- unsupported CRM architecture merely for future possibilities

Use existing Portal architecture, conventions, identity, tenancy, audit patterns and UI components. Inspect the current Lead/Opportunity implementation before changing schema. Reuse existing entities/enums where semantically correct; do not duplicate concepts.

---

## 3. Prospect

A Prospect represents the person or organisation being researched/approached.

Suggested information:

### Identity
- Organisation / Prospect Name
- Website
- Industry / Segment
- Business Type
- Location
- Primary Contact
- Role
- Phone
- Email

### Research
- Public Evidence / Research Notes
- Why Product May Fit
- Research Source / URL
- Suspected Operational Pain

### Prospecting
- Status
- Assigned User
- Last Contact Date
- Next Action
- Next Action Date
- Created / Updated metadata

### Suggested status model

Keep V1 deliberately small:

- `Research`
- `Ready`
- `Contacting`
- `Engaged`
- `Converted`
- `Disqualified`

Do not model individual call attempts as statuses.

---

## 4. Campaign

Qualification must **not** be hard-coded into Prospect because the same organisation may be relevant to different 3 Inventors products for different reasons.

Example campaign:

**Chaplin Pro — Cyprus ICP Validation — September 2026**

Suggested Campaign fields:
- Name
- Product (use existing Product relationship if available)
- Description / Objective
- Market / Geography
- Start Date
- End Date (optional)
- Status
- Weekly Call Target
- Owner
- Notes

Conceptual relationship:

**Campaign → ProspectCampaignEntry → Prospect**

The campaign entry should hold campaign-specific qualification data such as:
- ICP Fit
- Pain Probability
- Accessibility
- Learning Value
- Total Score
- Priority
- Why this prospect fits this campaign
- Recommended First Contact
- Campaign-specific status/notes if required

This allows the same Prospect to participate later in another campaign without corrupting its core identity.

---

## 5. Qualification Scoring

Current Chaplin Pro validation model:

- **ICP Fit:** 0–5
- **Pain Probability:** 0–5
- **Accessibility:** 0–5
- **Learning Value:** 0–5
- **Total:** 0–20

Priority:
- **A:** 16–20
- **B:** 12–15
- **Hold / Lower Priority:** below 12

Prefer calculating Total from the four dimensions rather than accepting inconsistent manual totals.

Do not make these four dimensions globally permanent if the current architecture can support campaign-specific criteria cleanly. V1 may use this fixed model for the Chaplin campaign, but implementation should avoid making `ChaplinICPScore` a Prospect property.

---

## 6. Prospect Activities

Do not create fields such as `Call1Date`, `Call2Date`, `Email1Date`.

Use an activity timeline.

Suggested Activity types:
- Research
- Call
- Email
- Meeting
- Demo
- Note
- LinkedIn / Social
- Other

Suggested fields:
- Prospect
- Campaign (optional/where appropriate)
- Activity Type
- Date/Time
- User
- Outcome
- Notes
- Next Action
- Next Action Date

Example timeline:

- 22 Sep — Research — Strong ICP; conference organiser
- 24 Sep — Call — No answer
- 25 Sep — Call — Receptionist requested email
- 25 Sep — Email — Introduction sent
- 28 Sep — Call — Event manager interested in demonstration
- Convert to Lead

The activity history must remain accessible after conversion.

---

## 7. Convert Prospect to Lead

Add a clear **Convert to Lead** action from the Prospect.

Conversion should:
- Create/use the appropriate Contact according to existing Portal rules
- Pre-populate known company/contact information where supported
- Link Product from the campaign where applicable
- Set Source appropriately (`Cold Call`, `Prospecting`, or existing supported equivalent)
- Preserve a reference to the originating Prospect
- Preserve campaign attribution
- Carry relevant notes/request context without duplicating unnecessary data
- Mark the Prospect/Campaign entry as Converted
- Prevent accidental duplicate conversion

Prefer an explicit relationship such as `Lead.ProspectId` only if it fits the existing domain model. Inspect the current Lead schema first.

### Existing New Lead UI

Current Lead form contains:
- Contact *
- Product
- Source *
- Source Reference
- Source URL
- Request Details

When `Source = Cold Call` / prospecting source, allow selection/reference of an existing Prospect where appropriate.

The preferred UX is still:

**Prospect Detail → Convert to Lead**

rather than forcing the user to recreate the Lead manually.

---

## 8. Excel Import — Current 30-Prospect Format

The existing workbook is:

`Chaplin-Pro-30-Cyprus-Prospects.xlsx`

Primary sheet: **30 Prospects**

The exact current column order is:

| # | Excel Column | Import Meaning |
|---:|---|---|
| 1 | `ID` | Import/external row identifier; do not assume DB primary key |
| 2 | `Prospect` | Organisation/prospect name |
| 3 | `Segment` | Campaign segment / industry grouping |
| 4 | `Location` | Prospect location |
| 5 | `Business Type` | Business classification |
| 6 | `Public Contact / Role` | Publicly identified contact and/or role |
| 7 | `Phone` | Public business phone |
| 8 | `Email` | Public business email |
| 9 | `Official Website` | Prospect website |
| 10 | `Public Evidence` | Research evidence supporting qualification |
| 11 | `Why Chaplin Pro May Fit` | Campaign-specific fit rationale |
| 12 | `ICP Fit /5` | Qualification score |
| 13 | `Pain Probability /5` | Qualification score |
| 14 | `Accessibility /5` | Qualification score |
| 15 | `Learning Value /5` | Qualification score |
| 16 | `Total /20` | Calculated/import validation value |
| 17 | `Priority` | A / B / Hold or equivalent |
| 18 | `Recommended First Contact` | Suggested outreach route/person |
| 19 | `Research Source` | Source URL/reference |

### Import behaviour

Import must:
1. Validate required values.
2. Trim whitespace and normalise obvious formatting safely.
3. Validate score range 0–5.
4. Recalculate Total and flag/reject mismatches rather than silently trusting invalid totals.
5. Map Priority consistently from score or validate imported Priority.
6. Detect likely duplicate Prospects using existing Portal conventions; do not blindly create duplicates.
7. Create Prospect records plus their Campaign entries.
8. Preserve research text and source URLs.
9. Produce an import result summary: created / matched / skipped / invalid.
10. Allow safe correction/re-import without producing duplicate campaign memberships.

The workbook also contains **Campaign Summary** and **Weekly Call Tracker** sheets. These are operational/reference sheets and should **not** be imported as Prospects.

### Future export/import template

After implementation, Portal should be able to provide/download an import template using the same or a clearly versioned canonical format. If schema naming differs internally, keep the import mapping explicit rather than changing the user's source workbook silently.

---

## 9. Prospect List / Working View

The main Prospect list should support quick operational use.

Recommended visible columns:
- Prospect
- Segment
- Campaign
- Product
- Primary Contact
- Priority
- Score
- Status
- Last Activity
- Next Action Date
- Assigned To

Useful filters:
- Campaign
- Product
- Segment
- Priority
- Status
- Assigned User
- Next Action / overdue
- Converted / not converted

Useful actions:
- Open Prospect
- Add Activity
- Schedule Next Action
- Convert to Lead
- Disqualify

---

## 10. Campaign Dashboard — V1

A simple campaign summary is enough:

**Total Prospects → Ready → Contacted → Engaged → Leads → Demos → Pilots → Won**

For the initial Chaplin campaign:

- Target cohort: 30 prospects
- Founder-led outreach target: 15 new calls/week
- Monday: research/qualification
- Tuesday–Thursday: 5 new targeted calls/day
- Friday: review evidence and prepare next week's leads

Do not overbuild analytics in V1. The dashboard must support operational review, not become a BI project.

---

## 11. Evidence Capture

The purpose is not only CRM administration. The module must help 3 Inventors learn its ICP.

For every meaningful prospect interaction, make it practical to preserve:

1. Current tools/process
2. Biggest operational friction
3. Exact customer language describing the problem
4. Product capability generating strongest reaction
5. Important missing capability
6. Support/software-provider complaints
7. Willingness to pilot
8. Willingness to pay
9. Objection / disqualification reason

Avoid forcing all of these into mandatory fields for every call. They may live in structured activity outcome fields and/or notes depending on the existing architecture.

---

## 12. Domain Principle

Do not make **Prospect = Lead**.

The system should preserve the full commercial acquisition chain:

**Campaign → Research → Prospect → Activities → Lead → Opportunity → Customer**

A failed/no-answer cold call is still useful prospecting evidence and must not pollute the Lead pipeline.

The same Prospect may participate in multiple product/campaign contexts over time.

---

## 13. Implementation Instructions for KIRO

Before implementation:

1. Inspect the existing Portal solution and architecture.
2. Locate current Contact, Company/Customer, Product, Lead, Opportunity, Source, activity/history, tenancy and audit entities.
3. Inspect EF Core migrations and existing enum/status conventions.
4. Inspect the current **New Lead** flow shown in the Portal.
5. Reuse existing relationships/components where correct.
6. Do not invent duplicate Contact/Company/Product concepts.
7. Do not alter Lead/Opportunity semantics merely to make the import easier.
8. If the current schema conflicts with this specification, document the conflict and propose the smallest coherent change before implementation.
9. Maintain tenant isolation and existing authorisation conventions.
10. Make import idempotent/safe enough for practical repeated use.

### Deliverables

- Prospect domain/entity implementation
- Campaign + campaign membership/qualification implementation
- Prospect activity/history implementation
- Prospect list/detail/create/edit UI
- Campaign list/detail/basic dashboard
- Excel import for the specified format
- Convert-to-Lead workflow
- Lead-origin linkage/history
- Filters and next-action workflow
- Migration(s)
- Validation
- Tests appropriate to existing project conventions
- Short implementation note documenting final mappings and any deviations from this specification

---

## 14. Acceptance Scenario

The immediate acceptance test is the real Chaplin Pro campaign.

1. Create campaign: **Chaplin Pro — Cyprus ICP Validation — September 2026**
2. Set Product = Chaplin Pro.
3. Set weekly call target = 15.
4. Import `Chaplin-Pro-30-Cyprus-Prospects.xlsx`.
5. Confirm 30 valid campaign prospects are available with scores and priorities.
6. Select Priority-A prospects for outreach.
7. Record call/email activities without creating Leads.
8. Schedule follow-ups.
9. When a prospect meaningfully engages, use **Convert to Lead**.
10. Confirm the resulting Lead retains attribution to the Prospect and Campaign.
11. Continue existing Lead → Opportunity workflow.
12. On Friday, review campaign funnel and prospect evidence to determine the next week's targets.

## Final Product Principle

> **Prospects organise who we intend to approach. Leads record genuine commercial engagement. Opportunities represent qualified commercial potential.**

Build the smallest coherent V1 that lets 3 Inventors run this real workflow inside Portal instead of spreadsheets, then refine it from actual usage.
