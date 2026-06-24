export const locales = ['is', 'en', 'ja'] as const;
export type Locale = (typeof locales)[number];

export const defaultLocale: Locale = 'is';
export const fallbackLocale: Locale = 'en';

export const ui = {
  is: { 'nav.home': 'Heim' },
  en: { 'nav.home': 'Home', 'foot.status': 'Open to interesting quests' },
  ja: {},
} as const;
