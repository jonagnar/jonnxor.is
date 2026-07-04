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
