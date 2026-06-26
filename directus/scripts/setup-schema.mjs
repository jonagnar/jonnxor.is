import {
  createCollection, createField, createRelation, readCollections,
  readFieldsByCollection, updateField,
} from '@directus/sdk';
import { connect, done } from '../../scripts/lib/directus-client.mjs';

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

// 6. seed the three languages
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
done();
