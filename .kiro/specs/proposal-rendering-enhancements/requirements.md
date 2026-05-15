# Requirements Document

## Introduction

This feature enhances the proposal snapshot rendering and quotation editing experience by introducing per-section totals boxes, narrative (rich content) sections, title/subtitle line item formatting, and subscription totals breakdowns (Monthly / Daily / Annual). These enhancements align the Portal's generated proposals with the visual quality and information density of real-world commercial quotations produced by the platform's target users.

## Glossary

- **Proposal_Renderer**: The Razor view component (Snapshot.cshtml) responsible for rendering the public-facing proposal snapshot.
- **Proposal_Section**: A named grouping of quotation lines within a quotation, rendered as a distinct card in the proposal snapshot.
- **Narrative_Section**: A Proposal_Section with SectionType "Narrative" that contains no line items — only a title and rich explanatory text content.
- **LineItems_Section**: A Proposal_Section with SectionType "LineItems" that contains a table of priced quotation lines.
- **Section_Totals_Box**: A summary box rendered below a section's line items table showing calculated totals specific to that section's ColumnConfiguration.
- **Subscription_Totals**: A breakdown showing Total Monthly, Total Daily Cost (Monthly / 30), and Total Annual (Monthly × 12) for subscription-type sections.
- **Signal_Card**: A design pattern using a left border accent (4px solid color) to visually emphasize a content card.
- **QuotationLine**: An individual priced item within a Quotation.
- **Business_User**: An authenticated user with quotation module permissions for the relevant business.
- **Portal**: The ASP.NET Core MVC web application.

## Requirements

### Requirement 1: Per-Section Totals Box for Subscription Sections

**User Story:** As a customer viewing a shared proposal, I want to see a totals breakdown below each subscription section's line items, so that I can immediately understand the monthly, daily, and annual cost commitment for that group of services.

#### Acceptance Criteria

1. WHEN a Proposal_Section has ColumnConfiguration "Subscription", THE Proposal_Renderer SHALL render a Section_Totals_Box below that section's line items table.
2. THE Section_Totals_Box for a "Subscription" section SHALL display three values: Total Monthly (sum of all line monthly prices), Total Daily Cost (Total Monthly divided by 30, rounded to 2 decimal places), and Total Annual (Total Monthly multiplied by 12).
3. THE Proposal_Renderer SHALL calculate the Total Monthly value as the sum of LineTotal for all QuotationLines in that section.
4. THE Proposal_Renderer SHALL format the Total Daily Cost with a "/day" suffix (e.g., "€5.40/day").
5. THE Proposal_Renderer SHALL format all monetary values in the Section_Totals_Box using the business CurrencySymbol.

### Requirement 2: Per-Section Totals Box for OneTime Sections

**User Story:** As a customer viewing a shared proposal, I want to see a subtotal below each one-time section's line items, so that I can understand the total one-time cost for that group of services.

#### Acceptance Criteria

1. WHEN a Proposal_Section has ColumnConfiguration "OneTime", THE Proposal_Renderer SHALL render a Section_Totals_Box below that section's line items table.
2. THE Section_Totals_Box for a "OneTime" section SHALL display a single value: Section Subtotal (sum of LineTotal for all QuotationLines in that section).
3. THE Proposal_Renderer SHALL format the Section Subtotal using the business CurrencySymbol.

### Requirement 3: Narrative Section Type

**User Story:** As a Business_User, I want to create narrative sections that contain only explanatory text without line items, so that I can include contextual information like operational positioning, included platforms, support details, and commercial notes in my proposals.

#### Acceptance Criteria

1. THE Portal SHALL allow a Proposal_Section to have a SectionType value of "Narrative" or "LineItems".
2. WHEN a Proposal_Section has SectionType "Narrative", THE Proposal_Renderer SHALL render the section as a content card displaying the section Name as a heading and the Description as rich text body content.
3. WHEN a Proposal_Section has SectionType "Narrative", THE Proposal_Renderer SHALL not render a line items table for that section.
4. THE Portal database SHALL store the SectionType as a non-nullable NVARCHAR(20) column on the [quotation].[ProposalSection] table with a default value of "LineItems".
5. THE ProposalSection entity SHALL expose SectionType as a string property.

### Requirement 4: Narrative Section Emphasis Toggle

**User Story:** As a Business_User, I want to emphasize certain narrative sections with a colored left border accent, so that I can visually distinguish important information blocks in the proposal.

#### Acceptance Criteria

