# i18n Phase 2 — Directus content layer (blog slice) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prove the Directus content pipeline end-to-end on the `blog` collection — local Directus (Postgres) holds blog content + is/en/ja translations, a `content:pull` script writes a per-locale Markdown snapshot into the repo, and Astro renders blog per-locale (English fallback) from that committed snapshot.

**Architecture:** A project-local `directus/` Docker stack (Postgres + Directus on `:8055`) is the *authoring* tool. Node scripts (`@directus/sdk`) create the schema, import the 5 existing posts as the English base, and pull all translations into `src/content/blog/<slug>.<locale>.md`. Astro builds from those committed files only — CI/Vercel never touch Directus. Blog pages select content by `Astro.currentLocale` (reusing Phase-1 fallback-rewrite), falling back to `en`.

**Tech Stack:** Astro 6 (SSG), Directus 11 + Postgres 16 via Docker Compose, `@directus/sdk`, `gray-matter` (frontmatter), Node 24 (`--env-file`), Vitest, pnpm via `mise exec --`.

**Spec:** [`2026-06-24-i18n-phase2-directus-blog-design.md`](./2026-06-24-i18n-phase2-directus-blog-design.md)

---

## Execution environment

- Repo in **WSL**: `~/dev/code/jonnxor.is`. Run commands via `wsl bash -lic "cd ~/dev/code/jonnxor.is && <cmd>"` (the `-lic` loads mise/direnv/docker on PATH). Edit files via the UNC path `\\wsl.localhost\Ubuntu\home\jonnxor\dev\code\jonnxor.is\`. git runs in WSL. **Don't push** (esp. not `preview`/`main`).
- **pnpm via mise:** `mise exec -- pnpm …`. **Node via mise:** `mise exec -- node …`.
- **Docker** runs in WSL; Directus serves on `localhost:8055`. **Scripts run inside WSL**, so they reach `localhost:8055` regardless of the Windows↔WSL relay issue. (The Directus **admin UI** from a Windows browser needs the host reboot — not required for this plan; everything here is scripted/CLI.)
- **Branch:** off `preview` → `feat/directus-blog`.
- **Applying edits:** Read each file first; anchor on quoted code, not line numbers.

## File structure

| File | Responsibility |
|---|---|
| `directus/docker-compose.yml` | Postgres 16 + Directus 11 services, health-checked |
| `directus/.env.example` | Documents env names (committed) |
| `directus/.env` | Real local values (gitignored) |
| `directus/scripts/setup-schema.mjs` | Create `languages` / `blog` / `blog_translations` model via SDK |
| `directus/schema/snapshot.yaml` | Captured schema (committed source of truth, `schema apply`-able) |
| `scripts/lib/post-markdown.mjs` | Pure helpers: parse a post `.md`, serialize a post → `.md` (unit-tested) |
| `scripts/content-import.mjs` | One-time: seed 5 existing posts → Directus `en` base |
| `scripts/content-pull.mjs` | Pull all posts+translations → per-locale `src/content/blog/*.md` |
| `tests/content/post-markdown.test.ts` | Unit tests for the serializer/parser |
| `src/i18n/blog.ts` | `localizedPost()` helper (unit-tested) |
| `tests/i18n/blog.test.ts` | Unit tests for `localizedPost` |
| `src/content.config.ts` | Blog collection becomes locale-aware (`locale` field) |
| `src/pages/blog.astro`, `src/pages/blog/[slug].astro` | Render per-locale |

**Out of scope** (later slices): grimoire, pages, games; production Directus hosting; translation *authoring* (we add one test translation only).

---

### Task 1: Branch, deps, gitignore, scripts

**Files:** Modify `package.json`, `.gitignore`.

- [ ] **Step 1: Branch off preview**
```bash
git fetch origin && git switch -c feat/directus-blog origin/preview
```
Expected: `Switched to a new branch 'feat/directus-blog'`.

- [ ] **Step 2: Add dependencies**
```bash
mise exec -- pnpm add -D @directus/sdk gray-matter
```
Expected: both appear in `devDependencies`.

- [ ] **Step 3: Add package.json scripts**

In `package.json` `"scripts"`, add:
```json
    "directus:up": "docker compose -f directus/docker-compose.yml up -d",
    "directus:down": "docker compose -f directus/docker-compose.yml down",
    "directus:schema": "node --env-file=directus/.env directus/scripts/setup-schema.mjs",
    "content:import": "node --env-file=directus/.env scripts/content-import.mjs",
    "content:pull": "node --env-file=directus/.env scripts/content-pull.mjs"
```

- [ ] **Step 4: Gitignore Directus data + secrets**

Append to `.gitignore`:
```
# Directus (local authoring stack)
directus/.env
directus/data/
directus/uploads/
```

- [ ] **Step 5: Commit**
```bash
git add package.json pnpm-lock.yaml .gitignore
git commit -m "chore(directus): add sdk/gray-matter deps + scripts + gitignore" -m "Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 2: Directus stack (compose + env) up

**Files:** Create `directus/docker-compose.yml`, `directus/.env.example`, `directus/.env`.

- [ ] **Step 1: Compose file**

Create `directus/docker-compose.yml`:
```yaml
name: jonnxor-directus
services:
  database:
    image: postgres:16
    env_file: .env
    environment:
      POSTGRES_USER: ${DB_USER}
      POSTGRES_PASSWORD: ${DB_PASSWORD}
      POSTGRES_DB: ${DB_DATABASE}
    volumes:
      - ./data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD", "pg_isready", "--username=${DB_USER}", "--dbname=${DB_DATABASE}"]
      interval: 5s
      timeout: 5s
      retries: 10

  directus:
    image: directus/directus:11.3.5
    ports:
      - "8055:8055"
    env_file: .env
    volumes:
      - ./uploads:/directus/uploads
    depends_on:
      database:
        condition: service_healthy
    healthcheck:
      test: ["CMD-SHELL", "wget --spider -q http://127.0.0.1:8055/server/ping || exit 1"]
      interval: 5s
      timeout: 5s
      retries: 20
```

- [ ] **Step 2: Env example (committed)**

Create `directus/.env.example`:
```bash
# Directus core
KEY=replace-with-random-uuid
SECRET=replace-with-random-uuid
PUBLIC_URL=http://localhost:8055
ADMIN_EMAIL=admin@jonnxor.is
ADMIN_PASSWORD=replace-with-strong-local-password

# Database (Directus -> Postgres service)
DB_CLIENT=pg
DB_HOST=database
DB_PORT=5432
DB_DATABASE=directus
DB_USER=directus
DB_PASSWORD=replace-with-strong-local-password

# Used by the Node scripts (run on host, hit the published port)
DIRECTUS_URL=http://localhost:8055
```

- [ ] **Step 3: Real env (gitignored)**

Create `directus/.env` by copying `.env.example` and filling values. Generate the two random secrets:
```bash
wsl bash -lic "cd ~/dev/code/jonnxor.is && cp directus/.env.example directus/.env && echo KEY=$(cat /proc/sys/kernel/random/uuid) && echo SECRET=$(cat /proc/sys/kernel/random/uuid)"
```
Edit `directus/.env`: paste the generated `KEY`/`SECRET`, and set `ADMIN_PASSWORD` and `DB_PASSWORD` to strong local values (they never leave the machine — the file is gitignored).
> Optional (reproducibility): encrypt to `directus/.env.sops` per the repo's sops pattern later; not required for this slice.

- [ ] **Step 4: Bring the stack up**
```bash
mise exec -- pnpm directus:up
```
Wait ~30–60s for first boot (Directus runs DB migrations). Then verify healthy:
```bash
wsl bash -lic "cd ~/dev/code/jonnxor.is && for i in $(seq 1 30); do c=$(curl -sS -m3 -o /dev/null -w '%{http_code}' http://localhost:8055/server/ping 2>/dev/null); echo try $i: $c; [ \"$c\" = 200 ] && break; sleep 3; done"
```
Expected: ends with `200` (Directus `server/ping` returns `pong`/200).

- [ ] **Step 5: Commit** (compose + example only; `.env` is gitignored)
```bash
git add directus/docker-compose.yml directus/.env.example
git commit -m "feat(directus): local Postgres + Directus 11 compose stack" -m "Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 3: Content model (schema-as-code)

Create the `languages` / `blog` / `blog_translations` model via the SDK, **verify it**, then capture a committed schema snapshot as the source of truth. This task is the one area that may need small adjustments against the running Directus — the verify + capture steps make it converge.

**Files:** Create `directus/scripts/setup-schema.mjs`, `directus/schema/snapshot.yaml`.

- [ ] **Step 1: Setup script**

Create `directus/scripts/setup-schema.mjs`:
```js
import {
  createDirectus, rest, authentication,
  createCollection, createField, createRelation, readCollections,
} from '@directus/sdk';

const url = process.env.DIRECTUS_URL;
const client = createDirectus(url).with(rest()).with(authentication());
await client.login(process.env.ADMIN_EMAIL, process.env.ADMIN_PASSWORD);

const existing = new Set((await client.request(readCollections())).map((c) => c.collection));
const need = (name) => !existing.has(name);

// 1. languages
if (need('languages')) {
  await client.request(createCollection({
    collection: 'languages',
    meta: { icon: 'translate', note: 'Available locales' },
    schema: {},
    fields: [
      { field: 'code', type: 'string', schema: { is_primary_key: true, length: 8 }, meta: { interface: 'input' } },
      { field: 'name', type: 'string', meta: { interface: 'input' } },
      { field: 'direction', type: 'string', schema: { default_value: 'ltr' }, meta: { interface: 'select-dropdown', options: { choices: [{ text: 'ltr', value: 'ltr' }, { text: 'rtl', value: 'rtl' }] } } },
    ],
  }));
}

// 2. blog (base, non-translatable)
if (need('blog')) {
  await client.request(createCollection({
    collection: 'blog',
    meta: { icon: 'article', note: 'The Codex — long-form posts' },
    schema: {},
    fields: [
      { field: 'id', type: 'integer', schema: { is_primary_key: true, has_auto_increment: true }, meta: { hidden: true } },
      { field: 'slug', type: 'string', schema: { is_unique: true }, meta: { interface: 'input', required: true } },
      { field: 'date', type: 'timestamp', meta: { interface: 'datetime' } },
      { field: 'category', type: 'string', meta: { interface: 'input' } },
      { field: 'draft', type: 'boolean', schema: { default_value: false }, meta: { interface: 'boolean' } },
    ],
  }));
}

// 3. blog_translations (junction)
if (need('blog_translations')) {
  await client.request(createCollection({
    collection: 'blog_translations',
    meta: { hidden: true },
    schema: {},
    fields: [
      { field: 'id', type: 'integer', schema: { is_primary_key: true, has_auto_increment: true }, meta: { hidden: true } },
      { field: 'blog', type: 'integer', meta: { hidden: true } },
      { field: 'languages_code', type: 'string', meta: { hidden: true } },
      { field: 'title', type: 'string', meta: { interface: 'input' } },
      { field: 'excerpt', type: 'text', meta: { interface: 'input-multiline' } },
      { field: 'body', type: 'text', meta: { interface: 'input-rich-text-md' } },
      { field: 'read_time', type: 'string', meta: { interface: 'input' } },
    ],
  }));

  // translations alias on blog
  await client.request(createField('blog', {
    field: 'translations', type: 'alias',
    meta: { interface: 'translations', special: ['translations'], options: { languageField: 'code' } },
  }));

  // relations: blog_translations.blog -> blog (O2M back via "translations"); blog_translations.languages_code -> languages
  await client.request(createRelation({
    collection: 'blog_translations', field: 'blog', related_collection: 'blog',
    meta: { one_field: 'translations', junction_field: 'languages_code' }, schema: { on_delete: 'SET NULL' },
  }));
  await client.request(createRelation({
    collection: 'blog_translations', field: 'languages_code', related_collection: 'languages',
    meta: { junction_field: 'blog' }, schema: { on_delete: 'SET NULL' },
  }));
}

// 4. seed the three languages
import { createItems, readItems } from '@directus/sdk';
const langs = await client.request(readItems('languages'));
const have = new Set(langs.map((l) => l.code));
const want = [
  { code: 'is', name: 'Íslenska', direction: 'ltr' },
  { code: 'en', name: 'English', direction: 'ltr' },
  { code: 'ja', name: '日本語', direction: 'ltr' },
].filter((l) => !have.has(l.code));
if (want.length) await client.request(createItems('languages', want));

console.log('schema setup complete');
```

- [ ] **Step 2: Run it**
```bash
mise exec -- pnpm directus:schema
```
Expected: prints `schema setup complete` with no errors.

- [ ] **Step 3: Verify the model exists (don't trust the script)**
```bash
wsl bash -lic "cd ~/dev/code/jonnxor.is && node --env-file=directus/.env -e \"const {createDirectus,rest,authentication,readCollections,readItems}=await import('@directus/sdk');const c=createDirectus(process.env.DIRECTUS_URL).with(rest()).with(authentication());await c.login(process.env.ADMIN_EMAIL,process.env.ADMIN_PASSWORD);console.log('collections:',(await c.request(readCollections())).map(x=>x.collection).filter(n=>['blog','blog_translations','languages'].includes(n)));console.log('languages:',(await c.request(readItems('languages'))).map(l=>l.code));\""
```
Expected: `collections: [ 'blog', 'blog_translations', 'languages' ]` and `languages: [ 'is', 'en', 'ja' ]`.
> If a relation/field didn't apply as expected, fix `setup-schema.mjs` and re-run (it's idempotent — guarded by `need()`/`have`). The captured snapshot in the next step is the real source of truth.

- [ ] **Step 4: Capture the schema snapshot (committed source of truth)**
```bash
wsl bash -lic "cd ~/dev/code/jonnxor.is && mkdir -p directus/schema && docker compose -f directus/docker-compose.yml exec -T directus npx directus schema snapshot --yes /directus/snapshot.yaml && docker compose -f directus/docker-compose.yml cp directus:/directus/snapshot.yaml directus/schema/snapshot.yaml && head -20 directus/schema/snapshot.yaml"
```
Expected: `directus/schema/snapshot.yaml` exists and lists the collections. (A fresh clone reproduces the model with `docker compose exec directus npx directus schema apply --yes /directus/snapshot.yaml`.)

- [ ] **Step 5: Commit**
```bash
git add directus/scripts/setup-schema.mjs directus/schema/snapshot.yaml
git commit -m "feat(directus): blog content model (languages + blog + translations) as schema-as-code" -m "Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 4: Pure markdown helpers (TDD)

The parse/serialize logic is pure — unit-test it before the scripts use it.

**Files:** Create `scripts/lib/post-markdown.mjs`, `tests/content/post-markdown.test.ts`.

- [ ] **Step 1: Write the failing test**

Create `tests/content/post-markdown.test.ts`:
```ts
import { describe, it, expect } from 'vitest';
import { parsePost, serializePost } from '../../scripts/lib/post-markdown.mjs';

const md = `---
title: Hello
date: 2026-05-28
category: Engineering
excerpt: A short blurb
readTime: 9 min
draft: false
---
Body line one.

Body line two.
`;

describe('parsePost', () => {
  it('splits frontmatter from body', () => {
    const p = parsePost(md);
    expect(p.data.title).toBe('Hello');
    expect(p.data.category).toBe('Engineering');
    expect(p.body.trim().startsWith('Body line one.')).toBe(true);
  });
});

describe('serializePost', () => {
  it('writes locale-aware frontmatter + body, round-trips', () => {
    const out = serializePost({
      locale: 'is', slug: 'hello', date: '2026-05-28', category: 'Engineering', draft: false,
      title: 'Halló', excerpt: 'Stutt', readTime: '9 mín', body: 'Texti.',
    });
    expect(out).toContain('title: Halló');
    expect(out).toContain('locale: is');
    expect(out).toContain('slug: hello');
    expect(out.trim().endsWith('Texti.')).toBe(true);
    // re-parsing yields the same data
    expect(parsePost(out).data.title).toBe('Halló');
  });
});
```

- [ ] **Step 2: Run to verify it fails**
```bash
mise exec -- pnpm test
```
Expected: FAIL — cannot resolve `scripts/lib/post-markdown.mjs`.

- [ ] **Step 3: Implement**

Create `scripts/lib/post-markdown.mjs`:
```js
import matter from 'gray-matter';

/** Parse a Markdown file string into { data, body }. */
export function parsePost(raw) {
  const { data, content } = matter(raw);
  return { data, body: content };
}

