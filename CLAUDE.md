# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project status

`jonnxor.is` — the personal website of Jón Agnar Stefánsson ("JonnXor"), an Icelandic
full-stack developer. The brand is **Norse myth meets fantasy gaming** (yellow/black/turquoise,
three themes).

This is an **early-stage repo** (no commits yet at init time). Read the state before assuming a stack:

- **`design/` is the only real content** — a build-free static design system plus 11 canonical
  example pages that ARE the reference implementation of the brand (no prior codebase or Figma).
- **`src/` and `docs/` are empty placeholders.** The production site / generated docs have not
  been built yet — there is no framework chosen, no application code.
- **`.gitignore` is the stock .NET / Visual Studio template** (with `node_modules/` added). It does
  **not** imply a .NET stack — there is zero .NET code. Don't infer the stack from it.
- Structured work is expected to use **GSD** (`gsd-*` skills); `.planning/` does not exist yet.
  Per project memory a prior planning init was intentionally discarded for a fresh start — do not
  restore old planning artifacts unasked.

## Running / previewing

There is **no build, bundler, package manager, linter, or test suite.** Pages are hand-authored
static HTML. Preview by serving `design/` over HTTP — do **not** open via `file://`, because the
browser-side Babel transpile of the `.jsx` files and the CDN `<script>`s require it:

```bash
cd design && python3 -m http.server 8000   # then open http://localhost:8000/
```

## Architecture — the static design system (`design/`)

The system is documented in depth in `design/design-system/README.md` (brand, voice, visual rules,
navigation spec) and `design/design-system/SKILL.md` (the `jonnxor-design` Claude skill entry point).
Read those before doing design work. The load-bearing structure:

**One token vocabulary, three themes.** All visual values are CSS custom properties in
`assets/tokens.css`. Three themes — `dawn`, `rune` (default), `neon` — reassign the *same* `--*`
vocabulary and are applied with `data-theme` on `<html>` (or any container, to scope locally).
Two invariants: **never hardcode a themed color** (always use the `--*` properties), and **every
component must be designed in all three themes** (Dawn has zero glow; Neon glows at rest via
`--ambient-card` / `--ambient-text`).

**Page bootstrap contract.** To add a page, copy the `<head>` boilerplate from any existing page.
Each page is:
- `<html data-theme="rune">` + a tiny inline pre-paint script that reads `localStorage['jx-theme']`
  and sets `data-theme` *before* the stylesheets (anti-FOUC), then `tokens.css` + `site.css`, then
  page-specific `<style>`.
- `<body data-page="…">` (this attribute drives the nav's active-page marking), with optional
  `data-dragonfly="on"`.
- Empty `<header class="site-nav"></header>` and `<footer class="site-footer"></footer>` placeholders
  that `site.js` fills in.
- `<script src="assets/site.js">` at the end, optionally followed by the React UMD + Babel +
  `.jsx` tweak scripts.

**`assets/site.js` is the runtime core.** It owns *all* shared chrome — render it from `data-page`,
do not hand-write nav/footer markup. It builds the **adaptive 4-tier nav** (HUD ≥1920 / Three-Realm
dropdowns 1280–1919 / search-first Seeker 881–1279 / bottom Quick-slot dock ≤880 — there is **no
burger menu**), the site-wide **search modal** (over the page list + Grimoire docs, opened via the
nav button, `/`, or `Ctrl/⌘K`), persists theme/lang/glow/scanlines in `localStorage`, and exposes
`window.JX` = `{ setTheme, getTheme, setLang, setScanlines, setGlow, daysUntil, openSearch, icons }`.

**Content as data.** Grimoire/docs content lives in `assets/docs-data.js` as `window.JX_DOCS` — an
array of `{ id, realm, game, cat, title, tags, updated, body }` where `body` is an HTML string whose
`<h2>`s become the table-of-contents. Search and `docs.html` consume it. Icons are hand-drawn inline
SVG in `site.js` (`JX.icons`) — no icon font, no icon CDN.

**The `.jsx` files are transpiled in the browser**, not built. `assets/tweaks.jsx` /
`tweaks-panel.jsx` implement the optional dev "Tweaks" panel (theme / glow / scanlines) and are loaded
via `<script type="text/babel">` with React 18 UMD + `@babel/standalone` from unpkg. This is a layer
on top of the static site, not a React application.

**Design-system specimens.** `design/design-system/cards/*.html` are standalone fragments (approved
looks for type, color, spacing, components, brand marks) surfaced in the design-system view.

**Conventions to preserve.** Sections are tagged with `data-screen-label="…"` (used to drive the
captures in `design/screenshots/`, generated outside the repo). The three-state theme switcher
(sun ☀ / moon ☾ / dragon 🐉) and gold active-page marking are signature elements — keep them.
