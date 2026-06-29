import { test, expect } from '@playwright/test';

test('switching tabs updates the grid and persists jx-games-tab', async ({ page }) => {
  await page.goto('/games');
  await expect(page.locator('.tracker-tabs .tab.active')).toHaveAttribute('data-tab', 'playing');
  await page.locator('.tracker-tabs .tab[data-tab="favorites"]').click();
  await expect(page.locator('.tracker-tabs .tab[data-tab="favorites"]')).toHaveClass(/active/);
  await expect(page.locator('#game-grid .game-card').first()).toBeVisible();
  expect(await page.evaluate(() => localStorage.getItem('jx-games-tab'))).toBe('favorites');
});