/** Serialize a post record into a per-locale Markdown file string. */
export function serializePost(p) {
  const fm = matter.stringify(p.body ?? '', {
    title: p.title,
    date: p.date,
    category: p.category,
    excerpt: p.excerpt,
    readTime: p.readTime,
    draft: p.draft ?? false,
    locale: p.locale,
    slug: p.slug,
  });
  return fm;
}
```

- [ ] **Step 4: Run to verify pass**
```bash
mise exec -- pnpm test
```
Expected: PASS — parse + serialize tests green.

- [ ] **Step 5: Commit**
```bash
git add scripts/lib/post-markdown.mjs tests/content/post-markdown.test.ts
git commit -m "feat(content): pure parse/serialize helpers for per-locale post markdown" -m "Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 5: Import existing posts → Directus (en base)

**Files:** Create `scripts/content-import.mjs`.

- [ ] **Step 1: Write the import script**

Create `scripts/content-import.mjs`:
```js
import { readdir, readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { createDirectus, rest, authentication, readItems, createItem } from '@directus/sdk';
import { parsePost } from './lib/post-markdown.mjs';

const BLOG_DIR = 'src/content/blog';
const client = createDirectus(process.env.DIRECTUS_URL).with(rest()).with(authentication());
await client.login(process.env.ADMIN_EMAIL, process.env.ADMIN_PASSWORD);

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
```

