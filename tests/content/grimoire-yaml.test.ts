import { describe, it, expect } from 'vitest';
import { parseDoc, serializeDoc } from '../../scripts/lib/grimoire-yaml.mjs';

const record = {
  slug: 'go-errors', locale: 'en',
  order: 7, realm: 'code', cat: 'Code patterns',
  title: 'Go error wrapping', tags: ['go', 'errors'],
  updated: '2026-05-26',
  body: '<p>Line one.</p>\n<h2>Heading</h2>\n<p>Line two.</p>',
};

describe('serializeDoc', () => {
  it('emits body as a block literal and round-trips', () => {
    const out = serializeDoc(record);
    expect(out).toContain('slug: go-errors');
    expect(out).toContain('locale: en');
    expect(out).toContain('body: |');           // block literal, not inline quoted
    const back = parseDoc(out);
    expect(back.slug).toBe('go-errors');
    expect(back.order).toBe(7);
    expect(back.tags).toEqual(['go', 'errors']);
    expect(back.body).toBe(record.body);
  });
  it('omits game when absent', () => {
    expect(serializeDoc(record)).not.toContain('game:');
  });
  it('includes game when present', () => {
    expect(serializeDoc({ ...record, game: 'Elden Ring' })).toContain('game: Elden Ring');
  });
  it('is deterministic (re-serialize a parsed file is identical)', () => {
    const out = serializeDoc(record);
    expect(serializeDoc(parseDoc(out))).toBe(out);
  });
  it('is deterministic with a game field (round-trips through parse)', () => {
    const withGame = { ...record, game: 'Elden Ring' };
    const out = serializeDoc(withGame);
    expect(serializeDoc(parseDoc(out))).toBe(out);
  });
});
