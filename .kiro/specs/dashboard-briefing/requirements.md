# Requirements: Dashboard Briefing

## Introduction

The Dashboard Briefing is a narrative summary card displayed at the top of the Portal dashboard. It presents the business state in plain, human-readable language — as if a trusted operations manager were giving a morning report. The briefing is template-driven (no AI/LLM), assembled from real-time business data, prioritized by urgency, and links directly to actionable pages.

The feature supports the 3 Inventors "Operational Intelligence" positioning: the platform doesn't just show numbers — it tells you what matters right now.

## Glossary

- **Briefing**: A collection of prioritized narrative sentences summarizing the current business state.
- **Signal**: A data point or metric that triggers a briefing sentence (e.g., overdue invoice count > 0).
- **Sentence Template**: A parameterized text pattern that produces a human-readable sentence from signal data.
- **Priority**: The order in which signals are presented — urgent items first, informational items last.
- **Insight**: A single sentence within the briefing, containing text, data values, and an optional action link.

## Requirements

### Requirement 1: Briefing Card Display

**User Story:** As a business user, I want to see a narrative summary at the top of my dashboard, so that I immediately understand what needs my attention without interpreting charts and numbers.

#### Acceptance Criteria

1. THE dashboard SHALL display a Briefing Card between the topbar and the KPI summary cards.
2. THE Briefing Card SHALL render as a section with a 3px left-border accent (#0D5EA6) and contain narrative text.
3. THE Briefing Card SHALL include a time-aware greeting: "Good morning", "Good afternoon", or "Good evening" based on the user's local time context.
4. WHEN no signals produce any insights, THE Briefing Card SHALL display a positive message: "Everything looks good. No items need your attention right now."
5. THE Briefing Card SHALL render server-side (not AJAX-loaded) to avoid layout shift.

### Requirement 2: Overdue Invoices Signal

**User Story:** As a business user, I want the briefing to alert me about overdue invoices, so I can prioritize collections.

#### Acceptance Criteria

1. WHEN one or more issued invoices have a DueDate earlier than today and an outstanding balance > 0, THE briefing SHALL include an insight stating the count and total overdue amount.
2. THE insight SHALL mention the oldest overdue invoice's customer name and days overdue.
3. THE insight SHALL link to the Revenue Receivables page.
4. THIS signal SHALL have Priority 1 (highest urgency).

### Requirement 3: Pending Proposals Signal

**User Story:** As a business user, I want to know how many proposals are awaiting client acceptance, so I can follow up proactively.

#### Acceptance Criteria

1. WHEN one or more quotations have StatusTypeId = 2 (Sent), THE briefing SHALL include an insight stating the count and total value of pending proposals.
2. THE insight SHALL link to the Quotations list (filtered to Sent status).
3. THIS signal SHALL have Priority 3.

### Requirement 4: Unassigned Purchases Signal

**User Story:** As a business user, I want to be reminded about purchases not yet assigned to a VAT period, so I don't miss them during VAT submission.

#### Acceptance Criteria

1. WHEN one or more non-cancelled purchases have no VatSubmissionPeriodId, THE briefing SHALL include an insight stating the count.
2. THE insight SHALL link to the Purchase list (filtered to unassigned).
3. THIS signal SHALL have Priority 4.

### Requirement 5: Upcoming Payment Schedule Instalments

**User Story:** As a business user, I want to be alerted about payment schedule instalments due this week, so I can expect and track incoming payments.

#### Acceptance Criteria

1. WHEN one or more payment schedule instalments are due within the next 7 days, THE briefing SHALL include an insight stating the count and total expected amount.
2. THE insight SHALL link to the Payment Schedules page.
3. THIS signal SHALL have Priority 5.

### Requirement 6: Payment Reminders Due Today

**User Story:** As a business user, I want to know if automated payment reminders are scheduled for today, so I'm aware of outgoing communications.

#### Acceptance Criteria

1. WHEN one or more payment reminders are scheduled to send today, THE briefing SHALL include an insight stating the count.
2. THE insight SHALL link to the Upcoming Reminders page.
3. THIS signal SHALL have Priority 6.

### Requirement 7: Draft Invoices Signal

**User Story:** As a business user, I want to be reminded about invoices sitting in Draft status, so I don't forget to issue them.

#### Acceptance Criteria

1. WHEN one or more invoices have StatusTypeId = 1 (Draft) and were created more than 3 days ago, THE briefing SHALL include an insight stating the count.
2. THE insight SHALL link to the Invoice list (filtered to Draft).
3. THIS signal SHALL have Priority 7.

### Requirement 8: Cash Flow Forecast Signal

**User Story:** As a business user, I want a quick cash flow outlook in the briefing, so I know if the next 30 days look healthy.

#### Acceptance Criteria

1. WHEN the user has cash flow access, THE briefing SHALL include an insight summarizing the 30-day cash flow outlook (positive or negative).
2. IF the 30-day projection is negative, THE insight SHALL use a warning tone and link to the Cash Flow page.
3. IF the 30-day projection is positive, THE insight SHALL briefly confirm: "Cash flow looks healthy for the next 30 days."
4. THIS signal SHALL have Priority 8 (informational).

### Requirement 9: Positive Reinforcement

**User Story:** As a business user, I want the briefing to acknowledge when things are going well, so the dashboard isn't only about problems.

#### Acceptance Criteria

1. WHEN all invoices are paid and no overdue items exist, THE briefing MAY include a positive sentence: "All invoices are up to date — great work."
2. WHEN a recent payment was received (last 24 hours), THE briefing MAY include: "A payment of €X was received yesterday from {CustomerName}."
3. POSITIVE insights SHALL appear after urgent and action items (lowest priority).

### Requirement 10: Briefing Sentence Formatting

**User Story:** As a developer, I want consistent formatting rules for briefing sentences, so the tone is professional and the output is predictable.

#### Acceptance Criteria

1. EACH insight SHALL be a single sentence or short phrase (max ~120 characters).
2. KEY values (amounts, counts, names) SHALL be wrapped in `<strong>` tags for emphasis.
3. EACH insight with an actionable destination SHALL include an inline link styled as blue text.
4. AMOUNTS SHALL be formatted with the business's configured currency symbol and 2 decimal places.
5. THE overall briefing SHALL contain a maximum of 6 insights — excess signals are dropped by priority.
6. INSIGHTS SHALL be separated by a period and a space when rendered as flowing text, OR as individual lines/bullets depending on design.

### Requirement 11: Permission-Aware Signals

**User Story:** As a business user with limited module access, I want the briefing to only mention things I have permission to see, so I'm not confused by references to hidden features.

#### Acceptance Criteria

1. THE briefing service SHALL check the user's module permissions before including each signal.
2. IF a user does not have access to the Revenue module, THE overdue invoices signal SHALL be omitted.
3. IF a user does not have access to the Quotation module, THE pending proposals signal SHALL be omitted.
4. IF a user does not have access to the Purchase module, THE unassigned purchases signal SHALL be omitted.
5. IF a user does not have access to the Cash Flow module, THE cash flow forecast signal SHALL be omitted.
