# Nav — server-render as an Astro component

**Date:** 2026-06-23
**Status:** Design approved, pending spec review
**Repo:** `code/jonnxor.is`

## Context

The site chrome is being migrated from the standalone `design/` mockups (one shared
`assets/site.js` that builds all chrome client-side) into the Astro app. The migration is
partly done:

- Pages ported to `.astro`, hrefs repointed to clean routes, grimoire search served via a
  JSON endpoint (`4700227`).
- **Footer extracted to a server-rendered `Footer.astro`** (`606ebdc`) — the precedent for
  this work. `footHTML()` in `site.js` is now dead code.

The **nav is the last piece still rendered client-side.** `Base.astro` ships an empty
`<header class="site-nav"></header>`, and `site.js` `init()` injects `navHTML()` into it and
appends the mobile dock to `<body>`, then wires all behavior.

## Goal

Move the nav's **markup** out of `site.js` into a server-rendered `Nav.astro`, keeping all
**behavior** in `site.js`. No visual or UX change — the four-tier adaptive design is
preserved exactly. Outcome: nav is in the initial HTML (no load-in flash, better SEO/perf),
and the Footer-style migration is complete.

## Non-goals

- No redesign of the nav (tiers, links, controls, search all stay as specified in the
  design-system README "Navigation & search").
- No change to `site.css`, tokens, or any page.
- No new framework integration / islands — stays vanilla, consistent with the project.

## Architecture

### `src/components/Nav.astro` (new)

Takes a `page` prop (the active page id). Server-renders exactly what `navHTML(current)` +
`dockHTML(current)` produce today:

- `<header class="site-nav">` containing `.nav-inner`: logo, `.nav-hud` (9 numbered links),
  `.nav-realms` (3 grouped dropdowns), `.nav-seek` pill, `.nav-controls` (search btn, ☰ menu
  btn, lang toggle, theme triple, theme orb), and `.nav-mega` panel. CSS media queries
  decide which tier is visible, so all tiers render at once — same as today.
- The mobile **dock + sheet** (`.jx-sheet`, `.jx-dock`) as **siblings emitted after the
  header**, NOT nested inside it.
- Active state (`.here` on links, `.active` on the current realm top) computed server-side
  from `page` — replaces the runtime `id === current` checks.

Component carries its own render data (pages, realm groups, dock slots, page descriptions,
theme/lang lists, icons) mirroring the `site.js` constants it replaces.

### `src/layouts/Base.astro` (edit)

Replace the empty `<header class="site-nav"></header>` (line 35) with `<Nav page={page} />`
and import the component. Update the stale comment on line 34.

### `public/assets/site.js` (edit — remove markup, keep behavior)

- Delete `navHTML()`, `dockHTML()`, and the dead `footHTML()`.
- In `init()`, delete the render block (the `header.innerHTML = navHTML(...)` and the dock
  build/append, ~lines 441–449), plus the now-unused `current` local.
- **Keep everything else unchanged:** theme/lang toggles, orb cycle, realm dropdown toggles,
  mega menu, `nav-seek`/`nav-search` → search, dock "More" sheet, outside-click + Escape
  close, reveal-on-scroll, dragonfly egg. All bind via `querySelector` and are agnostic to
  whether the DOM was injected or server-rendered.
- **Keep** `window.JX`, `ICONS`, `PAGES`, `PAGE_KEYWORDS`, `THEMES`, `LANGS` — still used by
  search, the theme orb, the dragonfly egg, and page-level consumers.

## Key constraints / faithful-port details

- **Dock must stay body-level.** The header uses `backdrop-filter`, which traps
  `position: fixed` descendants — the reason `site.js` appends the dock to `<body>` today.
  `Nav.astro` emits the header and the dock/sheet as separate root elements (Astro adds no
  wrapper), so placing `<Nav>` directly in `<body>` keeps all three as direct children of
  `<body>`. DOM structure is identical to today's.
- **Theme orb + active button states stay JS-painted.** The orb icon is set by `setTheme`
  on load; `.theme-toggle button.active` likewise. Server-render the `rune` (default)
  active states as the baseline so the common case has no flash; the inline pre-paint script
  + JS correct persisted non-default themes instantly, exactly as today.
- **Search modal stays JS-built** (`setupSearch()` appends it to `<body>`). Out of scope to
  port; its content is fully dynamic. (Granularity option A.)

## Decisions

- **Granularity A** (approved): a single `Nav.astro` owns header + dock + sheet. Mirrors the
  one-component `Footer.astro`. No separate `Dock.astro` / `SearchModal.astro`.
- **Data duplication accepted:** `Nav.astro` holds render data; `site.js` keeps its small
  `PAGES`/`PAGE_KEYWORDS` for search. Only the page list overlaps. A shared `src/data/nav.ts`
  is a possible later cleanup but is out of scope — `site.js` lives in `public/` and can't
  import bundled modules without first moving into `src/`.

## Files touched

| Action | File |
|---|---|
| Add | `src/components/Nav.astro` |
| Edit | `src/layouts/Base.astro` (use `<Nav>`, import, fix comment) |
| Edit | `public/assets/site.js` (remove 3 builders + render block) |

## Verification

- `pnpm build` clean.
- `pnpm dev`, then check:
  - Each tier by resizing: HUD ≥1920 · Three Realms 1280–1919 · Seeker 881–1279 · dock ≤880.
  - Theme switch (☀ ☾ 🐉) + persistence across reload; orb cycle.
  - Realm dropdowns open/close; ☰ mega menu; outside-click + Escape close.
  - Search via `/`, ⌘/Ctrl-K, nav search button, and the Seeker pill.
  - Mobile dock + "More" sheet (incl. theme/lang toggles inside it).
  - Correct active-page highlight on every route (home, about, cv, portfolio, blog,
    blog/[slug], docs, games, wallpapers, countdowns; 404 shows none).
  - Nav present in initial HTML / no load-in flash (view source or throttle).

## Out of scope / future

- Shared `src/data/nav.ts` single-source-of-truth for nav + search.
- Porting the search modal shell to `SearchModal.astro`.
- The lang toggle remains visual-only (unchanged).
