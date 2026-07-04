# Design — Content port + componentization (sub-project 1 of the wiring milestone)

**Date:** 2026-07-04
**Status:** Approved (design review 2026-07-04)
**Scope:** jonnxor.is — Directus content layer + Astro presentation layer
**Follows:** `frd-content.md`, `frd-frontend.md`, `2026-06-24-i18n-directus-design.md`, `2026-06-26-authoring-en-base-invariant.md`

## 0. Milestone context and decisions taken

The "wire everything together" milestone spans four subsystems. Decomposition agreed
2026-07-04, each sub-project getting its own design → plan → implementation cycle:

1. **Content port + componentization** — this document.
2. **.NET 10 API** — role decided: **build-time snapshot worker** (owns/wraps the
   Directus → snapshot pipeline, runs locally/CI only, no public hosting). This resolves
   `frd-api.md` open question #1–2; request-time endpoints deferred until hosting exists.
3. **Blazor Server admin, thin v1** — after the API exists (config/settings, content-pull
   orchestration, health tiles; logs/monitoring wait for the observability stack).

Approach for this sub-project (chosen over a Directus M2A "blocks" page-builder and over a
big-bang migration): **entity collections, ported as page-by-page vertical slices** — each
slice is schema → seed → pull → Astro collection → componentized page → tests, one PR to
`preview`, always shippable.

## 1. Goal & non-goals

**Goal.** Every remaining hardcoded content surface — games, countdowns, wallpapers,
portfolio, home, about, CV — becomes Directus-owned and flows through the existing seam
(Directus → `content:pull` → committed snapshot → SSG). Each page is refactored into typed
Astro components as its content lands. The site stays pixel-identical (visual suite is the
gate; one deliberate exception, §6).

**Non-goals.**
- No redesign; no token/theme changes.
- No media/uploads — all cover/gradient art stays CSS data (hex arrays), per current design.
- No is/ja translation authoring — seeds are `en` only; the English-base fallback serves
  is/ja exactly as blog/grimoire do today. Parity authoring is later Directus work.
- No .NET involvement — the Node pipeline remains the producer this cycle; sub-project 2
  later wraps it as the build-time worker.
- No changes to blog/grimoire collections.

## 2. Directus schema — five new collections

All follow the existing pattern: non-translatable base item + `<name>_translations`
junction keyed `languages_code` → `languages.code`, added idempotently to
`directus/scripts/setup-schema.mjs` (guarded by `need()`), then exported via
`pnpm directus:snapshot`. `slug` unique + required everywhere (keys the snapshot files).

### `games`
| Field | Where | Type | Notes |
|---|---|---|---|
| slug | base | string | kebab of title |
| tab | base | enum `upcoming/playing/played` | Favorites is a filter, not a tab |
| date | base | date, nullable | release date; null = "any day now" |
| platforms | base | json (string[]) | e.g. `["PS5","Xbox"]` — chips, untranslated |
| gradient | base | json (3× hex) | cover art |
| initials | base | string | 2-letter cover monogram (`gi`) |
| favorite | base | boolean | star + Favorites filter |
| title, sub | translations | string | game name, status line |

### `countdowns`
One collection for countdowns *and* count-ups (`kind` discriminator — they share
`what`/`note` and one page). The `BORN`/`AGE10` life-tickers become countup rows.
| Field | Where | Type | Notes |
|---|---|---|---|
| slug | base | string | |
| kind | base | enum `countdown/countup` | |
| when | base | date, nullable | countdowns; null = indefinite |
| gold | base | boolean | accent styling |
| icon | base | string, nullable | countups (emoji) |
| start | base | datetime, nullable | countups |
| rate | base | float, nullable | countups: hours/day |
| what, note | translations | string | |

### `wallpapers`
| Field | Where | Type | Notes |
|---|---|---|---|
| slug | base | string | also the download filename |
| tag | base | string | filter category; label treated as data (untranslated for now) |
| aspect_ratio | base | string | e.g. `16/10` |
| gradient | base | json (3× hex) | |
| angle | base | integer | gradient angle 0–360 |
| title | translations | string | |

