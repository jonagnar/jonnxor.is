// Build public/favicon.svg from the REAL Orbitron glyphs — the same self-hosted variable
// font the JONNXOR logotype uses — so the favicon is genuinely on-brand (not an
// approximation). Extracts the 'J' and 'X' outlines at weight 800 and bakes them in as
// vector paths, optically centred on the near-black tile.
//
//   corepack pnpm add -D fontkit
//   node scripts/build-favicon.mjs        # writes public/favicon.svg
//   node scripts/generate-icons.mjs       # then rasterizes the set
//
// favicon.svg stays the single source of truth for the raster set.

import { readFile, writeFile } from 'node:fs/promises';
import * as fontkit from 'fontkit';
import { decompress } from 'wawoff2';

const FONT = new URL('../public/assets/fonts/yMJRMIlzdpvBhQQL_Qq7dy0.woff2', import.meta.url);
const OUT = new URL('../public/favicon.svg', import.meta.url);

const TEXT = 'JX';
const WEIGHT = 800;            // match the logotype
const TILE = 32;               // viewBox / tile size
const RADIUS = 7;              // rounded-tile corner (browser favicon)
const PAD = 3;                 // padding inside the tile (px)
const TRACK = 0;               // extra letter-spacing in font units (0 = font-natural)
const BG = '#07060c';
const COLORS = { J: '#f5e642', X: '#2cffe5' }; // gold J, turquoise X

const round = (n) => Math.round(n * 10000) / 10000;

// The self-hosted Orbitron is a variable WOFF2; fontkit's variation path needs a real sfnt,
// so decompress WOFF2 → TTF first (fontkit chokes on getVariation straight from WOFF2).
const ttf = await decompress(await readFile(FONT));
let font = fontkit.create(Buffer.from(ttf));
const axes = font.variationAxes ?? {};
if (axes.wght) font = font.getVariation({ wght: Math.min(WEIGHT, axes.wght.max) });
console.log(`Orbitron axes: ${Object.keys(axes).join(', ') || '(static)'} · unitsPerEm ${font.unitsPerEm}`);

// Lay the two glyphs out, record each glyph's pen x in font units.
const run = font.layout(TEXT);
let penX = 0;
const items = run.glyphs.map((glyph, i) => {
  const x = penX + (run.positions[i].xOffset || 0);
  penX += run.positions[i].xAdvance + TRACK;
  return { glyph, char: TEXT[i], x };
});

// Ink bounding box across both glyphs (font units, y-up).
let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
for (const { glyph, x } of items) {
  const b = glyph.bbox;
  minX = Math.min(minX, x + b.minX);
  maxX = Math.max(maxX, x + b.maxX);
  minY = Math.min(minY, b.minY);
  maxY = Math.max(maxY, b.maxY);
}

// Scale the ink box into the padded content area (keep aspect), then optically centre.
const content = TILE - 2 * PAD;
const S = content / Math.max(maxX - minX, maxY - minY);
const cx = (minX + maxX) / 2;
const cy = (minY + maxY) / 2;
const TX = TILE / 2 - S * cx;
const TY = TILE / 2 + S * cy; // +cy because the outer scale flips Y (scale S,-S)

const paths = items
  .map(({ glyph, char, x }) =>
    `    <path transform="translate(${round(x)} 0)" d="${glyph.path.toSVG()}" fill="${COLORS[char] ?? '#2cffe5'}"/>`,
  )
  .join('\n');

const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${TILE} ${TILE}" role="img" aria-label="JonnXor">
  <rect width="${TILE}" height="${TILE}" rx="${RADIUS}" fill="${BG}"/>
  <g transform="translate(${round(TX)} ${round(TY)}) scale(${round(S)} ${-round(S)})">
${paths}
  </g>
</svg>
`;

await writeFile(OUT, svg);
console.log(`favicon.svg written — Orbitron ${WEIGHT}, glyphs [${items.map((i) => i.char).join('')}], scale ${round(S)}`);
