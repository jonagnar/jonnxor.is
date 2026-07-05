import { describe, it, expect } from 'vitest';
import { localizedByOrder, localizedEntry, requirePageHead, uniqueSlugs } from '../../src/i18n/localized';

const entries = [
  { id: 'a.en', data: { slug: 'a', locale: 'en' as const, title: 'A' } },
  { id: 'a.is', data: { slug: 'a', locale: 'is' as const, title: 'Á' } },
  { id: 'b.en', data: { slug: 'b', locale: 'en' as const, title: 'B' } },
  { id: 'c.ja', data: { slug: 'c', locale: 'ja' as const, title: 'C' } },
];

describe('localizedEntry', () => {
  it('returns the requested locale when present', () => {
    expect(localizedEntry(entries, 'a', 'is')?.data.title).toBe('Á');
  });
  it('falls back to en when the locale is missing', () => {
    expect(localizedEntry(entries, 'b', 'is')?.data.title).toBe('B');
  });
  it('returns undefined for an unknown slug', () => {
    expect(localizedEntry(entries, 'z', 'en')).toBeUndefined();
  });
  it('returns undefined when only a non-fallback, non-requested locale exists', () => {
    expect(localizedEntry(entries, 'c', 'is')).toBeUndefined();
  });
});

describe('requirePageHead', () => {
  it('returns the en-fallback entry when the locale entry is missing', () => {
    expect(requirePageHead(entries, 'b', 'is').data.title).toBe('B');
  });
  it('throws a message naming the slug when no entry exists at all', () => {
    expect(() => requirePageHead(entries, 'z', 'en')).toThrow(/'z' missing/);
  });
});

describe('uniqueSlugs', () => {
  it('dedupes slugs across locales', () => {
    expect(uniqueSlugs(entries).sort()).toEqual(['a', 'b', 'c']);
  });
});

describe('localizedByOrder', () => {
  const ordered = [
    { id: 'x.en', data: { slug: 'x', locale: 'en' as const, order: 2 } },
    { id: 'y.en', data: { slug: 'y', locale: 'en' as const, order: 1 } },
    { id: 'y.is', data: { slug: 'y', locale: 'is' as const, order: 1 } },
    { id: 'z.ja', data: { slug: 'z', locale: 'ja' as const, order: 3 } },
  ];

  it('resolves one locale entry per slug (en fallback) and sorts by order', () => {
    const result = localizedByOrder(ordered, 'is');
    expect(result.map((e) => e.id)).toEqual(['y.is', 'x.en']);
  });
});