- [ ] **Step 2: Run it**
```bash
mise exec -- pnpm content:import
```
Expected: `imported: <slug>` for all 5 posts, then `done — 5 created, 0 pre-existing`.

- [ ] **Step 3: Verify in Directus**
```bash
wsl bash -lic "cd ~/dev/code/jonnxor.is && node --env-file=directus/.env -e \"const {createDirectus,rest,authentication,readItems}=await import('@directus/sdk');const c=createDirectus(process.env.DIRECTUS_URL).with(rest()).with(authentication());await c.login(process.env.ADMIN_EMAIL,process.env.ADMIN_PASSWORD);const p=await c.request(readItems('blog',{fields:['slug',{translations:['languages_code','title']}],limit:-1}));console.log(JSON.stringify(p,null,1));\""
```
Expected: 5 posts, each with one `en` translation (languages_code `en`, the English title).

- [ ] **Step 4: Commit**
```bash
git add scripts/content-import.mjs
git commit -m "feat(content): one-time import of existing posts into Directus (en base)" -m "Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 6: `content:pull` → per-locale snapshot

**Files:** Create `scripts/content-pull.mjs`. Modify `src/content/blog/` (the 5 originals are replaced by generated `<slug>.en.md`).

- [ ] **Step 1: Write the pull script**

Create `scripts/content-pull.mjs`:
```js
import { readdir, writeFile, unlink } from 'node:fs/promises';
import { join } from 'node:path';
import { createDirectus, rest, authentication, readItems } from '@directus/sdk';
import { serializePost } from './lib/post-markdown.mjs';

