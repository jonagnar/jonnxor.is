---
name: jonnxor-design
description: Use this skill to generate well-branded interfaces and assets for JonnXor (Jón Agnar Stefánsson's personal brand — Norse myth meets fantasy gaming, yellow/black/turquoise, three themes). Contains essential design guidelines, tokens, fonts, components, and the canonical site implementation.
user-invocable: true
---

Read the README.md file within this skill, and explore the other available files.

Key facts:
- Tokens live in `../assets/tokens.css` — three themes (`dawn`, `rune` default, `neon`)
  share ONE token vocabulary, applied via `data-theme` on `<html>` or any container.
  Never hardcode themed colors; always use the `--*` custom properties.
- Components live in `../assets/site.css` (classes: adaptive nav tiers — .nav-hud /
  .nav-realms / .nav-seek+.nav-mega / .jx-dock+.jx-sheet — plus .search-modal, .btn,
  .chip, .badge, .card, .tabs, .doc-table, kbd, .tip, .rune-divider, .theme-toggle,
  .nav-orb, .lang-toggle, …) and `../assets/site.js` renders the shared nav/footer/dock/
  search and exposes `window.JX` (setTheme, openSearch, daysUntil, icons, …).
  Grimoire doc content lives as data in `../assets/docs-data.js`.
- The eleven HTML pages in the project root are the canonical usage examples; copy a
  page's head boilerplate to start a new page. The nav is adaptive (HUD ≥1920 / realm
  dropdowns 1280–1919 / search-first 881–1279 / bottom dock ≤880) — site.js handles it.
- Design EVERY new component in all three themes. Dawn must work with zero glow; Neon
  must glow at rest (use `--ambient-card` / `--ambient-text`).
- Specimen cards in `cards/` show approved looks for type, colors, spacing, components,
  and brand marks.

If creating visual artifacts (slides, mocks, throwaway prototypes), copy assets out and
create static HTML files for the user to view. If working on production code, copy assets
and read the rules here to become an expert in designing with this brand.

If the user invokes this skill without other guidance, ask what they want to build,
ask some questions, and act as an expert designer who outputs HTML artifacts _or_
production code, depending on the need.
