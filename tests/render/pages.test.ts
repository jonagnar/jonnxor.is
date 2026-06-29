import { experimental_AstroContainer as AstroContainer } from 'astro/container';
import { describe, it, expect } from 'vitest';
import Index from '../../src/pages/index.astro';
import About from '../../src/pages/about.astro';
import Cv from '../../src/pages/cv.astro';
import Portfolio from '../../src/pages/portfolio.astro';
import Games from '../../src/pages/games.astro';
import Countdowns from '../../src/pages/countdowns.astro';
import Wallpapers from '../../src/pages/wallpapers.astro';
import NotFound from '../../src/pages/404.astro';

const PAGES = [
  ['index', Index],
  ['about', About],
  ['cv', Cv],
  ['portfolio', Portfolio],
  ['games', Games],
  ['countdowns', Countdowns],
  ['wallpapers', Wallpapers],
  ['404', NotFound],
] as const;

describe('static page smoke render', () => {
  it.each(PAGES)('renders %s with nav + footer chrome', async (_name, Page) => {
    const container = await AstroContainer.create();
    const html = await container.renderToString(Page, {
      request: new Request('https://jonnxor.is/'),
    });
    expect(html).toContain('class="site-nav"');
    expect(html).toContain('class="site-footer"');
    expect(html.length).toBeGreaterThan(2000);
  });

  it('renders confirmed page landmarks', async () => {
    const container = await AstroContainer.create();
    const req = new Request('https://jonnxor.is/');
    expect(await container.renderToString(Cv, { request: req })).toContain('The Record of Deeds');
    expect(await container.renderToString(Countdowns, { request: req })).toContain('The Reckoning');
    expect(await container.renderToString(Games, { request: req })).toContain('The Game Hall');
    expect(await container.renderToString(NotFound, { request: req })).toContain('ÞÚ DÓST');
  });
});
