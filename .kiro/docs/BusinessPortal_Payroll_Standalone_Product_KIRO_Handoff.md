# Business Portal Payroll — Standalone Product & Integration Strategy
## KIRO Implementation Handoff

**Product:** 3 Inventors Payroll  
**Ecosystem:** 3 Inventors Business Portal / WorkforcePi  
**Purpose:** Strategic and implementation handoff for the KIRO agent responsible for Business Portal  
**Status:** Product direction. This document does **not** imply that every capability described below is already implemented.

---

## 1. Executive Direction

Payroll must no longer be treated only as a feature contained inside the Business Portal Enterprise tier.

The strategic direction is:

> **3 Inventors Payroll becomes an independently sellable product/module while remaining natively integrated with Business Portal and WorkforcePi.**

This is both a commercial packaging decision and an architectural boundary decision.

A customer must be able to use Payroll without purchasing the complete Business Portal and without using WorkforcePi.

At the same time, customers who use multiple 3 Inventors products should benefit from native integration without artificial connector charges between our own products.

---

## 2. Supported Commercial Configurations

The architecture must support these configurations:

| Configuration | Intended use |
|---|---|
| Payroll only | Standalone payroll processing |
| WorkforcePi + Payroll | Approved workforce records flow into payroll |
| Business Portal + Payroll | Payroll operates with broader business-management capabilities |
| Business Portal + WorkforcePi + Payroll | Integrated operational workflow |
| External attendance source + Payroll | Possible future import/integration path |

Therefore Payroll requires its own entitlement and product boundary.

Shared infrastructure with Business Portal is acceptable and desirable where technically appropriate. Commercial independence does **not** mean duplicating authentication, tenancy, logging, storage, or other infrastructure unnecessarily.

---

## 3. Product Boundary

Payroll owns the process from payroll-ready inputs onward.

Core intended capabilities include:

- employee payroll records;
- salary/hourly payroll inputs;
- payroll calculations;
- statutory deductions;
- employee contributions;
- employer contributions;
- configurable funds, deductions and contributions;
- payslips;
- payroll history;
- payroll reporting;
- payment preparation/export;
- statutory reporting/export;
- integration with approved attendance/workforce records.

Payroll should not become a duplicate workforce-management system.

WorkforcePi remains responsible for scheduling, attendance and attendance reconciliation.

---

## 4. Pricing Strategy

### Recommended Capacity Bands

| Employee capacity | Monthly | Annual |
|---|---:|---:|
| 1–25 employees | €49/month | €490/year |
| 26–100 employees | €89/month | €890/year |
| 101–250 employees | €149/month | €1,490/year |
| 251+ employees | Custom | Custom |

The annual model follows the broader 3 Inventors commercial philosophy of approximately:

> **Pay for 10 months and receive 12 months.**

Pricing must remain configurable. Do not hard-code commercial prices into payroll calculation/domain logic.

---

## 5. Why Payroll Uses Capacity Bands

Payroll should use **fixed employee-capacity bands**, rather than linear per-employee pricing.

All standard payroll functionality should remain available across the normal bands. The principal difference is supported employee capacity, not intentionally crippled functionality.

Payroll is fundamentally a periodic employer-level processing product. Marginal computational and support cost does not normally increase linearly for every additional employee.

This differs from WorkforcePi, where every active employee continuously creates operational activity:

- attendance events;
- shifts and breaks;
- rota assignments;
- availability;
- reconciliation;
- exceptions;
- notifications;
- reports;
- device activity;
- future Staff Portal activity.

The ecosystem therefore intentionally uses different natural pricing units:

| Product | Natural pricing model |
|---|---|
| WorkforcePi | Per active employee |
| 3 Inventors Payroll | Fixed employee-capacity band |
| Business Portal | Fixed business subscription |

This is intentional product economics, not inconsistency.

---

## 6. Country-Template Payroll Architecture

A strategically important characteristic of Payroll is the **country-template model**.

Cyprus is the immediate jurisdiction, but payroll rules should not be structurally hard-coded in a way that prevents other jurisdictions.

A country template should be capable of defining/configuring items such as:

- statutory deductions;
- employee contributions;
- employer contributions;
- funds;
- rates;
- thresholds;
- calculation rules;
- effective dates;
- reporting requirements;
- jurisdiction-specific payroll components.

