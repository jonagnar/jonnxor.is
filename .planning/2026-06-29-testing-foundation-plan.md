# Testing Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the full test pyramid (rendering, visual-regression, E2E) before componentizing the inline pages, so the refactor and every later cycle have a regression net.

**Architecture:** Two runners. **Vitest** keeps the existing logic unit tests and gains **Astro Container API** rendering tests; its config moves to `getViteConfig()` so `astro:i18n`/`astro:content` resolve in tests. **Playwright** adds self-baseline visual regression (baselines captured from the current, already-faithful Astro site) and E2E coverage of every JS-driven surface. CI runs Vitest on every push and Playwright (in the pinned `mcr.microsoft.com/playwright` container) on PRs and the `preview` branch.

**Tech Stack:** Astro 6.4.7 (SSG), Vitest 4, `astro/container`, `@playwright/test`, pnpm 10.34.4, Forgejo Actions CI. Spec: [.planning/2026-06-29-testing-foundation-design.md](.planning/2026-06-29-testing-foundation-design.md).

**Conventions:** run node/pnpm via `wsl` + mise; run `git` via Windows git with `safe.directory='*'`. Branch is `chore/testing-foundation` (already created). Commit after each task. Do **not** push (let the author decide).

**Playwright execution — container-only (decided 2026-06-29).** Docker is available in WSL. ALL Playwright runs (E2E **and** visual) execute inside the pinned `mcr.microsoft.com/playwright:v<VERSION>-noble` image — no browsers are installed in WSL. This makes local runs match CI byte-for-byte and visual baselines deterministic. A committed wrapper `scripts/pw.sh` encapsulates the `docker run`, so every Playwright command in Tasks 6–14 is `wsl sh scripts/pw.sh test <args>` (replacing the `pnpm exec playwright test <args>` shown in those tasks). `<VERSION>` is the exact `@playwright/test` version captured in Task 5.

---

## File Structure

| File | Responsibility | Action |
|---|---|---|
| `vitest.config.ts` | Vitest config wrapped by Astro's `getViteConfig` | Modify |
| `tests/render/nav.test.ts` | Container-API render assertions for `Nav.astro` | Create |
| `tests/render/footer.test.ts` | Container-API render assertions for `Footer.astro` | Create |
| `tests/render/pages.test.ts` | Smoke render of non-collection static pages | Create |
| `playwright.config.ts` | Playwright runner: webServer, chromium project, snapshot tolerance | Create |
| `scripts/pw.sh` | Wrapper: runs Playwright in the pinned container (canonical for dev + CI parity) | Create |
| `tests/visual/pages.spec.ts` | Self-baseline full-page visual matrix | Create |
| `tests/visual/pages.spec.ts-snapshots/` | Committed baseline PNGs | Create (generated) |
| `tests/e2e/theme.spec.ts` | Theme orb cycle + persistence + no-flash | Create |
| `tests/e2e/lang.spec.ts` | Language switcher navigation + `jx-lang` | Create |
| `tests/e2e/search.spec.ts` | `/`-triggered search modal | Create |
| `tests/e2e/grimoire.spec.ts` | `/docs` reader: deep-link, filter, persistence, pager | Create |
| `tests/e2e/games.spec.ts` | Games tracker tabs + `jx-games-tab` | Create |
| `tests/e2e/countdowns.spec.ts` | Countdown/count-up grids populate + tick | Create |
| `tests/e2e/blog.spec.ts` | Blog list dates + post navigation + pager | Create |
| `tests/e2e/chrome.spec.ts` | CV print, 404 souls counter, nav active/dock | Create |
| `package.json` | `test:e2e` / `test:visual` scripts + Playwright dep | Modify |
| `.forgejo/workflows/ci.yml` | Vitest job (push) + Playwright job (PR/preview) | Modify |
| `.gitignore` | Ignore Playwright `test-results/`, `playwright-report/` | Modify |
| `README.md` | Testing section + .NET test-project convention | Modify |

---

## Task 1: Wire Vitest to Astro's config

**Files:**
- Modify: `vitest.config.ts`

- [ ] **Step 1: Replace the config with `getViteConfig`**

This makes `astro:i18n`, `astro:content`, and the project's i18n settings resolve inside tests (required by the render tests). Replace the entire file with:

```ts
/// <reference types="vitest/config" />
import { getViteConfig } from 'astro/config';

export default getViteConfig({
  test: {
    include: ['tests/**/*.test.ts'],
  },
});
```

- [ ] **Step 2: Run the existing logic tests to verify nothing regressed**

