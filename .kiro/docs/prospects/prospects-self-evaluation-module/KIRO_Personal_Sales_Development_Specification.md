# KIRO Implementation Specification
## Personal Sales Development — Call Reflection

**Product:** 3 Inventors Business Portal  
**Module:** Opportunities → Prospecting → Prospect Activity  
**Status:** Proposed V1 implementation specification  
**Audience:** KIRO / engineering and product review  
**Primary objective:** Improve the founder's communication skills through structured, private reflection without conflating personal performance with prospect conversion.

---

## 1. Product intent and boundaries

The existing Portal already records prospect identity, research, campaign-specific qualification, activity history, status and conversion to Lead. The screenshot of **Cubix Minds Events** shows an Activity Timeline with calls, outcomes, next actions and a separate qualification panel.

**Do not rebuild those capabilities.** Introduce an optional personal-learning layer attached to eligible prospect activities.

- **Prospect Activity:** What happened commercially, with whom, and what happens next.
- **Call Reflection / Self-Evaluation:** How the user approached the interaction, what they observed about their own communication, and what to practise next.
- **Commercial outcome:** A separate attribute of the activity (e.g., no interest, follow-up, demo booked), not a proxy for communication quality.

A call can be executed well and produce no demo. A weakly executed call can still produce a demo. Never label either as a personal success or failure solely from conversion.

**Core product principle:** Adapt the *communication method* to observed circumstances, not the truthfulness of claims or the user's integrity. Do not diagnose or assign permanent personality types to prospects.

---

## 2. Section 1 — Call Reflection (primary feature)

### Entry points

On the existing **Prospect Activity** timeline, add an **Evaluate** action beside eligible completed Call / Meeting / Demo activities. If an evaluation already exists, show **View / Edit Reflection**. Optionally expose the action in the activity detail screen.

Unanswered calls need not be evaluated; the action can remain available but never mandatory. Prospect activities must continue to save normally without an evaluation.

### Form design

Header: **Call Reflection**; subtitle: **Personal development · Private evaluation**. Show prospect, activity date, activity type, and outcome as read-only context.

Six self-assessment criteria, each with a 1–5 rating:

| Criterion | Reflective question |
|---|---|
| Opening & credibility | Did I introduce myself naturally and establish relevance? |
| Listening & discovery | Did I understand their situation before presenting? |
| Adaptability | Did I adjust to the person's communication style and circumstances? |
| Clarity & relevance | Did I explain benefits in terms of their work? |
| Conversation control | Did I keep the discussion focused without dominating it? |
| Next-step agreement | Did I seek or confirm a clear, appropriate next action? |

Scale guidance: **1 = needs significant improvement; 2 = inconsistent; 3 = adequate; 4 = effective; 5 = particularly effective**. Provide a **Not assessed** state for any criterion that could not reasonably be observed (e.g., no opportunity to discuss next steps). Do not default all ratings to 3 in a persisted evaluation: blank means not yet assessed.

**Average:** arithmetic mean of rated criteria only; display with one decimal and the number rated (e.g., `4.0 / 5 · 5 of 6 assessed`). If none rated, show `Not scored`, not 0. Store individual ratings as the source of truth; compute the average in application logic (or consistently derived at query time).

Free-text prompts:
1. **What did I notice about this conversation?**
2. **What did I do effectively?**
3. **What will I do differently next time?** (prefer one concrete behaviour)

The last prompt is the most actionable. Allow drafts and partial evaluations; no ratings should be mandatory merely to save notes. Provide Save, Cancel, and optionally Delete with confirmation. Show created/updated metadata.

### UX constraints

- Compact, consistent with existing Portal card, border, typography and button patterns.
- Responsive desktop/tablet/mobile; keyboard accessible controls, visible focus and descriptive labels.
- The rating control must not rely on colour alone.
- No celebratory red/green performance grading or punitive labels.
- Distinguish a self-assessment from an objective quality audit.
- Do not create or modify Leads/Contacts when saving reflections.

---

## 3. Section 2 — Communication Adaptability Tips (guidance, not profiling)

Implement a **Tips** panel or expandable drawer alongside the reflection form, not a scored section. It should be helpful during pre-call preparation and post-call review, without requiring the user to categorise the prospect.

