# i18n Phase 3 — Grimoire vertical slice + regression guard — design

**Date:** 2026-06-26
**Status:** Design approved, pending spec review
**Repo:** `code/jonnxor.is`
**Parent specs:** [`2026-06-24-i18n-directus-design.md`](./2026-06-24-i18n-directus-design.md) (combined design) · [`2026-06-24-i18n-phase2-directus-blog-design.md`](./2026-06-24-i18n-phase2-directus-blog-design.md) (the blog slice this mirrors)
**Phase-2 handoff:** [`2026-06-26-i18n-phase2-directus-blog-completion.md`](./2026-06-26-i18n-phase2-directus-blog-completion.md)
**Branch (planned):** off latest `preview` (PRs #7/#8/#9 already merged) — no deploy-branch push without asking.

## Context

Phase 2 proved the **Directus → committed-Markdown-snapshot → per-locale Astro render** pipeline end-to-end on the `blog` collection (en base + English fallback; build has zero Directus dependency). Content is currently **en-only**; every locale renders English via fallback.

Phase 3 does two things:
1. **Extends the proven pattern to `grimoire`** — the collection closest in shape to blog — as a second vertical slice (en base + fallback, *no* real is/ja prose yet).
2. **Closes the Phase-2 regression-guard gap.** The `generateId` fix (distinct collection id per locale file, commit `c947f79`) has **no committed guard** while content is en-only — a revert would still build green. Phase 3 adds a **prose-independent unit-test guard** so the bug can't silently return.

Small cross-cutting cleanups that a *second* collection makes worthwhile are folded in: a shared SDK-client helper (resolves Phase-2 handoff #2's drift risk) and generalizing the blog-specific localized-entry helper.

**Decisions taken in brainstorming (2026-06-26):**
- **Grimoire translatable fields:** `title, body, cat, tags`. Shared base (non-translatable): `order, realm, game, updated`.
- **No real is/ja authoring this phase** — ship the en-only pipeline + the prose-independent test guard. The build-artifact guard (a committed 2-locale file) stays dormant until a later authoring pass.
- Grimoire snapshot stays **YAML** (its `body` is pre-rendered HTML injected as-is; converting to Markdown would change the render path).

## Goal

Local Directus holds `grimoire` content + `is`/`en`/`ja` translations → `content:pull` writes a locale-keyed YAML snapshot (`<slug>.<locale>.yaml`) → Astro renders `/docs` **per-locale** from the snapshot with English fallback, the client reader otherwise untouched. The build never depends on Directus being reachable. The `generateId` regression is guarded by committed unit tests that need no real prose.

## Key decisions

1. **Mirror the Phase-2 blog pattern** for grimoire (base item + `*_translations` junction; schema-as-code; per-locale snapshot files; locale-aware render with en fallback).
2. **Snapshot format: per-locale YAML** `<slug>.<locale>.yaml`, `body` as a block scalar for readable diffs.
3. **`docs.astro` localizes at build time** (single page, no per-locale route file): for `Astro.currentLocale`, pick each doc's locale-or-`en` entry, sort by `order`, serialize to `window.JX_DOCS`.
4. **Locale-stable `id`:** the serialized `id` is remapped to the doc's `slug` (not `<slug>.<locale>`), so deep-links (`?doc=<slug>`) and `localStorage('jx-doc')` stay constant across locales. The client reader code is unchanged.
5. **`tags` translatable as a Directus `json` field** (array of strings; tags may contain spaces).
6. **Prose-independent regression guard:** extract the id-derivation into a shared, exported `localeEntryId()` used by both collections, and unit-test it (plus the fallback helper) with fixtures.
7. **Cross-cutting DRY:** a shared `scripts/lib/directus-client.mjs` (login + `process.exit(0)` teardown) reused by all SDK scripts; a generalized `src/i18n/localized.ts` replacing `blog.ts`.

## Architecture

### 1. Grimoire content model (schema-as-code)

Added to `directus/scripts/setup-schema.mjs` (idempotent, mirroring the blog block), captured in `directus/schema/snapshot.yaml`:

- **`grimoire`** (base, non-translatable): `id` (auto-increment pk), `slug` (string, unique), `order` (integer), `realm` (string enum: `games`/`code`/`life`), `game` (string, nullable), `updated` (string).
- **`grimoire_translations`** (junction, hidden): `id` (pk), `grimoire` (int fk), `languages_code` (string fk), `title` (string), `cat` (string), `tags` (**json**), `body` (text, rich HTML).
- The `translations` alias on `grimoire` + the two relations (`grimoire_translations.grimoire → grimoire` O2M via `translations`; `grimoire_translations.languages_code → languages`), exactly as blog.

`languages` already exists from Phase 2 — reused, not recreated.

### 2. Snapshot pipeline (scripts)

- **`scripts/lib/grimoire-yaml.mjs`** (new) — `parseDoc(raw)` / `serializeDoc(record)` for per-locale grimoire YAML. Uses the `yaml` package (new devDep; gray-matter only handles MD frontmatter). `serializeDoc` emits `body` as a literal block scalar (`|`) and writes a deterministic key order for stable diffs.
- **`scripts/lib/directus-client.mjs`** (new) — `connect()` → a logged-in `@directus/sdk` client (object-payload login) + a `done()`/`process.exit(0)` teardown helper. All five SDK scripts switch to it so the auth-timer/login pattern can't drift (Phase-2 handoff #2).
- **`content:pull`** (extend) — additionally fetch `grimoire` + translations, write `src/content/grimoire/<slug>.<locale>.yaml`, prune generated locale files no longer backed by Directus **and** the original un-suffixed `*.yaml`. Deterministic (re-run → no diff). Blog behavior unchanged.
- **`content:restore`** (extend) — additionally group `src/content/grimoire/<slug>.<locale>.yaml` by slug → one `grimoire` item + its translations; idempotent at the item level (existing slugs skipped).
- **Grimoire seed (one-time)** — read the existing 11 `src/content/grimoire/<slug>.yaml` as the `en` base → create each as a `grimoire` item + `en` translation. Same role/shape as `content-import.mjs` (extend it to grimoire, or a parallel `content-import-grimoire.mjs` — finalized in planning).

### 3. `content.config.ts` (grimoire locale-aware)

- `grimoire` collection matches the per-locale files; schema gains `slug` and `locale` (enum `is`/`en`/`ja`), keeps `order/realm/game/updated/cat/tags/title/body`.
- Both `blog` and `grimoire` loaders use the shared `localeEntryId` for `generateId` (see §5).

### 4. Rendering — `src/pages/docs.astro`

- Frontmatter: `locale = Astro.currentLocale ?? defaultLocale`; for each `uniqueSlug`, `localizedEntry(all, slug, locale)`; sort by `order`; **set `id = slug`** on each shaped entry; serialize to `window.JX_DOCS` (existing `<`-escaping kept).
- The entire client-side reader (nav, filters, TOC, pager, deep-links, localStorage) is **untouched** — it consumes the same JSON shape, now locale-resolved with stable ids.
- Dates/labels already handled by the reader from `updated`; any chrome strings reuse the Phase-1 dictionary if applicable (no new per-locale UI strings expected).

### 5. Localized helpers + regression guard

- **`src/content/loaders.ts`** (new) — exported `localeEntryId(entry)` = `entry.replace(/\.(md|yaml)$/, '')`. Used by both collection loaders in `content.config.ts`. (Single home for the exact fix that bit Phase 2.)
- **`src/i18n/localized.ts`** (new, replaces `src/i18n/blog.ts`) — generic `localizedEntry(entries, slug, locale)` (locale entry else `en` fallback) + `uniqueSlugs(entries)`. `blog.astro`, `blog/[slug].astro`, `docs.astro` all consume it. `blog.ts` removed; imports updated.
- **Unit tests (vitest):**
  - `localeEntryId('foo.en.md') !== localeEntryId('foo.is.md')` and `localeEntryId('foo.en.yaml') !== localeEntryId('foo.is.yaml')` — guards the generateId collision directly.
  - `localizedEntry` returns the locale entry when present, falls back to `en` when absent, returns `undefined` when neither exists — via a small 2-locale fixture.
  - These commit green today and fail loudly on regression; no real prose needed.

## Files / dirs

| Action | Path |
|---|---|
| Add | `scripts/lib/grimoire-yaml.mjs`, `scripts/lib/directus-client.mjs` |
| Add | `src/content/loaders.ts`, `src/i18n/localized.ts` |
| Add | grimoire guard + helper tests (under the existing test dir) |
| Add | grimoire one-time seed (extend `content-import.mjs` or new script) |
| Edit | `directus/scripts/setup-schema.mjs` (+ regenerate `directus/schema/snapshot.yaml`) |
| Edit | `scripts/content-pull.mjs`, `scripts/content-restore.mjs` (grimoire + shared client) |
| Edit | `scripts/content-import.mjs` (shared client; grimoire seed if extended here) |
| Edit | `src/content.config.ts` (grimoire locale-aware; shared `localeEntryId`) |
| Edit | `src/pages/docs.astro` (locale-aware; stable `id`) |
| Edit | `src/pages/blog.astro`, `src/pages/blog/[slug].astro` (import `localized.ts`) |
| Remove | `src/i18n/blog.ts` (superseded by `localized.ts`) |
| Edit | `package.json` (`yaml` devDep; grimoire seed script if separate) |
| Generated | `src/content/grimoire/<slug>.en.yaml` ×11 (replace un-suffixed originals) |
| Add | authoring note (en-base invariant) under `notes/projects/jonnxor.is/` |

## Non-goals

- **Real is/ja prose** for blog or grimoire — deferred to a later authoring pass (the user authors/approves it; not invented here).
- **pages / games** collections — later slices (same pattern).
- **Production Directus hosting / build-time live queries** — the committed snapshot stays the contract.
- **Any visual/redesign change** to the grimoire reader UI — render path and styles unchanged.
- The .NET API / Blazor admin layers.

## Verification

- `directus:up` → `directus:schema` creates `grimoire` + `grimoire_translations`; `snapshot.yaml` captured; `schema apply` on a fresh DB reproduces it identically.
- Grimoire seed imports 11 docs + `en` translations.
- `content:pull` writes `<slug>.en.yaml` for all 11; re-run produces **no diff** (deterministic); the un-suffixed originals are pruned.
- `content:restore` rehydrates grimoire from the snapshot into an empty Directus (idempotent at item level).
- `pnpm build` clean **with Directus stopped** (no build-time dependency). `/docs`, `/en/docs`, `/ja/docs` render all 11 docs (English, since only `en` exists); deep-links (`?doc=<slug>`), realm/game filters, search, TOC, and pager all work; `localStorage('jx-doc')` value is locale-stable.
- Add one `is` test translation in Directus → `content:pull` → `/docs` shows it in Icelandic, `/en/docs` English, `/ja/docs` English fallback. (Verification only — not committed.)
- `astro check` 0 errors; full unit suite green (15 existing + new guard tests); blog rendering unchanged after the helper rename.

## Open items for the plan

- `tags` Directus field type (`json` vs `csv`) and exact pull/restore round-trip mapping for the array.
- Grimoire seed: extend `content-import.mjs` vs a separate one-time script.
- `serializeDoc` block-scalar/key-order specifics to guarantee byte-stable pull output.
- Test-runner mechanics for importing `localeEntryId`/`localizedEntry` (pure modules, no Astro runtime).
- Branch off latest `preview`; do not push a deploy branch without asking.
- Execution flow: subagent-driven (fresh implementer per task + spec review + code-quality review each), as in Phase 2.
