# Inventory Intelligence — Supply Mapping, BOM, Invoice Capture & Margin Drift

> **Status:** Living roadmap / design-capture document. Nothing here is built yet. This
> document defines the vision, the domain model, the phased delivery plan, the honest risks,
> and the pricing revision (Enterprise tier strengthening) that this capability drives.
>
> **Audience:** product + engineering. Each phase, when picked up, gets its own spec
> (requirements → design → tasks). This document is the map, not the build plan.
>
> **One-line pitch:** turn the hours a hospitality manager spends typing supplier invoices —
> and the invisible margin erosion that hides in those invoices — into a photo, a confirmation,
> and a daily "these items should change price" report.

---

## 1. The problem we are solving

Hospitality operators (cafes, kiosks, small production lines, franchises) spend **hundreds of
hours** on a task that is pure mechanical transcription: taking the paper invoices their
suppliers hand them and re-typing every line into a system. It is slow, it is boring, and —
critically — **it is where money quietly leaks**.

Two failures compound:

1. **Transcription cost.** A 30-line delivery invoice can take 10–15 minutes to key in
   correctly. A busy kiosk receives several of these a day. Multiply across a month and it is
   a part-time job nobody wants.

2. **Silent margin drift.** A supplier raises the price of a coffee bag by a few cents. Nobody
   notices, because nobody compares this invoice to the last one. The sellable product's cost
   base has moved, the retail price has not, and the margin has eroded — invisibly, across
   hundreds of items. By the time it is felt in the P&L, months have passed. **This is the
   expensive problem.** Recording the invoice is the chore; catching the drift is the value.

The manager's real question is not "did I type this invoice correctly?" It is **"which of my
products are now costing me more than I think, and what should I do about it?"** Today no tool
in their stack answers that. That is the bet.

---

## 2. What we are building (named precisely)

This is not one feature. It is a stack of four capabilities that people casually lump together
as "photograph an invoice." They differ enormously in difficulty and risk, and the order in
which we build them is deliberately the **reverse** of the order they run at runtime.

| # | Capability | What it is | Difficulty | Risk |
|---|------------|-----------|------------|------|
| 1 | **Supply ↔ Product mapping** (self-learning) | Link a supplier's invoice line (barcode/SKU/text) to a platform product; remember it forever. A product can have **many** supplier barcodes. | Medium (data model) | Low |
| 2 | **BOM / recipe layer** | Define what a sellable product consumes: 1-to-1 for resale, or a recipe (200 g milk, 18 g coffee…) for prepared items. Enables true cost. | Medium (we have the know-how) | Medium |
| 3 | **Cost + price-history + margin-drift report** | Every confirmed purchase line writes a supply price-history point; the daily report flags products whose cost rose and margin thinned. | Medium | **Medium-High** (financial correctness) |
| 4 | **Guided photo capture + OCR extraction** | Photograph the paper invoice; the system guides the shot, extracts lines, and **pre-fills** a purchase for human confirmation. | High (accuracy) | Medium |

The moat is **1 + 2 + 3** — the mapping memory, the recipe-based costing, and the drift report.
The OCR (4) is a commodity accelerator bought from a provider. It is the demo wow-moment and
the cheapest part to add, which is exactly why it is built **last**, on top of a system that
already works by hand.

### 2.1 The runtime flow (once everything is built)

```
Supplier hands over a paper invoice
        │
        ▼
Manager photographs it (system guides the shot: angle, lighting, full page)
        │
        ▼
OCR extraction  →  structured lines (supplier, barcode/SKU, description, qty, unit price, VAT)
        │
        ▼
Pre-filled purchase draft  ──►  MANAGER CONFIRMS AGAINST THE PAPER  ◄── (mandatory, always)
        │                         (amounts justified line-by-line)
        ▼
On confirm, atomically:
   • the Purchase + lines are saved (existing purchasing system)
   • each line's supply price-history point is written
   • stock movements are applied (via the BOM/mapping)
        │
        ▼
End of day  →  Margin-Drift Report: "these N products cost more now — consider repricing"
```

