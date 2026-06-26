// Rehydrate Directus blog content FROM the committed per-locale snapshot —
// the inverse of content:pull, and the content-side companion to the schema
// snapshot (directus/schema/snapshot.yaml). Use it to repopulate a fresh/empty
// Directus (new clone, wiped DB, re-bootstrap) so the local authoring stack
// matches the repo without re-authoring.
//
// It reads every src/content/blog/<slug>.<locale>.md, groups the locale files
// by slug into one `blog` item + its `blog_translations`, and creates any post
// whose slug doesn't already exist. It is idempotent at the POST level (existing
// slugs are skipped) — it rehydrates missing posts, it does NOT reconcile
// per-translation diffs on posts that already exist. The normal authoring loop
// is author-in-Directus -> content:pull -> commit; restore is the reverse trip
// for standing the stack back up from the committed snapshot.
import { readdir, readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { createDirectus, rest, authentication, readItems, createItem } from '@directus/sdk';
import { parsePost } from './lib/post-markdown.mjs';

const BLOG_DIR = 'src/content/blog';
const client = createDirectus(process.env.DIRECTUS_URL).with(rest()).with(authentication());
await client.login({ email: process.env.ADMIN_EMAIL, password: process.env.ADMIN_PASSWORD });

// Group per-locale snapshot files by slug -> one blog item with N translations.
const files = (await readdir(BLOG_DIR)).filter((f) => /\.(is|en|ja)\.md$/.test(f));
const bySlug = new Map();
for (const file of files) {
  const { data, body } = parsePost(await readFile(join(BLOG_DIR, file), 'utf8'));
  // `slug` comes from frontmatter (serializePost always writes it; assumes it
  // matches the filename). Base fields (date/category/draft) are non-translatable
  // and content:pull writes them identically to every locale file of a post, so
  // the first locale file seen for a slug sets them and the rest only add a
  // translation.
  if (!bySlug.has(data.slug)) {
    bySlug.set(data.slug, {
      slug: data.slug,
      date: new Date(data.date).toISOString(),
      category: data.category,
      draft: data.draft ?? false,
      translations: [],
    });
  }
  bySlug.get(data.slug).translations.push({
    languages_code: data.locale,
    title: data.title,
    excerpt: data.excerpt,
    read_time: data.readTime,
    body,
  });
}

const existing = new Set(
  (await client.request(readItems('blog', { fields: ['slug'], limit: -1 }))).map((p) => p.slug),
);
let created = 0;
for (const [slug, item] of bySlug) {
  if (existing.has(slug)) { console.log('skip (exists):', slug); continue; }
  await client.request(createItem('blog', item));
  created++;
  console.log('restored:', slug, '(' + item.translations.map((t) => t.languages_code).join(', ') + ')');
}
console.log(`done — ${created} restored, ${bySlug.size - created} skipped, ${bySlug.size} post(s) in snapshot`);

// The authenticated SDK client keeps a token-refresh timer alive, which would
// otherwise hang the process after the work is done (see content-pull.mjs).
process.exit(0);
