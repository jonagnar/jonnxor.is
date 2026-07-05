# jonnxor.is

> The personal website of Jón Agnar Stefánsson — portfolio, saga, games hall, CV, and grimoire, in a hand-built Norse-saga design system.

## What it is

**jonnxor.is** is the personal site of Jón Agnar Stefánsson ("JonnXor"), a full-stack
developer in Reykjavík. It is a single hub combining a **portfolio**, a **blog/saga**
(long-form writing on code, myth, and games), a **games hall**, a **CV**, and a
**grimoire** (a searchable docs/notes codex) — wrapped in a distinctive three-theme
design system (`dawn` / `rune` / `neon`) with a Norse-saga visual voice.

It is also the **first real project** of a self-hosted, backup-first dev environment: the
proving ground for the Astro + Directus + .NET stack, the trilingual (is/en/ja) i18n
approach, and the Forgejo → Vercel deploy pipeline. This is a monorepo: the Astro
frontend lives in `client/`, a .NET 10 seam-validation worker lives in `api/`, and both
share the top-level `tests/`.

## Architecture

A governing **4-layer** model separates presentation, content, business logic, and admin:

- **Astro — Presentation layer.** All UI: chrome, components, routing, the language
  switcher, the theme system. Fully static (SSG), vanilla — no UI framework, no islands.
  Lives in `client/`.
- **Directus — Content / data layer.** All content where possible — blog, grimoire,
  games, and page prose — with native is/en/ja translations. Runs locally (in the
  separate infra repo) and produces a committed, locale-keyed snapshot. The snapshot is
  the build contract: CI and Vercel never query a live backend.
- **.NET 10 — Business / logic layer (API).** Currently a **build-time seam validator**
  (`api/Jonnxor.Api`): it checks the committed content snapshot for drift (missing
  locale files, comment-node truncation, snapshot↔Directus field mismatches) that the
  Node pull/restore pipeline can't catch itself violating. It's read-only — no
  create/update/delete path anywhere — and never runs on the Astro build path. A full
  Blazor business-logic API is still *planned*.
- **.NET 10 Blazor Server — Admin layer.** Logs, monitoring, health, settings. *Planned.*

**The seam:** Directus (local) → `content:pull` → committed snapshot (`client/src/content/**`) →
Astro SSG build → Vercel. The content producer can evolve without the frontend changing.

## Tech stack

- **Astro 6** (`^6.4.7`) — fully static (SSG), vanilla, no UI framework.
- **i18n** — `locales: ['is', 'en', 'ja']`, `defaultLocale: 'is'` served at `/` with
  `/en/` and `/ja/` (`prefixDefaultLocale: false`). English is the authoring base and
  load-bearing fallback (`en`/`ja` rewrite to `is`), so no locale ever 404s.
- **Directus** (`@directus/sdk ^22`) — headless CMS. Runtime (compose stack, data,
  uploads) lives in the separate infra repo; this repo holds only the schema contract
  (`directus/schema/snapshot.yaml`) and client-side env, via sops + direnv.
- **.NET 10** — a console seam-validator (`api/Jonnxor.Api`) today; Blazor business API +
  admin are *planned*.
- **Design system** — three themes (`dawn` / `rune` / `neon`, `rune` default), a
  four-tier adaptive nav, theme orb, and self-hosted fonts; markup server-rendered in
  `Nav.astro` / `Footer.astro`.
- **Tooling** — pnpm (`pnpm@10.34.4`), mise (toolchain pinning), Node `>=22.12.0`,
  TypeScript 6, Vitest for unit tests, `astro check`, .NET 10 SDK + xUnit.

## Develop

The Astro workspace lives in `client/` — run `pnpm` commands from there.

```sh
mise install         # pin the toolchain (Node, pnpm)
cd client
pnpm install         # install dependencies
pnpm dev             # start the dev server → http://localhost:4321
```

The .NET seam validator needs the SDK version pinned in `global.json` (installed
separately — not via mise).

Other useful scripts (from `client/`):

