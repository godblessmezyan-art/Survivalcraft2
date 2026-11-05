"""Offline resource, reproducibility and presentation-only regression guards."""
import copy
import hashlib
import json
import re
import subprocess
import tempfile
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / 'src/SCIEPort'
TOOLS = ROOT / 'tools/pixelfx'
checks = 0


def check(value, label):
    global checks
    checks += 1
    if not value:
        raise AssertionError(label)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def validate(manifest, png, generated):
    check(manifest['schema'] == 'SCIEPort.SpriteFx/1', 'schema')
    check(manifest['pngSha256'] == digest(png), 'PNG hash')
    check((manifest['width'], manifest['height'], manifest['cell'], manifest['columns']) == (1024, 1024, 96, 10), 'atlas layout')
    check(manifest['alpha'] == 'straight' and manifest['sampler'] == 'PointClamp' and not manifest['mipmaps'], 'native texture policy')
    expected = ['charge-up', 'lightning-strike', 'thunderclap']
    check([c['name'] for c in manifest['clips']] == expected, 'three requested effects')
    end = 0
    for clip in manifest['clips']:
        check(clip['firstFrame'] == end and clip['frameCount'] > 0, 'no overlapping/missing clip frames')
        end += clip['frameCount']
        expression = (f'new SpriteFxClip("{clip["name"]}", {clip["firstFrame"]}, {clip["frameCount"]}, '
                      f'{clip["fps"]}, 1024, 1024, 10, 96, {clip["pivotX"]}f, {clip["pivotY"]}f, '
                      f'SpriteFxFacing.{clip["facing"]}, {clip["priority"]})')
        check(expression in generated, 'JSON/generated C# frame table identity')
        check(0 <= clip['pivotX'] <= 1 and 0 <= clip['pivotY'] <= 1, 'pivot')
    check(end == manifest['frameCount'] == 78 and end <= 100, 'frame capacity')


def main():
    png_path = PROJECT / 'Assets/Textures/PixelFxStormhammer.png'
    manifest = json.loads((PROJECT / 'Assets/Effects/PixelFxStormhammer.json').read_text('utf-8'))
    generated = (PROJECT / 'Features/PixelFx/StormhammerSpriteClips.g.cs').read_text('utf-8')
    png = png_path.read_bytes()
    validate(manifest, png, generated)
    for mutation in ('checksum', 'overlap', 'table'):
        bad = copy.deepcopy(manifest)
        if mutation == 'checksum':
            bad['pngSha256'] = '0' * 64
        elif mutation == 'overlap':
            bad['clips'][1]['firstFrame'] = 0
        else:
            bad['clips'][0]['pivotX'] = 0
        try:
            validate(bad, png, generated)
        except AssertionError:
            pass
        else:
            raise AssertionError('negative control accepted: ' + mutation)
    image = Image.open(png_path)
    check(image.mode == 'RGBA' and image.size == (1024, 1024), 'PNG format')
    alpha = image.getchannel('A')
    check(alpha.getextrema() == (0, 255), 'transparent background and bright core')
    for f in range(100):
        x, y = f % 10 * 96, f // 10 * 96
        tile = alpha.crop((x, y, x+96, y+96))
        if f >= 78:
            check(tile.getbbox() is None, 'unused atlas cell transparent')
            continue
        check(tile.getbbox() is not None, 'every exported frame contains effect')
        for bounds in [(0,0,96,2), (0,94,96,96), (0,0,2,96), (94,0,96,96)]:
            check(tile.crop(bounds).getbbox() is None, 'two-pixel gutter')
    check(alpha.crop((960,0,1024,1024)).getbbox() is None and alpha.crop((0,960,1024,1024)).getbbox() is None, 'atlas remainder empty')
    for entry in manifest['upstream']['files']:
        data = (TOOLS / 'upstream' / entry['path']).read_bytes()
        blob = hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()
        check(blob == entry['gitBlob'] and digest(data) == entry['sha256'], 'pinned upstream blob/SHA256: ' + entry['path'])
    license_text = (PROJECT / 'Assets/Effects/PixelFX-LICENSE.txt').read_bytes()
    check(license_text == (TOOLS / 'upstream/LICENSE').read_bytes(), 'complete MIT license ships')
    check(b'2026 Patch' in license_text and b'2026 Hugo Duprez' in license_text, 'both copyright holders')
    artifacts = ROOT / 'artifacts'
    artifacts.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='pixelfx-export-', dir=artifacts) as directory:
        subprocess.run(['node', str(TOOLS / 'export.mjs'), directory], check=True)
        for name in ('Assets/Textures/PixelFxStormhammer.png', 'Assets/Effects/PixelFxStormhammer.json',
                     'Assets/Effects/PixelFX-LICENSE.txt', 'Features/PixelFx/StormhammerSpriteClips.g.cs'):
            check((Path(directory) / name).read_bytes() == (PROJECT / name).read_bytes(), 'byte-reproducible offline export: ' + name)
    baseline = json.loads((TOOLS / 'gameplay-baseline.json').read_text('utf-8'))
    for path, expected in baseline['files'].items():
        source = (PROJECT / path).read_bytes().replace(b'\r\n', b'\n')
        if path.startswith('Features/Stormhammer/') and path.endswith('.cs'):
            # Only the explicit additive integration lines are removable. All preexisting code stays frozen.
            source = source.replace(b'using SCIEPort.Features.PixelFx;\n', b'')
            source = re.sub(rb'^ +scene\?\.SpriteFx\.Play\([^\n]+\);\n', b'', source, flags=re.M)
            source = source.replace(b'if (m_scene == null)\n', b'if (m_scene == null || m_scene.ActiveBolts == 0)\n')
        if path.endswith('/LightningScene.cs'):
            addition = ('        private readonly SpriteFxScene m_spriteFx = new SpriteFxScene();\n\n'
                        "        /// <summary>Presentation-only sprite pool, sharing this scene's renderer and clock.</summary>\n"
                        '        internal SpriteFxScene SpriteFx => m_spriteFx;\n')
            source = source.replace(addition.encode(), b'')
            for line in ('            m_spriteFx.Load(m_renderer, StormhammerSpriteClips.Texture);\n',
                         '            m_spriteFx.Clear();\n', '            m_renderer?.Clear();\n',
                         '            m_spriteFx.Player.Update(now, (int)StormhammerOptions.Quality);\n',
                         '            m_spriteFx.Queue(viewPosition, viewForward, now, (int)StormhammerOptions.Quality, StormhammerOptions.ReducedFlashes);\n',
                         '            LastQuadCount += m_spriteFx.LastQuadCount;\n'):
                source = source.replace(line.encode(), b'')
        check(digest(source) == expected, 'preserved mechanics/3D/persistence/protocol/attributes: ' + path)
    runtime = '\n'.join(p.read_text('utf-8') for p in (PROJECT / 'Features/PixelFx').glob('*.cs'))
    check(not re.search(r'\b(AttackBody|Injure|ChangeCell|DestroyCell|SetCellValueFast|ValuesDictionary|IPackage|SendPackage|WalkSpeed|m_lastHitTime)\b', runtime), 'sprite runtime isolated from gameplay/save/network')
    print(f'{checks} PixelFX resource/reproducibility/preservation checks passed (three corrupt-manifest negative controls rejected).')


if __name__ == '__main__':
    main()