| Observed circumstance | Suggested adjustment |
|---|---|
| Busy / direct | Short opening, immediate relevance, one question |
| Analytical / technical questions | Explain the mechanism and show concrete evidence |
| Experienced / sceptical | Respect established processes; explore gaps without assuming failure |
| Friendly / conversational | Allow natural rapport while keeping an objective |
| Price-focused | Answer cost clearly; relate it to value without evasion |
| Distracted / driving | Avoid lengthy explanations; offer a convenient follow-up |

Include a short reminder: **“Adapt your communication style, not your integrity or claims.”**

**V1 implementation:** static, curated guidance stored as application content/configuration, optionally filtered by an *observed situation* selected by the user for that interaction. If selected, the observation is transient or stored as a contextual note—not a permanent prospect personality label. No AI analysis or inferred personality classification in V1.

Potential contextual micro-tips:
- **Opening:** “I'm the developer behind Chaplin Pro. May I ask one quick question about how your team coordinates client decisions?”
- **Discovery:** “How does updated client information reach everyone involved?”
- **Demo request:** “In 20 minutes, you can see a real workflow and decide whether it fits your existing process.”
- **Follow-up:** “You suggested I reconnect this week; is there a convenient time for the demonstration?”

Do not present phrases as scripts the user must repeat verbatim.

---

## 4. Section 3 — Separate execution quality from commercial outcome

**Do not calculate self-evaluation ratings from:** prospect priority, ICP score, demo bookings, lead conversions, sales value, or prospect sentiment.

Maintain two distinct data streams:

| Commercial record (existing Activity/Lead) | Personal learning record (new) |
|---|---|
| Contacted / no answer / engaged | Opening & credibility rating |
| Follow-up scheduled | Listening & discovery rating |
| Demo booked / completed | Adaptability and clarity ratings |
| Lead / Opportunity conversion | Conversation control and next-step rating |
| Prospect objections and requirements | Personal observations and improvement action |

Illustrative contrast (not real data):
- **Call A:** 4.7/5 self-assessment, no demo booked — potentially strong communication despite no conversion.
- **Call B:** 2.5/5 self-assessment, demo booked — commercially advanced despite weak execution.

The dashboard may display commercial outcomes *beside* reflection trends to support analysis, but must not collapse them into a single composite score or imply causality.

---

## 5. Section 4 — Personal Development Dashboard

Add a personal view accessible from **Prospect Activity → My Development** or a comparable Portal navigation location. Keep it separate from sales performance/Lead Board insights.

### V1 widgets

1. **Evaluations completed:** number and date range.
2. **Average by criterion:** current selected period, with count of rated observations.
3. **Trend chart:** weekly/monthly averages for the six criteria (only if sufficient data); clearly mark self-reported values.
4. **Recurring lessons:** recent improvement goals and user-entered reflections.
5. **Weekly practice focus:** one chosen communication behaviour to practise.
6. **Review queue (optional):** recent eligible activities without evaluations; do not nag on unanswered calls.

Suggested filters: date range, activity type, product/campaign, and optionally prospect segment. Respect user/tenant authorisation at every query boundary.

### Analytical safeguards

- Display sample sizes; don't over-interpret trends from two or three calls.
- A higher average is not automatically proof of better real-world outcomes.
- Never infer that a demo was caused by a high reflection score.
- Avoid ranking employees against one another; this is a private coaching tool, not a management surveillance feature.
- V1 can use simple aggregation; no ML/AI scoring required.

**Example only:** Week 1 Listening 2.5/5; Week 4 Listening 4.0/5. This is illustrative UI data, not a real measured improvement.

---

## 6. Section 5 — Integration and technical architecture

### Existing Portal context

The prospect detail currently has an Activity Timeline with **Add Activity** and per-entry **Edit**, alongside qualification and research panels. Keep the current prospect/contact/lead distinction. Recording an activity or evaluation must never automatically create a Lead or Contact.

### Proposed domain model

Use names that fit existing code conventions. Conceptual model:

```text
Tenant
  └── Prospect
        └── ProspectActivity (existing)
              └── ActivitySelfEvaluation (new; zero or one per user per activity)
                    ├── CriterionRatings
                    ├── Observations / Strengths / Improvement
                    ├── PreCallIntention (optional)
                    └── Audit metadata
```

Suggested entity fields:

