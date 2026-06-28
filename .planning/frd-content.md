> **DRAFT — for validation.** Intent inferred from existing artifacts; confirm before relying on it.

# FRD — Content (Directus content layer)

**Project:** jonnxor.is
**Layer:** Content (Directus 11, local-only, Dockerized) → committed snapshot → Astro SSG
**Source of truth for this draft:** `directus/` (compose, schema snapshot, setup script, env), the `scripts/content-*.mjs` sync pipeline + `scripts/lib/`, and the design/authoring notes in `.planning/`.

## 1. Overview

Content is authored in a **local Directus instance** and exported to a **committed, locale-keyed snapshot** in the repo. The Astro build reads only that snapshot — **CI/Vercel never query Directus** (no hosting dependency, no runtime CMS). Directus is the *producer*; the snapshot is the *contract*; Astro is the *consumer*.

```
Directus (local, Docker)  ──content:pull──▶  committed snapshot  ──astro build──▶ Vercel
  blog · grimoire             (@directus/sdk    src/content/**.<locale>.{md,yaml}    (static)
  (+ translations)             → files)         (English baked in as fallback)
```

Today the content layer covers **two collections — `blog` and `grimoire`** — each with a base item + a `*_translations` junction, plus a `languages` collection. The design notes (`.planning/2026-06-24-i18n-directus-design.md`) also envision Directus-owned `pages` and `games` collections; **those are not yet modeled** (`directus/scripts/setup-schema.mjs` defines only languages/blog/grimoire).

`[confirm: `pages` (About/CV/home prose) and `games`/`countdowns`/`wallpapers` collections are planned but not built — that content is still hardcoded in the Astro pages.]`

## 2. Stack & topology

From `directus/docker-compose.yml` (`name: jonnxor-directus`):
- **`database`**: `postgres:16`, volume `./data:/var/lib/postgresql/data`, healthcheck `pg_isready`. (`directus/data/` is gitignored — permission-locked Postgres data dir.)
- **`directus`**: `directus/directus:11.3.5`, port **8055**, volume `./uploads:/directus/uploads`, depends on a healthy DB, healthcheck on `/server/ping`.
- **Env**: `directus/.env` (committed example `directus/.env.example`): Directus `KEY/SECRET/PUBLIC_URL/ADMIN_EMAIL/ADMIN_PASSWORD`, Postgres `DB_*`, and `DIRECTUS_URL` (used by host-side Node scripts hitting the published port). Repo `.sops.yaml` + `.envrc` indicate sops/direnv-managed secrets. `[confirm: `directus/.env` is sops-encrypted / not committed in plaintext; only `.env.example` is tracked.]`

npm scripts (`package.json`):
- `directus:up` / `directus:down` — compose lifecycle.
- `directus:schema` — run `directus/scripts/setup-schema.mjs` against the running instance.
- `directus:snapshot` — `directus schema snapshot` inside the container, tee'd to `directus/schema/snapshot.yaml`.
- `content:import` / `content:pull` / `content:restore` — the content pipeline (`scripts/content-*.mjs`), each `node --env-file=directus/.env`.

**Requirements**
- Directus **shall** run only locally via the repo's Docker compose; the production build **shall not** depend on a reachable Directus.
- Postgres data (`directus/data/`) and uploads handling **shall** follow the ops backup rule (DB data dir gitignored, backed up out-of-band). `[confirm: backup cadence/target for the local Postgres volume.]`
- Secrets **shall** be sops/direnv-managed; only `.env.example` **shall** be committed.

## 3. Schema / collections

Created idempotently by `directus/scripts/setup-schema.mjs` (each collection guarded by `need(name)`); the authoritative reproduction is the committed `directus/schema/snapshot.yaml` (`schema snapshot`, vendor postgres, directus 11.3.5).

### `languages`
- PK `code` (string, intended length 8), `name`, `direction` (default `ltr`).
- Seeded with `is` (Íslenska), `en` (English), `ja` (日本語).
- Note (in the script): Directus ignores `length` on a string PK at create time and can't alter it via the API; the script attempts `updateField` and otherwise warns to run raw SQL / apply the snapshot (`ALTER TABLE languages ALTER COLUMN code TYPE varchar(8)`).

### `blog` — "The Codex" (base, non-translatable)
- Fields: `id` (auto-int PK, hidden), `slug` (unique, required), `date` (timestamp), `category` (string), `draft` (boolean, default false).
- `translations` **alias** (interface `translations`, `languageField: 'code'`).

### `blog_translations` (junction, hidden)
- `id`, `blog` (→ `blog`), `languages_code` (→ `languages`), `title`, `excerpt` (text), `body` (markdown), `read_time`.
- Relations: `blog_translations.blog` → `blog` (O2M back via `translations`, `on_delete: SET NULL`); `blog_translations.languages_code` → `languages`.

### `grimoire` — "The Grimoire" (base, non-translatable)
- Fields: `id` (PK), `slug` (unique, required), `order` (int), `realm` (dropdown games/code/life), `game` (nullable), `updated` (string).
- `translations` alias as above.

### `grimoire_translations` (junction, hidden)
- `id`, `grimoire` (→ `grimoire`), `languages_code` (→ `languages`), `title`, `cat`, `tags` (json/tags), `body` (markdown — holds the hand-authored rich HTML).
- Relations mirror the blog junction.