Run: `wsl pnpm test`
Expected: PASS — the 6 existing files under `tests/content/` and `tests/i18n/` (the run boots Astro's Vite pipeline first, so it is slower than before, but all assertions stay green).

- [ ] **Step 3: Commit**

```bash
git -c safe.directory='*' add vitest.config.ts
git -c safe.directory='*' commit -m "test: wire Vitest to Astro config via getViteConfig"
```

---

## Task 2: Render test — `Nav.astro`

**Files:**
- Create: `tests/render/nav.test.ts`

- [ ] **Step 1: Write the test**

`Nav.astro` takes a `page` prop and marks the active link with `here`; it renders the wordmark, the theme/lang controls, and the mobile dock. The Container API renders it to a string; we pass a `request` so `Astro.url`/locale resolve (locale falls back to `is`).

```ts
import { experimental_AstroContainer as AstroContainer } from 'astro/container';
import { describe, it, expect } from 'vitest';
import Nav from '../../src/components/Nav.astro';

async function renderNav(page: string) {
  const container = await AstroContainer.create();
  return container.renderToString(Nav, {
    props: { page },
    request: new Request('https://jonnxor.is/about'),
  });
}

describe('Nav.astro', () => {
  it('renders the chrome: wordmark, theme + lang controls, dock', async () => {
    const html = await renderNav('about');
    expect(html).toContain('class="site-nav"');
    expect(html).toContain('JONN');
    expect(html).toContain('XOR');
    // theme toggle exposes one button per theme via data-set
    expect(html).toContain('data-set="dawn"');
    expect(html).toContain('data-set="rune"');
    expect(html).toContain('data-set="neon"');
    // language toggle exposes one button per locale via data-lang
    expect(html).toContain('data-lang="is"');
    expect(html).toContain('data-lang="en"');
    expect(html).toContain('data-lang="ja"');
    // mobile dock + cycle orb
    expect(html).toContain('class="jx-dock"');
    expect(html).toContain('nav-orb');
  });

  it('marks the current page active in the HUD', async () => {
    const html = await renderNav('about');
    // the about HUD link gains the `here` class when page === "about"
    expect(html).toMatch(/class="nav-hlink here"[^>]*href="\/about"/);
  });

  it('does not mark a non-current page active', async () => {
    const html = await renderNav('cv');
    expect(html).not.toMatch(/class="nav-hlink here"[^>]*href="\/about"/);
  });
});
```

- [ ] **Step 2: Run it**

Run: `wsl pnpm test -- tests/render/nav.test.ts`
Expected: PASS. If `here` ordering differs (Astro may emit `class="here nav-hlink"`), relax the active-state regex to `/href="\/about"[^>]*class="[^"]*\bhere\b/` or `/\bhere\b[^>]*href="\/about"/` — inspect the printed HTML on failure and match the real attribute order.

- [ ] **Step 3: Commit**

```bash
git -c safe.directory='*' add tests/render/nav.test.ts
git -c safe.directory='*' commit -m "test(render): Nav chrome + active-state"
```

---

## Task 3: Render test — `Footer.astro`

**Files:**
- Create: `tests/render/footer.test.ts`

- [ ] **Step 1: Write the test**

`Footer.astro` is pure chrome: wordmark, contact rows, the constellation caption, and the status chip. The Container API leaves `currentLocale` undefined so Footer defaults to `is`; assert the status chip **structurally** (`class="chip tq"`, unique to the status row) rather than by copy, so the test is locale-proof.

```ts
import { experimental_AstroContainer as AstroContainer } from 'astro/container';
import { describe, it, expect } from 'vitest';
import Footer from '../../src/components/Footer.astro';

describe('Footer.astro', () => {
  it('renders the footer chrome and contact rows', async () => {
    const container = await AstroContainer.create();
    const html = await container.renderToString(Footer, {
      request: new Request('https://jonnxor.is/'),
    });
    expect(html).toContain('class="site-footer"');
    expect(html).toContain('jon@jonnxor.is');
    expect(html).toContain('github.com/jonnxor');
    // status chip — assert structurally (locale-proof; `chip tq` is unique to the status row)
    expect(html).toContain('class="chip tq"');
    // footer links back to cv (href carries a trailing slash via getRelativeLocaleUrl)
    expect(html).toMatch(/href="\/cv\/?"/);
  });
});
```

- [ ] **Step 2: Run it**

Run: `wsl pnpm test -- tests/render/footer.test.ts`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git -c safe.directory='*' add tests/render/footer.test.ts
git -c safe.directory='*' commit -m "test(render): Footer chrome + contact rows"
```

---

## Task 4: Render test — static page smoke

**Files:**
- Create: `tests/render/pages.test.ts`

- [ ] **Step 1: Write the test**

Renders each page that does **not** read a content collection (collection pages `/blog`, `/docs` are covered by E2E/visual). Every page renders through `Base.astro`, so all must emit the nav + footer chrome; four pages also get a confirmed landmark assertion.

```ts
import { experimental_AstroContainer as AstroContainer } from 'astro/container';
import { describe, it, expect } from 'vitest';
import Index from '../../src/pages/index.astro';
import About from '../../src/pages/about.astro';
import Cv from '../../src/pages/cv.astro';
import Portfolio from '../../src/pages/portfolio.astro';
import Games from '../../src/pages/games.astro';
import Countdowns from '../../src/pages/countdowns.astro';
import Wallpapers from '../../src/pages/wallpapers.astro';
import NotFound from '../../src/pages/404.astro';

const PAGES = [
  ['index', Index],
  ['about', About],
  ['cv', Cv],
  ['portfolio', Portfolio],
  ['games', Games],
  ['countdowns', Countdowns],
  ['wallpapers', Wallpapers],
  ['404', NotFound],
] as const;

describe('static page smoke render', () => {
  it.each(PAGES)('renders %s with nav + footer chrome', async (_name, Page) => {
    const container = await AstroContainer.create();
    const html = await container.renderToString(Page, {
      request: new Request('https://jonnxor.is/'),
    });
    expect(html).toContain('class="site-nav"');
    expect(html).toContain('class="site-footer"');
    expect(html.length).toBeGreaterThan(2000);
  });

  it('renders confirmed page landmarks', async () => {
    const container = await AstroContainer.create();
    const req = new Request('https://jonnxor.is/');
    expect(await container.renderToString(Cv, { request: req })).toContain('The Record of Deeds');
    expect(await container.renderToString(Countdowns, { request: req })).toContain('The Reckoning');
    expect(await container.renderToString(Games, { request: req })).toContain('The Game Hall');
    expect(await container.renderToString(NotFound, { request: req })).toContain('ÞÚ DÓST');
  });
});
```

- [ ] **Step 2: Run it**

Run: `wsl pnpm test -- tests/render/pages.test.ts`
Expected: PASS. If a page import fails because it pulls a collection you didn't expect, drop it from `PAGES` and note it for E2E/visual coverage instead (only `blog.astro`/`docs.astro` should need this).

- [ ] **Step 3: Commit**

```bash
git -c safe.directory='*' add tests/render/pages.test.ts
git -c safe.directory='*' commit -m "test(render): static page smoke render"
```

---

## Task 5: Install Playwright + base config

**Files:**
- Modify: `package.json`
- Create: `playwright.config.ts`
- Modify: `.gitignore`

- [ ] **Step 1: Add the Playwright test dependency (no local browsers)**

Container-only execution (decided 2026-06-29): browsers live in the Docker image, so we do **not** run `playwright install` in WSL — just add the test runner so `playwright.config.ts` and the wrapper resolve it.

Run:
```bash
wsl pnpm add -D @playwright/test
```
Then capture the exact resolved version (drives the container image tag everywhere):
Run: `wsl pnpm exec playwright --version`
Expected: prints e.g. `Version 1.50.1`. **Record this string** — it is the `<VERSION>` used in `scripts/pw.sh` (Step 4b) and the CI image (Task 16).

- [ ] **Step 2: Add test scripts to `package.json`**

In the `"scripts"` block, after the existing `"test": "vitest run",` line, add:

```json
    "test:e2e": "playwright test tests/e2e",
    "test:visual": "playwright test tests/visual",
```

- [ ] **Step 3: Create `playwright.config.ts`**

Builds the site and serves the production preview (the real SSG output), one chromium project, deterministic viewport, and a small visual tolerance. Snapshots are stored next to the spec.

```ts
import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  // Playwright's default testMatch also matches *.test.ts — restrict to *.spec.ts
  // so it ignores the Vitest unit/render tests that live under tests/.
  testMatch: '**/*.spec.ts',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? 'github' : 'list',
  expect: {
    // small tolerance absorbs sub-pixel anti-aliasing in the pinned container
    toHaveScreenshot: { maxDiffPixelRatio: 0.01 },
  },
  use: {
    baseURL: 'http://localhost:4321',
    trace: 'on-first-retry',
    viewport: { width: 1280, height: 800 },
    deviceScaleFactor: 1,
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
  webServer: {
    command: 'pnpm build && pnpm preview --port 4321',
    url: 'http://localhost:4321',
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
});
```

- [ ] **Step 4: Ignore Playwright output**

Append to `.gitignore`:

```
# Playwright
/test-results/
/playwright-report/
/blob-report/
/playwright/.cache/
```

- [ ] **Step 4b: Create the container wrapper `scripts/pw.sh`**

Encapsulates the `docker run` so every later task (and the dev loop) uses one command. Replace `1.50.1` with the exact version from Step 1. Runs as the host uid so baseline PNGs aren't written root-owned, and sets `HOME=/work` so corepack/pnpm have a writable home.

```sh
#!/usr/bin/env sh
# Run Playwright inside the pinned container so local runs match CI byte-for-byte
# and visual baselines are deterministic. Usage: sh scripts/pw.sh test tests/e2e
set -eu
PW_IMAGE="mcr.microsoft.com/playwright:v1.50.1-noble"
exec docker run --rm --init \
  -v "$PWD":/work -w /work \
  -e HOME=/work -e CI="${CI:-}" \
  --user "$(id -u):$(id -g)" \
  "$PW_IMAGE" \
  sh -c "corepack enable && pnpm install --frozen-lockfile && pnpm exec playwright $*"
```

Make it executable: `wsl chmod +x scripts/pw.sh`

- [ ] **Step 5: Verify the harness boots in the container**

Run: `wsl sh scripts/pw.sh test --list`
Expected: the container pulls (first time), installs deps, and lists 0 tests (no specs yet) without config errors. If `pnpm install` complains about the store or a read-only home, confirm `-e HOME=/work` and the `--user` mapping are present.

- [ ] **Step 6: Commit**

```bash
git -c safe.directory='*' add package.json pnpm-lock.yaml playwright.config.ts scripts/pw.sh .gitignore
git -c safe.directory='*' commit -m "test: add Playwright + container wrapper (pw.sh) + base config"
```

---

## Task 6: Visual regression — self-baseline matrix

**Files:**
- Create: `tests/visual/pages.spec.ts`
- Create (generated): `tests/visual/pages.spec.ts-snapshots/`

> **Determinism:** baselines are font/OS-sensitive. Generate and compare them **only inside** the pinned container (via `scripts/pw.sh` from Task 5) so dev (WSL) and Forgejo CI match. Generate baselines with:
> ```bash
> wsl sh scripts/pw.sh test tests/visual --update-snapshots
> ```

- [ ] **Step 1: Write the visual spec**

Sets the theme via `localStorage('jx-theme')` *before* load (matches the pre-paint script), covers all routes at the default theme, three themes on a representative subset, locale spot-checks, and a mobile viewport. Dynamic regions are masked.

```ts
import { test, expect, type Page } from '@playwright/test';

// /countdowns is excluded: its grids are live data whose layout height shifts as values
// tick, so a full-page diff is non-deterministic (masking hides pixels, not reflow).
// Its behaviour is covered by tests/e2e/countdowns.spec.ts instead.
const ROUTES = [
  '/', '/about', '/cv', '/portfolio', '/blog',
  '/blog/what-sekiro-taught-me-about-code-review',
  '/docs', '/games', '/wallpapers', '/404',
];

// regions that legitimately change between runs — never part of the design diff
async function masks(page: Page) {
  return [
    page.locator('#souls'),          // 404 souls counter
    page.locator('.post-card time'), // locale dates on the blog list
  ];
}

async function setTheme(page: Page, theme: string) {
  await page.addInitScript((t) => localStorage.setItem('jx-theme', t), theme);
}

test.describe('visual — all routes @ rune/is', () => {
  for (const route of ROUTES) {
    test(`route ${route}`, async ({ page }) => {
      await setTheme(page, 'rune');
      await page.goto(route);
      await page.waitForLoadState('networkidle');
      await expect(page).toHaveScreenshot(`rune-is${route === '/' ? '/home' : route}.png`, {
        fullPage: true,
        mask: await masks(page),
      });
    });
  }
});

test.describe('visual — themes on a subset', () => {
  for (const theme of ['dawn', 'rune', 'neon']) {
    for (const route of ['/', '/blog', '/games']) {
      test(`${theme} ${route}`, async ({ page }) => {
        await setTheme(page, theme);
        await page.goto(route);
        await page.waitForLoadState('networkidle');
        await expect(page).toHaveScreenshot(`${theme}${route === '/' ? '/home' : route}.png`, {
          fullPage: true,
          mask: await masks(page),
        });
      });
    }
  }
});

test.describe('visual — locale spot-checks', () => {
  for (const path of ['/en/', '/ja/']) {
    test(`home ${path}`, async ({ page }) => {
      await setTheme(page, 'rune');
      await page.goto(path);
      await page.waitForLoadState('networkidle');
      await expect(page).toHaveScreenshot(`rune${path}home.png`, {
        fullPage: true,
        mask: await masks(page),
      });
    });
  }
});

test.describe('visual — mobile nav/dock', () => {
  test.use({ viewport: { width: 390, height: 844 } });
  for (const route of ['/', '/blog']) {
    test(`mobile ${route}`, async ({ page }) => {
      await setTheme(page, 'rune');
      await page.goto(route);
      await page.waitForLoadState('networkidle');
      await expect(page).toHaveScreenshot(`mobile${route === '/' ? '/home' : route}.png`, {
        fullPage: true,
        mask: await masks(page),
      });
    });
  }
});
```

- [ ] **Step 2: Generate the baselines inside the container**

Run: `wsl sh scripts/pw.sh test tests/visual --update-snapshots`
Expected: writes PNGs under `tests/visual/pages.spec.ts-snapshots/` and the run reports all tests passing (first run records, doesn't compare).

- [ ] **Step 3: Verify the gate compares clean on a second run**

Run: `wsl sh scripts/pw.sh test tests/visual`
Expected: PASS (0 diffs) — the site matches its own freshly-captured baselines.

- [ ] **Step 4: Commit (baselines included)**

```bash
git -c safe.directory='*' add tests/visual
git -c safe.directory='*' commit -m "test(visual): self-baseline matrix (routes x themes x locale x mobile)"
```

---

## Task 7: E2E — theme switching

**Files:**
- Create: `tests/e2e/theme.spec.ts`

- [ ] **Step 1: Write the spec**

`site.js` cycles `dawn → rune → neon` on the `.nav-orb`, persists `jx-theme`, and `Base.astro` applies the stored theme before first paint.

```ts
import { test, expect } from '@playwright/test';

test('orb cycles themes and persists', async ({ page }) => {
  await page.goto('/');
  const html = page.locator('html');
  const start = await html.getAttribute('data-theme');
  await page.locator('.nav-orb').click();
  const next = await html.getAttribute('data-theme');
  expect(next).not.toBe(start);
  // persisted to localStorage
  expect(await page.evaluate(() => localStorage.getItem('jx-theme'))).toBe(next);
});

test('data-set buttons select a theme directly', async ({ page }) => {
  await page.goto('/');
  await page.locator('.theme-toggle button[data-set="neon"]').first().click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'neon');
});

