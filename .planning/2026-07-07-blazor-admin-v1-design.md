# Design — Blazor Server admin, thin v1 (operator console)

**Date:** 2026-07-07
**Status:** Approved (design sign-off 2026-07-07)
**Scope:** sub-project 3 of the wiring milestone (after the content port and the
monorepo restructure + `Jonnxor.Api` seam validator)
**Follows:** `frd-admin.md` (the "realistic v1" subset of §3),
`2026-07-05-dotnet-api-v1-design.md`

## 1. Decisions (design review 2026-07-07)

- **Layout:** one solution, one folder per project. `admin/Jonnxor.Admin` joins
  `jonnxor.sln` beside `api/Jonnxor.Api` and `client/`; tests land in
  `tests/Jonnxor.Admin.Tests` (xUnit) beside `Jonnxor.Api.Tests`. No new CI job — the
  existing dotnet job's `dotnet build/test jonnxor.sln` picks both up automatically.
- **Auth posture:** none in v1. Kestrel hard-bound to `127.0.0.1` with a startup guard
  that refuses to boot on any non-loopback bind. Auth (sops-managed credential →
  cookie) becomes a rider the day the panel is exposed past localhost (WireGuard etc.).
  Rationale: v1 shows no secret values, never deploys, never pushes.
- **Pipeline invocation:** hybrid. The admin **project-references `Jonnxor.Api`** and
  calls `Verifier` / `CoverageReporter` / `SnapshotReader` / `EnvFile` /
  `DirectusClient` in-process (the CLI is a 2-line shim over library code — typed
  results, no JSON re-parse). The Node pipeline (`pnpm content:pull`) is a shell-out:
  the panel exercises exactly the command the docs and CI use, never a fork of it.
- **Observability scope:** three health tiles only (Directus ping, snapshot freshness,
  last CI). Everything Loki/Prometheus/Grafana-shaped in FRD §2.3–2.5 stays deferred
  until that stack exists. The one **new secret** in v1: a read-only sops-managed
  Forgejo API token for the CI tile.

## 2. Posture invariants

Same spirit as the API's "read-only by construction":

- **Never writes.** No `git add/commit/push`, no Directus mutations, no snapshot
  edits. The panel's only side effect is invoking the same read/pull commands the
  operator would run by hand; the content-pull page ends at "here is the diff —
  commit it yourself."
- **Loopback only.** The bind guard is a hard startup failure, not a warning, and
  carries its own test.
- **Secrets are names, not values.** Config surfaces show which keys exist and which
  source won; decrypted values never render.

## 3. Target layout

```
jonnxor.is/
├── jonnxor.sln                     # gains Jonnxor.Admin + Jonnxor.Admin.Tests
├── api/Jonnxor.Api/                # unchanged; now also consumed as a library
├── admin/Jonnxor.Admin/            # Blazor Server (.NET 10), interactive server rendering
│   ├── Components/                 # pages + tiles + tables (App/Routes/Layout per template)
│   ├── Services/                   # the seams listed in §5
│   └── appsettings.json            # RepoRoot, DirectusEnvPath, ForgejoBaseUrl (no secrets)
└── tests/Jonnxor.Admin.Tests/      # xUnit + bUnit
```

## 4. Pages (four)

1. **Dashboard** — three health tiles:
   - *Directus*: HTTP ping to `/server/health`, `DIRECTUS_URL` read from
     `directus/.env` via the existing `EnvFile` reader (explicit path, same
     worktree caveat as `verify --live`).
   - *Snapshot freshness*: newest commit touching `client/src/content` (read-only
     git) vs. Directus's newest `date_updated` across collections — renders
     "in sync" or "snapshot N hours behind CMS". Degrades gracefully to
     commit-age-only when the stack is down.
   - *Last CI*: Forgejo REST API (`ForgejoBaseUrl`, token from env) — latest run
     status per branch (main / preview / open PRs).
2. **Content pull** — orchestration surface. Trigger `pnpm content:pull`
   (shell-out, working dir `client/`, stdout/stderr streamed live). Afterwards a
   read-only diff view: `git status --porcelain` + `git diff`, scoped to
   `client/src/content`. Verify pair: offline (in-process, instant) and live
   (in-process, env from `DirectusEnvPath`), findings rendered as a table.
   One global run-at-a-time lock (`SemaphoreSlim`); output kept in a bounded
   in-memory ring buffer. **No commit button exists.**
