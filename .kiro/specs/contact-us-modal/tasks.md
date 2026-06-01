# Implementation Plan: Contact Us Modal

## Overview

This plan implements a context-aware contact modal on the 3 Inventors Portal landing page. The implementation covers: appsettings configuration, the `ContactUsRequest` model, `IReCaptchaService` with its implementation, the `ContactUs` controller action on `LandingController`, the `_ContactModal.cshtml` partial view, the `contact-modal.js` client-side module, and property-based tests validating correctness properties from the design.

## Tasks

- [x] 1. Configuration and core interfaces
  - [x] 1.1 Add reCAPTCHA configuration to appsettings.json
    - Add the `"ReCaptcha"` section with `SiteKey`, `SecretKey`, and `ScoreThreshold` (default 0.5) to `Portal.Web/appsettings.json`
    - Add the same section with empty/dev values to `Portal.Web/appsettings.Development.json`
    - _Requirements: 12.1, 12.2, 12.3, 12.4_

  - [x] 1.2 Create the ContactUsRequest model
    - Create `Portal.Web/Models/ContactUsRequest.cs` with properties: InquiryType, CompanyName, FirstName (required), LastName, Email (required), Telephone, Industry, Website (honeypot), RecaptchaToken
    - Add `[Required]` data annotations on FirstName and Email
    - Add `[EmailAddress]` validation on Email
    - _Requirements: 2.1, 2.5, 2.6_

  - [x] 1.3 Create IReCaptchaService interface and ReCaptchaResult model
    - Create `Portal.Web/Services/IReCaptchaService.cs` with `Task<ReCaptchaResult> VerifyAsync(string token)` method
    - Create `Portal.Web/Services/ReCaptchaResult.cs` with properties: Success (bool), Score (double), Action (string?), ErrorCodes (string[]?)
    - Create internal `GoogleReCaptchaResponse` class with `[JsonPropertyName]` attributes for deserialization
    - _Requirements: 6.1, 6.4_

  - [x] 1.4 Implement ReCaptchaService
    - Create `Portal.Web/Services/ReCaptchaService.cs` implementing `IReCaptchaService`
    - Inject `HttpClient` (via `IHttpClientFactory`), `IConfiguration`, and `ILogger<ReCaptchaService>`
    - Read `ReCaptcha:SecretKey` from configuration
    - POST to `https://www.google.com/recaptcha/api/siteverify` with secret and response token
    - Deserialize response into `ReCaptchaResult`
    - _Requirements: 6.1, 6.4_

  - [x] 1.5 Register IReCaptchaService in DI container
    - In `Portal.Web/Program.cs`, register `IReCaptchaService` with `AddHttpClient<ReCaptchaService>()` for typed HttpClient injection
    - _Requirements: 6.1_

- [x] 2. Server-side ContactUs action
  - [x] 2.1 Implement LandingController.ContactUs action
    - Add `[HttpPost]`, `[ValidateAntiForgeryToken]`, `[AllowAnonymous]` decorated `ContactUs` action to `Portal.Web/Controllers/LandingController.cs`
    - Inject `IReCaptchaService`, `IEmailSender`, and `ILogger<LandingController>` (if not already injected)
    - Implement honeypot check first: if `Website` field is non-empty, log warning and return `Ok()` silently
    - Implement reCAPTCHA verification: skip if SecretKey is empty; otherwise verify token, return 400 if score < threshold or success is false; log failures with score/action
    - Build notification email with subject `"3 Inventors Portal — {InquiryType}"` (fallback to "Contact Request" if empty) and HTML table body containing all submitted fields
    - Send notification email to `ask@3inventors.com` via `IEmailSender` using Ask department
    - Build confirmation email via `ConfirmationEmailBuilder.Build(...)` and send to visitor's email
    - Wrap email sending in try/catch: log error and return 400 on failure
    - Return `Ok()` on success
    - _Requirements: 7.1, 7.2, 7.3, 6.2, 6.3, 6.5, 6.6, 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 9.1, 9.2, 9.3, 9.4_

  - [ ]* 2.2 Write property test: Honeypot detection silently rejects
    - **Property 1: Honeypot detection silently rejects and logs**
    - Create `Portal.Tests/PropertyBased/ContactUs/HoneypotPropertyTests.cs`
    - Generate random non-empty strings for the Website field; verify 200 response, no IEmailSender calls, and warning logged
    - **Validates: Requirements 7.1, 7.2**

  - [ ]* 2.3 Write property test: reCAPTCHA score below threshold returns 400
    - **Property 2: reCAPTCHA score below threshold returns 400**
    - Create `Portal.Tests/PropertyBased/ContactUs/ReCaptchaThresholdPropertyTests.cs`
    - Generate random doubles in [0, threshold); verify 400 response
    - **Validates: Requirements 6.2**

  - [ ]* 2.4 Write property test: Valid request sends both emails
    - **Property 3: Valid request sends both notification and confirmation emails**
    - Create `Portal.Tests/PropertyBased/ContactUs/EmailSendingPropertyTests.cs`
    - Generate valid requests (empty honeypot, score ≥ threshold); verify exactly 2 IEmailSender calls and 200 response
    - **Validates: Requirements 8.2, 8.3, 8.6**

