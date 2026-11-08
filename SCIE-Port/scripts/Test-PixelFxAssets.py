"""Offline resource, reproducibility and presentation-only regression guards."""
import copy
import hashlib
import json
import re
import subprocess
import tempfile
from pathlib import Path
from PIL import Image, ImageChops

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
    check((manifest['width'], manifest['height'], manifest['cell'], manifest['columns']) == (1024, 2048, 96, 10), 'atlas layout')
    check(manifest['alpha'] == 'straight' and manifest['sampler'] == 'PointClamp' and not manifest['mipmaps'], 'native texture policy')
    expected = ['charge-up', 'lightning-strike', 'thunderclap', 'holy/blessing', 'holy/sunburst', 'holy/smite', 'water/splash', 'water/wave-crash', 'shadow/dark-explosion']
    check([c['name'] for c in manifest['clips']] == expected, 'nine selected effects')
    end = 0
    for clip in manifest['clips']:
        check(clip['firstFrame'] == end and clip['frameCount'] > 0, 'no overlapping/missing clip frames')
        end += clip['frameCount']
        expression = (f'new SpriteFxClip("{clip["name"]}", {clip["firstFrame"]}, {clip["frameCount"]}, '
                      f'{clip["fps"]}, 1024, 2048, 10, 96, {clip["pivotX"]}f, {clip["pivotY"]}f, '
                      f'SpriteFxFacing.{clip["facing"]}, {clip["priority"]})')
        check(expression in generated, 'JSON/generated C# frame table identity')
        check(0 <= clip['pivotX'] <= 1 and 0 <= clip['pivotY'] <= 1, 'pivot')
    check(end == manifest['frameCount'] == 186 and end <= 210, 'frame capacity')
    check(manifest['clips'][6]['options']['stone'] is False, 'splash preview stone removed')


