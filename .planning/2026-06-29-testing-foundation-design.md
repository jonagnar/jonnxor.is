# Testing Foundation — Design (cycle 0)

**Project:** jonnxor.is
**Date:** 2026-06-29
**Status:** Design — approved decisions captured; pending user review of this spec before planning.
**Cycle:** 0 (precedes Presentation componentization). Each of the four system layers — Presentation, Data, Business API, Admin — is its own later spec→plan→build cycle; this foundation lands first so the componentization refactor has a regression net.

---

## 1. Why this exists

The site has Vitest unit tests for **content-layer and i18n logic only** (`tests/content/*`, `tests/i18n/*` — serialization, id-derivation, fallback, YAML round-trip). **Nothing renders a page or component**, and there is **no E2E or visual coverage at all** — even though the PRD makes *"faithful port from `design/`, no visual regression"* a **hard requirement** and reference mockups already exist in `design/`.

Consequently the next planned work — componentizing the monolithic inline `.astro` pages — is currently **unguarded**. This cycle builds the full test pyramid first so that refactor (and every later cycle) is safe.

## 2. Goals

- Guard componentization: a component extracted from inline markup must still render the same output.
- Enforce "no visual regression" automatically via **self-baseline**; verify fidelity to the `design/` mockups at the component/chrome level + human review (full-page content diffs against mockups are explicitly avoided — see §6).
- Cover all JS-driven interactive behavior with E2E.
- Make all of the above **deterministic** across the WSL dev box and Forgejo Linux CI.
- Establish the standing convention that future .NET layers ship their own test projects.

## 3. Non-goals

- The componentization itself (cycle 1) and any content migration (cycle 2).
- Any .NET code (cycles 3–4) — only a documented convention here.
- Replacing the existing logic unit tests — they stay as-is.
- Testing third-party/library internals.

## 4. Topology — two runners, four pillars + one convention

| Pillar | Runner | Guards | Status |
|---|---|---|---|
| Unit | Vitest | content/i18n **logic** | exists — unchanged |
| Rendering | Vitest + Astro Container API | component/page **output** (structure, props, slots, locale) | new |
| Visual regression | Playwright | **pixel** stability (self-baseline) + design fidelity vs mockups at component level | new |
| E2E behavior | Playwright | **interaction** (JS-driven pages) | new |
| .NET tests | xUnit (later) | API/admin logic | convention now; code in cycle 3+ |

## 5. Pillar — Rendering tests (Vitest + Container API)

- Vehicle: `experimental_AstroContainer.create()` → `renderToString(Component, { props, slots, request, params })` from `astro/container` (stable since Astro 4.9; repo is on 6.4.7). Verified against current Astro docs.
- Render each extracted component/page to an HTML string and assert key content/attributes are present.
- Use the `request` option to assert **locale** rendering (is/en/ja) and `params` for `[slug]` routes.
- **Division of labor:** rendering tests catch structure/content/props/slots; they do **not** assert CSS/layout — that is the visual pillar's job.
- Location: `tests/render/`.
- Initial coverage targets the components the cycle-1 refactor will create (Nav, Footer already exist; Hero, ProjectCard, etc. as they are extracted) plus a smoke render of every page route.

## 6. Pillar — Visual regression (Playwright) — Option A: self-baseline, mockups as design reference

