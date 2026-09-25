# Future Features — Prospects & Campaigns

> **Status:** Deferred / design-capture. None of the items below are in the V1 build. They are
> recorded here so the reasoning isn't lost and so we can pick them up deliberately when a real
> need emerges — not speculatively.
>
> **Related:** V1 spec at `.kiro/specs/prospects-campaigns/` (requirements → design → tasks).
> Visual reference at `.kiro/docs/mockups/prospects-campaigns.html`.

---

## 1. `ProspectCampaignEntry` — one organisation across multiple campaigns

### What it is

A join table between Campaign and Prospect:

```
Campaign  →  ProspectCampaignEntry  →  Prospect
```

- **Prospect** would hold the organisation's stable *identity* (name, website, phone, email).
- **ProspectCampaignEntry** would hold the *campaign-specific* data: the four-dimension score,
  the computed priority, the fit rationale, and the prospect's status **within that campaign**.

### The problem it solves

The same organisation can be relevant to **different products in different campaigns**, with a
different score each time. Example — EasyConferences:

- In a **Chaplin Pro** campaign, scored 18/20 (strong fit for the collaboration layer).
- Later, in a **Portal HORECA** campaign, the same organisation scores 9/20 (weak fit).

With the join table, EasyConferences is **one Prospect record** linked to **two campaigns**, each
link row carrying its own score/priority/status. No duplication of the organisation, and
re-scoring it for a new product does not corrupt its identity.

### Why it is deferred (not in V1)

The V1 need is "simple enough to replace the prospecting spreadsheet." That spreadsheet is a
single campaign — 30 prospects, scored once. The join table would add:

- an extra table,
- a join in every prospect query,
- extra UI complexity,

…to solve a problem that does not exist yet. So V1 **folds the score, priority, and status
directly onto the Prospect**, and a Prospect belongs to exactly one Campaign.

### The tradeoff V1 accepts

If we later re-target the **same** organisation in a **new** campaign, the V1 model requires a
**second Prospect record** for it (e.g. "EasyConferences" appears once under Chaplin Pro and
again under HORECA). Mild, harmless duplication at 3 Inventors' scale.

### When to build it

Add the join table only when overlapping campaigns hitting the **same** organisations become a
real, recurring need. It is a clean, reversible seam: migrate the per-campaign fields
(scores, priority, status, why-fit) off `Prospect` into a new `ProspectCampaignEntry`, and repoint
the UI/queries. Not speculative work — do it when the evidence appears.

---

## 2. Prospecting Assistant (weekly cold-call objective)

A scheduled/scan-type digital assistant that each week generates the campaign's cold-call
objective (target calls, Priority-A prospects ready, follow-ups due) from the campaign target
and prospect statuses — replacing the static panel V1 ships.

- **Deferred deliberately:** build it after the module is used and tested, so we know exactly
  what the objective should say. Guessing the assistant's behaviour before the module has real
  usage would bake in the wrong rules.
- **Seam already in V1:** `ProspectCampaignService.GetWeeklyObjectiveAsync` returns the static
  figures today; the assistant later enriches this same seam. Built on the existing
  digital-assistants framework (`.kiro/docs/features/digital-assistants-catalog.md`).

---

## 3. Scoring-performance report (predicted vs. actual)

A report/visualisation comparing each prospect's Priority/Score against whether it actually
converted — answering "does the qualification scoring predict conversion, or is it a false
indicator?"

- **Data is already captured in V1:** the `Prospect.ConvertedLeadRequestId` back-reference set
  on conversion is exactly the link this report needs. Only the report/visualisation is deferred.
- A fast follow-on once there are enough conversions to be meaningful.

---

## 4. Per-business Prospect display number

A human-friendly per-business sequential number for prospects (like `LeadRequest.LeadNumber`),
so the global DB id is never shown. Nice-to-have; skipped in V1 to keep scope tight. The atomic
`MAX+1` HOLDLOCK/UPDLOCK pattern from `LeadRequestRepository.InsertAsync` is the template if we
add it.

---

## How to use this document

When picking up any item here, promote it into its own spec (or extend the existing
`prospects-campaigns` spec) — requirements → design → tasks — rather than building directly from
this note. This is the map of what we deliberately left out and why, not a build plan.