Filter chips derive from distinct `tag` values + the `All` label from the ui dictionary.

### `projects` (portfolio)
| Field | Where | Type | Notes |
|---|---|---|---|
| slug | base | string | |
| sigil | base | string | 2-letter plate monogram |
| gradient | base | json (3× hex) | |
| tech | base | json (string[]) | stack chips, untranslated |
| links | base | json ({label, href}[]) | labels treated as data, like chips |
| title, description, plate | translations | string | plate = time/status label |

### `pages` (prose surfaces: home, about, cv — plus page heads for games/countdowns/wallpapers/portfolio)
| Field | Where | Type | Notes |
|---|---|---|---|
| slug | base | string | page id: `home/about/cv/portfolio/games/countdowns/wallpapers` |
| kicker, title, lede | translations | string | the shared page-head trio |
| sections | translations | json | per-page structured prose, see §3 |

## 3. `pages.sections` — the structural decision

Page heads share kicker/title/lede as first-class fields. Everything bespoke lives in a
**per-page `sections` json** on the translation, validated at build time by **per-page Zod
schemas discriminated on slug**:

- `home`: paths cards (3× title/copy/link-label), now strip (3× label/value/sub),
  hero tagline + CTA labels. ("Latest writing" is *not* content — see §6.)
- `about`: body paragraphs, character-sheet rows (8× key/value), skill chips,
  cover letter (paragraphs + dateline + sign-off).
- `cv`: profile paragraph, experience roles (3× when/org/title/bullets[]),
  skills rows (6× key/value), education entries, languages line, contact block.
- `portfolio`/`games`/`countdowns`/`wallpapers`: head-only (kicker/title/lede) +
  small extras (e.g. countdowns methodology note). Chrome-like button labels
  (wallpapers "Download"/"Close") go to the `src/i18n/ui.ts` dictionary, not content.

Trade-off accepted: Directus's json editor is a clunkier authoring UX than dedicated
fields, but dedicated collections for CV alone would add ~4 more collections
(cv_roles, cv_skills, …) for content edited a few times a year. Build-time Zod keeps the
blob honest — drift fails the build loudly, which is the contract working.

## 4. Pipeline refactor — descriptor-driven pull/restore

`scripts/content-pull.mjs` is per-collection copy-paste (blog block + grimoire block).
It becomes a loop over a descriptor table in **`scripts/lib/collections.mjs`**:

```js
{ name, dir, ext, baseFields, translationFields, serialize(item, t), parse(file) }
```

- Generic per-collection logic shared: fetch all items + translations, write one file per
  `(slug, locale)`, prune orphaned/legacy files (current EISDIR-safe dirent walk kept).
- Blog keeps `post-markdown.mjs`; the five new collections use a **generalized
  grimoire-style deterministic YAML serializer** (`scripts/lib/entry-yaml.mjs`): fixed
  per-collection key order, block-literal scalars for long prose, line-wrap disabled,
  date-like strings forced quoted (the YAML-1.1 coercion guard grimoire already solved).
- `scripts/content-restore.mjs` generalizes over the same table (item-level idempotence
  unchanged: existing slugs skipped, no per-translation reconciliation).
- Snapshot layout: `src/content/<collection>/<slug>.<locale>.yaml` (blog stays `.md`).

## 5. Seeding — snapshot-first, round-trip proven

No throwaway import scripts. Per slice:

1. Extract the page's hardcoded data **directly into snapshot files** (`en` locale) —
   these seeds are the first commit of the slice.
2. Generalized `content:restore` hydrates local Directus from them.
3. `content:pull` regenerates the snapshot — output must be **byte-identical** to the
   seed files, proving serializer determinism before the page itself changes.

English-base invariant holds by construction (seeds are `en`).

## 6. Astro consumption & componentization

