#!/usr/bin/env python3
"""Generate the original Stormhammer COLLADA, UV atlases and audio assets.

The geometry is intentionally authored as deterministic low-poly game art.  It
does not depend on Blender, game binaries, or any third-party source asset.
"""

from __future__ import annotations

import math
import random
import struct
import wave
from dataclasses import dataclass, field
from pathlib import Path
from xml.sax.saxutils import escape

from PIL import Image, ImageDraw, ImageFilter
import numpy as np


ROOT = Path(__file__).resolve().parents[1]
MODEL_PATH = ROOT / "src/SCIEPort/Assets/Models/Stormhammer.dae"
TEXTURE_PATH = ROOT / "src/SCIEPort/Assets/Textures/Stormhammer.png"
GLOW_PATH = ROOT / "src/SCIEPort/Assets/Textures/StormhammerGlow.png"
EFFECTS_PATH = ROOT / "src/SCIEPort/Assets/Textures/StormhammerEffects.png"
AUDIO_DIR = ROOT / "src/SCIEPort/Assets/Audio/Stormhammer"
PREVIEW_DIR = ROOT / "docs/images"

Vec3 = tuple[float, float, float]
Vec2 = tuple[float, float]

ATLAS_REGIONS = {
    "iron": (0, 0, 256, 512),
    "silver": (256, 0, 512, 512),
    "gold": (512, 0, 768, 512),
    "rune": (768, 0, 1024, 512),
    "grip": (0, 512, 256, 1024),
    "connector": (256, 512, 512, 1024),
    "crystal": (512, 512, 768, 1024),
    "engraving": (768, 512, 1024, 1024),
}


def sub(a: Vec3, b: Vec3) -> Vec3:
    return a[0] - b[0], a[1] - b[1], a[2] - b[2]


def cross(a: Vec3, b: Vec3) -> Vec3:
    return a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]


def dot(a: Vec3, b: Vec3) -> float:
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def normalize(v: Vec3) -> Vec3:
    length = math.sqrt(max(dot(v, v), 1e-12))
    return v[0] / length, v[1] / length, v[2] / length


def center(points: list[Vec3]) -> Vec3:
    n = float(len(points))
    return sum(p[0] for p in points) / n, sum(p[1] for p in points) / n, sum(p[2] for p in points) / n


