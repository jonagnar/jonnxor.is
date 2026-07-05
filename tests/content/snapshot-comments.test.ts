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
const BLOG = COLLECTIONS.find((c) => c.name === 'blog')!;

function* yamlFiles() {
  for (const dir of YAML_DIRS) {
    for (const f of readdirSync(join(ROOT, dir))) {
      if (f.endsWith('.yaml')) yield join(dir, f);
    }
  }
}

function assertNoComments(doc: YAML.Document.Parsed) {
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
}

describe('snapshot YAML files contain no comment nodes', () => {
  it.each([...yamlFiles()])('%s', (rel) => {
    assertNoComments(YAML.parseDocument(readFileSync(join(ROOT, rel), 'utf8')));
  });
});

// Blog snapshots are Markdown with a YAML front-matter block (gray-matter);
// the same " #" truncation hazard applies to the front matter itself, so lint
// the substring between the leading `---` fences as plain YAML.
function* blogFiles() {
  for (const f of readdirSync(join(ROOT, BLOG.dir))) {
    if (BLOG.fileRe.test(f)) yield join(BLOG.dir, f);
  }
}

function frontMatter(raw: string): string {
  const match = /^---\n([\s\S]*?)\n---\n/.exec(raw);
  if (!match) throw new Error('expected a leading --- front-matter block');
  return match[1];
}

describe('blog front matter contains no comment nodes', () => {
  it.each([...blogFiles()])('%s', (rel) => {
    const raw = readFileSync(join(ROOT, rel), 'utf8');
    assertNoComments(YAML.parseDocument(frontMatter(raw)));
  });
});
