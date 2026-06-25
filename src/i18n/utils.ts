import { ui, defaultLocale, fallbackLocale, type Locale } from './ui';

type UiKey = keyof (typeof ui)[typeof fallbackLocale];

/** Returns a translator for `lang`, falling back lang → English → the key itself. */
export function useTranslations(lang: Locale | string | undefined) {
  const loc = (lang ?? defaultLocale) as Locale;
  return function t(key: UiKey | string): string {
    const inLocale = (ui[loc] as Record<string, string> | undefined)?.[key];
    if (inLocale) return inLocale;
    const inFallback = (ui[fallbackLocale] as Record<string, string>)[key];
    return inFallback ?? key;
  };
}

/** Strips a leading `en`/`ja` locale segment, returning the default-locale base path. */
export function stripLocale(pathname: string): string {
  const seg = pathname.split('/')[1];
  if (seg === 'en' || seg === 'ja') {
    const rest = pathname.slice(seg.length + 1);
    return rest === '' || rest === '/' ? '/' : rest;
  }
  return pathname;
}
