# Requirements Document

## Introduction

This feature adds an "Accept Invoice" capability to the shared invoice view (`/invoice-view/{token}`). When a customer receives a shared invoice link, they can formally accept and acknowledge the invoice through the platform. The acceptance creates a verifiable audit trail recording that the customer received, reviewed, and agreed to pay by the stated due date. The business owner sees acceptance status on the invoice detail page.

## Glossary

- **Invoice_Acceptance_System**: The subsystem responsible for capturing, persisting, and displaying invoice acceptance records from shared invoice recipients.
- **Shared_Invoice_Page**: The unauthenticated public page served at `/invoice-view/{token}` that renders a shared invoice HTML snapshot.
- **Acceptance_Record**: A database row in `[invoice].[InvoiceAcceptance]` that stores the acceptance terms, timestamp, IP address, and user-agent of the accepting party.
- **Business_Owner**: An authenticated Portal user who owns the invoice and views acceptance status on the invoice detail page (`/Invoice/Detail/{id}`).
- **Acceptance_Terms**: The fixed legal text presented to the customer: "I accept this invoice as correct and agree to pay by the due date."
- **InvoiceShare**: The existing `[invoice].[InvoiceShare]` table that tracks share tokens, links to invoices, and stores HTML snapshots.

## Requirements

### Requirement 1: Display Acceptance UI on Shared Invoice Page

**User Story:** As a customer viewing a shared invoice, I want to see an acceptance checkbox and button, so that I can formally acknowledge the invoice.

#### Acceptance Criteria

1. WHEN a customer opens a Shared_Invoice_Page with an active and non-expired share, THE Invoice_Acceptance_System SHALL display a checkbox labelled with the Acceptance_Terms text and a disabled "Accept Invoice" button below the invoice content.
2. WHEN the customer ticks the Acceptance_Terms checkbox, THE Invoice_Acceptance_System SHALL enable the "Accept Invoice" button.
3. WHEN the customer unticks the Acceptance_Terms checkbox, THE Invoice_Acceptance_System SHALL disable the "Accept Invoice" button.
4. WHILE the invoice has already been accepted for the current InvoiceShare, THE Invoice_Acceptance_System SHALL display a read-only confirmation message showing "Accepted on {date}" instead of the checkbox and button.

### Requirement 2: Record Invoice Acceptance

**User Story:** As a customer, I want my acceptance to be recorded with full audit details, so that both parties have a verifiable record of my agreement.

#### Acceptance Criteria

1. WHEN the customer clicks the enabled "Accept Invoice" button, THE Invoice_Acceptance_System SHALL persist an Acceptance_Record containing the InvoiceShareId, acceptance terms text, UTC timestamp, client IP address, and user-agent string.
2. WHEN the Acceptance_Record is successfully persisted, THE Invoice_Acceptance_System SHALL display a SweetAlert2 success confirmation to the customer and replace the acceptance UI with the read-only "Accepted on {date}" message.
3. IF the Acceptance_Record fails to persist due to a server error, THEN THE Invoice_Acceptance_System SHALL display a SweetAlert2 error message and keep the acceptance UI in its current state so the customer can retry.
4. WHEN the customer submits the acceptance, THE Invoice_Acceptance_System SHALL block the UI using BlockUI until the server responds.

### Requirement 3: Prevent Duplicate Acceptance

**User Story:** As a system operator, I want to prevent the same invoice share from being accepted more than once, so that the audit trail remains unambiguous.

#### Acceptance Criteria

1. THE Invoice_Acceptance_System SHALL store at most one Acceptance_Record per InvoiceShare.
2. IF a customer attempts to accept an InvoiceShare that already has an Acceptance_Record, THEN THE Invoice_Acceptance_System SHALL return an informational message indicating the invoice has already been accepted and display the existing acceptance date.

### Requirement 4: Display Acceptance Status to Business Owner

**User Story:** As a business owner, I want to see whether a shared invoice has been accepted, so that I have visibility into customer acknowledgement.

#### Acceptance Criteria

1. WHILE an Acceptance_Record exists for the active InvoiceShare of an invoice, THE Invoice_Acceptance_System SHALL display "Accepted on {date}" on the invoice detail page (`/Invoice/Detail/{id}`).
2. WHILE no Acceptance_Record exists for the active InvoiceShare of an invoice, THE Invoice_Acceptance_System SHALL display "Awaiting acceptance" on the invoice detail page.
3. WHILE no active InvoiceShare exists for an invoice, THE Invoice_Acceptance_System SHALL not display any acceptance status indicator on the invoice detail page.

### Requirement 5: Acceptance Only on Active Shares

**User Story:** As a system operator, I want acceptance to be possible only on active, non-expired shares, so that expired or cancelled links cannot be used to create acceptance records.

#### Acceptance Criteria

1. WHEN a customer opens a Shared_Invoice_Page with an inactive or expired share, THE Invoice_Acceptance_System SHALL not display the acceptance UI.
2. IF a customer submits an acceptance request for an InvoiceShare that is inactive or expired, THEN THE Invoice_Acceptance_System SHALL reject the request and return an error message indicating the share link is no longer valid.

### Requirement 6: Acceptance Audit Trail Integrity

**User Story:** As a business owner, I want the acceptance audit trail to be tamper-resistant and complete, so that it can serve as evidence that the customer agreed to pay.

#### Acceptance Criteria

1. THE Invoice_Acceptance_System SHALL store the exact Acceptance_Terms text that was displayed to the customer at the time of acceptance.
2. THE Invoice_Acceptance_System SHALL store the client IP address captured from the HTTP request at the time of acceptance.
3. THE Invoice_Acceptance_System SHALL store the user-agent string captured from the HTTP request at the time of acceptance.
4. THE Invoice_Acceptance_System SHALL store the UTC timestamp of the acceptance recorded by the server clock.
5. THE Invoice_Acceptance_System SHALL make Acceptance_Record fields immutable after creation — no updates or deletions permitted through application logic.