@dataclass
class Mesh:
    name: str
    material: str
    vertices: list[Vec3] = field(default_factory=list)
    normals: list[Vec3] = field(default_factory=list)
    uvs: list[Vec2] = field(default_factory=list)
    face_index: int = 0

    def _uv_quad(self) -> tuple[Vec2, Vec2, Vec2, Vec2]:
        x0, y0, x1, y1 = ATLAS_REGIONS[self.material]
        col = self.face_index % 4
        row = (self.face_index // 4) % 8
        cell_w = (x1 - x0) / 4.0
        cell_h = (y1 - y0) / 8.0
        pad = 4.0
        left = (x0 + col * cell_w + pad) / 1024.0
        right = (x0 + (col + 1) * cell_w - pad) / 1024.0
        top = 1.0 - (y0 + row * cell_h + pad) / 1024.0
        bottom = 1.0 - (y0 + (row + 1) * cell_h - pad) / 1024.0
        self.face_index += 1
        return (left, bottom), (right, bottom), (right, top), (left, top)

    def triangle(self, a: Vec3, b: Vec3, c: Vec3, uvs: tuple[Vec2, Vec2, Vec2] | None = None) -> None:
        normal = normalize(cross(sub(b, a), sub(c, a)))
        if uvs is None:
            q = self._uv_quad()
            uvs = q[0], q[1], q[2]
        self.vertices.extend((a, b, c))
        self.normals.extend((normal, normal, normal))
        self.uvs.extend(uvs)

    def polygon(self, points: list[Vec3], outward_from: Vec3) -> None:
        if len(points) < 3:
            return
        n = cross(sub(points[1], points[0]), sub(points[2], points[0]))
        if dot(n, sub(center(points), outward_from)) < 0:
            points = list(reversed(points))
        q = self._uv_quad()
        if len(points) == 3:
            self.triangle(points[0], points[1], points[2], (q[0], q[1], q[2]))
        elif len(points) == 4:
            self.triangle(points[0], points[1], points[2], (q[0], q[1], q[2]))
            self.triangle(points[0], points[2], points[3], (q[0], q[2], q[3]))
        else:
            for i in range(1, len(points) - 1):
                self.triangle(points[0], points[i], points[i + 1])

    @property
    def triangle_count(self) -> int:
        return len(self.vertices) // 3


def bevel_box(name: str, c: Vec3, half: Vec3, bevel: float, material: str) -> Mesh:
    mesh = Mesh(name, material)
    h = half
    b = min(bevel, min(half) * 0.45)
    axes = (0, 1, 2)
    for axis in axes:
        other = [a for a in axes if a != axis]
        for sign in (-1, 1):
            pts = []
            for s0, s1 in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                p = [c[0], c[1], c[2]]
                p[axis] += sign * h[axis]
                p[other[0]] += s0 * (h[other[0]] - b)
                p[other[1]] += s1 * (h[other[1]] - b)
                pts.append(tuple(p))
            mesh.polygon(pts, c)
    for a, b_axis in ((0, 1), (0, 2), (1, 2)):
        k = next(axis for axis in axes if axis not in (a, b_axis))
        for sa in (-1, 1):
            for sb in (-1, 1):
                pts = []
                for sk, face in ((-1, a), (1, a), (1, b_axis), (-1, b_axis)):
                    p = [c[0], c[1], c[2]]
                    p[k] += sk * (h[k] - b)
                    if face == a:
                        p[a] += sa * h[a]
                        p[b_axis] += sb * (h[b_axis] - b)
                    else:
                        p[a] += sa * (h[a] - b)
                        p[b_axis] += sb * h[b_axis]
                    pts.append(tuple(p))
                mesh.polygon(pts, c)
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1):
                pts = [
                    (c[0] + sx * h[0], c[1] + sy * (h[1] - b), c[2] + sz * (h[2] - b)),
                    (c[0] + sx * (h[0] - b), c[1] + sy * h[1], c[2] + sz * (h[2] - b)),
                    (c[0] + sx * (h[0] - b), c[1] + sy * (h[1] - b), c[2] + sz * h[2]),
                ]
                mesh.polygon(pts, c)
    return mesh


def cylinder_y(name: str, c: Vec3, radius: float, height: float, segments: int, material: str) -> Mesh:
    mesh = Mesh(name, material)
    y0, y1 = c[1] - height / 2, c[1] + height / 2
    for i in range(segments):
        a0 = 2 * math.pi * i / segments
        a1 = 2 * math.pi * (i + 1) / segments
        p0 = (c[0] + radius * math.cos(a0), y0, c[2] + radius * math.sin(a0))
        p1 = (c[0] + radius * math.cos(a1), y0, c[2] + radius * math.sin(a1))
        p2 = (p1[0], y1, p1[2])
        p3 = (p0[0], y1, p0[2])
        mesh.polygon([p0, p1, p2, p3], c)
        mesh.polygon([(c[0], y0, c[2]), p1, p0], c)
        mesh.polygon([(c[0], y1, c[2]), p3, p2], c)
    return mesh


def hex_crystal(name: str, c: Vec3, radius: float, depth: float, axis: int, material: str) -> Mesh:
    mesh = Mesh(name, material)
    ring_axes = [a for a in (0, 1, 2) if a != axis]
    ring: list[Vec3] = []
    for i in range(6):
        angle = math.pi / 6 + 2 * math.pi * i / 6
        p = [c[0], c[1], c[2]]
        p[ring_axes[0]] += radius * math.cos(angle)
        p[ring_axes[1]] += radius * math.sin(angle)
        ring.append(tuple(p))
    front = list(c); front[axis] += depth
    back = list(c); back[axis] -= depth * 0.3
    for i in range(6):
        j = (i + 1) % 6
        mesh.polygon([tuple(front), ring[i], ring[j]], c)
        mesh.polygon([tuple(back), ring[j], ring[i]], c)
    return mesh


