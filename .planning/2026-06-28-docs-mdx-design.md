# jonnxor.is project docs (MDX) — Design Spec

- **Date:** 2026-06-28
- **Status:** Design approved → pending implementation plan
- **Scope:** Replace the default-starter README and stand up the project docs as human-readable **MDX** in `code/jonnxor.is/`, structured so they can *optionally* be hosted on `/docs` later.

## 1. Context

The root `README.md` is still the default "Astro Starter Kit: Minimal". `docs/` holds `CHANGELOG.md` +
`RELEASE.md` (plain `.md`). `@astrojs/mdx` is **not** installed, and `/docs` is the **Grimoire** reader
(a YAML content collection) — so site hosting of these docs is a *future* option, not part of this work.

## 2. Decisions

- **`README.md` (root)** — a real **technical project readme** (developer-facing): what jonnxor.is is,
  the 4-layer architecture/stack, dev setup (mise · pnpm · `pnpm dev`), build, the deploy pipeline
  (Forgejo → GitHub mirror → Vercel; `preview`/`main` tracks), repo structure, key links. Replaces the starter.
- **`docs/README.mdx`** — the **docs-site landing / getting-started** page, modeled on
  `https://docs.astro.build/en/getting-started/`: a welcoming intro to the project (user-facing) + a
  map/cards to the docs sections (changelog · release notes · api reference). The "front page" of `/docs` if hosted.
- **`docs/CHANGELOG.mdx`** — convert + flesh out the existing changelog, drawn from the dev-log/commits (Keep-a-Changelog).
- **`docs/release/RELEASE.mdx`** — the release **process** (from the existing `docs/RELEASE.md` checklist).
- **`docs/release/v0.1.0.mdx`** — human **release notes** for the first/current release (Astro 6 rebuild · i18n is/en/ja · live deploy pipeline).
- **`docs/api-reference/README.mdx`** — placeholder: ".NET 10 business API — forthcoming (not yet built)."
- **Format:** `.mdx` (markdown-superset; renders as-is in the repo, Astro-renderable later). **Site `/docs` wiring is deferred.**
- **Not PDF'd** — these are the repo's shipped docs, they live as MDX in the repo (distinct from SDD specs/plans, which do go to `resources/` as PDF).
- The old `docs/CHANGELOG.md` + `docs/RELEASE.md` are **replaced** by the `.mdx` versions (removed).

## 3. Method (subagents)

1. **Subagent A** → `README.md` (root, technical — read `package.json`, `astro.config.mjs`, `src/`, the PRD
   draft, `notes/dev-environment.md`) **and** `docs/README.mdx` (docs landing — fetch the Astro getting-started
   page for style, then write the jonnxor.is equivalent).
2. **Subagent B** → `docs/CHANGELOG.mdx` (from `notes/logs/` + commit history), `docs/release/RELEASE.mdx`
   (from the existing `docs/RELEASE.md`), `docs/release/v0.1.0.mdx` (release notes), `docs/api-reference/README.mdx`
   (placeholder). Then remove `docs/CHANGELOG.md` + `docs/RELEASE.md`.
3. Verify the Astro **build is still green** (docs/ is outside `src/`, so it shouldn't matter); commit on `preview`.

## 4. Verification

- Root `README.md` is real (first line is NOT "Astro Starter Kit").
- `docs/` contains `README.mdx`, `CHANGELOG.mdx`, `release/{RELEASE.mdx,v0.1.0.mdx}`, `api-reference/README.mdx`; no stale `CHANGELOG.md`/`RELEASE.md`.
- `mise exec -- pnpm run build` succeeds.

## 5. Success criteria

- [ ] Real root `README.md`; the 5 MDX docs in place; old `.md` docs removed.
- [ ] Build green; committed + pushed (`preview`).
- [ ] MDX is hostable-ready (valid markdown), site wiring left for a future, opt-in task.
