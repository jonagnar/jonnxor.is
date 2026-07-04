# Content Port Phase 1 Implementation Plan — foundation + games, countdowns, wallpapers

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port the three structured-data pages (games, countdowns, wallpapers) to Directus-owned, snapshot-driven content and refactor them into typed server-rendered Astro components, on a generalized descriptor-driven pull/restore pipeline.

**Architecture:** Per `.planning/2026-07-04-content-port-design.md` — new Directus collections (base + `*_translations`), committed per-locale YAML snapshot as the build contract, deterministic serializer, pages consume `getCollection` + `localizedEntry` en-fallback, client JS reduced to behavior (show/hide, tickers, lightbox). Slices 4–7 (portfolio, home, about, cv) get a follow-up plan once this seam is proven (repo precedent: i18n phase plans).

**Tech Stack:** Astro 6 content layer (glob loader + Zod), Directus 11 via `@directus/sdk` 22, `yaml` 2.9, Vitest, containerized Playwright.

---

## Environment notes (read first)

- Repo root: `~/dev/src/jonnxor.is` (WSL). **Non-interactive WSL shells lack mise shims** — prepend `export PATH="$HOME/.local/share/mise/shims:$PATH"` or `pnpm` won't be found (see `notes/cheatsheets/mise.md`).
- Directus must be running for schema/seed tasks: `pnpm directus:up` (needs Docker + decrypted `directus/.env` via sops/direnv).
- Playwright never runs on the host: `pnpm test:e2e`, `pnpm test:visual` (containerized via `scripts/pw.sh`).
- Branching: all work branches off **`preview`** and PRs back into `preview` (production is under construction; never push `main`). Suggested: one branch per slice — foundation Tasks 1–4 ride the games-slice branch.
- Date-ish fields in new collections are **plain strings** (Directus type `string`, YAML double-quoted), following the `grimoire.updated` precedent — avoids TZ/format drift and YAML 1.1 date coercion.

---

## Task 1: Generic deterministic YAML codec (`entry-yaml`)

**Files:**
- Create: `scripts/lib/entry-yaml.mjs`
- Test: `tests/content/entry-yaml.test.ts`

- [ ] **Step 1: Write the failing test**

```ts
// tests/content/entry-yaml.test.ts
import { describe, it, expect } from 'vitest';
import jsYaml from 'js-yaml';
import { makeEntryCodec } from '../../scripts/lib/entry-yaml.mjs';

const codec = makeEntryCodec({
  keyOrder: ['slug', 'locale', 'order', 'date', 'gold', 'gradient', 'title', 'body'],
  blockKeys: ['body'],
  quoteKeys: ['date'],
  omitEmpty: ['date', 'gold'],
});

const record = {
  slug: 'fable', locale: 'en', order: 2, date: '2026-10-09', gold: false,
  gradient: ['#0e2d1c', '#2e7d4f', '#ecbd3e'],
  title: 'Fable: The Return', body: '<p>Line one.</p>\n<p>Line two.</p>',
};

describe('makeEntryCodec', () => {
  it('writes keys in fixed order and round-trips', () => {
    const out = codec.serialize(record);
    expect(out.indexOf('slug:')).toBeLessThan(out.indexOf('locale:'));
    expect(out.indexOf('order:')).toBeLessThan(out.indexOf('date:'));
    const back = codec.parse(out);
    expect(back.slug).toBe('fable');
    expect(back.order).toBe(2);
    expect(back.gradient).toEqual(record.gradient);
    expect(back.body).toBe(record.body);
  });
  it('emits blockKeys as block literals', () => {
    expect(codec.serialize(record)).toContain('body: |');
  });
  it('double-quotes quoteKeys so js-yaml (YAML 1.1) reads a string, not a Date', () => {
    const out = codec.serialize(record);
    expect(out).toMatch(/date: "2026-10-09"/);
    const parsed = jsYaml.load(out) as Record<string, unknown>;
    expect(typeof parsed.date).toBe('string');
  });
  it('omits omitEmpty keys when undefined/null/false, keeps them otherwise', () => {
    const out = codec.serialize({ ...record, date: null, gold: false });
    expect(out).not.toContain('date:');
    expect(out).not.toContain('gold:');
    expect(codec.serialize({ ...record, gold: true })).toContain('gold: true');
  });
  it('quotes # hex strings safely (never bare)', () => {
    const back = codec.parse(codec.serialize(record));
    expect(back.gradient[0]).toBe('#0e2d1c');
  });
  it('is deterministic (serialize∘parse∘serialize is identity)', () => {
    const out = codec.serialize(record);
    expect(codec.serialize(codec.parse(out))).toBe(out);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run tests/content/entry-yaml.test.ts`
Expected: FAIL — cannot find module `entry-yaml.mjs`.

- [ ] **Step 3: Write the implementation**

```js
// scripts/lib/entry-yaml.mjs
import YAML, { Scalar } from 'yaml';

/**
 * Build a {serialize, parse} codec for one snapshot collection. Mirrors the
 * discipline grimoire-yaml.mjs established: fixed key order for stable diffs,
 * block-literal scalars for long text, line-wrapping disabled, date-like
 * strings double-quoted (Astro's js-yaml loader is YAML 1.1 and would coerce
 * a bare date to a Date object). `omitEmpty` keys are dropped when
 * undefined/null/false so optional fields don't pollute every file.
 */
export function makeEntryCodec({ keyOrder, blockKeys = [], quoteKeys = [], omitEmpty = [] }) {
  function serialize(r) {
    const obj = {};
    for (const k of keyOrder) {
      const v = r[k];
      if (v === undefined || v === null) continue;
      if (omitEmpty.includes(k) && v === false) continue;
      obj[k] = v;
    }
    const doc = new YAML.Document(obj);
    for (const k of blockKeys) {
      const n = doc.get(k, true);
      if (n) n.type = Scalar.BLOCK_LITERAL;
    }
    for (const k of quoteKeys) {
      const n = doc.get(k, true);
      if (n) n.type = Scalar.QUOTE_DOUBLE;
    }
    return doc.toString({ lineWidth: 0 });
  }
  // yaml's default schema is YAML 1.2 core: bare dates parse as strings, which
  // keeps serialize∘parse∘serialize the identity. Don't change the parse schema.
  return { serialize, parse: (raw) => YAML.parse(raw) };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run tests/content/entry-yaml.test.ts`
Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add scripts/lib/entry-yaml.mjs tests/content/entry-yaml.test.ts
git commit -m "feat(content): generic deterministic YAML codec for snapshot collections"
```

---

## Task 2: Collection descriptor table

**Files:**
- Create: `scripts/lib/collections.mjs`
- Test: `tests/content/collections.test.ts`

Descriptors tell pull/restore how to move one collection through the seam. This task ships **blog + grimoire only** (wrapping their existing serializers so committed snapshot bytes cannot change); each slice task appends its own descriptor.

- [ ] **Step 1: Write the failing test**

```ts
// tests/content/collections.test.ts
import { describe, it, expect } from 'vitest';
import { COLLECTIONS } from '../../scripts/lib/collections.mjs';

const REQUIRED = ['name', 'dir', 'ext', 'fileRe', 'fields', 'toRecord', 'toItem', 'toTranslation', 'serialize', 'parse'];