**Decision (revised 2026-06-29):** the automated full-page gate baselines against the **current Astro site itself**, not the `design/` mockups. The mockups carry **placeholder** content while live pages render **real** content (already true for `/blog`, `/blog/<slug>`, `/docs`; universal after cycle 2's content migration), so a full-page pixel diff against mockups would fail on *content*, not design — and would degrade further as content becomes real. Self-baselining is content-proof: the same site's content appears in both the baseline and the comparison. (In dev the inline pages still happen to carry the mockup copy, so they align closely today — but we deliberately do **not** depend on that, since it dissolves as content migrates.)

**Mechanism — the automated gate**
1. Capture baseline PNGs from the **current, pre-refactor Astro site** (already the faithful port) via Playwright `toHaveScreenshot()`, committed under `tests/visual/`.
2. After componentization, and on every later change, Astro routes are re-screenshotted and diffed against those baselines; the refactor must produce **pixel-identical** output.
3. Baselines are generated and compared **only inside the pinned `mcr.microsoft.com/playwright` container** (identical viewport + DPR + fonts) so dev (WSL) and Forgejo CI match byte-for-byte.

**Mockups as a design reference (not an automated content gate)**
- The `design/` folder stays the canonical *design* source: chrome (nav/footer/dock), design-system components (buttons, cards, color tokens, spacing, glow/scanlines), typography.
- Fidelity to the mockups is checked at the **component/chrome level** (content-independent) and via periodic **human review** — never a full-page content pixel diff.

**Tolerance / masking** (applies to the self-baseline gate)
- Small `maxDiffPixelRatio` to absorb sub-pixel anti-aliasing.
- Per-test **masks** for regions that legitimately move between runs: the 404 "souls lost" counter, countdowns live tickers, locale dates, any time-based content.

**Operating rule:** an *intended* visual change is landed by deliberately regenerating baselines (`--update-snapshots`) with the diff reviewed in the PR — so an unreviewed pixel change can never slip through, and an intended one is a one-command, visible update.

**Routes under the gate** (one baseline per row; `*` = representative pick; design-reference column = the mockup that page was ported from)

| Astro route (gated) | Design reference |
|---|---|
| `/` | `design/index.html` |
| `/about` | `design/about.html` |
| `/cv` | `design/cv.html` |
| `/portfolio` | `design/portfolio.html` |
| `/blog` | `design/blog.html` |
| `/blog/<slug>` * | `design/blog-post.html` |
| `/docs` | `design/docs.html` |
| `/games` | `design/games.html` |
| `/countdowns` | `design/countdowns.html` |
| `/wallpapers` | `design/wallpapers.html` |
| `/404` | `design/404.html` |

**Matrix** (≈20–25 baselines, not the full 11×3×3=99):
- All routes above at default theme (`rune`) + default locale (`is`).
- All three themes (`dawn`/`rune`/`neon`) for a representative subset of routed pages: home (`/`) + a content page (`/blog`) + a chrome/component-heavy page (`/games`). (The `design/design-system/` card gallery is mockup-only — no Astro route — so it stays a human reference, not an automated diff target.)
- Locale spot-checks: home in `en` and `ja`.
- Mobile viewport for nav + dock on home and one content page.

- Location: `tests/visual/`.

## 7. Pillar — E2E behavior (Playwright)

Full coverage of the JS-dependent surfaces (run against `astro preview` of a production build):

- **Theme switching** — orb cycle through `dawn`/`rune`/`neon`, persistence via `localStorage('jx-theme')`, and **no pre-paint flash** (theme applied before first paint).
- **Language switching** — switcher navigates to the locale-equivalent URL, persists `jx-lang`, `<html lang>` reflects locale.
- **Search modal** — `/` trigger, lazy-fetch of `/assets/docs-data.json`, result navigation.
- **Grimoire reader** (`/docs`) — realm/game/text filters, per-doc TOC scroll-spy, prev/next, `?doc=<slug>` deep-link, persisted position (`jx-doc`), locale-stable across language switch.
- **Games tracker** (`/games`) — tab switching + `localStorage('jx-games-tab')`.
- **Countdowns** (`/countdowns`) — tickers update over time.
- **Blog** (`/blog`) — category filter chips, locale-formatted dates, prev/next pager on a post.
- **CV** (`/cv`) — print emulation (`@media print`) hides chrome, single-column light layout.
- **404** — renders custom page.
- **Nav** — active-state across HUD/realm/mega/dock; mobile dock + "more" sheet at mobile viewport.

- Location: `tests/e2e/`.

## 8. Pillar — .NET test-project convention

No code now. Documented standing rule: the cycle-3 Business API and cycle-4 Blazor admin **each ship their own xUnit test project from day one**, wired into their CI. Recorded here and propagated to README/CLAUDE during cycle 3.

## 9. Layout, scripts, CI, tooling

**Directory layout**
```
tests/
  content/   existing unit (unchanged)
  i18n/      existing unit (unchanged)
  render/    new — Vitest + Container API
  e2e/       new — Playwright behavior specs
  visual/    new — Playwright visual specs + committed baselines
playwright.config.ts   new
```

**package.json scripts**
- `test` → `vitest run` (unit + render) — fast, runs on every push.
- `test:e2e` → Playwright behavior specs.
- `test:visual` → Playwright visual specs; supports a baseline-(re)generation mode (`--update-snapshots`) that recaptures self-baselines from the current site.

**CI** (`.forgejo/workflows/ci.yml`)
- Every push: existing build + `pnpm test` (unit + render).
- PRs and the `preview` branch: a Playwright job (E2E + visual) in the pinned `mcr.microsoft.com/playwright` container — heavier, gated before anything reaches prod.

**Tooling**
- Playwright pinned via `mise` + lockfile; pnpm stays at **10.34.4** (already pinned; avoid 11.x per env rule).
- Visual baselines generated/compared only inside the pinned container for determinism.

## 10. Risks / open considerations

- **Self-baseline guards against *change*, not original fidelity.** It cannot prove the current port faithfully matches the design — that is covered by the component-level + human review (§6), and the current site is already the faithful port per the FRD. Accepted tradeoff (it's why we keep the mockups as a reference).
- **Intended visual changes require a deliberate baseline regen** (`--update-snapshots`) with the diff reviewed in the PR — by design, not a defect.
- **Font/render determinism** depends on the pinned container; baselines are authoritative only from the container, never from an ad-hoc local run.
- **Container API is experimental** (stable in practice since 4.9, but flagged experimental upstream) — low risk; fallback is asserting built HTML output.

## 11. Success criteria

- `pnpm test` runs unit + render green locally and in CI.
- Playwright E2E covers every interactive surface in §7 and passes against a production preview.
- Self-baselines exist for the §6 matrix; a componentization PR that changes rendered output fails the visual gate, while an *intended* change passes only after a reviewed `--update-snapshots`.
- CI gates wired: unit+render every push; Playwright E2E+visual on PRs + `preview`.
- The .NET test-project convention is documented.
- A subsequent componentization PR that changes rendered output fails the appropriate test (the net demonstrably works).
