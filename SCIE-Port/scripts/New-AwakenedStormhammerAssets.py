#!/usr/bin/env python3
"""Build the clean-room Skywrath V2 awakened hammer, thunder spear and HUD atlases."""

from __future__ import annotations

import importlib.util
import math
import sys
from pathlib import Path
from xml.sax.saxutils import escape

from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
BASE_SCRIPT = ROOT / "scripts/New-StormhammerAssets.py"
spec = importlib.util.spec_from_file_location("stormhammer_base", BASE_SCRIPT)
base = importlib.util.module_from_spec(spec)
assert spec.loader is not None
sys.modules[spec.name] = base
spec.loader.exec_module(base)

MODEL = ROOT / "src/SCIEPort/Assets/Models/StormhammerAwakened.dae"
SPEAR_MODEL = ROOT / "src/SCIEPort/Assets/Models/ThunderSpear.dae"
TEXTURE = ROOT / "src/SCIEPort/Assets/Textures/StormhammerAwakened.png"
GLOW = ROOT / "src/SCIEPort/Assets/Textures/StormhammerAwakenedGlow.png"
EFFECTS = ROOT / "src/SCIEPort/Assets/Textures/StormhammerAwakenedEffects.png"
ICONS = ROOT / "src/SCIEPort/Assets/Textures/StormhammerAwakenedSkills.png"
ICON_SOURCE = ROOT / "docs/images/stormhammer-awakened-skill-icons-source.png"
PREVIEW = ROOT / "docs/images/stormhammer-awakened-preview.png"


def write_dae(path: Path, root_name: str, meshes, attachments: list[tuple[str, tuple[float, float, float]]]) -> None:
    geometries, instances = [], []
    for mesh in meshes:
        name = escape(mesh.name)
        pos = [v for p in mesh.vertices for v in p]
        normals = [v for p in mesh.normals for v in p]
        uvs = [v for p in mesh.uvs for v in p]
        count = len(mesh.vertices)
        indices = " ".join(f"{i} {i} {i}" for i in range(count))
        geometries.append(f'''    <geometry id="{name}-mesh" name="{name}"><mesh>
      <source id="{name}-positions"><float_array id="{name}-positions-array" count="{len(pos)}">{base.fmt(pos)}</float_array><technique_common><accessor source="#{name}-positions-array" count="{count}" stride="3"><param name="X" type="float"/><param name="Y" type="float"/><param name="Z" type="float"/></accessor></technique_common></source>
      <source id="{name}-normals"><float_array id="{name}-normals-array" count="{len(normals)}">{base.fmt(normals)}</float_array><technique_common><accessor source="#{name}-normals-array" count="{count}" stride="3"><param name="X" type="float"/><param name="Y" type="float"/><param name="Z" type="float"/></accessor></technique_common></source>
      <source id="{name}-uv"><float_array id="{name}-uv-array" count="{len(uvs)}">{base.fmt(uvs)}</float_array><technique_common><accessor source="#{name}-uv-array" count="{count}" stride="2"><param name="S" type="float"/><param name="T" type="float"/></accessor></technique_common></source>
      <vertices id="{name}-vertices"><input semantic="POSITION" source="#{name}-positions"/></vertices>
      <triangles count="{mesh.triangle_count}"><input semantic="VERTEX" source="#{name}-vertices" offset="0"/><input semantic="NORMAL" source="#{name}-normals" offset="1"/><input semantic="TEXCOORD" source="#{name}-uv" offset="2" set="0"/><p>{indices}</p></triangles>
    </mesh></geometry>''')
        instances.append(f'        <instance_geometry url="#{name}-mesh" name="{name}"/>')
    attachment_xml = "\n".join(
        f'        <attachment name="{escape(name)}" x="{p[0]}" y="{p[1]}" z="{p[2]}"/>'
        for name, p in attachments
    )
    dae = f'''<?xml version="1.0" encoding="utf-8"?>
<!-- Original SCIE-Port Skywrath V2 clean-room low-poly asset. -->
<COLLADA xmlns="http://www.collada.org/2005/11/COLLADASchema" version="1.4.1">
  <asset><contributor><author>SCIE-Port contributors</author><authoring_tool>New-AwakenedStormhammerAssets.py</authoring_tool></contributor><unit name="meter" meter="1"/><up_axis>Y_UP</up_axis></asset>
  <library_geometries>
{chr(10).join(geometries)}
  </library_geometries>
  <library_visual_scenes><visual_scene id="Scene" name="Scene"><node id="{root_name}" name="{root_name}" type="NODE"><matrix sid="transform">1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1</matrix>
{chr(10).join(instances)}
      <extra><technique profile="SCIEPORT">
{attachment_xml}
      </technique></extra></node></visual_scene></library_visual_scenes>
  <scene><instance_visual_scene url="#Scene"/></scene>
</COLLADA>
'''
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(dae, encoding="utf-8", newline="\n")


