> **DRAFT — for validation.** Intent inferred from existing artifacts; confirm before relying on it.

# FRD — jonnxor.is .NET 10 Blazor Server Admin Panel (Admin Layer)

**Status:** Forward-looking DRAFT. This layer is **planned, not built.** Everything below is a reasonable proposal inferred from `~/dev/notes/dev-environment.md`, `~/dev/CLAUDE.md`, and `.planning/2026-06-24-i18n-directus-design.md`. No admin-panel code exists yet; treat each specific as a hypothesis to confirm, not a settled decision.
**Repo:** `code/jonnxor.is`
**Date:** 2026-06-28

---

## 1. Context and why this document is speculative

The stated **4-layer** architecture names the admin panel as the **Admin Layer** — described in the i18n design as **".NET 10 Blazor Server — Admin Layer: Logs, monitoring, health, settings."** The user further specifies its responsibilities as: **configurations, settings, logging, monitoring, observability, scheduling, data processing.**

None of this is built. Two facts constrain every proposal below:

1. **The site is static (SSG) with a committed content snapshot.** Content authoring already happens in **Directus**, not in a bespoke admin panel — so this admin panel is explicitly **not a CMS** and should not duplicate Directus.
2. **The observability stack (Grafana + Prometheus + Loki) is planned but deferred** "until something's deployed," and **Coolify** (deploys) is also not yet stood up. A Blazon admin panel that surfaces logging/monitoring/observability largely **depends on those systems existing first**, or must define its own minimal mechanisms.

So the recurring question across every subsection is: **does the admin panel implement a capability, or does it merely surface a capability that lives in Directus / Grafana / Loki / Prometheus / Coolify?** The proposal leans toward **the admin panel as a thin operator console / aggregator** over the real systems, not a reimplementation.

`[confirm: is the admin panel meant to be a unifying operator dashboard over the existing tools (Directus, Grafana, Loki, Coolify), or a standalone system that owns these concerns itself? This choice changes nearly every requirement below.]`

Cross-cutting proposals (apply to all subsections):
- **Blazor Server** (.NET 10), interactive server rendering; single self-hosted operator app. `[confirm]`
- **Auth:** single-operator (the owner). `[confirm: auth mechanism — Forgejo/OIDC SSO, a single sops-managed credential, or mTLS on the private network? It must be authenticated; it exposes operational data.]`
- **Hosting:** self-hosted (home infra), behind the private network / WireGuard, not public. `[confirm — consistent with the "own the tools" principle and remote-access-via-RDM/WireGuard setup.]`
- **Secrets/config:** sops + age + direnv, same as the rest of the env; never commit plaintext. `[confirm]`
- **Talks to the .NET API** (`frd-api.md`) for app-level data and to the observability stack for telemetry. `[confirm]`

---

## 2. Requirements by responsibility

Each subsection is **one proposal with `[confirm]` markers**, not an assertion.

### 2.1 Configurations `[confirm]`

**Proposal.** Provide a read-and-edit surface for **application/runtime configuration** of the jonnxor.is stack — e.g. the .NET API's settings, feature flags, locale/fallback policy, content-pull parameters, environment toggles (preview vs prod behavior).

- **R-CFG-1** `[confirm]`: View effective configuration of the API and admin app (which config source won, sops vs env vs default), **read-only by default**.
- **R-CFG-2** `[confirm]`: Edit a curated set of **non-secret** runtime settings (feature flags, toggles) with change history. Secrets stay in sops — the panel **never** displays or edits decrypted secret values; at most it shows which keys exist. `[confirm: hard boundary — should the panel touch secrets at all? Proposed: no.]`
- **R-CFG-3** `[confirm]`: Distinguish **build-time** config (baked into the SSG snapshot / Astro build, not changeable at runtime) from **runtime** config (changeable here). Build-time values are display-only.
- **Open:** where do editable settings persist — a small admin-owned store, the API, or a committed config file? `[confirm]`

### 2.2 Settings `[confirm]`

**Proposal.** Settings **of the admin panel itself and operator preferences**, kept distinct from §2.1 application configuration.

