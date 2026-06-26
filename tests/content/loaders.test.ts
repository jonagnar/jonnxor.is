import { describe, it, expect } from 'vitest';
import { localeEntryId } from '../../src/content/loaders';

describe('localeEntryId', () => {
  it('keeps a slug\'s locale files distinct (md)', () => {
    expect(localeEntryId({ entry: 'foo.en.md' })).toBe('foo.en');
    expect(localeEntryId({ entry: 'foo.is.md' })).toBe('foo.is');
    expect(localeEntryId({ entry: 'foo.en.md' })).not.toBe(localeEntryId({ entry: 'foo.is.md' }));
  });
  it('keeps a slug\'s locale files distinct (yaml)', () => {
    expect(localeEntryId({ entry: 'bar.en.yaml' })).toBe('bar.en');
    expect(localeEntryId({ entry: 'bar.ja.yaml' })).toBe('bar.ja');
    expect(localeEntryId({ entry: 'bar.en.yaml' })).not.toBe(localeEntryId({ entry: 'bar.ja.yaml' }));
  });
  it('preserves subdirectory paths in the id', () => {
    expect(localeEntryId({ entry: 'posts/foo.en.md' })).toBe('posts/foo.en');
  });
});
