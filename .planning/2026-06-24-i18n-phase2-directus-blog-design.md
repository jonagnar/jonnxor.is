# i18n Phase 2 — Directus content layer (blog vertical slice) — design

**Date:** 2026-06-24
**Status:** Design proposed, pending spec review
**Repo:** `code/jonnxor.is`
**Parent spec:** [`2026-06-24-i18n-directus-design.md`](./2026-06-24-i18n-directus-design.md) (the combined design; this implements its Phase 2, scoped to a vertical slice)
**Branch (planned):** off `preview` — no deploy-branch push without asking

## Context

Phase 1 (the Astro i18n frontend) is **live**: native routing (`is` default, `/en/`, `/ja/`), chrome dictionary + `t()`, the real switcher, dynamic `<html lang>`, and graceful English fallback. The content collections currently render **English under every locale** via fallback — there is no per-locale content yet.

The combined design set the Directus architecture: **Directus runs locally; a committed, locale-keyed snapshot is the SSG build contract** (CI/Vercel never query Directus). Phase 2 implements that — scoped here to a **vertical slice on the `blog` collection** to prove the whole pipeline end-to-end before scaling to grimoire / pages / games.

Current blog: 5 Markdown posts in `src/content/blog/*.md`; schema in `content.config.ts` (`title`, `date`, `category`, `excerpt`, `readTime`, `draft`). No Directus yet. Docker is available in WSL. Governing architecture: **Directus = Content Layer**.

## Goal

Prove the full content pipeline on blog: local Directus (Postgres) holds blog content + `is`/`en`/`ja` translations → a `content:pull` script writes a **locale-keyed Markdown snapshot** into the repo → Astro renders blog **per-locale** from the snapshot, with English fallback. No frontend build depends on Directus being reachable.

## Key decisions

1. **Vertical slice: `blog` only.** Grimoire, pages (About/CV), and games repeat this exact pattern in later slices.
2. **DB: Postgres** (dev/prod parity).
3. **Schema-as-code.** The content model is a committed Directus **schema snapshot** (`directus schema apply`-able). The initial model is created by a **scripted** SDK setup — no manual admin-UI clicking (important while the Directus UI is unreachable pre-reboot).
4. **Snapshot format: per-locale Markdown** (`<slug>.<locale>.md`) — keeps Astro's existing Markdown rendering and readable git diffs.
5. **Directus is local-only; the committed snapshot is the build contract.** CI/Vercel never touch Directus.
6. **Astro renders blog locale-aware** via Phase-1's fallback-rewrite + `Astro.currentLocale` + a locale-aware collection query (English fallback). No new per-locale route files.

## Architecture

### 1. Directus standup — project-local `directus/`

- `directus/docker-compose.yml`: two services — `postgres:16` and `directus/directus` (pinned to a recent 11.x) — with health-checked startup ordering, a named volume for PG data and a bind for `directus/uploads/`. Directus serves on `localhost:8055`.
- **Secrets via sops:** `directus/.env.example` documents the names; `directus/.env.sops` holds `KEY`, `SECRET`, `ADMIN_EMAIL`, `ADMIN_PASSWORD`, `DB_*`, and a `DIRECTUS_TOKEN` (a static service token the scripts authenticate with). `direnv` auto-loads the decrypted env; `.sops.yaml` updated for the new file.
- PG data volume + `directus/uploads/` are **gitignored** (live data backed up via `pg_dump` per the ops rule, not committed).

### 2. Content model (schema-as-code)

- **`languages`**: `code` (`is`/`en`/`ja`), `name`, `direction`.
- **`blog`** (base, non-translatable): `slug`, `date`, `category`, `draft`.
- **`blog_translations`** (junction): `languages_code`, `title`, `excerpt`, `body` (markdown), `read_time`.

Created by `directus/scripts/setup-schema.mjs` (Directus SDK — collections, fields, the translations O2M relation + junction), then captured as `directus/schema/snapshot.yaml` (committed). Fresh-clone reproduction: `directus bootstrap && directus schema apply directus/schema/snapshot.yaml`.

### 3. Seed / import (one-time)

`scripts/content-import.mjs` (Node + `@directus/sdk`) reads the 5 existing `src/content/blog/*.md` (frontmatter + body) and creates each as a `blog` item + an **`en` translation** (the English base). Skips slugs already present, so re-runs are safe.