### 2.2 The non-negotiable principle: **the photo is an accelerator, never an authority**

Confirmed explicitly with the product owner:

> "The OCR for invoices ALWAYS needs human intervention. The photo-to-invoice just saves time
> transferring from paper to the platform. The manager should always justify the auto-generated
> invoice with the paper, then confirm the amounts. Then the inventory and the price history
> will be updated."

This is the architectural spine of the whole feature, not a UX nicety:

- OCR output is **provisional** — it populates a draft, it never silently mutates stock or
  price history.
- Stock movements and price-history points are written **only on human confirmation**, inside
  the same transaction as the purchase save (so a rolled-back purchase leaves no phantom stock
  or price point).
- A wrong financial recommendation is **worse** than no feature. €1.20 misread as €1.28 on a
  supply feeding 400 coffees produces a wrong "reprice" report. Human confirmation at the
  cents level is the guardrail that makes the drift report trustworthy.

---

## 3. Domain model (to be designed together — this is the shape, not the final schema)

> The product owner has prior ERP experience with BOM/recipes (a tested WPF ERP, not yet
> promoted due to WPF deployment friction) — so the modeling is **known territory**, and the
> priority is to reuse that hard-won UX and "what to avoid," not to rediscover it.

### 3.1 New concepts

- **Supply** — a thing a business *buys* from a supplier. Distinct from a **Product** (a thing
  a business *sells*). A bag of coffee beans is a Supply; an espresso is a Product.
- **Supply Barcode / Supplier SKU** — the identifier a supplier prints on their invoice line.
  **A Product (or Supply) can have many** of these, because different suppliers label the same
  thing differently. This many-to-one mapping is the self-learning memory.
- **Unit of Measure + pack conversion** — a Supply is bought in a **pack** (e.g. 1 kg bag, 24 ×
  330 ml case) but consumed in a **base unit** (g, ml, each). Every Supply needs a
  pack → base-unit conversion. This is the silent bug factory if done loosely, so it is modeled
  explicitly.
- **BOM / Recipe** — for a Product, the list of Supplies it consumes and how much of each, in
  base units. **1-to-1** for pure resale (bottled water: 1 Product = 1 Supply, 1 each).
  **N-line recipe** for prepared items (cappuccino = 1 cup + 200 g milk + 18 g coffee + 2 g
  cinnamon).
- **Supply Price History** — a time series of what each Supply actually cost, one point per
  confirmed purchase line. The raw material of margin drift.
- **Stock / on-hand** — quantity of each Supply currently held, moved by confirmed purchase
  lines (increments) and by sales via BOM consumption (decrements — see POS ingestion, §6).

### 3.2 The BOM designer UX (as described by the product owner — keep it this simple)

> "Choose a product → enter the BOM designer → choose the 'Ingredients' and the unit of measure
> (units/g/kg, etc.) → choose the supply. Done. Water is mapped 1-to-1. A cappuccino is 1 cup,
> 200 g milk, 18 g coffee, 2 g cinnamon."

Deliberate design stances:

- **Simplicity is a feature.** No multi-level nested sub-assemblies for v1. A Product's recipe
  references Supplies directly. (Sub-recipes — e.g. a "milk foam" intermediate — can come
  later if demand proves it; not v1.)
- **Small deviation from the true recipe is acceptable and expected.** The product owner is
  explicit: "we will always deviate slightly from the actual recipe, but it is minimal." The
  goal is *decision-useful* cost accuracy, not laboratory precision. This keeps the UX fast.
- **Accuracy still matters for the decision.** "We want to be accurate; otherwise, if we sell
  something cheap but inefficient, what is the point? I need the cost analysis." So: minimal
  data entry, but the numbers that drive the reprice decision must be right.

### 3.3 What already exists that this builds on (why this is feasible *here*)

The platform is **not** greenfield for this. Verified against the existing pricing/module doc
and codebase touchpoints seen so far:

