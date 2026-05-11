# MyChair Platform — Architecture State
Version: v1.0  
Purpose: Maintain continuity for KIRO agent execution and system evolution

---

## 1. Current Architecture Overview

### System Type
Modular operational platform with future alignment to:
→ Event-driven Operational Intelligence (COM)

### Layers

1. Presentation Layer
- WPF (POS, OPS, ERP clients)
- ASP.NET MVC (Web portals: Insights, Quotation, Revenue Control)

2. Application Layer
- Services (business logic)
- Use-case driven orchestration

3. Domain Layer
- Core entities (POS, Orders, Invoices, Payments, Jobs)
- BOM (Bill of Materials) — partially planned

4. Infrastructure Layer
- SQL Server (primary storage)
- Messaging (RabbitMQ + MassTransit)
- Outbox pattern (ProcessedMessages)

---

## 2. Key Bounded Contexts

### POS Context
- Produces sales aggregates (current limitation)
- Future: transaction-level + BOM signals

### ERP Context
- Source of truth for:
  - Costs
  - Suppliers
  - Inventory

### OPS Context (Unified)
- OPS Hospitality
- OPS Retail
- Order execution layer

### JDS Context
- Production orchestration
- Triggered by stock thresholds
- Updates stock post-production

### Insights Context
- Aggregated operational visibility
- Input: daily totals per service type

### Revenue Control Context
- Invoice tracking
- Payment recording
- Receivables monitoring

### Quotation Context
- Device + setup selection
- Pricing logic
- Proposal generation

---

## 3. Data Contracts (Current)

### POS Aggregated Input

Fields:
- Id
- ShopId
- ZDate
- ServiceTypeId
- TotalInvoices
- TotalSales
- LastSyncTimestamp

Limitations:
- No product-level data
- No per-transaction detail

---

## 4. Messaging Architecture

Pattern:
- Transactional Outbox

Key Components:
- ProcessedMessages (outbox table)
- Background dispatcher
- Idempotent consumers

Transport:
- RabbitMQ (via MassTransit)

Purpose:
- Decouple POS, JDS, OPS
- Enable future COM ingestion

---

## 5. Current Constraints

1. No product-level sales data  
   → Limits insights sophistication

2. BOM not fully implemented  
   → No cost-based intelligence yet

3. ERP integration required  
   → For accurate margin calculations

4. UI-first development phase  
   → Backend partially scaffolded

---

## 6. Target Architecture (Mid-Term)

### Transition to COM Model

Flow:

POS / ERP / OPS → Ingestion Layer → Canonical Events → Intelligence Core

### Key Concepts

- Canonical Event Envelope
- Staging (append-only ingestion)
- Normalization workers
- Deterministic analytics engine

---

## 7. Intelligence Readiness

Planned capabilities:

- Margin drift detection
- Price simulation
- Recommendation engine
- Automated price updates (with approval)

Dependencies:

- BOM completion
- ERP invoice ingestion
- POS product-level data

---

## 8. UI-System Alignment

All UI must follow:
→ mychair_ui_design_system.md

Rules:
- Consistent layout
- Financial clarity
- Minimal noise
- Operational tone

---

## 9. Critical Invariants (DO NOT BREAK)

1. Financial values are computed, not edited
2. Outbox guarantees message delivery
3. Domain logic stays outside UI
4. Modules remain loosely coupled
5. Naming consistency across DB and code

---

## 10. Next Execution Priorities (KIRO)

### Phase 1
- Quotation → Invoice conversion logic
- Payment persistence (Revenue Control)

### Phase 2
- Insights Story Engine
- Daily summaries

### Phase 3
- BOM implementation
- ERP ingestion normalization

### Phase 4
- COM ingestion pipeline
- Intelligence engine

---

## 11. Risks & Failure Modes

- Tight coupling between UI and logic
- Inconsistent naming conventions
- Missing idempotency in messaging
- Overloading insights with weak data

Mitigation:
- Strict layering
- Repository pattern enforcement
- Event-driven boundaries

---

## 12. Guidance for KIRO

When executing:

- Follow execution plans strictly
- Validate data contracts before coding
- Preserve architecture boundaries
- Avoid premature complexity

If unclear:
→ Stop and request clarification

---

## 13. Final Note

This system is evolving toward:

→ Operational Intelligence Platform

Every implementation must support that trajectory.

---

END OF DOCUMENT
