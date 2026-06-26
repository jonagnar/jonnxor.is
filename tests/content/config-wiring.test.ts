import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

// The generateId collision bug (locale files of one slug overwriting each other on a
// shared id) only returns if `generateId: localeEntryId` is removed from a loader in
// content.config.ts. tests/content/loaders.test.ts proves localeEntryId works in
// isolation, but not that it's WIRED IN. Astro's content layer can't be loaded in
// vitest (no astro:content virtual module), so guard the wiring at the source level:
// both collections' glob loaders must pass generateId: localeEntryId.
const configSrc = readFileSync(
  fileURLToPath(new URL('../../src/content.config.ts', import.meta.url)),
  'utf8',
);

describe('content.config.ts wires the locale-aware id', () => {
  it('imports localeEntryId', () => {
    expect(configSrc).toMatch(/import\s*\{\s*localeEntryId\s*\}\s*from\s*['"]\.\/content\/loaders['"]/);
  });

  it('passes generateId: localeEntryId to both the blog and grimoire loaders', () => {
    // Count only non-comment lines to exclude the cross-reference comment in the import block.
    const codeLines = configSrc.split('\n').filter(l => !/^\s*\/\//.test(l));
    const matches = codeLines.join('\n').match(/generateId:\s*localeEntryId/g) ?? [];
    expect(matches.length).toBe(2);
  });
});