This provides a path toward other countries and other fund/deduction/contribution structures.

However:

> **Internationalizable architecture is not the same as international compliance.**

Every new jurisdiction must be independently implemented, tested and validated before being commercially supported.

---

## 7. Determinism and Historical Reproducibility

Payroll calculations must be deterministic and auditable.

Changes to future statutory rules must not silently alter historical payroll.

The architecture should preserve sufficient information to reproduce a finalized payroll period using the rules/template version applicable to that period.

Important concepts include:

- country-template version;
- effective dates;
- calculation inputs;
- calculated outputs;
- manual adjustments;
- approval/finalization state;
- user responsible for changes;
- timestamps.

Finalized payroll must not behave like a mutable spreadsheet.

---

## 8. Cyprus Social Insurance Export

Current product direction is **structured file/export**, not a claimed direct public API integration.

Conceptual workflow:

```text
3 Inventors Payroll
        ↓
Generate compliant Social Insurance submission/export file
        ↓
Authorized customer/user
        ↓
Upload/submission through the appropriate government mechanism
```

This belongs naturally to Payroll and should normally be a standard Payroll capability rather than a separate recurring connector fee.

### Implementation rule

KIRO must work from the current authoritative Cyprus specification/schema/sample before production implementation.

Do not infer the format.

Do not advertise direct API integration unless an actual supported API is verified and implemented.

---

# 9. Eurobank Payroll Payment Integration

## 9.1 Integration Direction

The current Eurobank opportunity should be treated primarily as an **ISO 20022 XML payment-file generation workflow**, not as a direct banking API project.

The available Eurobank implementation material supports payroll/mass-payment XML upload through Eurobank Online Banking Business.

Relevant ISO 20022 message versions include:

- `pain.001.001.03`
- `pain.001.001.09`

The intended workflow is:

```text
3 Inventors Payroll
        ↓
Generate Eurobank-compatible ISO 20022 XML
        ↓
Customer obtains/downloads XML
        ↓
Customer uploads XML to Eurobank Online Banking Business
        ↓
Eurobank validates the file
        ↓
Authorized bank user reviews/authorizes
        ↓
Eurobank executes payments
```

---

## 9.2 Security Boundary

3 Inventors Payroll must not be represented as executing the bank transfer in this workflow.

3 Inventors does **not** need to:

- store customer online-banking credentials;
- authenticate to the customer's bank account;
- authorize salary payments;
- transfer funds;
- bypass Eurobank authorization;
- impersonate an authorized banking user.

Our responsibility is generation of a valid payment instruction file.

Eurobank remains responsible for:

- bank authentication;
- file validation;
- payment authorization;
- payment execution.

This boundary significantly reduces security and regulatory exposure.

---

## 9.3 XML Generation Requirements

The Eurobank material describes ISO 20022 structures around:

```text
Group Header
    ↓
Payment Information
    ↓
Transaction Information
```

Relevant concepts include:

- unique message identifier;
- creation timestamp;
- number of transactions;
- control sum;
- debtor account/IBAN;
- payment amount;
- creditor information;
- creditor IBAN;
- end-to-end identifier;
- remittance information.

Payroll-specific service-level/category-purpose semantics must follow the authoritative Eurobank implementation guidelines and selected XSD.

### Critical rule

> **Do not implement production XML by copying the sample XML.**

Samples are examples, not the authoritative contract.

Production implementation must be validated against:

1. Eurobank implementation guidelines;
2. the appropriate XSD;
3. the selected ISO 20022 version;
4. the customer's actual account/bank requirements where relevant.

There are structural differences between `.03` and `.09`.

Do not promise both versions unless there is a genuine requirement.

Prefer the version Eurobank recommends for the target implementation and validate it fully.

---

## 10. Eurobank Commercial Position

Once validated, standard Eurobank-compatible payroll XML export should normally be included as a Payroll capability.

Avoid creating a fragmented commercial model such as:

```text
Payroll subscription
+ Eurobank monthly connector fee
+ Social Insurance export fee
+ WorkforcePi integration fee
```

Custom/proprietary bank APIs, unusual payment workflows, cheque-generation requirements, or customer-specific bank formats may be separately scoped.

Standard reusable Eurobank XML export is different: it is product capability.

---

# 11. WorkforcePi Integration