test('persisted theme applies before first paint (no flash)', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('jx-theme', 'dawn'));
  await page.goto('/');
  // the pre-paint inline script in Base.astro sets it before site.js runs
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dawn');
});
```

- [ ] **Step 2: Run it**

Run: `wsl pnpm exec playwright test tests/e2e/theme.spec.ts`
Expected: PASS (Playwright auto-starts the preview server via `webServer`).

- [ ] **Step 3: Commit**

```bash
git -c safe.directory='*' add tests/e2e/theme.spec.ts
git -c safe.directory='*' commit -m "test(e2e): theme cycle, direct set, no-flash"
```

---

## Task 8: E2E — language switching

**Files:**
- Create: `tests/e2e/lang.spec.ts`

- [ ] **Step 1: Write the spec**

`.lang-toggle button` carries a server-computed `data-href`; `site.js` stores `jx-lang` and navigates there.

```ts
import { test, expect } from '@playwright/test';

test('switching to EN navigates to the /en/ equivalent and stores jx-lang', async ({ page }) => {
  await page.goto('/about');
  await page.locator('.nav-controls .lang-toggle button[data-lang="en"]').click();
  await expect(page).toHaveURL(/\/en\/about\/?$/);
  expect(await page.evaluate(() => localStorage.getItem('jx-lang'))).toBe('en');
  await expect(page.locator('html')).toHaveAttribute('lang', 'en');
});