- [x] 3. Notification and confirmation email building
  - [x] 3.1 Implement notification email body builder
    - Add a method (or extend existing helper) to build the notification HTML table body containing all submitted fields: Inquiry Type, Company Name, First Name, Last Name, Email, Telephone, Industry
    - Use HTML-encoding for all user-provided values
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

  - [ ]* 3.2 Write property test: Notification email subject and body formatting
    - **Property 4: Notification email subject and body formatting**
    - Create `Portal.Tests/PropertyBased/ContactUs/NotificationFormattingPropertyTests.cs`
    - Generate random form data; verify subject format `"3 Inventors Portal — {InquiryType}"` and body contains all non-empty field values
    - **Validates: Requirements 9.1, 9.3**

  - [x] 3.3 Extend ConfirmationEmailBuilder for Demo Request template
    - Ensure `ConfirmationEmailBuilder.Build(...)` handles InquiryType "Demo Request": subject "3 Inventors Portal — Demo Request Received", greeting "Hi {FirstName},", details card with Platform/Industry/Company (omit null/whitespace), "What to expect" steps, branded header with logo and #0D5EA6 accent, footer with company details
    - Use table-based HTML layout with all CSS inline, no gradients, solid backgrounds only
    - HTML-encode all user-provided values
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 10.6, 10.7, 10.8, 11.6_

  - [ ]* 3.4 Write property test: Demo confirmation template
    - **Property 5: Demo confirmation template includes greeting and optional details**
    - Create `Portal.Tests/PropertyBased/ContactUs/DemoTemplatePropertyTests.cs`
    - Generate random first names and optional fields; verify greeting "Hi {FirstName}," and conditional inclusion of Industry/CompanyName
    - **Validates: Requirements 10.4, 10.5**

  - [x] 3.5 Extend ConfirmationEmailBuilder for General Inquiry template
    - Ensure `ConfirmationEmailBuilder.Build(...)` handles non-Demo inquiry types: subject "3 Inventors Portal — Message Received", badge "Message Received", greeting "Hi {FirstName},", details card with Name/Email/Company (omit null/whitespace), "What happens next" steps, closing paragraph with www.3inventors.com link
    - Use table-based HTML layout with all CSS inline, no gradients
    - HTML-encode all user-provided values
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5, 11.6_

  - [ ]* 3.6 Write property test: General inquiry template
    - **Property 6: General inquiry template subject and details card**
    - Create `Portal.Tests/PropertyBased/ContactUs/GeneralTemplatePropertyTests.cs`
    - Generate random non-demo inquiry types; verify subject "3 Inventors Portal — Message Received" and conditional field inclusion
    - **Validates: Requirements 11.1, 11.3**

  - [ ]* 3.7 Write property test: HTML encoding prevents XSS
    - **Property 7: HTML encoding prevents XSS in email templates**
    - Create `Portal.Tests/PropertyBased/ContactUs/HtmlEncodingPropertyTests.cs`
    - Generate strings with HTML special characters; verify encoded output and no raw unencoded characters
    - **Validates: Requirements 11.6**

  - [ ]* 3.8 Write property test: No CSS gradients in confirmation emails
    - **Property 8: No CSS gradients in confirmation emails**
    - Create `Portal.Tests/PropertyBased/ContactUs/NoGradientsPropertyTests.cs`
    - Generate random input combinations; verify no "gradient" substring in style attributes
    - **Validates: Requirements 10.8**

