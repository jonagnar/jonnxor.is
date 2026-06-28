# jonnxor.is docs-as-MDX Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (subagent fan-out for Task 1). Steps use checkbox (`- [ ]`) syntax.

**Goal:** Replace the default-starter root README with a real one, and stand up `docs/` as MDX (landing · changelog · release notes · api-ref placeholder).

**Architecture:** Two subagents generate the files from the existing artifacts; then remove the old `.md` docs, verify the Astro build, and commit on `preview`.

**Tech Stack:** subagents · Astro · git · MDX/Markdown.

---

## Task 1: Generate the docs (2 subagents, dispatched together)

- [ ] **Subagent A → root `README.md` + `docs/README.mdx`.**
  READ: `\\wsl.localhost\Ubuntu\home\jonnxor\dev\code\jonnxor.is\package.json`, `…\astro.config.mjs`,
  `…\src\pages` (page list), `…\.planning\prd-jonnxor.is.md` (the PRD draft),
  `\\wsl.localhost\Ubuntu\home\jonnxor\dev\notes\dev-environment.md`.
  - WRITE `\\wsl.localhost\Ubuntu\home\jonnxor\dev\code\jonnxor.is\README.md` — a real **technical project
    readme** (overwrite the Astro starter): one-line tagline, what jonnxor.is is, the 4-layer architecture
    (Astro · Directus · .NET API · Blazor admin), **Tech stack**, **Develop** (`mise install`, `pnpm install`,
    `pnpm dev` → :4321), **Build** (`pnpm build`), **Deploy** (`git push` → Forgejo → GitHub mirror → Vercel;
    `preview`→preview.jonnxor.is, `main`→jonnxor.is), **Repo structure**, links to `docs/`.
  - WRITE `\\wsl.localhost\Ubuntu\home\jonnxor\dev\code\jonnxor.is\docs\README.mdx` — the **docs landing /
    getting-started** page. First WebFetch `https://docs.astro.build/en/getting-started/` to model the style,
    then write the jonnxor.is equivalent: a welcoming intro to the project (user-facing) + a short "what's here"
    map linking the CHANGELOG, release notes, and (forthcoming) API reference. Markdown/MDX, LF.
  Do NOT run git. Return a one-line summary.

- [ ] **Subagent B → the rest of `docs/`.**
  READ: `\\wsl.localhost\Ubuntu\home\jonnxor\dev\code\jonnxor.is\docs\CHANGELOG.md` (existing),
  `…\docs\RELEASE.md` (existing checklist), the dev-log `\\wsl.localhost\Ubuntu\home\jonnxor\dev\notes\logs\`
  (read a few day-entries for what shipped).
  WRITE (Markdown/MDX, LF):
  - `…\code\jonnxor.is\docs\CHANGELOG.mdx` — Keep-a-Changelog, expanded from the existing changelog + the
    dev-log (the Astro 6 rebuild, i18n is/en/ja, the deploy pipeline, server-rendered chrome, self-hosted fonts).
  - `…\code\jonnxor.is\docs\release\RELEASE.mdx` — the release **process** (rework the existing RELEASE.md checklist).
  - `…\code\jonnxor.is\docs\release\v0.1.0.mdx` — human **release notes** for the first release (what a visitor/dev
    would care about: the rebuilt site, themes, i18n, live on Vercel).
  - `…\code\jonnxor.is\docs\api-reference\README.mdx` — a short placeholder: the .NET 10 business API is planned,
    not built; this is where its reference will live.
  Do NOT run git, do NOT delete the old files. Return a one-line list of files written.

- [ ] **Verify outputs:** `wsl.exe -e bash -lc "head -1 /home/jonnxor/dev/code/jonnxor.is/README.md; find /home/jonnxor/dev/code/jonnxor.is/docs -type f | sort"`
  Expected: root README first line is the real tagline (not "Astro Starter Kit"); `docs/` has the 5 new `.mdx`
  (plus the old `CHANGELOG.md`/`RELEASE.md` still there, removed next task).

---

## Task 2: Remove old docs, verify build, commit

- [ ] **Step 1: Remove the superseded plain-`.md` docs**

```bash
wsl.exe -e bash -lc "cd /home/jonnxor/dev/code/jonnxor.is && git rm -q docs/CHANGELOG.md docs/RELEASE.md && ls docs"
```
Expected: `docs/` now has `CHANGELOG.mdx`, `README.mdx`, `api-reference/`, `release/` — no `.md`.

- [ ] **Step 2: Verify the Astro build is still green** (docs/ is outside `src/`)

```bash
wsl.exe -e bash -lc "cd /home/jonnxor/dev/code/jonnxor.is && mise exec -- pnpm run build 2>&1 | tail -4"
```
Expected: build completes.

- [ ] **Step 3: Commit + push (`preview`)**

```bash
wsl.exe -e bash -lc "cd /home/jonnxor/dev/code/jonnxor.is && git add README.md docs && git status -s && git commit -q -m 'docs: real README + docs/ as MDX (landing, changelog, release notes, api-ref placeholder)' -m 'Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>' && git push origin preview 2>&1 | tail -1"
```

---

## Rollback
- `git revert` the commit, or `git checkout <prev> -- README.md docs`. The old `.md` docs are recoverable from history.
