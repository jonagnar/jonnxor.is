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
