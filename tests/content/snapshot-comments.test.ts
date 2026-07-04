import { describe, it, expect } from 'vitest';
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import YAML from 'yaml';
import { COLLECTIONS } from '../../scripts/lib/collections.mjs';

// The canonical serializer never emits YAML comments — so a comment node in a
// committed snapshot file is evidence that a hand-authored seed had an
// unquoted " #" mid-scalar, which silently truncates the value (a real bug
// caught during the games seeding). Fail loudly on any comment anywhere.
// dirs are repo-relative (e.g. src/content/games) — resolve from the repo root.
const ROOT = fileURLToPath(new URL('../..', import.meta.url));
const YAML_DIRS = COLLECTIONS.filter((c) => c.ext === '.yaml').map((c) => c.dir);

function* yamlFiles() {
  for (const dir of YAML_DIRS) {
    for (const f of readdirSync(join(ROOT, dir))) {
      if (f.endsWith('.yaml')) yield join(dir, f);
    }
  }
}

describe('snapshot YAML files contain no comment nodes', () => {
  it.each([...yamlFiles()])('%s', (rel) => {
    const doc = YAML.parseDocument(readFileSync(join(ROOT, rel), 'utf8'));
    const comments: string[] = [];
    YAML.visit(doc, {
      Node(_, node) {
        if (node.comment) comments.push(String(node.comment));
        if (node.commentBefore) comments.push(String(node.commentBefore));
      },
    });
    if (doc.comment) comments.push(String(doc.comment));
    if (doc.commentBefore) comments.push(String(doc.commentBefore));
    expect(comments, `unquoted " #" likely truncated a value: ${comments.join(' | ')}`).toEqual([]);
  });
});