const BLOG_DIR = 'src/content/blog';
const client = createDirectus(process.env.DIRECTUS_URL).with(rest()).with(authentication());
await client.login(process.env.ADMIN_EMAIL, process.env.ADMIN_PASSWORD);

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
for (const f of await readdir(BLOG_DIR)) {
  const isLocaleFile = /\.(is|en|ja)\.md$/.test(f);
  const isLegacy = f.endsWith('.md') && !isLocaleFile;
  if ((isLocaleFile && !wanted.has(f)) || isLegacy) {
    await unlink(join(BLOG_DIR, f));
    console.log('removed:', f);
  }
}
console.log(`pulled — ${wanted.size} locale file(s) from ${posts.length} post(s)`);
```

- [ ] **Step 2: Run it**
```bash
mise exec -- pnpm content:pull
```
Expected: writes `<slug>.en.md` ×5, removes the 5 original `<slug>.md`, prints `pulled — 5 locale file(s) from 5 post(s)`.

- [ ] **Step 3: Verify determinism (re-run = no diff)**
```bash
wsl bash -lic "cd ~/dev/code/jonnxor.is && mise exec -- pnpm content:pull && git status -s src/content/blog && ls src/content/blog"
```
Expected: only `*.en.md` files present; re-running produced no new changes beyond the first pull.

- [ ] **Step 4: Commit** (scripts + the regenerated snapshot)
```bash
git add scripts/content-pull.mjs src/content/blog
git commit -m "feat(content): content:pull writes per-locale blog snapshot from Directus" -m "Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 7: `localizedPost` helper (TDD)

