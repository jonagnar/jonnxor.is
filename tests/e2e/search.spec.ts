import { test, expect } from '@playwright/test';

test('"/" opens search, query yields results, Escape closes', async ({ page }) => {
  await page.goto('/');
  await page.keyboard.press('/');
  const modal = page.locator('.search-modal');
  await expect(modal).toHaveClass(/open/);
  await page.locator('.search-modal input').fill('git');
  await expect(page.locator('.search-modal .sm-item').first()).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(modal).not.toHaveClass(/open/);
});

test('the nav search button also opens the modal', async ({ page }) => {
  // The .nav-seek pill is the laptop-tier (881–1279px) search control; display:none outside it.
  await page.setViewportSize({ width: 1100, height: 800 });
  await page.goto('/');
  await page.locator('.nav-seek').click();
  await expect(page.locator('.search-modal')).toHaveClass(/open/);
});
