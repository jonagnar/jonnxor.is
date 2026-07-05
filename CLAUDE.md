# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Personal site of Jón Agnar Stefánsson ("JonnXor") — portfolio, blog/saga, games hall, CV, and grimoire (docs codex). Astro 6, fully static (SSG), **vanilla — no UI framework, no islands**. Trilingual (is/en/ja) with a hand-built three-theme design system.

## Commands

The JS/Astro workspace lives in `client/` — run all `pnpm` commands from there (`cd client` first).

```sh
pnpm install             # pnpm pinned via packageManager (corepack) / mise
pnpm dev                 # dev server → http://localhost:4321
pnpm build               # static build to ./dist/
pnpm test                # Vitest: ../tests/{content,i18n,render}
pnpm exec vitest run ../tests/i18n/utils.test.ts   # single test file
pnpm exec astro check    # type-check
```

**Playwright never runs on the host** — browsers aren't installed in WSL. All E2E and visual runs go through the pinned container (`mcr.microsoft.com/playwright:v1.61.1-noble`) via `scripts/pw.sh` (from `client/`), so local runs match CI byte-for-byte:

```sh
pnpm test:e2e            # = sh scripts/pw.sh test ../tests/e2e
pnpm test:visual         # = sh scripts/pw.sh test ../tests/visual
sh scripts/pw.sh test ../tests/visual --update-snapshots   # re-record baselines (review diff in PR)
sh scripts/pw.sh test ../tests/e2e/blog.spec.ts            # single spec
```

Playwright's `webServer` builds and previews on port 4321 itself. Vitest owns `*.test.ts`, Playwright owns `*.spec.ts` — both live under top-level `tests/`.

Content/CMS: the Directus **runtime** (compose stack, stack env, `data/`, `uploads/`) lives in the **infra repo** — runbook: `~/dev/src/infra/directus/README.md` (up/down from there: `cd ~/dev/src/infra/directus && docker compose up -d`). This repo's `directus/` holds only the **schema contract** (`schema/snapshot.yaml` + `scripts/setup-schema.mjs`) and the client-side env (`directus/.env`: `DIRECTUS_URL` + admin creds, decrypted via sops/direnv). With no compose file here, the worktree stack-hijack accident class (a worktree `directus:up` silently creating a fresh empty data dir) is structurally gone. Content scripts run from `client/` and resolve `directus/` as `../directus/`:

```sh
pnpm content:pull        # refresh committed snapshot from Directus → src/content/**
pnpm directus:schema     # idempotent schema setup against the running stack
pnpm directus:snapshot   # export schema-as-code → directus/schema/snapshot.yaml (execs container jonnxor-directus-directus-1 by name)
```

## Architecture

