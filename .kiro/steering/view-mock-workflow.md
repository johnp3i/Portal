---
inclusion: auto
---

# View Mock Workflow

## Rule

When speccing any feature that involves a **View** (Razor page, UI screen, dashboard, email template, or any user-facing HTML), **always ask the user** whether they want to create a standalone HTML mock first before proceeding with the spec.

## Prompt

Ask:

> "This feature involves a view/UI. Would you like me to create an HTML mock first so we can use it as a visual reference during implementation?"

## Behaviour

- If the user says **yes**: create a standalone `.html` file in `.kiro/mocks/{feature-name}/` with the full visual mock (inline CSS, no external dependencies, self-contained). Then proceed with the spec.
- If the user says **no**: proceed directly with the spec workflow.
- The mock should follow the MyChair Design System (colours, typography, spacing, card patterns).
- Mocks are reference-only — they are not deployed or included in the build.