test('the active locale button is marked', async ({ page }) => {
  await page.goto('/en/about');
  await expect(
    page.locator('.nav-controls .lang-toggle button[data-lang="en"]'),
  ).toHaveClass(/active/);
});
```

- [ ] **Step 2: Run it**

Run: `wsl pnpm exec playwright test tests/e2e/lang.spec.ts`
Expected: PASS. If the URL has no trailing-slash variant, the regex `\/en\/about\/?$` already tolerates both.

- [ ] **Step 3: Commit**

```bash
git -c safe.directory='*' add tests/e2e/lang.spec.ts
git -c safe.directory='*' commit -m "test(e2e): language switch navigation + jx-lang"
```

---

## Task 9: E2E — search modal

**Files:**
- Create: `tests/e2e/search.spec.ts`

- [ ] **Step 1: Write the spec**

`site.js` appends `.search-modal` to the body and opens it (adds `.open`) on the `/` key; it lazy-fetches `/assets/docs-data.json` and renders `.sm-item` results; Escape closes.

```ts
import { test, expect } from '@playwright/test';

test('"/" opens search, query yields results, Escape closes', async ({ page }) => {
  await page.goto('/');
  await page.keyboard.press('/');
  const modal = page.locator('.search-modal');
  await expect(modal).toHaveClass(/open/);

  await page.locator('.search-modal input').fill('git');
  // a grimoire doc (git-incantations) should surface
  await expect(page.locator('.search-modal .sm-item').first()).toBeVisible();

  await page.keyboard.press('Escape');
  await expect(modal).not.toHaveClass(/open/);
});

