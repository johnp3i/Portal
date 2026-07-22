# Sales Leads (Opportunities) — Testing Scenarios

## Prerequisites

1. Run migrations 131–147 against the Portal database (creates `[sales]` schema and all tables)
2. Run `Seed_PlanFeature_Sales.sql` to enable the `sales` module on Professional and Enterprise plans
3. Business has Professional or Enterprise subscription
4. At least one user account associated with the business
5. Log in as a user with full access to the `sales` module

---

## Scenario 1: Navigation — Opportunities Sidebar

1. Log in as a Professional tier user
2. Check sidebar for "OPPORTUNITIES" section
3. **Expected:** Section visible with items: Lead Board (main), Contacts, Products, Meetings, Templates
4. Click "Lead Board"
5. **Expected:** Kanban board page loads with empty columns for each stage (New, Contacted, Follow-Up, Meeting Scheduled, Proposal Sent, Won, Lost, Inactive)

---

## Scenario 2: Create a Sales Product

1. Navigate to **Opportunities → Products**
2. Click "New Product"
3. Fill in: Name = "Website Redesign", Description = "Full branding and web package"
4. Click "Save"
5. **Expected:** SweetAlert2 success → page reloads → product visible in list with "Active" badge

---

## Scenario 3: Create a Contact — Happy Path

1. Navigate to **Opportunities → Contacts**
2. Click "New Contact"
3. Fill in: First Name = "Maria", Last Name = "Costa", Email = "maria@example.com", Phone = "+356 9999 0001", Company = "Acme Ltd"
4. Click "Save"
5. **Expected:** SweetAlert2 success → page reloads → contact appears in list

---

## Scenario 4: Contact Deduplication — Email

1. With Maria Costa already created (email = maria@example.com)
2. Click "New Contact"
3. Fill in: First Name = "Test", Email = "maria@example.com"
4. Click "Save"
5. **Expected:** SweetAlert2 error with message containing "A contact with this email already exists: Maria Costa"

---

## Scenario 5: Contact Deduplication — Phone

1. Click "New Contact"
2. Fill in: First Name = "Test", Phone = "+356 9999 0001"
3. Click "Save"
4. **Expected:** SweetAlert2 error with message containing "A contact with this phone number already exists"

---

## Scenario 6: Contact Requires Email or Phone

1. Click "New Contact"
2. Fill in: First Name = "No Contact Info" (leave Email and Phone empty)
3. Click "Save"
4. **Expected:** SweetAlert2 error: "Either email or phone number is required."

---

## Scenario 7: Create a Lead — Full Flow

1. Navigate to **Opportunities → Lead Board**
2. Click "New Lead"
3. Select Contact = "Maria Costa", Product = "Website Redesign", Source = "Website"
4. Fill in Request Details = "Interested in a new website for spring launch"
5. Click "Create Lead"
6. **Expected:** SweetAlert2 success → Kanban reloads → card appears in "New" column showing "Maria Costa" and "Website Redesign"

---

## Scenario 8: Lead Board Kanban — Card Navigation

1. On the Lead Board page, click the lead card for "Maria Costa"
2. **Expected:** Navigates to `/Sales/LeadDetail/{id}` showing full lead information

---

## Scenario 9: Lead Detail — Stage Change

1. On the Lead Detail page, use the stage dropdown to change from "New" to "Contacted"
2. **Expected:** BlockUI → page reloads → stage badge shows "Contacted" with blue colour

---

## Scenario 10: Lead Detail — Compose Response (Template Selection)

1. Ensure at least one active template exists (e.g., "Standard Web Response" for "Website Redesign")
2. On the Lead Detail page for a lead linked to "Website Redesign", click "Compose Response"
3. **Expected:** Compose Response modal opens with:
   - Template dropdown populated with active templates
   - Subject field (empty, read-only)
   - Body Preview area (empty placeholder text)
   - Buttons: "Copy Body", "Copy All", "Cancel", "Log Response"
4. Select "Standard Web Response" from the template dropdown
5. **Expected:** BlockUI briefly shows "Rendering template..." → Subject and Body fill with rendered content:
   - Subject: "Re: Website Enquiry"
   - Body: "Hi Maria Costa, thanks for your interest in Website Redesign. We'll respond within 4 hours."
