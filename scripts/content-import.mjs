// One-time seed: import existing un-suffixed blog (.md) and grimoire (.yaml) files into Directus as the en base.
import { readdir, readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { readItems, createItem } from '@directus/sdk';
import { connect, done } from './lib/directus-client.mjs';
import { parsePost } from './lib/post-markdown.mjs';
import { parseDoc } from './lib/grimoire-yaml.mjs';

const BLOG_DIR = 'src/content/blog';
const client = await connect();

const existing = new Set(
  (await client.request(readItems('blog', { fields: ['slug'], limit: -1 }))).map((p) => p.slug),
);

const files = (await readdir(BLOG_DIR)).filter((f) => f.endsWith('.md') && !/\.(is|en|ja)\.md$/.test(f));
let created = 0;
for (const file of files) {
  const slug = file.replace(/\.md$/, '');
  if (existing.has(slug)) { console.log('skip (exists):', slug); continue; }
  const { data, body } = parsePost(await readFile(join(BLOG_DIR, file), 'utf8'));
  await client.request(createItem('blog', {
    slug,
    date: new Date(data.date).toISOString(),
    category: data.category,
    draft: data.draft ?? false,
    translations: [{
      languages_code: 'en',
      title: data.title,
      excerpt: data.excerpt,
      read_time: data.readTime,
      body,
    }],
  }));
  created++;
  console.log('imported:', slug);
}
console.log(`done — ${created} created, ${existing.size} pre-existing`);

// --- grimoire: seed the existing un-suffixed YAML docs as the en base ---
const GRIMOIRE_DIR = 'src/content/grimoire';
const existingDocs = new Set(
  (await client.request(readItems('grimoire', { fields: ['slug'], limit: -1 }))).map((d) => d.slug),
);
const docFiles = (await readdir(GRIMOIRE_DIR)).filter((f) => f.endsWith('.yaml') && !/\.(is|en|ja)\.yaml$/.test(f));
let docsCreated = 0;
for (const file of docFiles) {
  const slug = file.replace(/\.yaml$/, '');
  if (existingDocs.has(slug)) { console.log('skip grimoire (exists):', slug); continue; }
  const d = parseDoc(await readFile(join(GRIMOIRE_DIR, file), 'utf8'));
  await client.request(createItem('grimoire', {
    slug,
    order: d.order,
    realm: d.realm,
    game: d.game ?? null,
    updated: d.updated,
    translations: [{
      languages_code: 'en',
      title: d.title,
      cat: d.cat,
      tags: d.tags ?? [],
      body: d.body,
    }],
  }));
  docsCreated++;
  console.log('imported grimoire:', slug);
}
console.log(`grimoire — ${docsCreated} created, ${existingDocs.size} pre-existing`);
done();
