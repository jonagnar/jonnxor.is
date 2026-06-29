import { test, expect } from '@playwright/test';

test('?doc= deep-link selects that doc and renders the pane', async ({ page }) => {
  await page.goto('/docs?doc=git-incantations');
  await page.waitForFunction(() => !!document.querySelector('#doc-pane')?.textContent?.trim());
  // The deep-linked doc is the active nav item, confirming ?doc= drove selection.
  await expect(page.locator('#doc-nav .doc-item.active')).toHaveAttribute(
    'data-id',
    'git-incantations',
  );
  await expect(page.locator('#doc-pane')).not.toBeEmpty();
  await expect(page.locator('#doc-nav a, #doc-nav button')).not.toHaveCount(0);
});

test('selecting a doc persists jx-doc', async ({ page }) => {
  // jx-doc is written by goTo() on interaction (not on a fresh ?doc= deep-link load),
  // so persistence is exercised by clicking a nav item.
  await page.goto('/docs');
  const item = page.locator('#doc-nav .doc-item').first();
  const id = await item.getAttribute('data-id');
  await item.click();
  expect(await page.evaluate(() => localStorage.getItem('jx-doc'))).toBe(id);
});

test('realm filter narrows the list', async ({ page }) => {
  await page.goto('/docs');
  await page.locator('.realm-toggle button[data-realm="code"]').click();
  await expect(page.locator('.realm-toggle button[data-realm="code"]')).toHaveClass(/active/);
  await expect(page.locator('#game-chips')).toBeHidden();
});
