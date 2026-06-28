> **DRAFT — for validation.** Intent inferred from existing artifacts; confirm before relying on it.

# FRD — Frontend (Astro presentation layer)

**Project:** jonnxor.is
**Layer:** Presentation (Astro 6, static SSG, vanilla — no UI framework)
**Source of truth for this draft:** the code under `src/`, `public/assets/`, `astro.config.mjs`, and the i18n design notes in `.planning/`.

## 1. Overview

The site is a fully static Astro 6 site (`astro@^6.4.7`, `package.json`) with **no UI framework** — pages are `.astro` files with inline `<style is:global>` and inline vanilla-JS `<script is:inline>`. Behaviour shared across pages lives in one hand-written file, `public/assets/site.css` / `public/assets/site.js`, served from `public/` (not bundled). Build is `astro build`; CI builds on Forgejo and deploys to Vercel (`.forgejo/workflows/ci.yml` runs `pnpm run build`).

The presentation layer owns: the chrome (nav, footer, dock, search), the three-theme design system, i18n routing/switching, and the rendering of two content collections (blog, grimoire). All content is intended to be Directus-owned (see `frd-content.md`); the frontend consumes a committed snapshot, never Directus directly.

`[confirm: the 4-layer architecture (Directus content · Astro UI · .NET API · Blazor admin) from `.planning/2026-06-24-i18n-directus-design.md` is the governing model. Only the Astro + Directus foundations exist in the repo today; .NET API/Blazor are not present.]`

## 2. Pages (routes)

Enumerated from `src/pages/`. All are static. Each imports `src/layouts/Base.astro` and passes a `page` id used for nav active-state.

| Route | File | Title / "realm" name | Nature |
|---|---|---|---|
| `/` | `src/pages/index.astro` | Home (`page="home"`, `dragonfly="on"`) | Hero + dragon constellation SVG, three "paths" cards, "Now" strip, latest-posts list. Content currently **hardcoded inline** (incl. post links). |
| `/about` | `src/pages/about.astro` | The Saga of Jón | Prose + "character sheet" card + cover letter. Hardcoded inline. |
| `/cv` | `src/pages/cv.astro` | The Record of Deeds | Print-optimized one-pager; `window.print()` button; dedicated `@media print` light-theme override. Hardcoded inline. |
| `/portfolio` | `src/pages/portfolio.astro` | The Forge | 6 project cards (placeholder cover art via CSS gradients). Hardcoded inline. |
| `/blog` | `src/pages/blog.astro` | The Codex | Lists `blog` collection entries for current locale; category filter chips; locale-aware dates. |
| `/blog/<slug>` | `src/pages/blog/[slug].astro` | (post title) | `getStaticPaths` over unique slugs; renders `<Content />`; prev/next pager. Throws build error if a slug has no `en` base. |
| `/docs` | `src/pages/docs.astro` | The Grimoire | Client-side reader fed by `grimoire` collection serialized to `window.JX_DOCS`; sidebar (realm/game/text filters), reading pane, TOC scroll-spy, prev/next, deep-link `?doc=`, `localStorage('jx-doc')`. |
| `/games` | `src/pages/games.astro` | The Game Hall | Tabbed tracker (Upcoming/Playing/Played/Favorites); game list **hardcoded in inline JS**; uses `window.JX.daysUntil`; `localStorage('jx-games-tab')`. |
| `/countdowns` | `src/pages/countdowns.astro` | The Reckoning | Live countdown clocks + "count-up" days-of-life tickers; data **hardcoded in inline JS**; `setInterval(tick, 100)`. |
| `/wallpapers` | `src/pages/wallpapers.astro` | The Hoard | Masonry gallery (placeholder gradient art), tag filters, lightbox, real PNG download via `<canvas>`; data **hardcoded in inline JS**. |
| `/404` | `src/pages/404.astro` | ÞÚ DÓST ("You died") | Souls-style 404; animated "souls lost" counter. |
| `/assets/docs-data.json` | `src/pages/assets/docs-data.json.ts` | — | Static JSON endpoint: the whole `grimoire` collection, for the site-wide search lazy-fetch. |

**Requirements**

