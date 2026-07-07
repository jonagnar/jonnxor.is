# JonnXor Design System

The personal brand system for **JonnXor** (Jón Agnar Stefánsson) — Icelandic full-stack
developer, lifelong gamer, Viking-descended trickster-god enthusiast. The system powers
his personal website (home, about/cover letter, CV, portfolio, blog, docs/grimoire,
game tracker, wallpaper stash, countdowns, 404).

**Personality:** Norse myth meets fantasy gaming. Warm, a little mischievous, introspective
(INFJ), cozy (masala chai), but professional enough to send to a recruiter.

**Source:** built from the owner's written brief (June 2026). No prior codebase or Figma —
this site IS the canonical implementation.

---

## The three themes (one token system)

Everything is expressed as CSS custom properties in `../assets/tokens.css`. Three themes
re-assign the same vocabulary; **never hardcode a themed color in components**.

| Theme | Key | Story | Character |
|---|---|---|---|
| **Dawn** | `dawn` | Icelandic daylight | Paper whites, black ink, deep turquoise, warm yellow. NO glow (`--glow-r: 0`). |
| **Rune Night** | `rune` *(default)* | Electric runes on near-black | Near-black `#07060c`, electric turquoise `#00f5d4`, neon yellow `#f5e642`, moderate glow. |
| **Neon** | `neon` | Pink synthwave, ALL the lights on | Deep violet-black, hot pink `#ff3dae`, neon yellow `#ffe14d`, ambient glow **without hover** (`--ambient-card`, `--ambient-text`), pink CRT scanlines. |

Apply with `data-theme="dawn|rune|neon"` on `<html>` — or on **any container** to scope a
theme locally (specimen cards do this). The theme switcher (sun ☀ / moon ☾ / dragon 🐉)
is a signature element and must appear in any site chrome.

### Token vocabulary (semantic, not literal)
- `--bg`, `--bg-2`, `--surface`, `--surface-2` — backgrounds, page → card
- `--ink`, `--ink-soft`, `--ink-faint` — text hierarchy
- `--line`, `--line-strong` — borders
- `--tq`, `--tq-ink`, `--tq-deep`, `--tq-soft`, `--on-tq` — the **accent channel**.
  Turquoise in Dawn/Rune, **pink in Neon**. Links, active states, structure.
- `--gold`, `--gold-ink`, `--gold-soft`, `--on-gold` — the **highlight channel**.
  Yellow everywhere. Primary CTAs, badges, prized things.
- `--glow-tq`, `--glow-gold`, `--glow-r`, `--glow-mult` — glow system
- `--ambient-card`, `--ambient-text` — always-on glow (Neon only; `none` elsewhere)
- `--aurora` — decorative radial-gradient backdrop for heroes/page heads
- `--scanline-a`, `--scanline-c` — CRT overlay (Neon only, opacity .05, toggleable)
- `--font-display`, `--font-body`, `--font-mono`; `--r-sm/md/lg`; `--space-1..8`; `--nav-h`; `--maxw`; `--ease-out`

**Rule of accent balance:** turquoise/pink is structural and frequent; gold is scarce and
precious. If everything glows gold, nothing is treasure.

---

## Content fundamentals

- **Voice:** first person, warm, wry, precise. Mischief lives in *asides*, not in the
  facts. ("Honour mode · attempt #3, pray for us")
- **Mythic framing for navigation and titles:** pages get saga-names — The Forge
  (portfolio), The Codex (blog), The Grimoire (docs), The Game Hall, The Hoard
  (wallpapers), The Reckoning (countdowns), The Record of Deeds (CV). Body copy stays
  plain and concrete.
- **Professional surfaces stay sober:** CV and cover letter contain zero jokes beyond a
  light closing line. Print output is fully corporate.
- **Casing:** Title Case for page titles; sentence case for everything else. Kickers and
  nav are CSS-uppercased Orbitron.
- **Icelandic flavor** is welcome and untranslated where evocative (Þú dóst, Jól,
  Drekinn) — always with context so non-speakers follow.
- **Emoji:** essentially never in copy. The only sanctioned emoji are the theme-switcher
  glyphs (☀ ☾ 🐉 rendered as SVG) and count-up icons (⚒ 🎮 📖 🚽). No decorative emoji.
