# Implementation Plan: Legal Pages

## Overview

Add four standalone legal pages (Terms & Conditions, Terms of Use, Privacy Policy, Cookie Policy) to the 3 Inventors Portal. Implementation involves creating a new `LegalController` with anonymous access and attribute-routed URLs, four self-contained Razor views with inline CSS matching the landing page visual language, and updating the landing page footer links from placeholder `#` anchors to actual legal page URLs.

## Tasks

- [x] 1. Create LegalController with attribute-routed actions
  - [x] 1.1 Create `Controllers/LegalController.cs` with four actions
    - Add `[AllowAnonymous]` at class level (matching `LandingController` pattern)
    - Add `[HttpGet]` and `[Route]` attributes for each action: `/Terms-and-Conditions`, `/Terms-of-Use`, `/Privacy-Policy`, `/Cookies-Policy`
    - Each action returns `View()`
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

  - [ ]* 1.2 Write unit tests for LegalController
    - Verify each action returns a `ViewResult`
    - Verify the controller class has `[AllowAnonymous]` attribute
    - Verify each action has the correct `[Route]` attribute value
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

- [x] 2. Implement Terms & Conditions view
  - [x] 2.1 Create `Views/Legal/TermsAndConditions.cshtml`
    - Set `Layout = null` with full inline CSS
    - Implement Legal_Header: sticky nav with logo, legal page nav links (Terms & Conditions active), Sign In and Back to Site buttons
    - Implement Hero_Section: eyebrow "Legal & Policy", heading "Terms & Conditions", description, last-updated date
    - Implement Content_Card: frosted-glass container with legal text adapted from MyChair references, rebranded to "3 Inventors Portal — Business Management Platform"
    - Implement Legal_Footer: copyright "© 2026 3 Inventors. All rights reserved." and legal page links
    - Include responsive styles for ≤720px breakpoint (single column, reduced padding, stacked footer)
    - Use colour palette: Primary Blue #0D5EA6, Accent Cyan #57B8E8, text #0B1B28, muted #5E7385
    - Use typography: Manrope for headings, Inter for body text
    - Use background gradient pattern with radial gradients and grid-fog overlay
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 3.1, 3.2, 3.3, 3.4, 4.1, 4.2, 4.3, 4.4, 5.1, 5.2, 5.3, 5.4, 6.1, 6.2, 6.3, 6.4_

- [x] 3. Implement Terms of Use view
  - [x] 3.1 Create `Views/Legal/TermsOfUse.cshtml`
    - Set `Layout = null` with full inline CSS (same structure as Terms & Conditions)
    - Implement Legal_Header with Terms of Use nav link active
    - Implement Hero_Section: heading "Terms of Use", description "Acceptable use policies and user responsibilities when accessing the platform."
    - Implement Content_Card with Terms of Use legal text (rebranded from MyChair references)
    - Implement Legal_Footer matching the pattern
    - Include responsive styles for ≤720px breakpoint
    - Use privacy@3inventors.com as contact email for privacy-related references
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 3.1, 3.2, 3.3, 3.4, 4.1, 4.2, 4.3, 4.4, 5.1, 5.2, 5.3, 5.4, 6.1, 6.2, 6.3, 6.4_

- [x] 4. Implement Privacy Policy view
  - [x] 4.1 Create `Views/Legal/PrivacyPolicy.cshtml`
    - Set `Layout = null` with full inline CSS (same structure as Terms & Conditions)
    - Implement Legal_Header with Privacy Policy nav link active
    - Implement Hero_Section: heading "Privacy Policy", description "How we collect, use, store, and protect your personal information."
    - Implement Content_Card with Privacy Policy legal text (rebranded from MyChair references)
    - Implement Legal_Footer matching the pattern
    - Include responsive styles for ≤720px breakpoint
    - Use privacy@3inventors.com as contact email
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 3.1, 3.2, 3.3, 3.4, 4.1, 4.2, 4.3, 4.4, 5.1, 5.2, 5.3, 5.4, 6.1, 6.2, 6.3, 6.4_

- [x] 5. Implement Cookie Policy view
  - [x] 5.1 Create `Views/Legal/CookiesPolicy.cshtml`
    - Set `Layout = null` with full inline CSS (same structure as Terms & Conditions)
    - Implement Legal_Header with Cookies Policy nav link active
    - Implement Hero_Section: heading "Cookie Policy", description "Information about cookies and tracking technologies used on this platform."
    - Implement Content_Card with Cookie Policy legal text (rebranded from MyChair references)
    - Implement Legal_Footer matching the pattern
    - Include responsive styles for ≤720px breakpoint
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 3.1, 3.2, 3.3, 3.4, 4.1, 4.2, 4.3, 4.4, 5.1, 5.2, 5.3, 5.4, 6.1, 6.2, 6.3, 6.4_

- [x] 6. Checkpoint - Verify legal pages render correctly
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Update landing page footer links
  - [x] 7.1 Update `Views/Landing/Index.cshtml` footer links
    - Change `<a href="#">Terms &amp; Conditions</a>` to `<a href="/Terms-and-Conditions">Terms &amp; Conditions</a>`
    - Change `<a href="#">Terms of Use</a>` to `<a href="/Terms-of-Use">Terms of Use</a>`
    - Change `<a href="#">Privacy Policy</a>` to `<a href="/Privacy-Policy">Privacy Policy</a>`
    - Change `<a href="#">Cookie Policy</a>` to `<a href="/Cookies-Policy">Cookie Policy</a>`
    - _Requirements: 7.1, 7.2, 7.3, 7.4_

  - [ ]* 7.2 Write integration tests for routing
    - Verify `GET /Terms-and-Conditions` returns 200 OK
    - Verify `GET /Terms-of-Use` returns 200 OK
    - Verify `GET /Privacy-Policy` returns 200 OK
    - Verify `GET /Cookies-Policy` returns 200 OK
    - Verify all routes are accessible without authentication
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

- [x] 8. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- No database changes are required for this feature
- Property-based testing does not apply (no pure functions, data transformations, or business logic)
- All four views are self-contained (Layout = null, inline CSS) matching the existing landing page pattern
- The LegalController follows the same `[AllowAnonymous]` pattern as the existing `LandingController`
- Content is adapted from MyChair HTML references, rebranded to "3 Inventors Portal — Business Management Platform"

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2", "2.1"] },
    { "id": 2, "tasks": ["3.1", "4.1", "5.1"] },
    { "id": 3, "tasks": ["7.1"] },
    { "id": 4, "tasks": ["7.2"] }
  ]
}
```