6. **Expected:** Placeholders (`{{ContactName}}`, `{{ProductName}}`, `{{ResponseTime}}`) are replaced with actual values

---

## Scenario 10b: Lead Detail — Compose Response (Copy Subject)

1. With a template rendered in the Compose Response modal (Scenario 10 step 5 complete)
2. Click the "Copy" button next to the Subject field
3. **Expected:** SweetAlert2 toast: "Subject copied to clipboard." (auto-closes after 1.5s)
4. Paste into a text editor
5. **Expected:** Clipboard contains "Re: Website Enquiry"

---

## Scenario 10c: Lead Detail — Compose Response (Copy Body)

1. With a template rendered in the Compose Response modal
2. Click "Copy Body"
3. **Expected:** SweetAlert2 toast: "Body copied to clipboard." (auto-closes after 1.5s)
4. Paste into a text editor
5. **Expected:** Clipboard contains the rendered body text (plain text extracted from HTML)

---

## Scenario 10d: Lead Detail — Compose Response (Copy All)

1. With a template rendered in the Compose Response modal
2. Click "Copy All"
3. **Expected:** SweetAlert2 toast: "Subject and body copied to clipboard." (auto-closes after 1.5s)
4. Paste into a text editor
5. **Expected:** Clipboard contains "Subject: Re: Website Enquiry\n\n" followed by body text

---

## Scenario 10e: Lead Detail — Compose Response (Log Response)

1. With a template rendered in the Compose Response modal
2. Click "Log Response"
3. **Expected:** BlockUI "Logging response..." → SweetAlert2 success: "Response recorded successfully." → page reloads
4. Check the "Responses" section on the Lead Detail page
5. **Expected:** New response entry appears with channel = "Email", response text = rendered body, timestamp
6. Check Activity Feed
7. **Expected:** "Response Logged" entry visible

---

## Scenario 10f: Lead Detail — Compose Response (No Templates)

1. Deactivate all templates, then open a lead and click "Compose Response"
2. **Expected:** Modal opens, template dropdown shows "No templates available"
3. **Expected:** "Log Response" button shows SweetAlert2 warning: "Please select a template before logging a response."

---

## Scenario 10g: Lead Detail — Compose Response (Cancel)

1. Open Compose Response modal, select a template
2. Click "Cancel" or the × button
3. **Expected:** Modal closes, no response logged, no side effects

---

## Scenario 11: Lead Detail — Schedule Meeting

1. On the Lead Detail page, click "Schedule Meeting"
2. **Expected:** Redirects to Meetings page or opens meeting form
3. Fill in: Subject = "Discovery call", Type = "Phone Call", Date = tomorrow, Duration = 30 min
4. Submit
5. **Expected:** Meeting created → appears on lead detail's Meetings section
6. **Expected:** Stage auto-advances to "Meeting Scheduled" (if current stage < 4)

---

## Scenario 12: Lead Detail — Create Proposal (Document Linking)

1. On the Lead Detail page, click "Create Proposal"
2. **Expected:** Navigates to quotation creation with `leadRequestId` query parameter
3. Create and save the quotation
4. Return to Lead Detail
5. **Expected:** Quotation appears in "Linked Documents → Quotations" section
6. **Expected:** Stage auto-advances to "Proposal Sent" (if current stage < 5)

---

## Scenario 13: Lead Detail — Mark as Won

1. On the Lead Detail page (non-terminal stage), click "Mark as Won"
2. **Expected:** SweetAlert2 confirmation dialog: "This will move the lead to Won and convert the contact to a customer."
3. Confirm
4. **Expected:** 
   - Stage badge changes to "Won" (green)
   - Action buttons disappear (terminal stage)
   - "Reopen (→ New)" button appears
   - A new Customer record is created in `[customer].[Customer]` with `ContactId` FK set

---

## Scenario 14: Lead Detail — Cancel Lead

