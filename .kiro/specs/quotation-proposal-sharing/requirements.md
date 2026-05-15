# Requirements Document

## Introduction

This feature enables business users to generate branded, rich HTML proposals from existing quotations and share them with customers via secure, expiring links. The proposal is a point-in-time snapshot of the quotation data rendered in the MyChair design system. Customers can view the proposal without authentication and download it as a PDF. The business user controls the link expiration date and triggers email delivery to the customer.

## Glossary

- **Proposal_Snapshot**: A frozen, self-contained HTML representation of a Quotation at the time of sharing, including all line items, sections, business profile data, and customer details.
- **Share_Token**: A cryptographically secure, unguessable string used to construct the public URL for accessing a Proposal_Snapshot.
- **Proposal_Share**: The database record linking a Quotation to its Proposal_Snapshot, Share_Token, expiration date, and sharing metadata.
- **Proposal_Section**: A logical grouping of quotation lines within a proposal (e.g., "Equipment", "Installation", "Subscription"), each with its own heading and optional column configuration.
- **Public_Proposal_View**: The unauthenticated endpoint that renders a Proposal_Snapshot when accessed via a valid, non-expired Share_Token.
- **Proposal_Renderer**: The service responsible for generating the branded HTML Proposal_Snapshot from quotation data.
- **Email_Notification_Service**: The component responsible for sending the branded proposal email to the customer.
- **Portal**: The ASP.NET Core MVC web application.
- **Business_User**: An authenticated user with quotation module permissions for the relevant business.

## Requirements

### Requirement 1: Proposal Snapshot Generation

**User Story:** As a Business_User, I want to generate a branded HTML proposal from a quotation, so that I can share a professional document with my customer.

#### Acceptance Criteria

