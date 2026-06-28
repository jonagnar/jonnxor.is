# Authoring note — the English-base invariant (blog + grimoire)

**Date:** 2026-06-26
**Applies to:** the locale-aware Directus collections — `blog` and `grimoire` (added in i18n Phase 2 + Phase 3).

Every blog post and grimoire doc MUST have an English (`en`) translation. English is the
load-bearing fallback: `is`/`ja` render their own text when present and fall back to `en`
otherwise (`localizedEntry` in `src/i18n/localized.ts`).

- **Blog:** a post with no `en` base fails the build for ALL locales — `src/pages/blog/[slug].astro`
  throws a clear "Every post needs an en base" error rather than crashing opaquely.
- **Grimoire:** a doc with no `en` base is silently dropped from `/docs` for any locale that
  also lacks its own translation (there's no fallback to resolve to). Always author `en` first.

## Authoring loop
Edit in local Directus (`pnpm directus:up`, admin on :8055) → `pnpm content:pull` → review the
per-locale diff → commit. The build reads only the committed snapshot — it never queries Directus.

- Per-locale files: `src/content/blog/<slug>.<locale>.md`, `src/content/grimoire/<slug>.<locale>.yaml`.
- `pnpm content:restore` rehydrates a fresh/empty Directus from the committed snapshot (new clone,
  wiped DB, re-bootstrap). Idempotent at the item level — it restores missing items, it does NOT
  reconcile per-translation edits on items that already exist.
- `pnpm content:import` is the one-time seed of the original un-suffixed files (now historical).

## Grimoire specifics
- Translatable fields: `title`, `cat`, `tags`, `body`. Shared base (written identically to every
  locale file): `order`, `realm`, `game`, `updated`.
- `/docs` is a single page that ships the whole grimoire as a client-side `JX_DOCS` blob. It
  localizes at build time and remaps each entry's `id` to its `slug`, so `?doc=<slug>` deep-links
  and the saved reading position (`localStorage('jx-doc')`) stay stable across a language switch.
- The grimoire `updated` field is serialized **quoted** (`updated: "2026-06-01"`) on purpose:
  Astro's content loader parses YAML with 1.1 semantics and would coerce a bare date to a `Date`
  object, failing the `updated: z.string()` schema. `serializeDoc` forces the quoting; a unit test
  (`tests/content/grimoire-yaml.test.ts`) round-trips through `js-yaml` to guard it.

## Regression guard
`localeEntryId` (`src/content/loaders.ts`) derives a distinct collection id per locale file
(`<slug>.en` vs `<slug>.is`), preventing the Phase-2 bug where locale files collided on one id and
silently overwrote each other. Both collections share it. `tests/content/loaders.test.ts` and
`tests/i18n/localized.test.ts` guard the id-derivation and the fallback logic **without** needing
real translated prose — so the guard holds even while content is still en-only.
