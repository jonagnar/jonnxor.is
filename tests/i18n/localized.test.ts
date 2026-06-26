import { describe, it, expect } from 'vitest';
import { localizedEntry, uniqueSlugs } from '../../src/i18n/localized';

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

describe('uniqueSlugs', () => {
  it('dedupes slugs across locales', () => {
    expect(uniqueSlugs(entries).sort()).toEqual(['a', 'b', 'c']);
  });
});
