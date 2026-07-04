import { defineCollection, z } from 'astro:content';
import { glob } from 'astro/loaders';
// localeEntryId gives each locale file a distinct collection id; both loaders below
// MUST keep `generateId: localeEntryId` or a slug's locale files collide on one id
// (guarded by tests/content/config-wiring.test.ts).
import { localeEntryId } from './content/loaders';

// The Codex — long-form posts. Drop a Markdown file in src/content/blog/ and it
// appears on /blog and at /blog/<filename>. The frontmatter below is validated
// at build time, so a typo in a date or a missing title fails the build, loudly.
const blog = defineCollection({
  // Each post exists as one file per locale: `<slug>.<locale>.md`. The glob
  // loader's default generateId returns the frontmatter `slug` verbatim, which
  // is identical across a post's locale files — so they'd collide on the same
  // collection id and silently overwrite each other (the en base would vanish).
  // Derive the id from the filename instead (which keeps the locale suffix), so
  // `<slug>.en` and `<slug>.is` are distinct entries.
  loader: glob({
    pattern: '**/*.md',
    base: './src/content/blog',
    generateId: localeEntryId,
  }),
  schema: z.object({
    title: z.string(),
    date: z.coerce.date(),
    category: z.string(), // "Engineering" | "Games × Code" | "Myth" | "Chai & life" | …
    excerpt: z.string(),
    readTime: z.string(), // e.g. "9 min"
    draft: z.boolean().default(false),
    slug: z.string(),
    locale: z.enum(['is', 'en', 'ja']),
  }),
});

// The Grimoire — reference docs, one file per locale: `<slug>.<locale>.yaml`.
// YAML (not Markdown) because each body is rich hand-authored HTML the client-side
// reader injects as-is. Locale-keyed like blog; `localeEntryId` keeps a slug's
// locale files from colliding on one id. `docs.astro` remaps id back to slug so
// deep-links/localStorage stay locale-stable.
const grimoire = defineCollection({
  loader: glob({
    pattern: '**/*.yaml',
    base: './src/content/grimoire',
    generateId: localeEntryId,
  }),
  schema: z.object({
    slug: z.string(),
    locale: z.enum(['is', 'en', 'ja']),
    order: z.number(),
    realm: z.enum(['games', 'code', 'life']),
    game: z.string().optional(),
    cat: z.string(),
    title: z.string(),
    tags: z.array(z.string()),
    updated: z.string(),
    body: z.string(),
  }),
});

// The Game Hall — tracker entries, one file per locale: `<slug>.<locale>.yaml`.
const games = defineCollection({
  loader: glob({
    pattern: '**/*.yaml',
    base: './src/content/games',
    generateId: localeEntryId,
  }),
  schema: z.object({
    slug: z.string(),
    locale: z.enum(['is', 'en', 'ja']),
    order: z.number(),
    tab: z.enum(['upcoming', 'playing', 'played', 'favorites']),
    date: z.string().regex(/^\d{4}-\d{2}-\d{2}$/).optional(), // "YYYY-MM-DD"; absent = TBA
    platforms: z.array(z.string()),
    gradient: z.array(z.string()).length(3),
    initials: z.string(),
    favorite: z.boolean().default(false),
    title: z.string(),
    sub: z.string(),
  }),
});

// Page prose — heads (kicker/title/lede) + per-page structured `sections`.
// Section shapes are validated per page slug as pages are ported (superRefine
// arrives with the first sections consumer in the countdowns slice).
const pages = defineCollection({
  loader: glob({
    pattern: '**/*.yaml',
    base: './src/content/pages',
    generateId: localeEntryId,
  }),
  schema: z.object({
    slug: z.string(),
    locale: z.enum(['is', 'en', 'ja']),
    kicker: z.string(),
    title: z.string(),
    lede: z.string(),
    sections: z.record(z.string(), z.unknown()).optional(),
  }),
});

export const collections = { blog, grimoire, games, pages };
