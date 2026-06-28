# i18n (is/en/ja) + Directus content layer — combined design

**Date:** 2026-06-24
**Status:** Design proposed, pending spec review
**Repo:** `code/jonnxor.is`
**Branch (planned):** off `feat/nav-astro` / `preview` — no deploy-branch push without asking

## Context

The site is **Astro 6.4.7, fully static (SSG)**, vanilla (no UI framework), deploying Forgejo CI → Vercel (`preview` → preview.jonnxor.is, `main` → production). The nav was just server-rendered into `Nav.astro` (branch `feat/nav-astro`); its language switcher (`.lang-toggle` / `data-lang` / `jx-lang`) is **visual-only** — `setLang()` in `public/assets/site.js` writes `jx-lang` to localStorage and toggles an `.active` class, nothing more.

Today there is **no i18n**: `astro.config.mjs` has only a markdown setting, `Base.astro` hardcodes `<html lang="en">`, and **all UI text + page prose is hardcoded English inline** in the `.astro` files. Two content collections exist — `blog` (5 Markdown posts) and `grimoire` (11 YAML docs, rendered client-side from a JSON blob in `docs.astro`) — neither has any language concept.

Per the environment vision (`~/dev/notes/dev-environment.md`, `~/dev/README.md`), jonnxor.is is intended as **Astro + Directus + .NET 10 API**, with Directus the planned content home for blog/games/grimoire, and i18n (is/en/ja) on the Astro-native backlog. This design covers **both foundations** (the Astro i18n frontend and the Directus content layer) and the seam between them, to be **built in sequence**.

## Governing architecture (4 layers)

Stated by the user 2026-06-24 as the governing assumption for the project (KB + code to be updated to match):

- **Directus — Content Layer.** All content when possible: blog, grimoire, games, **and page prose** (About/CV/home/etc.), with native is/en/ja translations.
- **Astro — Presentation Layer.** All UI: chrome, components, routing, the language switcher.
- **.NET 10 API — Business Logic Layer.** All business logic.
- **.NET 10 Blazor Server — Admin Layer.** Logs, monitoring, health, settings.

The **content/UI boundary is the seam** between the two foundations in this design.

## Key decisions