def helix_wrap(name: str, y0: float, y1: float, radius: float, turns: float, material: str) -> Mesh:
    mesh = Mesh(name, material)
    segments = 112
    band = 0.075
    for i in range(segments):
        t0, t1 = i / segments, (i + 1) / segments
        pts = []
        for t, dy in ((t0, -band), (t1, -band), (t1, band), (t0, band)):
            a = 2 * math.pi * turns * t
            pts.append((radius * math.cos(a), y0 + (y1 - y0) * t + dy, radius * math.sin(a)))
        mesh.polygon(pts, (0.0, (y0 + y1) / 2, 0.0))
    return mesh


def lightning_ribbon(name: str, z: float, mirror: bool = False) -> Mesh:
    mesh = Mesh(name, "rune")
    points = [(-0.78, 2.90), (-0.28, 2.68), (-0.48, 2.49), (0.18, 2.28), (-0.02, 2.05), (0.72, 1.82)]
    width = 0.055
    for i in range(len(points) - 1):
        x0, y0 = points[i]
        x1, y1 = points[i + 1]
        dx, dy = x1 - x0, y1 - y0
        ln = math.sqrt(dx * dx + dy * dy)
        nx, ny = -dy / ln * width, dx / ln * width
        zz = -z if mirror else z
        pts = [(x0 + nx, y0 + ny, zz), (x0 - nx, y0 - ny, zz), (x1 - nx, y1 - ny, zz), (x1 + nx, y1 + ny, zz)]
        if mirror:
            pts.reverse()
        mesh.polygon(pts, (0.0, 2.35, 0.0))
    return mesh


def make_model() -> tuple[list[Mesh], int]:
    meshes: list[Mesh] = []
    meshes.append(bevel_box("Head_Core", (0, 2.40, 0), (1.22, 0.66, 0.56), 0.16, "iron"))
    meshes.append(bevel_box("Head_SilverSpine", (0, 2.40, 0), (0.26, 0.76, 0.63), 0.09, "silver"))
    for sx in (-1, 1):
        meshes.append(bevel_box(f"StrikeFrame_{sx}", (sx * 1.25, 2.40, 0), (0.17, 0.58, 0.50), 0.08, "silver"))
        meshes.append(bevel_box(f"StrikeFace_{sx}", (sx * 1.39, 2.40, 0), (0.06, 0.46, 0.39), 0.035, "engraving"))
        for sy in (-1, 1):
            for sz in (-1, 1):
                meshes.append(bevel_box(f"GoldGuard_{sx}_{sy}_{sz}", (sx * 1.05, 2.40 + sy * 0.53, sz * 0.47), (0.27, 0.105, 0.105), 0.035, "gold"))
    for sz in (-1, 1):
        meshes.append(bevel_box(f"RunePanel_{sz}", (0, 2.40, sz * 0.59), (0.78, 0.45, 0.055), 0.08, "engraving"))
        meshes.append(hex_crystal(f"CoreCrystal_{sz}", (0, 2.40, sz * 0.655), 0.29, 0.13, 2, "crystal"))
    meshes.append(lightning_ribbon("Rune_Front", 0.657, False))
    meshes.append(lightning_ribbon("Rune_Back", 0.657, True))
    meshes.append(cylinder_y("Connector", (0, 1.58, 0), 0.31, 0.70, 12, "connector"))
    meshes.append(cylinder_y("UpperRing", (0, 1.35, 0), 0.29, 0.16, 12, "gold"))
    meshes.append(cylinder_y("GripCore", (0, -0.05, 0), 0.205, 2.75, 12, "grip"))
    meshes.append(helix_wrap("GripSpiral", -1.38, 1.25, 0.222, 7.25, "grip"))
    for index, y in enumerate((0.68, 0.02, -0.64, -1.30)):
        meshes.append(cylinder_y(f"GripRing_{index}", (0, y, 0), 0.245, 0.10, 12, "connector"))
    meshes.append(cylinder_y("PommelHousing", (0, -1.57, 0), 0.31, 0.42, 12, "gold"))
    meshes.append(hex_crystal("PommelCrystal", (0, -1.89, 0), 0.26, 0.28, 1, "crystal"))
    triangle_count = sum(m.triangle_count for m in meshes)
    return meshes, triangle_count


