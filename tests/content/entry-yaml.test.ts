import { describe, it, expect } from 'vitest';
import jsYaml from 'js-yaml';
import { makeEntryCodec } from '../../scripts/lib/entry-yaml.mjs';

const codec = makeEntryCodec({
  keyOrder: ['slug', 'locale', 'order', 'date', 'gold', 'gradient', 'title', 'body'],
  blockKeys: ['body'],
  quoteKeys: ['date'],
  omitEmpty: ['date', 'gold'],
});

const record = {
  slug: 'fable', locale: 'en', order: 2, date: '2026-10-09', gold: false,
  gradient: ['#0e2d1c', '#2e7d4f', '#ecbd3e'],
  title: 'Fable: The Return', body: '<p>Line one.</p>\n<p>Line two.</p>',
};

describe('makeEntryCodec', () => {
  it('writes keys in fixed order and round-trips', () => {
    const out = codec.serialize(record);
    expect(out.indexOf('slug:')).toBeLessThan(out.indexOf('locale:'));
    expect(out.indexOf('order:')).toBeLessThan(out.indexOf('date:'));
    const back = codec.parse(out);
    expect(back.slug).toBe('fable');
    expect(back.order).toBe(2);
    expect(back.gradient).toEqual(record.gradient);
    expect(back.body).toBe(record.body);
  });
  it('emits blockKeys as block literals', () => {
    expect(codec.serialize(record)).toContain('body: |');
  });
  it('double-quotes quoteKeys so js-yaml (YAML 1.1) reads a string, not a Date', () => {
    const out = codec.serialize(record);
    expect(out).toMatch(/date: "2026-10-09"/);
    const parsed = jsYaml.load(out) as Record<string, unknown>;
    expect(typeof parsed.date).toBe('string');
  });
  it('omits omitEmpty keys when undefined/null/false, keeps them otherwise', () => {
    const out = codec.serialize({ ...record, date: null, gold: false });
    expect(out).not.toContain('date:');
    expect(out).not.toContain('gold:');
    expect(codec.serialize({ ...record, gold: true })).toContain('gold: true');
  });
  it('quotes # hex strings safely (never bare)', () => {
    const back = codec.parse(codec.serialize(record));
    expect(back.gradient[0]).toBe('#0e2d1c');
  });
  it('is deterministic (serialize∘parse∘serialize is identity)', () => {
    const out = codec.serialize(record);
    expect(codec.serialize(codec.parse(out))).toBe(out);
  });
});