- The site **shall** expose exactly the routes above as static pages; `astro build` shall emit them without a server runtime.
- The site **shall** render a custom `/404` page in the site's visual language.
- `/blog/<slug>` **shall** be generated only for slugs present in the `blog` collection (drafts excluded) and **shall fail the build loudly** when a referenced post lacks an `en` base (`src/pages/blog/[slug].astro` throws `"Every post needs an en base"`).
- `/cv` **shall** produce a clean, single-column, light-themed document when printed, hiding nav/footer/actions (`@media print` block in `cv.astro`).
- `/assets/docs-data.json` **shall** serialize the full grimoire collection as the single search index source.
- `[confirm: home "latest posts", games, countdowns, wallpapers, portfolio, about, cv content is intended to migrate to Directus-owned snapshot data (per design doc's `pages`/`games` collections), but is hardcoded inline today.]`

## 3. Layout & chrome

### Base layout — `src/layouts/Base.astro`
Props: `title`, `page`, `dragonfly?`, `theme?` (default `"rune"`).
- Sets `<html lang={Astro.currentLocale ?? "is"} data-theme={theme}>`.
- Inlines a **pre-paint theme script** (`is:inline`) that reads `localStorage('jx-theme')` and sets `data-theme` before first render, to avoid a flash of the wrong theme.
- Links three stylesheets in order: `/assets/fonts.css`, `/assets/tokens.css`, `/assets/site.css`.
- Renders `<Nav page={page} />`, the page `<slot />`, `<Footer />`, then `/assets/site.js` (`is:inline src`), then a `<slot name="end" />` for page-specific scripts that run **after** `site.js` (so `window.JX` is ready). Also exposes `<slot name="head" />`.

**Requirements**
- Every page **shall** render through `Base.astro` so chrome, fonts, tokens, and theme behaviour are consistent.
- The layout **shall** apply the persisted theme before first paint (no theme flash).
- Page-specific scripts **shall** run after `site.js` via `slot="end"`, relying on the global `window.JX` API (`setTheme`, `daysUntil`, etc. — defined in `site.js`).

### Nav — `src/components/Nav.astro`
Server-rendered (was previously injected client-side). Behaviour wiring (theme/realm dropdowns/mega menu/search/dock) stays in `site.js`, bound via `querySelector`.
- Single `PAGES` array drives a desktop "HUD" link row, three **realm dropdown groups** (`work` = portfolio/cv/about, `codex` = blog/docs, `hall` = games/wallpapers/countdowns), a mega-menu grid, a mobile bottom **dock** (`DOCK_SLOTS`: home/forge/grimoire/hall + "more" sheet), inline SVG icons, a language toggle, and a theme toggle + cycle "orb".
- Labels and descriptions come from the i18n dictionary (`t('nav.<id>')`, `t('navDesc.<id>')`, `t('realm.<key>')`, `t('dock.*')`). Links built with `getRelativeLocaleUrl(locale, path)`.

### Footer — `src/components/Footer.astro`
Server-rendered; all strings via `t('foot.*')`; links locale-aware. Includes a decorative star-constellation SVG and contact rows (raven/github/linkedin/hall/status).

**Requirements**
- Nav and footer **shall** be server-rendered with all labels sourced from the i18n dictionary and all internal hrefs locale-aware (`getRelativeLocaleUrl`).
- Nav **shall** show active state for the current `page` across HUD, realm panels, mega grid, and dock.
- The chrome **shall** present three navigation surfaces — desktop HUD/realms, mobile dock + "more" sheet, and a `/`-triggered search modal (appended by `site.js`).

## 4. Design system

### Tokens — `public/assets/tokens.css`
One token vocabulary, three themes selected by `[data-theme]`:
- **`rune`** — dark, default (`:root` + `[data-theme="rune"]`): near-black bg, electric turquoise (`--tq`) + golden-yellow (`--gold`) accents, glow tokens active.
- **`dawn`** — light ("Icelandic daylight"): warm paper bg, muted teal/gold, glow radius `0`.
- **`neon`** — gamer/synthwave: pink takes the accent channel, ambient always-on glow (`--ambient-card`/`--ambient-text`) and a CRT scanline overlay (`body::after`, `--scanline-a`).
- Tokens cover colour (bg/surface/ink/line + tq/gold families + `on-*` contrast pairs), typography (`--font-display/-body/-mono`), radii, an 8-step `--space-*` scale, `--nav-h`, `--maxw`, `--ease-out`, shadow/glow, and per-theme `--aurora` radial-gradient backdrops.
- Theme blocks use **bare `[data-theme="…"]`** (not `html[...]`) so any container can be re-scoped (used by design-system specimen cards). `[confirm: a design-system / specimen page exists in `design/` outside `src/` — not a routed page.]`
- Brand identity: **yellow · black · turquoise**, "JONN**XOR**" wordmark, dragon/rune/Norse motif.

