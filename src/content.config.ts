import { defineCollection, z } from 'astro:content';
import { glob } from 'astro/loaders';
// localeEntryId gives each locale file a distinct collection id; both loaders below
// MUST keep `generateId: localeEntryId` or a slug's locale files collide on one id
// (guarded by tests/content/config-wiring.test.ts).
import { localeEntryId } from './content/loaders';
import { sectionSchemas } from './content/page-sections';

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
    gradient: z.array(z.string().regex(/^#[0-9a-fA-F]{6}$/)).length(3),
    initials: z.string(),
    favorite: z.boolean().default(false),
    title: z.string(),
    sub: z.string(),
  }),
});

// Page prose — heads (kicker/title/lede) + per-page structured `sections`.
// Section shapes are validated per page slug via the sectionSchemas lookup
// (src/content/page-sections.ts) as pages are ported (design §3).
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
  }).superRefine((p, ctx) => {
    const shape = sectionSchemas[p.slug as keyof typeof sectionSchemas];
    if (shape) {
      const r = shape.safeParse(p.sections);
      if (!r.success) {
        ctx.addIssue({ code: 'custom', message: `pages/${p.slug} sections invalid: ${z.prettifyError(r.error)}` });
      }
    }
  }),
});

// The Reckoning — countdowns and count-ups, discriminated by `kind`.
const countdowns = defineCollection({
  loader: glob({
    pattern: '**/*.yaml',
    base: './src/content/countdowns',
    generateId: localeEntryId,
  }),
  schema: z.object({
    slug: z.string(),
    locale: z.enum(['is', 'en', 'ja']),
    order: z.number(),
    kind: z.enum(['countdown', 'countup']),
    when: z.string().regex(/^\d{4}-\d{2}-\d{2}$/).optional(),
    gold: z.boolean().default(false),
    icon: z.string().optional(),
    start: z.string().regex(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}$/).optional(),
    rate: z.number().positive().max(24).optional(),
    what: z.string(),
    note: z.string(),
  }).superRefine((c, ctx) => {
    // Kind coherence: the data layer can't enforce which nullable fields belong
    // to which kind — the build is the only gate under the snapshot seam.
    if (c.kind === 'countup') {
      for (const k of ['icon', 'start', 'rate'] as const) {
        if (c[k] === undefined) ctx.addIssue({ code: 'custom', path: [k], message: `countup '${c.slug}' requires ${k}` });
      }
      if (c.when !== undefined) ctx.addIssue({ code: 'custom', path: ['when'], message: `countup '${c.slug}' must not carry when` });
      if (c.gold) ctx.addIssue({ code: 'custom', path: ['gold'], message: `countup '${c.slug}' must not carry gold` });
    } else {
      for (const k of ['icon', 'start', 'rate'] as const) {
        if (c[k] !== undefined) ctx.addIssue({ code: 'custom', path: [k], message: `countdown '${c.slug}' must not carry ${k}` });
      }
    }
  }),
});

// The Hoard — generated gradient wallpapers.
const wallpapers = defineCollection({
  loader: glob({
    pattern: '**/*.yaml',
    base: './src/content/wallpapers',
    generateId: localeEntryId,
  }),
  schema: z.object({
    slug: z.string(),
    locale: z.enum(['is', 'en', 'ja']),
    order: z.number(),
    tag: z.string(),
    aspect_ratio: z.string().regex(/^\d+\s*\/\s*\d+$/), // raw CSS aspect-ratio fragment
    gradient: z.array(z.string().regex(/^#[0-9a-fA-F]{6}$/)).length(3),
    angle: z.number().int().min(0).max(360),
    title: z.string(),
  }),
});

// The Forge — portfolio project cards.
const projects = defineCollection({
  loader: glob({
    pattern: '**/*.yaml',
    base: './src/content/projects',
    generateId: localeEntryId,
  }),
  schema: z.object({
    slug: z.string(),
    locale: z.enum(['is', 'en', 'ja']),
    order: z.number(),
    sigil: z.string(),
    gradient: z.array(z.string().regex(/^#[0-9a-fA-F]{6}$/)).length(3),
    tech: z.array(z.string()),
    links: z.array(z.object({ label: z.string(), href: z.string() }).strict()),
    title: z.string(),
    description: z.string(),
    plate: z.string(),
  }),
});

export const collections = { blog, grimoire, games, pages, countdowns, wallpapers, projects };
