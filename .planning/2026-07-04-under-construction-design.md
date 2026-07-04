# Under-construction production + preview workflow — Design Spec

- **Date:** 2026-07-04
- **Status:** Design approved → pending implementation plan
- **Scope:** Put a branded under-construction page on production (jonnxor.is, built from
  `main`) while all site development moves to the `preview` branch
  (preview.jonnxor.is). Vercel already publishes both branches; no hosting changes.

## 1. Context

The site is being rebuilt toward the 4-layer model (Astro + Directus now; .NET API/admin
planned). Production should stop showing work-in-progress: `main` becomes a frozen "live"
branch fronted by a construction page; `preview` becomes the daily working branch (it
already gets the full e2e + visual CI treatment). Current state: `main` is 2 commits ahead
of `preview` (CLAUDE.md PR #20); `dist/` is gitignored; deploys flow
Forgejo → GitHub mirror → Vercel.

## 2. Decisions

- **Mechanism: traffic control, not code removal.** `main` keeps the full site codebase
  and builds normally. Two additive files, existing only on `main`:
  - `public/construction/index.html` — the page.
  - `vercel.json` — a 307 redirect of every path (except `/construction/`, `/assets/*`,
    favicon/manifest files) to `/construction/`, plus `X-Robots-Tag: noindex` on the
    construction route.
  Rejected: swapping `src/pages` on main (delete-vs-modify conflicts at go-live merge);
  Vercel dashboard protection (not in git, Vercel-branded wall, not "a page on main").
- **307, not 308/301** — the redirect is genuinely temporary; nothing may cache it as
  permanent.
- **The page** is a self-contained branded teaser in the rune theme: reuses the deployed
  `/assets/fonts.css` + `/assets/tokens.css` (they ship in the same build), hardcoded
  `data-theme="rune"`, no i18n routing (bilingual is/en line instead), no theme toggle.
  Content: JONNXOR wordmark (Orbitron), accent rule, "Eitthvað er í smíðum hér." /
  "Something is being forged here.", links to github.com/jonagnar and
  jonnxor@jonnxor.is. Type scale: ≥16px everywhere, body 19px.
- **Branches:** fast-forward `preview` to current `main` (clean FF, no force), push;
  construction commit lands on `main` and pushes. Day-to-day work happens on `preview`
  from then on; `main` is frozen until launch.

## 3. Go-live procedure (for when construction ends)

One commit on `main`: merge `preview` into `main`, delete `vercel.json` and
`public/construction/`, push. No conflicts are possible — neither file exists on
`preview`. Production becomes the full site again.

## 4. Error handling / edge cases

- Redirect must not catch the assets the construction page itself needs (`/assets/*`,
  favicons, webmanifest) — covered by the exclusion pattern; verified live post-deploy.
- Vercel redirects fire before filesystem routing, so the built site pages are
  unreachable on production even though they exist in the deployment.
- A stray future push to `main` before launch would deploy but remain invisible behind
  the redirect — harmless, but the convention stays "work on preview only".

## 5. Verification

- Local: `pnpm build` green; `dist/construction/index.html` present; page renders
  correctly when `dist` is served locally.
- Live (production URL): `curl -I` any path → 307 → `/construction/`;
  `/construction/` → 200; `X-Robots-Tag: noindex` present on the construction response.
- Live (preview URL): still serves the full site, no redirect.
- Existing Vitest suite untouched (deploy-config change, deliberately outside the test
  pyramid; verified live instead).

## 6. Success criteria

- [ ] jonnxor.is (production) shows the branded construction page on every path.
- [ ] preview.jonnxor.is serves the full site from `preview`, which contains everything
      `main` had (including CLAUDE.md PR #20).
- [ ] Construction page passes the type-scale rule (≥16px) and uses the site's real
      tokens/fonts.
- [ ] Production responses carry noindex on the construction route.
- [ ] Go-live procedure documented (this spec, §3).