1. WHEN a Business_User triggers proposal sharing for a Quotation, THE Proposal_Renderer SHALL generate a self-contained HTML Proposal_Snapshot using inline styles and no external resource dependencies.
2. THE Proposal_Snapshot SHALL include the business profile data (company name, address, contact details, registration numbers) from the BusinessProfile table at the time of generation.
3. THE Proposal_Snapshot SHALL include the customer data (name, contact person, email, address) from the Customer table at the time of generation.
4. THE Proposal_Snapshot SHALL include all quotation header data (Reference, ValidUntil, Subtotal, TaxAmount, TotalAmount, Notes) at the time of generation.
5. THE Proposal_Snapshot SHALL include all QuotationLine data (Description, Quantity, UnitPrice, VatRate, LineTotal, SortOrder, ReferenceUrl) grouped by Proposal_Section at the time of generation.
6. WHEN a QuotationLine has a non-null ReferenceUrl, THE Proposal_Renderer SHALL render the line item description as a hyperlink pointing to that URL (opening in a new tab).
6. THE Proposal_Snapshot SHALL render using the MyChair design system (Manrope headings, Inter body, primary blue #0D5EA6, accent cyan #57B8E8, card-based layout with rounded corners and soft shadows).
7. THE Proposal_Snapshot SHALL remain unchanged if the source Quotation is subsequently edited.
8. THE Proposal_Snapshot SHALL be responsive and render correctly on desktop and mobile viewports.
9. THE Proposal_Snapshot SHALL include a print-optimized CSS layout using @page and @media print rules.

### Requirement 2: Proposal Section Grouping

**User Story:** As a Business_User, I want to organize quotation lines into named sections with configurable column headers, so that the proposal matches the structure of my commercial offering (e.g., equipment, subscriptions, setup services).

#### Acceptance Criteria

1. THE Portal SHALL allow QuotationLines to be assigned to a Proposal_Section with a display name and sort order.
2. WHEN a Proposal_Section contains lines with subscription pricing, THE Proposal_Renderer SHALL display columns appropriate to subscription items (e.g., Monthly Price, Daily Cost, Annual Price).
3. WHEN a Proposal_Section contains lines with one-time pricing, THE Proposal_Renderer SHALL display columns appropriate to one-time items (e.g., Qty, Unit Price, Final Price).
4. THE Proposal_Renderer SHALL render each Proposal_Section as a distinct visual card with its own heading and table.
5. WHERE a Quotation has no explicit Proposal_Section assignments, THE Proposal_Renderer SHALL render all lines in a single default section.

### Requirement 3: Shareable Link with Expiration

**User Story:** As a Business_User, I want to generate a unique, expiring link for the proposal, so that my customer can view it without logging in and the link becomes invalid after a set period.

#### Acceptance Criteria

1. WHEN a Business_User shares a proposal, THE Portal SHALL generate a Share_Token of at least 32 bytes using a cryptographically secure random number generator.
2. THE Portal SHALL construct a public URL containing the Share_Token that resolves to the Public_Proposal_View endpoint.
3. THE Portal SHALL set a default expiration date of 3 calendar days from the current date.
4. THE Portal SHALL allow the Business_User to select a custom expiration date that is at least 1 day in the future.
5. THE Proposal_Share record SHALL store the Share_Token, expiration date (as DateTimeOffset in UTC), Quotation reference, and the generated Proposal_Snapshot HTML.
6. THE Share_Token SHALL be unique across all Proposal_Share records.

### Requirement 4: Public Proposal View

**User Story:** As a customer, I want to view the proposal by clicking the link I received, so that I can review the commercial offer without needing an account.

#### Acceptance Criteria

1. WHEN a valid, non-expired Share_Token is provided in the URL, THE Public_Proposal_View SHALL render the stored Proposal_Snapshot HTML without requiring authentication.
2. WHEN an expired Share_Token is provided in the URL, THE Public_Proposal_View SHALL display a branded "This proposal link has expired" message with the business contact information.
3. WHEN an invalid or non-existent Share_Token is provided in the URL, THE Public_Proposal_View SHALL return an HTTP 404 response.
4. THE Public_Proposal_View SHALL not expose any internal system identifiers (Quotation ID, Customer ID, Business ID) in the URL or rendered HTML source.
5. THE Public_Proposal_View SHALL include appropriate cache-control headers to prevent caching of the proposal content by intermediaries.

### Requirement 5: Proposal Download

**User Story:** As a customer, I want to download the proposal as a PDF, so that I can save it locally or print it for review.

#### Acceptance Criteria

1. THE Public_Proposal_View SHALL include a visible "Download PDF" button that triggers the browser print dialog with print-optimized styling applied.
2. THE Proposal_Snapshot print layout SHALL render as a clean A4 document without browser chrome, navigation elements, or the download button itself.
3. THE Proposal_Snapshot print layout SHALL preserve all section cards, tables, totals, and signature areas in a print-friendly format.

### Requirement 6: Email Notification

**User Story:** As a Business_User, I want to send the proposal link to my customer via email, so that they receive a professional notification with easy access to the proposal.

#### Acceptance Criteria

1. WHEN a Business_User shares a proposal, THE Email_Notification_Service SHALL send a branded HTML email to the customer email address on file.
2. THE email SHALL include the proposal link, the quotation reference, the business name, and the expiration date.
3. THE email SHALL use the MyChair design system styling (inline CSS, consistent with the proposal visual identity).
4. IF the Customer record has no email address, THEN THE Portal SHALL prevent the share action and display a validation message indicating that a customer email is required.
5. THE email SHALL be sent via the existing IEmailSender service using the Sales department designation.

### Requirement 7: Access Control

**User Story:** As a platform administrator, I want only authorized users to share proposals, so that proposal sharing is restricted to users with quotation permissions.

#### Acceptance Criteria

1. THE Portal SHALL restrict the proposal sharing action to authenticated users with quotation module access for the relevant business.
2. WHEN an unauthorized user attempts to access the share functionality, THE Portal SHALL return an HTTP 403 response.
3. THE Portal SHALL validate that the Quotation belongs to the same business as the authenticated user before allowing sharing.

### Requirement 8: Proposal Share Tracking

**User Story:** As a Business_User, I want to see when a proposal was shared and its link status, so that I can track which proposals have been sent and whether links are still active.

#### Acceptance Criteria

1. THE Portal SHALL record the sharing timestamp (UTC), the sharing user, and the customer email address for each Proposal_Share.
2. THE Portal SHALL display the share status (active link, expired link) on the quotation detail view.
3. WHEN a proposal has been previously shared, THE Portal SHALL allow the Business_User to reshare (generating a new Share_Token and expiration) or copy the existing link if still active.

### Requirement 9: Quotation Line Reference URL

**User Story:** As a Business_User, I want to attach a reference link to each quotation line item, so that the proposal can link out to product spec sheets, software documentation, or device information pages.

#### Acceptance Criteria

1. THE QuotationLine entity SHALL support an optional ReferenceUrl field (nullable, max 2048 characters) for storing a URL to external product or service documentation.
2. WHEN creating or editing a quotation line, THE Portal SHALL allow the Business_User to optionally provide a ReferenceUrl.
3. THE Portal SHALL validate that the ReferenceUrl, when provided, is a well-formed absolute URL (http or https scheme).
4. WHEN the Proposal_Snapshot is rendered and a line has a ReferenceUrl, THE line item title SHALL be rendered as a hyperlink (styled in the primary blue color, opening in a new browser tab).

### Requirement 10: Business Logo Library

**User Story:** As a Business_User, I want to upload and manage a library of logos in my business profile, so that I can use them across proposals without re-uploading each time.

#### Acceptance Criteria

1. THE Portal SHALL allow a Business_User to upload up to 20 logo images to the BusinessProfile logo library.
2. THE Portal SHALL accept logo uploads in PNG, JPG, SVG, or WebP format with a maximum file size of 2MB per image.
3. THE Portal SHALL store each uploaded logo with a display name and a publicly accessible URL.
4. THE Portal SHALL allow the Business_User to delete logos from the library.
5. THE Portal SHALL serve uploaded logos via a public URL that does not require authentication (so they render in shared proposals).

### Requirement 11: Proposal Logo Selection

**User Story:** As a Business_User, I want to select which logos appear on a proposal and where they are positioned, so that the proposal reflects the correct branding for the products or services being offered.

#### Acceptance Criteria

1. WHEN creating or sharing a proposal, THE Portal SHALL allow the Business_User to select one or more logos from the logo library to display in the hero section (displayed side by side as brand logos).
2. WHEN creating or sharing a proposal, THE Portal SHALL allow the Business_User to select one logo from the library to display in the metadata card area (small logo next to the "Prepared By" business name).
3. THE Proposal_Renderer SHALL render hero logos at a maximum height of 68px, maintaining aspect ratio.
4. THE Proposal_Renderer SHALL render the metadata card logo at a maximum height of 40px, maintaining aspect ratio.
5. IF no logos are selected for the proposal, THE Proposal_Renderer SHALL omit the logo areas without breaking the layout.
