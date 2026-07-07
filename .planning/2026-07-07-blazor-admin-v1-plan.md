# Blazor Admin Thin v1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development.
> Design: `.planning/2026-07-07-blazor-admin-v1-design.md` (read it first — posture
> invariants, page set, deferrals).

**Goal:** Stand up `admin/Jonnxor.Admin` (Blazor Server, .NET 10) + `tests/Jonnxor.Admin.Tests`
in `jonnxor.sln`: dashboard health tiles (Directus ping, snapshot freshness, last CI),
content-pull orchestration with diff view (never commits), verify offline/live surfaces,
and the translation-coverage report rendered from `Jonnxor.Api` in-process.

**Environment:** as the sub-project-2 plan — WSL via `wsl -e bash -lc` with mise-shims
PATH; system `dotnet` 10.0.1xx; Directus runs from the **infra** repo only
(`cd ~/dev/src/infra/directus && docker compose up -d`), never from a worktree; direnv
does not reach worktrees, so anything needing `directus/.env` takes the path explicitly.
Forgejo runs at `http://localhost:3000` (compose stack `~/dev/src/infra/forgejo`).

**Hard rules:**
- The admin **never writes**: no `git add/commit/push`, no Directus mutation, no snapshot
  edit. Its only side effect is spawning the same read/pull commands the operator runs by
  hand. (Close-out gate greps for write paths.)
- Kestrel binds `127.0.0.1` only; a startup guard hard-fails any non-loopback URL.
- Secrets render as key names, never values.
- The Astro build path gains no .NET dependency; CI workflow is untouched (the existing
  dotnet job builds/tests the solution, which now simply contains two more projects).
- Every task: TDD where there is logic, commit, **push**.

