// One descriptor per snapshot collection: how content:pull flattens a Directus
// item+translation into a per-locale file record, and how content:restore
// groups records back into a Directus create payload. Blog/grimoire wrap their
// pre-existing serializers so the committed snapshot stays byte-identical.
import { parsePost, serializePost } from './post-markdown.mjs';
import { parseDoc, serializeDoc } from './grimoire-yaml.mjs';
import { makeEntryCodec } from './entry-yaml.mjs';

/**
 * @typedef {object} CollectionDescriptor
 * @property {string} name - Directus collection name (e.g. 'blog', 'grimoire').
 * @property {string} dir - Repo-relative directory holding the committed snapshot files for this collection.
 * @property {string} ext - File extension for this collection's snapshot files, including the leading dot (e.g. '.md', '.yaml').
 * @property {RegExp} fileRe - Matches a valid `<slug>.<locale>.<ext>` filename in `dir`; used by content:pull's prune step to distinguish current locale files from legacy/orphaned ones.
 * @property {Array<string|object>} fields - Directus `readItems` fields selector: top-level item fields plus a nested `translations` field list, passed straight through to the SDK.
 * @property {(item: object, translation: object) => object} toRecord - Flattens one Directus item + one of its translations into a single flat record ready for `serialize`. Guarantee (1): the returned record's `slug` MUST equal `item.slug` and its `locale` MUST equal `translation.languages_code` — content:pull derives the on-disk filename from `item.slug`/`translation.languages_code` directly, while content:restore regroups parsed records back into items by reading `record.slug`/`record.locale`. If `toRecord` ever diverges from the item/translation it was given, the two directions disagree about which file belongs to which Directus row.
 * @property {(record: object) => object} toItem - Rebuilds the Directus item (top-level) create payload from one record. Only needs the fields captured in `toRecord`'s output.
 * @property {(record: object) => object} toTranslation - Rebuilds the Directus translation create payload from one record. Guarantee (3): any field this (or `toItem`) reads that a codec's `quoteKeys` forces to QUOTE_DOUBLE (see entry-yaml.mjs) must already be a plain string in the record produced by `toRecord` — quoting a non-string breaks the round-trip and would hand a stringified value to a typed Directus column on restore.
 * @property {(record: object) => string} serialize - Renders a flat record to the exact on-disk file contents (front matter + body, or YAML). Must be the left inverse of `parse`.
 * @property {(raw: string) => object} parse - Parses on-disk file contents back into a flat record shaped like `toRecord`'s output. Guarantee (2): `parse(serialize(toRecord(item, t)))` must round-trip losslessly enough that `toItem`/`toTranslation` can rebuild an equivalent Directus create payload from the parsed record — lossy serialization (e.g. dropping precision, reordering array items, coercing types) silently corrupts content:restore.
 */

