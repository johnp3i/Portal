# Requirements Document

## Introduction

This feature adds a context-aware contact modal to the 3 Inventors Portal landing page. The modal captures visitor inquiries with contextual metadata (inquiry type, industry, pricing plan) and integrates with a new ContactUs controller action on the LandingController. The system includes Google reCAPTCHA v3 for bot protection, a honeypot field as a secondary defence layer, and branded confirmation emails sent to visitors after successful submission. A notification email is also sent to the 3 Inventors team for every inquiry.

## Glossary

- **Landing_Page**: The public-facing marketing page at the root URL (`/`) served by the LandingController, containing hero, features, pricing, and footer sections
- **Contact_Modal**: A fixed overlay dialog containing the inquiry form, triggered by CTA buttons on the Landing_Page
- **CTA_Button**: A call-to-action button on the Landing_Page that opens the Contact_Modal with contextual metadata
- **Inquiry_Type**: A string value identifying the context of the visitor's request (e.g., "Demo Request", "Pricing - Core", "General Inquiry")
- **ReCAPTCHA_Service**: Google reCAPTCHA v3 invisible bot protection that scores each request from 0.0 (likely bot) to 1.0 (likely human)
- **Score_Threshold**: A configurable minimum reCAPTCHA score (default 0.5) below which submissions are rejected
- **Honeypot_Field**: A hidden form field invisible to real users but filled by bots, used as a secondary spam detection layer
- **Confirmation_Email**: A branded HTML email sent to the visitor after successful form submission acknowledging their inquiry
- **Notification_Email**: An internal email sent to the 3 Inventors team (ask@3inventors.com) containing the visitor's inquiry details
- **Email_Builder**: A static class responsible for constructing the HTML body and subject line for confirmation emails
- **Landing_Controller**: The ASP.NET Core MVC controller (AllowAnonymous) that serves the landing page and handles the ContactUs form submission
- **Anti_Forgery_Token**: An ASP.NET Core request verification token that protects POST endpoints against cross-site request forgery attacks

## Requirements

### Requirement 1: Context-Aware Modal Display

**User Story:** As a visitor on the Portal landing page, I want the contact modal to display contextual information based on the button I clicked, so that I know my inquiry is being routed appropriately.

#### Acceptance Criteria

1. WHEN a CTA_Button with inquiry type "Demo Request" is clicked, THE Contact_Modal SHALL display the badge text "Demo Request", the title "Request a Demo", and the subtitle "See the platform in action. We'll walk you through it."
2. WHEN a CTA_Button with inquiry type "Pricing - Core" is clicked, THE Contact_Modal SHALL display the badge text "Core Plan", the title "Interested in the Core plan?", and the subtitle "Tell us about your team and we'll help you get started."
3. WHEN a CTA_Button with inquiry type "Pricing - Enhanced" is clicked, THE Contact_Modal SHALL display the badge text "Enhanced Plan", the title "Interested in the Enhanced plan?", and the subtitle "Tell us about your team and we'll help you get started."
4. WHEN a CTA_Button with inquiry type "Pricing - Enterprise" is clicked, THE Contact_Modal SHALL display the badge text "Enterprise Plan", the title "Let's tailor a plan for you", and the subtitle "Tell us about your organisation and we'll build a custom proposal."
5. WHEN a CTA_Button with inquiry type "General Inquiry" is clicked, THE Contact_Modal SHALL display the badge text "Contact Us", the title "Talk to Us", and the subtitle "Have a question? We'll get back to you within 24 hours."
6. THE Contact_Modal SHALL store the Inquiry_Type value in a hidden form field named "inquiryType"

### Requirement 2: Modal Form Fields

**User Story:** As a visitor, I want to provide my contact details through a structured form, so that the 3 Inventors team can reach me with relevant information.

#### Acceptance Criteria

1. THE Contact_Modal SHALL contain the following form fields: Company Name (text, optional), First Name (text, required), Last Name (text, optional), Email (email, required), Telephone (tel, optional), and Industry (dropdown, optional)
2. THE Contact_Modal SHALL include an Industry dropdown with the following options: empty default "Select your industry (optional)", "HORECA (Hotels, Restaurants, Cafés)", "Retail", "Warehouse / Logistics", "Manufacturing", "Services", "Office", "Gym / Fitness", "Other"
3. THE Contact_Modal SHALL include an Anti_Forgery_Token hidden field for CSRF protection
4. THE Contact_Modal SHALL include a Honeypot_Field positioned off-screen with aria-hidden="true" and tabindex="-1"
5. WHEN the visitor submits the form without providing a First Name, THE Contact_Modal SHALL prevent submission and indicate the field is required
6. WHEN the visitor submits the form without providing a valid Email address, THE Contact_Modal SHALL prevent submission and indicate the field is required

### Requirement 3: Modal Interaction Behaviour

**User Story:** As a visitor, I want to open and close the contact modal easily, so that I can inquire without disrupting my browsing experience.

#### Acceptance Criteria

