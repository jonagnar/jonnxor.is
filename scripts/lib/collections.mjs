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

// Field kinds for simpleDescriptor — how a value crosses the seam in each direction.
const KIND = {
  req: { out: (v) => v, back: (v) => v },
  opt: { out: (v) => v ?? undefined, back: (v) => v ?? null },
  'opt-quoted': { out: (v) => v ?? undefined, back: (v) => v ?? null }, // date-like strings
  array: { out: (v) => v ?? [], back: (v) => v },
  flag: { out: (v) => v || undefined, back: (v) => v ?? false }, // booleans only — `||` would drop falsy-but-valid 0/'' for other kinds
};
const LOCALE_YAML_RE = /\.(is|en|ja)\.yaml$/;

/**
 * Build a full CollectionDescriptor for the standard shape (base item +
 * translations junction, YAML snapshot). `item`/`translation` are ordered maps
 * of field name -> kind (KIND above); slug/locale are implicit. Blog and
 * grimoire stay hand-rolled (custom serializers, date normalization, renames).
 */
export function simpleDescriptor({ name, item, translation }) {
  for (const [k, kind] of [...Object.entries(item), ...Object.entries(translation)]) {
    if (!KIND[kind]) throw new Error(`simpleDescriptor(${name}): unknown kind "${kind}" for field "${k}"`);
  }
  const itemKeys = Object.keys(item);
  const trKeys = Object.keys(translation);
  const codec = makeEntryCodec({
    keyOrder: ['slug', 'locale', ...itemKeys, ...trKeys],
    quoteKeys: itemKeys.filter((k) => item[k] === 'opt-quoted'),
    omitEmpty: itemKeys.filter((k) => item[k] === 'flag'),
  });
  return {
    name,
    dir: `src/content/${name}`,
    ext: '.yaml',
    fileRe: LOCALE_YAML_RE,
    fields: ['slug', ...itemKeys, { translations: ['languages_code', ...trKeys] }],
    toRecord: (it, t) => {
      const r = { slug: it.slug, locale: t.languages_code };
      for (const k of itemKeys) r[k] = KIND[item[k]].out(it[k]);
      for (const k of trKeys) r[k] = KIND[translation[k]].out(t[k]);
      return r;
    },
    toItem: (r) => {
      const it = { slug: r.slug };
      for (const k of itemKeys) it[k] = KIND[item[k]].back(r[k]);
      return it;
    },
    toTranslation: (r) => {
      const t = { languages_code: r.locale };
      for (const k of trKeys) t[k] = KIND[translation[k]].back(r[k]);
      return t;
    },
    serialize: codec.serialize,
    parse: codec.parse,
  };
}

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
  simpleDescriptor({
    name: 'games',
    item: { order: 'req', tab: 'req', date: 'opt-quoted', platforms: 'array', gradient: 'array', initials: 'req', favorite: 'flag' },
    translation: { title: 'req', sub: 'req' },
  }),
  simpleDescriptor({
    name: 'pages',
    item: {},
    translation: { kicker: 'req', title: 'req', lede: 'req', sections: 'opt' },
  }),
  simpleDescriptor({
    name: 'countdowns',
    item: { order: 'req', kind: 'req', when: 'opt-quoted', gold: 'flag', icon: 'opt', start: 'opt-quoted', rate: 'opt' },
    translation: { what: 'req', note: 'req' },
  }),
  simpleDescriptor({
    name: 'wallpapers',
    item: { order: 'req', tag: 'req', aspect_ratio: 'req', gradient: 'array', angle: 'req' },
    translation: { title: 'req' },
  }),
];
