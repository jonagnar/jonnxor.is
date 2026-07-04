// One descriptor per snapshot collection: how content:pull flattens a Directus
// item+translation into a per-locale file record, and how content:restore
// groups records back into a Directus create payload. Blog/grimoire wrap their
// pre-existing serializers so the committed snapshot stays byte-identical.
import { parsePost, serializePost } from './post-markdown.mjs';
import { parseDoc, serializeDoc } from './grimoire-yaml.mjs';

export const COLLECTIONS = [
  {
    name: 'blog',
    dir: 'src/content/blog',
    ext: '.md',
    fileRe: /\.(is|en|ja)\.md$/,
    fields: ['slug', 'date', 'category', 'draft', { translations: ['languages_code', 'title', 'excerpt', 'body', 'read_time'] }],
    toRecord: (p, t) => ({
      locale: t.languages_code, slug: p.slug,
      date: typeof p.date === 'string' ? p.date.slice(0, 10) : p.date,
      category: p.category, draft: p.draft,
      title: t.title, excerpt: t.excerpt, readTime: t.read_time, body: t.body ?? '',
    }),
    toItem: (r) => ({ slug: r.slug, date: new Date(r.date).toISOString(), category: r.category, draft: r.draft ?? false }),
    toTranslation: (r) => ({ languages_code: r.locale, title: r.title, excerpt: r.excerpt, read_time: r.readTime, body: r.body }),
    serialize: serializePost,
    parse: (raw) => { const { data, body } = parsePost(raw); return { ...data, body }; },
  },
  {
    name: 'grimoire',
    dir: 'src/content/grimoire',
    ext: '.yaml',
    fileRe: /\.(is|en|ja)\.yaml$/,
    fields: ['slug', 'order', 'realm', 'game', 'updated', { translations: ['languages_code', 'title', 'cat', 'tags', 'body'] }],
    toRecord: (d, t) => ({
      slug: d.slug, locale: t.languages_code,
      order: d.order, realm: d.realm, game: d.game ?? undefined,
      cat: t.cat, title: t.title, tags: t.tags ?? [],
      updated: typeof d.updated === 'string' ? d.updated.slice(0, 10) : d.updated,
      body: t.body ?? '',
    }),
    toItem: (r) => ({ slug: r.slug, order: r.order, realm: r.realm, game: r.game ?? null, updated: r.updated }),
    toTranslation: (r) => ({ languages_code: r.locale, title: r.title, cat: r.cat, tags: r.tags ?? [], body: r.body }),
    serialize: serializeDoc,
    parse: parseDoc,
  },
];