def fmt(values) -> str:
    return " ".join(f"{v:.6g}" for v in values)


def write_model(meshes: list[Mesh]) -> None:
    geometries = []
    instances = []
    for mesh in meshes:
        name = escape(mesh.name)
        pos = [v for p in mesh.vertices for v in p]
        normals = [v for p in mesh.normals for v in p]
        uvs = [v for p in mesh.uvs for v in p]
        count = len(mesh.vertices)
        indices = " ".join(f"{i} {i} {i}" for i in range(count))
        geometries.append(f'''    <geometry id="{name}-mesh" name="{name}"><mesh>
      <source id="{name}-positions"><float_array id="{name}-positions-array" count="{len(pos)}">{fmt(pos)}</float_array><technique_common><accessor source="#{name}-positions-array" count="{count}" stride="3"><param name="X" type="float"/><param name="Y" type="float"/><param name="Z" type="float"/></accessor></technique_common></source>
      <source id="{name}-normals"><float_array id="{name}-normals-array" count="{len(normals)}">{fmt(normals)}</float_array><technique_common><accessor source="#{name}-normals-array" count="{count}" stride="3"><param name="X" type="float"/><param name="Y" type="float"/><param name="Z" type="float"/></accessor></technique_common></source>
      <source id="{name}-uv"><float_array id="{name}-uv-array" count="{len(uvs)}">{fmt(uvs)}</float_array><technique_common><accessor source="#{name}-uv-array" count="{count}" stride="2"><param name="S" type="float"/><param name="T" type="float"/></accessor></technique_common></source>
      <vertices id="{name}-vertices"><input semantic="POSITION" source="#{name}-positions"/></vertices>
      <triangles count="{mesh.triangle_count}"><input semantic="VERTEX" source="#{name}-vertices" offset="0"/><input semantic="NORMAL" source="#{name}-normals" offset="1"/><input semantic="TEXCOORD" source="#{name}-uv" offset="2" set="0"/><p>{indices}</p></triangles>
    </mesh></geometry>''')
        instances.append(f'        <instance_geometry url="#{name}-mesh" name="{name}"/>')
    dae = f'''<?xml version="1.0" encoding="utf-8"?>
<!-- Original SCIE-Port Stormhammer. Deterministic Y_UP low-poly asset. -->
<COLLADA xmlns="http://www.collada.org/2005/11/COLLADASchema" version="1.4.1">
  <asset><contributor><author>SCIE-Port contributors</author><authoring_tool>New-StormhammerAssets.py</authoring_tool></contributor><unit name="meter" meter="1"/><up_axis>Y_UP</up_axis></asset>
  <library_geometries>
{chr(10).join(geometries)}
  </library_geometries>
  <library_visual_scenes><visual_scene id="Scene" name="Scene">
    <node id="Stormhammer" name="Stormhammer" type="NODE"><matrix sid="transform">1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1</matrix>
{chr(10).join(instances)}
      <extra><technique profile="SCIEPORT">
        <attachment name="Grip" x="0" y="-0.10" z="0"/>
        <attachment name="StrikeLeft" x="-1.46" y="2.40" z="0"/>
        <attachment name="StrikeRight" x="1.46" y="2.40" z="0"/>
        <attachment name="ArcFront" x="0" y="2.40" z="0.82"/>
        <attachment name="ArcBack" x="0" y="2.40" z="-0.82"/>
        <attachment name="Pommel" x="0" y="-2.17" z="0"/>
      </technique></extra>
    </node>
  </visual_scene></library_visual_scenes>
  <scene><instance_visual_scene url="#Scene"/></scene>
</COLLADA>
'''
    MODEL_PATH.parent.mkdir(parents=True, exist_ok=True)
    MODEL_PATH.write_text(dae, encoding="utf-8", newline="\n")


