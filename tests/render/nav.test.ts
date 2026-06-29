import { experimental_AstroContainer as AstroContainer } from 'astro/container';
import { describe, it, expect } from 'vitest';
import Nav from '../../src/components/Nav.astro';

async function renderNav(page: string) {
  const container = await AstroContainer.create();
  return container.renderToString(Nav, {
    props: { page },
    request: new Request('https://jonnxor.is/about'),
  });
}

describe('Nav.astro', () => {
  it('renders the chrome: wordmark, theme + lang controls, dock', async () => {
    const html = await renderNav('about');
    expect(html).toContain('class="site-nav"');
    expect(html).toContain('JONN');
    expect(html).toContain('XOR');
    expect(html).toContain('data-set="dawn"');
    expect(html).toContain('data-set="rune"');
    expect(html).toContain('data-set="neon"');
    expect(html).toContain('data-lang="is"');
    expect(html).toContain('data-lang="en"');
    expect(html).toContain('data-lang="ja"');
    expect(html).toContain('class="jx-dock"');
    expect(html).toContain('nav-orb');
  });

  it('marks the current page active in the HUD', async () => {
    const html = await renderNav('about');
    expect(html).toMatch(/class="nav-hlink here"[^>]*href="\/about\/?"/);
  });

  it('does not mark a non-current page active', async () => {
    const html = await renderNav('cv');
    expect(html).not.toMatch(/class="nav-hlink here"[^>]*href="\/about\/?"/);
  });
});
