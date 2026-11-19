---
name: dental-brand-identity
description: Apply Clínica Dental Ramírez's official public-site color palette and protect its brand assets.
argument-hint: "[public component or color task]"
---

# Dental Brand Identity

Use this Skill for visual color decisions on the public Clínica Dental Ramírez website. It defines the current official public palette; it does not authorize a site-wide recolor or changes to admin, patient, doctor, or cashier interfaces.

## Official public palette

| Color | HEX | Semantic role |
|---|---|---|
| White-blue | `#F4FAFD` | Primary light public background and pale wave cap |
| Light aqua | `#D3F3F5` | Soft surfaces, section accents, and translucent wave layers |
| Bright aqua | `#79DADB` | Luminous highlights and wave transitions |
| Soft turquoise | `#39B3B6` | Secondary public accents and gradient transitions |
| Main turquoise | `#2BAAAC` | Primary public brand color and gradient start |
| Band midpoint | `#19999C` | Center transition in the official bottom-band gradient |
| Deep turquoise | `#169597` | Gradient end, emphasis, and darker public interaction states |

Keep the existing white and readable text/neutral tokens where appropriate. Do not substitute legacy dark-green turquoise values for these public palette roles.

## Color and gradient usage

- Public header accents, primary buttons, section details, cards, and decorative backgrounds should use these palette colors consistently. Use white-blue or existing white for light surfaces, light aqua for restrained panels, and turquoise for meaningful accents.
- The reference bottom band runs left to right: `#2BAAAC` → `#19999C` → `#169597`.
- Build the upper wave with two or three broad, overlapping Bézier shapes: a white-blue cap, a wide light-to-bright aqua transition, then the main turquoise band. Match the reference's wide asymmetrical sweep and let adjacent layers merge near the center; do not stack several narrow stripes.
- Begin the cap in the same near-white tone as its surrounding surface so the wave grows organically from the background. Use diagonal or vertical SVG gradients and gentle opacity; do not introduce a flat horizontal aqua rectangle or hard boundary above the waves.
- Keep the aqua luminous. Do not replace these colors with dark-green turquoise or use solid opaque stripes where the reference blends colors.
- A practical scoped CSS example:

```css
.public-component {
  --public-color-white-blue: #F4FAFD;
  --public-color-light-aqua: #D3F3F5;
  --public-color-bright-aqua: #79DADB;
  --public-color-soft-turquoise: #39B3B6;
  --public-color-main-turquoise: #2BAAAC;
  --public-color-band-midpoint: #19999C;
  --public-color-deep-turquoise: #169597;

  background: linear-gradient(
    90deg,
    var(--public-color-main-turquoise) 0%,
    var(--public-color-band-midpoint) 50%,
    var(--public-color-deep-turquoise) 100%
  );
}
```

- A practical translucent SVG wave example:

```svg
<linearGradient id="public-wave" x1="0%" y1="0%" x2="100%" y2="100%">
  <stop offset="0%" stop-color="#D3F3F5" stop-opacity=".96" />
  <stop offset="60%" stop-color="#79DADB" stop-opacity=".86" />
  <stop offset="100%" stop-color="#39B3B6" stop-opacity=".8" />
</linearGradient>
```

Keep gradients restrained and smooth. Do not use a flat fill where the approved reference calls for layered transitions.

## Legacy styles and scope

- Older public styles may still contain dark-green turquoise colors. Treat them as legacy, not as the official palette for new public visual work.
- Do not replace legacy colors globally or modify other application areas as a side effect. Apply the palette only to the explicitly requested public components and preserve unrelated existing styles.
- Keep the palette variables scoped to the public site or component; do not make them global design tokens unless separately approved.

## Brand assets and content

- Preserve the original clinic logo, including its original gold. Never recolor it, apply filters, redraw it, distort its aspect ratio, or replace it with a palette-colored approximation.
- Reuse dynamically loaded, administration-managed logos, social assets, and content through their existing components and API bindings. Do not hardcode substitutes or sample data.

## Accessibility and interaction states

- Check text and control contrast for every gradient position. White normal-size text needs at least 4.5:1 contrast; if an approved reference color falls short, use the smallest visually compatible darkening treatment and report the resulting contrast.
- Never apply a global black overlay or tint to the brand gradient to solve text contrast; it changes the luminous palette across the entire band. Prefer a verified accessible foreground such as `#002124` on the turquoise band (at least 4.65:1 against the specified stops), or a subtle localized text treatment if the reference requires white text.
- Preserve visible keyboard focus and distinct hover, active, and disabled states. Do not rely on color alone to communicate interaction or status.
- Reuse the `web-accessibility` Skill for full accessibility reviews and `responsive-design` for layout behavior. This Skill covers brand color decisions, not their workflows.

## Reuse by frontend and UI/UX agents

Before proposing or implementing public-site colors, frontend and UI/UX agents should invoke `/dental-brand-identity [public component or color task]`, then apply this palette to the approved scope only. Load `responsive-design` or `web-accessibility` separately when those concerns are part of the task.

## Activation

Invoke on demand in Copilot Chat with `/dental-brand-identity [public component or color task]`.
