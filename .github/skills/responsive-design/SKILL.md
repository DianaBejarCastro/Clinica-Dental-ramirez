---
name: responsive-design
description: Plan, implement, or review responsive behavior in this project's React and CSS interfaces. Use when a screen must work across phone, tablet, and desktop sizes or when investigating layout overflow.
argument-hint: "[screen or component to review]"
---

# Responsive Design

Use this Skill to make a focused interface work across viewport sizes while preserving the project's existing visual language and behavior.

## Steps

1. Read the repository `AGENTS.md`. Inspect the target screen, parent layout, related CSS, nearby components, and any responsive tests or documented breakpoints.
2. Record the current layout constraints and identify actual failure points, such as horizontal overflow, clipped controls, unreadable text, awkward navigation, or inaccessible touch targets.
3. Define the important viewport states for the screen. Include a narrow phone (around 320–390 CSS px), a tablet-sized viewport (around 768 px), and a desktop viewport; adjust these values to match the real use case.
4. Prefer fluid sizing, flexible grid or flex layouts, wrapping, and the project's existing breakpoints and CSS tokens. Reuse existing components. Avoid fixed widths and one-off overrides when an existing pattern solves the problem.
5. Preserve content order and functionality. Check forms, tables, dialogs, navigation, long labels, and empty/error states at narrow widths. Consider safe-area insets and reduced motion where relevant to the component.
6. If implementing, make only the approved, scoped changes. If proposing or reviewing, describe the evidence and recommendation without editing.
7. Validate the changed behavior at the selected viewport sizes using the repository's existing browser or test workflow when available. Report what was actually checked; do not infer visual success from a build alone.

## Report

Summarize affected screens, responsive behavior by viewport, existing tokens/components reused, validation performed, and any unresolved trade-offs.

## Activation

- Invoke on demand in Copilot Chat with `/responsive-design [screen or component]`.
- Copilot may also load it automatically when a request matches the description. Discovery does not guarantee automatic selection; invoke it explicitly when the workflow is required.