1. WHEN a CTA_Button is clicked, THE Contact_Modal SHALL appear as a fixed overlay centred on the viewport with a semi-transparent backdrop
2. WHEN the close button (X) is clicked, THE Contact_Modal SHALL close and hide the overlay
3. WHEN the backdrop area outside the modal card is clicked, THE Contact_Modal SHALL close and hide the overlay
4. WHEN the Escape key is pressed, THE Contact_Modal SHALL close and hide the overlay
5. WHILE the Contact_Modal is open, THE Landing_Page SHALL prevent scrolling of the background content

### Requirement 4: Form Submission and Feedback

**User Story:** As a visitor, I want clear feedback when I submit the contact form, so that I know whether my inquiry was sent successfully.

#### Acceptance Criteria

1. WHEN the form is submitted, THE Contact_Modal SHALL disable the submit button and display the text "Sending..."
2. WHEN the server returns a success response, THE Contact_Modal SHALL close, reset all form fields, and display a success toast notification with the message "Your request has been sent. We will get in touch within 24 hours."
3. WHEN the server returns an error response, THE Contact_Modal SHALL re-enable the submit button with the text "Something went wrong — try again" for 3 seconds, then revert to "Send Request"
4. WHEN a network error occurs during submission, THE Contact_Modal SHALL re-enable the submit button with the text "Connection error — try again" for 3 seconds, then revert to "Send Request"
5. THE Contact_Modal SHALL submit the form data via a fetch POST request to the Landing_Controller ContactUs action with the Anti_Forgery_Token in the request header
6. WHEN the success toast is displayed, THE Landing_Page SHALL automatically hide the toast after 6 seconds

### Requirement 5: Google reCAPTCHA v3 Client-Side Integration

**User Story:** As the system owner, I want invisible bot protection on the contact form, so that spam submissions are blocked without inconveniencing legitimate visitors.

#### Acceptance Criteria

1. THE Landing_Page SHALL load the Google reCAPTCHA v3 script with the configured site key from appsettings.json
2. WHEN the contact form is submitted, THE Contact_Modal SHALL request a reCAPTCHA token using the action "contact_form" before sending the form data to the server
3. IF the reCAPTCHA token request fails, THEN THE Contact_Modal SHALL prevent form submission and display the text "Verification failed — try again" on the submit button for 3 seconds
4. THE Contact_Modal SHALL append the reCAPTCHA token to the form data as a field named "recaptchaToken"
5. THE Landing_Page SHALL hide the reCAPTCHA badge via CSS and display the required Google attribution text below the submit button

### Requirement 6: Server-Side reCAPTCHA Verification

**User Story:** As the system owner, I want the server to verify reCAPTCHA tokens and reject low-scoring submissions, so that bot traffic is filtered before processing.

#### Acceptance Criteria

1. WHEN a ContactUs request is received, THE Landing_Controller SHALL verify the reCAPTCHA token by posting it to Google's siteverify API at "https://www.google.com/recaptcha/api/siteverify" with the configured secret key
2. IF the reCAPTCHA verification returns a score below the configured Score_Threshold, THEN THE Landing_Controller SHALL return a 400 Bad Request response with the message "reCAPTCHA verification failed"
3. IF the reCAPTCHA verification returns success as false, THEN THE Landing_Controller SHALL return a 400 Bad Request response
4. THE Landing_Controller SHALL read the ReCaptcha:SiteKey, ReCaptcha:SecretKey, and ReCaptcha:ScoreThreshold values from appsettings.json configuration
5. IF the ReCaptcha:SecretKey configuration is empty or not set, THEN THE Landing_Controller SHALL skip reCAPTCHA verification and process the form normally
6. THE Landing_Controller SHALL log reCAPTCHA verification failures including the score, success status, and action for monitoring

### Requirement 7: Honeypot Spam Detection

**User Story:** As the system owner, I want a secondary spam detection layer using a honeypot field, so that simple bots that fill all form fields are silently rejected.

#### Acceptance Criteria

1. WHEN a ContactUs request is received with the Honeypot_Field ("website") containing a non-empty value, THE Landing_Controller SHALL log a warning indicating a likely bot submission
2. WHEN a ContactUs request is received with the Honeypot_Field containing a non-empty value, THE Landing_Controller SHALL return a 200 OK response without processing the form or sending emails
3. THE Landing_Controller SHALL check the Honeypot_Field before performing reCAPTCHA verification

### Requirement 8: ContactUs Controller Action

**User Story:** As the system owner, I want a dedicated controller action to receive and process contact form submissions, so that inquiries are handled securely and reliably.

#### Acceptance Criteria

1. THE Landing_Controller SHALL expose an HttpPost action at the route handled by the ContactUs action, decorated with ValidateAntiForgeryToken and AllowAnonymous attributes
2. WHEN a valid ContactUs request passes all verification checks, THE Landing_Controller SHALL send a Notification_Email to ask@3inventors.com containing all submitted form fields (inquiry type, company name, first name, last name, email, telephone, industry)
3. WHEN a valid ContactUs request passes all verification checks, THE Landing_Controller SHALL send a Confirmation_Email to the visitor's email address
4. THE Landing_Controller SHALL use the existing IEmailSender service with the Ask department for sending the Notification_Email
5. IF an exception occurs during email sending, THEN THE Landing_Controller SHALL log the error and return a 400 Bad Request response
6. WHEN the ContactUs action completes successfully, THE Landing_Controller SHALL return a 200 OK response

