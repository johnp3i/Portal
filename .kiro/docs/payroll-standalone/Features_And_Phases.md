# Payroll Standalone & Ecosystem — Features & Phases

**Purpose:** Break the payroll standalone initiative into three main features and their delivery phases, so each can become its own spec at the right time. **Status:** Planning document. No code changes. Grounded in the current codebase and the Phase 1 audit. **Companion to:** `Phase1_Architecture_Audit.md`, `BusinessPortal_Payroll_Standalone_Product_KIRO_Handoff.md` **Date:** 2026-09-13

>   This document maps the handoff direction (and the audit's R1-R7 roadmap) onto three concrete feature tracks. It records what exists today and what each phase must add. It does **not** claim anything is built beyond what the audit verified.

***

## Starting Point (verified in code)

-   Payroll is a coherent domain in the `[payroll]` schema with its own services and controllers.
-   Access is gated by a single flat `payroll` module key via `ModuleAccessAttribute`, seeded **only** against the Enterprise plan. There is no standalone entitlement and no capacity concept.
-   Subscriptions use one `[dbo].[Plan]` row per tier, each carrying `StripeProductId` + `StripePriceId`, `MonthlyPriceEur`, `AnnualPriceEur`, `MaxUsers`, `Slug`. Checkout uses the plan's `StripePriceId`; webhooks reverse-map a Stripe price back to a plan.
-   There is **no** Social Insurance export file, **no** Eurobank ISO 20022 XML, and **no** WorkforcePi ingestion contract.

## The Three Main Features (at a glance)

| \# | Feature                                                        | Nature                    | Hard external dependency?                           |
|----|----------------------------------------------------------------|---------------------------|-----------------------------------------------------|
| 1  | WorkforcePi integration (3 Inventors ecosystem)                | Cross-product contract    | Yes — WorkforcePi architecture                      |
| 2  | Individual Payroll product (gating, pricing, Stripe, capacity) | Commercial + platform     | No — internal decision                              |
| 3  | Eurobank ISO 20022 document generation                         | Deterministic file export | Yes — authoritative Eurobank XSD (you will provide) |

**Sequencing principle:** Feature 2 is the foundational unblocker — Payroll cannot be sold standalone, and neither WorkforcePi-only nor Eurobank customers can be served commercially, until Payroll has an independent entitlement. Features 1 and 3 are largely independent of each other and can proceed in parallel once their external inputs are ready.

***

## Feature 1 — WorkforcePi Integration

**Goal:** Let approved workforce records flow natively from WorkforcePi into Payroll, without an artificial connector charge (handoff §11, §14, §15). WorkforcePi keeps owning rota/attendance/ reconciliation; Payroll keeps owning calculation onward.

**Current state:** Absent. Payroll accepts inputs via manual entry and `EmployeeDefaultEarnings` only.

**Required contract characteristics (handoff §15):** tenant-safe, idempotent, auditable, versionable, traceable to source, resistant to duplicate processing.

### Phases

-   **1.1 — Contract definition (coordination).** Agree the canonical `PayrollWorkforceInput` payload with the WorkforcePi architecture (period, regular/overtime worked time, paid/unpaid leave, approved adjustments, approval metadata, source reference). Versionable from day one. **Blocked on WorkforcePi coordination.**
-   **1.2 — Ingestion endpoint + idempotency.** Receive approved records, dedup by (tenant, employer, employee, period, source reference), store with source traceability. Reject/ ignore duplicates safely.
-   **1.3 — Mapping into payroll inputs.** Translate ingested worked-time into payslip earning lines (respecting existing `EmployeeDefaultEarnings` override rules), so batch generation can consume them.
-   **1.4 — Reconciliation surface.** Show which payroll inputs came from WorkforcePi vs manual, and let the user review before finalisation. No re-implementation of WorkforcePi's reconciliation engine (handoff guardrail §22).
-   **1.5 — Native entitlement wiring.** When both WorkforcePi and Payroll are subscribed, the integration is included — no separate connector SKU (handoff §14). Depends on Feature 2 entitlement.

**Guardrails:** do not duplicate attendance/reconciliation; do not make WorkforcePi mandatory for Payroll; do not charge separately for the native path by default.

## Feature 2 — Individual Payroll Product

**Goal:** Make Payroll independently sellable — its own entitlement, its own capacity-banded pricing, its own Stripe products — while remaining includable in Enterprise (handoff §16-§18).

This feature has four sub-tracks, matching the request.

### 2.1 — Payroll gating (entitlement decoupling) — audit R1

**Current state:** `payroll` key is Enterprise-only; `ModuleAccessAttribute` layer 4 blocks anyone without it.

-   **2.1.a Decision:** how is standalone Payroll represented? It must be purchasable **without** the full Portal, so the current "add-on requires Enterprise" pattern (Inventory Intelligence) does not fit. Likely a first-class Payroll subscription that can coexist with, or be independent of, a Portal tier.
-   **2.1.b Entitlement source:** change only what populates `IncludedModules` so `payroll` can be granted via its own entitlement. Keep the `payroll` key and the 5-layer filter intact.
-   **2.1.c Preserve Enterprise inclusion:** Enterprise-includes-Payroll remains valid (handoff §17 says do not force removal). Both paths must resolve to the same access decision.
-   **2.1.d Standalone navigation/shell:** a Payroll-only customer needs a coherent entry point that does not assume the full Portal chrome is present.

### 2.2 — Payroll product pricing (capacity bands) — audit R2

**Current state:** No capacity concept anywhere in code.

-   **2.2.a Capacity model:** introduce employee-capacity bands (1-25, 26-100, 101-250, 251+ custom) on the Payroll entitlement.
-   **2.2.b Non-destructive enforcement:** when active-employee count exceeds the licensed band, trigger an upgrade/administrative flow — **never** delete or damage payroll data (handoff §18).
-   **2.2.c Configurable prices:** all band prices live in data, never in payroll calculation/domain logic (handoff §4). Support the "pay for 10 months, get 12" annual model already used platform-wide.
-   **2.2.d Capacity counting rule:** define precisely what counts toward a band (e.g. active employees in the current period) and where it is evaluated.

### 2.3 — Payroll Stripe update (new products per Payroll tier) — extends audit R2

**Current state:** One `Plan` row per tier with a single `StripeProductId`/`StripePriceId`; checkout and webhooks are plan-price based.

-   **2.3.a New Stripe products/prices:** one Stripe product per Payroll capacity band, each with monthly and annual prices. Store `StripeProductId`/`StripePriceId` per band (new plan rows or a new Payroll-plan table — decision in 2.1.a).
-   **2.3.b Checkout path:** extend `CheckoutService` so a Payroll-band purchase resolves the correct band price. Reuse the existing subscription-mode session pattern and tax-rate handling.
-   **2.3.c Webhook mapping:** extend the price-\>plan reverse lookup (`GetPlanByStripePriceIdAsync`) so Payroll-band prices resolve to the Payroll entitlement + band.
-   **2.3.d Band changes:** handle upgrade/downgrade between bands (proration, effective date) through Stripe subscription updates, keeping capacity enforcement (2.2.b) in sync.

### 2.4 — Something else (open items to confirm)

Candidates that belong to "making Payroll a product," to be confirmed with you:

-   **P&L / Compliance as optional capabilities (audit R3):** guard `PayrollPnlService` and `ComplianceIntegrationService` behind "is the Portal module subscribed?" so Payroll runs cleanly standalone (no-op integration) and richly inside Portal.
-   **Country mapping de-risk (audit R4):** move the hard-coded country name-\>ISO map out of `PayslipCalculationOrchestrator` into data, and replace the silent `"CY"` fallback with an explicit fail-closed validation error.
-   **Payroll-only onboarding/trial:** how a Payroll-only prospect signs up, trials, and is billed.
-   **Migration:** how existing Enterprise-with-Payroll customers map onto the new model without disruption.

>   Tell me which of these (or others) you want folded into "2.4" and I will scope them.

***

## Feature 3 — Eurobank ISO 20022 Document

**Goal:** Generate a valid, deterministic Eurobank-compatible ISO 20022 payment file (`pain.001.001.03` or `.09`) that the customer uploads to Eurobank Online Banking Business. 3 Inventors generates the file only — never authenticates, authorises, or executes the payment (handoff §9).

**Current state:** Absent. **You will provide the Eurobank documentation/XSD.**

### Phases

-   **3.1 — Specification intake (blocked until docs provided).** Confirm the exact ISO 20022 version, the authoritative XSD, and Eurobank's implementation guidelines. Choose one version to implement fully rather than promising both (handoff §9.3).
-   **3.2 — Deterministic generator.** Map a finalised payroll run to Group Header -\> Payment Information -\> Transaction Information: message id, creation timestamp, transaction count, control sum, debtor IBAN, per-employee creditor + amount + end-to-end id + remittance info.
-   **3.3 — Validation.** Schema-validate against the XSD; verify transaction count and control sum; enforce unique message identifiers; produce clear failure messages.
-   **3.4 — Export + audit record.** Downloadable XML plus a payment-file audit record (GeneratedBy, GeneratedAt, PayrollPeriod, PayrollRunId, TransactionCount, ControlTotal, Format, FormatVersion, FileIdentifier) so any file traces back to a finalised run (handoff §19).
-   **3.5 — Commercial inclusion.** Once validated, treat standard Eurobank XML export as a Payroll capability, not a separate recurring connector fee (handoff §10).

**Guardrails:** never call it a bank API; never store banking credentials; do not build production XML from the sample — validate against the authoritative XSD.

***

## Cross-Feature Dependency Map

```
Feature 2.1 (gating) ── unblocks ──> Feature 2.2 (bands) ──> Feature 2.3 (Stripe)
        │                                                         │
        └── unblocks ──> Feature 1.5 (native WFPi entitlement)    │
                                                                  │
Feature 1.1-1.4 (WorkforcePi contract) ── parallel, needs WFPi coordination
Feature 3.1-3.4 (Eurobank XML) ── parallel, needs Eurobank XSD (you provide)
```

**Recommended order:**

1.  Feature 2.1 (Payroll gating) — resolves the one active guardrail violation from the audit.
2.  Feature 2.2 + 2.3 (bands + Stripe) — commercially inseparable from gating.
3.  In parallel, start Feature 3 spec intake as soon as you hand over the Eurobank documentation.
4.  Feature 1 once WorkforcePi contract coordination is scheduled.

***

## What I Need From You

-   **Feature 2.1.a decision:** is standalone Payroll a first-class subscription independent of Portal tiers, or something else? (This shapes 2.2 and 2.3.)
-   **Feature 2.4 scope:** which open items to include.
-   **Feature 3:** the Eurobank documentation/XSD and the target ISO 20022 version.
-   **Feature 1:** confirmation of when WorkforcePi contract coordination can happen.

Once you confirm the 2.1.a direction, the natural first spec is **Feature 2.1 — Payroll gating**.