The WorkforcePi → Payroll integration is strategically important.

The intended end-to-end operational journey is:

```text
Rota
  ↓
Attendance
  ↓
Attendance Reconciliation
  ↓
Manager Review / Approval
  ↓
Approved Worked-Time Records
  ↓
Payroll Preparation
  ↓
Payroll Calculation / Review
  ↓
Payroll Finalization
  ↓
Bank Payment File
  ↓
Bank Authorization / Execution

and

Payroll
  ↓
Social Insurance Export
```

---

## 12. Domain Ownership

### WorkforcePi owns

- rota/workforce scheduling;
- employee availability;
- attendance capture;
- shifts;
- breaks;
- actual worked-time records;
- attendance exceptions;
- planned-vs-actual reconciliation;
- manager review and approval of workforce records.

### Payroll owns

- payroll-ready employee inputs;
- salary/hourly calculation;
- statutory deductions;
- contributions;
- employer contributions;
- funds;
- payroll adjustments;
- payslips;
- payroll history;
- finalization;
- payment-file preparation;
- statutory payroll exports.

Payroll must not recreate WorkforcePi's attendance/reconciliation engine merely because Payroll can operate standalone.

---

## 13. Payroll Without WorkforcePi

Payroll must remain fully useful independently.

A Payroll-only customer may supply inputs through:

- manual entry;
- structured import;
- approved external attendance records;
- future supported third-party integrations.

Therefore:

> **WorkforcePi is an optimized native source of approved workforce data, not a mandatory dependency.**

---

## 14. Native WorkforcePi Integration Pricing

When a customer subscribes to both WorkforcePi and Payroll, native integration between the two 3 Inventors products should be included.

Do not create:

```text
WorkforcePi subscription
+ Payroll subscription
+ WorkforcePi-to-Payroll connector subscription
```

The integration itself increases the value and retention of the ecosystem.

External/custom integrations are commercially different and may be separately scoped.

---

## 15. Integration Contract

The WorkforcePi integration should use an explicit versionable contract rather than direct coupling between persistence tables.

Payroll should receive **approved payroll-relevant workforce records**.

Illustrative conceptual payload:

```text
PayrollWorkforceInput
- TenantId
- EmployerId
- EmployeeId
- PayrollPeriod
- RegularWorkedTime
- Overtime
- PaidLeave
- UnpaidLeave
- ApprovedAdjustments
- ApprovalStatus
- ApprovedBy
- ApprovedAt
- SourceSystem
- SourceReference
```

This is illustrative, not a mandated final schema.

The final contract should be coordinated with the WorkforcePi architecture.

Required characteristics:

- tenant-safe;
- idempotent;
- auditable;
- versionable;
- traceable to source;
- resistant to duplicate processing.

---

# 16. Business Portal Relationship

Payroll may share Business Portal infrastructure while remaining commercially independent.

Potential shared infrastructure includes:

- tenant/business identity;
- authentication;
- user management;
- permissions infrastructure;
- subscription/billing infrastructure;
- audit infrastructure;
- financial reporting;
- P&L integration;
- document storage;
- notifications.

However, avoid deep assumptions such as:

```text
PayrollEnabled == BusinessPortalEnterpriseRequired
```

Payroll requires an independent entitlement.

---

## 17. Impact on Existing Business Portal Enterprise

The current Business Portal model previously positioned Payroll/Payslips within Enterprise.

Making Payroll independently sellable requires the commercial model to be revisited.

This does **not** automatically require removing Payroll from Enterprise.

A possible model is:

- Payroll remains included with Enterprise;
- Payroll can also be purchased standalone;
- other Portal tiers may be allowed to add Payroll independently if commercially desired.

The final commercial entitlement should remain configurable until explicitly locked.

Do not encode Portal pricing assumptions inside Payroll domain logic.

---

# 18. Licensing and Entitlement

The subscription system should be able to answer:

```text
Is Payroll enabled for this tenant/business?
What employee-capacity band applies?
What is the subscription state?
What country/jurisdiction template is enabled?
What optional integrations/capabilities are enabled?
```

Capacity enforcement must be commercially safe.

If a customer exceeds the licensed capacity, do not delete payroll data or damage historical records.

Use an upgrade/administrative flow according to the final billing policy.

---

# 19. Security and Audit

