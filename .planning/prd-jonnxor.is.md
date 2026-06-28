> **DRAFT — for validation.** Intent inferred from existing artifacts; confirm before relying on it.

# PRD — jonnxor.is

**Status:** Draft (inferred). **Date:** 2026-06-28. **Repo:** `code/jonnxor.is`.

This PRD reverse-engineers intent from the existing artifacts in the repo (`.planning/` design + completion notes, `src/pages/`, `directus/`, `astro.config.mjs`) and the environment vision (`~/dev/notes/dev-environment.md`, `~/dev/CLAUDE.md`). Every inferred claim is tagged `[confirm: …]` for the author to validate.

## Purpose

**jonnxor.is** is the personal website of Jón Agnar Stefánsson ("JonnXor"), a full-stack developer in Reykjavík. It is a single hub combining a **portfolio**, a **blog/saga** (long-form writing on code, myth, and games), a **games hall**, a **CV**, and a **grimoire** (a searchable documentation/notes codex) — wrapped in a distinctive, hand-built **3-theme design system** (`dawn` / `rune` / `neon`) with a Norse-saga visual voice (dragon constellation, runes, realms). Beyond being a calling card, it is the **first real project of the self-hosted dev environment** — the proving ground for the Astro + Directus + .NET stack, the i18n approach, and the Forgejo → Vercel deploy pipeline.

Current pages (`src/pages/`): `index` (home), `about`, `cv`, `portfolio`, `blog` + `blog/[slug]`, `games`, `docs` (the grimoire), `wallpapers`, `countdowns`, and `404`.

## Audience

- `[confirm: primary audience is prospective employers / clients / collaborators evaluating the author's work and skills (portfolio + CV are the conversion surfaces).]`
- `[confirm: secondary audience is fellow developers / readers drawn by the blog and grimoire (technical writing on code, myth, and game design).]`
- `[confirm: a personal/hobby audience for games, wallpapers, and countdowns — these read as self-expression more than recruiting surfaces.]`
- `[confirm: language reach — Icelandic (is) is the default/home audience, English (en) is the broadest reach and load-bearing base, Japanese (ja) is "for fun" and intentionally partial.]`

## Goals

What success looks like:

- A **fast, static, SEO-friendly** site where chrome (nav/footer) renders server-side in the initial HTML — no client-side load-in flash. (Established intent: the Nav/Footer server-render migration.)
- A **single, consistent design system** across all pages — 3 themes, four-tier adaptive navigation, self-hosted fonts — ported faithfully from the original design mockups with no visual regression. `[confirm: visual fidelity to the original `design/` handoff is a hard requirement.]`
- **Content authored once, served in three locales** (is/en/ja) with graceful English fallback so no locale ever 404s or shows a blank — even while translations are incomplete.
- **Content lives in Directus**, not hardcoded in `.astro` files: blog, grimoire, games, and page prose (About/CV/home) all become Directus-owned, translatable records.
- **The build has zero runtime dependency on Directus or any API** — a committed, locale-keyed snapshot is the build contract; CI/Vercel never query a live backend. (Verified: 15 pages build with the stack stopped.)
- **Reproducible + backup-first** per the environment rules: clone + one command runs it; the irreplaceable content snapshot is committed and backed up off-site through Forgejo.
- `[confirm: a working .NET 10 API (business logic) and Blazor admin (logs/monitoring/health/settings) are end-state goals — currently planned, not built.]`
- `[confirm: success metric for reach/recruiting — e.g. inbound contact, or simply "site is live, current, and represents me well." No analytics target is stated in the artifacts.]`

## Architecture

