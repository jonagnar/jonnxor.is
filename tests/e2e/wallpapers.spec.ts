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
  await expect(page.locator('#lightbox')).toHaveAttribute('aria-label', 'Wallpaper preview');
  await page.keyboard.press('Escape');
  await expect(page.locator('#lightbox')).not.toHaveClass(/open/);
  await expect(firstTile).toBeFocused();
});

test('lightbox traps Tab focus while open and never leaks to the page', async ({ page }) => {
  await page.goto('/wallpapers');
  await page.locator('#masonry .wall-tile').first().click();
  await expect(page.locator('#lightbox')).toHaveClass(/open/);

  // Focus starts on Close. Tab wraps forward to Download.
  await expect(page.locator('#lb-close')).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(page.locator('#lb-download')).toBeFocused();

  // Tab again wraps forward back to Close.
  await page.keyboard.press('Tab');
  await expect(page.locator('#lb-close')).toBeFocused();

  // Shift+Tab reverses: from Close back to Download.
  await page.keyboard.press('Shift+Tab');
  await expect(page.locator('#lb-download')).toBeFocused();

  // After a few more Tabs, focus is still inside the lightbox — never escapes to the page.
  await page.keyboard.press('Tab');
  await page.keyboard.press('Tab');
  await page.keyboard.press('Tab');
  const stillInside = await page.evaluate(() =>
    document.getElementById('lightbox')?.contains(document.activeElement) ?? false
  );
  expect(stillInside).toBe(true);

  // Clicking the artwork drops focus to <body>; the next Tab must recapture it
  // inside the lightbox (first focusable = Download), not leak to the page behind.
  await page.locator('#lb-art').click();
  await page.keyboard.press('Tab');
  await expect(page.locator('#lb-download')).toBeFocused();
});