def awakened_model():
    meshes, _ = base.make_model()
    # A coherent awakened silhouette: crown rails, floating side blades and a
    # second crystal tier, rather than merely subdividing the original forms.
    meshes += [
        base.bevel_box("Awakened_Crown", (0, 3.13, 0), (0.70, 0.12, 0.22), 0.06, "gold"),
        base.hex_crystal("Awakened_CrownCrystal", (0, 3.34, 0), 0.24, 0.18, 1, "crystal"),
        base.bevel_box("Awakened_Keel", (0, 1.72, 0), (0.40, 0.16, 0.28), 0.07, "silver"),
    ]
    for sx in (-1, 1):
        meshes.append(base.bevel_box(f"Awakened_Wing_{sx}", (sx * 1.57, 2.42, 0), (0.32, 0.29, 0.16), 0.08, "silver"))
        meshes.append(base.hex_crystal(f"Awakened_WingCrystal_{sx}", (sx * 1.88, 2.42, 0), 0.19, 0.16, 0, "crystal"))
    for sz in (-1, 1):
        meshes.append(base.hex_crystal(f"Awakened_ArcCrystal_{sz}", (0, 2.94, sz * 0.48), 0.17, 0.13, 2, "crystal"))
    return meshes


def spear_model():
    meshes = [
        base.cylinder_y("Spear_Shaft", (0, 0, 0), 0.075, 3.2, 10, "silver"),
        base.hex_crystal("Spear_Core", (0, 0.28, 0), 0.19, 0.16, 1, "crystal"),
        base.hex_crystal("Spear_Tip", (0, 1.88, 0), 0.34, 0.48, 1, "crystal"),
        base.cylinder_y("Spear_Guard", (0, 1.45, 0), 0.25, 0.10, 8, "gold"),
        base.hex_crystal("Spear_Tail", (0, -1.75, 0), 0.17, 0.25, 1, "rune"),
    ]
    return meshes


def write_textures() -> None:
    source = Image.open(base.TEXTURE_PATH).convert("RGBA")
    draw = ImageDraw.Draw(source, "RGBA")
    # Violet-blue awakened filigree and bright bevel accents, while retaining
    # the proven explicit UV islands of the base weapon.
    for x in range(16, 1008, 64):
        draw.line((x, 12, x + 42, 500), fill=(83, 63, 188, 70), width=3)
    for y in range(532, 1000, 46):
        draw.arc((518, y - 24, 1012, y + 38), 188, 352, fill=(86, 224, 255, 120), width=3)
    source.save(TEXTURE, optimize=True)

    glow = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow, "RGBA")
    gd.rectangle(base.ATLAS_REGIONS["rune"], fill=(42, 212, 255, 245))
    gd.rectangle(base.ATLAS_REGIONS["crystal"], fill=(103, 102, 255, 255))
    for i in range(18):
        y = 22 + i * 53
        gd.line((770, y, 1018, y + 26), fill=(174, 245, 255, 210), width=4)
    glow.filter(ImageFilter.GaussianBlur(1.2)).save(GLOW, optimize=True)

    effects = Image.open(base.EFFECTS_PATH).convert("RGBA")
    ed = ImageDraw.Draw(effects, "RGBA")
    for r in range(58, 5, -5):
        ed.ellipse((128-r, 128-r//3, 128+r, 128+r//3), outline=(82, 126, 255, max(18, 160-r*2)), width=2)
    effects.save(EFFECTS, optimize=True)

    if not ICON_SOURCE.exists():
        raise SystemExit(f"Missing ImageGen icon source: {ICON_SOURCE}")
    raw = Image.open(ICON_SOURCE).convert("RGBA")
    atlas = Image.new("RGBA", (2048, 512), (0, 0, 0, 0))
    third = raw.width / 3.0
    for i in range(3):
        x0, x1 = int(i * third), int((i + 1) * third)
        crop = raw.crop((x0, 0, x1, raw.height))
        bbox = crop.getbbox()
        if bbox:
            crop = crop.crop(bbox)
        crop.thumbnail((500, 500), Image.Resampling.LANCZOS)
        atlas.alpha_composite(crop, (i * 512 + (512 - crop.width)//2, (512 - crop.height)//2))
    atlas.save(ICONS, optimize=True)


def main() -> None:
    hammer = awakened_model()
    spear = spear_model()
    write_dae(MODEL, "StormhammerAwakened", hammer, [
        ("Grip", (0, -0.10, 0)), ("LanceMuzzle", (0, 3.34, 0)),
        ("DomainOrigin", (0, -1.2, 0)), ("UltimateFocus", (0, 2.4, 0)),
    ])
    write_dae(SPEAR_MODEL, "ThunderSpear", spear, [("Tip", (0, 2.36, 0)), ("Tail", (0, -2.0, 0))])
    write_textures()
    preview = base.render_preview(hammer, math.radians(32), 640, 720)
    preview.save(PREVIEW, optimize=True)
    ht, st = sum(m.triangle_count for m in hammer), sum(m.triangle_count for m in spear)
    if not 1000 <= ht <= 2500 or not 100 <= st <= 800:
        raise SystemExit(f"triangle budget violation hammer={ht}, spear={st}")
    print(f"Skywrath V2: {len(hammer)} meshes/{ht} tris; spear {len(spear)} meshes/{st} tris")


if __name__ == "__main__":
    main()
