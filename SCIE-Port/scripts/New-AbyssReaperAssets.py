"""Regenerate and install the original A3 geometry as the Abyss V0.2 runtime assets."""
from pathlib import Path
import json
import shutil
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
design = root / 'design/abyss-eclipse'
subprocess.run([sys.executable, str(design / 'generate.py')], check=True)
subprocess.run([sys.executable, str(design / 'validate.py')], check=True)
assets = root / 'src/SCIEPort/Assets'
audit = root / 'docs/assets/abyss-reaper'
for source, destination in (
    ('AbyssEclipseCandidate.dae', assets / 'Models/AbyssReaper.dae'),
    ('AbyssEclipseCandidate.png', assets / 'Textures/AbyssReaper.png'),
    ('geometry.json', audit / 'geometry.json'),
    ('palette.json', audit / 'palette.json'),
):
    shutil.copyfile(design / 'assets' / source, destination)
manifest = json.loads((design / 'assets/manifest.json').read_text(encoding='utf-8'))
manifest.update(runtimeIntegrated=True, runtimeVersion='0.47.0', artifactVersion='0.2')
(audit / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8', newline='\n')
print('Installed Abyss V0.2: 1 identity root / 32 meshes / 9260 triangles; device validation pending.')
