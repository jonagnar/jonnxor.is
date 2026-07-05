import { describe, it, expect } from 'vitest';
import { countdownsSections, sectionSchemas } from '../../src/content/page-sections';

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
