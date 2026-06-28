# i18n Phase 2 — Directus blog slice — Completion & Phase-3 handoff

**Date:** 2026-06-26
**Branch:** `feat/directus-blog` (off `origin/preview`) — 9 commits, all local, **not pushed**.
**Plan:** [`2026-06-24-i18n-phase2-directus-blog-plan.md`](./2026-06-24-i18n-phase2-directus-blog-plan.md) · **Spec:** [`2026-06-24-i18n-phase2-directus-blog-design.md`](./2026-06-24-i18n-phase2-directus-blog-design.md)
**Execution:** subagent-driven (fresh implementer per task + spec review + code-quality review each), per the plan's recommended flow.

## Outcome — phase goal met
The Directus → committed-Markdown-snapshot → per-locale Astro render pipeline is proven end-to-end:
- Local Directus 11.3.5 + Postgres 16 (`directus/docker-compose.yml`, `:8055`) holds the `languages`/`blog`/`blog_translations` model (schema-as-code in `setup-schema.mjs`, committed `snapshot.yaml`).
- `content:import` seeded the 5 existing posts as the `en` base; `content:pull` writes per-locale `src/content/blog/<slug>.<locale>.md` and prunes legacy files (deterministic — byte-identical re-run).
- Astro renders blog per-locale with English fallback (`localizedPost`/`uniqueSlugs` in `src/i18n/blog.ts`; locale-aware `blog.astro` + `blog/[slug].astro`).
- **Build has zero build-time Directus dependency** — verified building with the stack stopped (15 pages).
- Quality gates green: **15 unit tests**, `astro check` **0 errors**, build **15 pages**.

## Commits
```
c947f79 fix(content): give per-locale blog files distinct collection ids
2050f3f feat(i18n): render blog per-locale from the Directus snapshot (en fallback)
beaa7db feat(i18n): localizedPost helper with English fallback
01dc1b0 feat(content): content:pull writes per-locale blog snapshot from Directus
698cfda feat(content): one-time import of existing posts into Directus (en base)
168bc11 feat(content): pure parse/serialize helpers for per-locale post markdown
fffe54b feat(directus): blog content model (languages + blog + translations) as schema-as-code
24bad5e feat(directus): local Postgres + Directus 11 compose stack
266d2e6 chore(directus): add sdk/gray-matter deps + scripts + gitignore
```

## Key bug caught by end-to-end verification (Task 9)
**`generateId` collision (commit c947f79).** Astro's glob loader derives the entry `id` from the frontmatter `slug` when present, so every locale file of a post (`<slug>.en.md`, `<slug>.is.md`) collided on `id` and overwrote each other — silently destroying the `en` base the instant a second locale file appeared. **Latent under en-only content**, surfaced only when Task 9 added a real `is` file. Fix: `generateId: ({ entry }) => entry.replace(/\.md$/, '')` so each locale file is a distinct entry. (The "every post needs an en base" guard added in `[slug].astro` is what caught it, throwing a clear error instead of an opaque crash.)

## Deviations from the plan's verbatim scripts (all necessary, verified)
- **SDK login is an object payload** in `@directus/sdk@22`: `client.login({ email, password })`, NOT the plan's positional `client.login(email, password)` (positional throws a `'otp' in <password>` TypeError). Applies to all SDK scripts.
- **`languages.code` PK length:** `createCollection`/`updateField` can't set/alter a string-PK length in Directus 11.3.5; used a raw `ALTER TABLE languages ALTER COLUMN code TYPE varchar(8)` and the committed `snapshot.yaml` (`schema apply`) is the authoritative reproduction. `setup-schema.mjs` has an idempotent guard.
- **`content-pull.mjs` ends with `process.exit(0)`** — the authenticated SDK client schedules a token-refresh timer that keeps Node's event loop alive, hanging the script after success (breaks `content:pull && …` chains). (Empirically the one-shot `setup-schema`/`content-import` happen to exit cleanly, so they lack it — see handoff #2.)

## Phase-3 handoff items (deferred, NOT blockers)
1. **Multi-locale demonstration + regression guard (most important).** The committed snapshot is **en-only** (5 `*.en.md`). The non-fallback locale branch and the `generateId` fix therefore have **no committed build-artifact guard** — a revert of `content.config.ts` `generateId` would still build green until a real 2-locale file exists. Phase 3's first real `is`/`ja` translation closes this automatically (the build then enforces it). *Decision pending with the site owner:* whether to commit one real `*.is.md` before the PR to demonstrate the signature feature, or keep the branch en-only and let Phase 3 provide the guard. (Do NOT ship placeholder "PRÓFUN" content.)
2. **SDK-script consistency.** `process.exit(0)` + the token-refresh comment exist only in `content-pull.mjs`; add to `setup-schema.mjs`/`content-import.mjs` (or factor a shared `scripts/lib/directus-client.mjs` covering `createDirectus().with(rest()).with(authentication())` + login + teardown) to prevent drift as Phase 3 adds collections.
3. **Authoring-docs note:** a post with no `en` base fails the build for ALL locales (the en-base invariant guard in `[slug].astro`). Document this for content authors.
4. **Grimoire / pages / games** repeat this pattern (grimoire closest); **production Directus hosting** still deferred — the committed snapshot stays the contract; **real is/ja authoring** needs the Directus admin UI reachable from Windows (host reboot for the WSL mirrored-networking relay).

## Post-merge addendum (2026-06-26, after Phase 2 merged to preview + main)
- **`fix/blog-ts` deleted** (local + origin) — it was superseded by Task 8 (same two files, same ACCENT/date-helper type fix); not opened as a PR.
- **Directus admin password rotation gotcha:** `ADMIN_EMAIL`/`ADMIN_PASSWORD` in `directus/.env` are only read when Directus **bootstraps an empty DB**. Editing them after first boot does nothing to the existing admin user. To rotate: change it in the admin UI (then mirror into `.env` for the scripts), OR re-bootstrap from a wiped DB. Re-bootstrap procedure (no sudo): `directus:down` → wipe `directus/data` via a root alpine container (`docker run --rm -v <abs>/directus/data:/d alpine sh -c 'rm -rf /d/* /d/.[!.]* /d/..?*'`) → `directus:up` (fresh admin from current `.env`) → `directus:schema` → `content:restore`. (Done this session to make the user's Bitwarden creds authoritative.)
- **Content-rehydration gap CLOSED via `content:restore`** (new `scripts/content-restore.mjs`, branch `feat/content-restore`): `content:import` was one-time (reads the now-gone un-suffixed `.md`), so a fresh clone could reproduce the *schema* (snapshot.yaml) but not the *content*. `content:restore` is the inverse of `content:pull` (snapshot → Directus), making the dev stack fully reproducible. Idempotent at the post level (skips existing slugs); does not reconcile per-translation diffs on existing posts (rehydrate-empty is its purpose).
- **Open handoff still:** `process.exit(0)` consistency across the 3 original SDK scripts; multi-locale build-artifact demo/regression-guard arrives with Phase-3 real translations.
