# Implementation Plan: Landing Page Routing

## Overview

This plan implements a public-facing landing page at the root URL (`/`) for unauthenticated visitors, while authenticated users are redirected to the Dashboard. The implementation adds a new `LandingController`, updates routing on `HomeController`, adds a `Register` action to `AccountController`, and creates a standalone Razor view faithfully reproducing the HTML mock.

## Tasks

- [x] 1. Create LandingController and update routing
  - [x] 1.1 Create LandingController with authentication-based routing
    - Create `Portal.Web/Controllers/LandingController.cs`
    - Add `[AllowAnonymous]` at class level
    - Add `[HttpGet]` and `[Route("/")]` on the `Index` action
    - Implement authentication check: if `User.Identity?.IsAuthenticated == true`, return `RedirectToAction("Index", "Home")`; otherwise return `View()`
    - No constructor dependencies needed
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

  - [x] 1.2 Add `[Route("/Dashboard")]` to HomeController.Index
    - Add `[Route("/Dashboard")]` attribute to the existing `Index` action in `Portal.Web/Controllers/HomeController.cs`
    - Existing `[Authorize]` on the class ensures Dashboard remains protected
    - No other changes to HomeController logic
    - _Requirements: 1.2, 1.3_

  - [x] 1.3 Add Register action to AccountController
    - Add a new `[HttpGet]` `[AllowAnonymous]` `Register` action to `Portal.Web/Controllers/AccountController.cs`
    - Accept optional `string? plan` parameter
    - If `plan` is null or whitespace, redirect to `/Account/Login`
    - If `plan` has a value, redirect to `/Account/Login?plan={value}`
    - Use `Url.Action` to build the redirect URL
    - _Requirements: 8.1, 8.2, 8.3, 8.4_

  - [x]* 1.4 Write unit tests for LandingController routing logic
    - Create `Portal.Tests/Controllers/LandingControllerTests.cs`
    - Test: unauthenticated user returns `ViewResult`
    - Test: authenticated user returns `RedirectToActionResult` to Home/Index
    - Mock `HttpContext` and `ClaimsPrincipal` for authentication state
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

  - [x]* 1.5 Write unit tests for AccountController.Register
    - Create `Portal.Tests/Controllers/AccountControllerRegisterTests.cs`
    - Test: `plan=starter` redirects to `/Account/Login?plan=starter`
    - Test: `plan=business` redirects to `/Account/Login?plan=business`
    - Test: `plan=enterprise` redirects to `/Account/Login?plan=enterprise`
    - Test: no plan parameter redirects to `/Account/Login`
    - Test: empty/whitespace plan redirects to `/Account/Login` without plan param
    - _Requirements: 8.1, 8.2, 8.3, 8.4_

- [x] 2. Checkpoint - Verify controller routing
  - Ensure all tests pass, ask the user if questions arise.

