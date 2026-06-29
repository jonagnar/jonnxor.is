import { test, expect } from '@playwright/test';

test('CV exposes a print action and a print stylesheet', async ({ page }) => {
  await page.goto('/cv');
  await expect(page.getByRole('button', { name: /print/i })).toBeVisible();
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
  await expect(page.locator('.nav-hlink.here')).toHaveAttribute('href', /\/about\/?$/);
});

test('mobile dock is visible at small widths', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  await expect(page.locator('.jx-dock')).toBeVisible();
  await page.locator('.jx-dock .jx-more').click();
  await expect(page.locator('.jx-sheet')).toBeVisible();
});