**Files:** Create `src/i18n/blog.ts`, `tests/i18n/blog.test.ts`.

- [ ] **Step 1: Write the failing test**

Create `tests/i18n/blog.test.ts`:
```ts
import { describe, it, expect } from 'vitest';
import { localizedPost, uniqueSlugs } from '../../src/i18n/blog';

const entries = [
  { id: 'a.en', data: { slug: 'a', locale: 'en', title: 'A' } },
  { id: 'a.is', data: { slug: 'a', locale: 'is', title: 'Á' } },
  { id: 'b.en', data: { slug: 'b', locale: 'en', title: 'B' } },
];

describe('localizedPost', () => {
  it('returns the requested locale when present', () => {
    expect(localizedPost(entries, 'a', 'is')?.data.title).toBe('Á');
  });
  it('falls back to en when the locale is missing', () => {
    expect(localizedPost(entries, 'b', 'is')?.data.title).toBe('B');
  });
  it('returns undefined for an unknown slug', () => {
    expect(localizedPost(entries, 'z', 'en')).toBeUndefined();
  });
});

describe('uniqueSlugs', () => {
  it('dedupes slugs across locales', () => {
    expect(uniqueSlugs(entries).sort()).toEqual(['a', 'b']);
  });
});
```

- [ ] **Step 2: Run to verify it fails**
```bash
mise exec -- pnpm test
```
Expected: FAIL — cannot resolve `../../src/i18n/blog`.