Payroll contains sensitive employee and financial information.

Requirements include:

- strict tenant isolation;
- least-privilege access;
- payroll-specific permissions;
- audit trail for payroll changes;
- traceability of manual adjustments;
- finalization/locking;
- secure payslip/document access;
- controlled exports;
- protection against cross-tenant access;
- logging that does not leak salary information;
- deterministic recalculation;
- explicit draft/finalized states.

For generated bank files, preserve an audit record such as:

```text
GeneratedBy
GeneratedAt
PayrollPeriod
PayrollRunId
TransactionCount
ControlTotal
Format
FormatVersion
FileIdentifier
```

It must be possible to determine which finalized payroll run produced a payment file.

---

# 20. Payroll Lifecycle

The domain should distinguish states conceptually similar to:

```text
Draft
  ↓
Calculated
  ↓
Reviewed
  ↓
Approved / Finalized
  ↓
Payment File Generated
  ↓
Exported
```

Exact names are implementation decisions.

The important rule is that finalized payroll cannot change silently.

Corrections after finalization require an explicit controlled workflow.

---

# 21. Capability Status Classification

KIRO must not interpret this document as proof that every described capability already exists.

Use explicit statuses:

### Existing
Implemented and verified.

### Existing — Packaging/Extraction Required
Implemented within Business Portal but requiring entitlement/UI/product-boundary work for standalone operation.

### Committed Product Capability
Approved reusable functionality to implement.

### Possible Integration — Verification Required
Architecturally/commercially valid, but external specification or technical verification is still required.

### Customer-Specific / Separately Scoped
Not standard product capability unless later generalized.

Example:

| Capability | Direction |
|---|---|
| Payroll calculation engine | Verify current implementation |
| Country-template architecture | Existing direction — verify implementation |
| Standalone Payroll entitlement | Required |
| WorkforcePi native integration | Required product direction |
| Eurobank ISO 20022 XML | Possible/committed after specification validation |
| Cyprus Social Insurance export | Required direction; authoritative format required |
| Direct Eurobank API | Not current direction |
| Arbitrary bank APIs | Future/separate assessment |
| Cheque issuance | Outside standard scope unless separately required |

---

# 22. Product Guardrails

KIRO must **not**:

- make Business Portal mandatory for Payroll without genuine architectural necessity;
- make WorkforcePi mandatory for Payroll;
- duplicate WorkforcePi attendance/reconciliation functionality inside Payroll;
- hard-code subscription prices into payroll calculation logic;
- destroy the country-template architecture with Cyprus-specific hard-coding;
- claim international compliance merely because templates exist;
- describe Eurobank XML export as direct bank API integration;
- request/store customer online-banking credentials for the XML workflow;
- execute or authorize salary payments;
- generate production bank XML without validating the authoritative specification/XSD;
- treat sample XML as the specification;
- charge separately by default for native WorkforcePi → Payroll integration;
- make standard Social Insurance export an artificial recurring connector fee;
- silently mutate finalized payroll;
- expose salary/payroll information through generic permissions without payroll authorization;
- mark roadmap capabilities as completed merely because they appear in this document.

---

# 23. Target Architecture

```text
                 ┌───────────────────────┐
                 │      WorkforcePi      │
                 │                       │
                 │ Rota                  │
                 │ Attendance            │
                 │ Reconciliation        │
                 │ Manager Approval      │
                 └───────────┬───────────┘
                             │
                   Approved Workforce Data
                             │
                             ▼
┌──────────────────────────────────────────────────────┐
│                 3 Inventors Payroll                  │
│                                                      │
│ Employee Payroll Records                             │
│ Country Template / Rules                             │
│ Payroll Calculation                                 │
│ Deductions / Contributions / Funds                  │
│ Review / Finalization                               │
│ Payslips / History                                  │
└───────────────┬───────────────────────┬──────────────┘
                │                       │
                ▼                       ▼
       Eurobank ISO 20022        Social Insurance
           XML Export                Export
                │
                ▼
      Eurobank Business Banking
      Validation + Authorization
                │
                ▼
          Payment Execution
```

Business Portal can surround/integrate with this domain where subscribed, but Payroll remains coherent independently.

---

# 24. Commercial Examples

### Small Employer

```text
18 employees
Payroll band: 1–25
€49/month
€490/year
```

### Medium Employer

