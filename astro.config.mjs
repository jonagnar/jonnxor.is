// @ts-check
import { defineConfig } from 'astro/config';

// https://astro.build/config
export default defineConfig({
  // Off so the Container API (tests/render/**) emits plain markup — with it on,
  // the compiler injects data-astro-source-* debug attributes into every element,
  // which breaks exact-tag assertions like `toContain('<h1>...</h1>')`.
  devToolbar: { enabled: false },
  i18n: {
    locales: ['is', 'en', 'ja'],
    defaultLocale: 'is',
    routing: {
      prefixDefaultLocale: false,
      fallbackType: 'rewrite',
    },
    fallback: {
      en: 'is',
      ja: 'is',
    },
  },
  markdown: {
    // Render code blocks as plain (theme-aware) <pre><code> styled by our own CSS,
    // instead of Shiki's fixed palette which wouldn't adapt to the rune/light/dark
    // themes. TODO(foundation): swap in Expressive Code for theme-aware syntax colours.
    syntaxHighlight: false,
  },
});
