import { describe, it, expect } from 'vitest';
import { countdownsSections, homeSections, aboutSections, cvSections, sectionSchemas } from '../../src/content/page-sections';

const VALID_HOME = {
  hero: { tagline: 'A tagline.', cta_primary: 'View the work', cta_secondary: 'Read the saga' },
  paths: [
    { rune: 'I', title: 'The Work', copy: 'Copy.', link_label: 'Enter the forge →' },
    { rune: 'II', title: 'The Saga', copy: 'Copy.', link_label: 'Open the codex →' },
    { rune: 'III', title: 'The Game Hall', copy: 'Copy.', link_label: 'Take a seat →' },
  ],
  now_kicker: 'Currently',
  now: [
    { label: 'Now playing', value: 'A game', sub: 'A sub.' },
    { label: 'Now building', value: 'A project', sub: 'A sub.' },
    { label: 'Now brewing', value: 'Chai', sub: 'A sub.' },
  ],
  latest_heading: 'Latest from the codex',
};

describe('countdownsSections', () => {
  it('parses a valid shape', () => {
    const r = countdownsSections.safeParse({
      countdown_kicker: 'Counting down',
      countup_kicker: 'Counting up',
      methodology: 'How this is computed',
    });
    expect(r.success).toBe(true);
  });

  it('rejects an extra key (strict)', () => {
    const r = countdownsSections.safeParse({
      countdown_kicker: 'Counting down',
      countup_kicker: 'Counting up',
      methodology: 'How this is computed',
      extra: 'nope',
    });
    expect(r.success).toBe(false);
  });

  it('rejects a missing key', () => {
    const r = countdownsSections.safeParse({
      countdown_kicker: 'Counting down',
      methodology: 'How this is computed',
    });
    expect(r.success).toBe(false);
  });
});

describe('sectionSchemas.countdowns', () => {
  // Functional guard (not just source-level): proves the registered schema is
  // actually .strict() — a regression to a non-strict schema would pass the
  // config-wiring source check but silently accept typo'd/extra keys here.
  it('rejects an extra key', () => {
    const r = sectionSchemas.countdowns.safeParse({
      countdown_kicker: 'a',
      countup_kicker: 'b',
      methodology: 'c',
      extra: 'x',
    });
    expect(r.success).toBe(false);
  });
});

describe('homeSections', () => {
  it('parses a valid shape', () => {
    expect(homeSections.safeParse(VALID_HOME).success).toBe(true);
  });

  it('rejects an extra top-level key (strict)', () => {
    const r = homeSections.safeParse({ ...VALID_HOME, extra: 'nope' });
    expect(r.success).toBe(false);
  });

  it('rejects an extra key inside a path item (strict)', () => {
    const r = homeSections.safeParse({
      ...VALID_HOME,
      paths: [{ ...VALID_HOME.paths[0], extra: 'nope' }, VALID_HOME.paths[1], VALID_HOME.paths[2]],
    });
    expect(r.success).toBe(false);
  });

  it('rejects fewer or more than 3 paths', () => {
    expect(homeSections.safeParse({ ...VALID_HOME, paths: VALID_HOME.paths.slice(0, 2) }).success).toBe(false);
  });

  it('rejects fewer or more than 3 now entries', () => {
    expect(homeSections.safeParse({ ...VALID_HOME, now: VALID_HOME.now.slice(0, 2) }).success).toBe(false);
  });

  it('rejects a missing key', () => {
    const { latest_heading, ...rest } = VALID_HOME;
    expect(homeSections.safeParse(rest).success).toBe(false);
  });
});

describe('sectionSchemas.home', () => {
  it('rejects an extra key', () => {
    const r = sectionSchemas.home.safeParse({ ...VALID_HOME, extra: 'x' });
    expect(r.success).toBe(false);
  });
});

const VALID_ABOUT = {
  body: ['Paragraph one.', 'Paragraph two with <em>emphasis</em>.'],
  sheet_title: 'Character sheet',
  sheet: [
    { k: 'Class', v: 'Full-stack developer' },
    { k: 'Home base', v: 'Reykjavík, Iceland' },
    { k: 'Alignment', v: 'Chaotic kind (INFJ)' },
    { k: 'Patron gods', v: 'Loki · Hermes' },
    { k: 'Favorite hero', v: 'Odysseus' },
    { k: 'Familiar', v: 'One cat, one owl (alleged)' },
    { k: 'Potion', v: 'Masala chai, extra cardamom' },
    { k: 'Languages', v: 'Íslenska · English · 日本語 (learning)' },
  ],
  skills: [
    { label: 'TypeScript', gold: false },
    { label: 'Souls veteran', gold: true },
  ],
  letter: {
    kicker: 'Cover letter',
    who: 'Jón Agnar Stefánsson',
    when: 'Reykjavík · June 2026',
    body: ['Dear hiring team,', 'Body paragraph.'],
    sign_name: 'Jón Agnar Stefánsson',
    sign_small: 'jon@jonnxor.is · jonnxor.is',
    cta_label: 'Continue to CV →',
  },
};

