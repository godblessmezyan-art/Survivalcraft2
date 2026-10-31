"""Package regression: V2 upgrade, --visual-only patch, or --metadata-only for code/version changes."""
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
    p.add_argument('--visual-only', action='store_true')
    p.add_argument('--metadata-only', action='store_true')
    p.add_argument('--death-authority', action='store_true')
    args = p.parse_args()
    with ZipFile(args.baseline) as old, ZipFile(args.candidate) as new:
        before, after = set(old.namelist()), set(new.namelist())
        added = after-before
        changed = {name for name in before & after if sha(old.read(name)) != sha(new.read(name))}
        code_only = args.visual_only or args.metadata_only
        expected = set() if code_only else {'Assets/Textures/Abyss'+name+'V2.png' for name in ['WorldCut', 'Blink', 'Shatter']}
        if args.death_authority:
            expected = {'Assets/Textures/AbyssDeath'+name+'.png' for name in ['Switch', 'Empower', 'Wave', 'Guardian']}
        assert added == expected and not before-after, ('unexpected entries', added, before-after)
        expected_changed = {'SCIEPort.dll', 'modinfo.json'} if code_only else {'SCIEPort.dll', 'modinfo.json', 'THIRD-PARTY-NOTICES.md'}
        assert changed == expected_changed, ('unexpected changes', changed)
        if not args.metadata_only:
            assert json.loads(old.read('modinfo.json'))['Version'] == ('0.62.0' if args.death_authority else '0.61.0' if args.visual_only else '0.60.0')
            assert json.loads(new.read('modinfo.json'))['Version'] == ('0.63.0' if args.death_authority else '0.61.1' if args.visual_only else '0.61.0')
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
