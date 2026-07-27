---
inclusion: auto
---

# Implementation Workflow

## Rule: Specs First, Always

When the user approves a design (mockup or proposal) and says "proceed", "go ahead", "lock it", or similar — **always create specs** (requirements → design → tasks) before implementing.

Direct implementation without specs is only appropriate for:
- Trivial CSS fixes (padding, colour, font-size)
- Single-line bug fixes
- Adding a nav link or button
- Renaming a label or placeholder

For anything that involves a new page, new controller, new service, database changes, or multi-file work: **create specs first**.

## Prompt

When in doubt, explicitly ask:

> "Would you like me to create specs for this feature, or is this simple enough to implement directly?"

Never assume "proceed" means "skip specs and implement directly."

## Why

- Specs document what was built and why
- Specs enable future agents to understand decisions
- Specs create an audit trail of features
- Direct implementation leaves no documentation behind

## Spec Location

All specs live in `.kiro/specs/<feature-name>/` with:
- `requirements.md` — what the feature must do
- `design.md` — how it will be built (architecture, data flow)
- `tasks.md` — ordered implementation steps
