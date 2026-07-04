// Rehydrate Directus content (every snapshot collection — blog posts, grimoire
// docs, and any future descriptor) FROM the committed per-locale snapshot —
// the inverse of content:pull, and the content-side companion to the schema
// snapshot (directus/schema/snapshot.yaml). Use it to repopulate a fresh/empty
// Directus (new clone, wiped DB, re-bootstrap) so the local authoring stack
// matches the repo without re-authoring.
//
// It reads every src/content/<collection>/<slug>.<locale>.<ext> file, groups
// the locale files by slug into one collection item + its translations, and
// creates any item whose slug doesn't already exist. It is idempotent at the
// POST level (existing slugs are skipped) — it rehydrates missing items, it
// does NOT reconcile per-translation diffs on items that already exist. The
// normal authoring loop is author-in-Directus -> content:pull -> commit;
// restore is the reverse trip for standing the stack back up from the
// committed snapshot.
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
