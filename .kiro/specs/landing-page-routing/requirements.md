# Requirements Document

## Introduction

The 3 Inventors Portal currently loads the authenticated Dashboard directly at the root URL (`/`), redirecting unauthenticated visitors to the login page. This feature introduces a public-facing landing page that unauthenticated visitors see when they visit the root URL, while authenticated users continue to be redirected to the Dashboard. The landing page faithfully reproduces an existing HTML mock as a standalone Razor view, presenting the platform's value proposition, features, pricing tiers, and navigation to sign-in and registration flows.

## Glossary

- **Landing_Page**: The public-facing, server-rendered Razor view displayed to unauthenticated visitors at the root URL (`/`), presenting the platform's value proposition, features, and pricing
- **Portal**: The 3 Inventors Portal ASP.NET Core MVC 8 web application hosted at portal.3inventors.com
- **Routing_Controller**: The MVC controller responsible for handling the root URL (`/`) and determining whether to show the Landing_Page or redirect to the Dashboard based on authentication state
- **Dashboard**: The authenticated home page at `/Dashboard` displaying business KPIs, revenue, invoices, and operational data
- **Navigation_Bar**: The sticky top navigation component of the Landing_Page containing the brand logo, anchor links, Sign In button, and Get Started button
- **Pricing_Section**: The section of the Landing_Page displaying three subscription tiers (Starter, Business, Enterprise) with pricing and feature lists
- **Registration_URL**: The placeholder URL pattern `/Account/Register?plan={tier}` linked from pricing cards, where `{tier}` is one of `starter`, `business`, or `enterprise`
- **HTML_Mock**: The reference HTML file at `.kiro/docs/LandingPage/3_inventors_portal_landing_page.html` that defines the visual design, content, and responsive behavior of the Landing_Page

## Requirements

### Requirement 1: Root URL Routing Based on Authentication State

**User Story:** As a visitor, I want to see a marketing landing page when I visit the root URL without being logged in, so that I can learn about the platform before signing in.

#### Acceptance Criteria

1. WHEN an unauthenticated user requests the root URL (`/`), THE Routing_Controller SHALL render the Landing_Page view
2. WHEN an authenticated user requests the root URL (`/`), THE Routing_Controller SHALL redirect the user to the Dashboard at `/Dashboard`
3. THE Routing_Controller SHALL return an HTTP 302 redirect response when redirecting authenticated users to the Dashboard
4. THE Landing_Page SHALL be accessible without requiring authentication or authorization

### Requirement 2: Landing Page Standalone Razor View

**User Story:** As a developer, I want the landing page implemented as a standalone Razor view without the authenticated layout, so that it renders independently like the login page.

#### Acceptance Criteria

1. THE Landing_Page SHALL set `Layout = null` to render without the authenticated sidebar and topbar layout
2. THE Landing_Page SHALL be a server-rendered Razor view (`.cshtml` file) within the Portal.Web project
3. THE Landing_Page SHALL include all CSS inline within a `<style>` element in the `<head>`, matching the pattern used by the Login view
4. THE Landing_Page SHALL faithfully reproduce the visual design, content, and structure defined in the HTML_Mock

### Requirement 3: Navigation Bar

**User Story:** As a visitor, I want a navigation bar with clear links to features, pricing, sign-in, and getting started, so that I can navigate the landing page and access the portal.

#### Acceptance Criteria

1. THE Navigation_Bar SHALL display the 3 Inventors Portal brand logo and name
2. THE Navigation_Bar SHALL contain an anchor link labeled "Features" that scrolls to the features section (`#features`)
3. THE Navigation_Bar SHALL contain an anchor link labeled "Pricing" that scrolls to the pricing section (`#pricing`)
4. THE Navigation_Bar SHALL contain a "Sign In" button that navigates to `/Account/Login`
5. THE Navigation_Bar SHALL contain a "Get Started" button that scrolls to the pricing section (`#pricing`)
6. THE Navigation_Bar SHALL remain sticky at the top of the viewport during scrolling
7. THE Navigation_Bar SHALL use a semi-transparent background with backdrop blur effect as defined in the HTML_Mock

### Requirement 4: Hero Section

**User Story:** As a visitor, I want to see a compelling hero section with a headline and call-to-action buttons, so that I immediately understand the platform's value proposition.

#### Acceptance Criteria

1. THE Landing_Page SHALL display a hero section with the headline "Run your business with structure, clarity, and control."
2. THE Landing_Page SHALL display a subtitle describing the platform's capabilities beneath the headline
3. THE Landing_Page SHALL display a "Start Free Trial" primary call-to-action button that scrolls to the pricing section (`#pricing`)
4. THE Landing_Page SHALL display a "See Features" secondary call-to-action button that scrolls to the features section (`#features`)
5. THE Landing_Page SHALL display a product preview card showing sample dashboard KPIs and invoice data as defined in the HTML_Mock

### Requirement 5: Features Section

**User Story:** As a visitor, I want to see the platform's key features organized in cards, so that I can understand what the platform offers.

#### Acceptance Criteria