- **Product Catalog** exists (Foundation), including **named price tiers** (Retail / Wholesale
  / VIP) — the sell-side price that margin drift compares cost against.
- **Purchase Management** exists (Foundation): record, categorise, VAT-assign, with invoice
  lines and totals, plus **supplier due dates** (from the supplier-payment-due-dates spec).
- **Supplier Registry** exists (Foundation) — the counterpart to the self-learning supply
  mapping.
- **Purchase Import** and **POS/Sales import** paths exist (Professional) — proof the platform
  already ingests bulk line data, which the POS sales ingestion (§6) extends.

So the work is: **add a Supply/Stock/BOM/PriceHistory layer, and connect supply lines to
products**, on top of a purchasing system that already books invoices and VAT correctly. Not a
rebuild — a layer.

---

## 4. Phased delivery plan (build the hard part once; prove value before the camera)

> Practice confirmed with the product owner: **each phase must pass real UX tests before the
> next begins — even if the customer rushes.** Phases are ordered so each is independently
> valuable and de-risks the next.

### Phase 1 — Supply ↔ Product mapping + self-learning memory + multi-barcode

**Goal:** manual invoice entry gets faster and starts building the mapping memory. No camera,
no BOM yet.

- Introduce the **Supply** concept and the **many barcodes → one Supply/Product** mapping.
- When recording a purchase line, resolve the supplier's line to a known Supply by
  barcode/SKU/text; if unknown, the manager maps it once and it is **remembered** for next time.
- Pack → base-unit conversion captured per Supply.

**Why first:** lowest risk, immediate time savings, and it is the data spine everything else
rides on. The second invoice from a supplier becomes near-zero-effort — the core promise.

**UX gate:** a returning supplier's invoice recognises its lines automatically.

### Phase 2 — Cost, supply price-history & the **Margin-Drift Report** (the crown jewel)

**Goal:** ship the outcome the manager brags about — **before** the camera exists.

- Every confirmed purchase line writes a **Supply price-history point**.
- Compute product cost from the BOM (Phase 3 makes this rich; Phase 2 can start with 1-to-1
  resale costing and pack conversion).
- **Daily Margin-Drift Report:** "these products cost more than they did — margin has thinned —
  consider repricing," comparing latest confirmed supply cost against the product's active
  price tier(s).

**Why before the camera:** it proves the paid-for value using data the manager already trusts
(hand-confirmed purchases). The report is the reason to subscribe; the photo is how it gets
faster.

**UX gate:** a manager, shown the report after a week of normal purchasing, agrees it caught a
real price rise they would have missed.

### Phase 3 — BOM / recipe layer + full recipe-based costing

**Goal:** true cost for prepared items (the cafe/production case), not just resale.

- The **BOM designer** (§3.2): choose product → ingredients → unit → supply.
- Product cost = Σ (recipe quantity × supply base-unit cost). Margin drift now works for a
  cappuccino, not only a bottled drink.
- Optional: waste/yield allowance per recipe (kept minimal per the "small deviation is fine"
  stance).

**Why here:** it is the hardest *modeling*, but the product owner already has the know-how, so
it is deliberate rather than exploratory. It deepens the drift report from resale-only to
prepared-item costing.

**UX gate:** define a cappuccino recipe in under a minute; its computed cost matches the
operator's mental math within an acceptable margin.

### Phase 4 — Guided photo capture + OCR extraction → pre-filled confirmation

**Goal:** the accelerator. Make the now-proven workflow fast.

- **Guided capture** (angle, lighting, full-page framing) to maximise extraction quality.
- OCR/provider extracts lines → **pre-fills** a purchase draft.
- Manager **confirms against the paper** (mandatory), corrects, confirms → the existing
  Phase 1–3 machinery (mapping, stock, price history) runs on confirmation.

**Why last:** the photo is the cheapest part to add and the riskiest to lead with. Adding it
on top of a system that already works by hand means a bad extraction degrades to "manual entry"
— never to "wrong stock and a wrong reprice email." This is the difference between magical and
dangerous.