def paint_material(draw: ImageDraw.ImageDraw, box, base, accent, seed: int) -> None:
    rng = random.Random(seed)
    x0, y0, x1, y1 = box
    draw.rectangle(box, fill=base)
    for y in range(y0, y1):
        shade = int(15 * math.sin((y - y0) / 17.0) + 12 * (y - y0) / max(1, y1 - y0))
        color = tuple(max(0, min(255, c + shade)) for c in base)
        draw.line((x0, y, x1, y), fill=color)
    for _ in range(260):
        x = rng.randrange(x0 + 4, x1 - 4)
        y = rng.randrange(y0 + 4, y1 - 4)
        length = rng.randrange(3, 28)
        alpha = rng.randrange(30, 100)
        mixed = tuple((base[i] * (255 - alpha) + accent[i] * alpha) // 255 for i in range(3))
        draw.line((x, y, min(x1 - 3, x + length), y + rng.choice((-1, 0, 1))), fill=mixed, width=1)
    for gx in range(x0, x1, 64):
        for gy in range(y0, y1, 64):
            draw.rectangle((gx + 2, gy + 2, min(gx + 61, x1 - 2), min(gy + 61, y1 - 2)), outline=accent, width=2)
            draw.line((gx + 6, gy + 7, min(gx + 56, x1 - 5), gy + 7), fill=tuple(min(255, c + 35) for c in accent), width=1)


def write_textures() -> None:
    image = Image.new("RGBA", (1024, 1024), (0, 0, 0, 255))
    draw = ImageDraw.Draw(image)
    palettes = {
        "iron": ((43, 49, 56), (82, 91, 100)),
        "silver": ((122, 132, 143), (205, 220, 230)),
        "gold": ((112, 76, 29), (202, 151, 61)),
        "rune": ((10, 63, 86), (45, 225, 255)),
        "grip": ((16, 17, 21), (58, 62, 70)),
        "connector": ((67, 73, 82), (158, 170, 183)),
        "crystal": ((8, 58, 94), (79, 226, 255)),
        "engraving": ((28, 32, 38), (106, 116, 128)),
    }
    for i, (name, box) in enumerate(ATLAS_REGIONS.items()):
        paint_material(draw, box, palettes[name][0], palettes[name][1], 1049 + i * 97)
    # Strengthen authored leather diagonals and crystal facets.
    for y in range(512, 1024, 18):
        draw.line((0, y, 256, y - 64), fill=(72, 66, 62, 255), width=5)
        draw.line((0, y + 4, 256, y - 60), fill=(8, 9, 12, 255), width=2)
    for x in range(512, 768, 64):
        draw.polygon(((x + 4, 516), (min(x + 60, 766), 516), (x + 34, 1020)), fill=(12, 94, 139, 255))
        draw.line((x + 34, 520, x + 34, 1018), fill=(109, 241, 255, 255), width=3)
    image.save(TEXTURE_PATH, optimize=True)

    glow = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
    glow_draw = ImageDraw.Draw(glow)
    glow_draw.rectangle(ATLAS_REGIONS["rune"], fill=(38, 205, 255, 255))
    glow_draw.rectangle(ATLAS_REGIONS["crystal"], fill=(72, 226, 255, 255))
    glow = glow.filter(ImageFilter.GaussianBlur(1.4))
    glow.save(GLOW_PATH, optimize=True)

    effects = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    ed = ImageDraw.Draw(effects)
    rng = random.Random(8721)
    for slot_y in range(4):
        for slot_x in range(4):
            cx, cy = slot_x * 64 + 32, slot_y * 64 + 32
            radius = 4 + 2 * (slot_x + slot_y)
            for r in range(radius * 3, 1, -2):
                alpha = max(12, 180 - r * 7)
                ed.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(22, 174, 255, alpha))
            ed.ellipse((cx - 3, cy - 3, cx + 3, cy + 3), fill=(220, 252, 255, 255))
            for _ in range(5):
                angle = rng.random() * math.tau
                length = rng.randrange(9, 25)
                ed.line((cx, cy, cx + math.cos(angle) * length, cy + math.sin(angle) * length), fill=(116, 234, 255, 210), width=1)
    effects.save(EFFECTS_PATH, optimize=True)


