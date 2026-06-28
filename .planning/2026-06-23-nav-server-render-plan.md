# Nav → Nav.astro Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the nav's markup out of `site.js` into a server-rendered `Nav.astro`, keeping all behavior in `site.js` — no visual or UX change.

**Architecture:** `Nav.astro` takes a `page` prop and emits the `<header class="site-nav">` plus the body-level mobile dock/sheet, with active states computed server-side (replacing `site.js`'s `navHTML()`/`dockHTML()`). `Base.astro` renders `<Nav page={page} />` in place of the empty header. `site.js` loses its three markup builders and the `init()` render block; its behavior wiring already binds via `querySelector`, so it works unchanged against the server-rendered DOM.

**Tech Stack:** Astro 6, vanilla JS (`public/assets/site.js`), pnpm. Repo: `~/dev/code/jonnxor.is`. Commands run in WSL.

**Spec:** `projects/jonnxor.is/2026-06-23-nav-server-render.md`

**Conventions for this plan:**
- This is a faithful refactor with no automated unit tests; the "test" each task is a `pnpm build` + a grep of the built HTML + manual visual checks. The DOM emitted must match what `site.js` produced before.
- No worktree needed; work on the repo's current branch. Commit after each task.

---

## File Structure

| Path | Responsibility | Action |
|---|---|---|
| `src/components/Nav.astro` | Server-render the header (4 tiers) + dock + sheet from the `page` prop | **Create** |
| `src/layouts/Base.astro` | Render `<Nav page={page} />` instead of the empty `<header>` | **Modify** (lines 2, 33–35) |
| `public/assets/site.js` | Behavior only — remove `navHTML`/`dockHTML`/`footHTML`, the nav-only data, and the `init()` render block | **Modify** |

Active state and the four responsive tiers are pure CSS in `site.css` (untouched); the component just emits all tiers' markup at once, exactly as the JS did.

---

## Task 1: Create `Nav.astro`

**Files:**
- Create: `src/components/Nav.astro`

- [ ] **Step 1: Write the component**

Create `src/components/Nav.astro` with exactly:

```astro
---
// Site nav — was injected client-side by site.js (navHTML/dockHTML); now server-rendered.
// Behavior (theme/realm/mega/search/dock wiring) stays in site.js, which binds via querySelector.
interface Props { page?: string; }
const { page = "" } = Astro.props;
const current = page;

const PAGES = [
  { id: "home", label: "Home", href: "/" },
  { id: "about", label: "About", href: "/about" },
  { id: "cv", label: "CV", href: "/cv" },
  { id: "portfolio", label: "Portfolio", href: "/portfolio" },
  { id: "blog", label: "Blog", href: "/blog" },
  { id: "docs", label: "Docs", href: "/docs" },
  { id: "games", label: "Games", href: "/games" },
  { id: "wallpapers", label: "Wallpapers", href: "/wallpapers" },
  { id: "countdowns", label: "Countdowns", href: "/countdowns" },
];

const THEMES = [
  { key: "dawn", name: "Dawn (light)", icon: "sun" },
  { key: "rune", name: "Rune Night (dark)", icon: "moon" },
  { key: "neon", name: "Neon (gamer)", icon: "dragon" },
];
const LANGS = ["is", "en", "ja"];

const PAGE_DESC: Record<string, string> = {
  home: "The landing — start here",
  about: "The Saga of Jón + cover letter",
  cv: "The Record of Deeds — print-ready",
  portfolio: "The Forge — selected projects",
  blog: "The Codex — long-form writing",
  docs: "The Grimoire — guides & cheat sheets",
  games: "The Game Hall — tracker & favorites",
  wallpapers: "The Hoard — gallery & downloads",
  countdowns: "The Reckoning — timers & tallies",
};

const REALM_GROUPS = [
  { label: "The Work", ids: ["portfolio", "cv", "about"] },
  { label: "The Codex", ids: ["blog", "docs"] },
  { label: "The Hall", ids: ["games", "wallpapers", "countdowns"] },
];

const DOCK_SLOTS = [
  { id: "home", label: "Home", icon: "home" },
  { id: "portfolio", label: "Forge", icon: "hammer" },
  { id: "docs", label: "Grimoire", icon: "book" },
  { id: "games", label: "Hall", icon: "pad" },
];

const ICONS: Record<string, string> = {
  search: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.6" stroke-linecap="round"><circle cx="10.5" cy="10.5" r="6.5"/><path d="M15.5 15.5 L21 21"/></svg>',
  sun: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><circle cx="12" cy="12" r="4.2" fill="currentColor" stroke="none"/><path d="M12 2.5v2.4M12 19.1v2.4M2.5 12h2.4M19.1 12h2.4M5.2 5.2l1.7 1.7M17.1 17.1l1.7 1.7M18.8 5.2l-1.7 1.7M6.9 17.1l-1.7 1.7"/></svg>',
  moon: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z"/></svg>',
  dragon: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M21 11 L8 5 L3 1 L7 8 L4 9 L8 17 L14 14 L12 11 Z"/></svg>',
  home: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linejoin="round"><path d="M3 11 L12 3 L21 11 V21 H14 V15 H10 V21 H3 Z"/></svg>',
  hammer: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M4 7 L13 7 L13 3 L17 3 L20 6 L20 10 L13 10 L13 11 L4 11 Z"/><path d="M8.5 11 L8.5 21"/></svg>',
  book: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M4 4 H10 C11.1 4 12 4.9 12 6 V20 C12 19 11 18.5 10 18.5 H4 Z"/><path d="M20 4 H14 C12.9 4 12 4.9 12 6 V20 C12 19 13 18.5 14 18.5 H20 Z"/></svg>',
  pad: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M7 8 H17 C19.5 8 21 10 21 13 C21 16 19.8 17.5 18.4 17.5 C16.5 17.5 16.4 15 14.5 15 H9.5 C7.6 15 7.5 17.5 5.6 17.5 C4.2 17.5 3 16 3 13 C3 10 4.5 8 7 8 Z"/><path d="M8 10.6 V13.4 M6.6 12 H9.4 M15.6 11 h.01 M17.4 13 h.01"/></svg>',
  dots: '<svg viewBox="0 0 24 24" fill="currentColor"><circle cx="5" cy="12" r="1.8"/><circle cx="12" cy="12" r="1.8"/><circle cx="19" cy="12" r="1.8"/></svg>',
};

const pad2 = (i: number) => (i < 9 ? "0" : "") + (i + 1);
const pageById = (id: string) => PAGES.find((p) => p.id === id)!;
const dockIds = DOCK_SLOTS.map((s) => s.id);
const moreActive = current && !dockIds.includes(current);
const morePages = PAGES.filter((p) => !dockIds.includes(p.id));
---

<header class="site-nav">
  <div class="nav-inner">
    <a class="nav-logo" href="/">
      <svg width="22" height="22" viewBox="0 0 24 24" fill="currentColor"><path d="M21 11 L8 5 L3 1 L7 8 L4 9 L8 17 L14 14 L12 11 Z"/></svg>
      <span>JONN<span class="xor">XOR</span></span>
    </a>

    <nav class="nav-hud" aria-label="Primary">
      {PAGES.map((p, i) => (
        <a class:list={["nav-hlink", p.id === current && "here"]} href={p.href}>
          <span class="n">{pad2(i)}</span><span class="t">{p.label}</span>
        </a>
      ))}
    </nav>

    <nav class="nav-realms" aria-label="Primary">
      {REALM_GROUPS.map((g, gi) => (
        <div class="nav-realm">
          <button type="button" class:list={["nav-rtop", g.ids.includes(current) && "active"]} data-realm={gi} aria-expanded="false">
            {g.label} <span class="car">▼</span>
          </button>
          <div class="nav-rpanel" hidden>
            {g.ids.map((id) => (
              <a class:list={["nav-ritem", id === current && "here"]} href={pageById(id).href}>
                <span class="t">{pageById(id).label}</span><span class="d">{PAGE_DESC[id]}</span>
              </a>
            ))}
          </div>
        </div>
      ))}
    </nav>

    <button type="button" class="nav-seek" aria-label="Search">
      <Fragment set:html={ICONS.search} />
      <span>Seek pages, docs, spells…</span><span class="sk">/</span>
    </button>

    <div class="nav-controls">
      <button type="button" class="nav-search" aria-label="Search (press /)" title="Search (press /)"><Fragment set:html={ICONS.search} /><span class="sk">/</span></button>
      <button type="button" class="nav-menubtn" aria-expanded="false">☰ Menu</button>
      <div class="lang-toggle" role="group" aria-label="Language">
        {LANGS.map((l) => (<button type="button" data-lang={l}>{l.toUpperCase()}</button>))}
      </div>
      <div class="theme-toggle" role="group" aria-label="Theme">
        {THEMES.map((t) => (
          <button type="button" data-set={t.key} aria-label={t.name} title={t.name} set:html={ICONS[t.icon]} />
        ))}
      </div>
      <button type="button" class="nav-orb" aria-label="Theme — click to cycle"></button>
    </div>

    <div class="nav-mega" hidden>
      <div class="mg-grid">
        {PAGES.map((p, i) => (
          <a class:list={["mg-cell", p.id === current && "here"]} href={p.href}>
            <span class="n">{pad2(i)}</span><span class="t">{p.label}</span>
          </a>
        ))}
      </div>
    </div>
  </div>
</header>

{/* dock + sheet are body-level siblings — the header's backdrop-filter would trap position:fixed */}
<div class="jx-sheet" hidden>
  <div class="sh-hd">More halls</div>
  {morePages.map((p) => (
    <a class:list={["sh-row", p.id === current && "here"]} href={p.href}>
      <span class="t">{p.label}</span><span class="d">{(PAGE_DESC[p.id].split("—")[0] || "").trim().toLowerCase()}</span>
    </a>
  ))}
  <div class="sh-foot">
    <div class="theme-toggle" role="group" aria-label="Theme">
      {THEMES.map((t) => (<button type="button" data-set={t.key} aria-label={t.name} title={t.name} set:html={ICONS[t.icon]} />))}
    </div>
    <div class="lang-toggle" role="group" aria-label="Language">
      {LANGS.map((l) => (<button type="button" data-lang={l}>{l.toUpperCase()}</button>))}
    </div>
  </div>
</div>

<nav class="jx-dock" aria-label="Quick navigation">
  {DOCK_SLOTS.map((s) => (
    <a class:list={["jx-slot", s.id === current && "here"]} href={pageById(s.id).href}>
      <Fragment set:html={ICONS[s.icon]} /><span>{s.label}</span>
    </a>
  ))}
  <button type="button" class:list={["jx-slot", "jx-more", moreActive && "here"]} aria-expanded="false">
    <Fragment set:html={ICONS.dots} /><span>More</span>
  </button>
</nav>
```

- [ ] **Step 2: Type-check / build the component in isolation**

Run: `cd ~/dev/code/jonnxor.is && pnpm build`
Expected: build succeeds. (Nav.astro isn't imported yet, so this just confirms it has no syntax/TS errors. Astro builds all components.)

- [ ] **Step 3: Commit**

```bash
cd ~/dev/code/jonnxor.is
git add src/components/Nav.astro
git commit -m "feat(nav): add server-rendered Nav.astro component"
```

---

## Task 2: Wire `Nav.astro` into `Base.astro`

**Files:**
- Modify: `src/layouts/Base.astro` (line 2 import; lines 34–35 header)

- [ ] **Step 1: Add the import**

In `src/layouts/Base.astro`, change line 2 from:
```astro
import Footer from "../components/Footer.astro";
```
to:
```astro
import Footer from "../components/Footer.astro";
import Nav from "../components/Nav.astro";
```

- [ ] **Step 2: Replace the empty header with the component**

Replace these two lines (34–35):
```astro
    {/* site.js renders the adaptive nav + footer and appends the mobile dock + search modal */}
    <header class="site-nav"></header>
```
with:
```astro
    {/* Nav.astro server-renders the adaptive nav + mobile dock; site.js wires behavior + appends the search modal */}
    <Nav page={page} />
```

- [ ] **Step 3: Build and confirm the nav is now in the static HTML**

Run:
```bash
cd ~/dev/code/jonnxor.is && pnpm build
grep -o 'class="nav-inner"' dist/index.html | head -1
grep -o 'class="jx-dock"' dist/index.html | head -1
```
Expected: build succeeds; both greps print a match (the nav + dock are now server-rendered into `dist/index.html`, which they previously were NOT).

- [ ] **Step 4: Confirm active state renders server-side on a sub-page**

Run: `grep -o 'nav-hlink here[^"]*"[^>]*href="/docs"' dist/docs/index.html | head -1`
Expected: a match — the Docs HUD link carries `here` in the static HTML for `/docs` (active state computed from the `page` prop). If empty, check the markup order of `class`/`href` and grep more loosely: `grep -o 'href="/docs"' dist/docs/index.html | head`.

- [ ] **Step 5: Commit**

```bash
cd ~/dev/code/jonnxor.is
git add src/layouts/Base.astro
git commit -m "feat(nav): render Nav.astro in Base layout"
```

*(After this commit the nav renders twice in the browser — once server-side, once injected by `site.js` — until Task 3. That's expected and fixed next.)*

---

## Task 3: Slim `site.js` to behavior only

**Files:**
- Modify: `public/assets/site.js`

- [ ] **Step 1: Remove the nav-only data constants**

Delete these three blocks (the `PAGE_DESC`, `REALM_GROUPS`, and `DOCK_SLOTS` declarations). `PAGES`, `THEMES`, `LANGS`, and `ICONS` are still used by search/theme/dragonfly — keep them.

Delete:
```js
  var PAGE_DESC = {
    home: 'The landing — start here',
    about: 'The Saga of Jón + cover letter',
    cv: 'The Record of Deeds — print-ready',
    portfolio: 'The Forge — selected projects',
    blog: 'The Codex — long-form writing',
    docs: 'The Grimoire — guides & cheat sheets',
    games: 'The Game Hall — tracker & favorites',
    wallpapers: 'The Hoard — gallery & downloads',
    countdowns: 'The Reckoning — timers & tallies'
  };

  var REALM_GROUPS = [
    { label: 'The Work', ids: ['portfolio', 'cv', 'about'] },
    { label: 'The Codex', ids: ['blog', 'docs'] },
    { label: 'The Hall', ids: ['games', 'wallpapers', 'countdowns'] }
  ];

  var DOCK_SLOTS = [
    { id: 'home', label: 'Home', icon: 'home' },
    { id: 'portfolio', label: 'Forge', icon: 'hammer' },
    { id: 'docs', label: 'Grimoire', icon: 'book' },
    { id: 'games', label: 'Hall', icon: 'pad' }
  ];
```

- [ ] **Step 2: Remove the markup builders**

Delete the entire block from the `nav + footer` section comment through the end of `footHTML()` — that's `pageById`, `navHTML`, `dockHTML`, and the now-dead `footHTML` (the footer is already `Footer.astro`). The block starts with:
```js
  /* ---------- nav + footer ---------- */
  function pageById(id) {
```
and ends with the closing of `footHTML`:
```js
      '</div>'
    );
  }
```
(immediately before the `/* ---------- countdown utils ---------- */` comment). Delete everything in between, inclusive. Leave the `/* ---------- countdown utils ---------- */` section intact.

- [ ] **Step 3: Remove the render block from `init()`**

In `init()`, replace:
```js
  function init() {
    var body = document.body;
    var current = body.getAttribute('data-page') || '';

    var header = document.querySelector('header.site-nav');
    if (header) {
      header.innerHTML = navHTML(current);
      // dock + sheet live on <body> so position:fixed isn't trapped by the
      // header's backdrop-filter
      var dockWrap = document.createElement('div');
      dockWrap.innerHTML = dockHTML(current);
      while (dockWrap.firstChild) body.appendChild(dockWrap.firstChild);
    }

    // footer is server-rendered now (src/components/Footer.astro)

    // theme
```
with:
```js
  function init() {
    var body = document.body;

    // nav + dock + footer are server-rendered now (Nav.astro / Footer.astro);
    // this script only wires up behavior below.

    // theme
```

- [ ] **Step 4: Confirm no dangling references**

Run:
```bash
cd ~/dev/code/jonnxor.is
grep -nE 'navHTML|dockHTML|footHTML|REALM_GROUPS|DOCK_SLOTS|PAGE_DESC|pageById' public/assets/site.js || echo "clean — no references remain"
```
Expected: `clean — no references remain`. (If any line prints, it's a leftover use — remove it.)

- [ ] **Step 5: Build + confirm the nav renders exactly once**

Run:
```bash
cd ~/dev/code/jonnxor.is && pnpm build
grep -c 'class="nav-inner"' dist/index.html
```
Expected: build succeeds; count is `1` (server-rendered only — `site.js` no longer injects a second copy).

- [ ] **Step 6: Commit**

```bash
cd ~/dev/code/jonnxor.is
git add public/assets/site.js
git commit -m "refactor(nav): remove client-side nav/dock builders from site.js"
```

---

## Task 4: Verify behavior end-to-end in the browser

**Files:** none (manual verification).

- [ ] **Step 1: Start the dev server**

Run: `cd ~/dev/code/jonnxor.is && pnpm dev`
Expected: serves on http://localhost:4321. Open it in a browser.

- [ ] **Step 2: Verify the four responsive tiers**

Resize the window and confirm each tier shows and the nav is present immediately on load (no flash/pop-in):
- ≥1920px → **HUD** (9 numbered links).
- 1280–1919px → **Three Realms** (grouped dropdowns).
- 881–1279px → **Seeker** (search pill + ☰ Menu → mega panel).
- ≤880px → **Quick-slot dock** at the bottom + **More** sheet.

Expected: identical to before; the active page is highlighted in each tier.

- [ ] **Step 3: Verify interactive behavior (all wired by the untouched `site.js`)**

- Theme switch: click ☀ / ☾ / 🐉 (and the round orb) — theme changes, persists across reload, orb icon updates.
- Realm dropdowns open/close; outside-click and `Escape` close them.
- ☰ Menu opens the mega panel.
- Search opens via the nav search button, the Seeker pill, `/`, and `Ctrl/⌘-K`; results work and navigate.
- Mobile dock **More** opens the sheet (incl. theme/lang toggles).

Expected: every behavior works exactly as before (these bind via `querySelector` against the now-server-rendered DOM).

- [ ] **Step 4: Verify active page across routes**

Visit `/`, `/about`, `/cv`, `/portfolio`, `/blog`, a blog post, `/docs`, `/games`, `/wallpapers`, `/countdowns`, and `/404`. Confirm the correct nav item is marked active on each (and none on 404).

- [ ] **Step 5: Final build sanity**

Run: `cd ~/dev/code/jonnxor.is && pnpm build`
Expected: clean build, no warnings about Nav.astro.

---

## Done criteria
- `Nav.astro` server-renders the header + dock + sheet from the `page` prop; present in `dist/*.html`.
- `Base.astro` uses `<Nav page={page} />`; `site.js` no longer builds or injects nav markup (`grep` clean).
- Nav renders exactly once, no load-in flash, all four tiers + every behavior identical to before, active state correct on every route.