- [ ] **Step 3: Implement**

Create `src/i18n/blog.ts`:
```ts
import { fallbackLocale } from './ui';

type Entry = { id: string; data: { slug: string; locale: string;[k: string]: unknown } };

/** The post entry for `slug` in `locale`, else the English-fallback entry, else undefined. */
export function localizedPost<T extends Entry>(entries: T[], slug: string, locale: string): T | undefined {
  return (
    entries.find((e) => e.data.slug === slug && e.data.locale === locale) ??
    entries.find((e) => e.data.slug === slug && e.data.locale === fallbackLocale)
  );
}

/** Unique post slugs across all locale entries. */
export function uniqueSlugs(entries: Entry[]): string[] {
  return [...new Set(entries.map((e) => e.data.slug))];
}
```

- [ ] **Step 4: Run to verify pass**
```bash
mise exec -- pnpm test
```
Expected: PASS (these plus the prior i18n + content tests).

- [ ] **Step 5: Commit**
```bash
git add src/i18n/blog.ts tests/i18n/blog.test.ts
git commit -m "feat(i18n): localizedPost helper with English fallback" -m "Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 8: Locale-aware blog collection + pages

**Files:** Modify `src/content.config.ts`, `src/pages/blog.astro`, `src/pages/blog/[slug].astro`.

- [ ] **Step 1: Make the blog collection locale-aware**

In `src/content.config.ts`, replace the `blog` collection definition with:
```ts
const blog = defineCollection({
  loader: glob({ pattern: '**/*.md', base: './src/content/blog' }),
  schema: z.object({
    title: z.string(),
    date: z.coerce.date(),
    category: z.string(),
    excerpt: z.string(),
    readTime: z.string(),
    draft: z.boolean().default(false),
    slug: z.string(),
    locale: z.enum(['is', 'en', 'ja']),
  }),
});
```

- [ ] **Step 2: Localize the blog list page**

In `src/pages/blog.astro`, replace the frontmatter data block (the `getCollection("blog")` filter/sort and `ACCENT`/`fmtDate`/`isoDate`) with locale-aware logic:
```astro
---
import { getCollection } from "astro:content";
import Base from "../layouts/Base.astro";
import { localizedPost, uniqueSlugs } from "../i18n/blog";
import { getRelativeLocaleUrl } from "astro:i18n";

