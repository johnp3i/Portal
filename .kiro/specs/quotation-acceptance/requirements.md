# Requirements Document

## Introduction

This feature adds a "Accept Proposal" capability to the shared proposal view (`/proposal/{token}`). When a customer receives a shared proposal link, they can formally accept the proposal and agree to proceed with the quoted work. The acceptance creates a verifiable audit trail recording that the customer received, reviewed, and agreed to the proposal terms. The business owner sees acceptance status on the quotation detail page and in the quotation list.

## Glossary

- **Proposal_Acceptance_System**: The subsystem responsible for capturing, persisting, and displaying proposal acceptance records from shared proposal recipients.
- **Shared_Proposal_Page**: The unauthenticated public page served at `/proposal/{token}` that renders a shared proposal HTML snapshot.
- **Acceptance_Record**: A database row in `[quotation].[ProposalAcceptance]` that stores the acceptance terms, timestamp, IP address, and user-agent of the accepting party.
- **Business_Owner**: An authenticated Portal user who owns the quotation and views acceptance status on the quotation detail page and quotation list.
- **Acceptance_Terms**: The fixed legal text presented to the customer: "I accept this proposal and agree to proceed with the quoted work."
- **ProposalShare**: The existing `[quotation].[ProposalShare]` table that tracks share tokens, links to quotations, and stores HTML snapshots.

## Requirements

### Requirement 1: Display Acceptance UI on Shared Proposal Page

**User Story:** As a customer viewing a shared proposal, I want to see an acceptance checkbox and button, so that I can formally acknowledge the proposal and agree to proceed.

#### Acceptance Criteria

1. WHEN a customer opens a Shared_Proposal_Page with an active and non-expired share, THE Proposal_Acceptance_System SHALL display a checkbox labelled with the Acceptance_Terms text and a disabled "Accept Proposal" button below the proposal content.
2. WHEN the customer ticks the Acceptance_Terms checkbox, THE Proposal_Acceptance_System SHALL enable the "Accept Proposal" button.
3. WHEN the customer unticks the Acceptance_Terms checkbox, THE Proposal_Acceptance_System SHALL disable the "Accept Proposal" button.
4. WHILE the proposal has already been accepted for the current ProposalShare, THE Proposal_Acceptance_System SHALL display a read-only confirmation message showing "Accepted on {date}" instead of the checkbox and button.

### Requirement 2: Record Proposal Acceptance

**User Story:** As a customer, I want my acceptance to be recorded with full audit details, so that both parties have a verifiable record of my agreement to proceed with the quoted work.

#### Acceptance Criteria

1. WHEN the customer clicks the enabled "Accept Proposal" button, THE Proposal_Acceptance_System SHALL persist an Acceptance_Record containing the ProposalShareId, acceptance terms text, UTC timestamp, client IP address, and user-agent string.
2. WHEN the Acceptance_Record is successfully persisted, THE Proposal_Acceptance_System SHALL display a SweetAlert2 success confirmation to the customer and replace the acceptance UI with the read-only "Accepted on {date}" message.
3. IF the Acceptance_Record fails to persist due to a server error, THEN THE Proposal_Acceptance_System SHALL display a SweetAlert2 error message and keep the acceptance UI in its current state so the customer can retry.
4. WHEN the customer submits the acceptance, THE Proposal_Acceptance_System SHALL block the UI using BlockUI until the server responds.

### Requirement 3: Prevent Duplicate Acceptance

**User Story:** As a system operator, I want to prevent the same proposal share from being accepted more than once, so that the audit trail remains unambiguous.

#### Acceptance Criteria

1. THE Proposal_Acceptance_System SHALL store at most one Acceptance_Record per ProposalShare.
2. IF a customer attempts to accept a ProposalShare that already has an Acceptance_Record, THEN THE Proposal_Acceptance_System SHALL return an informational message indicating the proposal has already been accepted and display the existing acceptance date.

### Requirement 4: Display Acceptance Status to Business Owner on Quotation Detail

**User Story:** As a business owner, I want to see whether a shared proposal has been accepted on the quotation detail page, so that I have visibility into customer acknowledgement.

#### Acceptance Criteria

1. WHILE an Acceptance_Record exists for the active ProposalShare of a quotation, THE Proposal_Acceptance_System SHALL display "Accepted on {date}" on the quotation detail page.
2. WHILE no Acceptance_Record exists for the active ProposalShare of a quotation, THE Proposal_Acceptance_System SHALL display "Awaiting acceptance" on the quotation detail page.
3. WHILE no active ProposalShare exists for a quotation, THE Proposal_Acceptance_System SHALL not display any acceptance status indicator on the quotation detail page.

### Requirement 5: Display Acceptance Status in Quotation List

**User Story:** As a business owner, I want to see proposal acceptance status in the quotation list, so that I can quickly identify which quotations have been acknowledged by customers.

#### Acceptance Criteria

1. WHILE an Acceptance_Record exists for the active ProposalShare of a quotation, THE Proposal_Acceptance_System SHALL display a "✓ Accepted" note below the quotation number in the quotation list.
2. WHILE no Acceptance_Record exists for the active ProposalShare of a quotation, THE Proposal_Acceptance_System SHALL display a "⏳ Awaiting acceptance" note below the quotation number in the quotation list.
3. WHILE no active ProposalShare exists for a quotation, THE Proposal_Acceptance_System SHALL not display any acceptance note in the quotation list for that quotation.

### Requirement 6: Acceptance Only on Active Shares

**User Story:** As a system operator, I want acceptance to be possible only on active, non-expired shares, so that expired or cancelled links cannot be used to create acceptance records.

#### Acceptance Criteria

1. WHEN a customer opens a Shared_Proposal_Page with an inactive or expired share, THE Proposal_Acceptance_System SHALL not display the acceptance UI.
2. IF a customer submits an acceptance request for a ProposalShare that is inactive or expired, THEN THE Proposal_Acceptance_System SHALL reject the request and return an error message indicating the share link is no longer valid.

### Requirement 7: Acceptance Audit Trail Integrity

**User Story:** As a business owner, I want the acceptance audit trail to be tamper-resistant and complete, so that it can serve as evidence that the customer agreed to proceed with the quoted work.

#### Acceptance Criteria

1. THE Proposal_Acceptance_System SHALL store the exact Acceptance_Terms text that was displayed to the customer at the time of acceptance.
2. THE Proposal_Acceptance_System SHALL store the client IP address captured from the HTTP request at the time of acceptance.
3. THE Proposal_Acceptance_System SHALL store the user-agent string captured from the HTTP request at the time of acceptance.
4. THE Proposal_Acceptance_System SHALL store the UTC timestamp of the acceptance recorded by the server clock.
5. THE Proposal_Acceptance_System SHALL make Acceptance_Record fields immutable after creation — no updates or deletions permitted through application logic.