- [x] 3. Create Landing Page Razor View
  - [x] 3.1 Create the standalone Landing Page view
    - Create `Portal.Web/Views/Landing/Index.cshtml`
    - Set `Layout = null` — standalone HTML document (no authenticated layout)
    - Include full HTML5 document structure with `<html lang="en">`
    - Add SEO meta tags: `<title>`, `<meta name="description">`, `<meta name="theme-color" content="#0D5EA6">`
    - Add Google Fonts link for Manrope and Inter
    - Include all CSS inline in a `<style>` element in `<head>`
    - Faithfully reproduce the content and structure from `.kiro/docs/LandingPage/3_inventors_portal_landing_page.html`
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 10.1, 10.2, 10.3, 10.4_

  - [x] 3.2 Implement Navigation Bar section
    - Sticky nav with semi-transparent background and backdrop blur
    - Brand logo and "3 Inventors Portal" name
    - Anchor links: "Features" (`#features`), "Pricing" (`#pricing`)
    - "Sign In" button linking to `/Account/Login`
    - "Get Started" button linking to `#pricing`
    - CSS for sticky positioning and responsive behavior
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7_

  - [x] 3.3 Implement Hero Section
    - Headline: "Run your business with structure, clarity, and control."
    - Subtitle describing platform capabilities
    - "Start Free Trial" primary CTA button linking to `#pricing`
    - "See Features" secondary CTA button linking to `#features`
    - Product preview card with sample dashboard KPIs and invoice data
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

  - [x] 3.4 Implement Features Section
    - Section heading: "Everything your business needs to operate professionally."
    - Four feature cards: Sales, Finance, VAT, Purchasing with icons, titles, descriptions
    - `id="features"` anchor identifier on the section
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [x] 3.5 Implement Operational Philosophy Section
    - Dark-themed quote card: "Designed for operators, not accounting complexity."
    - Three mini-cards: "Clear daily workspace", "Professional documents", "Controlled growth"
    - Each mini-card with title and description from HTML mock
    - _Requirements: 6.1, 6.2, 6.3_

  - [x] 3.6 Implement Pricing Section
    - `id="pricing"` anchor identifier
    - Three pricing tiers: Starter (€29/mo), Business (€59/mo), Enterprise (€149/mo)
    - Business tier highlighted with "Most Practical" badge and elevated styling
    - Each card: tier name, description, price, feature list, "Get Started" button
    - Starter button links to `/Account/Register?plan=starter`
    - Business button links to `/Account/Register?plan=business`
    - Enterprise button links to `/Account/Register?plan=enterprise`
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7_

  - [x] 3.7 Implement CTA Strip and Footer
    - CTA strip: "Ready to bring structure to your business?" with "View Pricing" button linking to `#pricing`
    - Footer: "3 Inventors Limited · Business Management Platform" and tagline "Knowledge · Professionalism · Innovation"
    - _Requirements: 9.1, 9.2_

  - [x] 3.8 Implement responsive CSS breakpoints
    - Add `@media (max-width: 980px)` breakpoint: stack grids, hide text-only nav links, show only button links
    - Add `@media (max-width: 640px)` breakpoint: reduce heading sizes, card padding, border radius; make buttons full-width
    - Ensure `scroll-behavior: smooth` on `html` element for anchor navigation
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5_

- [x] 4. Checkpoint - Verify landing page renders correctly
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Property-based tests and integration verification
  - [x]* 5.1 Write property test for unauthenticated routing (Property 1)
    - **Property 1: Unauthenticated users always see the landing page**
    - Create test in `Portal.Tests/Properties/LandingRoutingPropertyTests.cs`
    - Use FsCheck to generate arbitrary request contexts with unauthenticated identity
    - Assert `LandingController.Index()` always returns `ViewResult` for any unauthenticated user
    - **Validates: Requirements 1.1, 1.4**

  - [x]* 5.2 Write property test for authenticated routing (Property 2)
    - **Property 2: Authenticated users always get redirected to Dashboard**
    - Use FsCheck to generate arbitrary authenticated user claims (varying roles, claim sets)
    - Assert `LandingController.Index()` always returns `RedirectToActionResult` for any authenticated user
    - **Validates: Requirements 1.2, 1.3**

  - [x]* 5.3 Write property test for registration URL redirect (Property 3)
    - **Property 3: Registration URL redirect preserves plan parameter**
    - Use FsCheck to generate arbitrary non-empty string values for the `plan` parameter
    - Assert `AccountController.Register(plan)` always redirects to a URL containing the original plan value
    - Test with empty, whitespace, special characters, and long strings
    - **Validates: Requirements 8.1, 8.2, 8.3, 8.4**

  - [x]* 5.4 Write integration tests for routing and HTTP responses
    - Create `Portal.Tests/Integration/LandingPageRoutingTests.cs`
    - Test: anonymous GET `/` returns HTTP 200
    - Test: authenticated GET `/` returns HTTP 302 to `/Dashboard`
    - Test: GET `/Account/Register?plan=business` returns HTTP 302 to `/Account/Login?plan=business`
    - Test: GET `/Dashboard` unauthenticated redirects to `/Account/Login`
    - Use `WebApplicationFactory<Program>` for integration testing
    - **Validates: Requirements 1.1, 1.2, 1.3, 8.1, 8.2, 8.3, 12.3**

- [x] 6. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck
- Unit tests validate specific examples and edge cases using xUnit + Moq
- The landing page view (task 3) is the largest task — sub-tasks 3.1–3.8 should be implemented as a single cohesive view file, not separate files
- No database changes, new services, or new repositories are needed for this feature
- The existing Login page at `/Account/Login` must remain unchanged (Requirement 12)

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["1.4", "1.5", "3.1"] },
    { "id": 2, "tasks": ["3.2", "3.3", "3.4", "3.5", "3.6", "3.7"] },
    { "id": 3, "tasks": ["3.8"] },
    { "id": 4, "tasks": ["5.1", "5.2", "5.3", "5.4"] }
  ]
}
```