1. THE Portal SHALL allow a Proposal_Section to have an IsEmphasized flag (BIT column, default 0).
2. WHEN a Proposal_Section has IsEmphasized set to true, THE Proposal_Renderer SHALL render that section card with a 4px left border accent using the Signal_Card pattern.
3. WHEN a Proposal_Section has IsEmphasized set to true and an AccentColor value is specified, THE Proposal_Renderer SHALL use the specified AccentColor for the left border.
4. WHEN a Proposal_Section has IsEmphasized set to true and no AccentColor is specified, THE Proposal_Renderer SHALL use the default Primary Blue (#0D5EA6) for the left border.
5. THE Portal database SHALL store the AccentColor as a nullable NVARCHAR(20) column on the [quotation].[ProposalSection] table.
6. THE ProposalSection entity SHALL expose IsEmphasized as a bool property and AccentColor as a nullable string property.

### Requirement 5: Line Item Title and Subtitle

**User Story:** As a Business_User, I want to split line item descriptions into a bold title and an optional muted subtitle, so that my proposals match the professional formatting pattern where items have a primary name and secondary detail text.

#### Acceptance Criteria

1. THE Portal database SHALL add a nullable Subtitle column (NVARCHAR(1000)) to the [quotation].[QuotationLine] table.
2. THE QuotationLine entity SHALL expose Subtitle as a nullable string property.
3. THE Proposal_Renderer SHALL render the QuotationLine Description as a bold title line in the line items table.
4. WHEN a QuotationLine has a non-null Subtitle value, THE Proposal_Renderer SHALL render the Subtitle below the title in a smaller font size with a muted text color.
5. WHEN a QuotationLine has a null or empty Subtitle value, THE Proposal_Renderer SHALL render only the Description title without additional spacing.
6. THE Portal SHALL allow the Business_User to enter and edit the Subtitle field when creating or editing a QuotationLine.

### Requirement 6: Subscription Section Column Rendering

**User Story:** As a customer viewing a shared proposal, I want subscription sections to display Monthly Price, Daily Cost, and Annual Price columns, so that I can see the cost breakdown at each time granularity for every line item.

#### Acceptance Criteria

1. WHEN a Proposal_Section has ColumnConfiguration "Subscription", THE Proposal_Renderer SHALL display columns: No., Description, Monthly Price, Daily Cost, and Annual Price.
2. THE Proposal_Renderer SHALL calculate the Daily Cost column value for each line as UnitPrice divided by 30, rounded to 2 decimal places.
3. THE Proposal_Renderer SHALL calculate the Annual Price column value for each line as UnitPrice multiplied by 12.
4. THE Proposal_Renderer SHALL format the Daily Cost with a "/day" suffix.
5. THE Proposal_Renderer SHALL format all monetary column values using the business CurrencySymbol.

### Requirement 7: Schema Migration for Proposal Rendering Enhancements

**User Story:** As a developer, I want the database schema extended with the new columns required for narrative sections, emphasis, accent colors, and line item subtitles, so that the application can persist and retrieve the enhanced proposal data.

#### Acceptance Criteria

1. THE Portal database SHALL add a non-nullable SectionType column (NVARCHAR(20), DEFAULT 'LineItems') to the [quotation].[ProposalSection] table.
2. THE Portal database SHALL add a non-nullable IsEmphasized column (BIT, DEFAULT 0) to the [quotation].[ProposalSection] table.
3. THE Portal database SHALL add a nullable AccentColor column (NVARCHAR(20)) to the [quotation].[ProposalSection] table.
4. THE Portal database SHALL add a nullable Subtitle column (NVARCHAR(1000)) to the [quotation].[QuotationLine] table.
5. THE existing ProposalSection records SHALL retain their current data after the schema migration, with SectionType defaulting to "LineItems" and IsEmphasized defaulting to 0.
6. THE existing QuotationLine records SHALL retain their current data after the schema migration, with Subtitle defaulting to NULL.

### Requirement 8: Render Model Extensions

**User Story:** As a developer, I want the ProposalSectionRenderModel and ProposalLineRenderModel extended with the new fields, so that the Razor view has access to all data needed for enhanced rendering.

#### Acceptance Criteria

1. THE ProposalSectionRenderModel SHALL expose SectionType as a string property.
2. THE ProposalSectionRenderModel SHALL expose IsEmphasized as a bool property.
3. THE ProposalSectionRenderModel SHALL expose AccentColor as a nullable string property.
4. THE ProposalLineRenderModel SHALL expose Subtitle as a nullable string property.
5. WHEN constructing the ProposalSectionRenderModel, THE Portal SHALL populate SectionType, IsEmphasized, and AccentColor from the corresponding ProposalSection entity values.
6. WHEN constructing the ProposalLineRenderModel, THE Portal SHALL populate Subtitle from the corresponding QuotationLine entity value.
