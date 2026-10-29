"""Validate topology and exact runtime copies of the A3 mesh/palette."""
from pathlib import Path
import hashlib
import json
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
design = root / 'design/abyss-eclipse'
subprocess.run([sys.executable, str(design / 'validate.py')], check=True)
manifest = json.loads((root / 'docs/assets/abyss-reaper/manifest.json').read_text(encoding='utf-8'))
assert manifest['runtimeIntegrated'] and manifest['meshes'] == 32 and manifest['triangles'] == 9260
assert manifest['surfaceAreaFractions']['brightVoidInlays'] < .08
for source, target in (
    ('AbyssEclipseCandidate.dae', 'src/SCIEPort/Assets/Models/AbyssReaper.dae'),
    ('AbyssEclipseCandidate.png', 'src/SCIEPort/Assets/Textures/AbyssReaper.png'),
):
    data = (root / target).read_bytes()
    assert data == (design / 'assets' / source).read_bytes(), target
    assert hashlib.sha256(data).hexdigest().upper() == manifest['sha256'][source], target
for name in ('geometry.json', 'palette.json'):
    assert (root / 'docs/assets/abyss-reaper' / name).read_bytes() == (design / 'assets' / name).read_bytes()
print('Abyss runtime A3: topology, winding, closed volumes, UV, opaque palette, narrow emission and exact asset hashes passed.')