### Themes runtime — `public/assets/site.js`
- `setTheme(t)` sets `data-theme`, persists `jx-theme`, repaints the orb icon; theme toggle buttons (`data-set`) and the orb cycle through `THEMES`.
- Pre-paint handled in `Base.astro`; `site.js` re-applies on load (`read('jx-theme') || getTheme()`).

### Fonts — `public/assets/fonts.css` + `public/assets/fonts/*.woff2`
- **Self-hosted** (`@font-face`, `font-display: swap`, `unicode-range` subsetting): **Orbitron** (display, weights 500–900), **Montserrat** (body, 400–700 + italic), **JetBrains Mono** (mono, 400/600). `.woff2` files committed under `public/assets/fonts/`.

**Requirements**
- The site **shall** support exactly three themes — `dawn` (light), `rune` (dark, default), `neon` (gamer) — selected via a single `data-theme` attribute and a shared CSS-variable token set.
- All page/component styling **shall** consume tokens (`var(--…)`) rather than hardcoded colours, so a theme switch needs no per-page CSS. (Hardcoded gradient art on portfolio/games/wallpapers is intentional decorative cover art.)
- The selected theme **shall** persist in `localStorage('jx-theme')` and apply before first paint.
- Fonts **shall** be self-hosted from `public/assets/fonts/` (no third-party font CDN at runtime) with `font-display: swap` and `unicode-range` subsetting.
- `neon` **shall** add ambient glow + scanline effects; `dawn` **shall** disable glow (radius `0`); a reduced-motion media query **shall** neutralize animations/transitions (`tokens.css`).
- `[confirm: scanlines can be disabled via `html[data-theme="neon"][data-scanlines="off"]` — confirm there is a UI control to toggle this, or it is design-time only.]`

## 5. Internationalization (i18n)

### Config — `astro.config.mjs`
```
i18n: { locales: ['is','en','ja'], defaultLocale: 'is',
        routing: { prefixDefaultLocale: false, fallbackType: 'rewrite' },
        fallback: { en: 'is', ja: 'is' } }
markdown: { syntaxHighlight: false }
```
- `is` is the default at the bare root `/`; `en` at `/en/`, `ja` at `/ja/`.
- Markdown `syntaxHighlight: false` so code blocks are theme-aware `<pre><code>` styled by site CSS (TODO in config: swap in Expressive Code).

### Dictionary & fallback — `src/i18n/ui.ts`, `src/i18n/utils.ts`
- `ui` holds **chrome/UI strings only** (nav labels + descriptions, realm/dock/theme labels, search/menu strings, footer copy). **English is complete and authoritative**; **Icelandic is an AI first draft** (entries tagged `// review`); **Japanese is intentionally partial** (nav nouns only).
- `useTranslations(lang)` returns `t(key)` resolving **lang → English → the key itself** (`utils.ts`).
- `stripLocale(pathname)` strips a leading `en`/`ja` segment to get the default-locale base path (used by the switcher).
- `localeNames` powers the switcher (each language shown in its own name: Íslenska / English / 日本語).

### Content fallback — `src/i18n/localized.ts`
- `localizedEntry(entries, slug, locale)` returns the entry for `(slug, locale)` else the **English-fallback** entry — used by `blog.astro`, `blog/[slug].astro`, `docs.astro`.
- `uniqueSlugs(entries)` dedupes locale entries to one set of slugs.
- **English-base invariant** (`.planning/2026-06-26-authoring-en-base-invariant.md`): every post/doc must have an `en` translation; blog fails the build without it, grimoire silently drops the entry.

### Switcher & dates
- Nav/footer language toggle buttons carry `data-lang` + `data-href={getRelativeLocaleUrl(l, basePath)}`; `site.js` navigates to the locale-equivalent URL and persists `jx-lang`. `[confirm: persistence key/behaviour — design specifies `jx-lang` + active-state sync on load.]`
- Dates format per locale: `is-IS` / `en-GB` / `ja-JP` (`blog.astro`, `blog/[slug].astro`).