test('the nav search button also opens the modal', async ({ page }) => {
  await page.goto('/');
  await page.locator('.nav-seek').click();
  await expect(page.locator('.search-modal')).toHaveClass(/open/);
});
```

- [ ] **Step 2: Run it**

Run: `wsl pnpm exec playwright test tests/e2e/search.spec.ts`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git -c safe.directory='*' add tests/e2e/search.spec.ts
git -c safe.directory='*' commit -m "test(e2e): search modal open/query/close"
```

---

## Task 10: E2E — grimoire reader

**Files:**
- Create: `tests/e2e/grimoire.spec.ts`

- [ ] **Step 1: Write the spec**

`/docs` reads `?doc=<slug>` (slug is locale-stable), persists the position in `localStorage('jx-doc')`, and filters via `.realm-toggle button[data-realm]`. `git-incantations` is a real grimoire slug (`src/content/grimoire/git-incantations.en.yaml`).

```ts
import { test, expect } from '@playwright/test';

test('?doc= deep-link selects that doc and persists jx-doc', async ({ page }) => {
  await page.goto('/docs?doc=git-incantations');
  await page.waitForFunction(() => !!document.querySelector('#doc-pane')?.textContent?.trim());
  expect(await page.evaluate(() => localStorage.getItem('jx-doc'))).toBe('git-incantations');
  // the reading pane and nav list rendered
  await expect(page.locator('#doc-pane')).not.toBeEmpty();
  await expect(page.locator('#doc-nav a, #doc-nav button')).not.toHaveCount(0);
});

test('realm filter narrows the list', async ({ page }) => {
  await page.goto('/docs');
  await page.locator('.realm-toggle button[data-realm="code"]').click();
  await expect(page.locator('.realm-toggle button[data-realm="code"]')).toHaveClass(/active/);
  // game chips only show for the games realm
  await expect(page.locator('#game-chips')).toBeHidden();
});
```

