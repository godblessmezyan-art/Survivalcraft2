// Offline, dependency-free exporter. The pinned MIT sources below are never executed in-game.
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { dirname, resolve, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { createHash } from 'node:crypto';
import { createFrame, createFx, clearFrame, composite } from './upstream/src/core/index.js';
import { encodePNG } from './upstream/tools/lib/png.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, '../..');
const output = process.argv[2] ? resolve(process.argv[2]) : join(root, 'src/SCIEPort');
const sha = (b) => createHash('sha256').update(b).digest('hex');
const sources = JSON.parse(readFileSync(join(here, 'sources.json'), 'utf8')).map((f) => {
  const bytes = readFileSync(join(here, 'upstream', f.path));
  const blob = createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex');
  if (blob !== f.gitBlob) throw new Error('Pinned source changed: ' + f.path);
  return { ...f, sha256: sha(bytes) };
});
const cell = 96, padding = 2, size = 1024, height = 2048, columns = 10, fps = 24;
const definitions = [
  { name: 'charge-up', field: 'ChargeUp', start: 0.25, count: 33, seed: 4, anchor: [15, -21], facing: 'Screen', priority: 1 },
  { name: 'lightning-strike', field: 'LightningStrike', start: 0.76, count: 21, seed: 5, anchor: [0, 0], facing: 'Vertical', priority: 3 },
  { name: 'thunderclap', field: 'Thunderclap', start: 0.12, count: 24, seed: 6, anchor: [0, 0], facing: 'Vertical', priority: 2 },
  { name: 'holy/blessing', field: 'HolyBlessing', start: 0.4, count: 16, seed: 7, anchor: [0, 0], facing: 'Vertical', priority: 1 },
  { name: 'holy/sunburst', field: 'HolySunburst', start: 0.2, count: 16, seed: 3, anchor: [0, 0], facing: 'Screen', priority: 2 },
  { name: 'holy/smite', field: 'HolySmite', start: 0.54, count: 20, seed: 9, anchor: [0, 0], facing: 'Vertical', priority: 3 },
  { name: 'water/splash', field: 'WaterSplash', start: 0.16, count: 18, seed: 3, anchor: [0, 0], facing: 'Vertical', priority: 1, options: { stone: false } },
  { name: 'water/wave-crash', field: 'WaterWave', start: 1.2, count: 20, seed: 6, anchor: [0, 0], facing: 'Vertical', priority: 2 },
  { name: 'shadow/dark-explosion', field: 'DarkExplosion', start: 0.52, count: 18, seed: 6, anchor: [0, -54], facing: 'Screen', priority: 2 },
];
const rgba = Buffer.alloc(size * height * 4), clips = [];
let first = 0;
for (const def of definitions) {
  const sourceId = def.name.includes('/') ? def.name : 'lightning/' + def.name;
  const mod = await import(pathToFileURL(join(here, 'upstream/src/fx', sourceId + '.js')));
  // The stock actor is a preview prop. An empty mask removes actor silhouette/edge lighting.
  const actor = { x: 0, y: 0, mask: { w: 0, h: 0, data: new Uint8Array(0) } };
  const fx = createFx(mod, { ...def.options, seed: def.seed, actor });
  const [w, h] = mod.meta.size, [ox, oy] = mod.meta.origin;
  const frame = createFrame(w, h, -ox, -oy), pixels = Buffer.alloc(w * h * 4);
  const scale = (cell - padding * 2) / Math.max(w, h);
  const dw = Math.round(w * scale), dh = Math.round(h * scale);
  const left = Math.floor((cell - dw) / 2), top = Math.floor((cell - dh) / 2);
  fx.update(def.start);
  for (let f = 0; f < def.count; f++) {
    clearFrame(frame); fx.draw(frame); composite(frame, pixels, null);
    const tile = first + f, dx = tile % columns * cell, dy = Math.floor(tile / columns) * cell;
    if (dy + cell > height) throw new Error('Atlas capacity exceeded');
    // Area-filter premultiplied RGBA, then restore straight alpha for the native PNG reader.
    // This keeps fine arcs visible when a source pixel covers less than one output pixel.
    for (let y = 0; y < dh; y++) for (let x = 0; x < dw; x++) {
      const x0 = x * w / dw, x1 = (x + 1) * w / dw, y0 = y * h / dh, y1 = (y + 1) * h / dh;
      let a = 0, r = 0, g = 0, b = 0;
      for (let sy = Math.floor(y0); sy < Math.ceil(y1); sy++) for (let sx = Math.floor(x0); sx < Math.ceil(x1); sx++) {
        const weight = (Math.min(x1, sx + 1) - Math.max(x0, sx)) * (Math.min(y1, sy + 1) - Math.max(y0, sy));
        const i = (sy * w + sx) * 4, k = pixels[i + 3] / 255 * weight;
        a += k; r += pixels[i] * k; g += pixels[i + 1] * k; b += pixels[i + 2] * k;
      }
      const i = ((dy + top + y) * size + dx + left + x) * 4;
      if (a > 0) {
        rgba[i] = Math.round(r / a); rgba[i + 1] = Math.round(g / a); rgba[i + 2] = Math.round(b / a);
        rgba[i + 3] = Math.round(a / ((x1 - x0) * (y1 - y0)) * 255);
        if (rgba[i + 3] === 0) rgba.fill(0, i, i + 4);
      }
    }
    fx.update(1 / fps);
  }
  clips.push({ name: def.name, field: def.field, firstFrame: first, frameCount: def.count, fps,
    pivotX: +( (left + (ox + def.anchor[0]) * dw / w) / cell).toFixed(6),
    pivotY: +( (top + (oy + def.anchor[1]) * dh / h) / cell).toFixed(6),
    facing: def.facing, priority: def.priority, sourceStart: def.start, seed: def.seed,
    sourceSize: [w, h], sourceOrigin: mod.meta.origin, sourceAnchor: def.anchor,
    ...(def.options ? { options: def.options } : {}) });
  first += def.count;
}
const png = encodePNG(size, height, rgba);
const manifest = { schema: 'SCIEPort.SpriteFx/1', version: '0.2',
  upstream: { repository: 'https://github.com/patch-gg/pixelfx', commit: 'a84159688af3ca37a4be05a0d422b94451b2e24e', license: 'MIT',
    files: sources },
  texture: 'Textures/PixelFxStormhammer', width: size, height, cell, padding, columns,
  alpha: 'straight', blend: 'additive', sampler: 'PointClamp', mipmaps: false,
  pngSha256: sha(png), frameCount: first, clips };
function save(path, data) { mkdirSync(dirname(path), { recursive: true }); writeFileSync(path, data); }
save(join(output, 'Assets/Textures/PixelFxStormhammer.png'), png);
save(join(output, 'Assets/Effects/PixelFxStormhammer.json'), JSON.stringify(manifest, null, 2) + '\n');
save(join(output, 'Assets/Effects/PixelFX-LICENSE.txt'), readFileSync(join(here, 'upstream/LICENSE')));
const generated = `// Generated by tools/pixelfx/export.mjs. Do not hand edit.\nnamespace SCIEPort.Features.PixelFx\n{\n    internal static class StormhammerSpriteClips\n    {\n        internal const string Texture = ArtifactSpriteClips.Texture;\n${clips.slice(0,3).map(c => `        internal static readonly SpriteFxClip ${c.field} = ArtifactSpriteClips.${c.field};`).join('\n')}\n    }\n}\n`;
save(join(output, 'Features/PixelFx/StormhammerSpriteClips.g.cs'), generated);
const shared = `// Generated by tools/pixelfx/export.mjs. Do not hand edit.\nnamespace SCIEPort.Features.PixelFx\n{\n    internal static class ArtifactSpriteClips\n    {\n        internal const string Texture = "${manifest.texture}";\n${clips.map(c => `        internal static readonly SpriteFxClip ${c.field} = new SpriteFxClip("${c.name}", ${c.firstFrame}, ${c.frameCount}, ${fps}, ${size}, ${height}, ${columns}, ${cell}, ${c.pivotX}f, ${c.pivotY}f, SpriteFxFacing.${c.facing}, ${c.priority});`).join('\n')}\n    }\n}\n`;
save(join(output, 'Features/PixelFx/ArtifactSpriteClips.g.cs'), shared);
console.log(`PixelFX 0.2: ${first} frames, ${size}x${height}, ${png.length} PNG bytes, SHA256 ${sha(png)}`);