- **Numbers are playful but precise** — "batch #214", "4.2k stars", four-decimal live
  counters. Never vague data-slop stats.

## Visual foundations

- **Type:** Orbitron (display; 500–900) for headings, nav, numbers, kickers — always with
  letter-spacing (.02–.3em, wider as text gets smaller/upper). Montserrat (body; 400–700).
  JetBrains Mono for code and dates. Body 16px/1.65; minimum UI size 10px Orbitron
  (uppercase, tracked) — used only for chips/labels.
- **Backgrounds:** flat token colors plus the `--aurora` radial wash on heroes/page heads.
  No photographic backgrounds. Gradients appear only as *content placeholders* (cover
  art, wallpapers) — diagonal (135–150deg), dark → brand hue → bright edge.
- **Corners:** small and angular — 4px (buttons, chips-adjacent), 8px (cards), 14px
  (lightbox). Pills (99px) only for toggle clusters and chips. The brand is angular;
  avoid big soft radii.
- **Borders:** 1px `--line`; `--line-strong` for interactive outlines. Cards are
  `--surface` + 1px border, sharp 8px corners.
- **Shadow/glow:** shadows are glow. `box-shadow: 0 0 var(--glow-r) var(--glow-tq|gold)`
  on accents; `--shadow` (soft black) only under elevated surfaces (lightbox, CV sheet).
  Dawn has zero glow. Neon adds ambient glow on cards/headings at rest.
- **Hover states:** cards lift 3px + border turns accent + glow blooms; buttons lift 1px
  + glow blooms; links underline (3px offset); nav links get `--tq-soft` wash. Press
  states: rely on hover + native; nothing shrinks.
- **Motion:** fast and restrained — .2–.35s `cubic-bezier(.22,1,.36,1)`. Scroll-reveal
  (.reveal → .in): 14px rise + fade, .6s. One long ambient exception: the dragonfly
  flight (14s). Everything respects `prefers-reduced-motion`.
- **Layout:** max-width 1120px, 24px gutters; sticky 64px nav with backdrop blur (bar
  widens to 1440px on ultrawide); CSS grid with `gap` everywhere; sections separated by
  rune dividers rather than big color bands. Mobile collapse: grids → 1 col; navigation
  is fully adaptive — see **Navigation & search** below.
- **Transparency/blur:** only the sticky nav (82% bg + 14px blur) and lightbox scrim.
- **Imagery:** none shipped yet — gradient placeholders with Orbitron initials stand in
  for project covers, game art, wallpapers. When real art arrives it should be dark,
  saturated, and cool-toned (aurora/synthwave palette).

## Navigation & search

The primary nav is **adaptive** — one chrome (`assets/site.js` renders it), four faces
by viewport width. There is no burger menu anywhere.

| Width | Tier | Shape |
|---|---|---|
| ≥1920px | **HUD** | All 9 links, each with a mono number `01–09` above an Orbitron label; current page gets gold text + a glowing 22px gold underline marker. Controls condense: search button + the **theme orb** (single round button cycling ☀→☾→🐉). |
| 1280–1919px | **Three Realms** | Links grouped into dropdowns — **The Work** (Portfolio, CV, About), **The Codex** (Blog, Docs), **The Hall** (Games, Wallpapers, Countdowns). Panel items show the page name + its saga-name subtitle. The realm containing the current page is underlined; the current item is gold with a `⟡` suffix. Full controls (search · lang · theme triple). |
| 881–1279px | **Seeker** | A centered “Seek pages, docs, spells…” pill (opens the search modal) + a gold-bordered **Menu** button toggling a 3-column mega panel of all 9 numbered pages. |
| ≤880px | **Quick-slot dock** | Fixed bottom bar, game quick-slot style: Home / Forge / Grimoire / Hall / **More**, icon above label, active slot glows turquoise with a top indicator bar. *More* opens a bottom sheet with the remaining five pages plus the theme + language toggles. Body gets 86px bottom padding; the dock is hidden in print. |

Rules: the three-state theme switcher (sun/moon/dragon) remains the signature — the
ultrawide orb is its sanctioned compact form, and the mobile sheet carries the full
triple. Active-page marking is always gold. Dropdowns/panels close on outside click and
<kbd>Esc</kbd>.