Governing **4-layer** model (stated by the author 2026-06-24 as the project's governing assumption):

- **Astro — Presentation Layer.** All UI: chrome, components, routing, the language switcher, theme system. Static (SSG); vanilla, no UI framework.
- **Directus — Content / Data Layer.** All content where possible — blog, grimoire, games, and page prose — with native is/en/ja translations (base item + `*_translations` pattern). Runs locally in Docker; produces the committed snapshot.
- **.NET 10 Blazor — Business / Logic Layer (API).** All business logic. `[confirm: status is planned, not yet built; concrete responsibilities/endpoints undefined in artifacts.]`
- **.NET 10 Blazor Server — Admin Layer.** Logs, monitoring, health, settings. `[confirm: status is planned, not yet built.]`

**The seam:** Directus (local) → `content:pull` → committed locale-keyed snapshot (`src/content/**`) → Astro SSG build → Vercel. The snapshot is the contract; the content *producer* can evolve (extraction seed → Directus) without the frontend changing.

## Tech stack

- **Astro 6** (6.4.7), fully static (SSG), vanilla — no UI framework, no islands.
- **Package manager:** `[confirm: repo uses pnpm (the `.planning` notes invoke `pnpm build` / `pnpm content:pull`); the environment-level default standardized on npm after pnpm 11.x friction — reconcile which applies to this repo.]`
- **Directus** — headless CMS, local Docker (`directus/docker-compose.yml`, schema-as-code via `directus/scripts/setup-schema.mjs` + `directus/schema/snapshot.yaml`), env via sops + direnv.
- **.NET 10** — Blazor API (business logic) + Blazor Server (admin). Planned.
- **Design system** — 3 themes (`dawn` / `rune` / `neon`, `rune` default), four-tier adaptive nav, theme orb, `site.css` tokens; behavior in `public/assets/site.js`, markup server-rendered in `Nav.astro` / `Footer.astro`.
- **i18n** — `locales: ['is','en','ja']`, `defaultLocale: 'is'` at root `/`, `/en/`, `/ja/` (`prefixDefaultLocale: false`), English as authoring base + load-bearing fallback resolved at lookup time. Dictionary (`src/i18n/ui.ts` + `utils.ts`) holds chrome/UI strings only; all real content comes from the snapshot.
- **Self-hosted fonts** — `[confirm: on the Astro-native backlog; confirm whether already implemented.]`
- **Deploy** — Forgejo CI → Vercel (`preview` branch → preview.jonnxor.is, `main` → production). The `github` remote push-mirrors to the live Vercel site — never pushed casually.
- **Testing** — Vitest-style unit tests (`tests/content/**`, `tests/i18n/**`), `astro check`; Playwright E2E `[confirm: planned per env vision, confirm if present in this repo]`.

## Non-goals

- `[confirm:]` **No SSR / live backend at request time.** The site stays SSG; no Astro adapter; CI/Vercel never query Directus or the API.
- `[confirm:]` **No production Directus hosting.** Directus is a local authoring tool; the committed snapshot is the deploy contract.
- `[confirm:]` **No redesign.** Themes, layout, nav tiers, and fonts are ported verbatim — visual change is explicitly out of scope for the migration work.
- `[confirm:]` **Not full translation up front.** The mechanism + seam come first; IS reaches parity incrementally and JA stays partial "for fun" via fallback. No placeholder/invented prose.
- `[confirm:]` **No Accept-Language auto-redirect** (deferred); the switcher remembers an explicit choice only.
- `[confirm:]` **Not a multi-author CMS / no public user accounts / no e-commerce** — it is a single-person personal site.

## Open questions

- **Audience & success metrics** — who is this primarily for, and what concretely counts as success (recruiting outcomes? readership? "it's live and current"?)? No metrics appear in the artifacts.
- **.NET API scope** — what business logic does the API actually own on a static personal site? Contact form handling, a "Saga Tracker"/quest-log feature (hinted on the home "Now building" strip), analytics, something else? Currently undefined.
- **Blazor admin scope** — "logs, monitoring, health, settings" of *what*, given the site is static and Directus is local? Confirm the real responsibility.
- **Package manager** — pnpm (per repo `.planning` commands) vs npm (per env-level standardization). Which governs this repo?
- **Self-hosted fonts & Playwright E2E** — implemented yet, or still backlog?
- **`games`, `wallpapers`, `countdowns` content modeling** — are these Directus-owned like blog/grimoire/pages, or do `wallpapers`/`countdowns` stay hand-built? `games` is slated for Directus; the other two are unspecified.
- **Translation ownership & priority** — which pages/posts must reach IS parity first, and is JA truly best-effort indefinitely?
- **Domain/deploy specifics** — confirm `preview.jonnxor.is` ↔ `preview` branch and `jonnxor.is` ↔ `main`, and the Coolify-vs-Vercel end state (env vision mentions Coolify as the planned self-hosted deploy target; this repo currently deploys to Vercel).
- **Live status** — which layers are actually shipped today (Astro frontend + i18n mechanism + local Directus appear done through grimoire; pages/games, .NET API, and admin appear not started).
