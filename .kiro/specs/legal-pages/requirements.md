# Requirements Document

## Introduction

This feature adds four standalone legal pages to the 3 Inventors Portal landing site: Terms & Conditions, Terms of Use, Privacy Policy, and Cookie Policy. Each page is a self-contained Razor view (Layout = null, inline CSS) accessible from the landing page footer. The pages follow the same visual language as the existing landing page and are served by a new LegalController with anonymous access. Content is adapted from the existing MyChair HTML mock references, rebranded to "3 Inventors Portal — Business Management Platform".

## Glossary

- **Legal_Page**: A standalone Razor view displaying legal or policy content, accessible without authentication
- **LegalController**: An ASP.NET Core MVC controller decorated with [AllowAnonymous] responsible for serving all legal page routes
- **Landing_Page**: The existing public-facing marketing page at the root URL (/)
- **Hero_Section**: The top area of a legal page containing an eyebrow label, page heading, description, and last-updated date
- **Content_Card**: A frosted-glass styled container holding the legal text with proper heading hierarchy
- **Legal_Header**: The sticky navigation bar on legal pages containing the logo, legal page navigation links, and action buttons
- **Legal_Footer**: The bottom bar on legal pages matching the landing page footer-bottom pattern

## Requirements

### Requirement 1: Legal Page Routing

**User Story:** As a visitor, I want to access legal pages at predictable URLs, so that I can review the platform's legal terms and policies.

#### Acceptance Criteria

1. WHEN a visitor navigates to /Terms-and-Conditions, THE LegalController SHALL return the Terms & Conditions view
2. WHEN a visitor navigates to /Terms-of-Use, THE LegalController SHALL return the Terms of Use view
3. WHEN a visitor navigates to /Privacy-Policy, THE LegalController SHALL return the Privacy Policy view
4. WHEN a visitor navigates to /Cookies-Policy, THE LegalController SHALL return the Cookie Policy view
5. THE LegalController SHALL allow anonymous access to all legal page routes without requiring authentication

### Requirement 2: Legal Page Layout Structure

**User Story:** As a visitor, I want legal pages to have a consistent, professional layout, so that I can easily read and navigate between legal documents.

#### Acceptance Criteria

1. THE Legal_Page SHALL use Layout = null and contain all CSS inline within the view file
2. THE Legal_Header SHALL display the 3 Inventors logo on the left side
3. THE Legal_Header SHALL display navigation links in the centre for Terms & Conditions, Terms of Use, Privacy Policy, and Cookies Policy
4. THE Legal_Header SHALL display a "Sign In" button linking to /Account/Login on the right side
5. THE Legal_Header SHALL display a "Back to Site" button linking to / on the right side
6. THE Legal_Footer SHALL display "© 2026 3 Inventors. All rights reserved." on the left side
7. THE Legal_Footer SHALL display links to Terms & Conditions, Terms of Use, Privacy Policy, and Cookie Policy on the right side

### Requirement 3: Legal Page Hero Section

**User Story:** As a visitor, I want each legal page to clearly identify its content, so that I know which legal document I am reading.

#### Acceptance Criteria

1. THE Hero_Section SHALL display an eyebrow label with the text "Legal & Policy"
2. THE Hero_Section SHALL display a large heading matching the page title (Terms & Conditions, Terms of Use, Privacy Policy, or Cookie Policy)
3. THE Hero_Section SHALL display a brief description of the page content
4. THE Hero_Section SHALL display a "Last updated" date

### Requirement 4: Legal Page Content Presentation

**User Story:** As a visitor, I want legal content presented in a readable, well-structured format, so that I can understand the terms and policies.

#### Acceptance Criteria

1. THE Content_Card SHALL use a frosted-glass visual style consistent with the landing page design system
2. THE Content_Card SHALL use proper heading hierarchy with h2 for main title, h3 for section headings, p for paragraphs, and ul for lists
3. THE Legal_Page SHALL adapt content from the MyChair HTML references, replacing "MyChair Operational Suite" with "3 Inventors Portal — Business Management Platform"
4. THE Legal_Page SHALL use privacy@3inventors.com as the contact email for all privacy-related references

### Requirement 5: Visual Consistency with Landing Page

**User Story:** As a visitor, I want legal pages to look like they belong to the same platform as the landing page, so that I have a cohesive brand experience.

#### Acceptance Criteria

1. THE Legal_Page SHALL use the same colour palette as the Landing_Page (Primary Blue #0D5EA6, Accent Cyan #57B8E8, text #0B1B28, muted #5E7385)
2. THE Legal_Page SHALL use the same typography as the Landing_Page (Manrope for headings, Inter for body text)
3. THE Legal_Page SHALL use the same background gradient pattern as the Landing_Page (radial gradients with grid-fog overlay)
4. THE Legal_Header SHALL use a sticky position with backdrop blur matching the landing page navigation style

### Requirement 6: Responsive Design

**User Story:** As a visitor on a mobile device, I want legal pages to be readable and usable, so that I can review legal content on any screen size.

#### Acceptance Criteria

1. WHILE the viewport width is 720px or less, THE Legal_Page SHALL display content in a single column layout
2. WHILE the viewport width is 720px or less, THE Content_Card SHALL reduce padding for improved readability
3. WHILE the viewport width is 720px or less, THE Legal_Footer SHALL stack copyright and links vertically
4. WHILE the viewport width is 720px or less, THE Legal_Header navigation links SHALL remain accessible

### Requirement 7: Landing Page Footer Link Integration

**User Story:** As a visitor on the landing page, I want footer legal links to navigate to the actual legal pages, so that I can access the legal documents from the main site.

#### Acceptance Criteria

1. WHEN the Landing_Page is rendered, THE Landing_Page footer SHALL link "Terms & Conditions" to /Terms-and-Conditions
2. WHEN the Landing_Page is rendered, THE Landing_Page footer SHALL link "Terms of Use" to /Terms-of-Use
3. WHEN the Landing_Page is rendered, THE Landing_Page footer SHALL link "Privacy Policy" to /Privacy-Policy
4. WHEN the Landing_Page is rendered, THE Landing_Page footer SHALL link "Cookie Policy" to /Cookies-Policy
