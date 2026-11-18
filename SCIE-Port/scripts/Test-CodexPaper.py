"""Validate lightweight UI artwork and optionally prove a UI-only business boundary."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from PIL import Image

parser=argparse.ArgumentParser()
parser.add_argument("--base",help="optional verified base commit for this UI-only change")
args=parser.parse_args()
port=Path(__file__).resolve().parents[1]
repo=port.parent
design=port/"design/codex-paper-ui"
manifest=json.loads((design/"manifest.json").read_text(encoding="utf-8"))
assert {a["name"] for a in manifest["assets"]}=={"Paper","Botanical"}
total=0
for record in manifest["assets"]:
    source=design/record["source"]
    runtime=port/"src/SCIEPort"/record["runtime"]
    for path,key in ((source,"sourceSha256"),(runtime,"runtimeSha256")):
        assert hashlib.sha256(path.read_bytes()).hexdigest()==record[key],path
    with Image.open(runtime) as image:
        assert image.mode=="RGBA" and image.size==(256,256),runtime
        raw=image.tobytes()
        assert all(raw[i]<=raw[i+3] and raw[i+1]<=raw[i+3] and raw[i+2]<=raw[i+3] for i in range(0,len(raw),4)),"premultiplied native blend encoding"
        extrema=image.getchannel("A").getextrema()
        assert extrema==((255,255) if record["name"]=="Paper" else (0,255)),extrema
    total+=runtime.stat().st_size
assert total<192*1024,"paper UI PNG budget"
print(f"Codex paper UI source/runtime SHA256, PNG/alpha/native blend/budget passed: {total} bytes, 0.5 MiB GPU")
if args.base:
    root=port/"src/SCIEPort/Features/CelestialCodex"
    unchanged=[p for folder in ("Core","Mod","Networking","Android") for p in (root/folder).glob("*") if p.is_file() and p.name!="ReaderLayout.cs"]
    unchanged += [port/"src/SCIEPort"/p for p in ("Assets/Blocks/CelestialCodex.csv","Assets/Database/CelestialCodex.xdb","Assets/Textures/CelestialCodex/Book.png")]
    for path in unchanged:
        name=path.relative_to(repo).as_posix()
        old=subprocess.check_output(["git","show",f"{args.base}:{name}"],cwd=repo)
        current=path.read_bytes()
        if path.suffix in (".cs",".csv",".xdb"):
            old=old.replace(b"\r\n",b"\n");current=current.replace(b"\r\n",b"\n")
        assert old==current,f"UI-only boundary changed: {name}"
    print(f"UI-only boundary: {len(unchanged)} reader/network/import/cache/save/identity/source resource files unchanged from {args.base}")
