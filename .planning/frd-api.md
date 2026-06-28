> **DRAFT — for validation.** Intent inferred from existing artifacts; confirm before relying on it.

# FRD — jonnxor.is .NET 10 Blazor Business API (Logic Layer)

**Status:** Forward-looking DRAFT. This layer is **planned, not built.** Everything below is a reasonable proposal inferred from `~/dev/notes/dev-environment.md`, `~/dev/CLAUDE.md`, and `.planning/2026-06-24-i18n-directus-design.md`. Nothing here has been implemented; treat each specific as a hypothesis to confirm, not a decision that has been made.
**Repo:** `code/jonnxor.is`
**Date:** 2026-06-28

---

## 1. Context and why this document is speculative

The stated governing architecture for jonnxor.is is **4 layers**:

- **Directus** — Content Layer (blog, grimoire, games, page prose; native is/en/ja translations).
- **Astro** — Presentation Layer (chrome, components, routing, language switcher; fully static / SSG).
- **.NET 10 API** — **Business Logic Layer** (this document).
- **.NET 10 Blazor Server** — Admin Layer (separate FRD: `frd-admin.md`).

As of the current build the site is **Astro 6.x, fully static (SSG)**, deploying Forgejo CI → Vercel. Directus is local-only (Docker) and feeds a **committed, locale-keyed content snapshot** that is the SSG build contract — Forgejo CI / Vercel never query Directus at build time. **No .NET API code exists yet.** This FRD therefore proposes *where the API would fit*, *what it might own*, and *what it must not break* — primarily the existing "build needs no live backend" guarantee.

`[confirm: is the .NET API still wanted at all, given the current SSG + committed-snapshot pipeline already delivers content without a runtime backend? The API's value proposition depends on whether any genuinely dynamic, request-time behavior is planned.]`

---

## 2. Proposed role between Directus and Astro