**Requirements**
- The site **shall** serve three locales — `is` (default, unprefixed `/`), `en` (`/en/`), `ja` (`/ja/`) — per `astro.config.mjs`.
- Every UI string and every content entry **shall** resolve to a value via **lang → English → key/undefined** fallback, so no locale shows a 404 or blank for missing translations (graceful fallback resolved at lookup time, not via Astro's built-in fallback alone).
- English **shall** be the authoritative base/fallback language for both UI strings and content.
- The language switcher **shall** navigate to the locale-equivalent of the current page and **shall** remember the explicit choice across reloads.
- `<html lang>` **shall** reflect the active locale; dates **shall** format per locale.
- `[confirm: the design doc plans locale-parameterized shared page components + `src/pages/[locale]/*` routes via `getStaticPaths` to avoid file triplication. This is NOT in the repo today — only `is`-base pages and collection fallback exist. Confirm whether `/en/` and `/ja/` route prefixes currently resolve for the static (non-collection) pages, or rely solely on `fallbackType: 'rewrite'`.]`

## 6. Content collections (presentation side)

Defined in `src/content.config.ts` (Astro Content Layer, `glob` loader). Consumed by pages; produced/owned by Directus (see `frd-content.md`).

### `blog` — "The Codex"
- Loader: `glob({ pattern: '**/*.md', base: './src/content/blog', generateId: localeEntryId })`.
- One file **per locale**: `<slug>.<locale>.md`. `localeEntryId` (`src/content/loaders.ts`) keeps the locale suffix in the id so `<slug>.en` and `<slug>.is` don't collide (the Phase-2 bug it guards against).
- Schema (Zod): `title, date (coerced), category, excerpt, readTime, draft (default false), slug, locale ('is'|'en'|'ja')`.
- Rendered as Markdown via `render(post)` → `<Content />`; code blocks styled by site CSS (no Shiki).

### `grimoire` — "The Grimoire"
- Loader: `glob({ pattern: '**/*.yaml', base: './src/content/grimoire', generateId: localeEntryId })`.
- One file per locale: `<slug>.<locale>.yaml`. `body` is **hand-authored rich HTML** injected as-is by the client reader (hence YAML, not Markdown).
- Schema: `slug, locale, order, realm ('games'|'code'|'life'), game?, cat, title, tags[], updated (string), body (string)`.
- `docs.astro` localizes at build time, remaps id → `slug` (locale-stable deep-links), sorts by `order`, JSON-escapes `<`, and ships as `window.JX_DOCS`.

**Requirements**
- The site **shall** define `blog` and `grimoire` collections in `src/content.config.ts`, each one-file-per-locale with a locale-suffixed id (`localeEntryId`), validated by a Zod schema at build time (a malformed/missing required field fails the build).
- `blog` bodies **shall** render as Markdown with theme-aware code styling (`syntaxHighlight: false`); `grimoire` bodies **shall** be treated as trusted pre-rendered HTML injected client-side. `[confirm: grimoire body HTML is authored by the site owner only — it is injected without sanitization, so untrusted authors must not be allowed.]`
- The grimoire reader **shall** support realm/game/text filtering, a per-doc TOC with scroll-spy, prev/next paging, `?doc=<slug>` deep-links, and a persisted reading position (`localStorage('jx-doc')`), all locale-stable across language switches.
- `/docs` **shall** ship the grimoire inline as `window.JX_DOCS`; all other pages **shall** lazy-fetch `/assets/docs-data.json` for the global `/`-search (`site.js`).

## 7. Cross-cutting / non-functional

- **Static-only:** no SSR/adapter; CI = checkout → `corepack enable` → `pnpm install --frozen-lockfile` → `pnpm run build` (`.forgejo/workflows/ci.yml`). Build must never query Directus.
- **Tests** (`tests/`, Vitest — `vitest.config.ts`): `content/config-wiring`, `content/loaders`, `content/post-markdown`, `content/grimoire-yaml`, `i18n/localized`, `i18n/utils` guard id-derivation, fallback logic, and YAML round-trip determinism.
- **Accessibility/motion:** `aria-*` on nav/search/dialogs; `prefers-reduced-motion` honoured globally.
- **No-JS posture:** chrome and content are server-rendered; games/countdowns/wallpapers/docs reader and search are JS-dependent. `[confirm: graceful degradation for JS-off is acceptable for the interactive pages.]`

**Requirements**
- The build **shall** be reproducible from the committed repo alone (snapshot + lockfile), with no network/Directus dependency.
- Content-layer invariants (locale id derivation, English fallback, YAML date quoting) **shall** remain covered by the Vitest suite.

## 8. Open items / inferred gaps
- `[confirm: locale-prefixed routes for non-collection pages — present or deferred?]`
- `[confirm: home/games/countdowns/wallpapers/portfolio/about/cv prose migration to Directus snapshot (`pages` collection) — planned but not built.]`
- `[confirm: `.NET API` and `Blazor admin` layers — roadmap only, absent from repo.]`
- `[confirm: Expressive Code syntax highlighting — TODO in `astro.config.mjs`.]`
