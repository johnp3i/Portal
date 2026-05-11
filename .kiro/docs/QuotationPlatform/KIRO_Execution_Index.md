# KIRO Execution Index — MyChair Platform
Version: v1.0  
Purpose: Orchestrate all documents into a clear execution pipeline for KIRO agents

---

## 1. Overview

This document defines:
- Execution order
- Dependencies
- Validation checkpoints

It ensures:
→ No ambiguity  
→ No architectural drift  
→ Deterministic progress

---

## 2. Required Documents

KIRO must load:

1. Architecture_State.md  
2. mychair_handoff.md  
3. mychair_ui_design_system.md  
4. Feature-specific specs (e.g., quotation_engine.md, insights spec, etc.)

---

## 3. Execution Phases

---

### Phase 1 — Foundation Validation

**Goal:** Ensure architecture integrity before coding

Steps:
- Read Architecture_State.md
- Validate:
  - Layers
  - Bounded contexts
  - Data contracts

Output:
→ Confirmed architecture alignment

---

### Phase 2 — UI Alignment

**Goal:** Ensure UI consistency

Steps:
- Read mychair_ui_design_system.md
- Validate:
  - Colors
  - Layout
  - Components

Output:
→ UI compliance checklist

---

### Phase 3 — Module Selection

**Goal:** Select implementation target

Possible modules:
- Quotation Engine
- Revenue Control
- Insights
- JDS integration

Output:
→ Selected module + scope definition

---

### Phase 4 — Implementation

**Rules:**
- Follow Repository pattern
- Keep domain logic separate
- No UI-driven logic

Steps:
1. Define entities
2. Define services
3. Implement persistence
4. Connect UI

Output:
→ Working module

---

### Phase 5 — Validation

Checklist:
- Financial values computed (not manual)
- No coupling between modules
- Naming consistency
- UI follows design system

---

### Phase 6 — Integration Readiness

Validate:
- Can module emit events?
- Compatible with COM future?

Output:
→ Integration-ready module

---

## 4. Constraints

DO NOT:
- Skip phases
- Modify architecture
- Introduce generic UI patterns

---

## 5. Failure Handling

If any step is unclear:
→ STOP  
→ Request clarification  

Never assume.

---

## 6. Execution Principle

This is not feature development.

This is:
→ System evolution toward Operational Intelligence

---

END OF DOCUMENT
