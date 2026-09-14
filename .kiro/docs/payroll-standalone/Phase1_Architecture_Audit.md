# Payroll Standalone — Phase 1: Architecture Audit

**Scope:** Handoff Phase 1 (Architecture Audit) + a coupling-removal roadmap.
**Status:** Analysis only. No code changes. Grounded in the current codebase, not in spec intent.
**Companion to:** `BusinessPortal_Payroll_Standalone_Product_KIRO_Handoff.md`
**Date:** 2026-09-13

> The handoff explicitly warns: "KIRO must not interpret this document as proof that every
> described capability already exists." This audit therefore reports what is **verified in code**,
> what is **specced but unverified**, and what is **absent**.

---

## 1. Executive Summary

Payroll today is a **feature inside the Business Portal**, hard-gated to the **Enterprise tier**
through the same entitlement chain as every other module. It is architecturally coherent as a
domain (own `[payroll]` schema, own services, deterministic calculation, finalisation lifecycle,
audit trail), but it is **commercially fused to Enterprise** and has **no concept of a Payroll-only
product, employee-capacity bands, external payment/statutory export, or a WorkforcePi ingestion
contract**.

The single most important finding — and the handoff's own Phase 1/2 priority — is that the code
embodies exactly the coupling the handoff names as a guardrail violation:

> Avoid deep assumptions such as `PayrollEnabled == BusinessPortalEnterpriseRequired`.

The `payroll` module key is defined once, seeded only against the Enterprise plan, and enforced
through `ModuleAccessAttribute`, whose layer (4) refuses access unless `payroll` is in the
subscribed plan's included modules. There is no standalone entitlement path.

---

## 2. Method & Evidence

Findings below cite the files inspected. Where a capability is described in the payroll specs
(Phases A-D) but could not be confirmed in code within this audit, it is marked
**Specced - unverified**.

Inspected:
- `Portal.Infrastructure/Constants/PortalModules.cs` — module key catalogue
- `Portal.Web/Security/ModuleAccessAttribute.cs` — the 5-layer access filter
- `Portal.Web/Filters/ModuleControllerMap.cs` — module -> controller mapping
- `Portal.Infrastructure/Services/PayslipCalculationOrchestrator.cs` — PAYE + country mapping
- `Portal.Infrastructure/Services/PayrollService.cs` — batch/preview/confirm orchestration
- `Portal.Database/Seeds/Seed_*.sql` — Cyprus templates, PAYE bands, plan-feature seeds
- `.kiro/docs/Subscription_Tier_Model.md` — commercial tier model
- Payroll specs A-D + `payslip-earnings-override`

---

## 3. Current Payroll Domain Boundary

**Verified.** Payroll owns a self-contained domain:

- **Schema:** all tables in `[payroll]` (Employee, Department, EarningType, DeductionType,
  DeductionCategoryType, DeductionRateHistory, PayslipPeriod, Payslip, PayslipEarningLine,
  PayslipDeductionLine, EmployeeDefaultEarnings, PayslipAuditLog, PayslipAuditActionType,
  PayslipStatusType, PayslipEmailLog, PayeTaxBand, CountryDeductionTemplate,
  PayslipPeriodComplianceFiling).
- **Services (Portal.Infrastructure):** `PayrollService`, `PayrollReportService`,
  `PayrollPnlService`, `PayslipCalculationEngine`, `PayslipCalculationOrchestrator`,
  `PayeCalculationService`, `ComplianceIntegrationService`, `CountryTemplateService`,
  `PayslipAuditService`, `PayslipPeriodStatusService`.
- **Controllers:** `Payroll`, `AdminPayroll`, `PayrollReport`, `PayrollCompliance`,
  `PayrollTemplate` (mapped under the `payroll` module key in `ModuleControllerMap`).

**Assessment:** the domain boundary is clean enough to extract. The problem is not the domain
shape — it is the **entitlement and pricing coupling** wrapped around it.

---

## 4. Entitlement / Tier Coupling (the core finding)

**Verified.** Access flows through `ModuleAccessAttribute.OnAuthorizationAsync` in five layers:

1. SuperAdmin bypass
2. Demo-session bypass (handled by `DemoPermissionFilter`)
3. Active-subscription check (`ISubscriptionPlanService.GetAccessAsync`)
4. **Plan-inclusion check** — `accessResult.IncludedModules.Contains(Module)` — this is the gate
5. User-level permission (`IPermissionService`), with Owner bypass

`PortalModules.Payroll = "payroll"` is a single flat key. The plan seeds add other modules to
Enterprise (e.g. `Seed_PlanFeature_*`), and the Subscription Tier Model lists `payroll` as an
**Enterprise-only** module key. So in production, `payroll ∈ IncludedModules` is true **only** for
Enterprise subscribers.

**Consequences for standalone:**
- There is no way to grant Payroll without an Enterprise plan that includes the `payroll` module.
- There is no capacity dimension on the entitlement (see §6).
- The `UpgradeRequired.cshtml` result assumes a tier-upgrade path, not a product-purchase path.

This is the literal `PayrollEnabled == EnterpriseRequired` assumption the handoff prohibits.

---

