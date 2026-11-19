---
name: web-accessibility
description: Audit or improve accessibility in this project's React interfaces, including semantics, keyboard access, focus, forms, contrast, and motion. Use for accessibility reviews or UI implementation tasks.
argument-hint: "[screen, component, or interaction to review]"
---

# Web Accessibility

Apply this workflow to the requested interface without introducing unnecessary ARIA or changing behavior beyond the approved scope.

## Steps

1. Read the repository `AGENTS.md`. Inspect the target component, surrounding layout, styles, tests, and existing accessibility conventions.
2. Check whether native HTML elements express the interaction correctly. Verify headings, landmarks, lists, buttons, and links have appropriate semantics and names.
3. Trace keyboard use through the flow: logical tab order, operable controls, visible focus, no keyboard traps, and a usable escape or dismissal path where applicable.
4. Check forms and dynamic feedback: programmatic labels, required/invalid states, understandable errors, and status announcements that do not rely on color alone.
5. Review contrast of text and meaningful controls, text resizing and reflow, touch-target usability, and reduced-motion behavior where applicable. Identify issues from the actual styles and content rather than assuming compliance.
6. Prefer semantic HTML and existing patterns. Add ARIA only when native semantics are insufficient, and verify that it accurately describes the interaction.
7. If implementing, make focused approved changes and add or update targeted tests using accessible queries such as roles and labels. Run relevant existing checks and report their actual results. If only auditing, do not edit.

## Report

List actionable findings with affected files and user impact, followed by the recommended fix and validation approach. Clearly distinguish verified issues from items that require manual assistive-technology testing.

## Activation

- Invoke on demand in Copilot Chat with `/web-accessibility [screen, component, or interaction]`.
- Copilot may also load it automatically when a request matches the description. Invoke it explicitly when accessibility review is a required acceptance criterion.