1. Create a new lead and navigate to its detail
2. Click "Cancel"
3. **Expected:** SweetAlert2 dialog with input field for reason
4. Enter reason: "Client went with competitor", confirm
5. **Expected:** Lead shows "Cancelled" badge, action buttons disabled

---

## Scenario 15: Lead Detail — Reopen Terminal Lead

1. On a Won or Cancelled lead, click "Reopen (→ New)"
2. **Expected:** Stage resets to "New", action buttons reappear

---

## Scenario 16: Lead Board Filtering

1. Create multiple leads: some assigned to current user, some unassigned, some with different products
2. On Lead Board page, filter by Product = "Website Redesign"
3. **Expected:** Only leads with that product show in Kanban columns
4. Filter by Assigned To = current user
5. **Expected:** Only assigned leads show
6. Click "Clear"
7. **Expected:** All leads visible again

---

## Scenario 17: Lead Board — Table View Toggle

1. On Lead Board page, click "Table" button
2. **Expected:** Kanban hides, table view appears showing all leads with Contact, Company, Product, Stage, Source, Created columns
3. Click "Board" button
4. **Expected:** Back to Kanban view

---

## Scenario 18: Contact Detail — Interest History

1. Navigate to Contacts list, click on "Maria Costa"
2. **Expected:** Contact Detail page shows contact info + Interest History table listing all leads for this contact
3. Each lead row shows Product, Stage (with colour badge), Request text (truncated), Created date, and "View" link to LeadDetail

---

## Scenario 19: Response Templates — CRUD (Quill Editor)

1. Navigate to **Opportunities → Templates**
2. Click "New Template"
3. Fill in:
   - Name = "Standard Web Response"
   - Product = "Website Redesign"
   - Channel = "Email"
   - Subject = "Re: Website Enquiry"
   - Body (Quill editor) = "Hi {{ContactName}}, thanks for your interest in {{ProductName}}. We'll respond within {{ResponseTime}}."
   - Response Time = 4 hours
4. **Expected:** Body field is a Quill rich text editor with toolbar (bold, italic, underline, lists, link, clean)
5. Click "Save"
6. **Expected:** Template appears in list with Product = "Website Redesign", Channel = "Email", 4 hrs
7. Click "Edit" on the template
8. **Expected:** Quill editor loads with the saved HTML content (formatted text preserved)
9. Add bold formatting to "{{ContactName}}", click "Save"
10. **Expected:** SweetAlert2 success → page reloads → edit reopens to verify bold persists in HTML

---

## Scenario 20: Template Deactivation

1. On Templates list, click the "Deactivate" button on "Standard Web Response"
2. **Expected:** SweetAlert2 confirmation dialog: 'Deactivate Template? Are you sure you want to deactivate "Standard Web Response"?'
3. Confirm
4. **Expected:** SweetAlert2 success → page reloads → template status changes to "Inactive" (red pill badge)
5. Open Compose Response on a lead for the same product
6. **Expected:** Deactivated template does NOT appear in the template dropdown

---

## Scenario 20b: Template Reactivation

1. On Templates list, click the "Activate" button on an inactive template
2. **Expected:** SweetAlert2 confirmation: 'Activate Template? Reactivate "Standard Web Response"?'
3. Confirm
4. **Expected:** SweetAlert2 success → page reloads → template status returns to "Active" (green pill badge)
5. Open Compose Response on a lead for the same product
6. **Expected:** Reactivated template now appears in the template dropdown

---

## Scenario 21: Meeting Management

1. Navigate to **Opportunities → Meetings**
2. Click "Schedule Meeting"
3. Fill in: Contact = "Maria Costa", Subject = "Project scope", Type = "Video Call", Date = next week, Duration = 45, Location = "Zoom"
4. Click "Schedule"
5. **Expected:** Meeting appears in list with "Upcoming" status badge
6. Click the calendar icon (📅) to download ICS
7. **Expected:** `.ics` file downloads with correct VEVENT (SUMMARY, DTSTART, DTEND, LOCATION)

---

## Scenario 22: Meeting Cancellation

1. On the Meetings list, click "Cancel" (❌) on an upcoming meeting
2. **Expected:** SweetAlert2 confirmation
3. Confirm
4. **Expected:** Meeting shows "Cancelled" badge (red), cancel button disappears

