import { experimental_AstroContainer as AstroContainer } from 'astro/container';
import { describe, it, expect } from 'vitest';
import Footer from '../../src/components/Footer.astro';

describe('Footer.astro', () => {
  it('renders the footer chrome and contact rows', async () => {
    const container = await AstroContainer.create();
    const html = await container.renderToString(Footer, {
      request: new Request('https://jonnxor.is/'),
    });
    expect(html).toContain('class="site-footer"');
    expect(html).toContain('jon@jonnxor.is');
    expect(html).toContain('github.com/jonnxor');
    // status chip — locale-proof structural assertion ("chip tq" is unique to the status row)
    expect(html).toContain('class="chip tq"');
    // footer links back to cv (href carries a trailing slash via getRelativeLocaleUrl)
    expect(html).toMatch(/href="\/cv\/?"/);
  });
});
