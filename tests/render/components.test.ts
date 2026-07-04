import { experimental_AstroContainer as AstroContainer } from 'astro/container';
import { describe, it, expect } from 'vitest';
import PageHead from '../../src/components/PageHead.astro';
import GameCard from '../../src/components/GameCard.astro';

describe('PageHead', () => {
  it('renders kicker, h1 and lede', async () => {
    const c = await AstroContainer.create();
    const html = await c.renderToString(PageHead, {
      props: { kicker: 'Game tracker', title: 'The Game Hall', lede: 'Lede text.', label: 'Games head' },
    });
    expect(html).toContain('class="kicker"');
    expect(html).toContain('<h1>The Game Hall</h1>');
    expect(html).toContain('data-screen-label="Games head"');
  });
});

describe('GameCard', () => {
  const base = {
    tab: 'playing', platforms: ['PS5'], gradient: ['#10131c', '#3a4a2e', '#f2c94c'],
    initials: 'ER', favorite: false, title: 'Elden Ring: Nightreign', sub: 'NG+2', hidden: false,
  };
  it('renders cover, title, platform chips and data-tab', async () => {
    const c = await AstroContainer.create();
    const html = await c.renderToString(GameCard, { props: base });
    expect(html).toContain('data-tab="playing"');
    expect(html).toContain('<span class="gi">ER</span>');
    expect(html).toContain('Elden Ring: Nightreign');
    expect(html).toContain('<span class="chip">PS5</span>');
    expect(html).not.toContain('hidden');
    expect(html).not.toContain('class="fav"');
  });
  it('marks hidden cards and favorites', async () => {
    const c = await AstroContainer.create();
    const html = await c.renderToString(GameCard, { props: { ...base, hidden: true, favorite: true } });
    expect(html).toContain('hidden');
    expect(html).toContain('class="fav"');
  });
  it('renders an empty countdown badge for dated upcoming games and TBA otherwise', async () => {
    const c = await AstroContainer.create();
    const dated = await c.renderToString(GameCard, { props: { ...base, tab: 'upcoming', date: '2026-11-19' } });
    expect(dated).toContain('data-date="2026-11-19"');
    const tba = await c.renderToString(GameCard, { props: { ...base, tab: 'upcoming' } });
    expect(tba).toContain('>TBA<');
  });
});