## 5. Portal-Specific Assumptions Embedded in Payroll

**Verified couplings** (things Payroll leans on that a standalone product must either keep as
shared infrastructure or abstract):

| Assumption | Where | Standalone implication |
|---|---|---|
| `payroll` key is Enterprise-gated | Tier model + plan seeds | Needs an independent entitlement + capacity band |
| P&L integration writes salary/contribution expense entries | `PayrollPnlService`, Phase B | Fine when Portal present; must be optional/no-op standalone |
| Compliance auto-population (Social Insurance filing) | `ComplianceIntegrationService`, Phase D | Depends on Business Applications Tracker (a Portal module); must degrade gracefully |
| Business identity / `BusinessId` from Portal tenancy | `_tenantService.CurrentBusinessId` | Acceptable shared infrastructure — keep |
| Auth / Owner / SuperAdmin roles | ASP.NET Core Identity | Acceptable shared infrastructure — keep |

**Assessment:** the P&L and Compliance integrations are the two domain couplings that must become
**capability-flagged** (present when Portal modules are subscribed, cleanly absent otherwise). The
identity/tenancy couplings are exactly the "shared infrastructure where appropriate" the handoff
endorses and should be kept.

---

## 6. Employee-Capacity Bands

**Absent.** A codebase search for `capacity`, `MaxEmployees`, `EmployeeLimit`, `CapacityBand`,
`employee_band` returned **no matches**. There is:
- no capacity field on any subscription/plan entity,
- no enforcement point that counts active employees against a licensed band,
- no upgrade/administrative flow for exceeding capacity.

The handoff's pricing model (1-25, 26-100, 101-250, 251+) has **no representation in code today**.
The handoff's guardrail — "If a customer exceeds the licensed capacity, do not delete payroll data"
— has nothing to enforce yet, which is safe by omission but blocks commercial packaging.

---

## 7. Determinism & Historical Reproducibility

**Verified — strong.**
- **Rate history:** deductions resolve `DeductionRateHistory` by effective-date window; the
  `DeductionRateHistoryId` is persisted on each `PayslipDeductionLine`, preserving the exact rate
  used (Phase A req 7).
- **Per-line rounding:** `MidpointRounding.AwayFromZero`, applied per line (Phase A req 7.9).
- **Lifecycle locking:** Draft -> Preview -> Finalised -> Unlocked -> Re-finalised, with
  finalised periods immutable and an append-only `PayslipAuditLog` (Phase B).
- **Reference date:** first day of the period month is used for rate lookups.

**Gap — country-template versioning.** PAYE bands are resolved by `CountryCode` + year via
`PayeTaxBand`, which is good. **But** the `PayslipCalculationOrchestrator` holds a hard-coded
`CountryCodeMapping` dictionary and **defaults unmapped countries to `"CY"`**:

```
private static readonly Dictionary<string, string> CountryCodeMapping = new(...) { { "Cyprus", "CY" }, ... };
...
if (!CountryCodeMapping.TryGetValue(countryName, out var countryCode))
    countryCode = "CY"; // Default to Cyprus if unmapped
```

This is a Cyprus-leaning assumption inside calculation logic. It does not break determinism for
Cyprus, but it silently mis-classifies any unmapped country as Cyprus — a correctness risk the
moment a second jurisdiction is onboarded. The handoff's guardrail against "Cyprus-specific
hard-coding" applies here.

---

## 8. Country-Template Architecture

**Partially verified.** The data structures exist and are jurisdiction-parameterised:
- `PayeTaxBand (CountryCode, LowerBound, UpperBound, Rate, EffectiveFromYear, EffectiveToYear)`
- `CountryDeductionTemplate (CountryCode, DeductionName, Code, IsPercentage, DeductionCategoryTypeId, DefaultRate, IsPayeDeductible, SortOrder, IsActive)`
- Seeds exist for Cyprus (`Seed_CyprusPAYETaxBands2024.sql`, `Seed_CyprusDeductionTemplates.sql`).

**Gaps:**
- The country **name -> ISO code** mapping is code-resident (§7), not data-driven.
- Only Cyprus is seeded; the "no schema change for new countries" claim is plausible from the
  table shape but **unproven** (no second country exists to validate it).

Per the handoff: "Internationalizable architecture is not the same as international compliance."
The architecture leans international; compliance is Cyprus-only and must not be advertised
otherwise.

---

## 9. Export Architecture

**Absent for the two handoff targets.**
- **Cyprus Social Insurance submission/export file** — not found in code. Phase D populates a
  Compliance *filing amount*, but does not generate a government submission file.
- **Eurobank ISO 20022 (`pain.001`) payment XML** — not found in code (no spec either).
- **Payment-file audit record** (GeneratedBy, ControlTotal, FileIdentifier, etc.) — absent.

What exists: PDF payslips, Excel report exports, email delivery (Phase C). These are internal
document exports, not statutory/bank interchange files.

---

## 10. WorkforcePi Integration Contract

**Absent.** No `PayrollWorkforceInput` contract, no ingestion endpoint, no idempotency/dedup layer,
no source-traceability record. Payroll accepts inputs via manual entry and `EmployeeDefaultEarnings`
only. The native WorkforcePi -> Payroll path is entirely unbuilt.

