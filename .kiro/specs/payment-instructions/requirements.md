# Requirements Document

## Introduction

Payment Instructions provides a lightweight bank-transfer payment flow for shared invoices. Instead of integrating a full payment gateway (Stripe Connect), this feature lets customers view the business's bank details directly on the shared invoice page and declare when they have made a payment. The business retains full control over whether bank transfer instructions are exposed publicly.

This replaces the original Module 4 (Stripe Connect) for the MVP. Stripe Connect remains documented as a future upgrade path (Option C) for card payments and automatic reconciliation.

## Glossary

- **Shared_Invoice_Page**: The anonymous public page at `/invoice-view/{token}` that renders a read-only HTML snapshot of an invoice for the customer
- **Payment_Instructions_Modal**: A modal/overlay on the Shared Invoice Page displaying bank transfer details and a suggested payment reference
- **Business_Payment_Detail**: The existing `[portal].[BusinessPaymentDetail]` entity that stores a business's bank account records (IBAN, BankName, PayeeName)
- **Invoice_Financial_Status**: A classification of an invoice's payment state (Unpaid, PartiallyPaid, Paid, Overdue, WrittenOff, PaymentOnboard)
- **PaymentOnboard_Status**: A new financial status (Id=6) indicating the customer has declared payment was made, pending business verification
- **Payment_Instructions_Toggle**: A business-level setting controlling whether the "Pay by Bank Transfer" button appears on shared invoices
- **Transfer_Reference**: A suggested payment description for the customer to include in their bank transfer (format: `{InvoiceNumber} — {BusinessName}`)
- **Outstanding_Amount**: The remaining balance on an invoice, calculated as TotalAmount minus sum of confirmed payments
- **InvoiceViewController**: The ASP.NET Core MVC controller handling anonymous access to shared invoice pages
- **Business_Settings_Page**: The authenticated settings page where business owners configure their operational preferences

## Requirements

### Requirement 1: Payment Instructions Toggle

**User Story:** As a business owner, I want to control whether bank transfer details are shown on my shared invoices, so that I can choose when to expose payment instructions publicly.

#### Acceptance Criteria

1. THE Business_Settings_Page SHALL display a Payment_Instructions_Toggle labelled "Show bank transfer payment option on shared invoices"
2. WHEN a business owner enables the Payment_Instructions_Toggle, THE System SHALL persist the setting against the business record
3. WHEN a business owner disables the Payment_Instructions_Toggle, THE System SHALL hide the "Pay by Bank Transfer" button on all subsequently loaded shared invoices for that business
4. THE Payment_Instructions_Toggle SHALL default to disabled for all businesses
5. IF the business has no active Business_Payment_Detail records, THEN THE System SHALL disable the Payment_Instructions_Toggle and display an informational message stating "Add bank details in your payment details section before enabling this option"

### Requirement 2: Pay by Bank Transfer Button

**User Story:** As a customer viewing a shared invoice, I want to see a clear option to pay by bank transfer, so that I can initiate payment without contacting the business directly.

#### Acceptance Criteria

1. WHILE the Payment_Instructions_Toggle is enabled for the business AND the invoice financial status is Unpaid or PartiallyPaid or Overdue, THE Shared_Invoice_Page SHALL display a "Pay by Bank Transfer" button
2. WHILE the Payment_Instructions_Toggle is disabled for the business, THE Shared_Invoice_Page SHALL NOT display the "Pay by Bank Transfer" button
3. WHILE the invoice financial status is Paid or WrittenOff or PaymentOnboard, THE Shared_Invoice_Page SHALL NOT display the "Pay by Bank Transfer" button
4. WHEN the customer clicks the "Pay by Bank Transfer" button, THE System SHALL open the Payment_Instructions_Modal

### Requirement 3: Payment Instructions Modal Content

**User Story:** As a customer, I want to see all the information I need to make a bank transfer, so that I can complete the payment accurately without requesting details from the business.

#### Acceptance Criteria

1. THE Payment_Instructions_Modal SHALL display the following information: business name, bank name, IBAN, payee name, outstanding amount (formatted with the business currency symbol), invoice due date, and a suggested Transfer_Reference
2. THE Transfer_Reference SHALL follow the format `{InvoiceNumber} — {BusinessName}` (e.g., "INV-2026-0089 — Acme Solutions Ltd")
3. THE Payment_Instructions_Modal SHALL include a copy-to-clipboard button next to the IBAN field
4. THE Payment_Instructions_Modal SHALL include a copy-to-clipboard button next to the Transfer_Reference field
5. WHERE the business has multiple active Business_Payment_Detail records, THE Payment_Instructions_Modal SHALL display the bank detail record with the lowest SortOrder value
6. THE Outstanding_Amount SHALL be calculated as the invoice TotalAmount minus the sum of all confirmed payment amounts recorded against the invoice

