# Requirements Document

## Introduction

The Dashboard Onboarding feature guides new businesses through initial platform setup by displaying a prominent onboarding panel on the Home Dashboard. The panel shows a checklist of setup steps with a progress indicator, links to relevant pages, and a celebration state upon completion. Dismissal is persisted per-business in the database.

## Glossary

- **Onboarding_Panel**: A glass-styled card displayed above the KPI gauges on the Dashboard that shows the onboarding checklist and progress
- **Onboarding_Step**: A single item in the checklist representing one setup action (e.g., upload a logo, create a customer)
- **Progress_Indicator**: A textual display showing "N of 6 completed" to communicate checklist progress
- **Celebration_State**: A visual state displayed when all 6 steps are complete, replacing the checklist with a success message
- **Dismissal_Flag**: A boolean column `IsOnboardingDismissed` on the `[portal].[Business]` table indicating whether the onboarding panel has been dismissed for that business
- **Business_Profile**: The `[portal].[BusinessProfile]` entity containing company registration, VAT, and address fields
- **Business_Logo**: The `[portal].[BusinessLogo]` entity representing an uploaded logo for a business
- **Payment_Detail**: The `[portal].[BusinessPaymentDetail]` entity storing bank account information
- **Invoice_Issued_Status**: Invoice with `InvoiceStatusTypeId = 2` (Issued)

## Requirements

### Requirement 1

**User Story:** As a new business owner, I want to see a guided checklist on my dashboard so that I know what setup steps remain before my account is fully operational.

#### Acceptance Criteria

1.1. WHEN a user navigates to the Dashboard AND the Dismissal_Flag for the current business is false AND fewer than 6 Onboarding_Steps are complete, THE Onboarding_Panel SHALL render above the KPI gauges section.

1.2. THE Onboarding_Panel SHALL display a Progress_Indicator showing the count of completed steps out of 6 total steps.

1.3. THE Onboarding_Panel SHALL display 6 Onboarding_Steps in fixed order:
  - Business profile completed (Business_Profile has Name, AddressLine1, and VatRegistrationNumber populated)
  - Logo uploaded (at least one Business_Logo exists for the business)
  - Payment details added (at least one active Payment_Detail exists for the business)
  - First customer created (at least one Customer exists for the business)
  - First quotation or invoice created (at least one Quotation OR at least one Invoice exists for the business)
  - First invoice issued (at least one Invoice with Invoice_Issued_Status exists for the business)

1.4. WHEN an Onboarding_Step is complete, THE Onboarding_Panel SHALL display a green checkmark icon and apply a strikethrough style to the step label.

1.5. WHEN an Onboarding_Step is incomplete, THE Onboarding_Panel SHALL display an empty circle icon and render the step label with normal font weight.

1.6. WHEN a user clicks an Onboarding_Step, THE Onboarding_Panel SHALL navigate to the relevant page for that step:
  - Business profile → `/MyBusiness`
  - Logo uploaded → `/MyBusiness` (Logos tab)
  - Payment details → `/MyBusiness` (Payment tab)
  - First customer → `/Customer`
  - First quotation or invoice → `/Quotation/Create`
  - First invoice issued → `/Invoice`

### Requirement 2

**User Story:** As a business owner who has completed all setup, I want to see a celebration message so that I feel acknowledged for finishing onboarding.

#### Acceptance Criteria

2.1. WHEN all 6 Onboarding_Steps are complete AND the Dismissal_Flag is false, THE Onboarding_Panel SHALL display the Celebration_State with a success icon and a congratulatory message.

2.2. WHEN the Celebration_State is displayed, THE Onboarding_Panel SHALL show a button labelled "Got it" that sets the Dismissal_Flag to true.

### Requirement 3

**User Story:** As a business owner, I want to dismiss the onboarding panel so that it no longer occupies dashboard space once I am familiar with the platform.

#### Acceptance Criteria

3.1. THE Onboarding_Panel SHALL display a "Dismiss" button while the checklist is visible.

3.2. WHEN a user clicks "Dismiss", THE Onboarding_Panel SHALL call a server endpoint that sets the Dismissal_Flag to true for the current business.

3.3. WHEN the Dismissal_Flag is true for the current business, THE Onboarding_Panel SHALL not render on the Dashboard.

3.4. IF the server call to set the Dismissal_Flag fails, THEN THE Onboarding_Panel SHALL display a SweetAlert2 error notification and remain visible.

### Requirement 4

**User Story:** As a platform maintainer, I want onboarding completion state computed dynamically from existing data so that no separate tracking table is required for step completion.

#### Acceptance Criteria

4.1. THE Dashboard Controller SHALL compute each Onboarding_Step's completion state by querying existing entities (BusinessProfile, BusinessLogo, BusinessPaymentDetail, Customer, Quotation, Invoice) filtered to the current business.

4.2. THE Dashboard Controller SHALL pass the 6 boolean completion flags and the Dismissal_Flag to the view via a structured model or ViewBag.

4.3. WHEN the Dismissal_Flag is true, THE Dashboard Controller SHALL skip onboarding-related queries to avoid unnecessary database load.