3. **Coverage** — `CoverageReporter` in-process: the per-locale × per-collection
   table (the `report` verb made visible), with drill-down to the fields still
   falling back to en.
4. **Config** — read-only effective configuration (FRD R-CFG-1): repo root,
   Directus URL, Forgejo endpoint, which source won (appsettings vs env),
   build-time vs runtime labeling. The only editable state in v1 is panel
   preferences (theme, timestamp locale) in a local JSON under the app data dir.

## 5. Services (the testable seams)

- `ProcessRunner` (interface + real impl) — spawns `pnpm` / `git`, streams lines,
  captures exit codes. Everything above it is tested against a fake with recorded
  transcripts.
- `ContentPipelineService` — orchestrates pull → diff; owns the global lock and the
  output buffer.
- `SnapshotHealthService` — wraps `Verifier`/`SnapshotReader` for the freshness tile
  and verify actions.
- `CoverageService` — wraps `CoverageReporter`.
- `DirectusHealthService` — `HttpClient` ping; `EnvFile` for the URL.
- `ForgejoCiService` — `HttpClient` against the Forgejo REST API, token from env;
  read-only scope.

## 6. Configuration

`appsettings.json`: `RepoRoot` (default: walk up from content root to the dir
containing `jonnxor.sln`), `DirectusEnvPath` (default `directus/.env`),
`ForgejoBaseUrl` (default `http://localhost:3000`). The Forgejo token arrives via
environment only (sops/direnv; passed explicitly in worktrees, same convention as
`verify --live --env`). Runs via `dotnet run --project admin/Jonnxor.Admin` from the
repo root on WSL — the same host where mise-shims put `pnpm` on `PATH`, so the
shell-out inherits a working environment.

## 7. Design system

The panel serves `client/public/assets/tokens.css` read-only through a static-file
provider pointed at the client workspace — it inherits dawn/rune/neon and cannot
drift from the site's tokens. Panel CSS stays minimal and token-based (no hardcoded
themed colors, per the design-system rule). No Playwright/visual coverage — this is
an operator tool, not the site.

## 8. Testing

xUnit, same style as the API suite:

- services against the fake `ProcessRunner` (pnpm/git transcripts) and fake
  `HttpMessageHandler`s (Directus health, Forgejo runs API);
- the loopback bind-guard as a hard startup test;
- freshness computation against fixture git/Directus timestamps;
- a few bUnit component tests (tiles, findings table, coverage table) — bUnit is the
  only new test dependency.

## 9. Deferred (explicitly, per FRD §2.3–2.5 + review)

Log viewer (Loki), metrics (Prometheus), alerting, tracing, Grafana links,
scheduling beyond the manual "run now" that content-pull already is, settings
*editing*, auth, Dockerized deploy, and any action that ends in a commit or deploy.

## 10. Definition of done

- `admin/Jonnxor.Admin` + `tests/Jonnxor.Admin.Tests` wired into `jonnxor.sln`; the
  CI dotnet job builds and tests both with zero workflow changes.
- All four pages functional against the live local stack (Directus up, Forgejo up):
  tiles truthful, pull streams and diffs, verify offline+live render findings,
  coverage table matches the `report` verb's output.
- Bind guard proven by test; grep-level check that no write-path (git commit/push,
  Directus mutation) exists in the admin project.
- Full solution green: existing 104 xUnit + new admin tests + 172 Vitest + e2e/visual
  untouched.

## Amendments (execution)

Three deviations from the text above, all review-approved during implementation:

1. **Freshness tile (§4.1):** the Directus schema carries no `date_updated` on the
   content collections, so "commit vs. CMS newest-edit" is uncomputable. The tile
   instead reports newest-commit age touching `client/src/content` + working-tree
   dirty state + whether the read-only git probes themselves succeeded (plan header
   refinement 1).
2. **Component tests (§8):** rendered-markup assertions use the framework's own
   `HtmlRenderer` (Microsoft.AspNetCore.Components.Web) — bUnit was never needed, so
   the suite adds **zero** new test dependencies (plan header refinement 2).
3. **Loopback guard (§2):** the guard covers not just explicit URLs and the `urls`
   configuration key (`--urls`, `ASPNETCORE_URLS`, appsettings) but also
   `Kestrel:Endpoints:*:Url` config binds, which Kestrel honours without touching the
   `urls` key at all (review-found gap, closed in Task 1's fix commit `23f261d`).
