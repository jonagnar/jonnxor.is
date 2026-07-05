import { test, expect } from '@playwright/test';

test('countdown and count-up grids populate', async ({ page }) => {
  await page.goto('/countdowns');
  await expect(page.locator('#cd-grid .cd-card').first()).toBeVisible();
  await expect(page.locator('#up-grid .up-card').first()).toBeVisible();
});

test('a count-up ticks over time', async ({ page }) => {
  // The count-up renders 4 decimal places of days-lived; at the slowest rate the 4th
  // decimal only advances every ~28s, so the observable tick window must be generous.
  test.setTimeout(60000);
  await page.goto('/countdowns');
  const up = page.locator('[data-up="0"]');
  const first = await up.textContent();
  await expect
    .poll(async () => (await up.textContent()) !== first, { timeout: 45000 })
    .toBeTruthy();
});