```text
72 employees
Payroll band: 26–100
€89/month
€890/year
```

### Hotel Example

```text
150 employees
Payroll band: 101–250
€149/month
€1,490/year
```

If the hotel also subscribes to WorkforcePi:

```text
WorkforcePi
+
3 Inventors Payroll
+
Native WorkforcePi → Payroll integration included
```

Hardware, exceptional implementation work, and customer-specific integrations remain separate commercial concerns.

---

# 25. Strategic Ecosystem Value

Standalone Payroll creates multiple valid entry paths:

```text
Payroll → WorkforcePi → Business Portal
```

or:

```text
WorkforcePi → Payroll
```

or:

```text
Business Portal → Payroll
```

The customer should not need to enter the ecosystem in a predetermined order.

Each product should solve a real problem independently and become more valuable when combined.

---

# 26. Recommended KIRO Execution Sequence

## Phase 1 — Architecture Audit

Determine and document:

- current Payroll domain boundaries;
- current country-template implementation;
- existing employee dependencies;
- Portal-specific assumptions;
- current permissions;
- current subscription/tier checks;
- payroll lifecycle/finalization behavior;
- historical reproducibility;
- current export architecture.

Identify coupling that prevents standalone operation.

## Phase 2 — Standalone Entitlement

Introduce or confirm:

- Payroll-specific entitlement;
- employee-capacity configuration;
- independent Payroll access/navigation;
- Payroll permissions;
- removal of unnecessary mandatory Portal-tier assumptions.

## Phase 3 — Payroll Domain Integrity

Ensure:

- deterministic calculation;
- country-template effective dates/versioning;
- controlled lifecycle/finalization;
- auditability;
- historical reproducibility.

## Phase 4 — WorkforcePi Contract

Coordinate with WorkforcePi architecture.

Define the canonical approved-workforce-data contract.

Implement safe, idempotent and traceable ingestion.

## Phase 5 — Cyprus Statutory Export

Implement/verify the required Social Insurance-compatible export against authoritative specifications.

## Phase 6 — Eurobank XML

After exact Eurobank version/XSD requirements are confirmed:

- implement ISO 20022 generator;
- schema validation;
- transaction-count validation;
- control-sum validation;
- unique message identifiers;
- deterministic payroll-to-payment mapping;
- export audit record;
- downloadable XML;
- clear validation/failure messages.

## Phase 7 — Commercial Packaging

Connect:

- employee-capacity bands;
- monthly/annual subscription;
- upgrade handling;
- Business Portal inclusion/add-on rules;
- customer-facing subscription information.

---

# 27. Acceptance Principles

The standalone Payroll initiative is successful when:

1. Payroll can be subscribed to without requiring the complete Business Portal.
2. Payroll is usable without WorkforcePi.
3. WorkforcePi can supply approved workforce records through a defined native integration.
4. Native WorkforcePi integration does not require an artificial connector subscription.
5. Payroll calculations remain deterministic and auditable.
6. Country-specific logic remains isolated through the country-template architecture.
7. Historical finalized payroll remains reproducible.
8. Cyprus statutory exports are based on authoritative specifications.
9. Eurobank XML can be generated and validated without 3 Inventors handling bank authorization or execution.
10. Payroll information is protected by explicit permissions.
11. Capacity/pricing rules are configurable and separated from calculation logic.
12. Business Portal adds ecosystem value without becoming a hidden technical dependency.

---

# 28. Final Direction to KIRO

The central architectural decision is:

> **Payroll is now a product, not merely a Business Portal feature.**

This does not mean creating another isolated application that duplicates infrastructure.

The desired model is:

> **Independent commercial product + clean domain boundary + shared infrastructure where appropriate + native ecosystem integration.**

The broader operational journey is:

> **Plan the workforce → record what happened → reconcile and approve → calculate payroll → prepare salary payments → prepare statutory submissions.**

WorkforcePi and Payroll own different sections of this journey.

Their integration should remove manual handovers while preserving the ability of each product to operate independently.

For Eurobank, the immediate opportunity should be treated as a **deterministic ISO 20022 XML generation and validation problem**, not as an online-banking API project.

Above all:

> **Do not overpromise integration status. Validate external specifications, build reusable product primitives, preserve auditability, and keep commercial packaging independent from payroll calculation logic.**
