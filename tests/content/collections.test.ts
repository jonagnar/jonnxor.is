import { describe, it, expect } from 'vitest';
import { COLLECTIONS, simpleDescriptor } from '../../scripts/lib/collections.mjs';

const REQUIRED = ['name', 'dir', 'ext', 'fileRe', 'fields', 'toRecord', 'toItem', 'toTranslation', 'serialize', 'parse'];

describe('collection descriptors', () => {
  it('rejects an unknown field kind', () => {
    expect(() => simpleDescriptor({ name: 'x', item: { a: 'requ' }, translation: {} })).toThrow(
      /unknown kind "requ" for field "a"/,
    );
  });
  it('every descriptor is complete', () => {
    expect(COLLECTIONS.length).toBeGreaterThanOrEqual(2);
    for (const c of COLLECTIONS) {
      for (const k of REQUIRED) expect(c[k], `${c.name}.${k}`).toBeDefined();
      expect(c.fileRe.test(`x.en${c.ext}`)).toBe(true);
      expect(c.fileRe.test(`x${c.ext}`)).toBe(false);
    }
  });
  it('blog round-trips pull-shape -> file -> restore-shape', () => {
    const blog = COLLECTIONS.find((c) => c.name === 'blog')!;
    const item = { slug: 'p1', date: '2026-05-28T00:00:00', category: 'Myth', draft: false };
    const t = { languages_code: 'en', title: 'T', excerpt: 'E', read_time: '9 min', body: 'Body.' };
    const rec = blog.toRecord(item, t);
    const parsed = blog.parse(blog.serialize(rec));
    // gray-matter (post-markdown.mjs) always terminates the body with a single
    // trailing newline on stringify — matches the on-disk file convention (every
    // committed *.md snapshot file ends in \n) and is pre-existing, unchanged
    // behavior of the wrapped serializer.
    expect(blog.toTranslation(parsed)).toEqual({ languages_code: 'en', title: 'T', excerpt: 'E', read_time: '9 min', body: 'Body.\n' });
    expect(blog.toItem(parsed).slug).toBe('p1');
  });
  it('grimoire round-trips', () => {
    const g = COLLECTIONS.find((c) => c.name === 'grimoire')!;
    const item = { slug: 'd1', order: 7, realm: 'code', game: null, updated: '2026-05-26' };
    const t = { languages_code: 'en', title: 'T', cat: 'C', tags: ['a'], body: '<p>x</p>' };
    const parsed = g.parse(g.serialize(g.toRecord(item, t)));
    expect(g.toItem(parsed)).toMatchObject({ slug: 'd1', order: 7, realm: 'code', updated: '2026-05-26' });
    expect(g.toTranslation(parsed)).toMatchObject({ languages_code: 'en', tags: ['a'] });
  });
  it('games round-trips, restoring omitted favorite/date to false/null', () => {
    const games = COLLECTIONS.find((c) => c.name === 'games')!;
    const item = { slug: 'fable', order: 2, tab: 'upcoming', date: null, platforms: ['Xbox', 'PC'], gradient: ['#0e2d1c', '#2e7d4f', '#ecbd3e'], initials: 'FA', favorite: false };
    const t = { languages_code: 'en', title: 'Fable', sub: 'Playground · RPG' };
    const out = games.serialize(games.toRecord(item, t));
    expect(out).not.toContain('date:');      // null date omitted from file
    expect(out).not.toContain('favorite:');  // false favorite omitted from file
    const parsed = games.parse(out);
    expect(games.toItem(parsed)).toEqual(item);           // ...but restored as null/false
    expect(games.toTranslation(parsed)).toEqual(t);
  });
  it('pages round-trips, restoring omitted sections to null', () => {
    const pages = COLLECTIONS.find((c) => c.name === 'pages')!;
    const item = { slug: 'games' };
    const t = { languages_code: 'en', kicker: 'Game tracker', title: 'The Game Hall', lede: 'Lede — with a dash.', sections: null };
    const out = pages.serialize(pages.toRecord(item, t));
    expect(out).not.toContain('sections:');
    const parsed = pages.parse(out);
    expect(pages.toItem(parsed)).toEqual(item);
    expect(pages.toTranslation(parsed)).toEqual(t);
  });
  it('countdowns round-trips a dated gold countdown', () => {
    const cd = COLLECTIONS.find((c) => c.name === 'countdowns')!;
    const item = { slug: 'jol-christmas-eve', order: 5, kind: 'countdown', when: '2026-12-24', gold: true, icon: null, start: null, rate: null };
    const t = { languages_code: 'en', what: 'Jól (Christmas Eve)', note: 'Hangikjöt o’clock' };
    const out = cd.serialize(cd.toRecord(item, t));
    expect(out).toMatch(/when: "2026-12-24"/);   // quoted — YAML 1.1 date-coercion guard
    expect(out).not.toContain('icon:');
    const parsed = cd.parse(out);
    expect(cd.toItem(parsed)).toEqual(item);
    expect(cd.toTranslation(parsed)).toEqual(t);
  });
  it('countdowns round-trips a countup with float rate', () => {
    const cd = COLLECTIONS.find((c) => c.name === 'countdowns')!;
    const item = { slug: 'on-the-toilet', order: 10, kind: 'countup', when: null, gold: false, icon: '🚽', start: '1992-05-05T08:00:00', rate: 0.35 };
    const t = { languages_code: 'en', what: 'On the toilet', note: 'A king on a porcelain throne.' };
    const out = cd.serialize(cd.toRecord(item, t));
    expect(out).toMatch(/start: "1992-05-05T08:00:00"/);
    expect(out).not.toContain('when:');
    expect(out).not.toContain('gold:');
    const parsed = cd.parse(out);
    expect(cd.toItem(parsed)).toEqual(item);
    expect(parsed.rate).toBe(0.35);
  });
  it('wallpapers round-trips', () => {
    const w = COLLECTIONS.find((c) => c.name === 'wallpapers')!;
    const item = { slug: 'grid-runner', order: 3, tag: 'Synthwave', aspect_ratio: '3/4', gradient: ['#120724', '#5d1d8f', '#00f5d4'], angle: 35 };
    const t = { languages_code: 'en', title: 'Grid Runner' };
    const parsed = w.parse(w.serialize(w.toRecord(item, t)));
    expect(w.toItem(parsed)).toEqual(item);
    expect(w.toTranslation(parsed)).toEqual(t);
  });
});