**Site-wide search** is a core component: a modal opened from any page via the nav
search button, the Seeker pill, <kbd>/</kbd>, or <kbd>Ctrl/⌘ K</kbd>. It searches page
names and full Grimoire doc text (`assets/docs-data.js`, lazy-loaded), groups results
into **Pages** / **Grimoire**, highlights matches with gold `<mark>` snippets, and
supports ↑↓ + Enter. Empty query shows the page quick-list.

## Iconography & motifs

- **Icons are hand-drawn inline SVG**, stroke or flat fill `currentColor`, 15–24px, drawn
  angular to match the brand. The set lives in `../assets/site.js` (`JX.icons`): sun,
  moon, dragon, dragonfly, search, home, hammer (Forge), book (Grimoire), gamepad
  (Hall), dots (More). No icon font, no external icon CDN. Keep new icons geometric,
  pointed, few.
- **The dragon mark** (angular polygon, `M21 11 L8 5 L3 1 L7 8 L4 9 L8 17 L14 14 L12 11 Z`)
  is the logo glyph — gold in chrome, any accent in decoration.
- **The JX monogram** — gold `J` + turquoise `X` on a fixed near-black tile — is the
  **favicon / app-icon**. It is the one mark that stays theme-independent (the dark tile is
  baked in) so it reads identically in browser tabs and on home screens. Source
  `public/favicon.svg`; the raster set (`.ico`, apple-touch, 192/512) is generated by
  `scripts/generate-icons.mjs`. It complements the dragon glyph, it does not replace it.
- **The constellation dragon** ("Drekinn") — turquoise/pink polylines + gold star nodes —
  is the hero illustration pattern: draw imagery as star-charts, not filled art. The
  footer carries a second chart: **Taurus** ("Nautíð", the owner's star sign) with
  Aldebaran as the one glowing gold star and the Pleiades as faint companions.
- **Rune dividers** are SVG masks (guaranteed render, theme-aware via `currentColor`),
  via `.rune-divider` — use between page sections.
- **Motif hierarchy:** dragons primary; cats and owls supporting (copy-level mostly);
  runes and knotwork as subtle structure. **One easter-egg dragonfly is sanctioned;
  no other insects, ever.**
- **Unicode runes** (ᛞ etc.) may appear inside generated/canvas art but not in UI chrome
  (font support varies).

## Accessibility

- Contrast ≥ 4.5:1 for body text in all three themes (that's why `--tq-ink`/`--gold-ink`
  exist as text-safe variants — e.g. gold text on Dawn uses `#876312`, never `#ecbd3e`).
- Neon is "readable first, neon second": neon as accent, never assault; scanlines .05.
- Focusable controls ≥ 44px touch targets on mobile; `aria-label`s on icon-only buttons.

---

## Index

| Path | What |
|---|---|
| `../assets/tokens.css` | **The tokens.** Three themes + base element styles. |
| `../assets/site.css` | Component library: adaptive nav (all four tiers + dock/sheet), search modal, footer, buttons, chips, badges, cards, tabs, grids, code blocks, doc tables, kbd, tip callouts, rune divider, lightbox, dragonfly. |
| `../assets/site.js` | Site chrome renderer + `window.JX` API (setTheme/setLang/setGlow/setScanlines/daysUntil/openSearch/icons). |
| `../assets/docs-data.js` | Grimoire content (docs as data: realm/game/cat/tags + HTML body). |
| `../assets/tweaks.jsx`, `../assets/tweaks-panel.jsx` | Tweaks panel (theme/glow/scanlines). |
| `cards/*.html` | Specimen cards rendered in the Design System tab. |
| `SKILL.md` | Agent skill entry point (for Claude Code handoff). |
| `../index.html` … `../404.html` | The eleven pages — canonical usage examples (docs.html shows the three-column reading layout: filter sidebar / article / scrollspy TOC). |

**Using the system in a new page:** copy the `<head>` boilerplate from any page (theme
pre-paint script + both stylesheets), add `<header class="site-nav"></header>` /
`<footer class="site-footer"></footer>`, set `data-page`, and include `assets/site.js`
— it renders the adaptive nav, appends the mobile dock + search modal, and wires all
behaviors automatically.
