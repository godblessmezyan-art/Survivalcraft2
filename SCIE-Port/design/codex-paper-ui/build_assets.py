"""Pack original image_gen artwork for SCNET's premultiplied 2D blend pipeline."""
from pathlib import Path
import hashlib
import json
from PIL import Image

ROOT = Path(__file__).resolve().parent
DEST = ROOT.parents[1] / "src/SCIEPort/Assets/Textures/CelestialCodex"


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


records = []
for name in ("Paper", "Botanical"):
    source = ROOT / "source" / f"{name}.png"
    target = DEST / f"{name}.png"
    with Image.open(source) as original:
        image = original.convert("RGBA").resize((256, 256), Image.Resampling.LANCZOS)
        if name == "Botanical":
            raw=image.tobytes()
            image.putdata([(raw[i]*raw[i+3]//255,raw[i+1]*raw[i+3]//255,raw[i+2]*raw[i+3]//255,raw[i+3]) for i in range(0,len(raw),4)])
        image.save(target, optimize=True)
    records.append(dict(name=name, source=f"source/{name}.png", sourceSha256=digest(source),
                        runtime=f"Assets/Textures/CelestialCodex/{name}.png", runtimeSha256=digest(target),
                        size=[256, 256], bytes=target.stat().st_size))
(ROOT / "manifest.json").write_text(json.dumps(dict(
    version="0.90.0", origin="Original image_gen artwork, 2026-11-17; prompts.json",
    processing="RGBA 256px Lanczos conversion; Botanical RGB premultiplied, alpha retained. "
               "Target Texture2DReader loads unchanged; PrimitivesRenderer2D uses Blend.One/InverseSourceAlpha.",
    assets=records), indent=2), encoding="utf-8")
print("Codex artwork: two 256px RGBA textures, 0.5 MiB GPU, PNG bytes:", sum(r["bytes"] for r in records))