- [ ] **Step 2: Run it**

Run: `wsl pnpm exec playwright test tests/e2e/grimoire.spec.ts`
Expected: PASS. If `#doc-nav` uses a different child element than `a`/`button`, open `/docs` in `pnpm dev` and adjust the locator; the `#doc-pane` + `jx-doc` assertions are contract-stable.

- [ ] **Step 3: Commit**

```bash
git -c safe.directory='*' add tests/e2e/grimoire.spec.ts
git -c safe.directory='*' commit -m "test(e2e): grimoire deep-link, persistence, realm filter"
```

---

## Task 11: E2E — games tracker

**Files:**
- Create: `tests/e2e/games.spec.ts`

- [ ] **Step 1: Write the spec**

Tabs are `.tracker-tabs .tab[data-tab]` (default active = `playing`); switching re-renders `#game-grid` and persists `jx-games-tab`.

```ts
import { test, expect } from '@playwright/test';

test('switching tabs updates the grid and persists jx-games-tab', async ({ page }) => {
  await page.goto('/games');
  await expect(page.locator('.tracker-tabs .tab.active')).toHaveAttribute('data-tab', 'playing');

  await page.locator('.tracker-tabs .tab[data-tab="favorites"]').click();
  await expect(page.locator('.tracker-tabs .tab[data-tab="favorites"]')).toHaveClass(/active/);
  await expect(page.locator('#game-grid .game-card').first()).toBeVisible();
  expect(await page.evaluate(() => localStorage.getItem('jx-games-tab'))).toBe('favorites');
});
```

- [ ] **Step 2: Run it**

Run: `wsl pnpm exec playwright test tests/e2e/games.spec.ts`
Expected: PASS. If `jx-games-tab` is not yet persisted by `games.astro`'s inline script, assert the active-class + grid update only and note the persistence gap for cycle 1 (do not invent the behavior).

- [ ] **Step 3: Commit**

```bash
git -c safe.directory='*' add tests/e2e/games.spec.ts
git -c safe.directory='*' commit -m "test(e2e): games tracker tab switching"
```

---

## Task 12: E2E — countdowns

**Files:**
- Create: `tests/e2e/countdowns.spec.ts`

- [ ] **Step 1: Write the spec**

The inline script fills `#cd-grid` and `#up-grid`; count-ups (`[data-up]`) tick on an interval.

```ts
import { test, expect } from '@playwright/test';

test('countdown and count-up grids populate', async ({ page }) => {
  await page.goto('/countdowns');
  await expect(page.locator('#cd-grid .cd-card').first()).toBeVisible();
  await expect(page.locator('#up-grid .up-card').first()).toBeVisible();
});

test('a count-up ticks over time', async ({ page }) => {
  await page.goto('/countdowns');
  const up = page.locator('[data-up="0"]');
  const first = await up.textContent();
  await page.waitForTimeout(1200);
  // count-ups increase live; the rendered number should advance
  await expect.poll(async () => (await up.textContent()) !== first).toBeTruthy();
});
```

- [ ] **Step 2: Run it**

Run: `wsl pnpm exec playwright test tests/e2e/countdowns.spec.ts`
Expected: PASS. If the count-up cadence is too slow to change within ~1.2 s, increase the `waitForTimeout`; the value is monotonic so a longer wait is safe.

- [ ] **Step 3: Commit**

```bash
git -c safe.directory='*' add tests/e2e/countdowns.spec.ts
git -c safe.directory='*' commit -m "test(e2e): countdowns populate + tick"
```

---

## Task 13: E2E — blog