describe('aboutSections', () => {
  it('parses a valid shape', () => {
    expect(aboutSections.safeParse(VALID_ABOUT).success).toBe(true);
  });

  it('rejects an extra top-level key (strict)', () => {
    const r = aboutSections.safeParse({ ...VALID_ABOUT, extra: 'nope' });
    expect(r.success).toBe(false);
  });

  it('rejects an extra key inside a sheet item (strict)', () => {
    const r = aboutSections.safeParse({
      ...VALID_ABOUT,
      sheet: [{ ...VALID_ABOUT.sheet[0], extra: 'nope' }, ...VALID_ABOUT.sheet.slice(1)],
    });
    expect(r.success).toBe(false);
  });

  it('rejects a sheet with fewer or more than 8 entries', () => {
    expect(aboutSections.safeParse({ ...VALID_ABOUT, sheet: VALID_ABOUT.sheet.slice(0, 7) }).success).toBe(false);
  });

  it('rejects an empty body array', () => {
    expect(aboutSections.safeParse({ ...VALID_ABOUT, body: [] }).success).toBe(false);
  });

  it('rejects an extra key inside a skill item (strict)', () => {
    const r = aboutSections.safeParse({
      ...VALID_ABOUT,
      skills: [{ ...VALID_ABOUT.skills[0], extra: 'nope' }, VALID_ABOUT.skills[1]],
    });
    expect(r.success).toBe(false);
  });

  it('rejects an extra key inside letter (strict)', () => {
    const r = aboutSections.safeParse({ ...VALID_ABOUT, letter: { ...VALID_ABOUT.letter, extra: 'nope' } });
    expect(r.success).toBe(false);
  });

  it('rejects an empty letter body array', () => {
    const r = aboutSections.safeParse({ ...VALID_ABOUT, letter: { ...VALID_ABOUT.letter, body: [] } });
    expect(r.success).toBe(false);
  });

  it('rejects a missing key', () => {
    const { sheet_title, ...rest } = VALID_ABOUT;
    expect(aboutSections.safeParse(rest).success).toBe(false);
  });
});

describe('sectionSchemas.about', () => {
  it('rejects an extra key', () => {
    const r = sectionSchemas.about.safeParse({ ...VALID_ABOUT, extra: 'x' });
    expect(r.success).toBe(false);
  });
});

const VALID_CV = {
  role: 'Full-stack developer',
  contact: { location: 'Reykjavík, Iceland', web: 'jon@jonnxor.is · jonnxor.is', github: 'github.com/jonnxor' },
  profile_heading: 'Profile',
  profile: 'A profile paragraph.',
  experience_heading: 'Experience',
  experience: [
    { when: '2022 — now', title: 'Senior Full-stack Developer', org: 'Straumur Pay', bullets: ['Did a thing.', 'Did another thing.'] },
    { when: '2019 — 2022', title: 'Full-stack Developer', org: 'Nordsky Software', bullets: ['Built stuff.'] },
  ],
  skills_heading: 'Skills',
  skills: [
    { k: 'Languages', v: 'TypeScript, Go, Python, SQL' },
    { k: 'Frontend', v: 'React, Next.js, CSS architecture, a11y' },
  ],
  education_heading: 'Education & languages',
  education: [
    { when: '2012 — 2015', degree: 'BSc Computer Science', school: 'University of Iceland' },
  ],
  languages: 'Icelandic (native) · English (fluent) · Japanese (JLPT N4, climbing)',
};

describe('cvSections', () => {
  it('parses a valid shape', () => {
    expect(cvSections.safeParse(VALID_CV).success).toBe(true);
  });

  it('rejects an extra top-level key (strict)', () => {
    const r = cvSections.safeParse({ ...VALID_CV, extra: 'nope' });
    expect(r.success).toBe(false);
  });

  it('rejects an extra key inside contact (strict)', () => {
    const r = cvSections.safeParse({ ...VALID_CV, contact: { ...VALID_CV.contact, extra: 'nope' } });
    expect(r.success).toBe(false);
  });

  it('rejects an extra key inside an experience item (strict)', () => {
    const r = cvSections.safeParse({
      ...VALID_CV,
      experience: [{ ...VALID_CV.experience[0], extra: 'nope' }, VALID_CV.experience[1]],
    });
    expect(r.success).toBe(false);
  });

  it('rejects an experience item with empty bullets', () => {
    const r = cvSections.safeParse({
      ...VALID_CV,
      experience: [{ ...VALID_CV.experience[0], bullets: [] }, VALID_CV.experience[1]],
    });
    expect(r.success).toBe(false);
  });

  it('rejects an empty experience array', () => {
    expect(cvSections.safeParse({ ...VALID_CV, experience: [] }).success).toBe(false);
  });

  it('rejects an extra key inside a skill item (strict)', () => {
    const r = cvSections.safeParse({
      ...VALID_CV,
      skills: [{ ...VALID_CV.skills[0], extra: 'nope' }, VALID_CV.skills[1]],
    });
    expect(r.success).toBe(false);
  });

  it('rejects an extra key inside an education item (strict)', () => {
    const r = cvSections.safeParse({
      ...VALID_CV,
      education: [{ ...VALID_CV.education[0], extra: 'nope' }],
    });
    expect(r.success).toBe(false);
  });

  it('rejects an empty education array', () => {
    expect(cvSections.safeParse({ ...VALID_CV, education: [] }).success).toBe(false);
  });

  it('rejects a missing key', () => {
    const { languages, ...rest } = VALID_CV;
    expect(cvSections.safeParse(rest).success).toBe(false);
  });
});

describe('sectionSchemas.cv', () => {
  it('rejects an extra key', () => {
    const r = sectionSchemas.cv.safeParse({ ...VALID_CV, extra: 'x' });
    expect(r.success).toBe(false);
  });
});