```sh
pnpm test                # run the Vitest suite (../tests/{content,i18n,render})
pnpm exec astro check    # type-check the project
pnpm content:pull        # refresh the committed content snapshot from Directus
```

Directus itself (schema setup/export) is driven from `client/` too, against a stack
started from the infra repo — see `directus/` and `CLAUDE.md` for the exact commands.

## Build

```sh
pnpm build       # (from client/) build the static site to ./dist/
pnpm preview     # preview the production build locally
```

The build has **zero runtime dependency** on Directus, any API, or the .NET worker —
the committed snapshot is the only content source, so the site builds with every other
stack stopped.

## Testing

| Command | Scope |
|---|---|
| `pnpm test` (from `client/`) | Vitest — content/i18n logic + Astro Container-API rendering tests (`tests/{content,i18n,render}`) |
| `pnpm test:e2e` (from `client/`) | Playwright E2E of the interactive pages (`tests/e2e`) |
| `pnpm test:visual` (from `client/`) | Playwright self-baseline visual regression (`tests/visual`) |
| `dotnet test jonnxor.sln` (from repo root) | xUnit tests for the .NET seam validator (`tests/Jonnxor.Api.Tests`) |

**Playwright runs in a pinned container.** Browsers are not installed in WSL; all Playwright
(E2E + visual) runs inside `mcr.microsoft.com/playwright:v1.61.1-noble` via `scripts/pw.sh`,
so local runs match CI byte-for-byte and visual baselines are deterministic.

```sh
sh scripts/pw.sh test ../tests/visual --update-snapshots  # re-record baselines (review the diff in the PR)
```

Visual baselines are captured from the current site (self-baseline): a change that alters
rendered output fails the gate, and an *intended* change passes only after a reviewed
`--update-snapshots`. CI runs Vitest on every push; the Playwright e2e + visual suite runs
on pull requests and on the `preview` branch; a dedicated `dotnet` CI job builds and tests
`jonnxor.sln` and runs the offline seam verifier against the committed snapshot on every push.

### Convention: business/admin layers ship their own tests

The .NET seam validator already ships its own xUnit project (`tests/Jonnxor.Api.Tests`),
wired into CI from day one. When a full Blazor business API and admin land, each follows
the same rule — the same way no feature merges here without a test.

## Deploy

Deploys flow through self-hosted git: `git push` → **Forgejo** → push-mirror to
**GitHub** → **Vercel** builds and serves.

- `preview` branch → **preview.jonnxor.is**
- `main` branch → **jonnxor.is** (production — never pushed casually)

Vercel must run with `ENABLE_EXPERIMENTAL_COREPACK=1` so the pinned pnpm version
(`packageManager` in `client/package.json`) is honoured during the build.

## Repo structure

```text
/
├── client/               # Astro workspace (the only pnpm workspace)
│   ├── src/
│   │   ├── pages/        # routes: index, about, cv, portfolio, blog (+ [slug]),
│   │   │                 #         games, docs (grimoire), wallpapers, countdowns, 404
│   │   ├── content/      # committed, locale-keyed snapshot (the build contract)
│   │   └── i18n/         # chrome/UI string dictionary (ui.ts + utils.ts)
│   ├── scripts/          # content import / pull / restore
│   ├── public/           # static assets (site.css, site.js, fonts)
│   ├── design/           # design-system reference + static mockups
│   └── docs/             # project documentation (see below)
├── api/
│   └── Jonnxor.Api/      # .NET 10 console worker: build-time seam validator
├── directus/             # schema contract only (schema/, .env); runtime lives in the infra repo
├── tests/                # Vitest (content/, i18n/, render/) + Playwright (e2e/, visual/)
│                         #   + Jonnxor.Api.Tests/ (xUnit, for the .NET worker)
├── .planning/            # dated design/plan docs, PRD/FRDs for each work cycle
├── .forgejo/             # CI workflows
├── jonnxor.sln           # top-level .NET solution (api/ + tests/Jonnxor.Api.Tests)
└── global.json           # .NET SDK version pin
```

## Docs

Project documentation lives in [`client/docs/`](./client/docs/) — start with the
[docs landing page](./client/docs/README.mdx).
