# Content Port Phase 2 Implementation Plan — simpleDescriptor + portfolio, home, about, cv

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port the four remaining content surfaces (portfolio, home, about, cv) to Directus-owned, snapshot-driven content rendered through typed components, on a `simpleDescriptor`-factored pipeline — completing sub-project 1 of the wiring milestone.

**Architecture:** Same seam as phase 1 (`.planning/2026-07-04-content-port-design.md`, slices 4–7): entity collection `projects` for portfolio cards; everything else is prose in the `pages` collection's per-page `sections` json validated by shared schemas in `src/content/page-sections.ts`. Phase-1 reviews queued four riders — the `simpleDescriptor` builder, a sections wiring guard, the lightbox focus trap + label i18n, and the blog-frontmatter comment lint — all land here.

**Tech Stack:** unchanged from phase 1. Environment notes (WSL/mise/Playwright container/Directus lifecycle, branch → PR → `preview`) as in `.planning/2026-07-04-content-port-plan.md` — read that prelude first. The proven in-repo templates are `src/pages/games.astro` / `countdowns.astro` / `wallpapers.astro` and their slice commits; **copy patterns from current code, not from older plan listings.**

**Hard rules carried over:** visual baselines unchanged for portfolio/about/cv (pixel-parity gate); home's baseline is re-recorded ONCE (latest-posts goes dynamic — the only sanctioned visual change, reviewed in its PR); every hand-authored seed value byte-verbatim from the current pages (watch the unquoted ` #` hazard — the comment lint will catch it); `restore → pull → pull` byte-identical per slice; en-only seeds and ui keys.

---

## Task 1: `simpleDescriptor` builder + sections plumbing

**Files:**
- Modify: `scripts/lib/collections.mjs`
- Modify: `src/content/page-sections.ts`, `src/content.config.ts`
- Modify: `tests/content/collections.test.ts` (only if assertions reference internals), `tests/content/config-wiring.test.ts`

- [ ] **Step 1: Add the builder** to `scripts/lib/collections.mjs` (above COLLECTIONS; keep the CollectionDescriptor JSDoc — the builder must honor its three guarantees):

```js
// Field kinds for simpleDescriptor — how a value crosses the seam in each direction.
const KIND = {
  req: { out: (v) => v, back: (v) => v },
  opt: { out: (v) => v ?? undefined, back: (v) => v ?? null },
  'opt-quoted': { out: (v) => v ?? undefined, back: (v) => v ?? null }, // date-like strings
  array: { out: (v) => v ?? [], back: (v) => v },
  flag: { out: (v) => v || undefined, back: (v) => v ?? false }, // false omitted from files
};
const LOCALE_YAML_RE = /\.(is|en|ja)\.yaml$/;

/**
 * Build a full CollectionDescriptor for the standard shape (base item +
 * translations junction, YAML snapshot). `item`/`translation` are ordered maps
 * of field name -> kind (KIND above); slug/locale are implicit. Blog and
 * grimoire stay hand-rolled (custom serializers, date normalization, renames).
 */
export function simpleDescriptor({ name, item, translation }) {
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
```

- [ ] **Step 2: Refactor the four existing simple descriptors onto it** (blog/grimoire untouched):

```js
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
```

Delete the now-dead hand-rolled codecs/descriptors for those four.

- [ ] **Step 3: Prove the refactor is inert.** `pnpm test` — ALL existing tests pass UNCHANGED (the round-trip tests in `tests/content/collections.test.ts` are the contract; if one fails, the builder is wrong — fix the builder, never the test). Then against live Directus: `pnpm content:pull && git status --porcelain src/content` → EMPTY (byte-identical). BLOCKED if not.

- [ ] **Step 4: Sections schema map + wiring guard.** In `src/content/page-sections.ts` add (schemas for home/about/cv arrive in their slices):

```ts
/** slug -> sections schema; content.config.ts validates any pages entry whose slug appears here. */
export const sectionSchemas = { countdowns: countdownsSections } as const;
```

In `src/content.config.ts`, replace the slug-specific superRefine branch with a lookup over `sectionSchemas` (same error shape, `z.prettifyError`). In `tests/content/config-wiring.test.ts`, add a test asserting the config source references `sectionSchemas` (same source-level guard style as the generateId test) — deleting the superRefine can no longer pass silently.

- [ ] **Step 5:** `pnpm test && pnpm build` green → commit `refactor(content): simpleDescriptor builder + sections schema map` → push.