---

## Scenario 23: Product Deactivation

1. Navigate to Products, click deactivate on "Website Redesign"
2. **Expected:** SweetAlert2 confirmation
3. Confirm
4. **Expected:** Product status changes to "Inactive"
5. Product still appears in existing leads but is no longer offered in dropdowns for new leads

---

## Scenario 24: Contact Search

1. Navigate to Contacts with 10+ contacts
2. Type "costa" in the search box, click Search
3. **Expected:** Only contacts matching "costa" in first name, last name, email, company, or phone appear
4. Click "Clear"
5. **Expected:** All contacts shown again

---

## Scenario 25: Tier Gating — Foundation User

1. Log in as a Foundation tier user (no `sales` module in plan)
2. Navigate directly to `/Sales/Pipeline`
3. **Expected:** UpgradeRequired view shown (HTTP 403)
4. Check sidebar
5. **Expected:** "OPPORTUNITIES" section not visible

---

## Scenario 26: Contact-to-Customer — Existing Customer Match

1. Create a Customer in the system with email "maria@example.com"
2. Create a sales contact with same email, create a lead, and click "Mark as Won"
3. **Expected:** System detects existing customer by email, links rather than creating duplicate
4. No new customer row created — the existing customer's ID is returned

---

## Scenario 27: Multi-tenant Isolation

1. Log in as Business A user, create contacts, products, leads
2. Log in as Business B user
3. Navigate to Lead Board, Contacts, Products
4. **Expected:** Business B sees zero items — cannot see Business A's data
5. Try direct URL `/Sales/LeadDetail/{businessA_leadId}`
6. **Expected:** 404 Not Found (global query filter prevents access)

---

## Database Verification Checklist

- [ ] `[sales].[Contact]` records scoped by `BusinessId`
- [ ] `[sales].[Contact]` partial unique index prevents duplicate email within same business
- [ ] `[sales].[Contact]` partial unique index prevents duplicate phone within same business
- [ ] `[sales].[LeadRequest]` defaults: `LeadStatusTypeId = 1`, `IsCancelled = 0`, `IsActive = 1`
- [ ] `[sales].[LeadRequest]` FK to `[sales].[Contact]` enforced
- [ ] `[sales].[LeadResponse]` records linked to correct `LeadRequestId`
- [ ] `[sales].[Meeting]` FK to `[sales].[Contact]` and optional FK to `[sales].[LeadRequest]`
- [ ] `[quotation].[Quotation].[LeadRequestId]` set when proposal linked from pipeline
- [ ] `[invoice].[Invoice].[LeadRequestId]` set when invoice linked from pipeline
- [ ] `[customer].[Customer].[ContactId]` set when contact converted via "Mark as Won"
- [ ] Stage transitions: Won = 6, Lost = 7, Inactive = 8 (all terminal)
- [ ] `[sales].[LeadStatusType]` seed data matches: New(1), Contacted(2), Follow-Up(3), Meeting Scheduled(4), Proposal Sent(5), Won(6), Lost(7), Inactive(8)


---

## Scenario 28: Team — Add a Team Member

1. Navigate to **Opportunities → Team**
2. Click "Add Member"
3. Fill in: First Name = "Alex", Last Name = "Demetriou", Email = "alex@example.com", Phone = "+357 99123456", Role = "Sales Agent"
4. Click "Save"
5. **Expected:** SweetAlert2 success → page reloads → team member appears in list with "Active" badge

---

## Scenario 29: Team — Edit a Team Member

1. On Team list, click "Edit" on Alex Demetriou
2. Change Role to "Senior Sales Agent"
3. Click "Save"
4. **Expected:** SweetAlert2 success → page reloads → role updated

---

## Scenario 30: Team — Deactivate and Reactivate

1. On Team list, click "Deactivate" on Alex Demetriou
2. **Expected:** SweetAlert2 confirmation → confirm → status changes to "Inactive", row greyed out
3. Click "Activate" on the same member
4. **Expected:** Status returns to "Active"
5. **Expected:** Deactivated members do NOT appear in lead assignment dropdowns

