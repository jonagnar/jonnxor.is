export const locales = ['is', 'en', 'ja'] as const;
export type Locale = (typeof locales)[number];

export const defaultLocale: Locale = 'is';
export const fallbackLocale: Locale = 'en';

/** Display names for the language switcher (always shown in their own language). */
export const localeNames: Record<Locale, string> = {
  is: 'Íslenska',
  en: 'English',
  ja: '日本語',
};

// English is complete and authoritative — it is the fallback for every key.
// Icelandic is an AI first draft; entries marked `// review` need the author's voice.
// Japanese is partial on purpose (nav nouns); the rest falls back to English.
export const ui = {
  en: {
    'nav.home': 'Home',
    'nav.about': 'About',
    'nav.cv': 'CV',
    'nav.portfolio': 'Portfolio',
    'nav.blog': 'Blog',
    'nav.docs': 'Docs',
    'nav.games': 'Games',
    'nav.wallpapers': 'Wallpapers',
    'nav.countdowns': 'Countdowns',
    'navDesc.home': 'The landing — start here',
    'navDesc.about': 'The Saga of Jón + cover letter',
    'navDesc.cv': 'The Record of Deeds — print-ready',
    'navDesc.portfolio': 'The Forge — selected projects',
    'navDesc.blog': 'The Codex — long-form writing',
    'navDesc.docs': 'The Grimoire — guides & cheat sheets',
    'navDesc.games': 'The Game Hall — tracker & favorites',
    'navDesc.wallpapers': 'The Hoard — gallery & downloads',
    'navDesc.countdowns': 'The Reckoning — timers & tallies',
    'realm.work': 'The Work',
    'realm.codex': 'The Codex',
    'realm.hall': 'The Hall',
    'dock.home': 'Home',
    'dock.forge': 'Forge',
    'dock.grimoire': 'Grimoire',
    'dock.hall': 'Hall',
    'nav.menu': 'Menu',
    'nav.seek': 'Seek pages, docs, spells…',
    'nav.more': 'More halls',
    'nav.searchAria': 'Search',
    'nav.searchHint': 'Search (press /)',
    'foot.tag': 'Forged in Reykjavík · est. 1992',
    'foot.note': 'Full-stack developer, saga enthusiast, professional dragon-botherer.',
    'foot.contactHd': 'Send a raven',
    'foot.k.raven': 'raven',
    'foot.k.github': 'github',
    'foot.k.linkedin': 'linkedin',
    'foot.k.hall': 'hall',
    'foot.k.status': 'status',
    'foot.location': 'Reykjavík, Iceland · GMT',
    'foot.status': 'Open to interesting quests',
    'foot.caption': 'Nautíð · born under the bull',
    'foot.link.cv': 'CV',
    'foot.link.wallpapers': 'Wallpapers',
    'foot.link.dragons': 'Here be dragons',
    'foot.copyright': '© 2026 Jón Agnar Stefánsson',
  },
  is: {
    'nav.home': 'Heim',
    'nav.about': 'Um mig',
    'nav.cv': 'Ferilskrá',
    'nav.portfolio': 'Safn',
    'nav.blog': 'Blogg',
    'nav.docs': 'Skjöl',
    'nav.games': 'Leikir',
    'nav.wallpapers': 'Veggfóður',
    'nav.countdowns': 'Niðurtalning',
    'navDesc.home': 'Lendingin — byrjaðu hér', // review
    'navDesc.about': 'Saga Jóns + kynningarbréf', // review
    'navDesc.cv': 'Afrekaskráin — tilbúin til prentunar', // review
    'navDesc.portfolio': 'Smiðjan — valin verkefni', // review
    'navDesc.blog': 'Kviðan — lengri skrif', // review
    'navDesc.docs': 'Galdrabókin — leiðbeiningar og svindlblöð', // review
    'navDesc.games': 'Leikjahöllin — yfirlit og uppáhöld', // review
    'navDesc.wallpapers': 'Fjársjóðurinn — myndir og niðurhal', // review
    'navDesc.countdowns': 'Uppgjörið — teljarar og talningar', // review
    'realm.work': 'Verkin', // review
    'realm.codex': 'Kviðan', // review
    'realm.hall': 'Höllin', // review
    'dock.home': 'Heim',
    'dock.forge': 'Smiðjan', // review
    'dock.grimoire': 'Galdrabók', // review
    'dock.hall': 'Höllin', // review
    'nav.menu': 'Valmynd',
    'nav.seek': 'Leitaðu að síðum, skjölum, göldrum…', // review
    'nav.more': 'Fleiri hallir', // review
    'nav.searchAria': 'Leita',
    'nav.searchHint': 'Leita (ýttu á /)',
    'foot.tag': 'Smíðað í Reykjavík · síðan 1992', // review
    'foot.note': 'Full-stack forritari, sagnaáhugamaður, atvinnu-drekaáreitir.', // review
    'foot.contactHd': 'Sendu hrafn', // review
    'foot.k.raven': 'hrafn',
    'foot.k.github': 'github',
    'foot.k.linkedin': 'linkedin',
    'foot.k.hall': 'höll',
    'foot.k.status': 'staða',
    'foot.location': 'Reykjavík, Ísland · GMT',
    'foot.status': 'Opinn fyrir spennandi verkefnum', // review
    'foot.caption': 'Nautíð · fæddur undir nautinu', // review
    'foot.link.cv': 'Ferilskrá',
    'foot.link.wallpapers': 'Veggfóður',
    'foot.link.dragons': 'Hér eru drekar',
  },
  ja: {
    'nav.home': 'ホーム',
    'nav.about': '概要',
    'nav.cv': '履歴書',
    'nav.portfolio': '作品集',
    'nav.blog': 'ブログ',
    'nav.docs': 'ドキュメント',
    'nav.games': 'ゲーム',
    'nav.wallpapers': '壁紙',
    'nav.countdowns': 'カウントダウン',
    'nav.menu': 'メニュー',
    'nav.searchAria': '検索',
  },
} as const;
