# Authorized login molar artwork (pending source)

No authorized model or prerender is currently included. The existing SVG tooth remains the fallback. Do not download viewer geometry or substitute a generated asset claiming it is the original.

Requested source: “tooth molar” by rebel, https://sketchfab.com/3d-models/tooth-molar-c258255931494c63bd288a0e6469d3cb . Obtain an authorized download and record its license and attribution requirements before distributing derivatives.

## Drop-in files

Required: `apps/web/src/assets/login/tooth-molar-login.webp`
Optional: `apps/web/src/assets/login/tooth-molar-login.avif`
Optional mobile: `apps/web/src/assets/login/tooth-molar-login-mobile.webp`

After adding the files, rebuild. Vite discovers these exact filenames and emits external hashed assets (never base64 in JavaScript). With no WebP supplied, there are no missing-asset network requests. AVIF is preferred on desktop when present; mobile WebP is preferred below 768px. The form is independent of artwork loading. A failed image retains the original SVG tooth.

## Offline production specification

Use the licensed original in an offline renderer, not in the login runtime. Frame only the tooth on a transparent 800 × 1040 canvas (mobile 400 × 520), no shield, platform or orbit: those already exist in SVG. Preserve alpha and the identical crop across formats. Keep crown facing the camera in a slight three-quarter view, roots at the bottom, occupying the canvas without excess padding.

Enamel: warm white #F7F7F2, roughness 0.35, metalness 0, subtle clearcoat if useful. One soft key, weak fill and ambient lighting; no blown-out enamel. Existing SVG provides contact shadow. Export antialiased transparent WebP, optionally AVIF. Tune compression visually; target <=250 KB desktop and <=100 KB mobile. Check edges on the actual cyan background.

The static strategy introduces no runtime mesh, GPU loop, 3D engine or geometry transfer. An optimized GLB is therefore not a runtime requirement. If creating a GLB intermediate, preserve silhouette at 12–25k triangles (30k ceiling), use Meshopt only if available in the offline toolchain, remove unused data and keep textures <=1024px; do not distribute the heavy source with the web app.

## Acceptance after the authorized files arrive

Record source license, source/final sizes, source/final triangle counts if mesh optimization was used, image dimensions and codec settings. Do not report these values before producing the asset. Confirm crown/root orientation, shield overlap, alpha edges, mobile crop and load/failure behavior at all requested viewports. No claim of visual replacement can be made while only the fallback is available.