const locale = Astro.currentLocale ?? "is";
const all = (await getCollection("blog")).filter((p) => !p.data.draft);
const posts = uniqueSlugs(all)
  .map((slug) => localizedPost(all, slug, locale)!)
  .filter(Boolean)
  .sort((a, b) => b.data.date.valueOf() - a.data.date.valueOf());

const ACCENT: Record<string, string> = { "Engineering": "tq", "Games × Code": "gold" };
const dateLocale = { is: "is-IS", en: "en-GB", ja: "ja-JP" }[locale] ?? "en-GB";
const fmtDate = (d: Date) => d.toLocaleDateString(dateLocale, { day: "numeric", month: "short", year: "numeric" });
const isoDate = (d: Date) => d.toISOString().slice(0, 10);
const categories = ["All", ...new Set(posts.map((p) => p.data.category))];
---
```
Then update the post-card link to use the slug + a **locale-aware href** (consistent with the Phase-1 nav, so `/en/blog` links stay in `/en/`) — change `href={`/blog/${post.id}`}` to:
```astro
          <a class="card lift post-card reveal" href={getRelativeLocaleUrl(locale, `/blog/${post.data.slug}`)}>
```

- [ ] **Step 3: Localize the post page**

In `src/pages/blog/[slug].astro`, replace `getStaticPaths` + the data block:
```astro
---
import { getCollection, render } from "astro:content";
import Base from "../../layouts/Base.astro";
import { localizedPost, uniqueSlugs } from "../../i18n/blog";
import { getRelativeLocaleUrl } from "astro:i18n";

export async function getStaticPaths() {
  const all = (await getCollection("blog")).filter((p) => !p.data.draft);
  return uniqueSlugs(all).map((slug) => ({ params: { slug } }));
}

const locale = Astro.currentLocale ?? "is";
const all = (await getCollection("blog")).filter((p) => !p.data.draft);
const ordered = uniqueSlugs(all)
  .map((slug) => localizedPost(all, slug, locale)!)
  .filter(Boolean)
  .sort((a, b) => b.data.date.valueOf() - a.data.date.valueOf());

const i = ordered.findIndex((p) => p.data.slug === Astro.params.slug);
const post = ordered[i];
const newer = ordered[i - 1] ?? null;
const older = ordered[i + 1] ?? null;
const { Content } = await render(post);

const ACCENT: Record<string, string> = { "Engineering": "tq", "Games × Code": "gold" };
const dateLocale = { is: "is-IS", en: "en-GB", ja: "ja-JP" }[locale] ?? "en-GB";
const fmtDate = (d: Date) => d.toLocaleDateString(dateLocale, { day: "numeric", month: "short", year: "numeric" });
const isoDate = (d: Date) => d.toISOString().slice(0, 10);
---
```
Update all four `href`s in `.article-foot` to be locale-aware: the prev/next links to `getRelativeLocaleUrl(locale, `/blog/${older.data.slug}`)` and `…${newer.data.slug}`, and the two "All posts" fallback links to `getRelativeLocaleUrl(locale, "/blog")`.

- [ ] **Step 4: Type-check + build**
```bash
wsl bash -lic "cd ~/dev/code/jonnxor.is && mise exec -- pnpm astro check 2>&1 | tail -6; mise exec -- pnpm build 2>&1 | tail -3"
```
Expected: `astro check` 0 errors; build Complete (15 pages).

- [ ] **Step 5: Commit**
```bash
git add src/content.config.ts src/pages/blog.astro src/pages/blog/[slug].astro
git commit -m "feat(i18n): render blog per-locale from the Directus snapshot (en fallback)" -m "Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 9: End-to-end verification (incl. one test translation)

- [ ] **Step 1: Build works with Directus *stopped*** (proves no build-time dependency)
```bash
wsl bash -lic "cd ~/dev/code/jonnxor.is && mise exec -- pnpm directus:down && mise exec -- pnpm build 2>&1 | tail -3"
```
Expected: build Complete — the snapshot is self-contained.