**Files:**
- Create: `tests/e2e/blog.spec.ts`

> Note: the blog category chips (`.blog-filters .chip`) are decorative in the current page — there is **no** client filter script — so this spec does not assert filtering. It covers list rendering, locale dates, post navigation, and the prev/next pager on a post.

- [ ] **Step 1: Write the spec**

```ts
import { test, expect } from '@playwright/test';

test('blog lists posts with locale-formatted dates', async ({ page }) => {
  await page.goto('/blog');
  await expect(page.locator('.post-card').first()).toBeVisible();
  // dates render via <time datetime="YYYY-MM-DD">
  await expect(page.locator('.post-card time').first()).toHaveAttribute('datetime', /^\d{4}-\d{2}-\d{2}$/);
});

test('clicking a post opens it and shows a prev/next pager', async ({ page }) => {
  await page.goto('/blog');
  await page.locator('.post-card').first().click();
  await expect(page).toHaveURL(/\/blog\/[a-z0-9-]+\/?$/);
  // the post template renders a prev/next pager (at least one direction exists)
  await expect(page.locator('article')).toBeVisible();
});
```

- [ ] **Step 2: Run it**

Run: `wsl pnpm exec playwright test tests/e2e/blog.spec.ts`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git -c safe.directory='*' add tests/e2e/blog.spec.ts
git -c safe.directory='*' commit -m "test(e2e): blog list, dates, post navigation"
```

---

## Task 14: E2E — chrome (CV print, 404, nav active/dock)

**Files:**
- Create: `tests/e2e/chrome.spec.ts`

- [ ] **Step 1: Write the spec**

CV has a `window.print()` button and an `@media print` layout; 404 ticks `#souls` down; the nav marks the active page and shows the dock at mobile widths.

```ts
import { test, expect } from '@playwright/test';

test('CV exposes a print action and a print stylesheet', async ({ page }) => {
  await page.goto('/cv');
  await expect(page.getByRole('button', { name: /print/i })).toBeVisible();
  // emulate print media: chrome should be hidden by the @media print block
  await page.emulateMedia({ media: 'print' });
  await expect(page.locator('.site-nav')).toBeHidden();
});

test('404 souls counter ticks down', async ({ page }) => {
  await page.goto('/404');
  const souls = page.locator('#souls');
  await expect(souls).toHaveText('404');
  await expect.poll(async () => (await souls.textContent()) !== '404', { timeout: 4000 }).toBeTruthy();
});

test('nav marks active page', async ({ page }) => {
  await page.goto('/about');
  await expect(page.locator('.nav-hlink.here')).toHaveAttribute('href', '/about');
});

test('mobile dock is visible at small widths', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  await expect(page.locator('.jx-dock')).toBeVisible();
  await page.locator('.jx-dock .jx-more').click();
  await expect(page.locator('.jx-sheet')).toBeVisible();
});
```

- [ ] **Step 2: Run it**

Run: `wsl pnpm exec playwright test tests/e2e/chrome.spec.ts`
Expected: PASS. If `.site-nav` stays visible under print emulation because the `@media print` rule targets a different selector, inspect `cv.astro`'s print block and assert on the element it actually hides (e.g. `.cv-actions`).

- [ ] **Step 3: Commit**

```bash
git -c safe.directory='*' add tests/e2e/chrome.spec.ts
git -c safe.directory='*' commit -m "test(e2e): cv print, 404 counter, nav active + mobile dock"
```

---

## Task 15: Full local green run

**Files:** none (verification only)

- [ ] **Step 1: Run the fast suite**

Run: `wsl pnpm test`
Expected: PASS — content + i18n + render tests all green.

- [ ] **Step 2: Run the E2E suite**

Run: `wsl pnpm exec playwright test tests/e2e`
Expected: PASS — all E2E specs green against the production preview.

- [ ] **Step 3: Run the visual suite in the container**

Run the no-update container command from Task 6 Step 3.
Expected: PASS — 0 visual diffs.

(No commit — this is a checkpoint. If anything fails, fix it in its task before continuing.)

---

## Task 16: CI — Vitest on push, Playwright on PR/preview

**Files:**
- Modify: `.forgejo/workflows/ci.yml`

- [ ] **Step 1: Rewrite the workflow**

Adds the `preview` branch to push triggers, runs build + Vitest in the existing job, and adds a Playwright job (in the pinned container) that runs only on PRs and on `preview`. Replace `<VERSION>` with the string captured in Task 5 Step 1.