---

## Task 2: Blog-frontmatter comment lint (phase-1 rider)

**Files:** Modify `tests/content/snapshot-comments.test.ts`.

- [ ] Extend the lint to blog: for each `src/content/blog/*.md` matching the blog fileRe, extract the frontmatter block between the `---` markers, run the same `YAML.parseDocument` + visit check. Derive the dir from the blog descriptor. Run the file (expect all green — 16 blog files + 40 yaml files), commit `test(content): extend comment-node lint to blog frontmatter`, push.

---

## Task 3: `projects` slice — schema, seeds, collection

**Files:** `directus/scripts/setup-schema.mjs` + `directus/schema/snapshot.yaml`; `scripts/lib/collections.mjs`; `src/content/projects/*.en.yaml` (6) + `src/content/pages/portfolio.en.yaml`; `src/content.config.ts`; `tests/content/{config-wiring,collections}.test.ts`.

- [ ] **Step 1: Directus schema** — `projects` + `projects_translations`, exact house pattern (copy the wallpapers blocks as template; renumber comments). Base: id, slug (unique, required), order (integer), sigil (string), gradient (json, input-code language json, note '3 hex colors'), tech (json, tags), links (json, input-code language json, note '[{label, href}] — labels treated as data'). Junction: title (string), description (text, input-multiline), plate (string). Alias + 2 SET-NULL relations. Apply + snapshot + idempotence re-run + commit (2 files, `feat(directus): projects collection with translations`) + push.

- [ ] **Step 2: Descriptor** — one call now:

```js
  simpleDescriptor({
    name: 'projects',
    item: { order: 'req', sigil: 'req', gradient: 'array', tech: 'array', links: 'array' },
    translation: { title: 'req', description: 'req', plate: 'req' },
  }),
```

- [ ] **Step 3: Seeds.** READ `src/pages/portfolio.astro` fully. Extract the 6 project cards into `src/content/projects/<slug>.en.yaml` (order 1–6, top-to-bottom; slug = kebab of title): title/description/plate byte-verbatim; sigil/gradient(3 hex, quoted)/tech array/links array (`- label: …` / `  href: …`) as data. Any value containing ` #` or leading specials: quote it. Plus `src/content/pages/portfolio.en.yaml` (kicker/title/lede verbatim from the page head; no sections).

- [ ] **Step 4: Round-trip proof** (restore → pull → stage → pull → empty status → restore all-skip) exactly as phase-1 slices. BLOCKED on any second-pull diff.

- [ ] **Step 5: Astro collection** — wiring count 6→7 (TDD); schema:

```ts
const projects = defineCollection({
  loader: glob({ pattern: '**/*.yaml', base: './src/content/projects', generateId: localeEntryId }),
  schema: z.object({
    slug: z.string(),
    locale: z.enum(['is', 'en', 'ja']),
    order: z.number(),
    sigil: z.string(),
    gradient: z.array(z.string().regex(/^#[0-9a-fA-F]{6}$/)).length(3),
    tech: z.array(z.string()),
    links: z.array(z.object({ label: z.string(), href: z.string() }).strict()),
    title: z.string(),
    description: z.string(),
    plate: z.string(),
  }),
});
```

Round-trip unit test in `collections.test.ts` (mirror the wallpapers one, include a links array). `pnpm test && pnpm build` → commit `feat(content): projects collection seeded through the snapshot seam` → push.

---

## Task 4: Portfolio page — `ProjectCard` + refactor

**Files:** `src/components/ProjectCard.astro`; `src/pages/portfolio.astro`; `tests/render/{components,pages}.test.ts`.

- [ ] **Step 1 (TDD):** Container test for ProjectCard (fixture with 2 links + 3 tech chips; assert title, description, sigil, plate, chip count, link labels+hrefs, gradient in the art style). Expect FAIL.
- [ ] **Step 2:** ProjectCard mirrors the current card markup in portfolio.astro CLASS-FOR-CLASS (read it — art/sigil/plate/body/chips/links structure; gradient inline style formula copied exactly). Props: `{ sigil, gradient, tech, links, title, description, plate }` (no hidden/order props — nothing filters).
- [ ] **Step 3:** Rewrite portfolio.astro: `localizedByOrder(await getCollection('projects'), locale)`, `requirePageHead(pagesAll, 'portfolio', locale)`, PageHead (label "Portfolio head"), grid of ProjectCards, style block verbatim, NO page JS (there was none). Remove Portfolio from `tests/render/pages.test.ts` (extend the comment).
- [ ] **Step 4:** `pnpm test && pnpm exec astro check && pnpm build && pnpm test:e2e && pnpm test:visual` — portfolio baseline UNCHANGED. Commit `feat(portfolio): port The Forge to Directus-owned collection, server-rendered cards` → push.

