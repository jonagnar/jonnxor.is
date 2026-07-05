import { test, expect } from '@playwright/test';

test('orb cycles themes and persists', async ({ page }) => {
  // The .nav-orb is the ultrawide-tier (≥1920px) theme control; it is display:none below that.
  await page.setViewportSize({ width: 1920, height: 1080 });
  await page.goto('/');
  const html = page.locator('html');
  const start = await html.getAttribute('data-theme');
  await page.locator('.nav-orb').click();
  const next = await html.getAttribute('data-theme');
  expect(next).not.toBe(start);
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
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dawn');
});