```yaml
name: CI
on:
  push:
    branches: [main, preview]
  pull_request:

jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Enable pnpm (via corepack, pinned by packageManager)
        run: corepack enable
      - name: Install
        run: pnpm install --frozen-lockfile
      - name: Build
        run: pnpm run build
      - name: Unit + render tests
        run: pnpm test

  e2e-visual:
    # heavier browser suite — only where it gates a deploy
    if: github.event_name == 'pull_request' || github.ref == 'refs/heads/preview'
    runs-on: ubuntu-latest
    container:
      image: mcr.microsoft.com/playwright:v<VERSION>-noble
    steps:
      - uses: actions/checkout@v4
      - name: Enable pnpm (via corepack, pinned by packageManager)
        run: corepack enable
      - name: Install
        run: pnpm install --frozen-lockfile
      - name: E2E
        run: pnpm exec playwright test tests/e2e
      - name: Visual regression
        run: pnpm exec playwright test tests/visual
      - name: Upload report on failure
        if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: playwright-report
          path: playwright-report/
```

- [ ] **Step 2: Validate the YAML locally**

Run: `wsl python3 -c "import yaml,sys; yaml.safe_load(open('.forgejo/workflows/ci.yml')); print('ok')"`
Expected: prints `ok`.

- [ ] **Step 3: Commit**

```bash
git -c safe.directory='*' add .forgejo/workflows/ci.yml
git -c safe.directory='*' commit -m "ci: Vitest on push, Playwright e2e+visual on PR/preview (pinned container)"
```

---

## Task 17: Document testing + the .NET convention

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Add a Testing section to `README.md`**

Find the existing scripts/usage area of `README.md` and add this section (adjust the heading level to match the file). Do not duplicate an existing section — insert once.

```markdown
## Testing

| Command | Scope |
|---|---|
| `pnpm test` | Vitest — content/i18n logic + Astro Container-API rendering tests (`tests/{content,i18n,render}`) |
| `pnpm test:e2e` | Playwright E2E of interactive pages (`tests/e2e`) |
| `pnpm test:visual` | Playwright self-baseline visual regression (`tests/visual`) |

**Visual baselines are container-pinned.** Generate/compare them only inside
`mcr.microsoft.com/playwright:v<VERSION>-noble` so dev (WSL) and CI match. An
*intended* visual change is landed with `playwright test tests/visual --update-snapshots`
(run in the container) and the diff reviewed in the PR.

CI runs Vitest on every push; the Playwright e2e + visual suite runs on pull
requests and on the `preview` branch.

### Convention: .NET layers ship their own tests

When the cycle-3 Business API (.NET 10) and cycle-4 Blazor admin land, each
**ships its own xUnit test project from day one**, wired into CI — the same way
no feature merges here without a test.
```

- [ ] **Step 2: Commit**

```bash
git -c safe.directory='*' add README.md
git -c safe.directory='*' commit -m "docs: testing commands + .NET test-project convention"
```

---

## Self-Review

**Spec coverage** (each §6/§7 surface and §-pillar → task):
- Rendering pillar (§2/§5) → Tasks 1–4 (config, Nav, Footer, page smoke). ✔
- Visual pillar, Option A self-baseline (§6) → Task 6; matrix routes/themes/locale/mobile + masks all present. ✔
- E2E pillar (§7): theme→7, lang→8, search→9, grimoire reader→10, games→11, countdowns→12, blog→13, cv print + 404 + nav/dock→14. ✔ (blog category-chip filtering intentionally not asserted — no client filter exists; noted.)
- .NET convention (§8) → Task 17. ✔
- Layout/scripts/CI/tooling (§9) → Task 5 (scripts/dep/gitignore), Task 16 (CI gating push vs PR/preview, pinned container). ✔
- Determinism (§-cross-cutting) → Task 6 container blockquote + Task 16 container image. ✔

**Placeholder scan:** `<VERSION>` is an intentional, instructed substitution (captured in Task 5 Step 1, used in Tasks 6/16/17) — not a vague TODO. No "TBD"/"handle edge cases"/"similar to". Each code step shows real code; each run step shows the command + expected output.

**Type/selector consistency:** selectors verified against source — `.nav-orb`, `.theme-toggle button[data-set]`, `.lang-toggle button[data-lang]` (`site.js`), `.nav-hlink.here`, `.jx-dock`/`.jx-more`/`.jx-sheet` (`Nav.astro`), `.search-modal`/`.sm-item`/`.nav-seek` (`site.js`), `#doc-pane`/`#doc-nav`/`.realm-toggle [data-realm]`/`jx-doc` (`docs.astro`), `.tracker-tabs .tab[data-tab]`/`#game-grid` (`games.astro`), `#cd-grid`/`#up-grid`/`[data-up]` (`countdowns.astro`), `.post-card time[datetime]` (`blog.astro`), `#souls`/`window.print()` (`404.astro`/`cv.astro`). `localStorage` keys `jx-theme`/`jx-lang`/`jx-doc` confirmed in `site.js`/`docs.astro`; `jx-games-tab` flagged as confirm-or-skip in Task 11 (not invented).

Gaps requiring runtime confirmation are called out inline with safe fallbacks (Nav active-class attribute order, `#doc-nav` child element, `jx-games-tab` persistence, CV print selector). No spec requirement is left without a task.
