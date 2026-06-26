import { defineCollection, z } from 'astro:content';
import { glob } from 'astro/loaders';

// The Codex — long-form posts. Drop a Markdown file in src/content/blog/ and it
// appears on /blog and at /blog/<filename>. The frontmatter below is validated
// at build time, so a typo in a date or a missing title fails the build, loudly.
const blog = defineCollection({
  loader: glob({ pattern: '**/*.md', base: './src/content/blog' }),
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

// The Grimoire — reference docs. YAML data (not Markdown) because each body is
// rich hand-authored HTML (tables, .tip callouts, hand-coloured code) that the
// client-side reader injects as-is. One file per doc, validated at build time.
const grimoire = defineCollection({
  loader: glob({ pattern: '**/*.yaml', base: './src/content/grimoire' }),
  schema: z.object({
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

export const collections = { blog, grimoire };
