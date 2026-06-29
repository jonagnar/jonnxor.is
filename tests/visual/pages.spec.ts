import { test, expect, type Page } from '@playwright/test';

const ROUTES = [
  '/', '/about', '/cv', '/portfolio', '/blog',
  '/blog/what-sekiro-taught-me-about-code-review',
  '/docs', '/games', '/countdowns', '/wallpapers', '/404',
];

// regions that legitimately change between runs — never part of the design diff
async function masks(page: Page) {
  return [
    page.locator('#souls'),        // 404 souls counter
    page.locator('.up-num'),       // countdowns count-ups
    page.locator('.cd-clock'),     // countdowns clocks
    page.locator('.post-card time'), // locale dates
  ];
}

async function setTheme(page: Page, theme: string) {
  await page.addInitScript((t) => localStorage.setItem('jx-theme', t), theme);
}

// Wait for JS-populated grids so layout is stable before screenshotting
async function settle(page: Page) {
  // countdowns page: wait for the grid to be populated by inline script
  const cdGrid = page.locator('#cd-grid .cd-card');
  const upGrid = page.locator('#up-grid .up-card');
  if (await cdGrid.count() === 0 && await upGrid.count() === 0) {
    const hasCdGrid = await page.locator('#cd-grid').count() > 0;
    if (hasCdGrid) {
      await page.waitForSelector('#cd-grid .cd-card', { timeout: 5000 }).catch(() => {});
    }
  }
}

test.describe('visual — all routes @ rune/is', () => {
  for (const route of ROUTES) {
    test(`route ${route}`, async ({ page }) => {
      await setTheme(page, 'rune');
      await page.goto(route);
      await page.waitForLoadState('networkidle');
      await settle(page);
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
        await settle(page);
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
      await settle(page);
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
      await settle(page);
      await expect(page).toHaveScreenshot(`mobile${route === '/' ? '/home' : route}.png`, {
        fullPage: true,
        mask: await masks(page),
      });
    });
  }
});