### Requirement 9: Notification Email to Team

**User Story:** As a 3 Inventors team member, I want to receive a structured notification email for each inquiry, so that I can review and respond to visitor requests efficiently.

#### Acceptance Criteria

1. THE Notification_Email SHALL use the subject format "3 Inventors Portal — {Inquiry_Type}" where Inquiry_Type is the value submitted in the form
2. IF the Inquiry_Type is empty or not provided, THEN THE Notification_Email SHALL use the subject "3 Inventors Portal — Contact Request"
3. THE Notification_Email SHALL contain all submitted fields formatted as an HTML table: Inquiry Type, Company Name, First Name, Last Name, Email, Telephone, and Industry
4. THE Notification_Email SHALL be sent from the ask@3inventors.com account using the Ask email department

### Requirement 10: Confirmation Email — Demo Request Template

**User Story:** As a visitor who requested a demo, I want to receive a branded confirmation email, so that I know my request was received and what to expect next.

#### Acceptance Criteria

1. WHEN the Inquiry_Type is "Demo Request", THE Email_Builder SHALL generate a confirmation email with the subject "3 Inventors Portal — Demo Request Received"
2. THE Confirmation_Email SHALL use a table-based HTML layout with all CSS inline, compatible with Outlook, Gmail, and Apple Mail
3. THE Confirmation_Email SHALL include a header section with the 3 Inventors logo, the tagline "Business Management Platform", and a 4px solid accent line in colour #0D5EA6
4. THE Confirmation_Email SHALL display a badge with text "Demo Request Received", a greeting "Hi {FirstName},", and a body paragraph confirming the demo request with a 24-hour response commitment
5. THE Confirmation_Email SHALL include a "What you requested" details card showing Platform (value: "3 Inventors Portal"), Industry (if provided), and Company (if provided)
6. THE Confirmation_Email SHALL include a "What to expect" section with three numbered steps: scheduling contact, guided walkthrough, and workflow discussion
7. THE Confirmation_Email SHALL include a footer with "3 Inventors Limited", "Nicosia, Cyprus · 700 75 700", a link to www.3inventors.com, and the tagline "Knowledge · Professionalism · Innovation"
8. THE Confirmation_Email SHALL use solid background colours only — no CSS gradients — for Outlook compatibility

### Requirement 11: Confirmation Email — General Inquiry Template

**User Story:** As a visitor who submitted a general inquiry or pricing question, I want to receive a branded confirmation email, so that I know my message was received.

#### Acceptance Criteria

1. WHEN the Inquiry_Type is not "Demo Request", THE Email_Builder SHALL generate a confirmation email with the subject "3 Inventors Portal — Message Received"
2. THE Confirmation_Email SHALL display a badge with text "Message Received", a greeting "Hi {FirstName},", and a body paragraph confirming the inquiry with a 24-hour response commitment
3. THE Confirmation_Email SHALL include a "Your inquiry details" card showing Name (FirstName + LastName), Email, and Company (if provided)
4. THE Confirmation_Email SHALL include a "What happens next" section with three numbered steps: team review, personalised reply within 24 hours, and optional call suggestion
5. THE Confirmation_Email SHALL include a closing paragraph directing the visitor to www.3inventors.com and inviting them to reply with additional details
6. THE Confirmation_Email SHALL HTML-encode all user-provided values before inserting them into the template to prevent XSS

### Requirement 12: Configuration

**User Story:** As a developer, I want all feature settings stored in appsettings.json, so that reCAPTCHA keys and thresholds can be changed without code modifications.

#### Acceptance Criteria

1. THE Landing_Page SHALL read the ReCaptcha:SiteKey from appsettings.json to render the reCAPTCHA script tag and expose the key to JavaScript
2. THE Landing_Controller SHALL read the ReCaptcha:SecretKey from appsettings.json for server-side token verification
3. THE Landing_Controller SHALL read the ReCaptcha:ScoreThreshold from appsettings.json with a default value of 0.5 if not configured
4. THE appsettings.json SHALL contain a "ReCaptcha" section with keys: SiteKey, SecretKey, and ScoreThreshold

### Requirement 13: Accessibility and UX Standards

**User Story:** As a visitor using assistive technology, I want the contact modal to be accessible, so that I can submit inquiries regardless of how I interact with the page.

#### Acceptance Criteria

1. THE Contact_Modal SHALL use appropriate ARIA attributes: role="dialog", aria-modal="true", and aria-labelledby referencing the modal title element
2. WHEN the Contact_Modal opens, THE Landing_Page SHALL move keyboard focus to the first focusable element within the modal
3. THE Honeypot_Field SHALL include aria-hidden="true" to prevent screen readers from announcing it
4. THE Contact_Modal SHALL use the 3 Inventors design system colours (Primary Blue #0D5EA6, Accent Cyan #57B8E8) and typography (Manrope headings, Inter body) consistent with the Landing_Page
5. THE Contact_Modal SHALL be responsive and usable on viewport widths from 320px to 1920px
