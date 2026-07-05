/// <reference types="vitest/config" />
import { getViteConfig } from 'astro/config';

export default getViteConfig(
  {
    test: {
      include: ['../tests/**/*.test.ts'],
    },
  },
  // devToolbar off for test runs only: with it on, the compiler injects
  // data-astro-source-* debug attributes into every element rendered via the
  // Container API, breaking exact-tag assertions like `toContain('<h1>…</h1>')`.
  // `astro dev` keeps the toolbar (astro.config.mjs stays untouched).
  { devToolbar: { enabled: false } },
);