**Amendment (post-T3 review):** stops + status added — per-card gradient stops and three chip variants exist on the real page; flat tech[] + 3 hexes were insufficient for pixel parity.

---

## Task 5: Home slice — sections, `PostRow`, dynamic latest posts

**Files:** `src/content/page-sections.ts`; `src/content/pages/home.en.yaml`; `src/components/PostRow.astro`; `src/pages/index.astro`; tests as usual; visual baselines for `/` (re-recorded, reviewed).

- [ ] **Step 1: Schema.** READ `src/pages/index.astro` fully, then define `homeSections` in page-sections.ts to match its real prose structure — expected shape (adjust ONLY to structures actually present; keep `.strict()` everywhere):

```ts
export const homeSections = z.object({
  hero: z.object({ tagline: z.string(), cta_primary: z.string(), cta_secondary: z.string() }).strict(),
  paths: z.array(z.object({ title: z.string(), copy: z.string(), link_label: z.string() }).strict()).length(3),
  now: z.array(z.object({ label: z.string(), value: z.string(), sub: z.string() }).strict()).length(3),
  latest_heading: z.string(),
}).strict();
```

Register in `sectionSchemas`. The wordmark/name/dragon SVG stay hardcoded (brand markup, not content). The page-head kicker line goes in the pages row's `kicker` field.

