import { fallbackLocale, type Locale } from './ui';

type Entry = { id: string; data: { slug: string; locale: Locale; [k: string]: unknown } };

/** The entry for `slug` in `locale`, else the English-fallback entry, else undefined. */
export function localizedEntry<T extends Entry>(entries: T[], slug: string, locale: Locale): T | undefined {
  return (
    entries.find((e) => e.data.slug === slug && e.data.locale === locale) ??
    entries.find((e) => e.data.slug === slug && e.data.locale === fallbackLocale)
  );
}

/** The pages entry for `slug`, else a build-failing error naming the missing file. */
export function requirePageHead<T extends Entry>(entries: T[], slug: string, locale: Locale): T {
  const entry = localizedEntry(entries, slug, locale);
  if (!entry) {
    throw new Error(`pages entry '${slug}' missing (no en fallback) — re-run content:pull?`);
  }
  return entry;
}

/** Unique slugs across all locale entries. */
export function uniqueSlugs(entries: Entry[]): string[] {
  return [...new Set(entries.map((e) => e.data.slug))];
}

type OrderedEntry = { id: string; data: { slug: string; locale: Locale; order: number; [k: string]: unknown } };

/** One locale-resolved entry per slug (en fallback), sorted by `data.order`. */
export function localizedByOrder<T extends OrderedEntry>(entries: T[], locale: Locale): T[] {
  return uniqueSlugs(entries)
    .map((s) => localizedEntry(entries, s, locale))
    .filter((e): e is T => Boolean(e))
    .sort((a, b) => a.data.order - b.data.order);
}
