"""Compose already-rendered Blender previews and audit package preservation.
Run after Optimize-BoneDragonBlender.py and the Release build. No artwork is
generated here: comparison/animation are composed from actual Blender renders.
"""
from pathlib import Path
import hashlib
import json
import shutil
import zipfile
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
DESIGN = ROOT / 'design/bone-dragon'
WORK = ROOT / 'artifacts/bone-dragon-blender'


def sha(data):
    return hashlib.sha256(data).hexdigest().upper()


def write(name, data):
    (DESIGN / name).write_text(json.dumps(data, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')


def previews():
    font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 25)
    small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 19)
    panel = Image.new('RGB', (1600, 680), (22, 29, 37))
    draw = ImageDraw.Draw(panel)
    for i, (file, title) in enumerate([('blender-before-flight.png', '优化前 · 7,674 三角形'),
                                       ('blender-flight.png', '优化后 · 5,862 三角形')]):
        image = Image.open(DESIGN / file).convert('RGB').resize((800, 587), Image.Resampling.LANCZOS)
        panel.paste(image, (i * 800, 55))
        draw.text((i * 800 + 28, 15), title, font=font, fill=(222, 230, 230))
    draw.text((28, 646), 'Blender 5.1.1 模型预览 · 非游戏画面', font=small, fill=(155, 185, 198))
    panel.save(DESIGN / 'blender-comparison.png')
    multiview = Image.new('RGB', (1800, 960), (22, 29, 37))
    for i, view in enumerate(['front', 'side', 'back', 'top', 'ground', 'head']):
        image = Image.open(DESIGN / f'blender-{view}.png').convert('RGB').resize((600, 440), Image.Resampling.LANCZOS)
        x, y = (i % 3) * 600, (i // 3) * 480
        multiview.paste(image, (x, y + 35))
        ImageDraw.Draw(multiview).text((x + 15, y + 8), view.upper() + ' | BLENDER', fill=(180, 210, 220))
    multiview.save(DESIGN / 'blender-multiview.png')
    for view in ('flight', 'ground'):
        shutil.copyfile(DESIGN / f'blender-{view}.png', DESIGN / f'preview-{view}.png')
    frames = []
    for path in sorted(WORK.glob('flight-*.png')):
        image = Image.open(path).convert('RGB')
        ImageDraw.Draw(image).text((15, 15), 'Blender pose test - not gameplay', fill=(180, 210, 220))
        frames.append(image)
    assert len(frames) == 12
    frames[0].save(DESIGN / 'flight-preview.gif', save_all=True, append_images=frames[1:], duration=85, loop=0)
    write('reference_manifest.json', {'source': 'original project-authored model at 5f429e1700c7ba44a0d823c8d237edfcc2caffe4',
          'baseline_model_sha256': 'D020CD9BA62B407D9FC3D127FB11E23B15D29AB123745AF71DB884929FD048BF',
          'external_artwork': [], 'scale_source': 'existing game bone pivots, saddle and unit scale',
          'style': 'Survivalcraft faceted skeletal dragon, existing ivory/cyan/purple palette',
          'skills': ['text-to-blender', 'blender-pro-workflow', 'blender-skill-harmonizer',
                     'blender-modeling', 'blender-uv-texturing', 'blender-lighting', 'blender-cameras', 'blender-rendering'],
          'script_hash_encoding': 'UTF-8 source bytes with LF newlines',
          'scripts': {p.name: sha(p.read_bytes().replace(b'\r\n', b'\n')) for p in [ROOT / 'scripts/New-BoneDragonAssets.py',
                      ROOT / 'scripts/Optimize-BoneDragonBlender.py']},
          'previews_are_gameplay': False})


def package():
    old = ROOT / 'artifacts/荒野探险家-0.83.0.netmod'
    new = ROOT / 'artifacts/荒野探险家-0.84.0.netmod'
    canonical = ROOT / 'artifacts/荒野探险家.netmod'
    assert sha(new.read_bytes()) == sha(canonical.read_bytes())
    with zipfile.ZipFile(old) as a, zipfile.ZipFile(new) as b:
        assert len(a.namelist()) == len(set(a.namelist()))
        assert len(b.namelist()) == len(set(b.namelist()))
        assert set(a.namelist()) == set(b.namelist())
        changed = sorted(n for n in a.namelist() if sha(a.read(n)) != sha(b.read(n)))
        expected = ['Assets/Models/AbyssBoneDragon.dae', 'SCIEPort.dll', 'THIRD-PARTY-NOTICES.md', 'modinfo.json']
        assert changed == expected, changed
        model = b.read('Assets/Models/AbyssBoneDragon.dae')
        assert model == (ROOT / 'src/SCIEPort/Assets/Models/AbyssBoneDragon.dae').read_bytes()
        assert b.read('Assets/Textures/AbyssBoneDragon.png') == a.read('Assets/Textures/AbyssBoneDragon.png')
        assert json.loads(b.read('modinfo.json'))['Version'] == '0.84.0'
        write('package-comparison.json', {'baseline': old.name, 'package': new.name, 'entries': len(b.namelist()),
              'changed_entries': changed, 'all_other_entries_byte_identical': True,
              'canonical_identical': True, 'model_sha256': sha(model), 'sha256': sha(new.read_bytes()),
              'bytes': new.stat().st_size, 'android_gameplay': '待验证'})


if __name__ == '__main__':
    previews()
    package()
    print('Blender preview provenance and 0.84.0 package preservation passed.')