Each page swaps hardcoded data for `getCollection` + the existing `localizedEntry`
fallback — every refactored page becomes locale-aware via the same mechanism `blog.astro`
uses today; `/en/` and `/ja/` rewrites start serving locale-resolved content for free.

**Server-render shift.** games/countdowns/wallpapers currently client-render their grids
from inline JS arrays. They move to **server-rendered cards** (typed Astro components);
JS reduces to behavior only:
- games: all cards server-rendered; tab filter toggles visibility via data-attributes;
  countdown badges JS-filled (`window.JX.daysUntil`); `jx-games-tab` persistence kept.
- countdowns: card/clock shells server-rendered; the 100 ms ticker fills numbers only.
- wallpapers: tiles server-rendered; filter toggles visibility; lightbox + canvas PNG
  download stay client-side as-is.

**New components** (`src/components/`), introduced by the slice that needs them:
`PageHead` (shared kicker/h1/lede — ~6 pages), `GameCard`, `CountdownCard`, `CountUpCard`,
`WallpaperTile`, `WallpaperLightbox`, `ProjectCard`, `FactList`, `CVRole`, `CVSkillsRow`,
`PostRow` (introduced by the home slice; blog.astro may adopt it there only if the swap
is mechanical — otherwise blog stays untouched). Component-scoped styles move with their component; page one-offs
stay in the page (current convention).

**Deliberate behavior change (the only one):** home's "latest writing" — 3 hardcoded
rows — becomes derived from the blog collection (top 3 by date, drafts excluded). Home's
visual baseline is re-recorded in that slice and reviewed in the PR.

## 7. Slice order

Each slice = schema + seed + pull + collection + componentized page + tests, one PR to
`preview`, merged green before the next starts:

Each slice ports its page *completely*, head included — so the `pages` collection
schema lands in slice 1 (with just the `games` row) and gains one row per slice.

1. **games** — most structured data; proves the whole seam + server-render shift.
2. **countdowns** — `kind` discriminator; page is visual-test-exempt (live data) — low risk.
3. **wallpapers** — lightbox/canvas JS boundary.
4. **portfolio** — simple, no page JS.
5. **pages: home** — sections json + dynamic latest-posts (baseline re-record).
6. **pages: about** — long prose + character sheet.
7. **pages: cv** — most structured prose; `@media print` must survive — last on purpose.

## 8. Testing

Per slice, riding the existing pyramid (no feature merges without a test):
- **Unit (Vitest):** serializer round-trip per collection (pattern:
  `tests/content/grimoire-yaml.test.ts`); `config-wiring.test.ts` extended so every new
  collection is guarded for `generateId: localeEntryId`; per-page `sections` Zod schemas
  exercised with the seed data.
- **Render (Container API):** componentized page renders for `is`/`en` with fallback.
- **E2E (containerized Playwright):** existing games/countdowns/wallpapers behavior specs
  (tabs, ticking, filtering, lightbox) stay green against the server-rendered DOM.
- **Visual:** baselines unchanged for all slices except home (§6), whose re-record is
  reviewed in its PR. `/countdowns` remains excluded (live data).

## 9. Risks & mitigations

- **Server-render shift changes the DOM the inline JS expects** → behavior JS moves in the
  same slice; e2e specs are the gate.
- **YAML determinism for long prose** (cover letter, CV bullets) → block-literal +
  fixed-key-order discipline already proven by grimoire; round-trip test per collection.
- **`pages.sections` drift** between Directus edits and Zod → build fails loudly by
  design; that is the seam's contract doing its job.
- **Directus schema growth** (5 collections + junctions) → `setup-schema.mjs` stays
  idempotent; `snapshot.yaml` remains the authoritative replayable definition.

## 10. Definition of done

- All seven surfaces edit in Directus, export via `content:pull`, build with the stack
  stopped (snapshot-only), and render through typed components.
- `setup-schema.mjs` + `snapshot.yaml` reproduce the full schema on a fresh instance;
  `content:restore` rehydrates all collections from the committed snapshot.
- Full test pyramid green in CI on `preview`; production under-construction mode untouched.
