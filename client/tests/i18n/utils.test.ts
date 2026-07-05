import { describe, it, expect } from 'vitest';
import { useTranslations, stripLocale } from '../../src/i18n/utils';
import { ui, fallbackLocale } from '../../src/i18n/ui';

describe('useTranslations', () => {
  it('returns the string for the requested locale', () => {
    const t = useTranslations('is');
    expect(t('nav.home')).toBe('Heim');
  });

  it('falls back to English when the locale lacks the key', () => {
    const t = useTranslations('ja');
    // 'foot.status' is intentionally English-only in the dictionary
    expect(t('foot.status')).toBe('Open to interesting quests');
  });

  it('returns the key itself if no locale and no English has it', () => {
    const t = useTranslations('is');
    expect(t('does.not.exist')).toBe('does.not.exist');
  });
});

describe('stripLocale', () => {
  it('leaves a default-locale (unprefixed) path unchanged', () => {
    expect(stripLocale('/about')).toBe('/about');
    expect(stripLocale('/')).toBe('/');
  });

  it('strips an en/ja prefix back to the base path', () => {
    expect(stripLocale('/en/about')).toBe('/about');
    expect(stripLocale('/ja/blog/foo')).toBe('/blog/foo');
  });

  it('maps a bare locale root to /', () => {
    expect(stripLocale('/en')).toBe('/');
    expect(stripLocale('/ja/')).toBe('/');
  });

  it('tolerates a trailing slash', () => {
    expect(stripLocale('/en/about/')).toBe('/about/');
  });
});

describe('dictionary coverage', () => {
  it('English (the fallback) defines every key used by any locale', () => {
    const enKeys = new Set(Object.keys(ui[fallbackLocale]));
    for (const loc of ['is', 'ja'] as const) {
      for (const key of Object.keys(ui[loc])) {
        expect(enKeys.has(key), `missing en key: ${key}`).toBe(true);
      }
    }
  });
});