describe('collection descriptors', () => {
  it('every descriptor is complete', () => {
    expect(COLLECTIONS.length).toBeGreaterThanOrEqual(2);
    for (const c of COLLECTIONS) {
      for (const k of REQUIRED) expect(c[k], `${c.name}.${k}`).toBeDefined();
      expect(c.fileRe.test(`x.en${c.ext}`)).toBe(true);
      expect(c.fileRe.test(`x${c.ext}`)).toBe(false);
    }
  });
  it('blog round-trips pull-shape -> file -> restore-shape', () => {
    const blog = COLLECTIONS.find((c) => c.name === 'blog')!;
    const item = { slug: 'p1', date: '2026-05-28T00:00:00', category: 'Myth', draft: false };
    const t = { languages_code: 'en', title: 'T', excerpt: 'E', read_time: '9 min', body: 'Body.' };
    const rec = blog.toRecord(item, t);
    const parsed = blog.parse(blog.serialize(rec));
    expect(blog.toTranslation(parsed)).toEqual({ languages_code: 'en', title: 'T', excerpt: 'E', read_time: '9 min', body: 'Body.' });
    expect(blog.toItem(parsed).slug).toBe('p1');
  });
  it('grimoire round-trips', () => {
    const g = COLLECTIONS.find((c) => c.name === 'grimoire')!;
    const item = { slug: 'd1', order: 7, realm: 'code', game: null, updated: '2026-05-26' };
    const t = { languages_code: 'en', title: 'T', cat: 'C', tags: ['a'], body: '<p>x</p>' };
    const parsed = g.parse(g.serialize(g.toRecord(item, t)));
    expect(g.toItem(parsed)).toMatchObject({ slug: 'd1', order: 7, realm: 'code', updated: '2026-05-26' });
    expect(g.toTranslation(parsed)).toMatchObject({ languages_code: 'en', tags: ['a'] });
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run tests/content/collections.test.ts`
Expected: FAIL — module not found.

- [ ] **Step 3: Write the implementation**

```js
// scripts/lib/collections.mjs
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
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run tests/content/collections.test.ts`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add scripts/lib/collections.mjs tests/content/collections.test.ts
git commit -m "feat(content): collection descriptor table (blog + grimoire)"
```

---

## Task 3: Descriptor-driven `content:pull`

**Files:**
- Modify: `scripts/content-pull.mjs` (full rewrite, currently 85 lines of per-collection copy-paste)

- [ ] **Step 1: Rewrite the script**

```js
// scripts/content-pull.mjs — Directus -> committed snapshot, one file per
// (slug, locale), driven by the descriptor table. Prune removes generated
// locale files no longer backed by Directus plus legacy un-suffixed files.
import { readdir, writeFile, unlink } from 'node:fs/promises';
import { join } from 'node:path';
import { readItems } from '@directus/sdk';
import { connect, done } from './lib/directus-client.mjs';
import { COLLECTIONS } from './lib/collections.mjs';

const client = await connect();

for (const c of COLLECTIONS) {
  const items = await client.request(readItems(c.name, { limit: -1, fields: c.fields }));
  const wanted = new Set();
  for (const item of items) {
    for (const t of item.translations ?? []) {
      const file = `${item.slug}.${t.languages_code}${c.ext}`;
      wanted.add(file);
      await writeFile(join(c.dir, file), c.serialize(c.toRecord(item, t)), 'utf8');
    }
  }
  // prune: skip non-files so unlink can't EISDIR; any un-suffixed file with the
  // collection's extension is legacy by definition (superseded by <slug>.en.*).
  for (const ent of await readdir(c.dir, { withFileTypes: true })) {
    if (!ent.isFile()) continue;
    const f = ent.name;
    const isLocaleFile = c.fileRe.test(f);
    const isLegacy = f.endsWith(c.ext) && !isLocaleFile;
    if ((isLocaleFile && !wanted.has(f)) || isLegacy) {
      await unlink(join(c.dir, f));
      console.log('removed:', join(c.dir, f));
    }
  }
  console.log(`pulled ${c.name} — ${wanted.size} locale file(s) from ${items.length} item(s)`);
}
done();
```

- [ ] **Step 2: Verify byte-stability against the live snapshot**

Directus must contain the current content (it does on the authoring machine; on a fresh clone run `pnpm directus:up && pnpm directus:schema && pnpm content:restore` first).

Run: `pnpm content:pull && git status --porcelain src/content`
Expected: pull logs for blog + grimoire; `git status` output **empty** (byte-identical regeneration).

- [ ] **Step 3: Run the whole unit suite**

Run: `pnpm test`
Expected: PASS — all existing content/i18n/render tests untouched.

- [ ] **Step 4: Commit**

```bash
git add scripts/content-pull.mjs
git commit -m "refactor(content): descriptor-driven content:pull (byte-identical output)"
```

---

## Task 4: Descriptor-driven `content:restore`

**Files:**
- Modify: `scripts/content-restore.mjs` (full rewrite, keep the header comment block explaining purpose/idempotence — update wording from "blog posts AND grimoire docs" to "every snapshot collection")

- [ ] **Step 1: Rewrite the script body**

```js
import { readdir, readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { readItems, createItem } from '@directus/sdk';
import { connect, done } from './lib/directus-client.mjs';
import { COLLECTIONS } from './lib/collections.mjs';

const client = await connect();

for (const c of COLLECTIONS) {
  const files = (await readdir(c.dir)).filter((f) => c.fileRe.test(f));
  const bySlug = new Map();
  for (const file of files) {
    const r = c.parse(await readFile(join(c.dir, file), 'utf8'));
    // Base fields are non-translatable and written identically to every locale
    // file of an item, so the first locale file seen sets them and the rest
    // only add a translation.
    if (!bySlug.has(r.slug)) bySlug.set(r.slug, { ...c.toItem(r), translations: [] });
    bySlug.get(r.slug).translations.push(c.toTranslation(r));
  }
  const existing = new Set(
    (await client.request(readItems(c.name, { fields: ['slug'], limit: -1 }))).map((i) => i.slug),
  );
  let created = 0;
  for (const [slug, item] of bySlug) {
    if (existing.has(slug)) { console.log(`skip ${c.name} (exists):`, slug); continue; }
    await client.request(createItem(c.name, item));
    created++;
    console.log(`restored ${c.name}:`, slug, '(' + item.translations.map((t) => t.languages_code).join(', ') + ')');
  }
  console.log(`${c.name} done — ${created} restored, ${bySlug.size - created} skipped, ${bySlug.size} item(s) in snapshot`);
}
done();
```

- [ ] **Step 2: Verify idempotence + round-trip**

Run: `pnpm content:restore && pnpm content:pull && git status --porcelain src/content`
Expected: restore logs every slug as `skip (exists)`; `git status` empty.

- [ ] **Step 3: Commit**

```bash
git add scripts/content-restore.mjs
git commit -m "refactor(content): descriptor-driven content:restore"
```

---

## Task 5: Directus schema — `games` + `pages` collections

**Files:**
- Modify: `directus/scripts/setup-schema.mjs` (insert after the grimoire_translations block, before the language seeding at the bottom)
- Modify (generated): `directus/schema/snapshot.yaml`

- [ ] **Step 1: Add the four collections to setup-schema.mjs**

Follow the exact blog/grimoire pattern (idempotent `need()` guards, hidden junctions, translations alias + two relations per pair):

```js
// 6. games (base, non-translatable)
if (need('games')) {
  await client.request(createCollection({
    collection: 'games',
    meta: { icon: 'sports_esports', note: 'The Game Hall — tracker & favorites' },
    schema: {},
    fields: [
      { field: 'id', type: 'integer', schema: { is_primary_key: true, has_auto_increment: true }, meta: { hidden: true } },
      { field: 'slug', type: 'string', schema: { is_unique: true }, meta: { interface: 'input', required: true } },
      { field: 'order', type: 'integer', meta: { interface: 'input' } },
      { field: 'tab', type: 'string', meta: { interface: 'select-dropdown', options: { choices: [{ text: 'upcoming', value: 'upcoming' }, { text: 'playing', value: 'playing' }, { text: 'played', value: 'played' }, { text: 'favorites', value: 'favorites' }] } } },
      { field: 'date', type: 'string', schema: { is_nullable: true }, meta: { interface: 'input', note: 'YYYY-MM-DD or empty = TBA' } },
      { field: 'platforms', type: 'json', meta: { interface: 'tags' } },
      { field: 'gradient', type: 'json', meta: { interface: 'input-code', note: '3 hex colors' } },
      { field: 'initials', type: 'string', meta: { interface: 'input' } },
      { field: 'favorite', type: 'boolean', schema: { default_value: false }, meta: { interface: 'boolean' } },
    ],
  }));
}

// 7. games_translations (junction)
if (need('games_translations')) {
  await client.request(createCollection({
    collection: 'games_translations',
    meta: { hidden: true },
    schema: {},
    fields: [
      { field: 'id', type: 'integer', schema: { is_primary_key: true, has_auto_increment: true }, meta: { hidden: true } },
      { field: 'games', type: 'integer', meta: { hidden: true } },
      { field: 'languages_code', type: 'string', meta: { hidden: true } },
      { field: 'title', type: 'string', meta: { interface: 'input' } },
      { field: 'sub', type: 'string', meta: { interface: 'input' } },
    ],
  }));
  await client.request(createField('games', {
    field: 'translations', type: 'alias',
    meta: { interface: 'translations', special: ['translations'], options: { languageField: 'code' } },
  }));
  await client.request(createRelation({
    collection: 'games_translations', field: 'games', related_collection: 'games',
    meta: { one_field: 'translations', junction_field: 'languages_code' }, schema: { on_delete: 'SET NULL' },
  }));
  await client.request(createRelation({
    collection: 'games_translations', field: 'languages_code', related_collection: 'languages',
    meta: { junction_field: 'games' }, schema: { on_delete: 'SET NULL' },
  }));
}

// 8. pages (base) — prose surfaces; one row per routed page
if (need('pages')) {
  await client.request(createCollection({
    collection: 'pages',
    meta: { icon: 'description', note: 'Page prose — heads + structured sections' },
    schema: {},
    fields: [
      { field: 'id', type: 'integer', schema: { is_primary_key: true, has_auto_increment: true }, meta: { hidden: true } },
      { field: 'slug', type: 'string', schema: { is_unique: true }, meta: { interface: 'input', required: true } },
    ],
  }));
}

// 9. pages_translations (junction)
if (need('pages_translations')) {
  await client.request(createCollection({
    collection: 'pages_translations',
    meta: { hidden: true },
    schema: {},
    fields: [
      { field: 'id', type: 'integer', schema: { is_primary_key: true, has_auto_increment: true }, meta: { hidden: true } },
      { field: 'pages', type: 'integer', meta: { hidden: true } },
      { field: 'languages_code', type: 'string', meta: { hidden: true } },
      { field: 'kicker', type: 'string', meta: { interface: 'input' } },
      { field: 'title', type: 'string', meta: { interface: 'input' } },
      { field: 'lede', type: 'text', meta: { interface: 'input-multiline' } },
      { field: 'sections', type: 'json', meta: { interface: 'input-code', options: { language: 'json' }, note: 'Per-page structured prose; shape validated by the Astro build' } },
    ],
  }));
  await client.request(createField('pages', {
    field: 'translations', type: 'alias',
    meta: { interface: 'translations', special: ['translations'], options: { languageField: 'code' } },
  }));
  await client.request(createRelation({
    collection: 'pages_translations', field: 'pages', related_collection: 'pages',
    meta: { one_field: 'translations', junction_field: 'languages_code' }, schema: { on_delete: 'SET NULL' },
  }));
  await client.request(createRelation({
    collection: 'pages_translations', field: 'languages_code', related_collection: 'languages',
    meta: { junction_field: 'pages' }, schema: { on_delete: 'SET NULL' },
  }));
}
```

Renumber the trailing "seed the three languages" comment to `// 10.`.

- [ ] **Step 2: Apply and snapshot**

Run: `pnpm directus:up && pnpm directus:schema && pnpm directus:snapshot`
Expected: `schema setup complete`; `directus/schema/snapshot.yaml` diff shows the four new collections + relations.

- [ ] **Step 3: Verify idempotence**

Run: `pnpm directus:schema`
Expected: completes with no create calls (all `need()` false), exit 0.

- [ ] **Step 4: Commit**

```bash
git add directus/scripts/setup-schema.mjs directus/schema/snapshot.yaml
git commit -m "feat(directus): games + pages collections with translations"
```

---

## Task 6: Games slice — descriptors, seeds, round-trip

**Files:**
- Modify: `scripts/lib/collections.mjs` (append two descriptors)
- Create: `src/content/games/*.en.yaml` (17 files), `src/content/pages/games.en.yaml`

- [ ] **Step 1: Append `games` and `pages` descriptors to COLLECTIONS**

```js
// append to scripts/lib/collections.mjs — imports at top:
import { makeEntryCodec } from './entry-yaml.mjs';

const gamesCodec = makeEntryCodec({
  keyOrder: ['slug', 'locale', 'order', 'tab', 'date', 'platforms', 'gradient', 'initials', 'favorite', 'title', 'sub'],
  quoteKeys: ['date'],
  omitEmpty: ['date', 'favorite'],
});
const pagesCodec = makeEntryCodec({
  keyOrder: ['slug', 'locale', 'kicker', 'title', 'lede', 'sections'],
  omitEmpty: ['sections'],
});

// inside COLLECTIONS, after grimoire:
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
```

Note: `favorite: g.favorite || undefined` + `omitEmpty` keeps `favorite: false` out of every non-favorite file, and `toItem`'s `?? false` restores it.

Run: `pnpm exec vitest run tests/content/collections.test.ts` — Expected: PASS (descriptor-completeness test now covers 4 descriptors automatically).

- [ ] **Step 2: Write the seed files**

Create `src/content/games/<slug>.en.yaml`, one per row. `title`/`sub` are copied **verbatim** from `src/pages/games.astro` lines 35–57 (preserve `’`, `·`, `—`). Full field table (order is global 1–17, top-to-bottom of the GAMES array; favorites rows get `favorite: true`):

| # | slug | tab | date | platforms | gradient | initials |
|---|------|-----|------|-----------|----------|----------|
| 1 | grand-theft-auto-vi | upcoming | 2026-11-19 | PS5, Xbox | #2b0a3d, #b0316e, #f5e642 | VI |
| 2 | fable | upcoming | 2026-10-09 | Xbox, PC | #0e2d1c, #2e7d4f, #ecbd3e | FA |
| 3 | the-duskmere-saga | upcoming | 2026-08-14 | PC, Switch | #101a33, #27447e, #3fe0cb | DS |
| 4 | hollow-knight-silksong | upcoming | — | PC, Switch | #1a0f24, #5d2a66, #e85f6a | SS |
| 5 | elden-ring-nightreign | playing | — | PS5 | #10131c, #3a4a2e, #f2c94c | ER |
| 6 | monster-hunter-wilds | playing | — | PC | #27190c, #7d5326, #3fe0cb | MH |
| 7 | baldurs-gate-3 | playing | — | PC | #1c0f0f, #6e2230, #ecbd3e | BG |
| 8 | lies-of-p-overture | played | — | PS5 | #14141e, #444f6e, #d9c27a | LP |
| 9 | clair-obscur-expedition-33 | played | — | PC | #0d1a26, #1f5b73, #e0d9c0 | CO |
| 10 | astro-bot | played | — | PS5 | #0a1f33, #1d63b8, #f5e642 | AB |
| 11 | metaphor-refantazio | played | — | PS5 | #1c1410, #8a4c1f, #3fe0cb | MR |
| 12 | dragons-dogma | favorites | — | Series | #231307, #94501c, #f2c94c | DD |
| 13 | dark-souls-i-iii | favorites | — | Series | #15120c, #5e5034, #e8d9a0 | DS |
| 14 | elden-ring | favorites | — | PS5, PC | #101708, #46551f, #f2c94c | ER |
| 15 | sekiro | favorites | — | PC | #1b0d0d, #7e2a20, #ecdcb8 | SK |
| 16 | bloodborne | favorites | — | PS4 | #0d0d14, #3c2f4d, #9e2f3c | BB |
| 17 | silver | favorites | — | PC, Dreamcast | #0e1620, #3a5a78, #cfd8e0 | SV |

Format template (hex strings and dates MUST be double-quoted by hand; the first pull re-serializes canonically anyway):

```yaml
slug: fable
locale: en
order: 2
tab: upcoming
date: "2026-10-09"
platforms:
  - Xbox
  - PC
gradient:
  - "#0e2d1c"
  - "#2e7d4f"
  - "#ecbd3e"
initials: FA
title: Fable
sub: Playground · RPG
```

And `src/content/pages/games.en.yaml` (head text verbatim from `games.astro` lines 9–11):

```yaml
slug: games
locale: en
kicker: Game tracker
title: The Game Hall
lede: What I'm waiting for, what I'm playing, what I've slain — and the favorites I'd defend in single combat.
```

(Use the real typographic apostrophes from the source file.)

- [ ] **Step 3: Restore → pull → prove determinism**

```bash
pnpm content:restore        # creates 17 games + 1 page in Directus
pnpm content:pull           # rewrites seeds in canonical serializer form
git add -A src/content && git status --porcelain src/content   # normalization diff is expected here
pnpm content:pull && git status --porcelain src/content        # second pull: MUST be empty
```

Expected: second `git status` empty (pull is deterministic); restore re-run logs `skip (exists)` for every slug.

- [ ] **Step 4: Run unit suite, commit**

Run: `pnpm test` — Expected: PASS.

```bash
git add scripts/lib/collections.mjs src/content/games src/content/pages
git commit -m "feat(content): games + pages collections seeded through the snapshot seam"
```

---

## Task 7: Astro collections — `games` + `pages`

**Files:**
- Modify: `src/content.config.ts`
- Modify: `tests/content/config-wiring.test.ts:25` (expected count 2 → 4)

- [ ] **Step 1: Update the wiring test first**

In `tests/content/config-wiring.test.ts`, change the assertion and its test name:

```ts
  it('passes generateId: localeEntryId to every collection loader (blog, grimoire, games, pages)', () => {
    const codeLines = configSrc.split('\n').filter(l => !/^\s*\/\//.test(l));
    const matches = codeLines.join('\n').match(/generateId:\s*localeEntryId/g) ?? [];
    expect(matches.length).toBe(4);
  });
```

Run: `pnpm exec vitest run tests/content/config-wiring.test.ts` — Expected: FAIL (still 2).

- [ ] **Step 2: Add the collections**

Append to `src/content.config.ts` (before the export), and extend the export:

```ts
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
    date: z.string().optional(), // "YYYY-MM-DD"; absent = TBA
    platforms: z.array(z.string()),
    gradient: z.array(z.string()).length(3),
    initials: z.string(),
    favorite: z.boolean().default(false),
    title: z.string(),
    sub: z.string(),
  }),
});

// Page prose — heads (kicker/title/lede) + per-page structured `sections`.
// Section shapes are validated per page slug as pages are ported (superRefine).
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
    sections: z.record(z.unknown()).optional(),
  }),
});

export const collections = { blog, grimoire, games, pages };
```

(If Astro's bundled zod is v4 and `z.record(z.unknown())` errors, use `z.record(z.string(), z.unknown())`.)

- [ ] **Step 3: Verify**

Run: `pnpm exec vitest run tests/content && pnpm exec astro check && pnpm build`
Expected: all pass; build output unchanged in page count.

- [ ] **Step 4: Commit**

```bash
git add src/content.config.ts tests/content/config-wiring.test.ts
git commit -m "feat(content): games + pages Astro collections with locale-aware ids"
```

---

## Task 8: `PageHead` + `GameCard` components (TDD via Container)

**Files:**
- Create: `src/components/PageHead.astro`, `src/components/GameCard.astro`
- Test: `tests/render/components.test.ts`

- [ ] **Step 1: Write the failing render test**

```ts
// tests/render/components.test.ts
import { experimental_AstroContainer as AstroContainer } from 'astro/container';
import { describe, it, expect } from 'vitest';
import PageHead from '../../src/components/PageHead.astro';
import GameCard from '../../src/components/GameCard.astro';

describe('PageHead', () => {
  it('renders kicker, h1 and lede', async () => {
    const c = await AstroContainer.create();
    const html = await c.renderToString(PageHead, {
      props: { kicker: 'Game tracker', title: 'The Game Hall', lede: 'Lede text.', label: 'Games head' },
    });
    expect(html).toContain('class="kicker"');
    expect(html).toContain('<h1>The Game Hall</h1>');
    expect(html).toContain('data-screen-label="Games head"');
  });
});

describe('GameCard', () => {
  const base = {
    tab: 'playing', platforms: ['PS5'], gradient: ['#10131c', '#3a4a2e', '#f2c94c'],
    initials: 'ER', favorite: false, title: 'Elden Ring: Nightreign', sub: 'NG+2', hidden: false,
  };
  it('renders cover, title, platform chips and data-tab', async () => {
    const c = await AstroContainer.create();
    const html = await c.renderToString(GameCard, { props: base });
    expect(html).toContain('data-tab="playing"');
    expect(html).toContain('<span class="gi">ER</span>');
    expect(html).toContain('Elden Ring: Nightreign');
    expect(html).toContain('<span class="chip">PS5</span>');
    expect(html).not.toContain('hidden');
    expect(html).not.toContain('class="fav"');
  });
  it('marks hidden cards and favorites', async () => {
    const c = await AstroContainer.create();
    const html = await c.renderToString(GameCard, { props: { ...base, hidden: true, favorite: true } });
    expect(html).toContain('hidden');
    expect(html).toContain('class="fav"');
  });
  it('renders an empty countdown badge for dated upcoming games and TBA otherwise', async () => {
    const c = await AstroContainer.create();
    const dated = await c.renderToString(GameCard, { props: { ...base, tab: 'upcoming', date: '2026-11-19' } });
    expect(dated).toContain('data-date="2026-11-19"');
    const tba = await c.renderToString(GameCard, { props: { ...base, tab: 'upcoming' } });
    expect(tba).toContain('>TBA<');
  });
});
```

Run: `pnpm exec vitest run tests/render/components.test.ts` — Expected: FAIL (components missing).

- [ ] **Step 2: Create the components**

```astro
---
// src/components/PageHead.astro — the shared page-head trio. Content comes from
// the `pages` collection; this component is presentational only.
interface Props { kicker: string; title: string; lede: string; label?: string }
const { kicker, title, lede, label } = Astro.props;
---
<section class="page-head" data-screen-label={label}>
  <div class="container">
    <p class="kicker">{kicker}</p>
    <h1>{title}</h1>
    <p class="lede">{lede}</p>
  </div>
</section>
```

```astro
---
// src/components/GameCard.astro — one tracker card, server-rendered. The tab
// filter toggles [hidden]; the countdown badge is filled client-side from
// data-date (window.JX.daysUntil).
interface Props {
  tab: 'upcoming' | 'playing' | 'played' | 'favorites';
  date?: string;
  platforms: string[];
  gradient: string[];
  initials: string;
  favorite: boolean;
  title: string;
  sub: string;
  hidden: boolean;
}
const { tab, date, platforms, gradient, initials, favorite, title, sub, hidden } = Astro.props;
const grad = `linear-gradient(150deg, ${gradient[0]}, ${gradient[1]} 58%, ${gradient[2]} 150%)`;
---
<article class="card lift game-card" data-tab={tab} hidden={hidden}>
  <div class="cover" style={`background: ${grad};`}>
    <span class="gi">{initials}</span>
    {tab === 'upcoming' && date && <span class="badge tq countdown" data-date={date}></span>}
    {tab === 'upcoming' && !date && <span class="badge quiet countdown">TBA</span>}
    {favorite && <span class="fav" aria-label="Favorite">★</span>}
  </div>
  <div class="body">
    <h3>{title}</h3>
    <p class="sub">{sub}</p>
    <div class="plats">{platforms.map((p) => <span class="chip">{p}</span>)}</div>
  </div>
</article>
```

- [ ] **Step 3: Run test to verify it passes**

Run: `pnpm exec vitest run tests/render/components.test.ts` — Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add src/components/PageHead.astro src/components/GameCard.astro tests/render/components.test.ts
git commit -m "feat(components): PageHead + GameCard server-rendered components"
```

---

## Task 9: Refactor `games.astro` to collections + behavior-only JS

**Files:**
- Modify: `src/pages/games.astro` (full rewrite)
- Modify: `src/i18n/ui.ts` (add en-only keys — **do not** add is/ja values this cycle: rendered text must not change at `/`)
- Modify: `tests/render/pages.test.ts` (remove Games from Container smoke list — collection-backed pages can't load `astro:content` under Vitest, same reason blog/docs are absent)
- Modify: `tests/e2e/games.spec.ts` (first-card locator must target the favorites card; DOM now contains all tabs' cards)

- [ ] **Step 1: Add en-only UI keys**

In the `en` block of `src/i18n/ui.ts` (alongside the other `nav.*` groups):

```ts
    'games.tab.upcoming': 'Upcoming',
    'games.tab.playing': 'Playing',
    'games.tab.played': 'Played',
    'games.tab.favorites': 'Favorites',
    'games.empty': 'The hall is quiet… for now.',
```

(is/ja intentionally omitted — they fall back to en, keeping `/` pixel-identical. Icelandic drafts arrive with the translation-parity work, not this port.)

- [ ] **Step 2: Rewrite games.astro**

```astro
---
import { getCollection } from 'astro:content';
import Base from "../layouts/Base.astro";
import PageHead from "../components/PageHead.astro";
import GameCard from "../components/GameCard.astro";
import { localizedEntry, uniqueSlugs } from "../i18n/localized";
import { useTranslations } from "../i18n/utils";
import type { Locale } from "../i18n/ui";

const locale = (Astro.currentLocale ?? 'is') as Locale;
const t = useTranslations(locale);

const all = await getCollection('games');
const games = uniqueSlugs(all)
  .map((s) => localizedEntry(all, s, locale))
  .filter((g): g is NonNullable<typeof g> => Boolean(g))
  .sort((a, b) => a.data.order - b.data.order);

const pagesAll = await getCollection('pages');
const head = localizedEntry(pagesAll, 'games', locale)!.data;

const TABS = ['upcoming', 'playing', 'played', 'favorites'] as const;
const DEFAULT_TAB = 'playing';
---

<Base title="Game Tracker — JonnXor" page="games">
  <main>
    <PageHead kicker={head.kicker} title={head.title} lede={head.lede} label="Games head" />

    <div class="container">
      <div class="rune-divider" aria-hidden="true"><span class="runes"></span></div>

      <div class="tabs tracker-tabs" role="tablist" data-screen-label="Tracker tabs">
        {TABS.map((tab) => (
          <button class:list={["tab", { active: tab === DEFAULT_TAB }]} role="tab" data-tab={tab} type="button">{t(`games.tab.${tab}`)}</button>
        ))}
      </div>

      <section class="game-grid" id="game-grid" data-screen-label="Game grid">
        {games.map((g) => (
          <GameCard
            tab={g.data.tab} date={g.data.date} platforms={g.data.platforms}
            gradient={g.data.gradient} initials={g.data.initials} favorite={g.data.favorite}
            title={g.data.title} sub={g.data.sub} hidden={g.data.tab !== DEFAULT_TAB}
          />
        ))}
        <p class="empty-note" id="game-empty" hidden>{t('games.empty')}</p>
      </section>
    </div>
  </main>

  <script is:inline slot="end">
(function () {
  'use strict';
  var tabs = document.querySelectorAll('.tracker-tabs .tab');
  var cards = document.querySelectorAll('#game-grid .game-card');
  var empty = document.getElementById('game-empty');

  function show(tab) {
    var n = 0;
    cards.forEach(function (c) {
      var on = c.getAttribute('data-tab') === tab;
      c.hidden = !on;
      if (on) n++;
    });
    if (empty) empty.hidden = n > 0;
  }

  tabs.forEach(function (btn) {
    btn.addEventListener('click', function () {
      tabs.forEach(function (b) { b.classList.remove('active'); });
      btn.classList.add('active');
      show(btn.getAttribute('data-tab'));
      try { localStorage.setItem('jx-games-tab', btn.getAttribute('data-tab')); } catch (e) {}
    });
  });

  // Countdown badges: server renders the shell, we fill the number.
  document.querySelectorAll('#game-grid .countdown[data-date]').forEach(function (el) {
    var c = window.JX.daysUntil(el.getAttribute('data-date'));
    el.textContent = c.past ? 'OUT NOW' : c.days + ' days';
  });

  var saved = null;
  try { saved = localStorage.getItem('jx-games-tab'); } catch (e) {}
  if (saved && saved !== 'playing') {
    tabs.forEach(function (b) { b.classList.toggle('active', b.getAttribute('data-tab') === saved); });
    show(saved);
  }
})();
  </script>
</Base>

<style is:global>
  .tracker-tabs { margin: 0 0 28px; }
  .game-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 20px; }
  /* .game-card sets display:flex, which outranks the UA's [hidden] rule — pin it. */
  .game-grid .game-card[hidden] { display: none; }
  .game-card { overflow: hidden; display: flex; flex-direction: column; }
  .game-card .cover {
    aspect-ratio: 3 / 4;
    position: relative;
    display: grid;
    place-items: center;
    color: rgba(255, 255, 255, .92);
    overflow: hidden;
  }
  .game-card .cover .gi {
    font-family: var(--font-display);
    font-size: 38px;
    font-weight: 900;
    letter-spacing: .06em;
    text-shadow: 0 2px 16px rgba(0, 0, 0, .45);
  }
  .game-card .cover .countdown {
    position: absolute;
    top: 10px;
    left: 10px;
  }
  .game-card .cover .fav {
    position: absolute;
    top: 10px;
    right: 10px;
    color: var(--gold);
    filter: drop-shadow(0 0 calc(var(--glow-r) * .5) var(--glow-gold));
    font-size: 16px;
  }
  .game-card .body { padding: 14px 16px 16px; display: flex; flex-direction: column; gap: 8px; flex: 1; }
  .game-card h3 { font-family: var(--font-body); font-size: 15px; font-weight: 700; margin: 0; line-height: 1.3; }
  .game-card .sub { font-size: 12.5px; color: var(--ink-faint); margin: 0; }
  .game-card .plats { display: flex; flex-wrap: wrap; gap: 5px; margin-top: auto; }
  .empty-note { color: var(--ink-faint); text-align: center; padding: 48px 0; font-family: var(--font-display); letter-spacing: .12em; font-size: 13px; grid-column: 1 / -1; }
</style>
```

(Note `grid-column: 1 / -1` added to `.empty-note` — it now lives inside the grid; it is hidden whenever any card is visible, so no visual change.)

- [ ] **Step 3: Update the render smoke test**

In `tests/render/pages.test.ts`: delete the `import Games…` line, the `['games', Games],` row, and the Games landmark assertion (line 39). Add this comment where the row was:

```ts
  // games is collection-backed since the content port — the Container can't load
  // astro:content under Vitest (same reason blog/docs aren't here). Covered by
  // tests/render/components.test.ts + e2e.
```

- [ ] **Step 4: Update the e2e spec's first-card locator**

`tests/e2e/games.spec.ts` — the DOM now contains all cards (hidden ones included), so `.first()` would grab a hidden upcoming card:

```ts
import { test, expect } from '@playwright/test';

test('switching tabs updates the grid and persists jx-games-tab', async ({ page }) => {
  await page.goto('/games');
  await expect(page.locator('.tracker-tabs .tab.active')).toHaveAttribute('data-tab', 'playing');
  await expect(page.locator('#game-grid .game-card[data-tab="playing"]').first()).toBeVisible();
  await page.locator('.tracker-tabs .tab[data-tab="favorites"]').click();
  await expect(page.locator('.tracker-tabs .tab[data-tab="favorites"]')).toHaveClass(/active/);
  await expect(page.locator('#game-grid .game-card[data-tab="favorites"]').first()).toBeVisible();
  await expect(page.locator('#game-grid .game-card[data-tab="playing"]').first()).toBeHidden();
  expect(await page.evaluate(() => localStorage.getItem('jx-games-tab'))).toBe('favorites');
});
```

- [ ] **Step 5: Verify everything**

```bash
pnpm test                                   # unit + render
pnpm exec astro check
sh scripts/pw.sh test tests/e2e/games.spec.ts
pnpm test:visual                            # /games baseline must be UNCHANGED
```

Expected: all green. If the visual diff flags /games, the DOM/CSS drifted — fix the page, do NOT re-record.

- [ ] **Step 6: Commit + PR**

```bash
git add -A
git commit -m "feat(games): port Game Hall to Directus-owned collection, server-rendered cards"
# PR this branch into preview; merge when CI is green.
```

---

## Task 10: Countdowns slice — schema + seeds

**Files:**
- Modify: `directus/scripts/setup-schema.mjs`, `directus/schema/snapshot.yaml` (generated)
- Modify: `scripts/lib/collections.mjs`
- Create: `src/content/countdowns/*.en.yaml` (10 files), `src/content/pages/countdowns.en.yaml`

- [ ] **Step 1: Add `countdowns` + `countdowns_translations` to setup-schema.mjs**

Same pattern as games. Base fields:

```js
      { field: 'id', type: 'integer', schema: { is_primary_key: true, has_auto_increment: true }, meta: { hidden: true } },
      { field: 'slug', type: 'string', schema: { is_unique: true }, meta: { interface: 'input', required: true } },
      { field: 'order', type: 'integer', meta: { interface: 'input' } },
      { field: 'kind', type: 'string', meta: { interface: 'select-dropdown', options: { choices: [{ text: 'countdown', value: 'countdown' }, { text: 'countup', value: 'countup' }] } } },
      { field: 'when', type: 'string', schema: { is_nullable: true }, meta: { interface: 'input', note: 'YYYY-MM-DD or empty = indefinite' } },
      { field: 'gold', type: 'boolean', schema: { default_value: false }, meta: { interface: 'boolean' } },
      { field: 'icon', type: 'string', schema: { is_nullable: true }, meta: { interface: 'input' } },
      { field: 'start', type: 'string', schema: { is_nullable: true }, meta: { interface: 'input', note: 'ISO datetime literal, countups only' } },
      { field: 'rate', type: 'float', schema: { is_nullable: true }, meta: { interface: 'input', note: 'hours/day, countups only' } },
```

And the junction + alias + relations:

```js
if (need('countdowns_translations')) {
  await client.request(createCollection({
    collection: 'countdowns_translations',
    meta: { hidden: true },
    schema: {},
    fields: [
      { field: 'id', type: 'integer', schema: { is_primary_key: true, has_auto_increment: true }, meta: { hidden: true } },
      { field: 'countdowns', type: 'integer', meta: { hidden: true } },
      { field: 'languages_code', type: 'string', meta: { hidden: true } },
      { field: 'what', type: 'string', meta: { interface: 'input' } },
      { field: 'note', type: 'string', meta: { interface: 'input' } },
    ],
  }));
  await client.request(createField('countdowns', {
    field: 'translations', type: 'alias',
    meta: { interface: 'translations', special: ['translations'], options: { languageField: 'code' } },
  }));
  await client.request(createRelation({
    collection: 'countdowns_translations', field: 'countdowns', related_collection: 'countdowns',
    meta: { one_field: 'translations', junction_field: 'languages_code' }, schema: { on_delete: 'SET NULL' },
  }));
  await client.request(createRelation({
    collection: 'countdowns_translations', field: 'languages_code', related_collection: 'languages',
    meta: { junction_field: 'countdowns' }, schema: { on_delete: 'SET NULL' },
  }));
}
```

Run: `pnpm directus:schema && pnpm directus:snapshot` → commit both files:

```bash
git add directus/scripts/setup-schema.mjs directus/schema/snapshot.yaml
git commit -m "feat(directus): countdowns collection with translations"
```

- [ ] **Step 2: Append the descriptor**

```js
const countdownsCodec = makeEntryCodec({
  keyOrder: ['slug', 'locale', 'order', 'kind', 'when', 'gold', 'icon', 'start', 'rate', 'what', 'note'],
  quoteKeys: ['when', 'start'],
  omitEmpty: ['when', 'gold', 'icon', 'start', 'rate'],
});
// in COLLECTIONS:
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
```

- [ ] **Step 3: Seed files** (`what`/`note` verbatim from `countdowns.astro` lines 38–53)

| # | slug | kind | when | gold | icon | start | rate |
|---|------|------|------|------|------|-------|------|
| 1 | thjodhatidardagurinn | countdown | 2026-06-17 | true | — | — | — |
| 2 | summer-solstice | countdown | 2026-06-21 | — | — | — | — |
| 3 | fable-release | countdown | 2026-10-09 | — | — | — | — |
| 4 | gta-vi-release | countdown | 2026-11-19 | — | — | — | — |
| 5 | jol-christmas-eve | countdown | 2026-12-24 | true | — | — | — |
| 6 | hollow-knight-silksong | countdown | — | — | — | — | — |
| 7 | working | countup | — | — | ⚒ | 2014-06-01T09:00:00 | 7.4 |
| 8 | gaming | countup | — | — | 🎮 | 2002-05-05T08:00:00 | 2.6 |
| 9 | studying | countup | — | — | 📖 | 2002-05-05T08:00:00 | 1.7 |
| 10 | on-the-toilet | countup | — | — | 🚽 | 1992-05-05T08:00:00 | 0.35 |

(Rows 8–9 inline the `AGE10` constant, row 10 inlines `BORN` — same instants as the page today.)

And `src/content/pages/countdowns.en.yaml` — head from lines 9–11 plus the section strings from lines 19, 26, 28:

```yaml
slug: countdowns
locale: en
kicker: Countdowns & count-ups
title: The Reckoning
lede: Time until the things worth waiting for — and a brutally honest accounting of where a life actually goes.
sections:
  countdown_kicker: Counting down
  countup_kicker: Counting up — days of my life spent…
  methodology: "Methodology: extremely scientific estimates since age 10, updated live. The toilet number includes time spent reading patch notes. No further questions."
```

- [ ] **Step 4: Restore, pull twice, verify determinism, commit**

```bash
pnpm content:restore && pnpm content:pull
git add -A src/content
pnpm content:pull && git status --porcelain src/content   # MUST be empty
pnpm test
git add scripts/lib/collections.mjs src/content/countdowns src/content/pages
git commit -m "feat(content): countdowns collection seeded through the snapshot seam"
```

---

## Task 11: Countdowns Astro collection + components

**Files:**
- Modify: `src/content.config.ts` (add `countdowns`; export it)
- Modify: `tests/content/config-wiring.test.ts` (count 4 → 5)
- Create: `src/components/CountdownCard.astro`, `src/components/CountUpCard.astro`
- Test: append to `tests/render/components.test.ts`

- [ ] **Step 1: Wiring test 4 → 5** (same edit shape as Task 7 Step 1; run, expect FAIL)

- [ ] **Step 2: Collection schema**

```ts
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
    when: z.string().optional(),
    gold: z.boolean().default(false),
    icon: z.string().optional(),
    start: z.string().optional(),
    rate: z.number().optional(),
    what: z.string(),
    note: z.string(),
  }),
});

export const collections = { blog, grimoire, games, pages, countdowns };
```

Also extend the `pages` schema (from Task 7) with the design's promised build-time
`sections` validation — chain `.superRefine` onto its `z.object({...})`:

```ts
  }).superRefine((p, ctx) => {
    // Per-page section shapes, validated as pages are ported (design §3).
    if (p.slug === 'countdowns') {
      const shape = z.object({
        countdown_kicker: z.string(),
        countup_kicker: z.string(),
        methodology: z.string(),
      });
      const r = shape.safeParse(p.sections);
      if (!r.success) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, message: `pages/countdowns sections invalid: ${r.error.message}` });
      }
    }
  }),
```

Run: `pnpm exec vitest run tests/content` — Expected: PASS.

- [ ] **Step 3: Failing component tests** (append to `tests/render/components.test.ts`)

```ts
import CountdownCard from '../../src/components/CountdownCard.astro';
import CountUpCard from '../../src/components/CountUpCard.astro';

describe('CountdownCard', () => {
  it('renders dated card with four clock cells and data-when', async () => {
    const c = await AstroContainer.create();
    const html = await c.renderToString(CountdownCard, {
      props: { what: 'Jól', when: '2026-12-24', note: 'Hangikjöt o’clock', gold: true },
    });
    expect(html).toContain('data-when="2026-12-24"');
    expect(html).toContain('cd-card gold');
    expect((html.match(/data-u=/g) ?? []).length).toBe(4);
  });
  it('renders the ∞ cell when undated', async () => {
    const c = await AstroContainer.create();
    const html = await c.renderToString(CountdownCard, {
      props: { what: 'Silksong', note: 'Any day now.', gold: false },
    });
    expect(html).toContain('∞');
    expect(html).not.toContain('data-when');
  });
});

describe('CountUpCard', () => {
  it('renders icon, data-up index and start/rate data attributes', async () => {
    const c = await AstroContainer.create();
    const html = await c.renderToString(CountUpCard, {
      props: { icon: '⚒', what: 'Working', note: 'Code.', start: '2014-06-01T09:00:00', rate: 7.4, index: 0 },
    });
    expect(html).toContain('data-up="0"');
    expect(html).toContain('data-start="2014-06-01T09:00:00"');
    expect(html).toContain('data-rate="7.4"');
  });
});
```

Run: expect FAIL (components missing).

- [ ] **Step 4: Components**

```astro
---
// src/components/CountdownCard.astro — clock shell; the page's ticker fills the
// cell numbers from data-when. Markup mirrors the pre-port client-rendered DOM.
interface Props { what: string; when?: string; note: string; gold: boolean }
const { what, when, note, gold } = Astro.props;
const UNITS = [['d', 'days'], ['h', 'hrs'], ['m', 'min'], ['s', 'sec']] as const;
---
<article class:list={["card cd-card", { gold }]} data-when={when}>
  <div class="cd-what">{what}</div>
  <div class="cd-when">{when ? `${when} · ${note}` : note}</div>
  <div class="cd-clock">
    {when ? UNITS.map(([u, lbl]) => (
      <div class="cd-cell"><div class="cd-num" data-u={u}>–</div><div class="cd-lbl">{lbl}</div></div>
    )) : (
      <div class="cd-cell" style="flex: 4;"><div class="cd-num">∞</div><div class="cd-lbl">soon™</div></div>
    )}
  </div>
</article>
```

```astro
---
// src/components/CountUpCard.astro — ticking accumulator shell; data-up keeps
// the e2e contract ([data-up="0"]), data-start/data-rate feed the ticker.
interface Props { icon?: string; what: string; note: string; start?: string; rate?: number; index: number }
const { icon, what, note, start, rate, index } = Astro.props;
---
<article class="card up-card">
  <div class="up-icon">{icon}</div>
  <div class="up-num"><span data-up={index} data-start={start} data-rate={rate}>0</span> <small>days</small></div>
  <div class="up-what">{what}</div>
  <p class="up-note">{note}</p>
</article>
```

- [ ] **Step 5: Run tests, commit**

```bash
pnpm exec vitest run tests/render/components.test.ts tests/content
git add -A
git commit -m "feat(components): CountdownCard + CountUpCard, countdowns collection"
```

---

## Task 12: Refactor `countdowns.astro`

**Files:**
- Modify: `src/pages/countdowns.astro` (full rewrite)
- Modify: `tests/render/pages.test.ts` (remove Countdowns import/row + its landmark assertion, same comment pattern as games)

- [ ] **Step 1: Rewrite the page**

Frontmatter + template:

```astro
---
import { getCollection } from 'astro:content';
import Base from "../layouts/Base.astro";
import PageHead from "../components/PageHead.astro";
import CountdownCard from "../components/CountdownCard.astro";
import CountUpCard from "../components/CountUpCard.astro";
import { localizedEntry, uniqueSlugs } from "../i18n/localized";
import type { Locale } from "../i18n/ui";

const locale = (Astro.currentLocale ?? 'is') as Locale;

const all = await getCollection('countdowns');
const entries = uniqueSlugs(all)
  .map((s) => localizedEntry(all, s, locale))
  .filter((e): e is NonNullable<typeof e> => Boolean(e))
  .sort((a, b) => a.data.order - b.data.order);
const downs = entries.filter((e) => e.data.kind === 'countdown');
const ups = entries.filter((e) => e.data.kind === 'countup');

const pagesAll = await getCollection('pages');
const page = localizedEntry(pagesAll, 'countdowns', locale)!.data;
const sections = page.sections as { countdown_kicker: string; countup_kicker: string; methodology: string };
---

<Base title="Countdowns — JonnXor" page="countdowns">
  <main>
    <PageHead kicker={page.kicker} title={page.title} lede={page.lede} label="Countdowns head" />

    <div class="container">
      <div class="rune-divider" aria-hidden="true"><span class="runes"></span></div>

      <section data-screen-label="Countdowns">
        <p class="kicker">{sections.countdown_kicker}</p>
        <div class="cd-grid" id="cd-grid">
          {downs.map((e) => (
            <CountdownCard what={e.data.what} when={e.data.when} note={e.data.note} gold={e.data.gold} />
          ))}
        </div>
      </section>

      <div class="rune-divider" aria-hidden="true"><span class="runes"></span></div>

      <section data-screen-label="Count-ups">
        <p class="kicker gold">{sections.countup_kicker}</p>
        <div class="up-grid" id="up-grid">
          {ups.map((e, i) => (
            <CountUpCard icon={e.data.icon} what={e.data.what} note={e.data.note} start={e.data.start} rate={e.data.rate} index={i} />
          ))}
        </div>
        <p class="up-method">{sections.methodology}</p>
      </section>
    </div>
  </main>

  <script is:inline slot="end">
(function () {
  'use strict';
  function pad(n) { return n < 10 ? '0' + n : '' + n; }

  var cdCards = document.querySelectorAll('#cd-grid .cd-card[data-when]');
  var upCells = document.querySelectorAll('#up-grid [data-up]');

  function tick() {
    var now = Date.now();
    cdCards.forEach(function (card) {
      var r = window.JX.daysUntil(card.getAttribute('data-when'));
      card.querySelectorAll('.cd-num[data-u]').forEach(function (el) {
        var u = el.getAttribute('data-u');
        var v = r.past ? 0 : r[u];
        el.textContent = u === 'd' ? v : pad(v);
      });
    });
    upCells.forEach(function (el) {
      var start = new Date(el.getAttribute('data-start')).getTime();
      var rate = parseFloat(el.getAttribute('data-rate'));
      var days = (now - start) / 86400000 * (rate / 24);
      el.textContent = days.toLocaleString('en-US', { minimumFractionDigits: 4, maximumFractionDigits: 4 });
    });
  }

  tick();
  setInterval(tick, 100);
})();
  </script>
</Base>
```

Keep the existing `<style is:global>` block **unchanged** (copy it over verbatim from the current file, lines 112–131).

- [ ] **Step 2: Update `tests/render/pages.test.ts`** — remove Countdowns import, `['countdowns', Countdowns],` row, and the `'The Reckoning'` landmark line.

- [ ] **Step 3: Verify**

```bash
pnpm test && pnpm exec astro check
sh scripts/pw.sh test tests/e2e/countdowns.spec.ts   # data-up contract preserved
pnpm test:visual                                     # /countdowns is excluded; suite must still pass
```

Expected: all green (the count-up tick test exercises the new DOM-driven ticker).

- [ ] **Step 4: Commit + PR into preview**

```bash
git add -A
git commit -m "feat(countdowns): port The Reckoning to Directus-owned collection, server-rendered cards"
```

---

## Task 13: Wallpapers slice — schema + seeds

**Files:**
- Modify: `directus/scripts/setup-schema.mjs`, `directus/schema/snapshot.yaml` (generated)
- Modify: `scripts/lib/collections.mjs`
- Create: `src/content/wallpapers/*.en.yaml` (12 files), `src/content/pages/wallpapers.en.yaml`

- [ ] **Step 1: Schema**

```js
if (need('wallpapers')) {
  await client.request(createCollection({
    collection: 'wallpapers',
    meta: { icon: 'wallpaper', note: 'The Hoard — generated gradient wallpapers' },
    schema: {},
    fields: [
      { field: 'id', type: 'integer', schema: { is_primary_key: true, has_auto_increment: true }, meta: { hidden: true } },
      { field: 'slug', type: 'string', schema: { is_unique: true }, meta: { interface: 'input', required: true } },
      { field: 'order', type: 'integer', meta: { interface: 'input' } },
      { field: 'tag', type: 'string', meta: { interface: 'input' } },
      { field: 'aspect_ratio', type: 'string', meta: { interface: 'input', note: 'CSS aspect-ratio, e.g. 16/10' } },
      { field: 'gradient', type: 'json', meta: { interface: 'input-code', note: '3 hex colors' } },
      { field: 'angle', type: 'integer', meta: { interface: 'input', note: 'gradient angle 0-360' } },
    ],
  }));
}
if (need('wallpapers_translations')) {
  await client.request(createCollection({
    collection: 'wallpapers_translations',
    meta: { hidden: true },
    schema: {},
    fields: [
      { field: 'id', type: 'integer', schema: { is_primary_key: true, has_auto_increment: true }, meta: { hidden: true } },
      { field: 'wallpapers', type: 'integer', meta: { hidden: true } },
      { field: 'languages_code', type: 'string', meta: { hidden: true } },
      { field: 'title', type: 'string', meta: { interface: 'input' } },
    ],
  }));
  await client.request(createField('wallpapers', {
    field: 'translations', type: 'alias',
    meta: { interface: 'translations', special: ['translations'], options: { languageField: 'code' } },
  }));
  await client.request(createRelation({
    collection: 'wallpapers_translations', field: 'wallpapers', related_collection: 'wallpapers',
    meta: { one_field: 'translations', junction_field: 'languages_code' }, schema: { on_delete: 'SET NULL' },
  }));
  await client.request(createRelation({
    collection: 'wallpapers_translations', field: 'languages_code', related_collection: 'languages',
    meta: { junction_field: 'wallpapers' }, schema: { on_delete: 'SET NULL' },
  }));
}
```

Run `pnpm directus:schema && pnpm directus:snapshot`, commit:

```bash
git add directus/scripts/setup-schema.mjs directus/schema/snapshot.yaml
git commit -m "feat(directus): wallpapers collection with translations"
```

- [ ] **Step 2: Descriptor**

```js
const wallpapersCodec = makeEntryCodec({
  keyOrder: ['slug', 'locale', 'order', 'tag', 'aspect_ratio', 'gradient', 'angle', 'title'],
});
// in COLLECTIONS:
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
```

- [ ] **Step 3: Seeds** (titles verbatim from `wallpapers.astro` lines 42–53; order 1–12 top-to-bottom):

| # | slug | tag | aspect_ratio | gradient | angle |
|---|------|-----|--------------|----------|-------|
| 1 | drekinn-rising | Dragons | 16/10 | #0b0e14, #15433d, #f2c94c | 160 |
| 2 | aurora-over-esja | Aurora | 16/9 | #070d18, #0e4a52, #3fe0cb | 120 |
| 3 | grid-runner | Synthwave | 3/4 | #120724, #5d1d8f, #00f5d4 | 35 |
| 4 | black-sand-coast | Landscapes | 4/3 | #0d0f10, #33414a, #cfd8d2 | 200 |
| 5 | owl-at-vespers | Cats & Owls | 3/4 | #15120c, #5a4a26, #ecbd3e | 75 |
| 6 | wyrm-of-the-fjord | Dragons | 16/9 | #0a1410, #1e5e46, #9fe8c4 | 145 |
| 7 | neon-reykjavik | Synthwave | 16/10 | #0a0616, #36206e, #f5e642 | 60 |
| 8 | kettle-and-cat | Cats & Owls | 1/1 | #1a100c, #6e3a22, #f0c08a | 100 |
| 9 | green-lady | Aurora | 3/4 | #060a12, #0d3b4f, #7ef0cf | 25 |
| 10 | highlands-in-fog | Landscapes | 16/10 | #101312, #3c4a40, #aebcb0 | 175 |
| 11 | dragon-gate | Dragons | 3/4 | #160a0a, #6e2230, #f2c94c | 10 |
| 12 | arcade-dusk | Synthwave | 4/3 | #0c0618, #8f1d5d, #2cffe5 | 80 |

And `src/content/pages/wallpapers.en.yaml` (head from lines 9–11):

```yaml
slug: wallpapers
locale: en
kicker: Wallpaper stash
title: The Hoard
lede: Placeholder art for now — these tiles generate real downloadable wallpapers. Drop in your own collection later.
```

- [ ] **Step 4: Restore, pull twice, verify, commit**

```bash
pnpm content:restore && pnpm content:pull
git add -A src/content
pnpm content:pull && git status --porcelain src/content   # MUST be empty
pnpm test
git add scripts/lib/collections.mjs src/content/wallpapers src/content/pages
git commit -m "feat(content): wallpapers collection seeded through the snapshot seam"
```

---

## Task 14: Wallpapers Astro collection + `WallpaperTile`

**Files:**
- Modify: `src/content.config.ts` (add `wallpapers`; export — now 6 collections)
- Modify: `tests/content/config-wiring.test.ts` (count 5 → 6)
- Create: `src/components/WallpaperTile.astro`
- Test: append to `tests/render/components.test.ts`

- [ ] **Step 1: Wiring test 5 → 6, expect FAIL; then collection schema:**

```ts
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
    aspect_ratio: z.string(),
    gradient: z.array(z.string()).length(3),
    angle: z.number(),
    title: z.string(),
  }),
});

export const collections = { blog, grimoire, games, pages, countdowns, wallpapers };
```

- [ ] **Step 2: Failing component test**

```ts
import WallpaperTile from '../../src/components/WallpaperTile.astro';

describe('WallpaperTile', () => {
  it('renders art, title, tag chip and lightbox dataset', async () => {
    const c = await AstroContainer.create();
    const html = await c.renderToString(WallpaperTile, {
      props: {
        slug: 'grid-runner', title: 'Grid Runner', tag: 'Synthwave', aspectRatio: '3/4',
        gradient: ['#120724', '#5d1d8f', '#00f5d4'], angle: 35,
      },
    });
    expect(html).toContain('data-tag="Synthwave"');
    expect(html).toContain('data-slug="grid-runner"');
    expect(html).toContain('data-angle="35"');
    expect(html).toContain('data-g0="#120724"');
    expect(html).toContain('aspect-ratio: 3/4');
    expect(html).toContain('ᛞ');
  });
});
```

- [ ] **Step 3: Component**

```astro
---
// src/components/WallpaperTile.astro — one gallery tile. The lightbox and the
// canvas PNG download read everything they need from the data-* attributes.
interface Props { slug: string; title: string; tag: string; aspectRatio: string; gradient: string[]; angle: number }
const { slug, title, tag, aspectRatio, gradient, angle } = Astro.props;
const grad = `linear-gradient(${angle}deg, ${gradient[0]}, ${gradient[1]} 55%, ${gradient[2]} 145%)`;
---
<button
  class="wall-tile" type="button"
  data-slug={slug} data-title={title} data-tag={tag}
  data-g0={gradient[0]} data-g1={gradient[1]} data-g2={gradient[2]} data-angle={angle}
>
  <div class="w-art" style={`aspect-ratio: ${aspectRatio}; background: ${grad};`}>
    <span class="w-mark">ᛞ</span>
  </div>
  <div class="w-bar"><span>{title}</span><span class="chip">{tag}</span></div>
</button>
```

- [ ] **Step 4: Run tests, commit**

```bash
pnpm exec vitest run tests/render/components.test.ts tests/content
git add -A
git commit -m "feat(components): WallpaperTile, wallpapers collection"
```

---

## Task 15: Refactor `wallpapers.astro`

**Files:**
- Modify: `src/pages/wallpapers.astro` (full rewrite)
- Modify: `src/i18n/ui.ts` (en-only: `'wall.all': 'All'`, `'wall.download': 'Download'`, `'wall.close': 'Close'`)
- Modify: `tests/render/pages.test.ts` (remove Wallpapers import/row, same comment pattern)

- [ ] **Step 1: Rewrite the page**

```astro
---
import { getCollection } from 'astro:content';
import Base from "../layouts/Base.astro";
import PageHead from "../components/PageHead.astro";
import WallpaperTile from "../components/WallpaperTile.astro";
import { localizedEntry, uniqueSlugs } from "../i18n/localized";
import { useTranslations } from "../i18n/utils";
import type { Locale } from "../i18n/ui";

const locale = (Astro.currentLocale ?? 'is') as Locale;
const t = useTranslations(locale);

const all = await getCollection('wallpapers');
const walls = uniqueSlugs(all)
  .map((s) => localizedEntry(all, s, locale))
  .filter((w): w is NonNullable<typeof w> => Boolean(w))
  .sort((a, b) => a.data.order - b.data.order);
// First-appearance tag order reproduces the old hardcoded TAGS array.
const tags = [...new Set(walls.map((w) => w.data.tag))];

const pagesAll = await getCollection('pages');
const head = localizedEntry(pagesAll, 'wallpapers', locale)!.data;
---

<Base title="Wallpapers — JonnXor" page="wallpapers">
  <main>
    <PageHead kicker={head.kicker} title={head.title} lede={head.lede} label="Wallpapers head" />

    <div class="container">
      <div class="rune-divider" aria-hidden="true"><span class="runes"></span></div>

      <div class="wall-filters" id="wall-filters" data-screen-label="Tag filters">
        <button class="chip on" type="button" data-tag="All">{t('wall.all')}</button>
        {tags.map((tag) => (
          <button class="chip" type="button" data-tag={tag}>{tag}</button>
        ))}
      </div>
      <section class="masonry" id="masonry" data-screen-label="Masonry grid">
        {walls.map((w) => (
          <WallpaperTile
            slug={w.data.slug} title={w.data.title} tag={w.data.tag}
            aspectRatio={w.data.aspect_ratio} gradient={w.data.gradient} angle={w.data.angle}
          />
        ))}
      </section>
    </div>
  </main>

  <div class="lightbox" id="lightbox" role="dialog" aria-modal="true" aria-label="Wallpaper preview">
    <div class="lb-frame">
      <div class="lb-art" id="lb-art"></div>
      <div class="lb-bar">
        <span class="lb-title" id="lb-title"></span>
        <span class="chip" id="lb-res">1920 × 1080</span>
        <button class="btn btn-gold" type="button" id="lb-download">{t('wall.download')}</button>
        <button class="btn btn-ghost" type="button" id="lb-close">{t('wall.close')}</button>
      </div>
    </div>
  </div>

  <script is:inline slot="end">
(function () {
  'use strict';
  var tiles = document.querySelectorAll('#masonry .wall-tile');
  var chips = document.querySelectorAll('#wall-filters .chip');
  var lb = document.getElementById('lightbox');
  var lbArt = document.getElementById('lb-art');
  var lbTitle = document.getElementById('lb-title');
  var current = null;

  function gradOf(d) {
    return 'linear-gradient(' + d.angle + 'deg, ' + d.g0 + ', ' + d.g1 + ' 55%, ' + d.g2 + ' 145%)';
  }

  chips.forEach(function (chip) {
    chip.addEventListener('click', function () {
      var tag = chip.getAttribute('data-tag');
      chips.forEach(function (c) { c.classList.toggle('on', c === chip); });
      tiles.forEach(function (tile) {
        tile.hidden = tag !== 'All' && tile.getAttribute('data-tag') !== tag;
      });
    });
  });

  document.getElementById('masonry').addEventListener('click', function (e) {
    var tile = e.target.closest('.wall-tile');
    if (!tile) return;
    current = tile.dataset;
    lbArt.style.background = gradOf(current);
    lbTitle.textContent = current.title;
    lb.classList.add('open');
  });

  function closeLb() { lb.classList.remove('open'); }
  document.getElementById('lb-close').addEventListener('click', closeLb);
  lb.addEventListener('click', function (e) { if (e.target === lb) closeLb(); });
  document.addEventListener('keydown', function (e) { if (e.key === 'Escape') closeLb(); });

  // Real download: paint the gradient to a canvas and save it
  document.getElementById('lb-download').addEventListener('click', function () {
    if (!current) return;
    var c = document.createElement('canvas');
    c.width = 1920; c.height = 1080;
    var ctx = c.getContext('2d');
    var rad = (parseInt(current.angle, 10) - 90) * Math.PI / 180;
    var x = Math.cos(rad) * 960, y = Math.sin(rad) * 540;
    var g = ctx.createLinearGradient(960 - x, 540 - y, 960 + x, 540 + y);
    g.addColorStop(0, current.g0);
    g.addColorStop(.55, current.g1);
    g.addColorStop(1, current.g2);
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, 1920, 1080);
    ctx.fillStyle = 'rgba(255,255,255,.5)';
    ctx.font = '600 28px Orbitron, sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText('ᛞ', 960, 556);
    var a = document.createElement('a');
    // Filename derives from the title with the original regex — NOT the slug —
    // so download names stay byte-identical to the pre-port page (e.g. the old
    // name for 'Kettle & Cat' is kettle-cat-…, but its slug is kettle-and-cat).
    a.download = current.title.toLowerCase().replace(/[^a-z0-9]+/g, '-') + '-1920x1080.png';
    a.href = c.toDataURL('image/png');
    a.click();
  });
})();
  </script>
</Base>
```

Keep the existing `<style is:global>` block verbatim (lines 140–178 of the current file) and add one rule at its top:

```css
  .wall-tile[hidden] { display: none; }
```

(`.wall-tile` sets `display: block`, which outranks the UA `[hidden]` rule — the same pin as games.)

- [ ] **Step 2: Update `tests/render/pages.test.ts`** — remove Wallpapers import/row (comment as before).

- [ ] **Step 3: Verify**

```bash
pnpm test && pnpm exec astro check && pnpm build
pnpm test:e2e            # full e2e sweep — search/chrome specs touch every page
pnpm test:visual         # /wallpapers baseline must be UNCHANGED
```

- [ ] **Step 4: Commit + PR into preview**

```bash
git add -A
git commit -m "feat(wallpapers): port The Hoard to Directus-owned collection, server-rendered tiles"
```

---

## Task 16: Phase close-out

- [ ] **Step 1: Full verification sweep**

```bash
pnpm test && pnpm exec astro check && pnpm build && pnpm test:e2e && pnpm test:visual
```

Expected: everything green; build page count unchanged; only home (`/`) remains a future baseline change (slice 5, next plan).

- [ ] **Step 2: Prove the fresh-clone story end-to-end**

```bash
pnpm directus:down
docker volume ls | grep jonnxor   # confirm which volume backs directus/data if wiping
# (optional full proof, destructive to LOCAL Directus only — content is committed:)
# wipe directus/data, then:
pnpm directus:up && pnpm directus:schema && pnpm content:restore && pnpm content:pull
git status --porcelain src/content   # MUST be empty
```

- [ ] **Step 3: Confirm slices 4–7 plan is queued** — portfolio, home, about, cv get `.planning/`-dated design-referencing plan #2; raise it at the next session OPEN.

---

## Definition of done (this plan)

- games, countdowns, wallpapers + their page heads are Directus-owned, snapshot-driven, and render through typed components; hardcoded arrays deleted from the three pages.
- `content:pull`/`content:restore` are descriptor-driven; blog/grimoire snapshot bytes unchanged (`git status` clean after pull).
- Schema reproducible: `setup-schema.mjs` idempotent; `snapshot.yaml` committed.
- Full pyramid green in CI on `preview`; visual baselines unchanged.
