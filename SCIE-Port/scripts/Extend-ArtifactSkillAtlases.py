#!/usr/bin/env python3
"""Extend the Gungnir and Mjolnir skill atlases with the third active skill.

Both atlases are rebuilt from their existing pixels, so previously shipped cells stay
byte-identical:

* Gungnir: 512x256 (2 cells) -> 768x256 (3 cells). Cell 2 is drawn here with the same
  engraved-rim vocabulary the existing Gungnir badges use.
* Awakened Stormhammer: 2048x512 (4 cells, three used) -> cell 3 is composited from the
  ImageGen source in docs/images, cropped to the medallion circle so the badge reads at
  78 logical units on a phone.

Run from the SCIE-Port directory with a Pillow-capable interpreter.
"""

from __future__ import annotations

import hashlib
import json
import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
GUNGNIR = ROOT / "src/SCIEPort/Assets/Textures/GungnirSkills.png"
MJOLNIR = ROOT / "src/SCIEPort/Assets/Textures/StormhammerAwakenedSkills.png"
MJOLNIR_SOURCE = ROOT / "docs/images/Ornate_circular_game_skill_ico_2026-10-30T04-10-27.png"
MANIFEST = ROOT / "docs/assets/artifact-skill-icons.json"
ICON_PROMPT = (
    "Ornate circular game skill icon, mobile RPG artifact ability badge. Dark violet-black storm "
    "backdrop inside a thick engraved dark-iron medallion rim with aged gold bevel and rivets. "
    "Center: a stylised storm crown sigil of three sharp lightning prongs rising from a thin "
    "circular domain ring on the ground, bright cyan-white lightning core with branching forked "
    "arcs and a deep indigo halo. Polished metal, precise symmetrical composition, subtle crackle "
    "texture, high contrast, no text, no letters, no numbers, centered, square, clean edges, game UI "
    "icon quality."
)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def gungnir_badge(size: int = 256) -> Image.Image:
    """Platinum edict badge: engraved rim, mandate scepter and radiating fate strokes."""
    scale = 4
    s = size * scale
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cx = cy = s // 2
    r_out = int(s * 0.43)
    r_in = int(s * 0.355)

    for radius in range(r_out, r_in, -2):
        t = (r_out - radius) / max(1, r_out - r_in)
        shade = int(78 + 92 * (1 - abs(t - 0.45) * 1.6))
        d.ellipse((cx - radius, cy - radius, cx + radius, cy + radius),
                  outline=(shade + 60, shade + 46, shade + 16, 245), width=2)

    d.ellipse((cx - r_in, cy - r_in, cx + r_in, cy + r_in), fill=(22, 25, 32, 235),
              outline=(242, 233, 198, 255), width=max(2, s // 170))
    d.arc((cx - r_out, cy - r_out, cx + r_out, cy + r_out), 200, 340, fill=(252, 246, 222, 235), width=max(2, s // 190))
    d.arc((cx - r_out, cy - r_out, cx + r_out, cy + r_out), 20, 160, fill=(92, 74, 44, 235), width=max(2, s // 190))

    # Sixteen engraved ticks around the rim.
    for i in range(16):
        a = i * math.tau / 16
        x0, y0 = cx + r_in * math.cos(a), cy + r_in * math.sin(a)
        x1, y1 = cx + (r_in + s * 0.035) * math.cos(a), cy + (r_in + s * 0.035) * math.sin(a)
        d.line([(x0, y0), (x1, y1)], fill=(235, 222, 176, 210), width=max(1, s // 300))

    # Mandate strokes: sixteen platinum rays behind the scepter.
    for i in range(16):
        a = i * math.tau / 16 + math.tau / 32
        r0, r1 = s * 0.16, s * 0.315
        d.line([(cx + r0 * math.cos(a), cy + r0 * math.sin(a)),
                (cx + r1 * math.cos(a), cy + r1 * math.sin(a))],
               fill=(250, 245, 222, 120), width=max(1, s // 260))

    # Scepter shaft with a platinum head and a gold collar.
    shaft_w = max(3, s // 62)
    d.line([(cx, cy + s * 0.20), (cx, cy - s * 0.17)], fill=(255, 252, 238, 255), width=shaft_w)
    d.line([(cx + shaft_w // 2, cy + s * 0.18), (cx + shaft_w // 2, cy - s * 0.15)],
           fill=(206, 176, 108, 240), width=max(1, s // 300))
    head = [(cx, cy - s * 0.255), (cx + s * 0.075, cy - s * 0.135), (cx, cy - s * 0.055),
            (cx - s * 0.075, cy - s * 0.135)]
    d.polygon(head, fill=(250, 246, 228, 255), outline=(224, 193, 122, 255))
    d.polygon([(cx, cy - s * 0.105), (cx + s * 0.045, cy - s * 0.015), (cx, cy + s * 0.07),
               (cx - s * 0.045, cy - s * 0.015)], fill=(30, 34, 42, 255), outline=(238, 208, 140, 255))
    d.arc((cx - s * 0.10, cy + s * 0.10, cx + s * 0.10, cy + s * 0.235), 200, 340,
          fill=(246, 238, 206, 235), width=max(2, s // 220))

    img = img.resize((size, size), Image.LANCZOS)
    return img


def extend_gungnir() -> None:
    old = Image.open(GUNGNIR).convert("RGBA")
    if old.size not in ((512, 256), (768, 256)):
        raise SystemExit(f"unexpected Gungnir atlas size {old.size}")
    atlas = Image.new("RGBA", (768, 256), (0, 0, 0, 0))
    atlas.paste(old.crop((0, 0, 512, 256)), (0, 0))
    atlas.paste(gungnir_badge(256), (512, 0))
    atlas.save(GUNGNIR, optimize=True)


def extend_mjolnir() -> None:
    atlas = Image.open(MJOLNIR).convert("RGBA")
    if atlas.size != (2048, 512):
        raise SystemExit(f"unexpected Mjolnir atlas size {atlas.size}")
    if not MJOLNIR_SOURCE.exists():
        raise SystemExit(f"missing ImageGen source: {MJOLNIR_SOURCE}")
    src = Image.open(MJOLNIR_SOURCE).convert("RGBA")
    side = min(src.size)
    src = src.crop(((src.width - side) // 2, (src.height - side) // 2,
                    (src.width + side) // 2, (src.height + side) // 2))
    # Circular badge mask: removes the outer backdrop and the generation watermark.
    mask = Image.new("L", (side, side), 0)
    ImageDraw.Draw(mask).ellipse((side * 0.015, side * 0.015, side * 0.985, side * 0.985), fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(side * 0.004))
    badge = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    badge.paste(src, (0, 0), mask)
    badge = badge.resize((512, 512), Image.LANCZOS)
    atlas.paste(badge, (1536, 0))
    atlas.save(MJOLNIR, optimize=True)


def main() -> None:
    extend_gungnir()
    extend_mjolnir()

    manifest = {
        "note": "Third active skill icons for 0.56.0. Gungnir cell 2 is drawn by "
                "scripts/Extend-ArtifactSkillAtlases.py; Mjolnir cell 3 is composited from an "
                "ImageGen source cropped to a circular badge.",
        "stormhammerDominion": {
            "prompt": ICON_PROMPT,
            "source": "docs/images/Ornate_circular_game_skill_ico_2026-10-30T04-10-27.png",
            "sourceSha256": sha256(MJOLNIR_SOURCE),
            "atlas": "src/SCIEPort/Assets/Textures/StormhammerAwakenedSkills.png",
            "atlasSha256": sha256(MJOLNIR),
            "cell": 3,
        },
        "gungnirFatalism": {
            "generator": "scripts/Extend-ArtifactSkillAtlases.py",
            "atlas": "src/SCIEPort/Assets/Textures/GungnirSkills.png",
            "atlasSha256": sha256(GUNGNIR),
            "cell": 2,
        },
    }
    MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("atlases:", Image.open(GUNGNIR).size, Image.open(MJOLNIR).size)


if __name__ == "__main__":
    main()
