# i18n Phase 3 — Grimoire vertical slice + regression guard — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend the Phase-2 Directus→snapshot→per-locale-render pipeline to the `grimoire` collection (en base + English fallback, no real is/ja prose), and close the Phase-2 `generateId` regression gap with committed, prose-independent unit tests.

**Architecture:** Mirror the blog vertical slice. Add a `grimoire` base + `grimoire_translations` junction in Directus (schema-as-code), seed the 11 existing English docs, and have `content:pull` write per-locale `src/content/grimoire/<slug>.<locale>.yaml`. `docs.astro` localizes the whole grimoire set at build time (single page, no per-locale route), remapping each entry's `id` back to its `slug` so deep-links/localStorage stay locale-stable. Cross-cutting DRY: a shared SDK-client lib for all scripts, a generalized `localizedEntry` helper, and an extracted `localeEntryId` loader-id function that both collections share and that the new tests guard.

**Tech Stack:** Astro 6.4.7 (SSG), `@directus/sdk@22`, Directus 11.3.5 + Postgres 16 (local Docker), `gray-matter` (blog MD), `yaml` (new — grimoire YAML), `vitest@4`.

---

## Execution environment (read first)

- All commands run **inside WSL** in the repo, with tools on PATH via mise. Wrap each command as:
  `wsl bash -lic "cd ~/dev/code/jonnxor.is && mise exec -- <command>"`
  (Plain `git`/`docker`/`mv` don't need `mise exec --`; `pnpm`/`node`/`astro` do.)
- Edit files via the UNC path `\\wsl.localhost\Ubuntu\home\jonnxor\dev\code\jonnxor.is\…`.
- **Inside `wsl bash -lic "…"`, inline `$()` and large nested-quote `node -e` get mangled** — put any non-trivial logic in a `.mjs`/`.sh` file and run the file. Delete temp scripts; never commit them.
- **Directus stack:** `mise exec -- pnpm directus:up` / `directus:down`. The Node scripts hit the published port (`DIRECTUS_URL=http://localhost:8055`) from inside WSL regardless of host networking. `@directus/sdk@22` login is an **object** payload (`client.login({ email, password })`); every SDK script must end by exiting the process (the auth-refresh timer otherwise hangs Node).
- **Branch:** before Task 1, branch off the latest `preview`:
  `git fetch origin && git switch -c feat/grimoire-i18n origin/preview`
- Per-task: subagent implements → spec review → code-quality review → commit. Commit messages below are suggestions.

---

## File structure

| Action | Path | Responsibility |
|---|---|---|
| Create | `src/content/loaders.ts` | `localeEntryId` — the single, tested home for the per-locale collection-id derivation used by both collections. |
| Create | `src/i18n/localized.ts` | Generic `localizedEntry` + `uniqueSlugs` (replaces `src/i18n/blog.ts`). |
| Remove | `src/i18n/blog.ts` | Superseded by `localized.ts`. |
| Create | `scripts/lib/directus-client.mjs` | Shared `connect()` (logged-in client) + `done()` teardown for all SDK scripts. |
| Create | `scripts/lib/grimoire-yaml.mjs` | `parseDoc`/`serializeDoc` for per-locale grimoire YAML. |
| Modify | `directus/scripts/setup-schema.mjs` | Add `grimoire` + `grimoire_translations` model; use shared client. |
| Modify | `scripts/content-import.mjs` | Seed grimoire en base alongside blog; use shared client. |
| Modify | `scripts/content-pull.mjs` | Pull grimoire → per-locale YAML; use shared client. |
| Modify | `scripts/content-restore.mjs` | Restore grimoire from snapshot; use shared client. |
| Modify | `src/content.config.ts` | grimoire locale-aware schema; both loaders use `localeEntryId`. |
| Modify | `src/pages/docs.astro` | Locale-aware grimoire; stable `id = slug`. |
| Modify | `src/pages/blog.astro`, `src/pages/blog/[slug].astro` | Import the renamed helper. |
| Modify | `directus/schema/snapshot.yaml` | Regenerated to include grimoire. |
| Modify | `package.json` | `yaml` devDep; `directus:snapshot` script. |
| Create | `tests/content/loaders.test.ts`, `tests/i18n/localized.test.ts`, `tests/content/grimoire-yaml.test.ts` | Guard + helper tests. |
| Remove | `tests/i18n/blog.test.ts` | Renamed to `localized.test.ts`. |
| Create | `C:\Dev\Notes\projects\jonnxor.is\2026-06-26-authoring-en-base-invariant.md` | Authoring note. |

---

## Task 1: Extract `localeEntryId` and wire the blog loader

**Files:**
- Create: `src/content/loaders.ts`
- Test: `tests/content/loaders.test.ts`
- Modify: `src/content.config.ts` (blog loader's inline `generateId`)

- [ ] **Step 1: Write the failing test**

Create `tests/content/loaders.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import { localeEntryId } from '../../src/content/loaders';

describe('localeEntryId', () => {
  it('keeps a slug\'s locale files distinct (md)', () => {
    expect(localeEntryId({ entry: 'foo.en.md' })).toBe('foo.en');
    expect(localeEntryId({ entry: 'foo.is.md' })).toBe('foo.is');
    expect(localeEntryId({ entry: 'foo.en.md' })).not.toBe(localeEntryId({ entry: 'foo.is.md' }));
  });
  it('keeps a slug\'s locale files distinct (yaml)', () => {
    expect(localeEntryId({ entry: 'bar.en.yaml' })).toBe('bar.en');
    expect(localeEntryId({ entry: 'bar.ja.yaml' })).toBe('bar.ja');
    expect(localeEntryId({ entry: 'bar.en.yaml' })).not.toBe(localeEntryId({ entry: 'bar.ja.yaml' }));
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `wsl bash -lic "cd ~/dev/code/jonnxor.is && mise exec -- pnpm test -- loaders"`
Expected: FAIL — cannot resolve `../../src/content/loaders`.

- [ ] **Step 3: Write minimal implementation**

Create `src/content/loaders.ts`:

```ts
/**
 * Derive a collection id from a per-locale content filename, keeping the locale
 * suffix so a slug's locale files stay distinct entries. Astro's glob loader
 * otherwise derives the id from the frontmatter `slug` (blog) or the bare
 * filename (grimoire), which is identical across `<slug>.en` / `<slug>.is` —
 * they'd collide on one id and silently overwrite each other (the bug that hit
 * Phase 2: the en base vanished the instant a second locale file appeared).
 */
export function localeEntryId({ entry }: { entry: string }): string {
  return entry.replace(/\.(md|yaml)$/, '');
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `wsl bash -lic "cd ~/dev/code/jonnxor.is && mise exec -- pnpm test -- loaders"`
Expected: PASS (2 tests).

- [ ] **Step 5: Wire the blog loader to use it**

In `src/content.config.ts`, add the import at the top (after the existing imports):

```ts
import { localeEntryId } from './content/loaders';
```

Replace the blog loader's inline `generateId` with the shared function. Change:

```ts
  loader: glob({
    pattern: '**/*.md',
    base: './src/content/blog',
    generateId: ({ entry }) => entry.replace(/\.md$/, ''),
  }),
```

to:

```ts
  loader: glob({
    pattern: '**/*.md',
    base: './src/content/blog',
    generateId: localeEntryId,
  }),
```

- [ ] **Step 6: Verify the build is unchanged**

Run: `wsl bash -lic "cd ~/dev/code/jonnxor.is && mise exec -- pnpm astro check && mise exec -- pnpm build"`
Expected: 0 errors; 15 pages (no change vs before).

- [ ] **Step 7: Commit**

```bash
git add src/content/loaders.ts tests/content/loaders.test.ts src/content.config.ts
git commit -m "refactor(content): extract localeEntryId loader helper (tested guard for the generateId fix)"
```

---

## Task 2: Generalize the localized-entry helper

**Files:**
- Create: `src/i18n/localized.ts`
- Create: `tests/i18n/localized.test.ts`
- Remove: `src/i18n/blog.ts`, `tests/i18n/blog.test.ts`
- Modify: `src/pages/blog.astro`, `src/pages/blog/[slug].astro`

- [ ] **Step 1: Write the failing test**

Create `tests/i18n/localized.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import { localizedEntry, uniqueSlugs } from '../../src/i18n/localized';

const entries = [
  { id: 'a.en', data: { slug: 'a', locale: 'en' as const, title: 'A' } },
  { id: 'a.is', data: { slug: 'a', locale: 'is' as const, title: 'Á' } },
  { id: 'b.en', data: { slug: 'b', locale: 'en' as const, title: 'B' } },
  { id: 'c.ja', data: { slug: 'c', locale: 'ja' as const, title: 'C' } },
];

describe('localizedEntry', () => {
  it('returns the requested locale when present', () => {
    expect(localizedEntry(entries, 'a', 'is')?.data.title).toBe('Á');
  });
  it('falls back to en when the locale is missing', () => {
    expect(localizedEntry(entries, 'b', 'is')?.data.title).toBe('B');
  });
  it('returns undefined for an unknown slug', () => {
    expect(localizedEntry(entries, 'z', 'en')).toBeUndefined();
  });
  it('returns undefined when only a non-fallback, non-requested locale exists', () => {
    expect(localizedEntry(entries, 'c', 'is')).toBeUndefined();
  });
});

describe('uniqueSlugs', () => {
  it('dedupes slugs across locales', () => {
    expect(uniqueSlugs(entries).sort()).toEqual(['a', 'b', 'c']);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `wsl bash -lic "cd ~/dev/code/jonnxor.is && mise exec -- pnpm test -- localized"`
Expected: FAIL — cannot resolve `../../src/i18n/localized`.

- [ ] **Step 3: Write the implementation**

Create `src/i18n/localized.ts`:

```ts
import { fallbackLocale, type Locale } from './ui';

type Entry = { id: string; data: { slug: string; locale: Locale; [k: string]: unknown } };

/** The entry for `slug` in `locale`, else the English-fallback entry, else undefined. */
export function localizedEntry<T extends Entry>(entries: T[], slug: string, locale: Locale): T | undefined {
  return (
    entries.find((e) => e.data.slug === slug && e.data.locale === locale) ??
    entries.find((e) => e.data.slug === slug && e.data.locale === fallbackLocale)
  );
}

/** Unique slugs across all locale entries. */
export function uniqueSlugs(entries: Entry[]): string[] {
  return [...new Set(entries.map((e) => e.data.slug))];
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `wsl bash -lic "cd ~/dev/code/jonnxor.is && mise exec -- pnpm test -- localized"`
Expected: PASS (5 tests).

- [ ] **Step 5: Update the blog consumers and delete the old files**

In `src/pages/blog.astro`, change:

```ts
import { localizedPost, uniqueSlugs } from "../i18n/blog";
```
to:
```ts
import { localizedEntry, uniqueSlugs } from "../i18n/localized";
```
and change the one call site `localizedPost(all, slug, locale)` to `localizedEntry(all, slug, locale)`.

In `src/pages/blog/[slug].astro`, change:

```ts
import { localizedPost, uniqueSlugs } from "../../i18n/blog";
```
to:
```ts
import { localizedEntry, uniqueSlugs } from "../../i18n/localized";
```
and change the one call site `localizedPost(all, slug, locale)` to `localizedEntry(all, slug, locale)`.

Delete the superseded files:

```bash
git rm src/i18n/blog.ts tests/i18n/blog.test.ts
```

- [ ] **Step 6: Verify build + tests**

Run: `wsl bash -lic "cd ~/dev/code/jonnxor.is && mise exec -- pnpm test && mise exec -- pnpm astro check && mise exec -- pnpm build"`
Expected: all tests pass (now includes loaders + localized, blog.test gone); 0 check errors; 15 pages.

- [ ] **Step 7: Commit**

```bash
git add -A src/i18n/localized.ts tests/i18n/localized.test.ts src/pages/blog.astro "src/pages/blog/[slug].astro"
git commit -m "refactor(i18n): generalize localizedPost -> localizedEntry in localized.ts"
```

---

## Task 3: Grimoire YAML parse/serialize lib

**Files:**
- Create: `scripts/lib/grimoire-yaml.mjs`
- Create: `tests/content/grimoire-yaml.test.ts`
- Modify: `package.json` (add `yaml` devDep)

- [ ] **Step 1: Add the `yaml` dependency**

Run: `wsl bash -lic "cd ~/dev/code/jonnxor.is && mise exec -- pnpm add -D yaml"`
Expected: `yaml` appears in `devDependencies`.

- [ ] **Step 2: Write the failing test**

Create `tests/content/grimoire-yaml.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import { parseDoc, serializeDoc } from '../../scripts/lib/grimoire-yaml.mjs';

const record = {
  slug: 'go-errors', locale: 'en',
  order: 7, realm: 'code', cat: 'Code patterns',
  title: 'Go error wrapping', tags: ['go', 'errors'],
  updated: '2026-05-26',
  body: '<p>Line one.</p>\n<h2>Heading</h2>\n<p>Line two.</p>',
};

describe('serializeDoc', () => {
  it('emits body as a block literal and round-trips', () => {
    const out = serializeDoc(record);
    expect(out).toContain('slug: go-errors');
    expect(out).toContain('locale: en');
    expect(out).toContain('body: |');           // block literal, not inline quoted
    const back = parseDoc(out);
    expect(back.slug).toBe('go-errors');
    expect(back.order).toBe(7);
    expect(back.tags).toEqual(['go', 'errors']);
    expect(back.body).toBe(record.body);
  });
  it('omits game when absent', () => {
    expect(serializeDoc(record)).not.toContain('game:');
  });
  it('includes game when present', () => {
    expect(serializeDoc({ ...record, game: 'Elden Ring' })).toContain('game: Elden Ring');
  });
  it('is deterministic (re-serialize a parsed file is identical)', () => {
    const out = serializeDoc(record);
    expect(serializeDoc(parseDoc(out))).toBe(out);
  });
});
```

- [ ] **Step 3: Run test to verify it fails**

Run: `wsl bash -lic "cd ~/dev/code/jonnxor.is && mise exec -- pnpm test -- grimoire-yaml"`
Expected: FAIL — cannot resolve `grimoire-yaml.mjs`.

- [ ] **Step 4: Write the implementation**

Create `scripts/lib/grimoire-yaml.mjs`:

```js
import YAML from 'yaml';

/** Parse a per-locale grimoire YAML file string into a plain record object. */
export function parseDoc(raw) {
  return YAML.parse(raw);
}

/**
 * Serialize a grimoire record into a per-locale YAML file string. Keys are
 * written in a fixed order for stable diffs; `body` (rich HTML) is forced to a
 * literal block scalar and line-wrapping is disabled so long HTML lines aren't
 * folded. `game` is omitted when not set.
 */
export function serializeDoc(r) {
  const obj = {
    slug: r.slug,
    locale: r.locale,
    order: r.order,
    realm: r.realm,
    ...(r.game ? { game: r.game } : {}),
    cat: r.cat,
    title: r.title,
    tags: r.tags ?? [],
    updated: r.updated,
    body: r.body ?? '',
  };
  const doc = new YAML.Document(obj);
  const bodyNode = doc.get('body', true);
  if (bodyNode) bodyNode.type = 'BLOCK_LITERAL';
  return doc.toString({ lineWidth: 0 });
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `wsl bash -lic "cd ~/dev/code/jonnxor.is && mise exec -- pnpm test -- grimoire-yaml"`
Expected: PASS (4 tests).

- [ ] **Step 6: Commit**

```bash
git add scripts/lib/grimoire-yaml.mjs tests/content/grimoire-yaml.test.ts package.json pnpm-lock.yaml
git commit -m "feat(content): grimoire per-locale YAML parse/serialize lib"
```

---

## Task 4: Shared Directus SDK-client lib + refactor existing scripts

**Files:**
- Create: `scripts/lib/directus-client.mjs`
- Modify: `directus/scripts/setup-schema.mjs`, `scripts/content-import.mjs`, `scripts/content-pull.mjs`, `scripts/content-restore.mjs`

**Prerequisite:** Directus stack running (`mise exec -- pnpm directus:up`) for the verification step.

- [ ] **Step 1: Create the shared client**

Create `scripts/lib/directus-client.mjs`:

```js
import { createDirectus, rest, authentication } from '@directus/sdk';

/**
 * A logged-in Directus SDK client for the local stack. Uses the object-payload
 * login required by @directus/sdk@22 (positional throws a TypeError).
 */
export async function connect() {
  const client = createDirectus(process.env.DIRECTUS_URL).with(rest()).with(authentication());
  await client.login({ email: process.env.ADMIN_EMAIL, password: process.env.ADMIN_PASSWORD });
  return client;
}

/**
 * Exit the process explicitly. The authenticated client schedules a token-refresh
 * timer that keeps Node's event loop alive, which would otherwise hang the script
 * after its work is done (and break `&&` chaining).
 */
export function done(code = 0) {
  process.exit(code);
}
```

- [ ] **Step 2: Refactor `directus/scripts/setup-schema.mjs`**

Replace the connection preamble. Change the top:

```js
import {
  createDirectus, rest, authentication,
  createCollection, createField, createRelation, readCollections,
  readFieldsByCollection, updateField,
} from '@directus/sdk';

const url = process.env.DIRECTUS_URL;
const client = createDirectus(url).with(rest()).with(authentication());
await client.login({ email: process.env.ADMIN_EMAIL, password: process.env.ADMIN_PASSWORD });
```

to:

```js
import {
  createCollection, createField, createRelation, readCollections,
  readFieldsByCollection, updateField,
} from '@directus/sdk';
import { connect, done } from '../../scripts/lib/directus-client.mjs';

const client = await connect();
```

Replace the final `process.exit(0);` (and its comment) with:

```js
done();
```

- [ ] **Step 3: Refactor the three `scripts/content-*.mjs`**

In each of `scripts/content-import.mjs`, `scripts/content-pull.mjs`, `scripts/content-restore.mjs`:

Replace:
```js
import { createDirectus, rest, authentication, readItems, createItem } from '@directus/sdk';
```
(or the pull variant `import { createDirectus, rest, authentication, readItems } from '@directus/sdk';`)
with the same named imports **minus** `createDirectus, rest, authentication`, plus the shared client. For `content-import.mjs` and `content-restore.mjs`:
```js
import { readItems, createItem } from '@directus/sdk';
import { connect, done } from './lib/directus-client.mjs';
```
For `content-pull.mjs`:
```js
import { readItems } from '@directus/sdk';
import { connect, done } from './lib/directus-client.mjs';
```

In each, replace:
```js
const client = createDirectus(process.env.DIRECTUS_URL).with(rest()).with(authentication());
await client.login({ email: process.env.ADMIN_EMAIL, password: process.env.ADMIN_PASSWORD });
```
with:
```js
const client = await connect();
```

And replace each script's trailing `process.exit(0);` (and the comment block above it) with:
```js
done();
```

- [ ] **Step 4: Verify the refactor against the live stack**

Run:
```bash
wsl bash -lic "cd ~/dev/code/jonnxor.is && mise exec -- pnpm directus:up && sleep 8 && mise exec -- pnpm content:pull && git status --porcelain src/content/blog"
```
Expected: `content:pull` runs, exits cleanly (no hang), and prints `pulled — 5 locale file(s) from 5 post(s)`; `git status` shows **no changes** to `src/content/blog` (blog snapshot still deterministic through the refactored client).

- [ ] **Step 5: Commit**

```bash
git add scripts/lib/directus-client.mjs directus/scripts/setup-schema.mjs scripts/content-import.mjs scripts/content-pull.mjs scripts/content-restore.mjs
git commit -m "refactor(scripts): share one logged-in Directus client + clean-exit helper"
```

---

## Task 5: Grimoire content model (schema-as-code) + snapshot regen

**Files:**
- Modify: `directus/scripts/setup-schema.mjs`
- Modify: `package.json` (add `directus:snapshot` script)
- Modify: `directus/schema/snapshot.yaml` (regenerated)

**Prerequisite:** Directus stack running.

- [ ] **Step 1: Add the grimoire model to `setup-schema.mjs`**

After the `blog_translations` block (and before the `// 4. seed the three languages` section), insert:

```js
// 5. grimoire (base, non-translatable)
if (need('grimoire')) {
  await client.request(createCollection({
    collection: 'grimoire',
    meta: { icon: 'menu_book', note: 'The Grimoire — guides & cheat sheets' },
    schema: {},
    fields: [
      { field: 'id', type: 'integer', schema: { is_primary_key: true, has_auto_increment: true }, meta: { hidden: true } },
      { field: 'slug', type: 'string', schema: { is_unique: true }, meta: { interface: 'input', required: true } },
      { field: 'order', type: 'integer', meta: { interface: 'input' } },
      { field: 'realm', type: 'string', meta: { interface: 'select-dropdown', options: { choices: [{ text: 'games', value: 'games' }, { text: 'code', value: 'code' }, { text: 'life', value: 'life' }] } } },
      { field: 'game', type: 'string', schema: { is_nullable: true }, meta: { interface: 'input' } },
      { field: 'updated', type: 'string', meta: { interface: 'input' } },
    ],
  }));
}

// 6. grimoire_translations (junction)
if (need('grimoire_translations')) {
  await client.request(createCollection({
    collection: 'grimoire_translations',
    meta: { hidden: true },
    schema: {},
    fields: [
      { field: 'id', type: 'integer', schema: { is_primary_key: true, has_auto_increment: true }, meta: { hidden: true } },
      { field: 'grimoire', type: 'integer', meta: { hidden: true } },
      { field: 'languages_code', type: 'string', meta: { hidden: true } },
      { field: 'title', type: 'string', meta: { interface: 'input' } },
      { field: 'cat', type: 'string', meta: { interface: 'input' } },
      { field: 'tags', type: 'json', meta: { interface: 'tags' } },
      { field: 'body', type: 'text', meta: { interface: 'input-rich-text-md' } },
    ],
  }));

  // translations alias on grimoire
  await client.request(createField('grimoire', {
    field: 'translations', type: 'alias',
    meta: { interface: 'translations', special: ['translations'], options: { languageField: 'code' } },
  }));

  // relations: grimoire_translations.grimoire -> grimoire (O2M via "translations"); .languages_code -> languages
  await client.request(createRelation({
    collection: 'grimoire_translations', field: 'grimoire', related_collection: 'grimoire',
    meta: { one_field: 'translations', junction_field: 'languages_code' }, schema: { on_delete: 'SET NULL' },
  }));
  await client.request(createRelation({
    collection: 'grimoire_translations', field: 'languages_code', related_collection: 'languages',
    meta: { junction_field: 'grimoire' }, schema: { on_delete: 'SET NULL' },
  }));
}
```

- [ ] **Step 2: Apply the schema to the running stack**

Run: `wsl bash -lic "cd ~/dev/code/jonnxor.is && mise exec -- pnpm directus:schema"`
Expected: prints `schema setup complete`, exits cleanly. (Idempotent — re-running makes no further changes.)

- [ ] **Step 3: Add a reproducible snapshot script**

In `package.json` `scripts`, add (after `directus:schema`):

```json
    "directus:snapshot": "docker compose -f directus/docker-compose.yml exec -T directus directus schema snapshot --yes /directus/snapshot.yaml && docker compose -f directus/docker-compose.yml exec -T directus cat /directus/snapshot.yaml > directus/schema/snapshot.yaml",
```

- [ ] **Step 4: Regenerate the committed snapshot**

Run: `wsl bash -lic "cd ~/dev/code/jonnxor.is && mise exec -- pnpm directus:snapshot && git diff --stat directus/schema/snapshot.yaml"`
Expected: `snapshot.yaml` now contains `grimoire` and `grimoire_translations` collections/fields/relations; the diff is additive (the existing blog/languages model unchanged). Confirm by checking the file contains `collection: grimoire` and `collection: grimoire_translations`.

- [ ] **Step 5: Commit**

```bash
git add directus/scripts/setup-schema.mjs directus/schema/snapshot.yaml package.json
git commit -m "feat(directus): grimoire content model (base + translations) as schema-as-code"
```

---

## Task 6: Seed the 11 English grimoire docs

**Files:**
- Modify: `scripts/content-import.mjs`

**Prerequisite:** Directus running with the grimoire schema (Tasks 4–5). The un-suffixed `src/content/grimoire/<slug>.yaml` files still present (not yet pulled).

- [ ] **Step 1: Extend `content-import.mjs` to seed grimoire**

At the top, add the YAML lib import alongside the existing imports:

```js
import { parseDoc } from './lib/grimoire-yaml.mjs';
```

After the existing blog import loop (after its `console.log(\`done — ${created} created, ...\`)` line and before `done();`), insert a grimoire seed block:

```js
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
```

(`readdir`, `readFile`, `join` are already imported at the top of the file from Phase 2.)

- [ ] **Step 2: Run the seed**

Run: `wsl bash -lic "cd ~/dev/code/jonnxor.is && mise exec -- pnpm content:import"`
Expected: prints `imported grimoire: <slug>` for 11 docs, then `grimoire — 11 created, 0 pre-existing`. Re-running prints all `skip grimoire (exists)` (idempotent).

- [ ] **Step 3: Commit**

```bash
git add scripts/content-import.mjs
git commit -m "feat(content): seed existing grimoire docs into Directus (en base)"
```

---

## Task 7: `content:pull` writes the per-locale grimoire snapshot

**Files:**
- Modify: `scripts/content-pull.mjs`

**Prerequisite:** Directus running, grimoire seeded (Task 6).

- [ ] **Step 1: Extend `content-pull.mjs` for grimoire**

Add the serializer import near the top, alongside the post-markdown import:

```js
import { serializeDoc } from './lib/grimoire-yaml.mjs';
```

After the blog pull's prune loop and its `console.log(\`pulled — ${wanted.size} ...\`)`, and **before** `done();`, insert:

```js
// --- grimoire ---
const GRIMOIRE_DIR = 'src/content/grimoire';
const docs = await client.request(readItems('grimoire', {
  limit: -1,
  fields: ['slug', 'order', 'realm', 'game', 'updated', { translations: ['languages_code', 'title', 'cat', 'tags', 'body'] }],
}));

const wantedDocs = new Set();
for (const doc of docs) {
  for (const t of doc.translations ?? []) {
    const file = `${doc.slug}.${t.languages_code}.yaml`;
    wantedDocs.add(file);
    const out = serializeDoc({
      slug: doc.slug, locale: t.languages_code,
      order: doc.order, realm: doc.realm, game: doc.game ?? undefined,
      cat: t.cat, title: t.title, tags: t.tags ?? [], updated: doc.updated, body: t.body ?? '',
    });
    await writeFile(join(GRIMOIRE_DIR, file), out, 'utf8');
  }
}

// prune: drop generated locale files no longer backed by Directus, plus the
// original un-suffixed *.yaml (now superseded by <slug>.en.yaml)
for (const ent of await readdir(GRIMOIRE_DIR, { withFileTypes: true })) {
  if (!ent.isFile()) continue;
  const f = ent.name;
  const isLocaleFile = /\.(is|en|ja)\.yaml$/.test(f);
  const isLegacy = f.endsWith('.yaml') && !isLocaleFile;
  if ((isLocaleFile && !wantedDocs.has(f)) || isLegacy) {
    await unlink(join(GRIMOIRE_DIR, f));
    console.log('removed:', f);
  }
}
console.log(`pulled grimoire — ${wantedDocs.size} locale file(s) from ${docs.length} doc(s)`);
```

- [ ] **Step 2: Run the pull**

Run: `wsl bash -lic "cd ~/dev/code/jonnxor.is && mise exec -- pnpm content:pull"`
Expected: writes 11 `<slug>.en.yaml`, removes the 11 un-suffixed originals, prints `pulled grimoire — 11 locale file(s) from 11 doc(s)`.

- [ ] **Step 3: Verify determinism**

Run: `wsl bash -lic "cd ~/dev/code/jonnxor.is && mise exec -- pnpm content:pull && git status --porcelain src/content/grimoire"`
Expected: the second pull changes nothing new — `git status` shows only the rename/reformat from Step 2 (the un-suffixed → `.en.yaml` swap), and re-running produces **no further diff**. Spot-check one file (e.g. `src/content/grimoire/go-errors.en.yaml`) has `slug:`, `locale: en`, `body: |`.

- [ ] **Step 4: Commit**

```bash
git add scripts/content-pull.mjs src/content/grimoire
git commit -m "feat(content): content:pull writes per-locale grimoire snapshot from Directus"
```

---

## Task 8: `content:restore` rehydrates grimoire from the snapshot

**Files:**
- Modify: `scripts/content-restore.mjs`

**Prerequisite:** Directus running; grimoire `<slug>.en.yaml` snapshot committed (Task 7).

- [ ] **Step 1: Extend `content-restore.mjs` for grimoire**

Add the parser import near the top, alongside the post-markdown import:

```js
import { parseDoc } from './lib/grimoire-yaml.mjs';
```

After the blog restore loop and its `console.log(\`done — ${created} restored, ...\`)`, and **before** `done();`, insert:

```js
// --- grimoire: group <slug>.<locale>.yaml by slug -> one grimoire item + translations ---
const GRIMOIRE_DIR = 'src/content/grimoire';
const docFiles = (await readdir(GRIMOIRE_DIR)).filter((f) => /\.(is|en|ja)\.yaml$/.test(f));
const docsBySlug = new Map();
for (const file of docFiles) {
  const d = parseDoc(await readFile(join(GRIMOIRE_DIR, file), 'utf8'));
  if (!docsBySlug.has(d.slug)) {
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
```

(`readdir`, `readFile`, `join` are already imported at the top from Phase 2.)

- [ ] **Step 2: Verify against the live stack (idempotent skip)**

Run: `wsl bash -lic "cd ~/dev/code/jonnxor.is && mise exec -- pnpm content:restore"`
Expected: since the 11 grimoire docs already exist, prints `skip grimoire (exists)` for each, then `grimoire done — 0 restored, 11 skipped, 11 doc(s) in snapshot`. (Full rehydrate-from-empty is covered by the Phase-2 re-bootstrap procedure; this confirms grouping + idempotency.)

- [ ] **Step 3: Commit**

```bash
git add scripts/content-restore.mjs
git commit -m "feat(content): content:restore rehydrates grimoire from the committed snapshot"
```

---

## Task 9: Make the grimoire collection locale-aware

**Files:**
- Modify: `src/content.config.ts`

- [ ] **Step 1: Update the grimoire collection definition**

In `src/content.config.ts`, replace the entire `grimoire` collection block:

```ts
const grimoire = defineCollection({
  loader: glob({ pattern: '**/*.yaml', base: './src/content/grimoire' }),
  schema: z.object({
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
```

with:

```ts
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
```

- [ ] **Step 2: Verify the collection type-checks**

Run: `wsl bash -lic "cd ~/dev/code/jonnxor.is && mise exec -- pnpm astro check"`
Expected: 0 errors. (`docs.astro` still reads `order`/`cat`/etc. and now also has `slug`/`locale` available; it is updated in Task 10.)

- [ ] **Step 3: Commit**

```bash
git add src/content.config.ts
git commit -m "feat(content): grimoire collection is locale-aware (slug + locale, shared id)"
```

---

## Task 10: Render `/docs` per-locale with stable ids

**Files:**
- Modify: `src/pages/docs.astro`

**Prerequisite:** grimoire `.en.yaml` snapshot present (Task 7); collection locale-aware (Task 9).

- [ ] **Step 1: Make the frontmatter locale-aware**

In `src/pages/docs.astro`, replace the frontmatter block:

```ts
---
import { getCollection } from "astro:content";
import Base from "../layouts/Base.astro";

// Feed the (unchanged) client-side reader from the grimoire collection instead of
// a hand-maintained JS blob. Sort by the explicit `order` field to keep the curated
// category sequence; shape each entry to exactly what the reader expects, then
// serialise (escaping "<" so no doc body can break out of the <script>).
const docs = (await getCollection("grimoire"))
  .sort((a, b) => a.data.order - b.data.order)
  .map((e) => ({ id: e.id, ...e.data }));
const docsJson = JSON.stringify(docs).replace(/</g, "\\u003c");
---
```

with:

```ts
---
import { getCollection } from "astro:content";
import Base from "../layouts/Base.astro";
import { localizedEntry, uniqueSlugs } from "../i18n/localized";
import { defaultLocale, type Locale } from "../i18n/ui";

// Feed the (unchanged) client-side reader from the grimoire collection. For the
// current locale, pick each doc's locale entry or its English fallback, sort by the
// curated `order`, and shape to exactly what the reader expects. The serialised `id`
// is the locale-stable `slug` (not `<slug>.<locale>`) so deep-links (?doc=) and the
// saved reading position survive a language switch. Escape "<" so no body can break
// out of the <script>.
const locale = (Astro.currentLocale ?? defaultLocale) as Locale;
const all = await getCollection("grimoire");
const docs = uniqueSlugs(all)
  .map((slug) => localizedEntry(all, slug, locale))
  .filter((e): e is NonNullable<typeof e> => e != null)
  .map((e) => ({ ...e.data, id: e.data.slug }))
  .sort((a, b) => a.order - b.order);
const docsJson = JSON.stringify(docs).replace(/</g, "\\u003c");
---
```

(Everything below the frontmatter — the markup and the entire client-side reader script — is unchanged.)

- [ ] **Step 2: Build with Directus STOPPED (prove no build-time dependency)**

Run:
```bash
wsl bash -lic "cd ~/dev/code/jonnxor.is && mise exec -- pnpm directus:down && mise exec -- pnpm astro check && mise exec -- pnpm build"
```
Expected: 0 check errors; build succeeds (15 pages) with the Directus stack down.

- [ ] **Step 3: Verify the rendered output**

Run:
```bash
wsl bash -lic "cd ~/dev/code/jonnxor.is && grep -c 'JX_DOCS' dist/docs/index.html && grep -o '\"id\":\"go-errors\"' dist/docs/index.html | head -1 && grep -c 'JX_DOCS' dist/en/docs/index.html && grep -c 'JX_DOCS' dist/ja/docs/index.html"
```
Expected: `JX_DOCS` present in `/docs`, `/en/docs`, and `/ja/docs`; the id is the stable slug `go-errors` (not `go-errors.en`). All three render the 11 English docs (only `en` exists).

- [ ] **Step 4: Commit**

```bash
git add src/pages/docs.astro
git commit -m "feat(i18n): render /docs per-locale from the grimoire snapshot (en fallback, stable ids)"
```

---

## Task 11: Document the en-base invariant for authors

**Files:**
- Create: `C:\Dev\Notes\projects\jonnxor.is\2026-06-26-authoring-en-base-invariant.md`

- [ ] **Step 1: Write the authoring note**

Create the file with:

```markdown
# Authoring note — the English-base invariant (blog + grimoire)

**Date:** 2026-06-26

Every blog post and grimoire doc MUST have an English (`en`) translation. English is
the load-bearing fallback: `is`/`ja` render their own text when present and fall back
to `en` otherwise (`localizedEntry` in `src/i18n/localized.ts`).

- **Blog:** a post with no `en` base fails the build for ALL locales — `src/pages/blog/[slug].astro`
  throws a clear "Every post needs an en base" error rather than crashing opaquely.
- **Grimoire:** a doc with no `en` base is silently dropped from `/docs` for locales that
  also lack it (it has no fallback to resolve to). Always author `en` first.

**Authoring loop:** edit in local Directus → `pnpm content:pull` → review the per-locale
diff → commit. `pnpm content:restore` rehydrates a fresh/empty Directus from the committed
snapshot. Per-locale files: `src/content/blog/<slug>.<locale>.md`,
`src/content/grimoire/<slug>.<locale>.yaml`.

**Regression guard:** `localeEntryId` (`src/content/loaders.ts`) keeps a slug's locale
files from colliding on one collection id (the Phase-2 bug). `tests/content/loaders.test.ts`
and `tests/i18n/localized.test.ts` guard this without needing real translated prose.
```

- [ ] **Step 2: Commit (only if the KB is git-tracked; otherwise skip)**

The note lives in the external KB (`C:\Dev\Notes`), not the code repo. No repo commit needed. If the KB is version-controlled, commit it there.

---

## Task 12: Final full verification

**Files:** none (verification only).

- [ ] **Step 1: Full unit suite**

Run: `wsl bash -lic "cd ~/dev/code/jonnxor.is && mise exec -- pnpm test"`
Expected: all green — the 15 Phase-2 tests plus the new `loaders` (2), `localized` (5), and `grimoire-yaml` (4) tests.

- [ ] **Step 2: Type check + build with Directus stopped**

Run: `wsl bash -lic "cd ~/dev/code/jonnxor.is && mise exec -- pnpm directus:down && mise exec -- pnpm astro check && mise exec -- pnpm build"`
Expected: 0 errors; build succeeds (15 pages) with no Directus dependency.

- [ ] **Step 3: Snapshot reproducibility (schema)**

Run: `wsl bash -lic "cd ~/dev/code/jonnxor.is && mise exec -- pnpm directus:up && sleep 8 && mise exec -- pnpm directus:schema"`
Expected: `schema setup complete`, no errors — the committed `setup-schema.mjs` + `snapshot.yaml` reproduce the grimoire model on the existing DB idempotently.

- [ ] **Step 4: Confirm clean git state**

Run: `wsl bash -lic "cd ~/dev/code/jonnxor.is && git status -sb"`
Expected: branch `feat/grimoire-i18n`, working tree clean (all work committed), ready for PR into `preview`.

- [ ] **Step 5: Stop the stack**

Run: `wsl bash -lic "cd ~/dev/code/jonnxor.is && mise exec -- pnpm directus:down"`

---

## Done criteria

- Grimoire renders per-locale from the committed YAML snapshot with English fallback; `/docs`, `/en/docs`, `/ja/docs` all build with Directus stopped.
- Deep-links (`?doc=<slug>`) and saved reading position (`localStorage('jx-doc')`) are locale-stable.
- `content:pull`/`content:restore`/seed handle grimoire alongside blog; pull is deterministic.
- The `generateId` regression is guarded by `loaders.test.ts` + `localized.test.ts` (no real prose required).
- One shared Directus client across all SDK scripts; one shared `localizedEntry`; one shared `localeEntryId`.
- All scripts exit cleanly; full unit suite + `astro check` + build green.

## Out of scope (per spec)

Real is/ja prose authoring; pages/games collections; production Directus hosting; any visual change to the grimoire reader UI.
