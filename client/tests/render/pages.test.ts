import { experimental_AstroContainer as AstroContainer } from 'astro/container';
import { describe, it, expect } from 'vitest';
import NotFound from '../../src/pages/404.astro';

const PAGES = [
  // games, countdowns, wallpapers, portfolio, index (home), about and cv are collection-backed
  // since the content port — the Container can't load astro:content under Vitest
  // (same reason blog/docs aren't here). Covered by tests/render/components.test.ts + e2e.
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
    expect(await container.renderToString(NotFound, { request: req })).toContain('ÞÚ DÓST');
  });
});