def write_wav(path: Path, duration: float, generator) -> None:
    sample_rate = 44100
    frames = bytearray()
    rng = random.Random(path.name)
    for i in range(int(duration * sample_rate)):
        t = i / sample_rate
        value = max(-1.0, min(1.0, generator(t, duration, rng)))
        frames.extend(struct.pack("<h", int(value * 32767)))
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(sample_rate)
        output.writeframes(frames)


def write_audio() -> None:
    AUDIO_DIR.mkdir(parents=True, exist_ok=True)

    def swing(t, d, rng):
        env = math.sin(math.pi * t / d) ** 1.7
        return env * (0.35 * math.sin(2 * math.pi * (150 + 620 * t / d) * t) + 0.16 * (rng.random() * 2 - 1))

    def charge(t, d, rng):
        env = min(1.0, t * 5) * min(1.0, (d - t) * 6)
        chirp = math.sin(2 * math.pi * (70 + 260 * (t / d) ** 2) * t)
        crackle = (rng.random() * 2 - 1) if rng.random() < 0.035 else 0
        return env * (0.22 * chirp + 0.30 * crackle)

    def thunder(t, d, rng):
        decay = math.exp(-3.4 * t)
        rumble = 0.34 * math.sin(2 * math.pi * 43 * t) + 0.22 * math.sin(2 * math.pi * 61 * t)
        crack = (rng.random() * 2 - 1) * math.exp(-26 * t)
        return decay * rumble + 0.65 * crack

    write_wav(AUDIO_DIR / "Swing.wav", 0.48, swing)
    write_wav(AUDIO_DIR / "Charge.wav", 1.35, charge)
    write_wav(AUDIO_DIR / "Thunder.wav", 2.15, thunder)


