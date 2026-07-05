# Monorepo Restructure + .NET API v1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development. Design: `.planning/2026-07-05-dotnet-api-v1-design.md` (read it first — layout, gates, non-goals).

**Goal:** Move the Astro workspace to `client/`, stand up the top-level .NET solution (`api/Jonnxor.Api` + `tests/Jonnxor.Api.Tests`), and ship the seam validator (`verify --offline`, `verify --live`, `report`) gated into CI.

**Environment:** as the phase-1/2 plans (WSL via `wsl -e bash -lc` with mise shims; Playwright container via `client/scripts/pw.sh` after the move; Directus from the MAIN checkout only, never worktree; system `dotnet` 10.0.109 at /usr/bin/dotnet — mise shims PATH does not affect it).

**Hard rules:** the move repaints nothing (visual baselines byte-untouched); the Astro build never gains a .NET dependency; the Node descriptor table stays the only encoding of per-collection kind knowledge; every task pushes its commits.

---

## Task 1: Phase A — move the Astro workspace to `client/`

Two commits on the branch.

**Commit 1 — pure move (`refactor: move the Astro workspace into client/ (monorepo layout)`):**
- `git mv` into `client/`: `src public tests scripts design docs package.json pnpm-lock.yaml pnpm-workspace.yaml astro.config.mjs vitest.config.ts playwright.config.ts tsconfig.json` plus any other Astro-workspace files found at root (inspect `git ls-files` — NOT: `.planning .forgejo directus CLAUDE.md README.md mise.toml .gitignore .sops.yaml .envrc` which stay at root; check each remaining root file and decide with rationale in the report). `node_modules`, `dist`, `test-results` are untracked — delete at root, reinstall inside client.
- NOTHING else changes in this commit — reviewers must see pure renames (`git show -M --stat` ~100% renames).

**Commit 2 — seam fixes (`fix: repoint seams at the client/ workspace`):**
- `client/package.json` scripts: `--env-file=directus/.env` → `--env-file=../directus/.env` (all content:*/directus:* scripts); check `directus:up/down/snapshot` compose -f paths (→ `../directus/docker-compose.yml`).
- `client/scripts/pw.sh`: inspect — the container mount/workdir must resolve the client workspace; adjust relative paths.
- `client/scripts/lib/collections.mjs` descriptor `dir` values are workspace-relative (`src/content/<name>`) and the scripts run from `client/` — verify unchanged semantics (scripts cwd = client). Same for `content-pull/restore` dir handling.
- `.forgejo/workflows/ci.yml`: both jobs gain `defaults: { run: { working-directory: client } }`; artifact path → `client/playwright-report/`.
- Root `mise.toml` stays (node pin applies repo-wide); `client/tests/**` path-based tests (config-wiring reads `../../src/content.config.ts` — relative within client, unaffected; snapshot-comments ROOT resolution — verify).
- CLAUDE.md: commands section gains `cd client` context (one line: "JS workspace lives in client/ — run pnpm commands there"); path references updated (src/ → client/src/ where load-bearing).
- **Gates (all from `client/`):** `pnpm install` → `pnpm test` (172) → `pnpm exec astro check` (16-error baseline) → `pnpm build` (15 pages) → `pnpm test:e2e` (25) → `pnpm test:visual` (23, `git status` clean — baselines byte-untouched) → live round-trip (`pnpm content:restore` all-skip 68 items, `pnpm content:pull` clean status).
- Push both commits.

## Task 2: Solution + projects + CI dotnet job

One commit (`feat(api): .NET solution scaffold — Jonnxor.Api + xUnit, CI wired`):
- Root `global.json`: `{ "sdk": { "version": "10.0.109", "rollForward": "latestFeature" } }`.
- `dotnet new sln -n jonnxor`; `api/Jonnxor.Api` (`dotnet new console -n Jonnxor.Api -o api/Jonnxor.Api`, `<Nullable>enable</Nullable> <ImplicitUsings>enable</ImplicitUsings> <TreatWarningsAsErrors>true</TreatWarningsAsErrors>`); `tests/Jonnxor.Api.Tests` (`dotnet new xunit`), project reference to the Api; both added to the sln.
- Program: minimal verb dispatch (plain args switch is FINE — do not fight System.CommandLine if it adds friction; report the choice): `verify [--offline|--live] [--content <dir>]`, `report [--json <path>]` — stubs returning exit 2 "not implemented" for now, plus `--help`. One smoke unit test (dispatcher routes verbs).
- Root `.gitignore`: add `bin/ obj/` patterns (scoped `api/**/bin/` etc. or global).
- `.forgejo/workflows/ci.yml`: new job `dotnet` (parallel to build-test): container `mcr.microsoft.com/dotnet/sdk:10.0`, steps checkout → `dotnet build jonnxor.sln --configuration Release` → `dotnet test jonnxor.sln --configuration Release --no-build`. (verify --offline joins in Task 3.) NO working-directory default (runs at root).
- Gates: `dotnet build` + `dotnet test` green locally; `pnpm test` from client untouched (172).

