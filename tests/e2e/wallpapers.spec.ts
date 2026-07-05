import { test, expect } from '@playwright/test';

test('filters toggle tiles and aria-pressed', async ({ page }) => {
  await page.goto('/wallpapers');
  await expect(page.locator('#masonry .wall-tile')).toHaveCount(12);
  await expect(page.locator('#wall-filters')).toHaveAttribute('role', 'group');
  await expect(page.locator('#wall-filters .chip[data-tag="All"]')).toHaveAttribute('aria-pressed', 'true');
  await page.locator('#wall-filters .chip[data-tag="Dragons"]').click();
  await expect(page.locator('#wall-filters .chip[data-tag="Dragons"]')).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('#wall-filters .chip[data-tag="All"]')).toHaveAttribute('aria-pressed', 'false');
  await expect(page.locator('#masonry .wall-tile:not([hidden])')).toHaveCount(3);
});

test('lightbox opens, traps focus start, and closes with focus restoration', async ({ page }) => {
  await page.goto('/wallpapers');
  const firstTile = page.locator('#masonry .wall-tile').first();
  await firstTile.click();
  await expect(page.locator('#lightbox')).toHaveClass(/open/);
  await expect(page.locator('#lb-close')).toBeFocused();
  await page.keyboard.press('Escape');
  await expect(page.locator('#lightbox')).not.toHaveClass(/open/);
  await expect(firstTile).toBeFocused();
});