def render_preview(meshes: list[Mesh], yaw: float, width: int = 560, height: int = 660) -> Image.Image:
    atlas = Image.open(TEXTURE_PATH).convert("RGB")
    atlas_pixels = np.asarray(atlas)
    yy = np.linspace(0.0, 1.0, height, dtype=np.float32)[:, None]
    background = np.zeros((height, width, 3), dtype=np.uint8)
    background[:, :, 0] = (20 + 13 * (1.0 - yy)).astype(np.uint8)
    background[:, :, 1] = (24 + 14 * (1.0 - yy)).astype(np.uint8)
    background[:, :, 2] = (30 + 16 * (1.0 - yy)).astype(np.uint8)
    zbuffer = np.full((height, width), -1e9, dtype=np.float32)
    glow = np.zeros((height, width, 4), dtype=np.uint8)
    scale = min(width / 3.55, height / 5.0)
    cos_y, sin_y = math.cos(yaw), math.sin(yaw)
    light = normalize((-0.35, 0.75, 0.95))

    def rotate(p: Vec3) -> Vec3:
        return cos_y * p[0] + sin_y * p[2], p[1], -sin_y * p[0] + cos_y * p[2]

    for mesh in meshes:
        for i in range(0, len(mesh.vertices), 3):
            world = [rotate(p) for p in mesh.vertices[i:i + 3]]
            screen = [(width * 0.5 + p[0] * scale, height * 0.54 - (p[1] - 0.35) * scale, p[2]) for p in world]
            min_x = max(0, int(math.floor(min(p[0] for p in screen))))
            max_x = min(width - 1, int(math.ceil(max(p[0] for p in screen))))
            min_y = max(0, int(math.floor(min(p[1] for p in screen))))
            max_y = min(height - 1, int(math.ceil(max(p[1] for p in screen))))
            if min_x > max_x or min_y > max_y:
                continue
            x0, y0 = screen[0][0], screen[0][1]
            x1, y1 = screen[1][0], screen[1][1]
            x2, y2 = screen[2][0], screen[2][1]
            denom = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2)
            if abs(denom) < 1e-6:
                continue
            xs, ys = np.meshgrid(np.arange(min_x, max_x + 1), np.arange(min_y, max_y + 1))
            w0 = ((y1 - y2) * (xs - x2) + (x2 - x1) * (ys - y2)) / denom
            w1 = ((y2 - y0) * (xs - x2) + (x0 - x2) * (ys - y2)) / denom
            w2 = 1.0 - w0 - w1
            inside = (w0 >= -0.001) & (w1 >= -0.001) & (w2 >= -0.001)
            if not np.any(inside):
                continue
            depth = w0 * screen[0][2] + w1 * screen[1][2] + w2 * screen[2][2]
            zslice = zbuffer[min_y:max_y + 1, min_x:max_x + 1]
            visible = inside & (depth > zslice)
            if not np.any(visible):
                continue
            uvs = mesh.uvs[i:i + 3]
            u = float(sum(p[0] for p in uvs) / 3.0)
            v = float(sum(p[1] for p in uvs) / 3.0)
            texel = atlas_pixels[max(0, min(1023, int((1.0 - v) * 1023))), max(0, min(1023, int(u * 1023)))].astype(np.float32)
            normal = normalize(cross(sub(world[1], world[0]), sub(world[2], world[0])))
            shade = 0.38 + 0.62 * max(0.0, dot(normal, light))
            if mesh.material in ("rune", "crystal"):
                shade = max(shade, 1.08)
            color = np.clip(texel * shade, 0, 255).astype(np.uint8)
            target = background[min_y:max_y + 1, min_x:max_x + 1]
            target[visible] = color
            zslice[visible] = depth[visible]
            if mesh.material in ("rune", "crystal"):
                gs = glow[min_y:max_y + 1, min_x:max_x + 1]
                gs[visible] = np.array([32, 190, 255, 215], dtype=np.uint8)
    base = Image.fromarray(background, "RGB").convert("RGBA")
    bloom = Image.fromarray(glow, "RGBA").filter(ImageFilter.GaussianBlur(7.0))
    return Image.alpha_composite(base, bloom).convert("RGB")


def write_previews(meshes: list[Mesh]) -> None:
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    front = render_preview(meshes, 0.0)
    three_quarter = render_preview(meshes, math.radians(34.0))
    side = render_preview(meshes, math.radians(90.0))
    sheet = Image.new("RGB", (front.width * 3 + 32, front.height + 16), (15, 18, 23))
    sheet.paste(front, (8, 8))
    sheet.paste(three_quarter, (front.width + 16, 8))
    sheet.paste(side, (front.width * 2 + 24, 8))
    sheet.save(PREVIEW_DIR / "stormhammer-asset-preview.png", optimize=True)
    icon = render_preview(meshes, math.radians(28.0), 320, 320)
    icon.save(PREVIEW_DIR / "stormhammer-inventory-preview.png", optimize=True)


def main() -> None:
    meshes, triangles = make_model()
    if not 800 <= triangles <= 2500:
        raise SystemExit(f"Stormhammer triangle budget violation: {triangles}")
    write_model(meshes)
    write_textures()
    write_audio()
    write_previews(meshes)
    print(f"Stormhammer: {len(meshes)} meshes, {triangles} triangles")
    print(MODEL_PATH)
    print(TEXTURE_PATH)


if __name__ == "__main__":
    main()
