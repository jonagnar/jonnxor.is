// @ts-check
import { defineConfig } from 'astro/config';

// https://astro.build/config
export default defineConfig({
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
