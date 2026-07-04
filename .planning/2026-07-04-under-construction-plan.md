# Under-Construction Production Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Production (jonnxor.is, `main`) shows a branded under-construction page; all development moves to `preview` (preview.jonnxor.is).

**Architecture:** Traffic control, not code removal — `main` keeps the full site and gains two additive files: a self-contained construction page in `public/` and a `vercel.json` that 307-redirects every non-asset path to it. `preview` is fast-forwarded to main first.

**Tech Stack:** Static HTML using the site's deployed design system (`/assets/tokens.css`, `/assets/fonts.css`), Vercel redirects/headers, git.

**Spec:** `.planning/2026-07-04-under-construction-design.md`

**Environment notes:** repo at `~/dev/src/jonnxor.is` (WSL checkout; currently on `main` with the spec commit `1d369ec` unpushed). File writes via UNC paths (`\\wsl.localhost\ubuntu\home\jonnxor\dev\src\jonnxor.is\...`); shell/git via WSL (`/usr/bin/git`), using the base64 wrapper pattern from Windows:

```powershell
$script = @'
<bash>
'@
$b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($script))
wsl.exe bash -c "echo $b64 | base64 -d | bash"
```

---

## File structure

| File | Action | Responsibility |
| --- | --- | --- |
| `public/construction/index.html` | create (main only) | the branded teaser page, fully self-contained |
| `vercel.json` | create (main only) | redirect + noindex traffic control |
| `CLAUDE.md` | modify (Deploy section) | record construction mode + go-live pointer |
| branch `preview` | fast-forward to `main` | the working branch, full site |

### Task 1: Fast-forward `preview` to `main` and push it

**Files:** none (branch pointer only)

- [ ] **Step 1: FF-update the local preview branch without checking it out**

```bash
cd ~/dev/src/jonnxor.is
/usr/bin/git fetch . main:preview        # non-force refspec = fast-forward only; fails loudly if not FF
/usr/bin/git log --oneline -1 preview    # expect: 1d369ec docs(planning): under-construction ... design
```

Expected: preview now points at `1d369ec` (same as main). If the fetch refuses (non-FF), STOP — the branch state differs from the spec's assumption; escalate.

- [ ] **Step 2: Push preview**

```bash
cd ~/dev/src/jonnxor.is
/usr/bin/git push origin preview:preview
```

Expected: `<old>..1d369ec preview -> preview`. This triggers a preview.jonnxor.is deploy of the full site (already its current content — harmless).

### Task 2: The construction page

**Files:**
- Create: `public/construction/index.html`

- [ ] **Step 1: Write the page with exactly this content**

```html
<!doctype html>
<html lang="is" data-theme="rune">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>jonnxor.is — í smíðum</title>
    <meta name="robots" content="noindex" />
    <link rel="icon" href="/favicon.svg" type="image/svg+xml" />
    <link rel="icon" href="/favicon.ico" sizes="32x32" />
    <link rel="apple-touch-icon" href="/apple-touch-icon.png" />
    <link rel="stylesheet" href="/assets/fonts.css" />
    <link rel="stylesheet" href="/assets/tokens.css" />
    <style>
      /* Standalone page: fixed rune theme, no site.css/site.js, no nav — by design. */
      * { box-sizing: border-box; }
      body {
        margin: 0;
        min-height: 100svh;
        display: grid;
        place-items: center;
        background: var(--bg);
        color: var(--ink);
        font-family: var(--font-body);
        font-size: 19px;
        text-align: center;
      }
      main { padding: 2rem; }
      h1 {
        font-family: var(--font-display);
        font-size: clamp(2.2rem, 6vw, 3.4rem);
        font-weight: 700;
        letter-spacing: 0.14em;
        margin: 0;
        text-shadow: 0 0 var(--glow-r) var(--glow-tq);
      }
      .rule {
        width: 64px;
        height: 2px;
        background: var(--tq);
        margin: 1.4rem auto;
      }
      .is { margin: 0 0 0.4rem; }
      .en { margin: 0 0 2.2rem; color: var(--ink-soft); font-size: 17px; }
      .links { color: var(--ink-faint); font-size: 16px; }
      .links a {
        color: var(--tq);
        text-decoration: none;
        border-bottom: 1px solid var(--tq-soft);
      }
      .links a:hover { border-bottom-color: var(--tq); }
      .links .sep { margin: 0 0.7em; }
    </style>
  </head>
  <body>
    <main>
      <h1>JONNXOR</h1>
      <div class="rule" role="presentation"></div>
      <p class="is">Eitthvað er í smíðum hér.</p>
      <p class="en" lang="en">Something is being forged here.</p>
      <p class="links">
        <a href="https://github.com/jonagnar" rel="me">github.com/jonagnar</a>
        <span class="sep">·</span>
        <a href="mailto:jonnxor@jonnxor.is">jonnxor@jonnxor.is</a>
      </p>
    </main>
  </body>
</html>
```

