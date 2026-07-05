# Design — Monorepo restructure + .NET API v1 (seam validator)

**Date:** 2026-07-05
**Status:** Approved (design review 2026-07-05)
**Scope:** sub-project 2 of the wiring milestone (after the content port, before the Blazor admin)
**Follows:** `frd-api.md` (role resolved 2026-07-04: build-time worker, no hosting), `2026-07-04-content-port-design.md`

## 1. Decisions

- **Role (already decided):** the .NET layer is a build-time worker. It is NEVER on the
  Astro build path — CI/Vercel keep building from the committed snapshot alone.
- **v1 scope (decided this review):** validator-first. The battle-tested Node
  pull/restore stays the snapshot producer; .NET earns its place with NEW capability —
  seam verification + translation-coverage reporting (the exact surfaces the Blazor
  admin will render in sub-project 3). Porting pull/restore to .NET is API phase 2,
  only after `DirectusClient` is proven read-only.
- **Placement (decided this review):** monorepo restructure. The entire Astro workspace
  moves to `client/`; a top-level solution wraps the .NET projects; `blazor/` joins in
  sub-project 3 (no hollow scaffold now). `directus/` stays top-level — it is the
  content layer, not part of the client.

## 2. Target layout

```
jonnxor.is/
├── jonnxor.is.sln            # top-level solution
├── global.json               # pins .NET SDK 10.0.1xx (system dotnet in WSL; no mise plugin)
├── client/                   # the entire current Astro workspace (git mv, history kept)
├── api/Jonnxor.Api/          # console app, verb-based (verify, report)
├── tests/Jonnxor.Api.Tests/  # xUnit (client/tests/ keeps the JS pyramid)
├── directus/                 # unchanged, top-level
├── .planning/ .forgejo/ CLAUDE.md README.md   # repo-level, stay put
```

## 3. Phase A — the move

One history-preserving `git mv` commit + a seams-fix commit:
- `client/scripts/*.mjs` env path becomes `../directus/.env` (npm scripts updated);
  `scripts/pw.sh` container mounts; CI jobs gain `working-directory: client`;
  CLAUDE.md/docs paths updated.
- **Gate: a pure move repaints nothing.** Full pyramid green from `client/`
  (172 unit / 25 e2e / 23 visual with baselines byte-untouched) + a live
  restore/pull round-trip (all-skip / clean status).
- **Merge-day step (operator, dashboard-only):** flip the Vercel project's
  **Root Directory to `client`** when this merges to `preview` — the deploy breaks
  without it. Same for production/`main` whenever it next advances (the construction
  redirect keeps prod masked regardless).
- Sequencing: the pending phase-2 PR should merge into `preview` first so the
  restructure reviews as a move-only PR.

## 4. Phase B — solution + validator

- **`Jonnxor.Api`** (net10.0 console; System.CommandLine verbs), FRD decomposition
  scoped read-only:
  - `DirectusClient` — HttpClient against `DIRECTUS_URL`, admin login or static token
    from the `directus/.env` conventions; read-only endpoints only.
  - `SnapshotReader` — walks `client/src/content/**`; YamlDotNet for `.yaml`,
    frontmatter extraction for blog `.md`.
  - `Verifier` — invariants; `CoverageReporter` — per-locale fallback coverage.
- **Commands:**
  - `verify --offline` (CI-safe, no Directus): every slug has an `en` locale file;
    zero comment nodes in any snapshot document (the ` #` truncation class);
    countdowns kind coherence; registered pages slugs have valid sections presence.
  - `verify --live` (local, like content:pull): per collection, slug sets match
    Directus and field values agree — a GENERIC field-walk comparison, deliberately
    NOT a re-encoding of the Node descriptor kind-maps, so `scripts/lib/collections.mjs`
    remains the single owner of that knowledge.
  - `report` — translation coverage per locale/collection; console table + JSON
    artifact (`--json <path>`) for the future admin.
- **Tests:** xUnit over fixture snapshots — valid set + broken fixtures reproducing the
  real bug classes phase 1/2 caught live (missing en base, comment-node truncation,
  kind violation). No live Directus in unit tests.
- **CI:** new `dotnet` job (`mcr.microsoft.com/dotnet/sdk:10.0` container):
  `dotnet build` + `dotnet test` + `verify --offline` against the real committed
  snapshot on every push — seam invariants gate CI from day one.
- **Posture:** stateless; no API-owned datastore; secrets via the existing sops/direnv
  env conventions; no public exposure of anything.

## 5. Non-goals

No Node pull/restore replacement; no request-time endpoints or hosting; no Blazor
project; no snapshot format changes; no `ops/`-level infra changes.

## 6. Definition of done

- Repo in the target layout; full JS pyramid green from `client/`; visual baselines
  byte-untouched by the move.
- `dotnet build && dotnet test` green; `verify --offline` passes on the committed
  snapshot and FAILS on each broken fixture (proven by tests); `verify --live` passes
  against the local stack; `report` emits a correct coverage table (en 100%, is/ja
  reflecting reality).
- CI runs the dotnet job on push; CLAUDE.md documents the new layout + commands.
- Vercel root-directory flip documented as the merge-day operator step.
