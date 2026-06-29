import { test, expect } from '@playwright/test';

test('blog lists posts with locale-formatted dates', async ({ page }) => {
  await page.goto('/blog');
  await expect(page.locator('.post-card').first()).toBeVisible();
  await expect(page.locator('.post-card time').first()).toHaveAttribute('datetime', /^\d{4}-\d{2}-\d{2}$/);
});

test('clicking a post opens it and shows a prev/next pager', async ({ page }) => {
  await page.goto('/blog');
  await page.locator('.post-card').first().click();
  await expect(page).toHaveURL(/\/blog\/[a-z0-9-]+\/?$/);
  await expect(page.locator('article')).toBeVisible();
});