- [ ] **Step 2: Seed** `src/content/pages/home.en.yaml` — every string byte-verbatim from index.astro. Long values single-line in YAML is fine (determinism over prettiness; nested block literals aren't supported by the codec). Restore → pull ×2 → empty. (`pages` needs no new Directus schema — sections is already json.)
- [ ] **Step 3: PostRow** component (Container-tested): date (locale-formatted `is-IS`/`en-GB`/`ja-JP` — copy blog.astro's formatting), title, category chip — markup mirroring the existing home post-row/blog card row (read both; if blog.astro's markup differs, home's own row markup wins — this is home's component; blog adoption only if the swap is mechanical, else leave blog alone).
- [ ] **Step 4: Rewrite index.astro:** sections-driven hero/paths/now; latest writing = top 3 from `getCollection('blog')` (drafts excluded, `localizedEntry` per slug, sort date desc) rendered via PostRow with locale-aware hrefs (`getRelativeLocaleUrl`). Keep dragonfly/theme/dragon exactly. Remove Index from `tests/render/pages.test.ts`.
- [ ] **Step 5:** Full gates. **Home visual baseline WILL differ** (real top-3 posts replace the hardcoded rows): re-record with `sh scripts/pw.sh test tests/visual --update-snapshots`, then inspect: ONLY `/` (and theme variants of `/`) snapshots may change — any other changed baseline is a regression, fix it. Commit (baselines included) `feat(home): sections-driven home with dynamic latest posts (baseline re-record)` → push.

---

## Task 6: About slice

**Files:** page-sections.ts; `src/content/pages/about.en.yaml`; `src/components/FactList.astro`; `src/pages/about.astro`; tests.

- [ ] **Step 1:** READ `src/pages/about.astro` fully. Define `aboutSections` (`.strict()`, adjust to reality — expected: `body: z.array(z.string()).min(1)` paragraphs; `sheet_title`; `sheet: array({ k, v }).length(8)`; `skills: z.array(z.string())`; `letter: { dateline, body: z.array(z.string()), signoff }`). Register in `sectionSchemas`.
- [ ] **Step 2:** Seed byte-verbatim (the ~600-word bio + 652-word cover letter — paragraph per array item; drop-cap styling is CSS, don't bake markup into content; any inline HTML in paragraphs must be preserved verbatim and rendered with `set:html` ONLY if the current page already uses markup there — check first, prefer plain text). Restore → pull ×2 → empty.
- [ ] **Step 3:** `FactList.astro` (Container-tested: renders k/v rows matching the current character-sheet markup class-for-class).
- [ ] **Step 4:** Rewrite about.astro from sections; style block verbatim; remove About from render smoke list. Full gates — about baseline UNCHANGED. Commit `feat(about): port The Saga of Jón to sections-driven prose` → push.

---

## Task 7: CV slice (print CSS must survive)

**Files:** page-sections.ts; `src/content/pages/cv.en.yaml`; `src/components/CVRole.astro`, `src/components/CVSkillsRow.astro`; `src/pages/cv.astro`; `tests/e2e/cv.spec.ts` (new); tests as usual.

- [ ] **Step 1:** READ `src/pages/cv.astro` fully. Define `cvSections` (`.strict()`, adjust to reality — expected: `contact { location, email, github }`; `profile`; `experience: array({ when, org, title, bullets: array(string).min(1) }).min(1)`; `skills: array({ k, v }).min(1)`; `education: array({ when, degree, school }).min(1)`; `languages`; plus any section-heading strings the page hardcodes). Register in `sectionSchemas`. The print-button label goes to `ui.ts` en-only (`'cv.print'`).
- [ ] **Step 2:** Seed byte-verbatim; restore → pull ×2 → empty.
- [ ] **Step 3:** CVRole + CVSkillsRow (Container-tested, class-for-class with current markup).
- [ ] **Step 4:** Rewrite cv.astro from sections. The `@media print` block and `window.print()` button behavior are copied VERBATIM — they are the page's reason to exist.
- [ ] **Step 5: New `tests/e2e/cv.spec.ts`** — print survival smoke:

```ts
import { test, expect } from '@playwright/test';

test('cv page prints clean: chrome hidden under print media', async ({ page }) => {
  await page.goto('/cv');
  await expect(page.locator('.cv-actions button, button.btn-print').first()).toBeVisible();
  await page.emulateMedia({ media: 'print' });
  await expect(page.locator('.site-nav')).toBeHidden();
  await expect(page.locator('.site-footer')).toBeHidden();
});
```
(Adjust selectors to the page's actual print-hidden elements — read the `@media print` block; the assertion set must cover nav + footer at minimum.)
- [ ] **Step 6:** Full gates — cv baseline UNCHANGED. Commit `feat(cv): port The Record of Deeds to sections-driven prose` → push.

---

## Task 8: Lightbox focus trap + label i18n (phase-1 rider)

**Files:** `src/pages/wallpapers.astro`; `src/i18n/ui.ts`; `tests/e2e/wallpapers.spec.ts`.

- [ ] Add `'wall.preview': 'Wallpaper preview'` (en-only) and use it for the lightbox `aria-label`. Implement a minimal focus trap while `.open`: keydown Tab handler cycling within the lightbox's focusable elements (the two buttons), active only when open. Extend the wallpapers e2e focus test: with lightbox open, Tab from Close wraps to Download (or per implemented order). Zero visual change; visual suite must pass untouched. Commit `fix(a11y): lightbox focus trap + i18n label` → push.

---

## Task 9: Phase close-out

- [ ] Full sweep: `pnpm test`, `astro check` (baseline only), `pnpm build`, `pnpm test:e2e`, `pnpm test:visual` (only home baselines differ vs phase-1 state, already committed in Task 5).
- [ ] Fresh-clone story (non-destructive): `directus:schema` idempotent; `content:restore` all-skip across ALL collections (now 7 incl. projects: 5+11+17+7 pages+10+12+6 = 68 items); `content:pull` clean.
- [ ] Hardcoded-content eradication audit: grep `src/pages/{index,about,cv,portfolio}.astro` for the known prose markers (project titles, "Character sheet", CV company names, hero tagline) — zero hits outside snapshot files.
- [ ] Definition-of-done audit vs design §10 (now fully in reach: all seven surfaces Directus-owned).
- [ ] Report; sub-project 1 complete → sub-project 2 (.NET build-time worker) is next on the milestone roadmap.

---

## Self-review notes (plan author)

- The builder's KIND table was verified against all four existing descriptors' exact semantics (out/back pairs, quoteKeys, omitEmpty) — byte-stability is asserted by Task 1 Step 3, which is the gate for everything after.
- Prose seeds deliberately are NOT inlined here (unlike phase-1's data tables): the executors extract byte-verbatim from the named source pages, and the pull-twice + comment-lint + visual gates catch extraction errors mechanically — same protections that caught the ` #` truncation in phase 1.
- Sections shapes are stated as expected structures with explicit permission to adjust to page reality (pages must be read first) — `.strict()` and registration in `sectionSchemas` are the non-negotiables.
