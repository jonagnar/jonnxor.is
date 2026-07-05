import { experimental_AstroContainer as AstroContainer } from 'astro/container';
import { describe, it, expect } from 'vitest';
import PageHead from '../../src/components/PageHead.astro';
import GameCard from '../../src/components/GameCard.astro';
import CountdownCard from '../../src/components/CountdownCard.astro';
import CountUpCard from '../../src/components/CountUpCard.astro';
import WallpaperTile from '../../src/components/WallpaperTile.astro';
import ProjectCard from '../../src/components/ProjectCard.astro';

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

describe('CountdownCard', () => {
  it('renders dated card with four clock cells and data-when', async () => {
    const c = await AstroContainer.create();
    const html = await c.renderToString(CountdownCard, {
      props: { what: 'Jól', when: '2026-12-24', note: 'Hangikjöt o’clock', gold: true },
    });
    expect(html).toContain('data-when="2026-12-24"');
    expect(html).toContain('cd-card gold');
    expect((html.match(/data-u=/g) ?? []).length).toBe(4);
  });
  it('renders the ∞ cell when undated', async () => {
    const c = await AstroContainer.create();
    const html = await c.renderToString(CountdownCard, {
      props: { what: 'Silksong', note: 'Any day now.', gold: false },
    });
    expect(html).toContain('∞');
    expect(html).not.toContain('data-when');
  });
});

describe('CountUpCard', () => {
  it('renders icon, data-up index and start/rate data attributes', async () => {
    const c = await AstroContainer.create();
    const html = await c.renderToString(CountUpCard, {
      props: { icon: '⚒', what: 'Working', note: 'Code.', start: '2014-06-01T09:00:00', rate: 7.4, index: 0 },
    });
    expect(html).toContain('data-up="0"');
    expect(html).toContain('data-start="2014-06-01T09:00:00"');
    expect(html).toContain('data-rate="7.4"');
  });
});

describe('WallpaperTile', () => {
  it('renders art, title, tag chip and lightbox dataset', async () => {
    const c = await AstroContainer.create();
    const html = await c.renderToString(WallpaperTile, {
      props: {
        slug: 'grid-runner', title: 'Grid Runner', tag: 'Synthwave', aspectRatio: '3/4',
        gradient: ['#120724', '#5d1d8f', '#00f5d4'], angle: 35,
      },
    });
    expect(html).toContain('data-tag="Synthwave"');
    expect(html).toContain('data-slug="grid-runner"');
    expect(html).toContain('data-angle="35"');
    expect(html).toContain('data-g0="#120724"');
    expect(html).toContain('aspect-ratio: 3/4');
    expect(html).toContain('ᛞ');
  });
});

describe('ProjectCard', () => {
  const base = {
    sigil: 'ST',
    gradient: ['#1a1233', '#2b2a72', '#00f5d4'],
    stops: [50, 150],
    tech: ['Go', 'PWA'],
    status: { label: 'In progress', kind: 'gold' as const },
    links: [
      { label: 'GitHub', href: '#' },
      { label: 'Devlog', href: '#' },
    ],
    title: 'Saga Tracker',
    description: 'A quest log for real life.',
    plate: 'side quest',
  };
  it('renders title, description, sigil, plate, tech chips and a gold status chip', async () => {
    const c = await AstroContainer.create();
    const html = await c.renderToString(ProjectCard, { props: base });
    expect(html).toContain('<h3>Saga Tracker</h3>');
    expect(html).toContain('A quest log for real life.');
    expect(html).toContain('<span class="sigil">ST</span>');
    expect(html).toContain('<span class="plate">side quest</span>');
    expect(html).toContain('<span class="chip tq">Go</span>');
    expect(html).toContain('<span class="chip tq">PWA</span>');
    expect(html).toContain('<span class="chip gold">In progress</span>');
    expect(html).toContain('background: linear-gradient(135deg, #1a1233, #2b2a72 50%, #00f5d4 150%)');
    expect(html).toContain('<a href="#">GitHub</a>');
    expect(html).toContain('<a href="#">Devlog</a>');
  });
  it('renders a plain (non-gold) status chip', async () => {
    const c = await AstroContainer.create();
    const html = await c.renderToString(ProjectCard, {
      props: { ...base, status: { label: 'Production', kind: 'plain' as const } },
    });
    expect(html).toContain('<span class="chip">Production</span>');
    expect(html).not.toContain('<span class="chip gold">Production</span>');
  });
  it('omits the status chip entirely when the card has none', async () => {
    const c = await AstroContainer.create();
    const html = await c.renderToString(ProjectCard, { props: { ...base, status: undefined } });
    expect(html).not.toContain('gold');
    expect((html.match(/class="chip tq"/g) ?? []).length).toBe(2);
  });
});