1. **Design both foundations now; build in sequence** (Phase 1 frontend → Phase 2 Directus + snapshot → Phase 3 translate).
2. **Locales** `is` / `en` / `ja`; **defaultLocale `is`** at the bare root `/`, English at `/en/`, Japanese at `/ja/` (`prefixDefaultLocale: false`).
3. **English is the authoring/base language and the load-bearing fallback** — IS leans on it until parity; JA is "for fun" and stays partial. Fallback is resolved **at lookup time** (dictionary + data layer) so every locale *always* has content — never a 404 or blank. (Astro's built-in `fallback` can't target a non-default locale *from* the default locale, so we don't depend on it; `routing.fallbackType: 'rewrite'` is kept as a whole-page safety net.)
4. **Directus runs locally (Docker); a committed, locale-keyed content snapshot is the SSG build contract.** Forgejo CI / Vercel never query Directus — no hosting dependency.
5. **All content (collections + page prose) is Directus-owned.** The Astro dictionary holds **only** chrome/UI strings.
6. **Page-template DRY:** each page body becomes a locale-parameterized shared component; the default locale (`is`) renders at `src/pages/*`, and `en`/`ja` via `src/pages/[locale]/*` with `getStaticPaths`. No file triplication.
7. **Snapshot format:** per-locale Markdown (blog) / YAML (grimoire) / structured data (pages) — least disruption to current rendering paths.
8. **Switcher:** navigates to the locale-equivalent of the current path; remembers the explicit choice (`jx-lang`); **no** Accept-Language auto-redirect (deferred).

## Architecture

### The seam — the committed snapshot

```
Directus (local, Docker)  ──content:pull──▶  committed snapshot  ──build──▶  Astro SSG  ──▶ Vercel
  = Content Layer            (SDK → files)     locale-keyed .md /            = Presentation
  blog · grimoire ·                            .yaml / data in repo           Layer
  pages · games                                (English baked in as fallback)
```

The snapshot (locale-keyed content files in the repo) is the **contract**. The *producer* evolves without the frontend changing:
- **Phase 1:** snapshot files are produced by **extracting today's inline page prose + the existing collections** (English base; IS/JA fall back to English).
- **Phase 2 onward:** `content:pull` **regenerates the same files from Directus**. The Phase-1 extraction is the English import seed — not throwaway.

### Foundation 1 — Astro i18n frontend

- **Config** (`astro.config.mjs`): `i18n: { locales: ['is','en','ja'], defaultLocale: 'is', routing: { prefixDefaultLocale: false, fallbackType: 'rewrite' } }`.
- **Routing / file layout:** page bodies move to locale-parameterized shared components (e.g. `src/components/pages/Home.astro` taking a `locale` prop). `src/pages/*.astro` render the `is` variant at the unprefixed root; `src/pages/[locale]/*.astro` (with `getStaticPaths` → `en`, `ja`) render the same components prefixed. Dynamic routes (`blog/[slug]`) get the locale segment too. One body, thin route shells.
- **Dictionary:** `src/i18n/ui.ts` (is/en/ja keyed) + `src/i18n/utils.ts` exposing `useTranslations(locale)` / `t(key)` with **`is`/`ja` → `en` fallback**. Holds **chrome/UI strings only** — nav labels + page descriptions, realm/theme/dock labels, footer copy, button/aria labels, and generic UI text (e.g. "On this page", "All posts", "Read", pager prev/next, search placeholder, switcher labels).
- **Nav / Footer:** labels read from the dictionary; every `href` built with `getRelativeLocaleUrl(locale, path)` so links stay in-locale. Active-state logic unchanged.
- **`Base.astro`:** `<html lang={Astro.currentLocale}>`; `<title>` via dictionary; theme pre-paint script unchanged.
- **Dates / formatting:** per-locale (replace the hardcoded `en-GB`) — `is-IS`, `en-GB`, `ja-JP`.
- **Content (Phase 1):** pages + collections read from the snapshot seam (locale-keyed files), English fallback baked in. Until Directus exists, the seam files are the extracted current content.

### The language switcher (make it real)

- Replace the visual-only `setLang`: clicking IS/EN/JA computes the current path's locale-stripped form and **navigates** to the target locale's URL (via `astro:i18n` helpers / a small path map), instead of toggling a class.
- Persist the explicit choice in `jx-lang`; on load, sync the active-state styling to `Astro.currentLocale`. Works in all switcher copies (nav controls, mobile sheet).
- Because fallback is graceful, **every target URL exists** — no dead toggles.
- **`site.js` constraint:** it lives in `public/` and can't import bundled modules. The switcher needs the current locale + the locale→URL mapping; provide it via `data-*` attributes rendered by `Nav.astro` (e.g. `data-locale`, per-button `href`) or a tiny inline module. (Exact mechanism finalized in the Phase 1 plan.)

### Foundation 2 — Directus content layer (local)

- **Local Directus** via **project-local** Docker compose (compose + config in the repo, e.g. `directus/`), env via **sops** (`.env.sops`) + direnv, data dir gitignored (DB dump backed up per the ops backup rule). Project service → not in `ops/`.
- **`languages` collection:** `is`, `en`, `ja` (`code`, `name`, `direction: ltr`).
- **Content models** (Directus translations pattern — base item + `*_translations`):
  - **blog:** base `{ slug, date, draft, category_key, cover? }`; translations `{ title, excerpt, body (markdown), read_time, category_label, tags? }`.
  - **grimoire:** base `{ slug, order, realm, game, updated }`; translations `{ title, cat, tags, body (rich HTML/markdown) }`.
  - **pages (new):** structured per-page content with translatable text fields. About = intro paragraphs + character-sheet (k/v) + chips + cover letter; CV = profile + experience[] (when/title/org/bullets) + skills[] + education[]; home = hero (tagline/kicker/CTAs) + now-strip[] + path cards[]; etc. About/CV are **structured + prose**, so the model is field/block-based, not a single Markdown body.
  - **games:** same pattern (current `games.astro` to be inspected during Phase 2 modeling).
- **One-time import:** seed the existing English content (the Phase-1 extracted seam files) as the base-language entries.

### The snapshot pipeline

- **`content:pull`** (Node + `@directus/sdk`): fetch all items + all translations for all locales; for each locale, write snapshot files applying **English fallback** where a translation is missing. Output: blog → `src/content/blog/<slug>.<locale>.md`; grimoire → `src/content/grimoire/<slug>.<locale>.yaml`; pages → `src/content/pages/<page>.<locale>.{md,yaml,json}`.
- **`content.config.ts`:** collections become locale-aware (locale in id/field); add the `pages` collection. (Exact shape finalized in the Phase 2 plan.)
- **Author workflow:** edit in local Directus → `pnpm content:pull` → review diff → commit → push → Forgejo CI → Vercel. Snapshot committed; build needs no Directus.

## Build sequence

| Phase | Scope | Directus dep |
|---|---|---|
| **1 — Astro i18n frontend** | i18n config; shared page components + locale routes; `ui.ts` dictionary + `t()`; nav/footer/`Base` locale-aware; switcher navigates; per-locale dates; extract current inline prose + collections into the locale-keyed seam (English base/fallback). **Ships real switching to preview → prod.** | none |
| **2 — Directus + snapshot** | Local Directus; model blog/grimoire/pages/games with translations; import Phase-1 English base; `content:pull`; frontend consumes Directus-generated snapshot (no frontend change beyond pointing at generated files). | local only |
| **3 — Translate (incremental)** | Author IS to parity + JA "for fun" in Directus; finalize any remaining page-prose modeling. JA stays partial via fallback. | local only |
| **Cross-cutting** | Update KB (`README.md` / `dev-environment.md`) to the 4-layer architecture + this approach. | — |

## Files touched (Phase 1 focus)

| Action | File |
|---|---|
| Edit | `astro.config.mjs` (add `i18n`) |
| Add | `src/i18n/ui.ts`, `src/i18n/utils.ts` |
| Add | `src/components/pages/*.astro` (extracted page bodies) |
| Add | `src/pages/[locale]/*.astro` (en/ja routes via `getStaticPaths`) |
| Add | `src/content/pages/*` + blog/grimoire locale-keyed files; `pages` collection in `content.config.ts` |
| Edit | `src/layouts/Base.astro` (`lang`, title via dict) |
| Edit | `src/components/Nav.astro` (labels from dict, locale-aware hrefs) |
| Edit | `src/components/Footer.astro` (dict) |
| Edit | `src/pages/*.astro` (become thin `is` shells rendering shared components) |
| Edit | `src/pages/blog.astro`, `blog/[slug].astro`, `docs.astro` (locale-aware data + dict) |
| Edit | `public/assets/site.js` (switcher navigates) |
| Phase 2 add | `directus/` (compose, config), `scripts/content-pull.*`, locale-aware `content.config.ts` |

## Non-goals

- No redesign / visual change — themes, layout, nav tiers, fonts all unchanged.
- No live Directus hosting; no SSR/adapter (stays SSG).
- No .NET API / Blazor admin work (separate foundations).
- Not translating all content in the first build — mechanism + seam first; translation is Phase 3, authored in Directus.
- No Accept-Language auto-redirect (deferred).

## Verification

- **Phase 1:** `pnpm build` clean; `pnpm dev`. Then:
  - `/` = `is`, `/en/`, `/ja/` resolve for **every** page (home, about, cv, portfolio, blog, blog/[slug], docs, games, wallpapers, countdowns, 404).
  - Switcher navigates between locales **preserving the current page**; remembers the choice across reload.
  - `<html lang>` correct per locale; nav/footer/dock links stay in-locale; dates render per locale.
  - Untranslated strings/content **fall back to English** (no 404, no blank).
  - Nav active-state correct on every route; **no visual regression** vs current.
- **Phase 2:** `content:pull` regenerates a snapshot matching the Phase-1 English base; build clean from generated files; a single Directus edit → pull → diff shows only that change.

## Open items to finalize in planning

- **Phase 1 plan:** exact locale route layout (shared component + `[locale]` vs a `[...locale]` optional rest param); `content.config.ts` locale-aware schema (per-locale files vs a translations array); the `site.js` switcher mechanism given the `public/` import constraint.
- **Phase 2 plan:** Directus `pages` / `games` field & block model (structured About/CV/home); `content:pull` output shape per collection.
- **Branch/deploy:** branch off `feat/nav-astro` (or `preview` once the nav PR merges); do not push a deploy branch without asking.
