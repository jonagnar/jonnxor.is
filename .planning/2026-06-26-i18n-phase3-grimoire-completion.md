# i18n Phase 3 — Grimoire vertical slice + regression guard — Completion & Phase-4 handoff

**Date:** 2026-06-26
**Branch:** `feat/grimoire-i18n` (off `origin/preview`) — 17 commits, **pushed**; PR **#10** open into `preview` (http://localhost:3000/jonnxor/jonnxor.is/pulls/10).
**Spec:** [`2026-06-26-i18n-phase3-grimoire-and-guard-design.md`](./2026-06-26-i18n-phase3-grimoire-and-guard-design.md) · **Plan:** [`2026-06-26-i18n-phase3-grimoire-and-guard-plan.md`](./2026-06-26-i18n-phase3-grimoire-and-guard-plan.md) · **Authoring note:** [`2026-06-26-authoring-en-base-invariant.md`](./2026-06-26-authoring-en-base-invariant.md)
**Execution:** subagent-driven (fresh implementer per task + spec review + code-quality review each), 12 tasks + a final whole-branch review.

## Outcome — phase goal met
The locale-aware Directus pattern now covers **grimoire** as a second vertical, mirroring Phase-2 blog:
- `grimoire` + `grimoire_translations` schema-as-code (`setup-schema.mjs`, regenerated `snapshot.yaml`); translatable `title/cat/tags(json)/body`, shared base `order/realm/game/updated`.
- `content:import` seeded 11 en docs; `content:pull` writes `src/content/grimoire/<slug>.<locale>.yaml` (deterministic, prunes legacy); `content:restore` rehydrates (idempotent, groups locale files by slug).
- `/docs` localizes the client `JX_DOCS` blob at build time with English fallback, remapping each entry `id` → `slug` so `?doc=` deep-links + `localStorage('jx-doc')` stay locale-stable.
- **Build has zero build-time Directus dependency** — verified building 15 pages with the stack stopped.
- **Regression guard closed:** `localeEntryId` (`src/content/loaders.ts`) shared by both loaders + `config-wiring.test.ts` asserts both loaders keep `generateId: localeEntryId`. Prose-independent — holds while content is en-only.
- Quality gates green: **26 unit tests**, `astro check` 0 errors, build 15 pages.
- Cross-cutting: shared `scripts/lib/directus-client.mjs` (resolves Phase-2 handoff #2); `blog.ts` → generic `localized.ts` (`localizedEntry`); new `scripts/lib/grimoire-yaml.mjs`.

## New gotchas learned this phase (don't relearn)
- **Astro parses content YAML with js-yaml (YAML 1.1).** A bare date scalar (`updated: 2026-06-01`) is coerced to a `Date`, breaking `updated: z.string()` and the build. `serializeDoc` forces `updated` to a **double-quoted** scalar (`Scalar.QUOTE_DOUBLE`); guarded by a round-trip test through js-yaml (the `yaml` pkg used to *write* is 1.2 and won't reproduce the bug). Our own `parseDoc` (yaml 1.2 core) keeps bare dates as strings — so it didn't surface the issue; only the Astro loader did.
- **Directus CLI in the container is `node /directus/cli.js`**, NOT a bare `directus` on PATH. The `directus:snapshot` script uses that, and pipes via `| tee directus/schema/snapshot.yaml > /dev/null` (a plain `> file` truncates the committed snapshot *before* the `&&` chain runs — a failed snapshot would destroy the good file).
- **`content:pull` prunes content not backed by Directus.** Guard any pull/refactor verification with `content:restore` first if the DB might be empty, or it deletes the committed snapshot files.
- Regenerating `snapshot.yaml` via the CLI may reorder existing fields (additive diff with some reordering) — that's fine; blog/languages content stays intact.

## Deferred / Phase-4 handoff (NOT blockers)
1. **Real is/ja prose** for blog AND grimoire — still en-only by design (author's to write/approve). The first real second-locale file activates the build-artifact guard on top of the committed unit guards. Do NOT invent prose; no placeholder content.
2. **Next collections:** `pages` (About/CV — structured + prose, the meatiest model) and `games`. Grimoire was the last "easy" mirror of blog; pages needs block/field modeling per the combined design.
3. **Known minors from the final review (non-blocking, documented):**
   - `docs.astro` has no all-empty guard: if the entire grimoire collection were empty, the client `docs[0].id` would throw (blog throws loudly for its analog). Won't occur with 11 committed en docs.
   - `content-pull.mjs` slices `updated` to 10 chars defensively; grimoire `updated` is a free string (not a Directus timestamp), so an author string >10 chars would be silently truncated. It's a date by convention.
   - `localizedEntry`'s `Entry` type widens `data` with `[k: string]: unknown`, so the `JX_DOCS` render contract is implicit (untyped). Carries `slug`/`locale` as harmless extra keys into the blob.
4. **Production Directus hosting** still deferred — the committed snapshot remains the build contract.

## Branch workflow note
PR #10 opened via the Forgejo API (`~/.forgejo-token`, `POST /api/v1/repos/jonnxor/jonnxor.is/pulls`, base `preview`) — `gh` does not apply. The push reminder suggests `main` as the compare base (Forgejo default branch); the PR correctly targets `preview`.
