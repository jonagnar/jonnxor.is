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
approach, and the Forgejo → Vercel deploy pipeline.

## Architecture

A governing **4-layer** model separates presentation, content, business logic, and admin:

- **Astro — Presentation layer.** All UI: chrome, components, routing, the language
  switcher, the theme system. Fully static (SSG), vanilla — no UI framework, no islands.
- **Directus — Content / data layer.** All content where possible — blog, grimoire,
  games, and page prose — with native is/en/ja translations. Runs locally in Docker and
  produces a committed, locale-keyed snapshot. The snapshot is the build contract: CI and
  Vercel never query a live backend.
- **.NET 10 Blazor — Business / logic layer (API).** All business logic. *Planned.*
- **.NET 10 Blazor Server — Admin layer.** Logs, monitoring, health, settings. *Planned.*

**The seam:** Directus (local) → `content:pull` → committed snapshot (`src/content/**`) →
Astro SSG build → Vercel. The content producer can evolve without the frontend changing.

## Tech stack

- **Astro 6** (`^6.4.7`) — fully static (SSG), vanilla, no UI framework.
- **i18n** — `locales: ['is', 'en', 'ja']`, `defaultLocale: 'is'` served at `/` with
  `/en/` and `/ja/` (`prefixDefaultLocale: false`). English is the authoring base and
  load-bearing fallback (`en`/`ja` rewrite to `is`), so no locale ever 404s.
- **Directus** (`@directus/sdk ^22`) — headless CMS, local Docker, schema-as-code
  (`directus/schema/snapshot.yaml`), env via sops + direnv.
- **.NET 10** — Blazor API (business logic) + Blazor Server (admin). *Planned.*
- **Design system** — three themes (`dawn` / `rune` / `neon`, `rune` default), a
  four-tier adaptive nav, theme orb, and self-hosted fonts; markup server-rendered in
  `Nav.astro` / `Footer.astro`.
- **Tooling** — pnpm (`pnpm@10.34.4`), mise (toolchain pinning), Node `>=22.12.0`,
  TypeScript 6, Vitest for unit tests, `astro check`.

## Develop

```sh
mise install     # pin the toolchain (Node, pnpm)
pnpm install     # install dependencies
pnpm dev         # start the dev server → http://localhost:4321
```

Other useful scripts:

```sh
pnpm test                # run the Vitest suite
pnpm astro check         # type-check the project
pnpm directus:up         # start local Directus (Docker)
pnpm content:pull        # refresh the committed content snapshot from Directus
```

## Build

```sh
pnpm build       # build the static site to ./dist/
pnpm preview     # preview the production build locally
```

The build has **zero runtime dependency** on Directus or any API — the committed snapshot
is the only content source, so the site builds with the stack stopped.

## Testing

| Command | Scope |
|---|---|
| `pnpm test` | Vitest — content/i18n logic + Astro Container-API rendering tests (`tests/{content,i18n,render}`) |
| `sh scripts/pw.sh test tests/e2e` | Playwright E2E of the interactive pages (`tests/e2e`) |
| `sh scripts/pw.sh test tests/visual` | Playwright self-baseline visual regression (`tests/visual`) |

**Playwright runs in a pinned container.** Browsers are not installed in WSL; all Playwright
(E2E + visual) runs inside `mcr.microsoft.com/playwright:v1.61.1-noble` via `scripts/pw.sh`,
so local runs match CI byte-for-byte and visual baselines are deterministic.

```sh
sh scripts/pw.sh test tests/visual --update-snapshots  # re-record baselines (review the diff in the PR)
```

Visual baselines are captured from the current site (self-baseline): a change that alters
rendered output fails the gate, and an *intended* change passes only after a reviewed
`--update-snapshots`. CI runs Vitest on every push; the Playwright e2e + visual suite runs
on pull requests and on the `preview` branch.

### Convention: .NET layers ship their own tests

When the cycle-3 Business API (.NET 10) and cycle-4 Blazor admin land, each **ships its own
xUnit test project from day one**, wired into CI — the same way no feature merges here
without a test.

## Deploy

Deploys flow through self-hosted git: `git push` → **Forgejo** → push-mirror to
**GitHub** → **Vercel** builds and serves.

- `preview` branch → **preview.jonnxor.is**
- `main` branch → **jonnxor.is** (production — never pushed casually)

Vercel must run with `ENABLE_EXPERIMENTAL_COREPACK=1` so the pinned pnpm version
(`packageManager` in `package.json`) is honoured during the build.

## Repo structure

```text
/
├── src/
│   ├── pages/        # routes: index, about, cv, portfolio, blog (+ [slug]),
│   │                 #         games, docs (grimoire), wallpapers, countdowns, 404
│   ├── content/      # committed, locale-keyed snapshot (the build contract)
│   └── i18n/         # chrome/UI string dictionary (ui.ts + utils.ts)
├── directus/         # local Directus: docker-compose, schema-as-code, scripts
├── scripts/          # content import / pull / restore
├── tests/            # Vitest (content/**, i18n/**, render/**) + Playwright (e2e/**, visual/**)
├── public/           # static assets (site.css, site.js, fonts)
├── docs/             # project documentation (see below)
└── astro.config.mjs
```

## Docs

Project documentation lives in [`docs/`](./docs/) — start with the
[docs landing page](./docs/README.mdx).