- **R-SET-1** `[confirm]`: Operator preferences — theme (the site already ships dawn/rune/neon themes; the panel may reuse them), default landing view, timezone/locale for displayed timestamps (is-IS / en-GB / ja-JP).
- **R-SET-2** `[confirm]`: Connection settings to upstream systems (Directus URL, API base URL, Loki/Prometheus/Grafana endpoints, Coolify endpoint) — **references only; credentials via sops**, not stored in the UI.
- **R-SET-3** `[confirm]`: Notification preferences (where alerts go — see §2.4).
- **Open:** is §2.1/§2.2 a meaningful split for a single-operator tool, or should they be one "Settings" area? `[confirm]`

### 2.3 Logging `[confirm]`

**Proposal.** A **log viewer**, primarily a query/aggregation front-end over **Loki** (the planned logging store), with a minimal fallback if Loki isn't up yet.

- **R-LOG-1** `[confirm]`: Query and tail structured logs from the .NET API and admin app — filter by level, time range, service, locale, request id. Source of truth = **Loki** when it exists. `[confirm: until Loki is deployed, is a simpler source acceptable (e.g. container logs, a local rolling file, or the API streaming its own logs)?]`
- **R-LOG-2** `[confirm]`: Logs are **structured** (Serilog or `Microsoft.Extensions.Logging` with a structured sink) so they're queryable; correlation/request ids flow from the API. `[confirm: Serilog vs built-in logging.]`
- **R-LOG-3** `[confirm]`: The panel **reads** logs; it does not own retention — that's Loki/the backup rules. The env vision explicitly notes **"monitoring ≠ logging"**, so logging and monitoring (§2.4) are kept as separate views.
- **Boundary:** logs are operational data — view requires auth; no public exposure. `[confirm]`

### 2.4 Monitoring `[confirm]`

