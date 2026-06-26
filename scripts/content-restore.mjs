// Rehydrate Directus content (blog posts AND grimoire docs) FROM the committed
// per-locale snapshot — the inverse of content:pull, and the content-side
// companion to the schema snapshot (directus/schema/snapshot.yaml). Use it to
// repopulate a fresh/empty Directus (new clone, wiped DB, re-bootstrap) so the
// local authoring stack matches the repo without re-authoring.
//
// It reads every src/content/blog/<slug>.<locale>.md, groups the locale files
// by slug into one `blog` item + its `blog_translations`, and creates any post
// whose slug doesn't already exist. Grimoire docs are grouped the same way from
// src/content/grimoire/<slug>.<locale>.yaml into one `grimoire` item + its
// `grimoire_translations`. It is idempotent at the POST level (existing slugs
// are skipped) — it rehydrates missing items, it does NOT reconcile
// per-translation diffs on items that already exist. The normal authoring loop
// is author-in-Directus -> content:pull -> commit; restore is the reverse trip
// for standing the stack back up from the committed snapshot.
import { readdir, readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { readItems, createItem } from '@directus/sdk';
import { connect, done } from './lib/directus-client.mjs';
import { parsePost } from './lib/post-markdown.mjs';
import { parseDoc } from './lib/grimoire-yaml.mjs';

const BLOG_DIR = 'src/content/blog';
const client = await connect();

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

// --- grimoire: group <slug>.<locale>.yaml by slug -> one grimoire item + translations ---
const GRIMOIRE_DIR = 'src/content/grimoire';
const docFiles = (await readdir(GRIMOIRE_DIR)).filter((f) => /\.(is|en|ja)\.yaml$/.test(f));
const docsBySlug = new Map();
for (const file of docFiles) {
  const d = parseDoc(await readFile(join(GRIMOIRE_DIR, file), 'utf8'));
  if (!docsBySlug.has(d.slug)) {
    // Base fields (order/realm/game/updated) are non-translatable; content:pull
    // writes them identically to every locale file of a doc, so the first locale
    // file seen for a slug sets them and the rest only add a translation.
    docsBySlug.set(d.slug, {
      slug: d.slug,
      order: d.order,
      realm: d.realm,
      game: d.game ?? null,
      updated: d.updated,
      translations: [],
    });
  }
  docsBySlug.get(d.slug).translations.push({
    languages_code: d.locale,
    title: d.title,
    cat: d.cat,
    tags: d.tags ?? [],
    body: d.body,
  });
}

const existingDocs = new Set(
  (await client.request(readItems('grimoire', { fields: ['slug'], limit: -1 }))).map((d) => d.slug),
);
let docsCreated = 0;
for (const [slug, item] of docsBySlug) {
  if (existingDocs.has(slug)) { console.log('skip grimoire (exists):', slug); continue; }
  await client.request(createItem('grimoire', item));
  docsCreated++;
  console.log('restored grimoire:', slug, '(' + item.translations.map((t) => t.languages_code).join(', ') + ')');
}
console.log(`grimoire done — ${docsCreated} restored, ${docsBySlug.size - docsCreated} skipped, ${docsBySlug.size} doc(s) in snapshot`);
done();