def main():
    png_path = PROJECT / 'Assets/Textures/PixelFxStormhammer.png'
    manifest = json.loads((PROJECT / 'Assets/Effects/PixelFxStormhammer.json').read_text('utf-8'))
    generated = (PROJECT / 'Features/PixelFx/ArtifactSpriteClips.g.cs').read_text('utf-8')
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
    check(image.mode == 'RGBA' and image.size == (1024, 2048), 'PNG format')
    alpha = image.getchannel('A')
    check(alpha.getextrema() == (0, 255), 'transparent background and bright core')
    for f in range(210):
        x, y = f % 10 * 96, f // 10 * 96
        tile = alpha.crop((x, y, x+96, y+96))
        if f >= 186:
            check(tile.getbbox() is None, 'unused atlas cell transparent')
            continue
        check(tile.getbbox() is not None, 'every exported frame contains effect')
        for bounds in [(0,0,96,2), (0,94,96,96), (0,0,2,96), (94,0,96,96)]:
            check(tile.crop(bounds).getbbox() is None, 'two-pixel gutter')
    check(alpha.crop((960,0,1024,2048)).getbbox() is None and alpha.crop((0,2016,1024,2048)).getbbox() is None, 'atlas remainder empty')
    legacy = json.loads((TOOLS / 'legacy-frame-baseline.json').read_text('utf-8'))
    for f, expected in enumerate(legacy['frames']):
        x, y = f % 10 * 96, f // 10 * 96
        check(digest(image.crop((x,y,x+96,y+96)).tobytes()) == expected, 'old lightning frame RGBA retained')
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
        exported = Path(directory)
        exported_png = exported / 'Assets/Textures/PixelFxStormhammer.png'
        # Node/OS floating point and zlib can differ by one channel level.
        # The shipped PNG still has its own exact manifest hash and frame checks.
        with Image.open(exported_png) as regenerated:
            check(regenerated.mode == image.mode and regenerated.size == image.size, 'reproduced PNG dimensions')
            channels = ImageChops.difference(regenerated, image).split()
            changed = sum(sum(channel.histogram()[1:]) for channel in channels[:3])
            check(channels[3].getextrema() == (0, 0) and
                  all(channel.getextrema()[1] <= 1 for channel in channels[:3]) and changed <= 128,
                  'reproduced PNG channels within cross-platform rounding bounds')
        exported_manifest = json.loads((exported / 'Assets/Effects/PixelFxStormhammer.json').read_text('utf-8'))
        check(exported_manifest['pngSha256'] == digest(exported_png.read_bytes()), 'export PNG hash')
        exported_manifest['pngSha256'] = manifest['pngSha256']
        check(exported_manifest == manifest, 'export metadata other than PNG compression hash')
        for name in ('Assets/Effects/PixelFX-LICENSE.txt', 'Features/PixelFx/StormhammerSpriteClips.g.cs', 'Features/PixelFx/ArtifactSpriteClips.g.cs'):
            check((Path(directory) / name).read_bytes() == (PROJECT / name).read_bytes(), 'byte-reproducible offline export: ' + name)
    baseline = json.loads((TOOLS / 'gameplay-baseline.json').read_text('utf-8'))
    for path, expected in baseline['files'].items():
        source = (PROJECT / path).read_bytes().replace(b'\r\n', b'\n')
        if path.startswith('Features/Stormhammer/') and path.endswith('.cs'):
            # Only the explicit additive integration lines are removable. All preexisting code stays frozen.
            source = source.replace(b'using SCIEPort.Features.PixelFx;\n', b'')
            source = re.sub(rb'^ +scene\?\.SpriteFx\.Play\([^\n]+\);\n', b'', source, flags=re.M)
            source = source.replace(b'if (m_scene == null || !m_scene.HasEffects)\n', b'if (m_scene == null || m_scene.ActiveBolts == 0)\n')
        if path.endswith('/LightningScene.cs'):
            addition = ('        private readonly SpriteFxScene m_spriteFx = new SpriteFxScene();\n\n'
                        "        /// <summary>Presentation-only sprite pool, sharing this scene's renderer and clock.</summary>\n"
                        '        internal SpriteFxScene SpriteFx => m_spriteFx;\n')
            source = source.replace(addition.encode(), b'')
            idle_gate = ("\n        // Includes sprite-only tails and ground runes; idle scenes skip the renderer entirely.\n"
                         '        internal bool HasEffects\n        {\n            get\n            {\n'
                         '                if (ActiveBolts > 0 || m_spriteFx.Player.ActiveCount > 0) return true;\n'
                         '                for (int i = 0; i < m_runes.Length; i++) if (m_runes[i].Active) return true;\n'
                         '                return false;\n            }\n        }\n')
            source = source.replace(idle_gate.encode(), b'')
            for line in ('            m_spriteFx.Load(m_renderer, StormhammerSpriteClips.Texture);\n',
                         '            m_spriteFx.Clear();\n', '            m_renderer?.Clear();\n',
                         '            m_spriteFx.Player.Update(now, (int)StormhammerOptions.Quality);\n',
                         '            m_spriteFx.Queue(viewPosition, viewForward, camera.ViewProjectionMatrix, now, (int)StormhammerOptions.Quality, StormhammerOptions.ReducedFlashes);\n',
                         '            LastQuadCount += m_spriteFx.LastQuadCount;\n'):
                source = source.replace(line.encode(), b'')
        check(digest(source) == expected, 'preserved mechanics/3D/persistence/protocol/attributes: ' + path)
    artifacts_baseline = json.loads((TOOLS / 'artifact-baseline.json').read_text('utf-8'))
    for path, expected in artifacts_baseline['files'].items():
        source = (PROJECT / path).read_bytes().replace(b'\r\n', b'\n')
        source = re.sub(rb'^ +[^\n]*SCIEPort\.Features\.PixelFx\.ArtifactSpriteFx\.[^\n]+\n', b'', source, flags=re.M)
        check(digest(source) == expected, 'other artifact mechanics/3D/persistence/protocol retained: ' + path)
    runtime = '\n'.join(p.read_text('utf-8') for p in (PROJECT / 'Features/PixelFx').glob('*.cs'))
    check(not re.search(r'\b(AttackBody|Injure|ChangeCell|DestroyCell|SetCellValueFast|ValuesDictionary|IPackage|SendPackage|WalkSpeed|m_lastHitTime)\b', runtime), 'sprite runtime isolated from gameplay/save/network')
    print(f'{checks} PixelFX resource/reproducibility/preservation checks passed (three corrupt-manifest negative controls rejected).')


if __name__ == '__main__':
    main()
