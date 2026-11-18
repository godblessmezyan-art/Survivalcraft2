"""Rebuild the original embedded Android picker; external tools are local, pinned references only."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--tools', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args()
tools = args.tools.resolve()
work = args.output.resolve()
work.mkdir(parents=True, exist_ok=True)
pins = {
    'android-4.1.1.4.jar': ('84072541cbb711eff89f7277100ff854929a446dba7ceb1b195c340e0b4fd3cb', 'https://repo.maven.apache.org/maven2/com/google/android/android/4.1.1.4/android-4.1.1.4.jar'),
    'r8-8.3.37.jar': ('59753e70a74f918389cc87f1b7d66b5c0862932559167425708ded159e3de439', 'https://dl.google.com/dl/android/maven2/com/android/tools/r8/8.3.37/r8-8.3.37.jar'),
}
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
for name, (expected, _) in pins.items():
    if sha(tools/name) != expected: raise SystemExit('Tool hash mismatch: '+name)
classes, dex = work/'classes', work/'dex'
classes.mkdir(exist_ok=True); dex.mkdir(exist_ok=True)
source = root/'tools/codex-picker/CodexDocumentPicker.java'
test = root/'tools/codex-picker/PickerCoreTests.java'
subprocess.run(['javac', '--release', '8', '-encoding', 'UTF-8', '-classpath', str(tools/'android-4.1.1.4.jar'), '-d', str(classes), str(source), str(test)], check=True)
subprocess.run(['java', '-cp', str(classes)+';'+str(tools/'android-4.1.1.4.jar'), 'org.scieport.codex.PickerCoreTests'], check=True)
subprocess.run(['java', '-cp', str(tools/'r8-8.3.37.jar'), 'com.android.tools.r8.D8', '--release', '--no-desugaring', '--min-api', '23', '--lib', str(tools/'android-4.1.1.4.jar'), '--output', str(dex)] + [str(p) for p in sorted(classes.rglob('CodexDocumentPicker*.class'))], check=True)
target = root/'src/SCIEPort/Features/CelestialCodex/Android/DocumentPicker.dex'
target.parent.mkdir(parents=True, exist_ok=True)
target.write_bytes((dex/'classes.dex').read_bytes())
manifest = {
    'source': str(source.relative_to(root)).replace('\\', '/'),
    'sourceSha256': sha(source), 'dexSha256': sha(target), 'dexBytes': target.stat().st_size,
    'minApi': 23, 'javaRelease': 8, 'compiler': subprocess.check_output(['javac', '-version'], text=True).strip(),
    'tools': {name: {'sha256': digest, 'url': url} for name, (digest, url) in pins.items()},
    'scope': 'Original project Fragment and bounded stream copy. No game/Android SDK/AndroidX classes embedded.'
}
(root/'tools/codex-picker/manifest.json').write_text(json.dumps(manifest, indent=2)+'\n', encoding='utf-8')
print('Original picker DEX rebuilt:', sha(target), target.stat().st_size)