- [x] 4. Checkpoint - Ensure all server-side tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Client-side modal implementation
  - [x] 5.1 Create the _ContactModal.cshtml partial view
    - Create `Portal.Web/Views/Landing/_ContactModal.cshtml` with the modal HTML structure
    - Include: fixed overlay with semi-transparent backdrop, modal card with close button (X), contextual header (badge, title, subtitle), form fields (Company Name, First Name, Last Name, Email, Telephone, Industry dropdown), hidden inquiryType field, honeypot field (off-screen, aria-hidden="true", tabindex="-1"), antiforgery token, submit button, Google reCAPTCHA attribution text
    - Apply ARIA attributes: role="dialog", aria-modal="true", aria-labelledby referencing the title
    - Use 3 Inventors design system colours (Primary Blue #0D5EA6, Accent Cyan #57B8E8) and typography (Manrope headings, Inter body)
    - Ensure responsive layout from 320px to 1920px
    - _Requirements: 1.1–1.6, 2.1–2.6, 3.1, 13.1, 13.3, 13.4, 13.5_

  - [x] 5.2 Render the partial and add CTA button data attributes in Landing/Index.cshtml
    - Add `@Html.Partial("_ContactModal")` to `Portal.Web/Views/Landing/Index.cshtml`
    - Add `data-inquiry-type` attributes to existing CTA buttons on the landing page (hero, pricing cards, footer)
    - Add the reCAPTCHA v3 script tag with site key from ViewBag/ViewData (set by controller from configuration)
    - Add CSS to hide the reCAPTCHA badge
    - _Requirements: 5.1, 5.5, 12.1_

  - [x] 5.3 Pass reCAPTCHA SiteKey from LandingController to the view
    - In the `Index` action of `LandingController`, read `ReCaptcha:SiteKey` from configuration and pass it to the view via ViewBag or ViewData
    - _Requirements: 12.1, 5.1_

  - [x] 5.4 Implement contact-modal.js
    - Create `Portal.Web/wwwroot/js/contact-modal.js`
    - Implement modal open: read `data-inquiry-type` from clicked CTA, set badge/title/subtitle/hidden field from `MODAL_CONTEXTS` map, show overlay, prevent body scroll, move focus to first focusable element
    - Implement modal close: close button, backdrop click, Escape key; restore body scroll, reset form
    - Implement form validation: require FirstName and valid Email before submission
    - Implement reCAPTCHA token acquisition: call `grecaptcha.execute(siteKey, {action: 'contact_form'})`, handle failure with "Verification failed — try again" button text for 3s
    - Implement form submission: disable button with "Sending...", fetch POST with antiforgery token header, append recaptchaToken to form data
    - Implement success handling: close modal, reset form, show success toast "Your request has been sent. We will get in touch within 24 hours." with 6s auto-hide
    - Implement error handling: server error → "Something went wrong — try again" for 3s; network error → "Connection error — try again" for 3s; then revert to "Send Request"
    - _Requirements: 1.1–1.6, 3.1–3.5, 4.1–4.6, 5.2–5.4, 13.2_

- [x] 6. Checkpoint - Ensure full integration works
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 7. Integration tests
  - [ ]* 7.1 Write integration tests for ContactUs endpoint
    - Create `Portal.Tests/Integration/ContactUs/ContactUsIntegrationTests.cs`
    - Test full POST to `/Landing/ContactUs` with mocked `IEmailSender` and `IReCaptchaService`
    - Test honeypot rejection returns 200 with no emails sent
    - Test reCAPTCHA failure returns 400
    - Test valid submission returns 200 with both emails sent
    - Test missing antiforgery token returns 400
    - _Requirements: 7.1, 7.2, 6.2, 8.2, 8.3, 8.6_

- [x] 8. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck.Xunit
- Unit tests validate specific examples and edge cases
- The existing `ConfirmationEmailBuilder` and `IEmailSender` services are reused — no new email infrastructure needed
- No database changes required — this feature is email-only with no persistence
- All client-side code uses vanilla JavaScript consistent with the project's existing patterns

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["1.4", "1.5"] },
    { "id": 2, "tasks": ["2.1", "3.1", "3.3", "3.5"] },
    { "id": 3, "tasks": ["2.2", "2.3", "2.4", "3.2", "3.4", "3.6", "3.7", "3.8"] },
    { "id": 4, "tasks": ["5.1", "5.3"] },
    { "id": 5, "tasks": ["5.2", "5.4"] },
    { "id": 6, "tasks": ["7.1"] }
  ]
}
```