The central tension: the current architecture is **static-first** and explicitly avoids a runtime hosting dependency. A business-logic API only earns its place where behavior must happen **at request time** (something a committed snapshot cannot serve) or **on a schedule / event** (something the build step shouldn't own).

Proposed framing of the API's role:

1. **Not on the SSG critical path.** `[confirm]` The committed snapshot stays the build contract; the API is *not* queried during `astro build`. Astro pages render statically as today. This preserves "Forgejo CI / Vercel never depend on a live backend."
2. **Runtime/dynamic surface only.** `[confirm]` The API serves the handful of things that are inherently dynamic — form submissions, interactive game endpoints, search beyond static indexes, anything personalized or rate-limited.
3. **Optional content-transformation worker.** `[confirm]` The API *may* sit beside (not in front of) the `content:pull` pipeline as the thing that talks to Directus, applies business rules / fallback logic, and emits the snapshot — moving that logic out of an ad-hoc Node script into a typed .NET service. This is an alternative to the Node `@directus/sdk` puller described in the i18n design.

`[confirm: which of these three roles is actually intended? They imply very different deployment and availability requirements. Role 3 (build/CI-time worker) needs no public hosting; roles 1–2 (request-time) require an always-on hosted service, which contradicts the current "no hosting dependency" stance and would need Coolify first.]`

### Proposed boundary rules

- The API **reads from Directus**, never the other way around. `[confirm]`
- Astro (or the browser) calls the API; the API calls Directus. Astro never calls Directus directly. `[confirm]`
- The API is **stateless** where possible; durable state (if any) lives in Directus or a dedicated store, not in API memory. `[confirm: is any API-owned datastore wanted, or is Directus the single source of truth?]`

---

## 3. Proposed responsibilities

### 3.1 Content delivery / transformation `[confirm]`
- Talk to local/hosted Directus via the Directus REST/GraphQL API or `@directus/sdk` equivalent.
- Apply the **English-base fallback rule** (is/ja → en) at the data layer, matching the rule the i18n design already specifies for lookup-time fallback. Centralizing fallback here avoids duplicating it across Astro and the snapshot puller. `[confirm]`
- Shape Directus's normalized `*_translations` model into the locale-keyed snapshot shapes the frontend expects (blog `.md`, grimoire `.yaml`, pages structured data). `[confirm: does this transformation belong in the API or stay in the Node content-pull script?]`

### 3.2 Business rules `[confirm]`
- Any logic that is "more than content": draft/publish gating, date-based visibility (scheduled posts), category/tag derivation, read-time computation, computed fields. The i18n design already lists `read_time` and `category_label` as translation fields — `[confirm: are these authored in Directus, or computed by business logic? If computed, they belong here.]`
- Validation rules for inbound data (contact form, game submissions). `[confirm]`

### 3.3 Interactive / dynamic endpoints `[confirm]`
- The site has **games**, **countdowns**, **wallpapers**, a **grimoire**, and a **blog** (per the i18n design's page list). Some of these may want runtime behavior (e.g. a game leaderboard, a dynamic countdown source, a server-validated form). `[confirm: which pages, if any, need request-time logic vs. staying fully static?]`

---

## 4. Candidate endpoints / services (inferred — all speculative)

All of the following are **proposals derived from the site's existing page inventory**, not requirements. Each needs confirmation that the behavior is wanted at runtime at all.

| Candidate | Shape | Inferred from | Confidence |
|---|---|---|---|
| `GET /api/content/{collection}/{locale}` | Returns fallback-resolved items | Directus collections blog/grimoire/pages/games | `[confirm]` low |
| `POST /api/content/pull` (internal) | Triggers snapshot regeneration | `content:pull` pipeline | `[confirm]` low |
| `POST /api/contact` | Validates + forwards a contact/CV message | About/CV pages mention a cover letter | `[confirm]` low |
| `GET /api/search?q=&locale=` | Search across blog/grimoire | static index exists; runtime search may not be needed | `[confirm]` low |
| `GET /api/games/{slug}/...` | Game state / score endpoints | `games.astro` | `[confirm]` low |
| `GET /api/countdowns` | Live countdown data | `countdowns` page | `[confirm]` low |
| `GET /healthz`, `GET /readyz` | Liveness/readiness | observability stack planned | `[confirm]` medium |
| `GET /metrics` (Prometheus) | Metrics scrape | Grafana+Prometheus+Loki planned | `[confirm]` medium |

**Proposed service decomposition (internal):** `DirectusClient` (data access), `FallbackResolver` (is/ja → en), `SnapshotMapper` (Directus → snapshot shapes), `ContentService` (orchestration), plus per-feature services (`ContactService`, `GamesService`, …) only where a runtime feature is confirmed. `[confirm]`

`[confirm: the entire endpoint table is a guess from page names. Before any of this is planned, the real question is which features need a server. A static site with a committed snapshot may need none of these.]`

---

## 5. Auth and secrets

Per the environment conventions: **sops + age + direnv, no secret server.** One age key (`~/.config/sops/age/keys.txt`, held in Bitwarden) decrypts every repo's `.env.sops`; direnv auto-loads on `cd`. Never commit plaintext secrets.

Proposed application to the API:

- **Config & secrets:** API reads its config from a sops-encrypted `.env.sops` in the repo (Directus base URL, Directus static token / service account, any signing keys), decrypted via direnv at dev time. `[confirm: does .NET's configuration pipeline read direnv-exported env vars cleanly, or is a `dotenv`-style loader / `IConfiguration` env provider needed? .NET 10's `IConfiguration` does read environment variables, so direnv-exported values should flow in — confirm in practice.]`
- **Directus auth:** API authenticates to Directus with a **dedicated service account / static token**, scoped read-only to the content collections, never the admin token. `[confirm]`
- **Inbound auth (request-time endpoints):** `[confirm: is any of the public API authenticated? A personal site likely wants none for public reads, but mutating endpoints (contact, game scores) need at least rate-limiting / anti-abuse — confirm whether that's API keys, a turnstile/captcha, or just rate limits.]`
- **Internal endpoints** (`/content/pull`, metrics): not publicly exposed; reachable only from CI / the admin panel / the metrics scraper on the private network. `[confirm]`
- **Production secret delivery:** under Coolify (planned), secrets would be injected as environment variables; the sops→env step happens in CI/deploy, not by shipping the age key. `[confirm]`

---

## 6. Deployment thoughts

The environment plans **Coolify** ("git push → build → preview URLs → auto-HTTPS, like Vercel") but it is **not yet stood up**, and the frontend currently deploys to **Vercel** via Forgejo CI. This creates a real open question for a .NET service.

Proposed options (pick after confirming the API's role):

- **Option A — Build/CI-time worker only (role 3).** The API runs only in CI / locally as the snapshot producer. **No public hosting needed**, no availability burden, fully compatible with today's "no live backend" guarantee. Lowest cost, narrowest value. `[confirm — likely the safest first increment.]`
- **Option B — Self-hosted long-running service on Coolify.** Dockerized .NET 10 app, deployed by Coolify with auto-HTTPS, on the home infrastructure. Needed for any request-time endpoint. Requires Coolify to exist first and accepts a real availability/maintenance burden. `[confirm]`
- **Option C — Hosted alongside Vercel.** A separate serverless/edge .NET deployment. `[confirm: probably not — .NET on Vercel is awkward and splits the deploy story; noted only for completeness.]`

Cross-cutting deployment proposals:
- **Containerized** via the repo's Docker conventions (`ops/docker/<svc>/` for env services; a project-local service for this one, mirroring how Directus is project-local rather than in `ops/`). `[confirm placement: project-local vs `ops/`.]`
- **Tool versions** pinned via `mise`; **.NET 10** SDK pinned per repo. `[confirm]`
- **CI** through Forgejo Actions (build, test, container publish). `[confirm]`
- **Observability hooks** (`/healthz`, `/metrics`, structured logs to Loki) wired from day one so the planned Grafana/Prometheus/Loki stack and the Blazor admin panel can consume them. `[confirm]`

---

## 7. Non-goals (proposed)

- Do **not** put the API on the SSG build critical path or reintroduce a live-backend dependency for Vercel builds. `[confirm — this seems core to the architecture.]`
- Do **not** duplicate Directus's job; the API adds *logic*, not a second CMS.
- Do **not** hold durable content state outside Directus unless a feature demonstrably requires it. `[confirm]`
- No live public Directus exposure; the API is the only thing that talks to Directus. `[confirm]`

---

## 8. Open questions to resolve before planning

1. **Does the API need to exist for v1?** The static + committed-snapshot pipeline may cover all current pages with zero runtime backend. `[confirm]`
2. **Which role** (§2) is intended — build-time worker, request-time service, or both?
3. **Which pages, if any, need request-time behavior** (forms, games, countdowns, search)?
4. **Where does fallback + snapshot transformation live** — Node `content:pull` script (as the i18n design assumes) or this API?
5. **Hosting:** is Coolify a prerequisite, or does the API stay CI/local-only until it is?
6. **Is any API-owned datastore wanted**, or is Directus the single source of truth?
7. **Auth posture** for any mutating endpoints (rate limit / captcha / API key / none)?