// omitEmpty only needs keys whose "empty" value is `false` — entry-yaml.mjs
// already drops undefined/null unconditionally, so listing date-like/optional
// string keys here would be inert (they can never equal `false`).
const gamesCodec = makeEntryCodec({
  keyOrder: ['slug', 'locale', 'order', 'tab', 'date', 'platforms', 'gradient', 'initials', 'favorite', 'title', 'sub'],
  quoteKeys: ['date'],
  omitEmpty: ['favorite'],
});
const pagesCodec = makeEntryCodec({
  keyOrder: ['slug', 'locale', 'kicker', 'title', 'lede', 'sections'],
  omitEmpty: [],
});
const countdownsCodec = makeEntryCodec({
  keyOrder: ['slug', 'locale', 'order', 'kind', 'when', 'gold', 'icon', 'start', 'rate', 'what', 'note'],
  quoteKeys: ['when', 'start'],
  omitEmpty: ['gold'],
});
const wallpapersCodec = makeEntryCodec({
  keyOrder: ['slug', 'locale', 'order', 'tag', 'aspect_ratio', 'gradient', 'angle', 'title'],
  omitEmpty: [],
});

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
  {
    name: 'games',
    dir: 'src/content/games',
    ext: '.yaml',
    fileRe: /\.(is|en|ja)\.yaml$/,
    fields: ['slug', 'order', 'tab', 'date', 'platforms', 'gradient', 'initials', 'favorite', { translations: ['languages_code', 'title', 'sub'] }],
    toRecord: (g, t) => ({
      slug: g.slug, locale: t.languages_code, order: g.order, tab: g.tab,
      date: g.date ?? undefined, platforms: g.platforms ?? [], gradient: g.gradient ?? [],
      initials: g.initials, favorite: g.favorite || undefined,
      title: t.title, sub: t.sub,
    }),
    toItem: (r) => ({
      slug: r.slug, order: r.order, tab: r.tab, date: r.date ?? null,
      platforms: r.platforms, gradient: r.gradient, initials: r.initials, favorite: r.favorite ?? false,
    }),
    toTranslation: (r) => ({ languages_code: r.locale, title: r.title, sub: r.sub }),
    serialize: gamesCodec.serialize,
    parse: gamesCodec.parse,
  },
  {
    name: 'pages',
    dir: 'src/content/pages',
    ext: '.yaml',
    fileRe: /\.(is|en|ja)\.yaml$/,
    fields: ['slug', { translations: ['languages_code', 'kicker', 'title', 'lede', 'sections'] }],
    toRecord: (p, t) => ({
      slug: p.slug, locale: t.languages_code,
      kicker: t.kicker, title: t.title, lede: t.lede, sections: t.sections ?? undefined,
    }),
    toItem: (r) => ({ slug: r.slug }),
    toTranslation: (r) => ({ languages_code: r.locale, kicker: r.kicker, title: r.title, lede: r.lede, sections: r.sections ?? null }),
    serialize: pagesCodec.serialize,
    parse: pagesCodec.parse,
  },
  {
    name: 'countdowns',
    dir: 'src/content/countdowns',
    ext: '.yaml',
    fileRe: /\.(is|en|ja)\.yaml$/,
    fields: ['slug', 'order', 'kind', 'when', 'gold', 'icon', 'start', 'rate', { translations: ['languages_code', 'what', 'note'] }],
    toRecord: (c, t) => ({
      slug: c.slug, locale: t.languages_code, order: c.order, kind: c.kind,
      when: c.when ?? undefined, gold: c.gold || undefined, icon: c.icon ?? undefined,
      start: c.start ?? undefined, rate: c.rate ?? undefined,
      what: t.what, note: t.note,
    }),
    toItem: (r) => ({
      slug: r.slug, order: r.order, kind: r.kind, when: r.when ?? null,
      gold: r.gold ?? false, icon: r.icon ?? null, start: r.start ?? null, rate: r.rate ?? null,
    }),
    toTranslation: (r) => ({ languages_code: r.locale, what: r.what, note: r.note }),
    serialize: countdownsCodec.serialize,
    parse: countdownsCodec.parse,
  },
  {
    name: 'wallpapers',
    dir: 'src/content/wallpapers',
    ext: '.yaml',
    fileRe: /\.(is|en|ja)\.yaml$/,
    fields: ['slug', 'order', 'tag', 'aspect_ratio', 'gradient', 'angle', { translations: ['languages_code', 'title'] }],
    toRecord: (w, t) => ({
      slug: w.slug, locale: t.languages_code, order: w.order, tag: w.tag,
      aspect_ratio: w.aspect_ratio, gradient: w.gradient ?? [], angle: w.angle, title: t.title,
    }),
    toItem: (r) => ({ slug: r.slug, order: r.order, tag: r.tag, aspect_ratio: r.aspect_ratio, gradient: r.gradient, angle: r.angle }),
    toTranslation: (r) => ({ languages_code: r.locale, title: r.title }),
    serialize: wallpapersCodec.serialize,
    parse: wallpapersCodec.parse,
  },
];