Governing 4-layer model: **Astro** (presentation, this repo's frontend), **Directus** (content/data, local Docker), **.NET 10 Blazor** API + admin (*planned* — when they land, each ships its own xUnit test project wired into CI).

**The content seam is the key invariant:** Directus (local) → `content:pull` → committed, locale-keyed snapshot in `client/src/content/**` → Astro SSG build. The committed snapshot is the build contract — CI and Vercel never query a live backend; the site builds with the stack stopped. Never hand-edit generated snapshot content expecting it to persist; the source of truth is Directus (schema in `directus/schema/snapshot.yaml`).

### i18n

- `locales: ['is', 'en', 'ja']`, `defaultLocale: 'is'` served at `/`, with `/en/` and `/ja/` prefixes; `fallbackType: 'rewrite'` (en/ja rewrite to is) so no locale ever 404s.
- **English is the authoring base** and load-bearing fallback for content and UI strings.
- Content files are one file per locale: `<slug>.<locale>.md` (blog) / `<slug>.<locale>.yaml` (grimoire, games, pages, countdowns, wallpapers, projects). EVERY collection in `client/src/content.config.ts` MUST keep `generateId: localeEntryId` (from `client/src/content/loaders.ts`) or a slug's locale files collide on one id and silently overwrite each other — guarded by `tests/content/config-wiring.test.ts` (count updated per collection).
- The sync pipeline is descriptor-driven: one entry per collection in `client/scripts/lib/collections.mjs` (read its `CollectionDescriptor` contract JSDoc before adding one); serialization determinism via `client/scripts/lib/entry-yaml.mjs`. Snapshot YAML must never contain comment nodes (an unquoted mid-scalar ` #` silently truncates values through restore) — guarded by `tests/content/snapshot-comments.test.ts`.
- Per-page `pages.sections` shapes live in `client/src/content/page-sections.ts` (schema + inferred type share one source, imported by both `content.config.ts` and the page).
- Chrome/UI strings live in `client/src/i18n/ui.ts`; `useTranslations` in `client/src/i18n/utils.ts` falls back lang → en → key.

### Frontend

- Routes in `client/src/pages/`; single layout `client/src/layouts/Base.astro`; nav/footer are **server-rendered** in `client/src/components/Nav.astro` / `Footer.astro`.
- Design system: `client/public/assets/tokens.css` defines three themes (`dawn` / `rune` default / `neon`) sharing one token vocabulary via `data-theme` on `<html>`. **Never hardcode themed colors — always use the `--*` custom properties**, and design every new component in all three themes (dawn: zero glow; neon: glows at rest via `--ambient-*`). Components in `client/public/assets/site.css`, behavior (adaptive nav tiers, theme orb, search, `window.JX`) in `client/public/assets/site.js`.
- Canonical design-system reference (tokens, specimen cards, brand rules): `client/design/design-system/` (has its own SKILL.md/README).
- Grimoire entries are YAML whose `body` is hand-authored HTML injected as-is by the client-side reader on `/docs`; markdown code blocks render unhighlighted on purpose (`syntaxHighlight: false`, themed by site CSS).

### Testing & CI

Test pyramid: Vitest unit tests for content/i18n logic plus Astro Container-API render tests (`tests/render/` — components and non-collection pages only: the Container can't load `astro:content` under Vitest, and `vitest.config.ts` passes `devToolbar: {enabled: false}` as `getViteConfig`'s second arg so rendered HTML carries no debug attributes), then containerized Playwright E2E (`tests/e2e/`) and self-baseline visual regression (`tests/visual/`, `/countdowns` excluded — live-data layout). Tests live at the repo root (`tests/`), outside `client/` — imports and configs cross that boundary with `../`. No feature merges without a test. CI (`.forgejo/workflows/ci.yml`): both jobs run with `working-directory: client`; Vitest on every push; e2e + visual on PRs and the `preview` branch.

### API worker (.NET, seam validator)

Top-level solution `jonnxor.sln`, sibling to `client/`, wires two projects: `api/Jonnxor.Api`
(the console app) and `tests/Jonnxor.Api.Tests` (xUnit) alongside the JS suites under
`tests/`. It is a **build-time worker only — never on the Astro build path**: CI/Vercel
build the site from the committed snapshot alone, with the .NET toolchain absent from that
path entirely. Its job is to catch seam drift the Node pull/restore pipeline itself can't
see itself violating (missing locale files, comment-node truncation, snapshot↔Directus
field drift) — it never writes to Directus or the snapshot; there is no create/update/delete
path anywhere in the client.

Commands (run from the repo root, not `client/`):

```sh
# offline: snapshot invariants only, no Directus — this is what CI runs on every push
dotnet run --project api/Jonnxor.Api -- verify --offline --content client/src/content

# live: offline rules first (fails fast before touching Directus), then a generic
# field-by-field equivalence check against a running Directus instance. direnv does not
# reach into worktrees, so --env must be passed explicitly there (main checkout can rely
# on the exported vars instead).
dotnet run --project api/Jonnxor.Api -- verify --live --content client/src/content --env directus/.env

# per-locale translation coverage table, offline; --json also writes a machine-readable artifact
dotnet run --project api/Jonnxor.Api -- report
dotnet run --project api/Jonnxor.Api -- report --json coverage.json
```

CI (`.forgejo/workflows/ci.yml`) runs a dedicated `dotnet` job on every push (container
`mcr.microsoft.com/dotnet/sdk:10.0`, no `working-directory` default — it runs at repo root):
`dotnet build jonnxor.sln --configuration Release` → `dotnet test jonnxor.sln --configuration
Release --no-build` → `verify --offline` against the real committed snapshot. `verify --live`
is a manual/local-only gate — it needs a reachable Directus and is not part of CI.

### Deploy & secrets

`git push` → Forgejo → push-mirror to GitHub → Vercel. `preview` branch → preview.jonnxor.is; `main` → production — **never push main casually**.
Production is currently in **construction mode**: `vercel.json` on `main` 307-redirects
everything to `/construction/` while the site is rebuilt on `preview`; go-live = merge
`preview` → `main` and delete `vercel.json` + `client/public/construction/` in that commit
(spec: `.planning/2026-07-04-under-construction-design.md` §3). Vercel needs `ENABLE_EXPERIMENTAL_COREPACK=1`. Secrets are sops-encrypted (`.sops.yaml`, direnv via `.envrc`); never commit plaintext env files.

### Planning docs

Design/plan documents for each work cycle live in `.planning/` (dated `*-design.md` / `*-plan.md`, plus PRD/FRDs) — check there for intent behind recent changes.