**Design refinements discovered while planning** (report them in task reports; they are
not scope changes):
1. The Directus schema has **no `date_updated` field on any collection** (grep of
   `directus/schema/snapshot.yaml`: zero hits), so the freshness tile cannot compare
   against CMS timestamps. It instead shows: age of the newest commit touching
   `client/src/content` + whether that path is dirty in the working tree ("pull output
   pending commit"). Both signals come from read-only git.
2. Component render tests prefer the framework's built-in
   `Microsoft.AspNetCore.Components.Web.HtmlRenderer` (zero new dependencies, analogous
   to the JS side's Container-API render tests). Fall back to bUnit only if HtmlRenderer
   proves insufficient for a needed assertion — report the choice.

---

## Task 1: Project scaffold + solution wiring + loopback guard

One commit (`feat(admin): Jonnxor.Admin scaffold — Blazor Server, loopback-only, sln-wired`):

- `admin/Jonnxor.Admin/Jonnxor.Admin.csproj`: `Sdk="Microsoft.NET.Sdk.Web"`, `net10.0`,
  `<Nullable>enable</Nullable> <ImplicitUsings>enable</ImplicitUsings>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>` (match `Jonnxor.Api.csproj`), plus
  a `ProjectReference` to `../../api/Jonnxor.Api/Jonnxor.Api.csproj`.
- Minimal Blazor Server app **written by hand, not `dotnet new blazor`** (no hollow
  scaffold, no Bootstrap payload): `Program.cs` (AddRazorComponents +
  AddInteractiveServerComponents, MapRazorComponents<App> with
  AddInteractiveServerRenderMode), `Components/App.razor`, `Components/Routes.razor`,
  `Components/Layout/MainLayout.razor`, `Components/Pages/Dashboard.razor` (placeholder
  `@page "/"` with an h1 — tiles arrive in Task 5). `appsettings.json` with
  `"Urls": "http://127.0.0.1:5170"` and an empty `"Admin"` section (filled in Task 2).
- **Loopback guard (TDD):** `Services/LoopbackGuard.cs` — static
  `EnsureLoopback(IEnumerable<string> urls)` that throws `InvalidOperationException`
  naming the offending URL unless every URL's host parses as a loopback address
  (`IPAddress.IsLoopback` / `localhost`). Wildcard binds (`0.0.0.0`, `*`, `+`, `[::]`)
  are the failure cases to assert. Called from `Program.cs` against the resolved
  `app.Urls`/configured URLs **before** `app.Run()`. Tests first
  (`tests/Jonnxor.Admin.Tests/LoopbackGuardTests.cs`): accepts `http://127.0.0.1:5170`
  and `http://localhost:5170`; rejects `http://0.0.0.0:5170`, `http://+:80`,
  `http://192.168.1.10:5170`.
- `tests/Jonnxor.Admin.Tests/Jonnxor.Admin.Tests.csproj`: `dotnet new xunit` shape copied
  from `Jonnxor.Api.Tests.csproj` conventions, project-references the admin project.
- `dotnet sln jonnxor.sln add admin/Jonnxor.Admin tests/Jonnxor.Admin.Tests` (they nest
  under new/existing solution folders to match the api/tests pattern already in the sln).
- Gates: `dotnet build jonnxor.sln -c Release` 0 warnings; `dotnet test jonnxor.sln`
  green (104 api + new); `wsl -e bash -lc` run of
  `ASPNETCORE_ENVIRONMENT=Development dotnet run --project admin/Jonnxor.Admin` serves
  the placeholder on `http://127.0.0.1:5170` (curl 200) and **refuses to boot** with
  `--urls http://0.0.0.0:5171` (assert non-zero exit, clear message).

## Task 2: Options, repo-root resolution, ProcessRunner (the foundations)

One commit (`feat(admin): AdminOptions + RepoPaths + ProcessRunner seams`):

- `Services/AdminOptions.cs` — bound from the `"Admin"` config section:
  `RepoRoot` (string?, default null → resolve), `DirectusEnvPath` (default
  `directus/.env`, resolved relative to RepoRoot), `ForgejoBaseUrl` (default
  `http://localhost:3000`), `ForgejoRepo` (default `WAAAGH/jonnxor.is`),
  `ContentDir` (default `client/src/content`, relative to RepoRoot).
- `Services/RepoPaths.cs` — resolves RepoRoot: explicit option wins; otherwise walk up
  from `AppContext.BaseDirectory` to the first directory containing `jonnxor.sln`;
  throw with a clear message if not found. Exposes absolute `ContentDir`,
  `ClientDir` (`client/`), `DirectusEnvPath`. TDD against temp directory trees.
- `Services/IProcessRunner.cs` + `Services/ProcessRunner.cs` — the one seam every
  shell-out goes through:
  `RunAsync(string fileName, string[] args, string workingDir, Action<string> onLine, CancellationToken ct)`
  → `Task<int>` (exit code); merges stdout+stderr line-by-line into `onLine` (prefix
  stderr lines `"! "`), never throws on non-zero exit (exit code is data), throws
  `InvalidOperationException` on spawn failure (missing binary) with the fileName in the
  message. Real impl on `System.Diagnostics.Process` with async stream readers.
- `tests/Jonnxor.Admin.Tests/FakeProcessRunner.cs` — scripted double: queue of
  `(expectedFileName, expectedArgs, linesToEmit, exitCode)`; asserts invocation order
  and args; used by every service test from here on. Real `ProcessRunner` gets one
  integration-ish test spawning `git --version` (git exists everywhere the suite runs,
  incl. the CI sdk image).
- DI wiring in `Program.cs` (options bind, singletons for RepoPaths/ProcessRunner).
- Gates: `dotnet test` green; push.

## Task 3: Health services — Directus ping, snapshot freshness, Forgejo CI (TDD)

One commit (`feat(admin): health services — Directus ping, snapshot freshness, last CI`):

- `Services/DirectusHealthService.cs`: reads `DIRECTUS_URL` via `Jonnxor.Api.EnvFile.Load`
  on `RepoPaths.DirectusEnvPath` (env var wins over file, same precedence as
  `CliRunner`); GET `{url}/server/health` with a 5s-timeout `HttpClient`;
  result record `DirectusHealth(bool Reachable, string? Status, string? Detail, string Url)`
  — `Status` from the response JSON's `status` property when parseable. Missing env file
  / missing key → `Reachable=false, Detail` explains (worktree direnv caveat verbatim).
  Tests: fake `HttpMessageHandler` (healthy JSON, 503, timeout via canceled token,
  missing env file).
- `Services/SnapshotHealthService.cs`:
  - `GetFreshnessAsync()` → `SnapshotFreshness(DateTimeOffset? LastCommit, bool Dirty, int FileCount)`
    via `IProcessRunner` + git (read-only): `git log -1 --format=%cI -- client/src/content`
    and `git status --porcelain -- client/src/content` (Dirty = any output;
    FileCount = line count), workingDir = RepoRoot.
  - `RunOfflineVerifyAsync()` → typed `IReadOnlyList<Finding>` + counts, via
    `SnapshotReader.ReadAll(contentDir)` + `Verifier.Run` in-process (wrap in
    `Task.Run` — it is file I/O). `DirectoryNotFoundException` → surfaced as a
    single synthetic finding, not a crash.
  - Tests: freshness against `FakeProcessRunner` transcripts (fresh+clean,
    stale+dirty, git failure → LastCommit null); offline verify against a small
    fixture content dir (reuse the pattern of `tests/Jonnxor.Api.Tests/fixtures/` —
    one valid, one missing-en).
- `Services/ForgejoCiService.cs`: token from env `FORGEJO_TOKEN` **only** (never
  config/appsettings); GET
  `{ForgejoBaseUrl}/api/v1/repos/{ForgejoRepo}/actions/tasks?limit=30` with header
  `Authorization: token {token}`; parse `workflow_runs[]` (fields: `name`,
  `head_branch`, `display_title`, `status`, `created_at`, `run_number`) into
  `CiRun` records; expose `GetLatestPerBranchAsync()` → newest run per `head_branch`
  ordered main, preview, rest. No token → degraded result ("no FORGEJO_TOKEN configured"),
  not an exception. Tests: fake handler with a recorded JSON body (copy real shape from
  the running Forgejo 13), 401, empty, no-token cases.
- **Operator step (document in the task report + Task 8 README):** create the token in
  Forgejo (user settings → applications, **read:repository scope only**) and add
  `FORGEJO_TOKEN` to the repo's sops-managed env (`.envrc` path) — never plaintext.
- Gates: `dotnet test` green; push.

## Task 4: ContentPipelineService — pull orchestration, diff, verify (TDD)

One commit (`feat(admin): content-pull orchestration — run, stream, diff, verify; no write path`):

- `Services/ContentPipelineService.cs` (singleton):
  - **Global lock:** `SemaphoreSlim(1,1)`; a second trigger while running returns a
    "already running" state, never queues. State record `PipelineRun(string Kind,
    DateTimeOffset StartedAt, bool Running, int? ExitCode)` + bounded output buffer
    (`ConcurrentQueue<string>`, cap 2000 lines, drop-oldest) + an event/callback so the
    UI can live-refresh.
  - `RunPullAsync()`: `pnpm content:pull` via `IProcessRunner`, workingDir =
    `RepoPaths.ClientDir`, streaming into the buffer.
  - `GetDiffAsync()`: read-only `git status --porcelain -- client/src/content` +
    `git diff -- client/src/content` (workingDir RepoRoot) → record
    `SnapshotDiff(IReadOnlyList<string> StatusLines, string DiffText)`. **There is no
    commit method. Do not add one.**
  - `RunVerifyOfflineAsync()`: delegates to `SnapshotHealthService` (typed findings).
  - `RunVerifyLiveAsync()`: calls `Jonnxor.Api.CliRunner.Run(
    ["verify","--live","--content",<abs content>,"--env",<abs directus/.env>], out, err)`
    with `StringWriter`s captured into the buffer — exact CLI parity, zero duplication
    of the live-equivalence logic; exit code is the result. Runs under the same global
    lock (it hits Directus).
  - Tests (all via `FakeProcessRunner`; live-verify test only asserts arg/lock plumbing
    around a stubbed runner — CI never needs Directus): happy pull (transcript → exit 0,
    buffer holds lines), failing pull (exit 1 propagated), concurrent trigger rejected
    while first still streaming, diff parsing, buffer cap enforced.
- Gates: `dotnet test` green; push.

## Task 5: UI — layout, tokens, Dashboard tiles

One commit (`feat(admin): dashboard — theme tokens + three health tiles`):

- Static assets: `Program.cs` adds a read-only `StaticFileOptions` with a
  `PhysicalFileProvider` rooted at `{RepoRoot}/client/public/assets`, request path
  `/site-assets` → the layout links `/site-assets/tokens.css`. `data-theme="rune"` on
  `<html>` (default theme; the theme switcher is a Config-page preference, Task 7).
  `admin/Jonnxor.Admin/wwwroot/admin.css`: minimal, token-based (`var(--*)` only — the
  design-system rule: zero hardcoded themed colors).
- `Components/Tiles/HealthTile.razor`: shared presentation (title, status enum
  ok/warn/fail/unknown mapped to token colors, detail lines, refreshed-at).
- `Components/Pages/Dashboard.razor` (`@page "/"`, `@rendermode InteractiveServer`):
  three tiles loading async on init from the Task-3 services (each tile isolated —
  one failing service must not blank the page), manual refresh button per tile.
  Freshness tile wording: `in sync` (clean) / `N uncommitted change(s) pending` (dirty),
  plus `last snapshot commit: <relative age>`.
- Render tests (`tests/Jonnxor.Admin.Tests/RenderTests.cs`) via `HtmlRenderer` with the
  services swapped for fakes registered in a test `ServiceCollection`: Dashboard renders
  three tiles; a degraded ForgejoCiService (no token) renders the degraded message, not
  an exception; HealthTile maps each status to its css class.
- Gates: `dotnet test` green; manual: app up on 127.0.0.1:5170, tiles truthful against
  the live stack (Directus up from infra, Forgejo up) — screenshot in report; push.

## Task 6: UI — Content pull page + Coverage page

One commit (`feat(admin): content-pull surface + coverage report page`):

- `Components/Pages/ContentPull.razor` (`@page "/content"`): buttons Pull / Verify
  offline / Verify live, disabled while the global lock is held; live-streaming output
  pane (monospace, auto-scroll, bounded by the service's buffer); after any run
  completes, the diff panel refreshes from `GetDiffAsync()` — status lines + raw diff in
  a `<pre>` (read-only; page copy says explicitly: *review and commit from your own
  shell — this panel never commits*). Offline-verify findings render as a table
  (Rule | File | Detail from the typed `Finding`s); live verify renders its captured
  CLI output verbatim.
- `Services/CoverageService.cs`: `Build()` → `CoverageReport` via
  `SnapshotReader.ReadAll` + `CoverageReporter.Build(entries, DateTimeOffset.UtcNow)`
  in-process; plus `MissingBySlug(collection, locale)` → the slugs lacking that locale
  file (drill-down data the report record doesn't carry). Unit tests over a mixed-locale
  fixture dir.
- `Components/Pages/Coverage.razor` (`@page "/coverage"`): the per-collection ×
  is/en/ja table (percentages + present/total, same numbers as the `report` verb),
  cell click → drill-down list of missing slugs for that (collection, locale).
- Render tests: ContentPull in idle state shows three buttons + no-commit copy;
  Coverage table against a fake `CoverageService` shows a 0%-ja cell and its drill-down
  list renders slug names.
- Nav links for both pages in `MainLayout`.
- Gates: `dotnet test` green; manual against live stack: real pull streams and the diff
  pane shows the result (or clean), offline verify OK, live verify OK with
  `directus/.env`, coverage matches `dotnet run --project api/Jonnxor.Api -- report`
  output numbers; push.

## Task 7: UI — Config page + panel preferences

One commit (`feat(admin): config surface — effective config, sources, preferences`):

- `Services/PanelPreferencesService.cs`: `Theme` (dawn/rune/neon) + `TimestampLocale`
  (is-IS/en-GB/ja-JP) persisted as JSON at
  `Environment.SpecialFolder.ApplicationData`/`jonnxor-admin/preferences.json`
  (create-if-missing, corrupt file → defaults + warning, atomic write via temp+rename).
  TDD: round-trip, corrupt-file, first-run.
- `Components/Pages/Config.razor` (`@page "/config"`), read-only sections:
  - *Paths*: RepoRoot (+ how it was resolved: explicit vs walked-up), ContentDir,
    DirectusEnvPath (exists? yes/no).
  - *Endpoints*: Directus URL (value + **source: env var vs env file**), ForgejoBaseUrl
    + ForgejoRepo (source: appsettings default vs override).
  - *Secrets*: key names only with set/not-set badges — `FORGEJO_TOKEN`,
    `ADMIN_EMAIL`, `ADMIN_PASSWORD` (from the env file: name + set/not-set; **values
    never render** — assert this in a render test).
  - *Build-time vs runtime* labeling per design §4.4: everything above is runtime;
    the note names the snapshot/Astro build as the build-time domain the panel cannot
    touch.
  - *Preferences* (the only editable thing): theme selector (applies `data-theme` on
    `<html>` live) + timestamp locale (used by tiles' relative/absolute times).
- Render tests: secrets section renders badges and never the value string (seed a
  recognizable fake value, assert absent from HTML); theme preference round-trips.
- Gates: `dotnet test` green; push.

## Task 8: Close-out — docs, write-path gate, full sweep

One commit (`docs(admin): close-out — README/CLAUDE.md, write-path audit, DoD sweep`):

- **Write-path gate:** grep `admin/` for forbidden verbs — `git (add|commit|push)`,
  `PostAsJsonAsync|PutAsync|PatchAsync|DeleteAsync` outside `CliRunner`-delegated live
  verify, any Directus mutation path. Expected: zero hits (`DirectusClient` has no write
  members by construction; the admin must not either). Record the grep in the report.
- Loopback re-check: `--urls http://0.0.0.0:X` refusal re-verified.
- CLAUDE.md: new "Admin panel" subsection under the API-worker section — run command
  (`dotnet run --project admin/Jonnxor.Admin` from repo root, WSL), loopback/no-auth
  posture, `FORGEJO_TOKEN` operator setup, the four pages, never-writes rule.
  README: layout map + commands table gain the admin. `frd-admin.md`: mark the v1
  subset shipped (one-line status note at top), leave §2.3–2.5 as deferred.
- Full sweep: `dotnet build jonnxor.sln -c Release` (0 warnings) → `dotnet test`
  (104 api + all admin tests) → `verify --offline` on the real snapshot → from
  `client/`: `pnpm test` (172) → `pnpm test:e2e` (25) → `pnpm test:visual` (23,
  baselines untouched — the admin must not have touched the site). Live smoke: stack up
  from infra, all four pages exercised, tiles truthful, pull→diff→verify live all green.
- DoD audit vs design §10, report; push.

---

## Plan self-review notes

- Every shell-out flows through `IProcessRunner`, so all pipeline/git behavior tests run
  without pnpm/git/Directus — CI (the sdk:10.0 container) needs none of them beyond the
  one `git --version` spawn test.
- Live verify reuses `CliRunner.Run` with captured writers instead of re-plumbing
  Directus login + equivalence — the CLI stays the single owner of that flow.
- Freshness-tile deviation from design §4 (no `date_updated` in the schema) is
  documented above; the design doc gets a one-line amendment in Task 8.
- No task adds a NuGet package except (possibly) bUnit under refinement 2's fallback.
