import { test, expect } from '@playwright/test';

test('switching tabs updates the grid and persists jx-games-tab', async ({ page }) => {
  await page.goto('/games');
  await expect(page.locator('.tracker-tabs .tab.active')).toHaveAttribute('data-tab', 'playing');
  await expect(page.locator('#game-grid .game-card[data-tab="playing"]').first()).toBeVisible();
  await page.locator('.tracker-tabs .tab[data-tab="favorites"]').click();
  await expect(page.locator('.tracker-tabs .tab[data-tab="favorites"]')).toHaveClass(/active/);
  await expect(page.locator('#game-grid .game-card[data-tab="favorites"]').first()).toBeVisible();
  await expect(page.locator('#game-grid .game-card[data-tab="playing"]').first()).toBeHidden();
  expect(await page.evaluate(() => localStorage.getItem('jx-games-tab'))).toBe('favorites');
});