---

## Scenario 31: Team — Email Uniqueness

1. Create a team member with email "alex@example.com" (already exists)
2. **Expected:** SweetAlert2 error: "A team member with this email already exists: Alex Demetriou"

---

## Scenario 32: Lead Assignment to Team Member

1. Navigate to Lead Board, open a lead (click card → opens in new tab)
2. On Lead Detail, find the "Assigned To" field
3. Select "Alex Demetriou" from the dropdown
4. **Expected:** Assignment saved → "Assigned To" shows "Alex Demetriou"
5. Return to Lead Board, filter by "Assigned To" = Alex Demetriou
6. **Expected:** Only Alex's assigned leads appear

---

## Scenario 33: Lead Unassignment

1. On Lead Detail for an assigned lead, change "Assigned To" to "Unassigned"
2. **Expected:** Assignment cleared → shows "Unassigned"

---

## Scenario 34: Activity Feed — Lead Created

1. Create a new lead (via "New Lead" on Lead Board)
2. Open the lead detail
3. Scroll to "Activity Feed" section
4. **Expected:** First entry shows: "Lead Created" with contact name, product, source, and timestamp

---

## Scenario 35: Activity Feed — Stage Change

1. On a lead, change stage from "New" to "Contacted"
2. Check Activity Feed
3. **Expected:** Entry shows: "Stage Changed — Moved from New to Contacted" with user name

---

## Scenario 36: Activity Feed — Cancel and Reactivate

1. Cancel a lead with reason "Testing cancellation"
2. Check Activity Feed
3. **Expected:** Entry shows: "Lead Cancelled — Reason: Testing cancellation"
4. Reactivate the lead
5. Check Activity Feed
6. **Expected:** Entry shows: "Lead Reactivated — Lead reactivated and returned to New stage"

---

## Scenario 37: Activity Feed — Response Logged

1. On a lead, click "Log Response", enter text, save
2. Check Activity Feed
3. **Expected:** Entry shows: "Response Logged" with channel and snippet of the response text

---

## Scenario 38: Activity Feed — Meeting Scheduled

1. Schedule a meeting from a lead
2. Check Activity Feed
3. **Expected:** Entry shows: "Meeting Scheduled" with subject and date

---

## Scenario 39: Activity Feed — Proposal Linked

1. Create a proposal from a lead (via "Create Proposal" button)
2. Return to Lead Detail
3. Check Activity Feed
4. **Expected:** Entry shows: "Proposal Linked" with quotation reference

---

## Scenario 40: Activity Feed — Mark as Won

1. Mark a lead as Won
2. Check Activity Feed
3. **Expected:** Entry shows: "Marked as Won" with user name

---

## Scenario 41: Activity Feed — Assignment

1. Assign a lead to a team member
2. Check Activity Feed
3. **Expected:** Entry shows: "Assigned — Assigned to Alex Demetriou"
4. Unassign the lead
5. **Expected:** Entry shows: "Unassigned"

---

## Scenario 42: Activity Feed — Pagination

1. Perform many actions on a lead (20+)
2. Check Activity Feed
3. **Expected:** First 20 entries shown, "Load more" button visible
4. Click "Load more"
5. **Expected:** Next batch loaded below

---

## Scenario 43: Team — Deactivated Member on Existing Leads

1. Assign a lead to Alex Demetriou
2. Deactivate Alex Demetriou from the Team page
3. Return to the lead detail
4. **Expected:** "Assigned To" still shows "Alex Demetriou" (historical data preserved)
5. Open the assignment dropdown
6. **Expected:** Alex does NOT appear in the dropdown (deactivated)

---

## Scenario 44: Cancelled Leads Section on Lead Board

1. Cancel a lead from Lead Detail
2. Return to the Lead Board
3. **Expected:** Cancelled lead does NOT appear in any Kanban column
4. **Expected:** "Cancelled Leads" section appears below the board
5. Expand the section
6. **Expected:** The cancelled lead card is shown with red-tinted border
7. Click the card
8. **Expected:** Opens LeadDetail in a new tab with the cancelled banner and "Reactivate Lead" button
