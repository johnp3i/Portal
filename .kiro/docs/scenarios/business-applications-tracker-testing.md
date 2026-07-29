# Business Applications Tracker — Testing Scenarios

## Prerequisites
- SQL migrations 162, 163, 164 applied to Portal database
- User logged in with Professional tier subscription (compliance module access)
- SuperAdmin account available for admin template management

## Scenario 1: SuperAdmin Template Management

### Steps
1. Login as SuperAdmin
2. Navigate to /AdminCompliance
3. Verify the seeded Cyprus templates are listed (IR7, Social Insurance Monthly, VAT Return, Annual Levy, Employer's Declaration)
4. Click "Create Template" — fill in Name: "Test Filing", Country: "Cyprus", Category: Tax, Frequency: Monthly, Due Day: 20
5. Click Save Template
6. Verify the new template appears in the list
7. Click Edit on the new template, change name to "Test Filing Updated"
8. Click Save Template
9. Click Deactivate → confirm
10. Verify template shows "Inactive" status

### Expected Results
- All CRUD operations succeed with SweetAlert2 feedback
- Duplicate name+country validation prevents creation of duplicates
- Deactivated templates don't appear in business import

## Scenario 2: Category Management

### Steps
1. Navigate to /AdminCompliance/Categories
2. Verify seeded categories: Tax, Employee, Regulatory, Business Registration
3. Create a new category "Insurance"
4. Edit it to "Insurance & Pensions"
5. Verify changes saved

## Scenario 3: Template Import with Due Day Override

### Steps
1. Login as business user with Professional plan
2. Navigate to /Compliance/Import
3. Select Country: Cyprus, Year: current year
4. Check all templates (Social Insurance Monthly, VAT Return, IR7, Employer's Declaration, Annual Levy)
5. Verify each template shows a due day input field pre-filled with the default value (e.g., 15 for Social Insurance, 10 for VAT)
6. Verify preview shows: 12 + 4 + 1 + 1 + 1 = 19 records
7. Click "Import Selected"
8. Verify success message shows "19 filing(s) imported"
9. Navigate to /Compliance — verify 19 filings in the list with the default due days

### Due Day Override
10. Navigate to /Compliance/Import again (use a different year to avoid duplicate warning)
11. Check "Social Insurance (Monthly)" only
12. Change the due day input from 15 to 25
13. Click "Import Selected"
14. Navigate to /Compliance — verify all 12 Social Insurance records have due date on the 25th of each month

### Edge Cases
15. Enter due day 31 for a template → February records should clamp to Feb 28 (or 29 in leap year)
16. Leave due day empty → uses the template's default day
17. Enter 0 or 32 → should be ignored (uses default)

### Duplicate Import
18. Navigate to /Compliance/Import again
19. Select same templates, same year
20. Click Import — should show duplicate warning SweetAlert2 dialog
21. Cancel → no new records created

## Scenario 4: Filing List & Filters

### Steps
1. Navigate to /Compliance
2. Verify all 19 filings displayed (paginated at 15)
3. Filter by Category: "Tax" — should show VAT + IR7 = 5 filings
4. Filter by Status: "Pending" — should show all 19 (all new)
5. Filter by date range: select next 3 months
6. Clear filters — back to full list
7. Click View on any filing

## Scenario 5: Status Workflow

### Steps
1. Open a Pending filing detail (/Compliance/Detail/{id})
2. Verify allowed transitions: "Mark In Progress" and "Mark Submitted"
3. Click "Mark In Progress" → status changes to InProgress
4. Verify allowed transitions now: "Mark Submitted" only
5. Click "Mark Submitted" → status changes to Submitted, SubmittedAtUtc populated
6. Verify allowed transitions: "Approve" and "Reject"
7. Click "Approve" → status changes to Approved, ApprovedAtUtc populated
8. Verify no transition buttons shown (terminal state)

### Edge Case: Rejected → InProgress
9. Submit another filing to Submitted
10. Click "Reject" → status changes to Rejected
11. Verify "Mark In Progress" button appears (re-entry allowed)

## Scenario 6: Detail Edit

### Steps
1. Open a filing detail
2. Enter Reference Number: "SI-2027-01"
3. Enter Notes: "Submitted via TaxisNet"
4. Click "Save Details"
5. Reload page — verify values persisted

## Scenario 7: Attachment Upload/Download/Delete

### Steps
1. Open a filing detail
2. Click "Upload PDF" → select a valid PDF file (< 5 MB)
3. Verify success, page reloads showing "Attachments (1/3)"
4. Upload a second PDF → shows "Attachments (2/3)"
5. Upload a third PDF → shows "Attachments (3/3)", upload button disabled
6. Click Download on an attachment → file downloads correctly
7. Click Delete → SweetAlert2 confirmation → confirm → attachment removed

### Edge Cases
8. Try uploading a .docx file → "Only PDF files are accepted" error
9. Try uploading a 6 MB PDF → "File size must not exceed 5 MB" error

## Scenario 8: Calendar View

### Steps
1. Navigate to /Compliance/Calendar
2. Verify 12-month grid rendered
3. Verify dots appear in months with filings
4. Click on a month box → detail panel shows filings for that month
5. Navigate year forward/backward with arrows
6. Verify data reloads via AJAX

## Scenario 9: Dashboard Widget

### Steps
1. Navigate to / (Dashboard)
2. If user has Professional plan with compliance module:
   - Verify "Upcoming Filings" widget appears
   - Shows up to 5 filings due within 30 days
   - Overdue filings highlighted in red
   - "View All" link goes to /Compliance
3. If user does NOT have compliance access:
   - Widget should not render (empty)

## Scenario 10: Plan Permission Gating

### Steps
1. Login as Foundation tier user
2. Navigate to /Compliance → should see soft-gate teaser (not the module)
3. Login as Professional tier user → full access
4. Login as Enterprise tier user → full access

## Scenario 11: Create Custom Filing

### Steps
1. Login as business user with Professional plan
2. Navigate to /Compliance
3. Click "Create Filing" button in the topbar
4. Verify the create filing form appears (inline, with blue left border)
5. Fill in:
   - Filing Name: "Annual Insurance Renewal"
   - Category: select "Regulatory"
   - Due Date: pick a date 2 months from now
   - Notes: "Policy renewal for liability insurance"
6. Click "Create"
7. Verify SweetAlert2 success message
8. Page reloads — verify the new filing appears in the list with:
   - Name: "Annual Insurance Renewal"
   - Category: "Regulatory"
   - Status: "Pending"
   - Due date as selected

### Validation
9. Try creating without a name → "Please enter a filing name" validation
10. Try creating without a category → "Please select a category" validation
11. Try creating without a due date → "Please select a due date" validation

### Edge Cases
12. Create multiple custom filings with the same name → should succeed (no uniqueness constraint on custom filings)
13. Custom filings should NOT appear in the admin template catalog (IsActive=false on the generated ApplicationType)
14. Custom filings should appear in the Calendar view
15. Custom filings support the full status workflow (Pending → InProgress → Submitted → Approved)

## Scenario 12: Template Reactivation (SuperAdmin)

### Steps
1. Login as SuperAdmin
2. Navigate to /AdminCompliance
3. Find a template and click "Deactivate" → confirm
4. Verify the row becomes faded (opacity) with strikethrough on the name
5. Verify the "Deactivate" button is replaced by an "Activate" button (green)
6. Click "Activate" → confirm
7. Verify the row returns to normal styling
8. Verify the template appears in the business Import page again

### Expected Results
- Deactivated templates show with faded styling + strikethrough
- Activate button reactivates the template
- Reactivated templates become available for business import

## Verification Checklist

| # | Check | Pass? |
|---|-------|-------|
| 1 | Migrations apply without error | |
| 2 | Seed data populates 4 categories + 5 templates | |
| 3 | Import creates correct number of filings (12+4+1+1+1=19) | |
| 4 | Status transitions follow allowed paths only | |
| 5 | Invalid transitions return error message | |
| 6 | SubmittedAtUtc set on Submitted transition | |
| 7 | ApprovedAtUtc set on Approved transition | |
| 8 | Attachments limited to 3 per filing | |
| 9 | Only PDF files accepted | |
| 10 | File size limit 5 MB enforced | |
| 11 | Overdue filings show red + pulse badge | |
| 12 | Calendar loads via AJAX correctly | |
| 13 | Dashboard widget visible for Professional plan | |
| 14 | Dashboard widget hidden for Foundation plan | |
| 15 | Tenant isolation: can't access other business's filings | |
| 16 | Admin views accessible only by SuperAdmin | |
| 17 | All AJAX uses BlockUI + SweetAlert2 | |
| 18 | Mobile responsive below 576px | |
| 19 | Due day override applies to all records from template | |
| 20 | Feb 31 clamps to Feb 28/29 (leap year aware) | |
| 21 | Create custom filing works with all required fields | |
| 22 | Custom filing validation rejects empty name/category/date | |
| 23 | Custom ApplicationType has IsActive=false (hidden from import) | |
| 24 | Template reactivation restores import visibility | |
| 25 | Deactivated templates show faded + strikethrough in admin | |

## Database Verification Queries

```sql
-- Check seeded categories
SELECT * FROM [compliance].[ApplicationCategory];

-- Check seeded templates
SELECT ApplicationType.*, ApplicationCategory.Name AS CategoryName
FROM [compliance].[ApplicationType]
INNER JOIN [compliance].[ApplicationCategory]
    ON ApplicationType.ApplicationCategoryId = ApplicationCategory.Id;

-- Check imported filings for a business
SELECT BusinessApplication.*, ApplicationType.Name AS TypeName
FROM [compliance].[BusinessApplication]
INNER JOIN [compliance].[ApplicationType]
    ON BusinessApplication.ApplicationTypeId = ApplicationType.Id
WHERE BusinessApplication.BusinessId = @BusinessId
ORDER BY BusinessApplication.DueDate;

-- Check attachments
SELECT ApplicationAttachment.*, BusinessApplication.BusinessId
FROM [compliance].[ApplicationAttachment]
INNER JOIN [compliance].[BusinessApplication]
    ON ApplicationAttachment.BusinessApplicationId = BusinessApplication.Id
WHERE BusinessApplication.BusinessId = @BusinessId;

-- Check status transitions (look for SubmittedAtUtc/ApprovedAtUtc)
SELECT Id, Status, SubmittedAtUtc, ApprovedAtUtc
FROM [compliance].[BusinessApplication]
WHERE BusinessId = @BusinessId AND (SubmittedAtUtc IS NOT NULL OR ApprovedAtUtc IS NOT NULL);

-- Plan feature check
SELECT * FROM [dbo].[PlanFeature] WHERE ModuleKey = 'compliance';

-- Check custom filings (ApplicationType with IsActive=0 and Country='Custom')
SELECT BusinessApplication.Id, ApplicationType.Name, BusinessApplication.DueDate, BusinessApplication.Status
FROM [compliance].[BusinessApplication]
INNER JOIN [compliance].[ApplicationType]
    ON BusinessApplication.ApplicationTypeId = ApplicationType.Id
WHERE BusinessApplication.BusinessId = @BusinessId
  AND ApplicationType.Country = 'Custom'
ORDER BY BusinessApplication.DueDate;

-- Verify due day override was applied (check specific month days)
SELECT BusinessApplication.Id, ApplicationType.Name, BusinessApplication.DueDate, DAY(BusinessApplication.DueDate) AS DueDay
FROM [compliance].[BusinessApplication]
INNER JOIN [compliance].[ApplicationType]
    ON BusinessApplication.ApplicationTypeId = ApplicationType.Id
WHERE BusinessApplication.BusinessId = @BusinessId
ORDER BY BusinessApplication.DueDate;
```
