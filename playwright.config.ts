import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  // Playwright's default testMatch also matches *.test.ts — restrict to *.spec.ts
  // so it ignores the Vitest unit/render tests that live under tests/.
  testMatch: '**/*.spec.ts',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? 'github' : 'list',
  expect: {
    // small tolerance absorbs sub-pixel anti-aliasing in the pinned container
    toHaveScreenshot: { maxDiffPixelRatio: 0.01 },
  },
  use: {
    baseURL: 'http://localhost:4321',
    trace: 'on-first-retry',
    viewport: { width: 1280, height: 800 },
    deviceScaleFactor: 1,
    reducedMotion: 'reduce',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
  webServer: {
    command: 'corepack pnpm build && corepack pnpm preview --port 4321',
    url: 'http://localhost:4321',
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
});
