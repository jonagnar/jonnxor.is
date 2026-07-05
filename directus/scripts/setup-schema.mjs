import {
  connect, done,
  createCollection, createField, createRelation, createItems, readItems,
  readCollections, readFieldsByCollection, updateField,
} from '../../client/scripts/lib/directus-client.mjs';

const client = await connect();

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

// Directus ignores `length` on a string PK at createCollection time (creates varchar(255)),
// and updateField can't alter a string PK's length (it tries to drop NOT NULL on the PK column).
// Converge `languages.code` to length 8 idempotently; if the SDK can't (PK quirk), fall back to
// raw SQL on the DB. The committed snapshot.yaml is the authoritative reproduction (schema apply
// recreates code as varchar(8) directly).
{
  const codeField = (await client.request(readFieldsByCollection('languages'))).find((f) => f.field === 'code');
  if (codeField && codeField.schema?.max_length !== 8) {
    try {
      await client.request(updateField('languages', 'code', { schema: { max_length: 8 } }));
    } catch {
      console.warn(
        'Could not narrow languages.code via SDK (string PK length is immutable through the API). '
        + 'Run this once against the DB, or apply directus/schema/snapshot.yaml: '
        + "ALTER TABLE languages ALTER COLUMN code TYPE varchar(8);",
      );
    }
  }
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

// 4. grimoire (base, non-translatable)
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

// 5. grimoire_translations (junction)
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
      { field: 'gradient', type: 'json', meta: { interface: 'input-code', options: { language: 'json' }, note: '3 hex colors' } },
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

// Converge games.gradient's editor to JSON highlighting — the option was added
// after the collection first shipped, and createCollection above only runs on
// fresh installs.
{
  const gradientField = (await client.request(readFieldsByCollection('games'))).find((f) => f.field === 'gradient');
  if (gradientField && gradientField.meta?.options?.language !== 'json') {
    await client.request(updateField('games', 'gradient', { meta: { options: { language: 'json' } } }));
  }
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

// 10. countdowns (base, non-translatable)
if (need('countdowns')) {
  await client.request(createCollection({
    collection: 'countdowns',
    meta: { icon: 'hourglass_top', note: 'The Reckoning — countdowns & count-ups' },
    schema: {},
    fields: [
      { field: 'id', type: 'integer', schema: { is_primary_key: true, has_auto_increment: true }, meta: { hidden: true } },
      { field: 'slug', type: 'string', schema: { is_unique: true }, meta: { interface: 'input', required: true } },
      { field: 'order', type: 'integer', meta: { interface: 'input' } },
      { field: 'kind', type: 'string', meta: { interface: 'select-dropdown', options: { choices: [{ text: 'countdown', value: 'countdown' }, { text: 'countup', value: 'countup' }] } } },
      { field: 'when', type: 'string', schema: { is_nullable: true }, meta: { interface: 'input', note: 'YYYY-MM-DD or empty = indefinite' } },
      { field: 'gold', type: 'boolean', schema: { default_value: false }, meta: { interface: 'boolean' } },
      { field: 'icon', type: 'string', schema: { is_nullable: true }, meta: { interface: 'input' } },
      { field: 'start', type: 'string', schema: { is_nullable: true }, meta: { interface: 'input', note: 'ISO datetime literal, countups only' } },
      { field: 'rate', type: 'float', schema: { is_nullable: true }, meta: { interface: 'input', note: 'hours/day, countups only' } },
    ],
  }));
}

// 11. countdowns_translations (junction)
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

// 12. wallpapers (base, non-translatable)
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
      { field: 'gradient', type: 'json', meta: { interface: 'input-code', options: { language: 'json' }, note: '3 hex colors' } },
      { field: 'angle', type: 'integer', meta: { interface: 'input', note: 'gradient angle 0-360' } },
    ],
  }));
}

// 13. wallpapers_translations (junction)
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