### 4. `content:pull` — the snapshot contract

`scripts/content-pull.mjs` (SDK; `pnpm content:pull`): fetches all `blog` items + their translations for all locales, and writes per-locale Markdown into `src/content/blog/`:
- `<slug>.en.md` always; `<slug>.is.md` / `<slug>.ja.md` only where a translation exists.
- Each file: frontmatter (`title`, `date`, `category`, `excerpt`, `readTime`, `draft`, `locale`) + the markdown `body`.
- **Prunes** generated files whose item/translation no longer exists in Directus, so deletions propagate. (Pull output is deterministic — re-running with no Directus change yields no diff.)
- The original un-suffixed `src/content/blog/<slug>.md` files are **removed** once Directus is the source — superseded by the generated `<slug>.<locale>.md` files (the import reads the originals into Directus first).

### 5. `content.config.ts` (locale-aware)

The `blog` collection now matches the per-locale files; each entry carries `locale` and `slug` from frontmatter (`locale` is written by `content:pull`; `slug` is the filename stem). Entry `id` stays unique per file (`<slug>.<locale>`). Schema gains `locale`.

### 6. Astro wiring (blog pages)

- A small helper `localizedPost(slug, locale)` returns the entry for `locale`, else the `en` entry (fallback).
- `blog.astro` (list): for `Astro.currentLocale`, list each post via its locale-or-`en` entry; dates formatted per locale (`is-IS`/`en-GB`/`ja-JP`).
- `blog/[slug].astro`: `getStaticPaths` over the **unique slugs** (default set); the page reads `Astro.currentLocale` and selects the `(slug, locale-or-en)` entry. Phase-1 fallback-rewrite serves `/en/blog/<slug>` and `/ja/blog/<slug>` with `currentLocale` set, so each renders the right locale's content.
- Reuses Phase-1 i18n for any blog UI strings (e.g. "All posts") via the dictionary.

### 7. Author workflow

Edit in local Directus → `pnpm content:pull` → review diff → commit → push → CI/Vercel build from the committed snapshot. Directus is never touched at build time.

## Files / dirs

| Action | Path |
|---|---|
| Add | `directus/` — `docker-compose.yml`, `.env.example`, `.env.sops`, `schema/snapshot.yaml`, `scripts/setup-schema.mjs` |
| Add | `scripts/content-import.mjs`, `scripts/content-pull.mjs` |
| Add | `@directus/sdk` devDep; `package.json` scripts (`content:pull`, `content:import`) |
| Edit | `.sops.yaml` (new encrypted file), `.gitignore` (PG data, `directus/uploads/`) |
| Edit | `src/content.config.ts` (blog locale-aware) |
| Edit | `src/pages/blog.astro`, `src/pages/blog/[slug].astro` (locale-aware rendering) |
| Generated | per-locale `src/content/blog/<slug>.<locale>.md` (committed snapshot) |

## Non-goals

- Grimoire, pages (About/CV), games — later slices (same pattern).
- Production Directus hosting / build-time live queries — deferred; the snapshot is the contract.
- Translation *authoring* (`is`/`ja` content) — Phase 3. This slice proves the pipeline with `en` + one test translation.
- The .NET API / Blazor admin layers.

## Verification

- `docker compose up` → Directus healthy on `:8055` (WSL-internal `curl`; admin UI once the host networking is restored).
- Setup script creates the model; `schema snapshot` captures it; `schema apply` on a fresh DB reproduces it identically.
- Import seeds 5 posts + `en` translations.
- `pnpm content:pull` writes `<slug>.en.md` for all 5; running it again produces **no diff** (deterministic).
- `pnpm build` clean **with Directus stopped** (proves no build-time dependency). `/blog` + `/en/blog` render the 5 posts (English, since only `en` exists yet).
- Add one `is` test translation in Directus → `content:pull` → `/blog` shows that post in Icelandic, `/en/blog` still English, `/ja/blog` English fallback.
- `astro check` clean; the existing 8 i18n unit tests still pass.

## Open items for the plan

- Exact schema-snapshot authoring (SDK setup script + snapshot capture) and the translations relation specifics.
- `content.config.ts` locale-aware schema shape + the `localizedPost` helper.
- Directus version pin; static-token creation in the setup flow.
- Branch off `preview`; do not push a deploy branch without asking.
