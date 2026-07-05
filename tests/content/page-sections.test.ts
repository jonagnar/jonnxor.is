import { describe, it, expect } from 'vitest';
import { countdownsSections, homeSections, sectionSchemas } from '../../src/content/page-sections';

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