// 14. projects (base, non-translatable)
if (need('projects')) {
  await client.request(createCollection({
    collection: 'projects',
    meta: { icon: 'workspaces', note: 'The Forge — portfolio project cards' },
    schema: {},
    fields: [
      { field: 'id', type: 'integer', schema: { is_primary_key: true, has_auto_increment: true }, meta: { hidden: true } },
      { field: 'slug', type: 'string', schema: { is_unique: true }, meta: { interface: 'input', required: true } },
      { field: 'order', type: 'integer', meta: { interface: 'input' } },
      { field: 'sigil', type: 'string', meta: { interface: 'input' } },
      { field: 'gradient', type: 'json', meta: { interface: 'input-code', options: { language: 'json' }, note: '3 hex colors' } },
      { field: 'stops', type: 'json', meta: { interface: 'input-code', options: { language: 'json' }, note: '2 gradient stop percentages' } },
      { field: 'tech', type: 'json', meta: { interface: 'tags' } },
      { field: 'status', type: 'json', schema: { is_nullable: true }, meta: { interface: 'input-code', options: { language: 'json' }, note: '{label, kind: plain|gold} — label treated as data' } },
      { field: 'links', type: 'json', meta: { interface: 'input-code', options: { language: 'json' }, note: '[{label, href}] — labels treated as data' } },
    ],
  }));
}

// Converge projects.stops/status onto existing installs — these two fields were
// added after the collection first shipped, and createCollection above only
// runs on fresh installs (house pattern, see the games.gradient converge).
{
  const projectsFields = new Set((await client.request(readFieldsByCollection('projects'))).map((f) => f.field));
  if (!projectsFields.has('stops')) {
    await client.request(createField('projects', {
      field: 'stops', type: 'json',
      meta: { interface: 'input-code', options: { language: 'json' }, note: '2 gradient stop percentages' },
    }));
  }
  if (!projectsFields.has('status')) {
    await client.request(createField('projects', {
      field: 'status', type: 'json', schema: { is_nullable: true },
      meta: { interface: 'input-code', options: { language: 'json' }, note: '{label, kind: plain|gold} — label treated as data' },
    }));
  }
}

// 15. projects_translations (junction)
if (need('projects_translations')) {
  await client.request(createCollection({
    collection: 'projects_translations',
    meta: { hidden: true },
    schema: {},
    fields: [
      { field: 'id', type: 'integer', schema: { is_primary_key: true, has_auto_increment: true }, meta: { hidden: true } },
      { field: 'projects', type: 'integer', meta: { hidden: true } },
      { field: 'languages_code', type: 'string', meta: { hidden: true } },
      { field: 'title', type: 'string', meta: { interface: 'input' } },
      { field: 'description', type: 'text', meta: { interface: 'input-multiline' } },
      { field: 'plate', type: 'string', meta: { interface: 'input' } },
    ],
  }));
  await client.request(createField('projects', {
    field: 'translations', type: 'alias',
    meta: { interface: 'translations', special: ['translations'], options: { languageField: 'code' } },
  }));
  await client.request(createRelation({
    collection: 'projects_translations', field: 'projects', related_collection: 'projects',
    meta: { one_field: 'translations', junction_field: 'languages_code' }, schema: { on_delete: 'SET NULL' },
  }));
  await client.request(createRelation({
    collection: 'projects_translations', field: 'languages_code', related_collection: 'languages',
    meta: { junction_field: 'projects' }, schema: { on_delete: 'SET NULL' },
  }));
}

// 16. seed the three languages
const langs = await client.request(readItems('languages'));
const have = new Set(langs.map((l) => l.code));
const want = [
  { code: 'is', name: 'Íslenska', direction: 'ltr' },
  { code: 'en', name: 'English', direction: 'ltr' },
  { code: 'ja', name: '日本語', direction: 'ltr' },
].filter((l) => !have.has(l.code));
if (want.length) await client.request(createItems('languages', want));

console.log('schema setup complete');
done();