**Requirements**
- The schema **shall** use the Directus translations pattern: a non-translatable base item + a `*_translations` junction keyed by `languages_code` → `languages.code`.
- `blog.slug` and `grimoire.slug` **shall** be unique and required (they key the snapshot files and Astro entries).
- Schema setup **shall** be idempotent (`need()` guards) and the committed `directus/schema/snapshot.yaml` **shall** be the authoritative, replayable definition.
- `[confirm: translatable vs base split is fixed — base = `slug/date/category/draft` (blog) and `slug/order/realm/game/updated` (grimoire); everything else is per-translation, per `.planning/2026-06-26-authoring-en-base-invariant.md`.]`

## 4. Media / uploads
- `directus/uploads/` mounted into the container (`/directus/uploads`); empty in the repo today.
- `[confirm: upload/asset handling and how images would reach the static build — current blog/grimoire content is text/HTML-only; portfolio/wallpaper/game art is CSS-gradient placeholder in the frontend, not Directus media.]`

**Requirements**
- If/when media is used, the content layer **shall** define how files are referenced from snapshot content and surfaced to the static build (committed assets vs. external URLs). `[confirm: not yet designed.]`

## 5. Content sync pipeline (the seam)

All in `scripts/`, sharing `scripts/lib/`:
- `lib/directus-client.mjs` — `connect()` returns a logged-in `@directus/sdk@22` client (object-payload login); `done()` calls `process.exit` to kill the SDK's token-refresh timer so scripts don't hang.
- `lib/post-markdown.mjs` — `parsePost` / `serializePost` (gray-matter front-matter ↔ Markdown).
- `lib/grimoire-yaml.mjs` — `parseDoc` / `serializeDoc`; fixed key order for stable diffs, `body` forced to a **block-literal** scalar with line-wrapping disabled, and **`updated` forced double-quoted** (Astro's loader parses YAML 1.1 and would coerce a bare date to a `Date`, breaking `updated: z.string()`; guarded by `tests/content/grimoire-yaml.test.ts`).

### `content:pull` — Directus → snapshot (the normal authoring export)
- `scripts/content-pull.mjs`: fetch all `blog`/`grimoire` items with all translations; write one file per `(slug, locale)`:
  - blog → `src/content/blog/<slug>.<locale>.md`
  - grimoire → `src/content/grimoire/<slug>.<locale>.yaml`
- Non-translatable base fields are written **identically to every locale file** of an item.
- **Prune step:** removes generated locale files no longer backed by Directus, and any legacy un-suffixed `*.md`/`*.yaml` (superseded by `<slug>.en.*`). Skips non-file dirents so `unlink` can't `EISDIR`.

### `content:import` — one-time seed (historical)
- `scripts/content-import.mjs`: imports the original **un-suffixed** `.md`/`.yaml` files into Directus as the `en` base. Skips slugs that already exist. One-time bootstrap; now historical.

### `content:restore` — snapshot → Directus (rehydrate)
- `scripts/content-restore.mjs`: groups `<slug>.<locale>.*` files by slug into one base item + N translations and creates any slug not already present. **Idempotent at the item level** — restores missing items, does **not** reconcile per-translation edits on existing items. Use for a fresh/empty Directus (new clone, wiped DB).

**Authoring loop** (`.planning/2026-06-26-authoring-en-base-invariant.md`): edit in local Directus → `pnpm content:pull` → review per-locale diff → commit → push → CI → Vercel.

**Requirements**
- `content:pull` **shall** be the single producer of the committed snapshot, writing one deterministic file per `(slug, locale)` and pruning orphaned/legacy files.
- The snapshot **shall** be byte-stable on round-trip (fixed YAML key order, block-literal bodies, quoted `updated`, line-wrap disabled) so diffs reflect only real content changes.
- `content:restore` **shall** rehydrate a fresh Directus from the committed snapshot, idempotent at the item level (existing slugs skipped, no per-translation reconciliation). `[confirm: per-translation reconcile on existing items is intentionally out of scope.]`
- Every item **shall** carry an `en` translation (English-base invariant): without it, blog fails the Astro build and grimoire entries are silently dropped — so authors **shall** write `en` first.
- Sync scripts **shall** exit explicitly (`done()`) to avoid the SDK keep-alive hang.

## 6. How Astro consumes the snapshot
- The snapshot files are the inputs to the Astro `blog`/`grimoire` collections (`src/content.config.ts`); the build never touches Directus. (Frontend consumption detailed in `frd-frontend.md` §6.)
- Mapping is 1:1: a Directus `blog` item + its `en/is/ja` translations becomes `<slug>.en.md` / `<slug>.is.md` / `<slug>.ja.md`; Astro picks the locale entry or the `en` fallback via `localizedEntry`.
- The serialization contract is shared by both sides: front-matter fields (`title/date/category/excerpt/readTime/draft/locale/slug`) for blog; the fixed YAML field set (`slug/locale/order/realm/game?/cat/title/tags/updated/body`) for grimoire.

**Requirements**
- The producer (Directus + `content:pull`) and consumer (Astro collections) **shall** agree on the per-locale file naming (`<slug>.<locale>.<ext>`) and the field set/serialization, validated by `tests/content/*`.
- A single Directus edit, pulled, **shall** produce a snapshot diff containing only that change (determinism contract).

## 7. Open items / inferred gaps
- `[confirm: `pages` collection (structured About/CV/home prose) and `games`/countdowns/wallpapers collections — designed in `.planning/` but not in `setup-schema.mjs`/snapshot.]`
- `[confirm: media/uploads strategy for the static build.]`
- `[confirm: `directus/.env` secret handling (sops) and Postgres-volume backup target.]`
- `[confirm: production translation status — `en` complete, `is` AI-draft pending author review, `ja` intentionally partial via fallback.]`