**UX gate:** a real crumpled thermal receipt, photographed in kiosk lighting, produces a draft
the manager confirms faster than typing — and mis-reads are caught at confirmation, never
silently booked.

---

## 5. Honest risk register (so we go in clear-eyed)

| Risk | Why it matters | Mitigation |
|------|----------------|------------|
| **Extraction accuracy on poor receipts** | Thermal paper, bad light, odd angles will never be 100%. | Guided capture; **mandatory** human confirm; degrade to manual entry, never to silent booking. |
| **Cents-level OCR error → wrong reprice advice** | A wrong financial recommendation is worse than none. | Provisional-until-confirmed principle (§2.2); confirm against paper. |
| **Unit / pack / VAT conversion bugs** | "24 × 330 ml case" vs "each," gross vs net line, mixed-VAT invoices — quiet financial errors hide here. | Model pack→base conversion explicitly; reuse the existing VAT-correct purchasing path; validate conversions at confirm. |
| **Stock drift over time** | On-hand diverges from reality if BOM/waste is off or spillage unmodeled; erodes trust. | Periodic **stock-count reconciliation** (not just invoice-driven increments); keep recipe deviation "minimal but honest." |
| **Scope creep into full ERP** | BOM/stock can balloon into multi-level manufacturing. | v1 stances: single-level recipes, no nested sub-assemblies, "small deviation acceptable." Defer depth until demand proves it. |
| **Provider cost / data residency (OCR)** | Per-page OCR cost and GDPR/data-residency vary by provider. | Provider choice is an explicit Phase 4 decision; OCR isolated behind an interface so it is swappable. |
| **Effort is multi-quarter** | This is not a sprint. | It is phaseable; Phases 1–2 deliver value before the heaviest work. |

---

## 6. Adjacent: POS Sales Ingestion (straightforward, complements this)

Noted by the product owner as **more straightforward** than the invoice/BOM work, and
complementary:

- Ingest **POS sales** (what was sold) so that, combined with the BOM, the platform can
  **decrement stock by consumption** (a sold cappuccino consumes 18 g coffee, 200 g milk…) and
  compute **realised margin** (what it actually cost to make vs what it sold for).
- The platform already has **Sales Invoice Import (POS data)**, **External Platform Sales
  Import**, and **Revenue Summary Entry (Z-Reports)** — POS sales ingestion for inventory is a
  natural extension of these existing line-ingestion paths, now feeding the **stock ledger**
  and the **realised-margin** side of the drift analysis rather than only VAT.
- **Sequencing:** POS ingestion becomes most valuable **after** Phase 3 (BOM exists, so sales
  can consume supplies). It can be specced in parallel but delivers its inventory value once the
  recipe layer is live.

This closes the loop: **purchases raise cost (in), sales consume stock (out), and the margin
report sees both sides.**

---

## 7. Pricing revision — strengthening the Enterprise tier

This capability is the strategic reason to **strengthen Enterprise**. It is heavyweight,
operationally deep, and squarely aimed at the businesses that already need scale (hospitality
groups, franchises, multi-site operators). It gives the Enterprise tier a second flagship
alongside Payroll — moving Enterprise from "unlimited users + integrations" to "the tier that
actually runs your operation."

### 7.1 Positioning

- **DECIDED: a paid Enterprise add-on** (not flat-included in the Enterprise base price).
  Rationale: it serves a *specific kind* of business (hospitality, kiosks, production lines),
  not every Enterprise customer. Charging every Enterprise subscriber — including
  services firms and agencies with no supplier invoices or recipes — would be unfair and would
  blur the tier. Pricing it as an opt-in add-on means the operators who get the margin value
  pay for it; others aren't taxed for a feature they'll never use. It also becomes an
  **incremental revenue line** on top of the base subscription rather than a reason to raise the
  base Enterprise price. (Concrete example that settled it: a customer may want **Payroll but
  not Inventory Intelligence** — the add-on model serves that cleanly.)
- **Requires Enterprise; never sold standalone.** Compute- and support-heavy (OCR cost, stock
  correctness, reconciliation), so it belongs on top of the highest tier.