## Task 3: SnapshotReader + offline invariants (TDD)

One commit (`feat(api): verify --offline — snapshot invariants with fixture-proven failures`):
- `tests/Jonnxor.Api.Tests/fixtures/`: `valid/` (copy 2–3 real entries per collection type incl. a blog .md) and broken fixtures — `missing-en/` (slug with only .is file), `comment-node/` (unquoted ` # ` mid-scalar), `kind-violation/` (countdown carrying rate). Fixtures are self-contained mini content dirs.
- TDD: tests first asserting `Verifier` passes `valid/` and fails each broken fixture with a message naming file + rule; then implement:
  - `SnapshotReader`: enumerate `<content>/<collection>/<slug>.<locale>.{yaml,md}`; YamlDotNet with comment-preserving parse for the comment check (or a raw scan pairing YamlDotNet's parser events); blog frontmatter = text between leading `---` fences.
  - Rules: en-file-per-slug (every collection); zero YAML comment tokens in any document/frontmatter; countdowns kind coherence (countdown ⇒ no icon/start/rate; countup ⇒ requires them, no when/gold); pages entries whose slug ∈ {countdowns, home, about, cv} carry non-empty sections.
- Wire `verify --offline --content client/src/content` for real; run it against the real snapshot → exit 0, human-readable summary (N files, N slugs, rules passed).
- CI: append `dotnet run --project api/Jonnxor.Api --configuration Release -- verify --offline --content client/src/content` to the dotnet job.
- Gates: `dotnet test` green (incl. fixture failures asserted); live offline verify exit 0.

## Task 4: DirectusClient + verify --live

One commit (`feat(api): verify --live — snapshot↔Directus equivalence`):
- `DirectusClient`: base URL + credentials from env (`DIRECTUS_URL`, `ADMIN_EMAIL`/`ADMIN_PASSWORD` — same names the Node scripts use; load via env, document `--env-file`-style usage: run through `node --env-file`? No — .NET: read `../directus/.env` path passed as `--env <path>` flag parsing KEY=VALUE lines, or rely on exported env; implement the tiny .env file reader, it's 15 lines, test it). Login → token → GET `/items/<collection>?limit=-1&fields=*,translations.*`. Read-only; no create/update/delete methods AT ALL (enforce by not writing them).
- Equivalence check per collection (blog, grimoire, games, pages, countdowns, wallpapers, projects): slug sets equal both ways; per slug+locale, GENERIC field comparison — snapshot record fields vs Directus base+translation fields (normalize: null≈absent, numbers by value, arrays/objects structurally; date strings sliced like the Node toRecord does for blog/grimoire `date`/`updated`). Differences reported field-precise.
- Unit tests: comparison logic against in-memory fakes (mismatch cases: missing slug, extra slug, field drift, translation drift). Live Directus NOT required by tests.
- Manual gate: `verify --live --content client/src/content --env ../directus/.env` (from repo root in the worktree; Directus running from main checkout) → exit 0 against the real stack. Then mutate nothing — this is read-only.

## Task 5: report — translation coverage

One commit (`feat(api): report — per-locale translation coverage table + JSON artifact`):
- From the snapshot alone (offline): per collection × locale, count slugs with a real locale file vs falling back to en; overall totals. Console table + `--json <path>` artifact (schema: `{ generatedAt, collections: [{ name, slugs, locales: { is: { present, coverage } ... } }] }` — stamp generatedAt from a `DateTimeOffset.UtcNow` at emit).
- Unit tests over fixtures (mixed-locale fixture added).
- Manual gate: run on the real snapshot; expect en 100%, is/ja matching reality (blog/grimoire en-only, etc.).

## Task 6: Close-out

- Full sweep: client pyramid (172/25/23 from client/) + `dotnet build/test` + offline verify + live verify + report.
- CLAUDE.md: new "API worker" section (commands, CI job, read-only posture, layout map); README touch if it references paths.
- DoD audit vs design §6; report. Reminder in the report: **Vercel Root Directory → client** is the operator's merge-day step; phase-2 PR should merge before this branch's PR.

---

## Plan self-review notes

- The move task deliberately splits pure-rename from seam-fixes so review can trust rename detection.
- Kind knowledge duplication is confined to the countdowns coherence RULE (a validator invariant, mirrored from zod, acceptable) — the field-level kind maps are NOT re-encoded; equivalence is generic by design.
- `verify --live` and unit tests are decoupled (fakes) so CI never needs Directus.
