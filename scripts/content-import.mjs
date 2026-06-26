import { readdir, readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { createDirectus, rest, authentication, readItems, createItem } from '@directus/sdk';
import { parsePost } from './lib/post-markdown.mjs';

const BLOG_DIR = 'src/content/blog';
const client = createDirectus(process.env.DIRECTUS_URL).with(rest()).with(authentication());
await client.login({ email: process.env.ADMIN_EMAIL, password: process.env.ADMIN_PASSWORD });

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