- **Not Professional.** Professional's story is *automation of the sell-side pipeline*
  (remind → pay-link → record). Inventory Intelligence is *operational cost control* — a
  distinct, heavier value axis, delivered as an Enterprise add-on.
- **Price:** deferred — set from evidence when the module ships (likely a flat monthly add-on,
  possibly with a metered OCR-pages component). Base Enterprise price (€129/€169) is unchanged.

### 7.2 Proposed new module keys (to add to the model's Module Keys table)

| Key | Feature | Available From |
|-----|---------|----------------|
| `inventory` | Stock / on-hand tracking (supply ledger) | Enterprise |
| `supply_mapping` | Supply ↔ Product self-learning mapping (multi-barcode) | Enterprise |
| `bom` | BOM / Recipe designer + recipe-based product costing | Enterprise |
| `margin_drift` | Margin-Drift Report (cost vs price-tier erosion alerts) | Enterprise |
| `invoice_capture` | Guided photo capture + OCR invoice extraction (human-confirmed) | Enterprise |
| `pos_ingestion` | POS Sales Ingestion → stock consumption + realised margin | Enterprise |

> Grouped under a marketing umbrella — **"Inventory Intelligence"** — on the landing page and
> feature matrix, so buyers see one flagship capability, not six checkboxes.

### 7.3 Feature-matrix additions (Enterprise column)

A new **"Inventory Intelligence"** section in the Feature Distribution Matrix, all
❌ Foundation / ❌ Professional / ✅ Enterprise:

- Supply ↔ Product Mapping (self-learning, multi-barcode)
- BOM / Recipe Designer
- Stock / Inventory Tracking
- Supply Price History
- Margin-Drift Report (daily reprice recommendations)
- Invoice Photo Capture & OCR (human-confirmed)
- POS Sales Ingestion (consumption + realised margin)

### 7.4 Pricing impact — the Enterprise story gets its second pillar

The current model already anticipated this: Enterprise Early Access (€129/mo) transitions to
Full (€169/mo) "when all Phase 3 modules ship (Payroll, Multi-Currency, API)." Inventory
Intelligence **strengthens the justification** for the Full Enterprise price and can extend the
Early-Access → Full narrative:

- **Reinforces €169/mo Full Enterprise:** Payroll alone justified the jump; Inventory
  Intelligence makes it decisive. A hospitality operator replacing manual invoice entry +
  spreadsheet cost tracking + guesswork repricing is saving **many hours/week and catching
  margin leaks worth far more than the subscription**.
- **Value framing (to add to the Value Comparison table):** manual invoice transcription
  (hours/week) + no margin visibility (silent profit erosion) vs automated capture + a daily
  reprice report. This is the strongest ROI story in the whole platform — it protects *margin*,
  not just *time*.
- **Roadmap presentation:** present Inventory Intelligence as a **"Coming Soon"** paid Enterprise
  add-on in the Early Access card, so hospitality-oriented early adopters know it is on the way
  as an opt-in on top of their Enterprise subscription.

### 7.5 Edits applied to `Subscription_Tier_Model.md`

> ✅ **Applied (06 Sep 2026), as a paid Enterprise add-on** (Option A — the fairest model).
> Base Enterprise price (€129 early access / €169 full) is **unchanged**; Inventory Intelligence
> is incremental add-on revenue, not a base-price bump.

1. **New "Add-Ons (Enterprise)" tier section** — defines the Inventory Intelligence add-on: for
   whom, value proposition, included capabilities, "requires active Enterprise," price TBD.
2. **Enterprise tier definition** — added "Eligible for the Inventory Intelligence add-on" line
   (capabilities now live in the Add-Ons section, not the flat tier list).
3. **Feature Distribution Matrix** — added an "Inventory Intelligence *(Enterprise add-on)*"
   section with **➕ Add-on** markers (not flat ✅), plus a legend note explaining ➕.
