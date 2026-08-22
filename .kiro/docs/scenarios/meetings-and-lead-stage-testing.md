# Testing Scenarios: Meetings Page Enhancements & Lead Stage Reevaluation

## Prerequisites

- Run migration `176_CreateLeadTrackingHistory.sql` against the Portal database
- Run migration `177_BackfillLeadTrackingHistory.sql` to seed history for existing leads
- Have at least one contact and one lead in the system
- Log in as a user with Sales module access

---

## Scenario 1: Meetings Page — Default Filter and AJAX Loading

1. Navigate to `/Sales/Meetings`
2. **Expected:** Page loads with the Status dropdown defaulted to "Upcoming"
3. **Expected:** Table shows only upcoming meetings (future, non-cancelled)
4. **Expected:** No full page reload — data loads via AJAX with a "Loading meetings..." indicator
5. **Expected:** If no upcoming meetings exist, the table shows "No meetings found." and pagination is hidden

---

## Scenario 2: Meetings Page — Filter Panel

1. On the Meetings page, change the Status dropdown to "All"
2. Click "Filter"
3. **Expected:** Table reloads showing all meetings (upcoming + completed + cancelled)
4. Change Status to "Completed", click "Filter"
5. **Expected:** Only past, non-cancelled meetings appear with green "Completed" or red "Needs Outcome" urgency pills
6. Change Status to "Cancelled", click "Filter"
7. **Expected:** Only cancelled meetings appear with red "Cancelled" pill and an "Activate" button instead of Edit/Cancel
8. Select a Meeting Type from the dropdown, click "Filter"
9. **Expected:** Results narrowed to that type only
10. Click "Clear"
11. **Expected:** All filters reset, Status returns to "Upcoming", table reloads

---

## Scenario 3: Meetings Page — Quick Date Presets

1. Click "This Month" in the Quick presets row
2. **Expected:** Date From = first of current month, Date To = today, table reloads
3. Click "Last Month"
4. **Expected:** Date From = first of last month, Date To = last day of last month
5. Click "All Time"
6. **Expected:** Both date fields clear, shows all matching the current Status filter
7. Click "Next Month"
8. **Expected:** Date From = first of next month, Date To = last day of next month
9. **Expected:** Active button is highlighted with a visual indicator

---

## Scenario 4: Meetings Page — Pagination

1. Ensure the database has more than 15 meetings (create some if needed)
2. Set Status to "All", clear date filters, click "Filter"
3. **Expected:** Table shows first 15 meetings
4. **Expected:** Below the table: "Showing 1–15 of X meetings"
5. **Expected:** Page buttons appear (windowed if many pages: 1 ... 3 4 5 ... 10)
6. Click page 2
7. **Expected:** Table updates with items 16–30, pagination info updates, page 2 button is highlighted

---

## Scenario 5: Meetings Page — Urgency Indicators

1. Create meetings at different times to trigger each urgency state:
   - A meeting scheduled for today → amber "Today" pill
   - A meeting scheduled for tomorrow → blue "Upcoming" pill
   - A past meeting with no outcome → red "Needs Outcome" pill
   - A past meeting with an outcome recorded → green "Completed" pill
   - A cancelled meeting → red "Cancelled" pill
2. Set Status to "All" and verify each pill displays correctly
3. **Expected:** Each meeting shows exactly one urgency pill

---

## Scenario 6: Meetings Page — Relative Time Labels

1. Look at the "Scheduled" column for any meeting
2. **Expected:** Below the formatted date, a grey label shows relative time:
   - Future meeting within 24h: "in X hours"
   - Future meeting beyond 24h: "in X days"
   - Past meeting within 24h: "X hours ago"
   - Past meeting beyond 24h: "X days ago"

---

## Scenario 7: Meetings Page — Edit Meeting Modal

1. Find an upcoming meeting in the table, click the pencil (✏) icon
2. **Expected:** BlockUI shows briefly, then the Edit Meeting modal opens
3. **Expected:** All fields pre-populated: Subject, Meeting Type, Date/Time, Duration, Location, Notes
4. **Expected:** Contact name displayed as read-only text (not editable)
5. **Expected:** Outcome textarea is empty (or filled if already recorded)
6. Change the Subject, add an Outcome, click "Save Changes"
7. **Expected:** BlockUI shows "Updating...", then success SweetAlert, modal closes, table refreshes on current page
8. Verify the updated subject appears in the table

---

## Scenario 8: Meetings Page — Cancel and Reactivate (AJAX reload)

