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
  }),
});

export const collections = { blog };
