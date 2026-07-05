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
