// Generate the favicon raster set from public/favicon.svg.
//
//   corepack pnpm install          # brings in sharp (already in the lockfile via Astro)
//   node scripts/generate-icons.mjs
//
// Outputs (committed, served statically from public/):
//   apple-touch-icon.png (180)  icon-192.png  icon-512.png  favicon.ico (16/32/48)
//
// favicon.svg is the single source of truth. Rasters are flattened onto the brand
// near-black (#07060c), so the rounded-corner tile becomes a full opaque square; the OS
// masks the corners for home-screen icons. The .ico uses PNG-embedded entries (a tiny
// inline encoder below), so no extra dependency is needed.

import { readFile, writeFile } from 'node:fs/promises';
import sharp from 'sharp';

const PUBLIC = new URL('../public/', import.meta.url);
const TILE = '#07060c';

const svg = await readFile(new URL('favicon.svg', PUBLIC));

// Render one high-resolution master from the vector, then take crisp lanczos downscales
// for every target size. density 2304 on a 32-unit viewBox → a 1024px intrinsic raster.
const master = await sharp(svg, { density: 2304 })
  .resize(1024, 1024, { fit: 'contain', background: TILE })
  .flatten({ background: TILE })
  .png()
  .toBuffer();

const png = (size) =>
  sharp(master).resize(size, size, { fit: 'cover' }).png({ compressionLevel: 9 }).toBuffer();

async function writePng(name, size) {
  const buf = await png(size);
  await writeFile(new URL(name, PUBLIC), buf);
  console.log(`  ${name.padEnd(22)} ${size}×${size}\t${buf.length} B`);
}

// PNG-embedded ICO: a 6-byte ICONDIR, then a 16-byte ICONDIRENTRY per image, then the PNG
// blobs. A width/height byte of 0 means 256. Supported by every modern browser + Windows.
function buildIco(images /* [{ size, buffer }] */) {
  const header = Buffer.alloc(6);
  header.writeUInt16LE(0, 0); // reserved
  header.writeUInt16LE(1, 2); // type: 1 = icon
  header.writeUInt16LE(images.length, 4);

  const entries = [];
  const blobs = [];
  let offset = 6 + images.length * 16;
  for (const { size, buffer } of images) {
    const e = Buffer.alloc(16);
    e.writeUInt8(size >= 256 ? 0 : size, 0); // width
    e.writeUInt8(size >= 256 ? 0 : size, 1); // height
    e.writeUInt8(0, 2); // palette size
    e.writeUInt8(0, 3); // reserved
    e.writeUInt16LE(1, 4); // color planes
    e.writeUInt16LE(32, 6); // bits per pixel
    e.writeUInt32LE(buffer.length, 8); // image data length
    e.writeUInt32LE(offset, 12); // image data offset
    entries.push(e);
    blobs.push(buffer);
    offset += buffer.length;
  }
  return Buffer.concat([header, ...entries, ...blobs]);
}

console.log('Generating favicon raster set from public/favicon.svg …');
await writePng('apple-touch-icon.png', 180);
await writePng('icon-192.png', 192);
await writePng('icon-512.png', 512);

const icoSizes = [16, 32, 48];
const icoImages = await Promise.all(
  icoSizes.map(async (size) => ({ size, buffer: await png(size) })),
);
const ico = buildIco(icoImages);
await writeFile(new URL('favicon.ico', PUBLIC), ico);
console.log(`  favicon.ico            ${icoSizes.join('/')}\t\t${ico.length} B`);
console.log('Done.');