---

## 11. Capability Status Classification (handoff §21)

| Capability | Status | Evidence |
|---|---|---|
| Payroll domain + `[payroll]` schema | **Existing** | Services, controllers, schema all present |
| Deterministic calculation + rate history | **Existing** | `PayslipCalculationEngine`, persisted `DeductionRateHistoryId` |
| Finalisation lifecycle + immutable audit | **Existing** | Phase B lifecycle, `PayslipAuditLog` |
| PDF / email / reporting | **Existing** | Phase C services |
| PAYE engine + Compliance filing auto-populate | **Existing** | `PayeCalculationService`, `ComplianceIntegrationService` |
| Country-template data model | **Existing - Cyprus only** | `PayeTaxBand`, `CountryDeductionTemplate` seeded for CY |
| Country name->ISO mapping data-driven | **Packaging/Extraction Required** | Currently hard-coded, CY-defaulting |
| Standalone Payroll entitlement | **Required (absent)** | `payroll` key is Enterprise-only |
| Employee-capacity bands | **Required (absent)** | No capacity concept in code |
| P&L / Compliance as optional capabilities | **Packaging/Extraction Required** | Currently assume Portal modules present |
| Cyprus Social Insurance export file | **Required - authoritative format needed** | Not built |
| Eurobank ISO 20022 XML | **Possible/committed after XSD validation** | Not built |
| WorkforcePi ingestion contract | **Required product direction** | Not built |

---

## 12. Coupling-Removal Roadmap

Ordered by dependency. Each item is a candidate future spec; none are implemented here.

### R1 - Independent Payroll entitlement (unblocks everything)
- Introduce a Payroll entitlement that is **not** synonymous with the Enterprise plan.
- Options to evaluate (decision required): (a) a first-class product/subscription separate from the
  tier plan, or (b) an add-on entitlement (like Inventory Intelligence) but sellable without
  Enterprise. The handoff requires it to be purchasable **without** the full Portal, which rules
  out the current add-on-requires-Enterprise pattern.
- Keep the `payroll` module key and `ModuleAccessAttribute` chain; change **only** the source that
  populates `IncludedModules` so Payroll can be included via its own entitlement.
- Preserve the Enterprise-includes-Payroll option (handoff §17 says do not force its removal).

### R2 - Employee-capacity band model
- Add a capacity dimension to the Payroll entitlement (band: 1-25 / 26-100 / 101-250 / 251+).
- Add a **non-destructive** enforcement point: exceeding capacity triggers an upgrade/administrative
  flow, never data deletion (handoff §18 guardrail).
- Keep prices **configurable/data-driven** — no prices in calculation or domain logic (handoff §4).

### R3 - Make P&L and Compliance integrations capability-flagged
- Guard `PayrollPnlService` and `ComplianceIntegrationService` behind "is the Portal P&L /
  Compliance module subscribed?" checks so Payroll runs cleanly standalone (no-op integration)
  and richly inside Portal.

### R4 - Data-drive the country mapping + de-risk the CY default
- Move country name->ISO mapping out of `PayslipCalculationOrchestrator` into data.
- Replace the silent `"CY"` fallback with an explicit validation error for unmapped/unconfigured
  jurisdictions (fail closed, per determinism guardrail).

### R5 - Cyprus Social Insurance export file
- Separate spec. Requires the **authoritative** Cyprus schema/sample before implementation.
- Add the payment/export audit record shape from handoff §19.

### R6 - Eurobank ISO 20022 XML export
- Separate spec. Requires the authoritative Eurobank XSD + chosen version (`.03` vs `.09`).
- Generation only — never bank authentication/authorisation/execution (handoff §9.2).

### R7 - WorkforcePi ingestion contract
- Separate spec, coordinated with WorkforcePi. Versionable, idempotent, tenant-safe, traceable.

---

## 13. Guardrail Compliance Check (handoff §22)

| Guardrail | Current state |
|---|---|
| Don't make Business Portal mandatory for Payroll | **Violated today** — Payroll requires Enterprise |
| Don't hard-code prices in payroll logic | **OK** — no prices found in calculation logic |
| Don't destroy country-template with CY hard-coding | **At risk** — `CountryCodeMapping` + CY default |
| Don't claim international compliance from templates | **OK if messaged correctly** — only CY seeded |
| Don't call Eurobank XML a bank API | **N/A** — not built |
| Don't silently mutate finalised payroll | **OK** — lifecycle + immutable audit enforce this |
| Don't expose payroll via generic permissions | **OK** — dedicated `payroll` key + role checks |
| Don't mark roadmap items as done | This audit keeps them as Required/Absent |

---

## 14. Recommended Next Step

Proceed to **R1 (Independent Payroll entitlement)** as the first buildable spec — it is the
foundational unblocker and directly resolves the one active guardrail violation. R2 (capacity
bands) should follow immediately, since entitlement and capacity are commercially inseparable.

The two hard-external-dependency items (R5 Social Insurance export, R6 Eurobank XML) must not be
specced for production until their authoritative specifications are in hand.
