import { readdir, writeFile, unlink } from 'node:fs/promises';
import { join } from 'node:path';
import { readItems } from '@directus/sdk';
import { connect, done } from './lib/directus-client.mjs';
import { serializePost } from './lib/post-markdown.mjs';

const BLOG_DIR = 'src/content/blog';
const client = await connect();

const posts = await client.request(readItems('blog', {
  limit: -1,
  fields: ['slug', 'date', 'category', 'draft', { translations: ['languages_code', 'title', 'excerpt', 'body', 'read_time'] }],
}));

const wanted = new Set();
for (const post of posts) {
  for (const t of post.translations ?? []) {
    const file = `${post.slug}.${t.languages_code}.md`;
    wanted.add(file);
    const out = serializePost({
      locale: t.languages_code, slug: post.slug,
      date: typeof post.date === 'string' ? post.date.slice(0, 10) : post.date,
      category: post.category, draft: post.draft,
      title: t.title, excerpt: t.excerpt, readTime: t.read_time, body: t.body ?? '',
    });
    await writeFile(join(BLOG_DIR, file), out, 'utf8');
  }
}

// prune: remove generated locale files no longer backed by Directus, and the
// original un-suffixed *.md (now superseded by <slug>.en.md)
for (const ent of await readdir(BLOG_DIR, { withFileTypes: true })) {
  // Skip non-files (e.g. a future subdir whose name ends in .md) so unlink
  // can't throw EISDIR and abort the prune mid-run.
  if (!ent.isFile()) continue;
  const f = ent.name;
  const isLocaleFile = /\.(is|en|ja)\.md$/.test(f);
  // isLegacy: any un-suffixed *.md is deleted on purpose. The only valid files
  // here are <slug>.<locale>.md; an un-suffixed .md would fail Astro's
  // locale-required schema anyway, so pruning it keeps the snapshot valid.
  const isLegacy = f.endsWith('.md') && !isLocaleFile;
  if ((isLocaleFile && !wanted.has(f)) || isLegacy) {
    await unlink(join(BLOG_DIR, f));
    console.log('removed:', f);
  }
}
console.log(`pulled — ${wanted.size} locale file(s) from ${posts.length} post(s)`);
done();
