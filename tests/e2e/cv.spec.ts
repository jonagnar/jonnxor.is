import { test, expect } from '@playwright/test';

test('cv page prints clean: chrome hidden under print media', async ({ page }) => {
  await page.goto('/cv');
  const printButton = page.locator('.cv-actions button.btn-gold');
  await expect(printButton).toBeVisible();
  await expect(printButton).toHaveText('Print / Save as PDF');

  await page.emulateMedia({ media: 'print' });
  await expect(page.locator('.site-nav')).toBeHidden();
  await expect(page.locator('.site-footer')).toBeHidden();
  await expect(printButton).toBeHidden();
  // .page-head .lede and h1/kicker are display:none under print, but the
  // section itself stays (padding/background reset instead of hidden).
  await expect(page.locator('.page-head .lede')).toBeHidden();
  await expect(page.locator('.page-head h1')).toBeHidden();
  await expect(page.locator('.page-head .kicker')).toBeHidden();
  // The sheet itself stays visible — it's the print output.
  await expect(page.locator('.cv-sheet')).toBeVisible();
});