| Field | Type / rule |
|---|---|
| `Id` | Existing Portal ID convention |
| `TenantId` | Required; enforce tenant isolation |
| `ProspectActivityId` | Required FK to eligible activity |
| `EvaluatorUserId` | Required; current authenticated user |
| `OpeningCredibility` | Nullable smallint, 1–5 |
| `ListeningDiscovery` | Nullable smallint, 1–5 |
| `Adaptability` | Nullable smallint, 1–5 |
| `ClarityRelevance` | Nullable smallint, 1–5 |
| `ConversationControl` | Nullable smallint, 1–5 |
| `NextStepAgreement` | Nullable smallint, 1–5 |
| `Observations` | Nullable text, reasonable max length |
| `Strengths` | Nullable text |
| `NextImprovement` | Nullable text |
| `PreCallObjective` | Nullable text |
| `PreCallFocus` | Nullable text |
| `CreatedAtUtc`, `UpdatedAtUtc` | Required audit timestamps |
| `DeletedAtUtc` | Optional soft delete if existing convention |

Enforce unique `(TenantId, ProspectActivityId, EvaluatorUserId)` for V1. This permits one private reflection per evaluator/activity and avoids accidental duplicates. If only the activity owner can evaluate, restrict further according to existing authorisation policy. Prefer retaining history after Prospect → Lead conversion; do not copy records into Lead tables.

**Important:** If pre-call intentions must be recorded *before an activity exists*, either (a) create a planned activity using the existing scheduling model and attach the intention, or (b) introduce a minimal `PreCallPlan` linked to the planned task/campaign entry. Do not force users to fabricate a completed call merely to record preparation.

### Service/API responsibilities (illustrative, adapt to existing MVC conventions)

- `GET /prospect-activities/{activityId}/self-evaluation`
- `PUT /prospect-activities/{activityId}/self-evaluation` (create/update idempotently)
- `DELETE /prospect-activities/{activityId}/self-evaluation`
- `GET /my-development?from=...&to=...`

These are conceptual routes, not instructions to introduce a new API style if the Portal uses MVC controllers/services.

Service must:
- Check authenticated user, tenant, activity existence, and permission to evaluate/view.
- Verify activity type and relationship to the accessible Prospect.
- Enforce 1–5/null ratings and length limits server-side.
- Calculate aggregates from visible authorised records only.
- Preserve evaluation across prospect conversion.
- Record standard audit metadata.
- Avoid exposing private evaluation notes in shared customer/lead views, external links, exports, general prospect activity feeds, or notifications.

### Privacy and access

**Author-only by default.** Even administrators/managers should not automatically see private reflections unless a separately reviewed access policy explicitly grants access for defined purposes. Follow existing tenant security conventions and applicable data protection/retention policies. Document retention and deletion behavior; avoid storing unnecessary sensitive descriptions about prospects.

This feature is a personal learning tool, **not** a hidden employee-monitoring mechanism.

### Data migrations and compatibility

- Add a new table; do not alter existing Activity outcome semantics.
- No backfill or fabricated scores for historical calls.
- Existing Prospect/Lead workflows must remain unchanged.
- Index by tenant, evaluator, activity and creation date for dashboard queries.
- Use existing EF Core migration style, optimistic concurrency or equivalent if used by Portal.

---

## 7. Section 6 — Pre-call intention and post-call learning loop

### Before a meaningful call

Allow the user to record:
- **Commercial objective:** e.g., “Understand the event company's process and propose a relevant demonstration.”
- **Personal practice focus:** e.g., “Ask one discovery question before explaining features.”

Optional; no mandatory prep for quick calls.

### After the call

Present the pre-call intention alongside the reflection and ask:
1. Did I act consistently with my intended approach?
2. What did the prospect reveal?
3. What did I do effectively?
4. What one behaviour will I change next time?

A weekly review should surface the most recent practice focus and allow a new focus to be selected. Avoid gamification that rewards volume over thoughtful conversations.

### Learning loop

```text
Set intention → Have conversation → Record commercial activity
      → Reflect on own execution → Identify one improvement
      → Practise on next suitable interaction → Weekly review
```

---

## 8. UI layout proposal

### Prospect detail — existing page

