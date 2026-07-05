import { describe, it, expect } from 'vitest';
import { countdownsSections } from '../../src/content/page-sections';

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