Type-scale check (site rule + owner preference): body 19px, en-line 17px, links 16px — nothing under 16px. Colors/fonts are all `--*` tokens (repo rule: never hardcode themed colors); theme fixed to `rune` via `data-theme` on `<html>`.

- [ ] **Step 2: No commit yet** — committed together with `vercel.json` and the CLAUDE.md note in Task 4 (one commit = one production deploy).

### Task 3: vercel.json traffic control

**Files:**
- Create: `vercel.json` (repo root)

- [ ] **Step 1: Write exactly this content**

```json
{
  "redirects": [
    {
      "source": "/((?!construction|assets/|favicon|apple-touch-icon|icon-|site\\.webmanifest).*)",
      "destination": "/construction/",
      "statusCode": 307
    }
  ],
  "headers": [
    {
      "source": "/construction(.*)",
      "headers": [{ "key": "X-Robots-Tag", "value": "noindex" }]
    }
  ]
}
```

How it works: Vercel applies `redirects` before filesystem routing, so every built site page is unreachable; the negative lookahead exempts the construction page itself and the assets it needs (`/assets/*` stylesheets+fonts, favicons, webmanifest). `307` = temporary, never cached as permanent. The header adds crawler protection on top of the page's own `noindex` meta.

- [ ] **Step 2: Validate the JSON parses**

```bash
cd ~/dev/src/jonnxor.is
python3 -m json.tool vercel.json >/dev/null && echo "JSON OK"
```

Expected: `JSON OK`

### Task 4: Local verification + the single main commit

**Files:**
- Modify: `CLAUDE.md` (Deploy & secrets section)

- [ ] **Step 1: Build**

```bash
cd ~/dev/src/jonnxor.is
pnpm build 2>&1 | tail -5
ls dist/construction/index.html dist/assets/tokens.css dist/assets/fonts.css dist/favicon.svg
```

Expected: build succeeds; all four paths listed (public/ is copied verbatim into dist/).

- [ ] **Step 2: Serve dist and probe the page**

```bash
cd ~/dev/src/jonnxor.is/dist
python3 -m http.server 8899 >/dev/null 2>&1 &
SRV=$!
sleep 1
curl -s -o /dev/null -w "construction: %{http_code}\n" http://localhost:8899/construction/
curl -s http://localhost:8899/construction/ | grep -c "Eitthvað er í smíðum"
kill $SRV
```

Expected: `construction: 200` and grep count `1`. (The redirect itself is Vercel-level and can only be verified live — Task 5.)

- [ ] **Step 3: Record construction mode in CLAUDE.md**

In `CLAUDE.md`, section `### Deploy & secrets`, append this sentence to the paragraph ending "...never push main casually**.":

```
Production is currently in **construction mode**: `vercel.json` on `main` 307-redirects
everything to `/construction/` while the site is rebuilt on `preview`; go-live = merge
`preview` → `main` and delete `vercel.json` + `public/construction/` in that commit
(spec: `.planning/2026-07-04-under-construction-design.md` §3).
```

- [ ] **Step 4: Commit + push main (carries the spec commit too)**

```bash
cd ~/dev/src/jonnxor.is
/usr/bin/git add public/construction/index.html vercel.json CLAUDE.md
/usr/bin/git commit -m "feat: production under-construction mode (redirect all -> /construction/)

Branded rune-theme teaser at /construction/; vercel.json 307-redirects
every non-asset path to it and marks the route noindex. Full site still
builds on main and is restored at go-live by deleting these two files
after merging preview (spec .planning/2026-07-04-under-construction-design.md).

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
/usr/bin/git push origin main
```

Expected: push lands the pending planning commits (`1d369ec` spec + the plan) plus this one on origin/main; Vercel starts a production deploy.

### Task 5: Live verification

- [ ] **Step 1: Wait for the production deploy, then probe (retry up to ~5 min)**

```bash
for i in $(seq 1 10); do
  code=$(curl -s -o /dev/null -w "%{http_code}" https://jonnxor.is/)
  [ "$code" = "307" ] && break
  echo "attempt $i: $code (deploy not live yet)"; sleep 30
done
curl -sI https://jonnxor.is/ | grep -iE "^(HTTP|location)"
curl -sI https://jonnxor.is/en/ | grep -iE "^(HTTP|location)"
curl -sI https://jonnxor.is/construction/ | grep -iE "^(HTTP|x-robots-tag)"
curl -s https://jonnxor.is/construction/ | grep -c "Eitthvað er í smíðum"
```

Expected: `/` and `/en/` → `HTTP/2 307` + `location: /construction/`; `/construction/` → `200` with `x-robots-tag: noindex`; grep count `1`.

- [ ] **Step 2: Preview untouched**

```bash
curl -s -o /dev/null -w "preview root: %{http_code}\n" https://preview.jonnxor.is/
curl -s https://preview.jonnxor.is/ | grep -c "construction" || true
```

Expected: `preview root: 200`; grep count `0` (no redirect, full site).

- [ ] **Step 3: Walk the spec's success criteria (§6) and report each item honestly.**