**Proposal.** A **health/status dashboard** — current up/down and key signals for each component — distinct from logging (per the env's explicit "monitoring ≠ logging").

- **R-MON-1** `[confirm]`: Component health tiles: .NET API (`/healthz`, `/readyz`), Directus, the snapshot pipeline freshness, last deploy, last backup (`forgejo-backup.timer`). `[confirm: which components are in scope — just the jonnxor.is stack, or the wider home infra / Greek-god hosts?]`
- **R-MON-2** `[confirm]`: Surface live metrics from **Prometheus** (request rates, errors, latency, resource use) — the panel queries Prometheus rather than collecting metrics itself. The API exposes `/metrics`. `[confirm.]`
- **R-MON-3** `[confirm]`: **Alerting** on threshold breaches / component-down, routed to the operator (per §2.2 notification prefs). `[confirm: is alerting in scope for v1, or deferred with the rest of observability? Alertmanager may own this instead.]`
- **Relationship to Grafana:** `[confirm — strong open question. Grafana is the planned dashboards/monitoring tool. Does the admin panel reimplement a status view, or just deep-link to Grafana? Proposed: a lightweight at-a-glance status here, deep links to Grafana for detail.]`

### 2.5 Observability `[confirm]`

**Proposal.** Treat observability as the **umbrella** over §2.3 (logs) + §2.4 (metrics/health) plus **tracing**, and make the panel the **single pane of glass** that correlates them — not a replacement for the stack.

- **R-OBS-1** `[confirm]`: Correlate a request across logs/metrics/traces via a shared correlation id emitted by the API. `[confirm: is distributed tracing (OpenTelemetry → a tracing backend) in scope, or are logs+metrics enough for a personal site? Proposed: OpenTelemetry instrumentation from the API so traces are *possible*, even if a tracing UI is deferred.]`
- **R-OBS-2** `[confirm]`: Standardize on **OpenTelemetry** in the API/admin app so logs/metrics/traces share conventions and can target Loki/Prometheus/(a tracing backend) without code churn. `[confirm.]`
- **R-OBS-3** `[confirm]`: Provide drill-down: status tile → relevant metrics → relevant logs, in two clicks.
- **Boundary / honesty:** observability is **deferred until something is deployed**; this subsection is the most speculative. Much of it is contingent on Coolify + the Grafana/Prometheus/Loki stack existing. `[confirm scope and sequencing.]`

### 2.6 Scheduling `[confirm]`

**Proposal.** A view + trigger surface for **scheduled jobs** relevant to jonnxor.is, and a place to define new ones.

- **R-SCH-1** `[confirm]`: List scheduled jobs with last-run / next-run / status — e.g. `content:pull` regeneration, backup verification, link/health checks, any digest jobs. `[confirm: which jobs actually exist or are wanted?]`
- **R-SCH-2** `[confirm]`: **Manual trigger** ("run now") and enable/disable for each job, with an audit trail.
- **R-SCH-3** `[confirm]`: Scheduling mechanism — `[confirm: in-process .NET scheduler (e.g. a hosted `BackgroundService` / Quartz.NET / Hangfire) vs. system-level `systemd` timers like the existing `forgejo-backup.timer`. The env already uses systemd timers; the panel could *surface and trigger* those rather than own a separate scheduler. Proposed: surface systemd timers + allow app-level jobs via a .NET scheduler, clearly labeled by origin.]`
- **R-SCH-4** `[confirm]`: Jobs that mutate content/state (e.g. `content:pull`) must not run against production casually — respect the repo rule about never pushing the deploy branch without asking. `[confirm: should the panel be able to trigger anything that ends in a deploy, or stop at producing a reviewable diff/PR?]`

### 2.7 Data processing `[confirm]`

**Proposal.** A workspace for **operator-initiated data tasks** over the content/snapshot pipeline — distinct from authoring (which is Directus's job).

- **R-DAT-1** `[confirm]`: Run and inspect the **content snapshot pipeline** — pull from Directus, apply English-base fallback (is/ja → en, per the i18n design), produce the locale-keyed snapshot, **show a diff** before anything is committed. `[confirm: does this belong in the admin panel, or stay a CLI `content:pull` as the i18n design assumes? Proposed: the panel *orchestrates and shows results*; the actual transform may live in the API (`frd-api.md` §3.1).]`
- **R-DAT-2** `[confirm]`: Bulk/maintenance operations — translation-coverage report (which is/ja fields are still falling back to en), broken-link scan, content validation, reindex of any search index. `[confirm which are wanted.]`
- **R-DAT-3** `[confirm]`: Import/export helpers — e.g. seed/export the English base, export logs/metrics snapshots. `[confirm.]`
- **R-DAT-4** `[confirm]`: All data-processing runs are **logged, auditable, and non-destructive by default** (produce a reviewable artifact / diff rather than mutating production directly).
- **Boundary:** **not a CMS.** Authoring stays in Directus; this is operational data processing only. `[confirm.]`

---

## 3. Cross-cutting non-functional proposals `[confirm]`

- **Auth & exposure:** authenticated, single-operator, private-network only (WireGuard/RDM remote access already in use). Never public. `[confirm mechanism.]`
- **Read-mostly, safe-by-default:** destructive/mutating actions are gated, audited, and prefer producing a reviewable diff/PR over direct production mutation.
- **Thin over the real systems:** prefer surfacing/aggregating Directus, Loki, Prometheus, Grafana, Coolify, and systemd timers over reimplementing them. `[confirm this is the intended philosophy.]`
- **Secrets:** sops + age + direnv; the panel never shows decrypted secrets.
- **Deploy:** Dockerized, self-hosted, eventually via Coolify; Forgejo Actions CI; `mise`-pinned .NET 10. `[confirm.]`
- **Sequencing:** much of §2.3–§2.5 is **blocked on the deferred observability stack and Coolify.** A realistic v1 may be only §2.1/§2.2 (config/settings) + §2.6 (surface existing timers) + §2.7 (content-pull diff), with logging/monitoring/observability arriving as the stack lands. `[confirm phasing.]`

---

## 4. Open questions to resolve before planning

1. **Aggregator vs. owner** — does the panel surface Directus/Grafana/Loki/Prometheus/Coolify, or own these concerns itself? (Governs the whole FRD.)
2. **Sequencing** — is the panel worth building before the observability stack and Coolify exist? Which subset is the realistic v1?
3. **Scheduling mechanism** — systemd timers (existing) vs. an in-app .NET scheduler vs. both.
4. **Data processing ownership** — content-pull/transform in the API, the panel, or a CLI?
5. **Auth** — OIDC/Forgejo SSO, single sops credential, or mTLS?
6. **Scope of monitoring** — just the jonnxor.is stack, or the wider home infra (the Greek-god hosts)?
7. **Relationship to Grafana** specifically — reimplement a status view or deep-link out?
