"""Optional 0.60.0 -> 0.61.0 package regression; requires the user's retained baseline netmod."""
import argparse
import hashlib
import json
from pathlib import Path
from zipfile import ZipFile


def sha(data):
    return hashlib.sha256(data).hexdigest().upper()


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('baseline', type=Path)
    p.add_argument('candidate', type=Path)
    p.add_argument('--report', type=Path)
    args = p.parse_args()
    with ZipFile(args.baseline) as old, ZipFile(args.candidate) as new:
        before, after = set(old.namelist()), set(new.namelist())
        added = after-before
        changed = {name for name in before & after if sha(old.read(name)) != sha(new.read(name))}
        expected = {'Assets/Textures/Abyss'+name+'V2.png' for name in ['WorldCut', 'Blink', 'Shatter']}
        assert added == expected and not before-after, ('unexpected entries', added, before-after)
        assert changed == {'SCIEPort.dll', 'modinfo.json', 'THIRD-PARTY-NOTICES.md'}, ('unexpected changes', changed)
        assert json.loads(old.read('modinfo.json'))['Version'] == '0.60.0'
        assert json.loads(new.read('modinfo.json'))['Version'] == '0.61.0'
        root = Path(__file__).resolve().parents[1]
        assert new.read('THIRD-PARTY-NOTICES.md') == (root/'THIRD-PARTY-NOTICES.md').read_bytes()
        assert new.read('SCIEPort.dll') == (root/'artifacts/build/SCIEPort.dll').read_bytes()
        report = dict(baseline_sha256=sha(args.baseline.read_bytes()), package_sha256=sha(args.candidate.read_bytes()),
                      dll_sha256=sha(new.read('SCIEPort.dll')), bytes=args.candidate.stat().st_size, entries=len(after),
                      added=sorted(added), changed=sorted(changed), preserved=len(before)-len(changed))
    if args.report:
        args.report.write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