4. **Module Keys table** — added the six keys, marked **"Enterprise add-on."**
5. **Enterprise Early Access → "What Enterprise Gets Next"** — added Inventory Intelligence as a
   **paid add-on** upcoming item (not an included feature).
6. **Add-On Pricing (Enterprise) table** under Pricing Summary — Inventory Intelligence price
   **TBD**, set from evidence at launch (likely flat monthly + possible metered OCR-pages).
7. **Pricing Rationale** — Payroll anchors the base Enterprise price; Inventory Intelligence is
   explained as a fair opt-in add-on and an incremental revenue line.

> **📌 Backlog — landing page (deferred):** Do **not** update the public landing page for
> Inventory Intelligence yet. Build the module first; update the landing page (Enterprise card,
> add-on presentation, feature list, "Coming Soon" → live badges, marketing copy, and the final
> add-on price) only **after** the module ships and passes its UX tests. Sequence:
> **develop → then landing page.**

---

## 8. Suggested delivery sequence (summary)

1. **Phase 1 — Supply ↔ Product mapping + multi-barcode + self-learning** (manual entry;
   builds the data spine and immediately speeds recording).
2. **Phase 2 — Cost + supply price-history + Margin-Drift Report** (ship the crown jewel on
   trusted, hand-confirmed data — before the camera).
3. **Phase 3 — BOM / recipe designer + recipe-based costing** (true cost for prepared items;
   deliberate modeling, known territory).
4. **Phase 4 — Guided photo capture + OCR extraction → confirm** (the accelerator, added last,
   on top of a proven manual workflow).
5. **POS Sales Ingestion** — specced in parallel; delivers inventory value once Phase 3 (BOM)
   is live, closing the in/out loop for realised margin.

The single most important stance this document captures: **the photo is an accelerator, never
an authority — stock and price history change only on human confirmation.** Build the mapping,
the recipe costing, and the drift report first (the moat), and add the camera last (the
commodity). That ordering is counterintuitive because the photo is the demo wow-moment, but it
is what makes the feature trustworthy enough to protect a manager's margins.

---

## 9. Open decisions to resolve at spec time

1. **Inventory model:** none exists today — it is designed from scratch, **together**, drawing
   on the product owner's ERP/BOM experience.
2. **BOM depth for v1:** single-level recipes referencing Supplies directly; **no nested
   sub-assemblies** for v1 (revisit only if demand proves it).
3. **OCR provider:** deferred to Phase 4; isolate behind an interface so it is swappable;
   evaluate accuracy, per-page cost, and GDPR/data-residency at that point.
4. **Stock reconciliation:** confirm whether v1 includes periodic stock-count reconciliation or
   defers it (recommended: at least a manual stock-count adjustment in the phase that introduces
   the stock ledger).
5. **Primary sell:** both invoice-recording speed **and** margin-drift are primary; the ordering
   above ships the margin report (value) before the camera (speed) deliberately.
6. **Enterprise vs a paid add-on:** ✅ **DECIDED — paid Enterprise add-on** (Option A). Base
   Enterprise price unchanged; Inventory Intelligence is opt-in on top. The **exact add-on
   price** (flat monthly vs flat + metered OCR pages) is the only piece still deferred — set
   from evidence when the module ships.

---

## 10. Where this fits with existing docs

- **Pricing / gating:** `.kiro/docs/Subscription_Tier_Model.md` — the six new module keys and
  the Enterprise strengthening in §7 are proposed against this doc.
- **Adjacent existing ingestion:** Purchase Import, Sales Invoice Import (POS), External
  Platform Sales Import, Revenue Summary Entry (Z-Reports) — all Professional today; POS Sales
  Ingestion (§6) extends these toward inventory.
- **Sibling roadmap:** `.kiro/docs/features/digital-assistants-catalog.md` — a separate track
  (built by another agent). The Margin-Drift Report could *later* be delivered as a Category B
  scheduled digest assistant, but that is an integration idea, not a dependency.

Each phase, when picked up, should get its own spec (requirements → design → tasks). This
document is the map, not the build plan.