### Requirement 4: Customer Payment Declaration

**User Story:** As a customer, I want to notify the business that I have made a bank transfer, so that they know to expect and verify the payment.

#### Acceptance Criteria

1. THE Payment_Instructions_Modal SHALL display an "I've made the payment" button below the bank transfer details
2. WHEN the customer clicks the "I've made the payment" button, THE System SHALL update the invoice financial status to PaymentOnboard (Id=6)
3. WHEN the customer clicks the "I've made the payment" button, THE System SHALL record the declaration timestamp in UTC
4. WHEN the payment declaration succeeds, THE Payment_Instructions_Modal SHALL close and the Shared_Invoice_Page SHALL display a confirmation message stating "Thank you. The business has been notified of your payment."
5. WHILE the invoice financial status is PaymentOnboard, THE Shared_Invoice_Page SHALL display a status badge reading "Payment Onboard — Awaiting Verification"
6. IF the payment declaration request fails, THEN THE System SHALL display an error message and allow the customer to retry

### Requirement 5: PaymentOnboard Financial Status

**User Story:** As a business owner, I want to see which invoices have a pending customer payment declaration, so that I can prioritise bank statement verification.

#### Acceptance Criteria

1. THE System SHALL support a new Invoice_Financial_Status value: PaymentOnboard (Id=6, Name="PaymentOnboard")
2. WHEN an invoice has financial status PaymentOnboard, THE Invoice Detail page SHALL display an informational note: "The customer has declared that payment was made via bank transfer. This is a customer declaration only — please verify receipt on your bank statement before marking as paid."
3. WHILE an invoice has financial status PaymentOnboard, THE System SHALL continue to allow the business owner to record payments or change the financial status manually
4. THE PaymentOnboard status SHALL appear in invoice lists and filters alongside existing financial statuses
5. WHEN the business records a payment that brings the total payments to equal or exceed the invoice TotalAmount, THE System SHALL update the financial status to Paid regardless of the previous PaymentOnboard status

### Requirement 6: SWIFT/BIC Field Extension

**User Story:** As a business owner, I want to store my bank's SWIFT/BIC code alongside my IBAN, so that international customers have all details required for cross-border transfers.

#### Acceptance Criteria

1. THE Business_Payment_Detail entity SHALL include an optional SwiftBic field (maximum 11 characters)
2. THE Business_Settings_Page payment details section SHALL display an input field for SWIFT/BIC when adding or editing a bank account record
3. WHERE the SwiftBic field has a value, THE Payment_Instructions_Modal SHALL display the SWIFT/BIC code alongside the IBAN
4. WHERE the SwiftBic field is empty, THE Payment_Instructions_Modal SHALL omit the SWIFT/BIC row

### Requirement 7: Audit and Security

**User Story:** As a business owner, I want payment declarations to be auditable and secure, so that I can track who declared payment and when.

#### Acceptance Criteria

1. WHEN a customer declares payment via the Payment_Instructions_Modal, THE System SHALL create an audit log entry containing: invoice ID, share token used, declaration timestamp (UTC), and the customer's IP address
2. THE payment declaration endpoint SHALL validate that the share token is active, not expired, and matches an existing invoice
3. IF the share token is inactive or expired, THEN THE System SHALL reject the payment declaration with an appropriate error message
4. THE payment declaration endpoint SHALL be rate-limited to prevent abuse (maximum 3 declarations per share token per hour)
5. WHEN a payment declaration is made, THE System SHALL NOT create an actual Payment record — only the financial status changes to PaymentOnboard

### Requirement 8: Phase 1 Timetable Update (Module 4 Replacement)

**User Story:** As the development team, I want a simplified Module 4 scope that removes Stripe dependencies, so that the MVP can ship without external payment gateway integration.

#### Acceptance Criteria

1. THE Phase 1 timetable SHALL replace the existing Module 4 (Stripe Connect) tasks with Payment Instructions tasks
2. THE updated Module 4 SHALL NOT require Stripe API keys, OAuth flows, webhook endpoints, or external payment gateway accounts
3. THE updated Module 4 SHALL document Option C (Stripe Connect) as a future upgrade path in a dedicated section of the timetable or linked design document

### Requirement 9: Future Upgrade Path Documentation

**User Story:** As the product team, I want the Stripe Connect upgrade path documented, so that future development can add card payments and automatic reconciliation without redesigning the payment flow.

#### Acceptance Criteria

1. THE design documentation SHALL include a section titled "Option C: Stripe Connect (Future)" describing: card payment support, automatic payment reconciliation via webhooks, and the OAuth Connect flow for business onboarding
2. THE documentation SHALL specify that Stripe Connect would supplement (not replace) bank transfer instructions — both payment methods would coexist
3. THE documentation SHALL list the database tables from the original Module 4 design (BusinessPaymentGateway, InvoicePaymentLink) as required for the Stripe upgrade