1. THE Landing_Page SHALL display a features section with the heading "Everything your business needs to operate professionally."
2. THE Landing_Page SHALL display four feature cards: Sales, Finance, VAT, and Purchasing
3. EACH feature card SHALL display an icon, a title, and a description as defined in the HTML_Mock
4. THE features section SHALL be reachable via the `#features` anchor identifier

### Requirement 6: Operational Philosophy Section

**User Story:** As a visitor, I want to understand the platform's operational philosophy, so that I can see how it differs from traditional accounting tools.

#### Acceptance Criteria

1. THE Landing_Page SHALL display a dark-themed quote card with the heading "Designed for operators, not accounting complexity."
2. THE Landing_Page SHALL display three mini-cards alongside the quote card: "Clear daily workspace", "Professional documents", and "Controlled growth"
3. EACH mini-card SHALL contain a title and a description as defined in the HTML_Mock

### Requirement 7: Pricing Section

**User Story:** As a visitor, I want to see the available subscription tiers with pricing and features, so that I can choose the right plan for my business.

#### Acceptance Criteria

1. THE Pricing_Section SHALL display three pricing tiers: Starter (€29/mo), Business (€59/mo), and Enterprise (€149/mo)
2. THE Pricing_Section SHALL visually highlight the Business tier with a "Most Practical" badge and elevated card styling
3. EACH pricing card SHALL display the tier name, description, price, feature list, and a "Get Started" call-to-action button
4. THE Starter tier "Get Started" button SHALL link to `/Account/Register?plan=starter`
5. THE Business tier "Get Started" button SHALL link to `/Account/Register?plan=business`
6. THE Enterprise tier "Get Started" button SHALL link to `/Account/Register?plan=enterprise`
7. THE Pricing_Section SHALL be reachable via the `#pricing` anchor identifier

### Requirement 8: Registration URL Placeholder Handling

**User Story:** As a visitor clicking "Get Started" on a pricing card, I want to be directed appropriately even though registration is not yet built, so that I am not shown a broken page.

#### Acceptance Criteria

1. WHEN a visitor navigates to `/Account/Register?plan=starter`, THE Portal SHALL redirect to `/Account/Login`
2. WHEN a visitor navigates to `/Account/Register?plan=business`, THE Portal SHALL redirect to `/Account/Login`
3. WHEN a visitor navigates to `/Account/Register?plan=enterprise`, THE Portal SHALL redirect to `/Account/Login`
4. THE redirect from Registration_URL to Login SHALL preserve the `plan` query parameter for future use by appending it as a return URL or query parameter

### Requirement 9: CTA Strip and Footer

**User Story:** As a visitor, I want to see a final call-to-action and footer at the bottom of the page, so that I have one more opportunity to engage and can see the company identity.

#### Acceptance Criteria

1. THE Landing_Page SHALL display a CTA strip section with the heading "Ready to bring structure to your business?" and a "View Pricing" button linking to `#pricing`
2. THE Landing_Page SHALL display a footer containing "3 Inventors Limited · Business Management Platform" and the tagline "Knowledge · Professionalism · Innovation"

### Requirement 10: SEO Meta Tags

**User Story:** As a marketing stakeholder, I want the landing page to have proper SEO meta tags, so that search engines can index and display the page correctly.

#### Acceptance Criteria

1. THE Landing_Page SHALL set the HTML `<title>` to "3 Inventors Portal — Business Management Platform"
2. THE Landing_Page SHALL include a `<meta name="description">` tag with the content "Run your business with structure, clarity, and control. Manage quotations, invoices, customers, VAT, purchases, and revenue from one operational workspace."
3. THE Landing_Page SHALL include a `<meta name="theme-color">` tag with the value `#0D5EA6`
4. THE Landing_Page SHALL set the `lang` attribute on the `<html>` element to `en`

### Requirement 11: Responsive Design

**User Story:** As a visitor on a tablet or mobile device, I want the landing page to adapt to my screen size, so that I can read and navigate the content comfortably.

#### Acceptance Criteria

1. WHEN the viewport width is 980px or less, THE Landing_Page SHALL switch the hero grid, split layout, feature grid, and pricing grid to stacked single or two-column layouts
2. WHEN the viewport width is 980px or less, THE Navigation_Bar SHALL hide text-only anchor links and display only the button links
3. WHEN the viewport width is 640px or less, THE Landing_Page SHALL reduce heading font sizes, card padding, and border radius for mobile readability
4. WHEN the viewport width is 640px or less, THE Landing_Page SHALL make buttons full-width for touch accessibility
5. THE Landing_Page SHALL include responsive CSS breakpoints at 980px and 640px as defined in the HTML_Mock

### Requirement 12: Existing Login Page Preservation

**User Story:** As an existing user, I want the login page to remain unchanged, so that my sign-in experience is not disrupted.

#### Acceptance Criteria

1. THE Portal SHALL continue to serve the existing Login view at `/Account/Login` without modification
2. THE "Sign In" button on the Landing_Page SHALL navigate to `/Account/Login`
3. WHEN an unauthenticated user attempts to access a protected route, THE Portal SHALL continue to redirect to `/Account/Login` as the authentication challenge endpoint
