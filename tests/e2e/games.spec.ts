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

test('tracker tabs expose filter-button semantics (aria-pressed toggles)', async ({ page }) => {
  await page.goto('/games');

  // The container is a labeled group of toggle buttons, not an ARIA tablist.
  const group = page.locator('.tracker-tabs');
  await expect(group).toHaveAttribute('role', 'group');
  await expect(group).toHaveAttribute('aria-label', 'Filter games by status');
  await expect(page.locator('.tracker-tabs [role="tab"]')).toHaveCount(0);

  // Default state: only the "playing" filter is pressed.
  await expect(page.locator('.tracker-tabs .tab[data-tab="playing"]')).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('.tracker-tabs .tab[aria-pressed="false"]')).toHaveCount(3);

  // Clicking flips aria-pressed alongside the visual .active class.
  await page.locator('.tracker-tabs .tab[data-tab="favorites"]').click();
  await expect(page.locator('.tracker-tabs .tab[data-tab="favorites"]')).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('.tracker-tabs .tab[data-tab="playing"]')).toHaveAttribute('aria-pressed', 'false');

  // aria-pressed survives the saved-tab restoration path on reload.
  await page.reload();
  await expect(page.locator('.tracker-tabs .tab[data-tab="favorites"]')).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('.tracker-tabs .tab[aria-pressed="false"]')).toHaveCount(3);
});

test('tracker tabs activate with the keyboard', async ({ page }) => {
  await page.goto('/games');
  const upcoming = page.locator('.tracker-tabs .tab[data-tab="upcoming"]');
  await upcoming.focus();
  await page.keyboard.press('Enter');
  await expect(upcoming).toHaveAttribute('aria-pressed', 'true');
  await expect(upcoming).toHaveClass(/active/);
  await expect(page.locator('#game-grid .game-card[data-tab="upcoming"]').first()).toBeVisible();
  await expect(page.locator('#game-grid .game-card[data-tab="playing"]').first()).toBeHidden();

  const played = page.locator('.tracker-tabs .tab[data-tab="played"]');
  await played.focus();
  await page.keyboard.press('Space');
  await expect(played).toHaveAttribute('aria-pressed', 'true');
  await expect(upcoming).toHaveAttribute('aria-pressed', 'false');
  await expect(page.locator('#game-grid .game-card[data-tab="played"]').first()).toBeVisible();
});