- Preserve existing **Activity Timeline** layout.
- For eligible completed activities, add `Evaluate` / `View Reflection` near `Edit`.
- Small private indicator (e.g., lock icon) after evaluation; never reveal score in shared/commercial activity summaries.
- Clicking opens a right-side drawer or dedicated page consistent with Portal styling.

### Reflection drawer/page

1. Activity context and privacy note.
2. Pre-call objective/focus (if recorded).
3. Six criteria with 1–5 rating and `Not assessed`.
4. Average with assessed count.
5. Observations, strengths, next improvement.
6. Collapsible **Communication Tips** panel.
7. Save/Cancel actions.

### Personal Development page

- Time filters and criterion trends.
- Weekly focus and qualitative notes.
- Link back to the original activity (subject to permissions).
- Clear separation from sales pipeline and conversion reports.

---

## 9. Implementation order

**Phase A — Minimum viable reflection**
1. Inspect current Activity entities, services, controllers, views, auth and audit conventions.
2. Add private evaluation entity/migration.
3. Add Evaluate/View Reflection action and form.
4. Implement save/update, partial ratings, computed average and privacy checks.
5. Add static Communication Tips panel.

**Phase B — Intentional practice**
1. Pre-call intention tied to planned activities/tasks.
2. Surface prior intention in post-call reflection.
3. Add weekly practice focus.

**Phase C — Personal insights**
1. Private dashboard with trends/counts and date filters.
2. Recent lessons and weekly focus review.
3. Consider later improvements only after real use.

Do not delay Phase A waiting for a sophisticated dashboard.

---

## 10. Acceptance criteria

1. A user can add an ordinary Prospect Activity without creating a Lead or evaluation.
2. An eligible activity exposes **Evaluate**; existing evaluation exposes **View/Edit**.
3. The evaluation saves with zero, some or all criteria rated; null is distinct from a score of 1.
4. Every non-null rating must be an integer from 1 to 5.
5. Average uses only rated criteria and shows the assessed count; zero ratings shows “Not scored.”
6. Personal notes and improvement goals persist and can be edited by the author.
7. Other users in the same tenant cannot read another author's reflection by guessing IDs or using list/dashboard endpoints.
8. Other tenants cannot access the reflection under any circumstances.
9. Evaluation never changes commercial outcome, prospect status, ICP score, or Lead conversion.
10. Conversion from Prospect to Lead preserves the original activity and reflection association.
11. Tips are clearly presented as guidance, not prospect classification or scored criteria.
12. The user can capture a pre-call objective/focus without falsely recording a completed call.
13. Dashboard excludes unauthorised evaluations, labels data as self-reported, and shows sample sizes.
14. Existing Prospect Activity and Lead workflows pass regression tests.
15. Keyboard and mobile interaction are usable; rating selections have accessible names and focus states.
16. Delete/retention behaviour follows documented Portal policy; personal reflections are not accidentally included in customer-facing exports.

---

## 11. Test cases for KIRO

- Create partial evaluation with only two scores; verify average and count.
- Save notes-only evaluation; verify “Not scored.”
- Attempt rating 0, 6, decimal, and nonnumeric; reject server-side.
- Attempt duplicate evaluation for same activity/evaluator; update or reject safely, never duplicate.
- Attempt cross-tenant read/write and same-tenant other-user read; deny.
- Attempt evaluate deleted/inaccessible/noneligible activity; handle safely.
- Convert Prospect to Lead; verify historical reflection remains attached and private.
- Change an activity's commercial outcome; verify self-rating unchanged.
- Query dashboard with no evaluations, sparse weeks, and date filters.
- Test accessibility of rating controls, keyboard navigation and responsive layout.

---

## 12. KIRO execution instruction

**Before coding:** inspect the actual Portal implementation and report conflicts between this specification and the current domain model. Preserve the existing Prospect, Activity, Contact, Lead, Opportunity, identity, tenancy, authorisation, audit and UI conventions. Do not invent duplicate entities. Implement the smallest coherent version that can be used immediately by the founder during the Chaplin Pro prospecting campaign.

**Deliver:** migrations, domain/service changes, UI, privacy enforcement, validation, automated tests, and a short implementation note covering deviations and unresolved decisions.

### Final product statement

> **Prospect Activity captures what happened. Call Reflection captures what I learned about how I communicated. The next call is where that learning becomes improvement.**