- [ ] **Step 2: Per-locale rendering (English everywhere for now)**
```bash
wsl bash -lic "cd ~/dev/code/jonnxor.is && for p in dist/blog/index.html dist/en/blog/index.html dist/ja/blog/index.html; do echo $p:; grep -oc 'post-card' $p; done"
```
Expected: each lists the 5 posts (same English content under all locales, since only `en` exists).

- [ ] **Step 3: Add one Icelandic test translation, pull, rebuild**

Bring Directus up, add an `is` translation to the newest post via the SDK, pull, rebuild:
```bash
wsl bash -lic "cd ~/dev/code/jonnxor.is && mise exec -- pnpm directus:up && sleep 20 && node --env-file=directus/.env -e \"const {createDirectus,rest,authentication,readItems,updateItem}=await import('@directus/sdk');const c=createDirectus(process.env.DIRECTUS_URL).with(rest()).with(authentication());await c.login(process.env.ADMIN_EMAIL,process.env.ADMIN_PASSWORD);const [p]=await c.request(readItems('blog',{fields:['id','slug'],limit:1}));await c.request(updateItem('blog',p.id,{translations:{create:[{languages_code:'is',title:'PRÓFUN: '+p.slug,excerpt:'Íslensk prufuþýðing',body:'Þetta er prufa.',read_time:'5 mín'}],update:[],delete:[]}}));console.log('added is translation to',p.slug);\""
```
Then:
```bash
wsl bash -lic "cd ~/dev/code/jonnxor.is && mise exec -- pnpm content:pull && mise exec -- pnpm build 2>&1 | tail -2 && echo '--- is file? ---' && ls src/content/blog | grep '\.is\.md'"
```
Expected: a new `<slug>.is.md` appears; build Complete.

- [ ] **Step 4: Confirm the Icelandic post shows in `is`, English elsewhere**
```bash
wsl bash -lic "cd ~/dev/code/jonnxor.is && echo 'IS root:'; grep -c 'PRÓFUN' dist/blog/index.html; echo 'EN:'; grep -c 'PRÓFUN' dist/en/blog/index.html"
```
Expected: `IS root: 1` (Icelandic title shows), `EN: 0` (English still). This proves the full Directus → snapshot → locale-aware render path.

- [ ] **Step 5: (cleanup) remove the test translation, pull, commit a clean snapshot** — or keep it as a demo. If removing: delete the `is` translation in Directus, `content:pull`, then commit. Otherwise commit the demo state.

- [ ] **Step 6: Final checks**
```bash
wsl bash -lic "cd ~/dev/code/jonnxor.is && mise exec -- pnpm test 2>&1 | tail -3 && mise exec -- pnpm astro check 2>&1 | tail -4"
```
Expected: all unit tests pass; `astro check` 0 errors.

---

## Verification checklist (maps to spec)

- [ ] Directus + Postgres up, `server/ping` 200 (Task 2).
- [ ] Schema model created + captured as committed snapshot; reproducible via `schema apply` (Task 3).
- [ ] 5 posts imported as `en` base (Task 5).
- [ ] `content:pull` writes per-locale `.md`, deterministic, prunes legacy files (Task 6).
- [ ] Blog renders per-locale with English fallback (Tasks 7–8).
- [ ] **Build works with Directus stopped** — no build-time dependency (Task 9.1).
- [ ] One `is` translation flows Directus → snapshot → `/blog` Icelandic, `/en/blog` English (Task 9.3–9.4).
- [ ] `astro check` 0 errors; all unit tests pass (Task 9.6).

## Notes / follow-ups (not this plan)

- **Grimoire / pages / games** repeat this pattern — grimoire is closest (swap Markdown for the YAML/HTML body shape); pages (About/CV) need the structured/blocks model.
- **Production Directus hosting** (live build-time queries) — deferred; the committed snapshot stays the contract until an always-on host exists.
- **Translation authoring** (real is/ja) — Phase 3, in the Directus admin UI (wants the host networking restored).
- **sops** for `directus/.env` — optional reproducibility upgrade; the file is gitignored so secrets stay local by default.
- The `directus schema snapshot/apply` exact flags + the `setup-schema.mjs` relation `meta` may need a small tweak against the running Directus 11.3.5 — Task 3's verify + capture steps converge it; the **committed snapshot** is the source of truth thereafter.