1. Find an upcoming meeting, click "Cancel"
2. **Expected:** SweetAlert confirmation: "Cancel Meeting? Cancel "Subject"? This cannot be undone."
3. Click "Yes, cancel it"
4. **Expected:** BlockUI → success SweetAlert → table reloads on current page (no full page reload)
5. Switch Status filter to "Cancelled"
6. Find the cancelled meeting, click "Activate"
7. **Expected:** SweetAlert confirmation → BlockUI → success → table reloads
8. Switch back to "Upcoming" — the meeting should reappear

---

## Scenario 9: Meetings Page — Create Meeting Bug Fix

1. Navigate to `/Sales/Meetings?leadRequestId=1&contactId=1` (simulate coming from Lead Detail)
2. **Expected:** The Create Meeting modal auto-opens with contact pre-selected
3. Fill in Subject, Date/Time, click "Schedule"
4. **Expected:** Success SweetAlert, then redirect to `/Sales/Meetings` (clean URL, no query params)
5. **Expected:** The Create Modal does NOT re-open after redirect
6. **Expected:** The URL bar shows `/Sales/Meetings` with no `?leadRequestId=...`

---

## Scenario 10: Meetings Page — Calendar Task Download

1. Find an upcoming meeting, click "Calendar Task"
2. **Expected:** A `.ics` file downloads
3. Open the file in a calendar app (or text editor)
4. **Expected:** Contains correct DTSTART, DTEND, SUMMARY, LOCATION matching the meeting

---

## Scenario 11: Lead Stage Reevaluation — Cancel Last Meeting

1. Navigate to `/Sales/Pipeline`, create a new lead (it starts at "New" stage)
2. Schedule a meeting for that lead (from Lead Detail → Schedule Meeting)
3. **Expected:** Lead moves to "Meetings" stage (stage 4) on the Pipeline board
4. Navigate to `/Sales/Meetings`, find the meeting, click "Cancel"
5. After confirmation, go back to Pipeline
6. **Expected:** The lead has regressed from "Meetings" back to an earlier stage:
   - If the lead had responses → "Contacted" (stage 2)
   - If no responses → "New" (stage 1)
7. **Expected:** A "MeetingCancelled" record exists in `[sales].[LeadTrackingHistory]`

---

## Scenario 12: Lead Stage Reevaluation — Cancel Non-Last Meeting

1. Create a lead, schedule TWO meetings for it
2. **Expected:** Lead is at "Meetings" stage
3. Cancel ONE of the two meetings
4. **Expected:** Lead STAYS at "Meetings" stage (the other meeting is still active)
5. Cancel the second meeting
6. **Expected:** NOW the lead regresses (no valid meetings remain)

---

## Scenario 13: Lead Stage Reevaluation — Reactivate Meeting

1. From Scenario 11 or 12, the lead has regressed to "Contacted" or "New"
2. Navigate to Meetings, filter by "Cancelled", find the cancelled meeting
3. Click "Activate" to reactivate it
4. Go back to Pipeline
5. **Expected:** The lead has advanced back to "Meetings" stage (stage 4)
6. **Expected:** A "MeetingReactivated" record exists in tracking history

---

## Scenario 14: Lead Stage Reevaluation — No Regression Beyond Meetings

1. Create a lead, schedule a meeting (lead → "Meetings")
2. Link a proposal to the lead (lead → "Proposal", stage 5)
3. Cancel the meeting
4. **Expected:** Lead STAYS at "Proposal" stage — no regression beyond "Meetings"
5. The system only regresses leads that are AT stage 4, never stages 5/6/7

---

## Scenario 15: Lead Stage Reevaluation — Standalone Meeting

1. Schedule a meeting that is NOT linked to any lead (standalone meeting)
2. Cancel it
3. **Expected:** No stage logic runs, no errors, meeting is simply cancelled
4. No records appear in `[sales].[LeadTrackingHistory]` for this action

---

## Scenario 16: Lead Tracking History Audit Trail

1. After running through scenarios 11–14, query the database:
   ```sql
   SELECT * FROM [sales].[LeadTrackingHistory]
   ORDER BY [CreatedAtUtc] DESC
   ```
2. **Expected:** Complete audit trail showing:
   - MeetingScheduled (ActionTypeId=1) with meeting ID in RelatedEntityId
   - MeetingCancelled (ActionTypeId=2) with meeting ID in RelatedEntityId
   - MeetingReactivated (ActionTypeId=3) with meeting ID in RelatedEntityId
   - Each record has From/To stage IDs matching the actual transitions
