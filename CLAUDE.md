# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Personal site of Jón Agnar Stefánsson ("JonnXor") — portfolio, blog/saga, games hall, CV, and grimoire (docs codex). Astro 6, fully static (SSG), **vanilla — no UI framework, no islands**. Trilingual (is/en/ja) with a hand-built three-theme design system.

## Commands

```sh
pnpm install             # pnpm pinned via packageManager (corepack) / mise
pnpm dev                 # dev server → http://localhost:4321
pnpm build               # static build to ./dist/
pnpm test                # Vitest: tests/{content,i18n,render}
pnpm exec vitest run tests/i18n/utils.test.ts   # single test file
pnpm exec astro check    # type-check
```

**Playwright never runs on the host** — browsers aren't installed in WSL. All E2E and visual runs go through the pinned container (`mcr.microsoft.com/playwright:v1.61.1-noble`) via `scripts/pw.sh`, so local runs match CI byte-for-byte:

```sh
pnpm test:e2e            # = sh scripts/pw.sh test tests/e2e
pnpm test:visual         # = sh scripts/pw.sh test tests/visual
sh scripts/pw.sh test tests/visual --update-snapshots   # re-record baselines (review diff in PR)
sh scripts/pw.sh test tests/e2e/blog.spec.ts            # single spec
```

Playwright's `webServer` builds and previews on port 4321 itself. Vitest owns `*.test.ts`, Playwright owns `*.spec.ts` — both live under `tests/`.

Content/CMS (needs Docker + `directus/.env`, decrypted via sops/direnv):

```sh
pnpm directus:up         # local Directus stack
pnpm content:pull        # refresh committed snapshot from Directus → src/content/**
pnpm directus:snapshot   # export schema-as-code → directus/schema/snapshot.yaml
```

## Architecture

Governing 4-layer model: **Astro** (presentation, this repo's frontend), **Directus** (content/data, local Docker), **.NET 10 Blazor** API + admin (*planned* — when they land, each ships its own xUnit test project wired into CI).

**The content seam is the key invariant:** Directus (local) → `content:pull` → committed, locale-keyed snapshot in `src/content/**` → Astro SSG build. The committed snapshot is the build contract — CI and Vercel never query a live backend; the site builds with the stack stopped. Never hand-edit generated snapshot content expecting it to persist; the source of truth is Directus (schema in `directus/schema/snapshot.yaml`).

### i18n

- `locales: ['is', 'en', 'ja']`, `defaultLocale: 'is'` served at `/`, with `/en/` and `/ja/` prefixes; `fallbackType: 'rewrite'` (en/ja rewrite to is) so no locale ever 404s.
- **English is the authoring base** and load-bearing fallback for content and UI strings.
- Content files are one file per locale: `<slug>.<locale>.md` (blog) / `<slug>.<locale>.yaml` (grimoire, games, pages, countdowns, wallpapers). EVERY collection in `src/content.config.ts` MUST keep `generateId: localeEntryId` (from `src/content/loaders.ts`) or a slug's locale files collide on one id and silently overwrite each other — guarded by `tests/content/config-wiring.test.ts` (count updated per collection).
- The sync pipeline is descriptor-driven: one entry per collection in `scripts/lib/collections.mjs` (read its `CollectionDescriptor` contract JSDoc before adding one); serialization determinism via `scripts/lib/entry-yaml.mjs`. Snapshot YAML must never contain comment nodes (an unquoted mid-scalar ` #` silently truncates values through restore) — guarded by `tests/content/snapshot-comments.test.ts`.
- Per-page `pages.sections` shapes live in `src/content/page-sections.ts` (schema + inferred type share one source, imported by both `content.config.ts` and the page).
- Chrome/UI strings live in `src/i18n/ui.ts`; `useTranslations` in `src/i18n/utils.ts` falls back lang → en → key.

### Frontend

- Routes in `src/pages/`; single layout `src/layouts/Base.astro`; nav/footer are **server-rendered** in `src/components/Nav.astro` / `Footer.astro`.
- Design system: `public/assets/tokens.css` defines three themes (`dawn` / `rune` default / `neon`) sharing one token vocabulary via `data-theme` on `<html>`. **Never hardcode themed colors — always use the `--*` custom properties**, and design every new component in all three themes (dawn: zero glow; neon: glows at rest via `--ambient-*`). Components in `public/assets/site.css`, behavior (adaptive nav tiers, theme orb, search, `window.JX`) in `public/assets/site.js`.
- Canonical design-system reference (tokens, specimen cards, brand rules): `design/design-system/` (has its own SKILL.md/README).
- Grimoire entries are YAML whose `body` is hand-authored HTML injected as-is by the client-side reader on `/docs`; markdown code blocks render unhighlighted on purpose (`syntaxHighlight: false`, themed by site CSS).

### Testing & CI

Test pyramid: Vitest unit tests for content/i18n logic plus Astro Container-API render tests (`tests/render/` — components and non-collection pages only: the Container can't load `astro:content` under Vitest, and `vitest.config.ts` passes `devToolbar: {enabled: false}` as `getViteConfig`'s second arg so rendered HTML carries no debug attributes), then containerized Playwright E2E (`tests/e2e/`) and self-baseline visual regression (`tests/visual/`, `/countdowns` excluded — live-data layout). No feature merges without a test. CI (`.forgejo/workflows/ci.yml`): Vitest on every push; e2e + visual on PRs and the `preview` branch.

### Deploy & secrets

`git push` → Forgejo → push-mirror to GitHub → Vercel. `preview` branch → preview.jonnxor.is; `main` → production — **never push main casually**.
Production is currently in **construction mode**: `vercel.json` on `main` 307-redirects
everything to `/construction/` while the site is rebuilt on `preview`; go-live = merge
`preview` → `main` and delete `vercel.json` + `public/construction/` in that commit
(spec: `.planning/2026-07-04-under-construction-design.md` §3). Vercel needs `ENABLE_EXPERIMENTAL_COREPACK=1`. Secrets are sops-encrypted (`.sops.yaml`, direnv via `.envrc`); never commit plaintext env files.

### Planning docs

Design/plan documents for each work cycle live in `.planning/` (dated `*-design.md` / `*-plan.md`, plus PRD/FRDs) — check there for intent behind recent changes.
