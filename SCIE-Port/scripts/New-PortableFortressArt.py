#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Portable Fortress Mk.I "Hearthguard Edition" asset generator.

Single authoritative source for every Portable Fortress asset:

  src/SCIEPort/Assets/Models/PortableFortress.dae    fortress world model
  src/SCIEPort/Assets/Models/FortressCore.dae        deploy item / control core model
  src/SCIEPort/Assets/Textures/PortableFortress.png  1024x1024 UV atlas
  src/SCIEPort/Assets/Textures/FortressCore.png      item palette strip
  src/SCIEPort/Assets/Textures/FortressGlass.png     pane overlay (alpha)
  src/SCIEPort/Assets/Textures/FortressGlow.png      warm glow sprite (alpha)
  docs/portable-fortress/previews/*.png              generated previews

Design rules this script enforces:

* Envelope is exactly X[-5.5,5.5] Y[0,9] Z[-6.5,6.5]; the validator re-checks it.
  Nothing is ever rotated, so the baked bounds are exact and the collision proxies
  (which are axis aligned) stay in agreement with the visual.
* One root bone, eleven geometries. Static parts never become scene nodes.
* Every face is subdivided to roughly one atlas tile and mapped into the *named*
  region, so a part can never sample a different material than it asked for.
* UVs are inset by two texels: the model shader uses PointClamp, so a UV sitting on
  a region border would bleed into the neighbouring material.
* Every coordinate below mirrors a constant in
  src/SCIEPort/Features/PortableFortress/PortableFortressGeometry.cs.

Run:  python scripts/New-PortableFortressArt.py
"""

from __future__ import print_function

import math
import os

from PIL import Image, ImageDraw, ImageFont

# ---------------------------------------------------------------------------
# Frozen geometry contract (mirrors PortableFortressGeometry.cs)
# ---------------------------------------------------------------------------
HALF_WIDTH = 5.5
HALF_LENGTH = 6.5
ENVELOPE_HEIGHT = 9.0

FLOOR_TOP = 0.36
DECK_BOTTOM = 3.60
DECK_TOP = 3.84
ROOF_BOTTOM = 7.15
ROOF_TOP = 7.41
RAIL_TOP = 8.55

STAIR_STEPS = 8
STAIR_RISE = 0.435
STAIR_RUN = 0.50
STAIR_MIN_X = -4.30
STAIR_MAX_X = -2.20
STAIR_MIN_Z = -4.00
STAIR_END_Z = STAIR_MIN_Z + STAIR_STEPS * STAIR_RUN
STAIRWELL_Z = 0.25

DOOR_MIN_X = -1.20
DOOR_MAX_X = 1.20
DOOR_TOP = 2.75   # matches LowerRearDoorHeader in PortableFortressGeometry
DOOR_INNER_Z = 6.14
REAR_FACE_Z = 6.38

SHAFT_MIN_X = 2.325
SHAFT_MAX_X = 3.975
SHAFT_MIN_Z = 2.725
SHAFT_MAX_Z = 4.375

WINDOW_SILL_TOP = 4.705
WINDOW_HEAD_BOTTOM = 6.445

ATLAS = 1024
CELL = 256
UV_INSET = 2.0 / ATLAS

GROUPS = ("Hull", "HullTrim", "UpperShell", "Nose", "Roof", "RoofRails",
          "InteriorLower", "FurnitureLower", "Deck", "FurnitureUpper", "Details")

# Warm light anchors. FortressGlazingRenderer.GlowPoints must list the same points.
GLOW_POINTS = [
    (-4.00, 0.98, 3.05),   # hearth firebox
    (-4.90, 2.20, 2.20),   # west wall lamp
    (4.90, 2.20, 2.20),    # east wall lamp
    (0.00, 6.70, 0.80),    # level-2 hanging lantern
]

# ---------------------------------------------------------------------------
# Atlas layout: 4 x 4 cells of 256 px
# ---------------------------------------------------------------------------
REGIONS = {
    "armor":      (0, 0),
    "armor_dark": (1, 0),
    "silver":     (2, 0),
    "bronze":     (3, 0),
    "plaster":    (0, 1),
    "wood":       (1, 1),
    "wood_dark":  (2, 1),
    "leather":    (3, 1),
    "cloth":      (0, 2),
    "metal":      (1, 2),
    "stone":      (2, 2),
    "fire":       (3, 2),
    "roof":       (0, 3),
    "trim":       (1, 3),
    "canvas":     (2, 3),
    "glassframe": (3, 3),
}

PALETTE = {
    "armor":      (0x68, 0x7B, 0x88),
    "armor_dark": (0x35, 0x44, 0x4B),
    "silver":     (0xA6, 0xB1, 0xAF),
    "bronze":     (0xAE, 0x84, 0x56),
    "plaster":    (0xDD, 0xD2, 0xBC),
    "wood":       (0x99, 0x6F, 0x4D),
    "wood_dark":  (0x6E, 0x4E, 0x36),
    "leather":    (0x6E, 0x4A, 0x33),
    "cloth":      (0xA8, 0x5C, 0x42),
    "metal":      (0x8C, 0x94, 0x99),
    "stone":      (0x6F, 0x6B, 0x63),
    "fire":       (0xE9, 0xAA, 0x68),
    "roof":       (0x4C, 0x5C, 0x66),
    "trim":       (0x8F, 0x9B, 0x9C),
    "canvas":     (0xB8, 0xA8, 0x8A),
    "glassframe": (0x4E, 0x5C, 0x63),
}


def clamp8(value):
    if value < 0:
        return 0
    if value > 255:
        return 255
    return int(value)


def shift(color, amount):
    return (clamp8(color[0] + amount), clamp8(color[1] + amount), clamp8(color[2] + amount))


def mix(a, b, t):
    return (clamp8(a[0] + (b[0] - a[0]) * t),
            clamp8(a[1] + (b[1] - a[1]) * t),
            clamp8(a[2] + (b[2] - a[2]) * t))


def noise(x, y, seed):
    """Deterministic 0..1 hash noise; no RNG state, so output is reproducible."""
    h = (x * 374761393 + y * 668265263 + seed * 2246822519) & 0xFFFFFFFF
    h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((h ^ (h >> 16)) & 0xFFFF) / 65535.0


# ---------------------------------------------------------------------------
# Atlas painting
# ---------------------------------------------------------------------------
def paint_plate(img, region, base, style="panel", seed=7):
    px, py = REGIONS[region]
    ox, oy = px * CELL, py * CELL
    draw = ImageDraw.Draw(img)
    top = shift(base, 16)
    bottom = shift(base, -22)
    edge = shift(base, -46)

    for y in range(CELL):
        falloff = int(round(-10.0 * (y / float(CELL)) / 3.0)) * 3
        for x in range(CELL):
            # Coarse (2x2) noise quantised to 3-unit steps: still reads as material
            # grain, but keeps the PNG small enough to ship on mobile.
            n = noise(x // 2, y // 2, seed) - 0.5
            delta = int(round(n * 14.0 / 3.0)) * 3
            img.putpixel((ox + x, oy + y), shift(shift(base, delta), falloff))

    if style == "panel":
        draw.rectangle([ox, oy, ox + CELL - 1, oy + CELL - 1], outline=edge)
        draw.line([(ox + 2, oy + 2), (ox + CELL - 3, oy + 2)], fill=top)
        draw.line([(ox + 2, oy + 2), (ox + 2, oy + CELL - 3)], fill=top)
        draw.line([(ox + 2, oy + CELL - 3), (ox + CELL - 3, oy + CELL - 3)], fill=bottom)
        draw.line([(ox + CELL - 3, oy + 2), (ox + CELL - 3, oy + CELL - 3)], fill=bottom)
        for bx, by in ((14, 14), (CELL - 18, 14), (14, CELL - 18), (CELL - 18, CELL - 18)):
            draw.ellipse([ox + bx, oy + by, ox + bx + 5, oy + by + 5], fill=shift(top, 24), outline=edge)
    elif style == "hull":
        draw.rectangle([ox, oy, ox + CELL - 1, oy + CELL - 1], outline=shift(base, -70))
        draw.line([(ox + 4, oy + 4), (ox + CELL - 5, oy + 4)], fill=shift(base, 34))
        draw.line([(ox + 4, oy + 5), (ox + CELL - 5, oy + 5)], fill=shift(base, 18))
        draw.line([(ox + 3, oy + CELL // 2 - 1), (ox + CELL - 4, oy + CELL // 2 - 1)], fill=bottom)
        draw.line([(ox + 3, oy + CELL // 2), (ox + CELL - 4, oy + CELL // 2)], fill=edge)
        draw.line([(ox + 3, oy + CELL // 2 + 1), (ox + CELL - 4, oy + CELL // 2 + 1)], fill=top)
        draw.line([(ox + 3, oy + CELL - 4), (ox + CELL - 4, oy + CELL - 4)], fill=edge)
        for bx in (18, CELL // 2 - 3, CELL - 22):
            for by in (18, CELL - 22):
                draw.ellipse([ox + bx, oy + by, ox + bx + 5, oy + by + 5], fill=shift(top, 30), outline=edge)
    elif style == "plank":
        boards = 4
        step = CELL // boards
        # A heavy border turned the floor into a grid of tiles at grazing angles;
        # only the board joints should be visible.
        draw.rectangle([ox, oy, ox + CELL - 1, oy + CELL - 1], outline=shift(base, -26))
        for i in range(boards):
            y0 = oy + i * step
            draw.line([(ox + 1, y0 + 1), (ox + CELL - 2, y0 + 1)], fill=shift(base, 12))
            draw.line([(ox + 1, y0 + step - 2), (ox + CELL - 2, y0 + step - 2)], fill=shift(base, -16))
            joint = ox + ((i * 97) % (CELL - 40)) + 20
            draw.line([(joint, y0 + 2), (joint, y0 + step - 3)], fill=shift(base, -20))
        for i in range(70):
            gx = ox + int(noise(i, 3, seed + 11) * (CELL - 6)) + 3
            gy = oy + int(noise(i, 9, seed + 23) * (CELL - 6)) + 3
            draw.line([(gx, gy), (gx + 5, gy)], fill=shift(base, -18))
    elif style == "smooth":
        # Interior plaster: a faint reveal only. A strong per-cell border turns a
        # 11 m wall into a brick grid and reads as cheap up close.
        draw.rectangle([ox, oy, ox + CELL - 1, oy + CELL - 1], outline=shift(base, -14))
        draw.line([(ox + 2, oy + 2), (ox + CELL - 3, oy + 2)], fill=shift(base, 8))
    elif style == "ribbed":
        draw.rectangle([ox, oy, ox + CELL - 1, oy + CELL - 1], outline=edge)
        for i in range(1, 4):
            gx = ox + i * CELL // 4
            draw.line([(gx - 1, oy + 3), (gx - 1, oy + CELL - 4)], fill=shift(base, 14))
            draw.line([(gx, oy + 3), (gx, oy + CELL - 4)], fill=edge)
            draw.line([(gx + 1, oy + 3), (gx + 1, oy + CELL - 4)], fill=shift(base, -12))
    elif style == "cloth":
        for y in range(CELL):
            for x in range(CELL):
                n = noise(x // 2, y // 2, seed) - 0.5
                img.putpixel((ox + x, oy + y), shift(base, n * 20.0))
        for i in range(0, CELL, 16):
            draw.line([(ox, oy + i), (ox + CELL - 1, oy + i)], fill=shift(base, -12))
            draw.line([(ox + i, oy), (ox + i, oy + CELL - 1)], fill=shift(base, -8))
    elif style == "fire":
        for y in range(CELL):
            dy = (y - CELL * 0.74) / (CELL * 0.26)
            for x in range(CELL):
                dx = (x - CELL * 0.5) / (CELL * 0.5)
                d = math.sqrt(dx * dx + dy * dy)
                glow = max(0.0, 1.0 - d * 0.85)
                c = mix((40, 26, 20), base, min(1.0, glow * 1.3))
                c = mix(c, (255, 236, 190), max(0.0, glow - 0.55) * 1.6)
                img.putpixel((ox + x, oy + y), c)
        draw.rectangle([ox, oy, ox + CELL - 1, oy + CELL - 1], outline=(28, 20, 16))


STYLES = {
    "armor": ("hull", 11),
    "armor_dark": ("panel", 13),
    "silver": ("panel", 17),
    "bronze": ("panel", 19),
    "plaster": ("smooth", 23),
    "wood": ("plank", 29),
    "wood_dark": ("plank", 31),
    "leather": ("cloth", 37),
    "cloth": ("cloth", 41),
    "metal": ("panel", 43),
    "stone": ("panel", 47),
    "fire": ("fire", 53),
    "roof": ("hull", 59),
    "trim": ("panel", 61),
    "canvas": ("cloth", 67),
    "glassframe": ("smooth", 71),
}


def paint_atlas(path):
    img = Image.new("RGBA", (ATLAS, ATLAS), (255, 0, 255, 255))
    for region, (style, seed) in STYLES.items():
        paint_plate(img, region, PALETTE[region], style, seed)
    img.save(path, "PNG")
    return img


def region_uv(region):
    px, py = REGIONS[region]
    u0 = px * CELL / float(ATLAS) + UV_INSET
    v0 = py * CELL / float(ATLAS) + UV_INSET
    u1 = (px + 1) * CELL / float(ATLAS) - UV_INSET
    v1 = (py + 1) * CELL / float(ATLAS) - UV_INSET
    return (u0, v0, u1, v1)


def paint_glass(path):
    img = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    for i in range(-64, 128, 9):
        draw.line([(i, 0), (i + 30, 64)], fill=(214, 238, 252, 30), width=3)
    for i in range(-64, 128, 23):
        draw.line([(i, 0), (i + 14, 64)], fill=(255, 255, 255, 16), width=1)
    draw.line([(0, 0), (0, 63)], fill=(236, 246, 255, 70), width=2)
    img.save(path, "PNG")


def paint_glow(path):
    size = 64
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    for y in range(size):
        dy = (y - size / 2.0 + 0.5) / (size / 2.0)
        for x in range(size):
            dx = (x - size / 2.0 + 0.5) / (size / 2.0)
            a = max(0.0, 1.0 - math.sqrt(dx * dx + dy * dy))
            a = a * a * 1.05
            if a <= 0.004:
                continue
            img.putpixel((x, y), (255, 214, 158, clamp8(a * 255)))
    img.save(path, "PNG")


# ---------------------------------------------------------------------------
# Geometry
# ---------------------------------------------------------------------------
class Group(object):
    def __init__(self, name):
        self.name = name
        self.positions = []
        self.normals = []
        self.uvs = []
        self.indices = []
        self.quads = 0

    @property
    def vertices(self):
        return len(self.positions) // 3


PARTS = []
QUADS = []

# axis -> (u axis, v axis) so that cross(u, v) = +axis
FACE_AXES = {0: (2, 1), 1: (0, 2), 2: (0, 1)}


def _emit_face(group, region, axis, sign, constant, u0, u1, v0, v1, tile):
    ua, va = FACE_AXES[axis]
    nu = max(1, int(round(abs(u1 - u0) / tile)))
    nv = max(1, int(round(abs(v1 - v0) / tile)))
    ur = region_uv(region)
    flip = sign < 0

    for iu in range(nu):
        au = u0 + (u1 - u0) * (iu / float(nu))
        bu = u0 + (u1 - u0) * ((iu + 1) / float(nu))
        for iv in range(nv):
            av = v0 + (v1 - v0) * (iv / float(nv))
            bv = v0 + (v1 - v0) * ((iv + 1) / float(nv))
            corners = [(au, av), (bu, av), (bu, bv), (au, bv)]
            uvs = [(ur[0], ur[3]), (ur[2], ur[3]), (ur[2], ur[1]), (ur[0], ur[1])]
            if flip:
                corners = corners[::-1]
                uvs = uvs[::-1]

            base = group.vertices
            world = []
            for (cu, cv) in corners:
                point = [0.0, 0.0, 0.0]
                point[axis] = constant
                point[ua] = cu
                point[va] = cv
                group.positions.extend(point)
                world.append(tuple(point))
                normal = [0.0, 0.0, 0.0]
                normal[axis] = float(sign)
                group.normals.extend(normal)
            for uv in uvs:
                group.uvs.extend(uv)
            for index in (base, base + 1, base + 2, base, base + 2, base + 3):
                group.indices.extend((index, index, index))
            group.quads += 2
            QUADS.append((region, world, (0.0, float(sign), 0.0) if axis == 1 else
                          ((float(sign), 0.0, 0.0) if axis == 0 else (0.0, 0.0, float(sign)))))


def box(groups, group_name, name, x0, y0, z0, x1, y1, z1, region, tile=1.0):
    """Records one axis aligned box; faces are emitted later by emit_all()."""
    if x1 <= x0 or y1 <= y0 or z1 <= z0:
        raise ValueError("degenerate box %s (%s)" % (name, (x0, y0, z0, x1, y1, z1)))
    if x0 < -HALF_WIDTH - 1e-6 or x1 > HALF_WIDTH + 1e-6:
        raise ValueError("box %s exceeds X envelope: %s..%s" % (name, x0, x1))
    if z0 < -HALF_LENGTH - 1e-6 or z1 > HALF_LENGTH + 1e-6:
        raise ValueError("box %s exceeds Z envelope: %s..%s" % (name, z0, z1))
    if y0 < -1e-6 or y1 > ENVELOPE_HEIGHT + 1e-6:
        raise ValueError("box %s exceeds Y envelope: %s..%s" % (name, y0, y1))

    PARTS.append((group_name, name, x0, y0, z0, x1, y1, z1, region, tile))


def _face_planes(x0, y0, z0, x1, y1, z1):
    """Six (axis, sign, plane, u0, u1, v0, v1) entries, u/v as used by _emit_face."""
    return [
        (0, -1, x0, z0, z1, y0, y1),
        (0, 1, x1, z0, z1, y0, y1),
        (1, -1, y0, x0, x1, z0, z1),
        (1, 1, y1, x0, x1, z0, z1),
        (2, -1, z0, x0, x1, y0, y1),
        (2, 1, z1, x0, x1, y0, y1),
    ]


def _covered(face, planes, skip_index):
    """True when another box's opposite face exactly covers this one."""
    axis, sign, plane, u0, u1, v0, v1 = face
    for owner, other in planes:
        if owner == skip_index:
            continue
        oaxis, osign, oplane, ou0, ou1, ov0, ov1 = other
        if oaxis != axis or osign != -sign:
            continue
        if abs(oplane - plane) > 1e-4:
            continue
        if ou0 <= u0 + 1e-4 and ou1 >= u1 - 1e-4 and ov0 <= v0 + 1e-4 and ov1 >= v1 - 1e-4:
            return True
    return False


def emit_all(groups):
    """
    Emits every recorded box, dropping faces that are completely buried against the
    opposite face of another box.

    Two reasons. Visually, a buried face still costs triangles on Android. For the
    previews, a buried face and the surface above it are almost the same depth, and a
    painter's-algorithm renderer cannot order them reliably: the plinth showed through
    the wooden floor as dark bands. Removing genuinely hidden faces fixes both.
    """
    planes = []
    for index, part in enumerate(PARTS):
        for face in _face_planes(*part[2:8]):
            planes.append((index, face))

    dropped = 0
    for index, part in enumerate(PARTS):
        group_name, name, x0, y0, z0, x1, y1, z1, region, tile = part
        group = groups[group_name]
        for face in _face_planes(x0, y0, z0, x1, y1, z1):
            if _covered(face, planes, index):
                dropped += 1
                continue
            axis, sign, constant, u0, u1, v0, v1 = face
            _emit_face(group, region, axis, sign, constant, u0, u1, v0, v1, tile)
    return dropped


def plate(groups, group_name, name, x0, z0, x1, z1, y0, y1, region):
    """Thin plate helper (armour bands, sills, pilasters)."""
    box(groups, group_name, name, x0, y0, z0, x1, y1, z1, region, 1.0)


def build_parts():
    del PARTS[:]
    del QUADS[:]
    groups = dict((name, Group(name)) for name in GROUPS)

    # ---------------- Foundation ------------------------------------------
    # y = 0 is the deployment plane and is touched here only.
    box(groups, "Hull", "FoundationCore", -5.36, 0.0, -6.36, 5.36, 0.30, 6.36, "armor_dark")
    box(groups, "HullTrim", "PlinthBumper", -5.44, 0.04, -6.44, 5.44, 0.18, 6.44, "bronze")
    for side in (-1, 1):
        for zs in (-1, 1):
            box(groups, "HullTrim", "PlinthChamfer%d%d" % (side, zs),
                side * 5.36 if side > 0 else -5.44, 0.0, zs * 5.9 if zs > 0 else -6.44,
                side * 5.44 if side > 0 else -5.36, 0.30, zs * 6.44 if zs > 0 else -5.9,
                "trim")
    box(groups, "InteriorLower", "Level1Floor", -5.36, 0.30, -6.36, 5.36, FLOOR_TOP, 6.36, "wood")

    # ---------------- Lower hull ------------------------------------------
    # Wall bodies stop 6 cm short of the envelope; the armour plates make up the
    # difference, which gives a real shadow reveal between plates.
    box(groups, "Hull", "SideWallWest", -5.44, 0.30, -6.14, -5.14, 3.10, 6.14, "armor_dark")
    box(groups, "Hull", "SideWallEast", 5.14, 0.30, -6.14, 5.44, 3.10, 6.14, "armor_dark")
    box(groups, "Hull", "FrontWallWest", -5.14, 0.30, -6.38, -0.02, 3.10, -6.14, "armor_dark")
    box(groups, "Hull", "FrontWallEast", 0.02, 0.30, -6.38, 5.14, 3.10, -6.14, "armor_dark")
    box(groups, "Hull", "RearWallWest", -5.14, 0.30, DOOR_INNER_Z, DOOR_MIN_X, 3.10, REAR_FACE_Z, "armor_dark")
    box(groups, "Hull", "RearWallEast", DOOR_MAX_X, 0.30, DOOR_INNER_Z, 5.14, 3.10, REAR_FACE_Z, "armor_dark")
    box(groups, "Hull", "RearDoorHeader", DOOR_MIN_X, DOOR_TOP, DOOR_INNER_Z, DOOR_MAX_X, 3.10, REAR_FACE_Z, "armor_dark")

    # Armour bands: three plate courses per face, 6 cm proud of the wall body.
    for band, (y0, y1) in enumerate(((0.52, 1.28), (1.40, 2.16), (2.28, 2.98))):
        for side in (-1, 1):
            box(groups, "Hull", "SidePlate%d%d" % (side, band),
                min(side * 5.44, side * 5.50), y0, -4.60, max(side * 5.44, side * 5.50), y1, 4.60,
                "armor")
            box(groups, "Hull", "FrontPlate%d%d" % (side, band),
                min(side * 3.90, side * 0.40), y0, -6.44, max(side * 3.90, side * 0.40), y1, -6.38,
                "armor")
            box(groups, "Hull", "RearPlate%d%d" % (side, band),
                min(side * 4.50, side * 1.45), y0, 6.38, max(side * 4.50, side * 1.45), y1, 6.50,
                "armor")
    for side in (-1, 1):
        box(groups, "Hull", "FrontBrowWest" if side < 0 else "FrontBrowEast",
            min(side * 0.40, side * 3.90), 2.98, -6.50, max(side * 0.40, side * 3.90), 3.10, -6.38, "trim")
        # Front detailing: without it an 11 m armour face is a blank slab.
        box(groups, "HullTrim", "FrontSpine", -0.13, 0.42, -6.50, 0.13, 3.02, -6.44, "bronze")
        box(groups, "HullTrim", "FrontSpineStud", -0.22, 1.30, -6.50, 0.22, 1.46, -6.44, "bronze")
        box(groups, "Hull", "FrontVent%d" % side,
            min(side * 1.10, side * 2.60), 0.72, -6.50, max(side * 1.10, side * 2.60), 1.16, -6.44, "metal")
        for i in range(3):
            y = 0.78 + i * 0.13
            box(groups, "Hull", "FrontVentBar%d%d" % (side, i),
                min(side * 1.14, side * 2.56), y, -6.50, max(side * 1.14, side * 2.56), y + 0.05, -6.47, "armor_dark")
        box(groups, "HullTrim", "FrontCornice" if side < 0 else "FrontCorniceR",
            min(side * 3.90, side * 0.00), 2.86, -6.50, max(side * 3.90, side * 0.00), 2.98, -6.40, "bronze")

    # Bronze corner posts wrap the four vertical corners and touch both limits.
    for sx in (-1, 1):
        for sz in (-1, 1):
            x0, x1 = (5.24, 5.50) if sx > 0 else (-5.50, -5.24)
            z0, z1 = (6.24, 6.50) if sz > 0 else (-6.50, -6.24)
            box(groups, "HullTrim", "CornerPost%d%d" % (sx, sz), x0, 0.30, z0, x1, 3.18, z1, "bronze")

    for i, z in enumerate((-3.20, -1.10, 0.90, 3.00)):
        for side in (-1, 1):
            box(groups, "HullTrim", "SideRib%d%d" % (side, i),
                min(side * 5.44, side * 5.50), 0.40, z - 0.09, max(side * 5.44, side * 5.50), 3.06, z + 0.09,
                "silver")
    for side in (-1, 1):
        box(groups, "HullTrim", "Buttress%d" % side,
            min(side * 5.20, side * 5.50), 0.30, -0.70, max(side * 5.20, side * 5.50), 2.40, 0.70, "armor_dark")
        box(groups, "HullTrim", "ButtressCap%d" % side,
            min(side * 5.16, side * 5.50), 2.40, -0.78, max(side * 5.16, side * 5.50), 2.62, 0.78, "bronze")

    # ---------------- Rear entrance ---------------------------------------
    box(groups, "HullTrim", "DoorJambWest", DOOR_MIN_X - 0.16, FLOOR_TOP, DOOR_INNER_Z - 0.02,
        DOOR_MIN_X, DOOR_TOP + 0.16, REAR_FACE_Z, "bronze")
    box(groups, "HullTrim", "DoorJambEast", DOOR_MAX_X, FLOOR_TOP, DOOR_INNER_Z - 0.02,
        DOOR_MAX_X + 0.16, DOOR_TOP + 0.16, REAR_FACE_Z, "bronze")
    box(groups, "HullTrim", "DoorLintel", DOOR_MIN_X - 0.16, DOOR_TOP, DOOR_INNER_Z - 0.02,
        DOOR_MAX_X + 0.16, DOOR_TOP + 0.16, REAR_FACE_Z, "bronze")
    # The sill is the visual of the 0.36 threshold the collision uses; top face is 0.36.
    box(groups, "HullTrim", "DoorSill", DOOR_MIN_X - 0.18, 0.30, DOOR_INNER_Z - 0.08,
        DOOR_MAX_X + 0.18, FLOOR_TOP + 0.015, REAR_FACE_Z + 0.02, "trim")
    box(groups, "HullTrim", "DoorCanopy", DOOR_MIN_X - 0.34, 3.22, DOOR_INNER_Z - 0.02,
        DOOR_MAX_X + 0.34, 3.30, REAR_FACE_Z - 0.04, "bronze")
    for side in (-1, 1):
        box(groups, "HullTrim", "DoorBrace%d" % side,
            min(side * 1.34, side * 1.44), 3.30, DOOR_INNER_Z - 0.06,
            max(side * 1.34, side * 1.44), 3.56, REAR_FACE_Z - 0.08, "silver")

    # ---------------- Interior level 1 ------------------------------------
    box(groups, "InteriorLower", "AirlockWallWest", -1.53, FLOOR_TOP, 3.85, -1.37, 3.10, DOOR_INNER_Z, "plaster")
    box(groups, "InteriorLower", "AirlockWallEast", 1.37, FLOOR_TOP, 3.85, 1.53, 3.10, DOOR_INNER_Z, "plaster")
    box(groups, "InteriorLower", "AirlockHeader", -1.53, 2.62, 3.85, 1.53, 3.10, DOOR_INNER_Z, "plaster")
    for side in (-1, 1):
        box(groups, "InteriorLower", "WallWainscot%d" % side,
            min(side * 5.14, side * 5.08), FLOOR_TOP, -5.90, max(side * 5.14, side * 5.08), 1.25, 5.90, "wood_dark")
        box(groups, "InteriorLower", "WallPlate%d" % side,
            min(side * 5.14, side * 5.07), 1.25, -5.90, max(side * 5.14, side * 5.07), 3.04, 5.90, "plaster")
        box(groups, "InteriorLower", "RearLining%d" % side,
            min(side * 1.55, side * 5.06), FLOOR_TOP, DOOR_INNER_Z - 0.07,
            max(side * 1.55, side * 5.06), 3.04, DOOR_INNER_Z, "plaster")
        box(groups, "InteriorLower", "FrontLining%d" % side,
            min(side * 0.02, side * 5.06), FLOOR_TOP, -6.14,
            max(side * 0.02, side * 5.06), 3.04, -6.07, "plaster")
    box(groups, "InteriorLower", "FrontPanelRail", -5.06, 1.25, -6.07, 5.06, 1.33, -6.00, "wood_dark")
    box(groups, "Details", "FrontMapBoard", -2.60, 1.60, -6.08, -0.60, 2.32, -6.02, "canvas")
    box(groups, "Details", "FrontMapFrame", -2.66, 1.54, -6.09, -0.54, 2.38, -6.01, "bronze")
    for i in range(3):
        box(groups, "Details", "PegRail%d" % i, 0.20, 1.62 + i * 0.24, -6.09, 2.30, 1.68 + i * 0.24, -6.03, "wood_dark")
    box(groups, "InteriorLower", "CeilingLining", -5.06, 3.02, -6.07, 5.06, 3.10, 5.90, "wood_dark")
    for side in (-1, 1):
        box(groups, "InteriorLower", "CeilingBeam%d" % side,
            min(side * 5.06, side * 4.76), 2.90, -6.07, max(side * 5.06, side * 4.76), 3.02, 5.90, "wood_dark")
    for i, z in enumerate((-3.6, -1.2, 1.2, 3.6)):
        box(groups, "InteriorLower", "CeilingJoist%d" % i, -4.76, 2.92, z - 0.08, 4.76, 3.02, z + 0.08, "wood_dark")

    # ---------------- Furniture level 1 -----------------------------------
    # Double bunk, west wall: frame, mattresses, pillow, posts, rungs.
    box(groups, "FurnitureLower", "BunkLowerFrame", -5.06, 0.55, -1.30, -3.46, 0.68, 1.45, "wood_dark")
    box(groups, "FurnitureLower", "BunkLowerMattress", -5.00, 0.68, -1.24, -3.52, 0.86, 1.39, "cloth")
    box(groups, "FurnitureLower", "BunkLowerPillow", -4.94, 0.86, -1.18, -3.58, 0.99, -0.72, "canvas")
    box(groups, "FurnitureLower", "BunkLowerDuvet", -4.98, 0.86, -0.10, -3.54, 0.94, 1.37, "leather")
    box(groups, "FurnitureLower", "BunkUpperFrame", -5.06, 1.86, -1.30, -3.46, 1.98, 1.45, "wood_dark")
    box(groups, "FurnitureLower", "BunkUpperMattress", -5.00, 1.98, -1.24, -3.52, 2.14, 1.39, "cloth")
    box(groups, "FurnitureLower", "BunkUpperPillow", -4.94, 2.14, -1.18, -3.58, 2.26, -0.72, "canvas")
    for name, x, z in (("A", -5.04, 1.36), ("B", -5.04, -1.42), ("C", -3.58, 1.36), ("D", -3.58, -1.42)):
        box(groups, "FurnitureLower", "BunkPost" + name, x, FLOOR_TOP, z, x + 0.12, 2.34, z + 0.12, "bronze")
    for i in range(4):
        y = 1.00 + i * 0.24
        box(groups, "FurnitureLower", "BunkRung%d" % i, -4.98, y, 1.38, -4.62, y + 0.05, 1.44, "silver")
    box(groups, "FurnitureLower", "BunkGuardRail", -3.54, 0.98, -1.28, -3.48, 1.08, 1.43, "bronze")

    # Wardrobe, east wall. It opens toward the room (-X): a wardrobe whose doors face
    # a side wall reads as a blank brown box from everywhere the player can stand.
    box(groups, "FurnitureLower", "WardrobeBack", 4.98, FLOOR_TOP, 0.40, 5.06, 2.55, 2.10, "wood_dark")
    box(groups, "FurnitureLower", "WardrobeSideA", 4.36, FLOOR_TOP, 0.40, 5.06, 2.55, 0.48, "wood_dark")
    box(groups, "FurnitureLower", "WardrobeSideB", 4.36, FLOOR_TOP, 2.02, 5.06, 2.55, 2.10, "wood_dark")
    box(groups, "FurnitureLower", "WardrobeTop", 4.36, 2.55, 0.40, 5.06, 2.62, 2.10, "wood_dark")
    box(groups, "FurnitureLower", "WardrobePlinth", 4.30, FLOOR_TOP, 0.36, 5.06, 0.46, 2.14, "wood_dark")
    box(groups, "FurnitureLower", "WardrobeShelf", 4.40, 1.52, 0.48, 5.00, 1.58, 2.02, "wood")
    box(groups, "FurnitureLower", "WardrobeDoorA", 4.30, 0.46, 0.52, 4.36, 2.52, 1.22, "wood")
    box(groups, "FurnitureLower", "WardrobeDoorB", 4.30, 0.46, 1.28, 4.36, 2.52, 1.98, "wood")
    box(groups, "FurnitureLower", "WardrobeRecessA", 4.34, 0.62, 0.62, 4.37, 2.36, 1.12, "wood_dark")
    box(groups, "FurnitureLower", "WardrobeRecessB", 4.34, 0.62, 1.38, 4.37, 2.36, 1.88, "wood_dark")
    box(groups, "Details", "WardrobeHandleA", 4.27, 1.36, 1.10, 4.31, 1.54, 1.18, "bronze")
    box(groups, "Details", "WardrobeHandleB", 4.27, 1.36, 1.32, 4.31, 1.54, 1.40, "bronze")
    box(groups, "Details", "WardrobeLintel", 4.28, 2.52, 0.44, 4.38, 2.62, 2.06, "bronze")

    # Mechanical hearth: stands against the west wall and faces the room. The old
    # heating alcove partition hid the fire behind a solid plaster wall.
    box(groups, "FurnitureLower", "HearthBody", -5.06, FLOOR_TOP, 2.30, -4.46, 1.68, 3.72, "armor_dark")
    box(groups, "FurnitureLower", "HearthTop", -5.06, 1.68, 2.24, -4.26, 1.82, 3.78, "metal")
    box(groups, "FurnitureLower", "HearthFirebox", -4.46, 0.62, 2.48, -4.34, 1.26, 3.54, "fire")
    box(groups, "FurnitureLower", "HearthRecess", -4.47, 0.56, 2.44, -4.44, 1.32, 3.58, "armor_dark")
    box(groups, "FurnitureLower", "HearthGrate", -4.46, 0.50, 2.44, -4.36, 0.62, 3.58, "metal")
    box(groups, "FurnitureLower", "HearthHood", -5.06, 1.82, 2.20, -4.20, 2.02, 3.82, "bronze")
    box(groups, "FurnitureLower", "HearthLipA", -4.42, 0.62, 2.40, -4.30, 1.30, 2.48, "bronze")
    box(groups, "FurnitureLower", "HearthLipB", -4.42, 0.62, 3.54, -4.30, 1.30, 3.62, "bronze")
    box(groups, "FurnitureLower", "HearthFlue", -4.90, 2.02, 2.86, -4.50, 3.02, 3.22, "metal")
    box(groups, "FurnitureLower", "HearthFlueCollar", -4.98, 2.42, 2.78, -4.44, 2.54, 3.30, "bronze")
    for i in range(4):
        z = 2.62 + i * 0.25
        box(groups, "FurnitureLower", "HearthBar%d" % i, -4.34, 0.66, z, -4.30, 1.22, z + 0.06, "metal")
    box(groups, "FurnitureLower", "HearthFootA", -4.98, FLOOR_TOP, 2.34, -4.42, 0.50, 2.46, "bronze")
    box(groups, "FurnitureLower", "HearthFootB", -4.98, FLOOR_TOP, 3.56, -4.42, 0.50, 3.68, "bronze")
    box(groups, "FurnitureLower", "HearthMantel", -5.06, 2.02, 2.16, -4.18, 2.16, 3.86, "wood_dark")

    # Table and two chairs, forward of the activity square.
    box(groups, "FurnitureLower", "TableTop", -0.86, 1.14, -3.66, 0.86, 1.24, -2.82, "wood")
    box(groups, "FurnitureLower", "TableApron", -0.78, 1.02, -3.58, 0.78, 1.14, -2.90, "wood_dark")
    for sx in (-1, 1):
        for sz in (-1, 1):
            x = 0.78 * sx
            z = -3.24 + 0.38 * sz
            box(groups, "FurnitureLower", "TableLeg%d%d" % (sx, sz), x - 0.05, FLOOR_TOP, z - 0.05, x + 0.05, 1.14, z + 0.05, "wood_dark")
    for i, x in enumerate((-1.55, 1.55)):
        box(groups, "FurnitureLower", "ChairSeat%d" % i, x - 0.30, 0.86, -3.60, x + 0.30, 0.94, -3.00, "wood")
        box(groups, "FurnitureLower", "ChairBack%d" % i, x - 0.30, 0.94, -3.68, x + 0.30, 1.52, -3.60, "wood_dark")
        for sz in (0, 1):
            z = -3.56 + sz * 0.50
            box(groups, "FurnitureLower", "ChairLegL%d%d" % (i, sz), x - 0.28, FLOOR_TOP, z - 0.04, x - 0.20, 0.86, z + 0.04, "wood_dark")
            box(groups, "FurnitureLower", "ChairLegR%d%d" % (i, sz), x + 0.20, FLOOR_TOP, z - 0.04, x + 0.28, 0.86, z + 0.04, "wood_dark")

    # Open shelving: a solid block swallowed the books that give the wall its life.
    box(groups, "FurnitureLower", "ShelfBack", 5.02, FLOOR_TOP, -3.70, 5.10, 2.30, -1.30, "wood_dark")
    box(groups, "FurnitureLower", "ShelfSideA", 4.30, FLOOR_TOP, -3.70, 5.02, 2.30, -3.62, "wood_dark")
    box(groups, "FurnitureLower", "ShelfSideB", 4.30, FLOOR_TOP, -1.38, 5.02, 2.30, -1.30, "wood_dark")
    box(groups, "FurnitureLower", "ShelfTop", 4.30, 2.22, -3.70, 5.02, 2.30, -1.30, "wood_dark")
    for i in range(4):
        y = 0.58 + i * 0.42
        box(groups, "FurnitureLower", "ShelfBoard%d" % i, 4.30, y, -3.62, 5.02, y + 0.05, -1.38, "wood")
        box(groups, "FurnitureLower", "ShelfBooks%d" % i, 4.40, y + 0.05, -3.54, 4.94, y + 0.34, -2.20, "cloth")
        box(groups, "FurnitureLower", "ShelfBooksB%d" % i, 4.42, y + 0.05, -2.10, 4.90, y + 0.30, -1.46, "leather")

    # Workbench with tool rack, east wall aft.
    box(groups, "FurnitureLower", "WorkbenchTop", 3.30, 1.02, 3.30, 5.08, 1.12, 4.86, "wood")
    box(groups, "FurnitureLower", "WorkbenchApron", 3.30, 0.86, 4.78, 5.08, 1.02, 4.90, "wood_dark")
    for x in (3.36, 5.00):
        box(groups, "FurnitureLower", "WorkbenchLeg%d" % int(x * 100), x - 0.06, FLOOR_TOP, 3.38, x + 0.06, 1.02, 4.80, "wood_dark")
    box(groups, "FurnitureLower", "ToolRack", 4.98, 1.30, 3.34, 5.06, 2.20, 4.82, "wood_dark")
    for i in range(3):
        z = 3.56 + i * 0.42
        box(groups, "Details", "ToolHook%d" % i, 4.94, 1.94, z, 4.99, 2.02, z + 0.10, "bronze")
        box(groups, "Details", "Tool%d" % i, 4.84, 1.40, z + 0.02, 4.96, 1.94, z + 0.08, "silver")
    box(groups, "FurnitureLower", "ViceBody", 3.40, 1.12, 3.52, 3.80, 1.32, 3.92, "metal")

    # ---------------- Main stair ------------------------------------------
    for tread in range(STAIR_STEPS):
        top = FLOOR_TOP + STAIR_RISE * (tread + 1)
        z0 = STAIR_MIN_Z + tread * STAIR_RUN
        box(groups, "Deck", "StairTread%d" % tread, STAIR_MIN_X, FLOOR_TOP, z0, STAIR_MAX_X, top, z0 + STAIR_RUN, "wood")
        box(groups, "Deck", "StairNosing%d" % tread, STAIR_MIN_X, top - 0.04, z0, STAIR_MAX_X, top, z0 + 0.06, "wood_dark")
    box(groups, "Deck", "StairStringer", STAIR_MIN_X - 0.02, FLOOR_TOP, STAIR_MIN_Z,
        STAIR_MIN_X + 0.06, DECK_TOP, STAIR_END_Z, "wood_dark")
    # The handrail follows the tread line: a constant-height rail cannot follow a
    # 0.435 rise per 0.50 run and would float away from the flight.
    for tread in range(STAIR_STEPS):
        top = FLOOR_TOP + STAIR_RISE * (tread + 1)
        z0 = STAIR_MIN_Z + tread * STAIR_RUN
        box(groups, "Deck", "StairBaluster%d" % tread,
            STAIR_MAX_X - 0.10, top, z0 + 0.22, STAIR_MAX_X - 0.03, top + 0.86, z0 + 0.30, "bronze")
        box(groups, "Deck", "StairRail%d" % tread,
            STAIR_MAX_X - 0.11, top + 0.86, z0, STAIR_MAX_X - 0.02, top + 0.94, z0 + STAIR_RUN + 0.02,
            "bronze", 0.25)

    # ---------------- Level 2 deck ----------------------------------------
    # Deck slabs stop at the stairwell (x -4.40..-2.20, z -4.00..0.25).
    deck_boxes = [
        ("DeckMain", 1.10, -5.95, 4.40, 4.95),
        ("DeckWestMain", -2.20, -5.95, 1.10, 4.95),
        ("DeckWestRear", -4.40, STAIRWELL_Z, -2.20, 4.95),
        ("DeckWestFront", -4.40, -5.95, -2.20, STAIR_MIN_Z - 0.03),
    ]
    for name, x0, z0, x1, z1 in deck_boxes:
        box(groups, "Deck", name + "Slab", x0, DECK_BOTTOM, z0, x1, DECK_TOP - 0.04, z1, "armor_dark")
        box(groups, "Deck", name + "Floor", x0, DECK_TOP - 0.04, z0, x1, DECK_TOP, z1, "wood")
    # Shoulder cap: the upper shell is 8.5 wide over an 11 wide hull, so the exposed
    # hull top must be closed with armour or the deck reads as a hole from above.
    for side in (-1, 1):
        x0, x1 = (4.40, 5.50) if side > 0 else (-5.50, -4.40)
        box(groups, "Hull", "ShoulderCap%d" % side, x0, 3.10, -6.14, x1, 3.18, 6.14, "roof")
        box(groups, "HullTrim", "ShoulderTrim%d" % side,
            min(x0, x1) if side > 0 else x0, 3.10, 6.10, x1 if side > 0 else -4.40, 3.22, 6.20, "bronze")
    for zs in (-1, 1):
        z0, z1 = (4.40, 5.50) if zs > 0 else (-5.50, -4.40)
        box(groups, "Hull", "ShoulderCapRear" if zs > 0 else "ShoulderCapFront",
            -5.50, 3.10, z0, 5.50, 3.18, z1, "roof")
    box(groups, "Deck", "DeckFasciaWest", -4.42, DECK_BOTTOM - 0.10, -5.97, -2.20, DECK_BOTTOM, STAIR_MIN_Z, "bronze")
    box(groups, "Deck", "DeckFasciaMain", -2.20, DECK_BOTTOM - 0.10, -5.97, 4.42, DECK_BOTTOM, 4.97, "bronze")
    box(groups, "Deck", "DeckEdgeTrim", -4.42, DECK_TOP - 0.02, 4.90, 4.42, DECK_TOP + 0.06, 4.98, "bronze")

    # ---------------- Upper shell -----------------------------------------
    for side in (-1, 1):
        x = side * 4.25
        box(groups, "UpperShell", "SideSill%d" % side,
            min(x - 0.13, x + 0.13), DECK_TOP, -4.80, max(x - 0.13, x + 0.13), WINDOW_SILL_TOP, 4.80, "armor")
        box(groups, "UpperShell", "SideHeader%d" % side,
            min(x - 0.13, x + 0.13), WINDOW_HEAD_BOTTOM, -4.80, max(x - 0.13, x + 0.13), 7.15, 4.80, "armor")
        box(groups, "UpperShell", "SideBand%d" % side,
            min(x - 0.16, x + 0.16), 6.86, -4.80, max(x - 0.16, x + 0.16), 7.15, 4.80, "bronze")
        box(groups, "UpperShell", "SideLining%d" % side,
            min(x - 0.15, x - 0.09), DECK_TOP, -4.70, max(x - 0.15, x - 0.09), 7.05, 4.70, "plaster")
    for z in (-4.65, -2.45, -0.35, 1.85, 4.65):
        for side in (-1, 1):
            x = side * 4.25
            box(groups, "UpperShell", "SidePillar%d%d" % (side, int(z * 10)),
                min(x - 0.14, x + 0.14), WINDOW_SILL_TOP, z - 0.17, max(x - 0.14, x + 0.14), WINDOW_HEAD_BOTTOM, z + 0.17, "silver")
            box(groups, "Details", "SidePillarCap%d%d" % (side, int(z * 10)),
                min(x - 0.18, x + 0.18), WINDOW_HEAD_BOTTOM - 0.14, z - 0.21, max(x - 0.18, x + 0.18), WINDOW_HEAD_BOTTOM, z + 0.21, "bronze")
    box(groups, "UpperShell", "RearWallWest", -4.30, DECK_TOP, 4.82, DOOR_MIN_X, 7.15, 5.08, "armor")
    box(groups, "UpperShell", "RearWallEast", DOOR_MAX_X, DECK_TOP, 4.82, 4.30, 7.15, 5.08, "armor")
    box(groups, "UpperShell", "RearHeader", DOOR_MIN_X, WINDOW_HEAD_BOTTOM, 4.82, DOOR_MAX_X, 7.15, 5.08, "armor")
    box(groups, "UpperShell", "RearOpeningSill", DOOR_MIN_X, DECK_TOP, 4.80, DOOR_MAX_X, 3.96, 5.10, "trim")
    box(groups, "UpperShell", "RearLiningWest", -4.20, DECK_TOP, 4.74, DOOR_MIN_X, 7.10, 4.82, "plaster")
    box(groups, "UpperShell", "RearLiningEast", DOOR_MAX_X, DECK_TOP, 4.74, 4.20, 7.10, 4.82, "plaster")
    box(groups, "UpperShell", "DeckCeiling", -4.12, 7.04, -6.10, 4.12, 7.15, 5.00, "plaster")
    for side in (-1, 1):
        box(groups, "UpperShell", "CeilingBeam%d" % side,
            min(side * 4.12, side * 3.84), 6.96, -6.10, max(side * 4.12, side * 3.84), 7.04, 5.00, "wood_dark")

    # ---------------- Panoramic nose --------------------------------------
    box(groups, "Nose", "NoseSillFront", -3.20, DECK_TOP, -6.50, 3.20, WINDOW_SILL_TOP, -6.22, "armor")
    box(groups, "Nose", "NoseHeaderFront", -3.20, WINDOW_HEAD_BOTTOM, -6.50, 3.20, 7.15, -6.22, "armor")
    box(groups, "Nose", "NoseSillInner", -4.10, DECK_TOP, -6.22, 4.10, WINDOW_SILL_TOP, -5.96, "trim")
    for x in (-3.12, 0.0, 3.12):
        box(groups, "Nose", "NoseMullion%d" % int(x * 10),
            min(x - 0.07, x + 0.07), WINDOW_SILL_TOP, -6.46, max(x - 0.07, x + 0.07), WINDOW_HEAD_BOTTOM, -6.26, "bronze")
    box(groups, "Nose", "NoseTransom", -3.20, 5.52, -6.46, 3.20, 5.62, -6.24, "bronze")
    # Splayed corner bays, built as three stepped plates per side so every corner
    # stays axis aligned with the collision proxy.
    for side in (-1, 1):
        steps = ((4.10, 5.06, -6.46, -6.22), (4.62, 5.10, -6.10, -5.70), (4.94, 5.10, -5.70, -4.42))
        for i, (x0, x1, z0, z1) in enumerate(steps):
            xa, xb = (x0, x1) if side > 0 else (-x1, -x0)
            box(groups, "Nose", "NoseSplaySill%d%d" % (side, i), xa, DECK_TOP, z0, xb, WINDOW_SILL_TOP, z1, "armor")
            box(groups, "Nose", "NoseSplayHead%d%d" % (side, i), xa, WINDOW_HEAD_BOTTOM, z0, xb, 7.15, z1, "armor")
            # A post only at the outer end of each step: the aperture between two
            # posts has to stay open or the splayed bay is blind.
            post_x0 = xa if side > 0 else xb - 0.12
            box(groups, "Nose", "NoseSplayPost%d%d" % (side, i), post_x0, WINDOW_SILL_TOP, z1 - 0.12,
                post_x0 + 0.12, WINDOW_HEAD_BOTTOM, z1, "bronze")

    # ---------------- Roof -------------------------------------------------
    box(groups, "Roof", "RoofDeckWest", -4.44, ROOF_BOTTOM, -5.97, SHAFT_MIN_X, ROOF_TOP, 4.97, "roof")
    box(groups, "Roof", "RoofDeckEast", SHAFT_MAX_X, ROOF_BOTTOM, -5.97, 4.44, ROOF_TOP, 4.97, "roof")
    box(groups, "Roof", "RoofDeckSouth", SHAFT_MIN_X, ROOF_BOTTOM, -5.97, SHAFT_MAX_X, ROOF_TOP, SHAFT_MIN_Z, "roof")
    box(groups, "Roof", "RoofDeckNorth", SHAFT_MIN_X, ROOF_BOTTOM, SHAFT_MAX_Z, SHAFT_MAX_X, ROOF_TOP, 4.97, "roof")
    box(groups, "Roof", "HatchCurbNorth", SHAFT_MIN_X - 0.10, ROOF_BOTTOM, SHAFT_MAX_Z, SHAFT_MAX_X + 0.10, 7.72, SHAFT_MAX_Z + 0.10, "bronze")
    box(groups, "Roof", "HatchCurbSouth", SHAFT_MIN_X - 0.10, ROOF_BOTTOM, SHAFT_MIN_Z - 0.10, SHAFT_MAX_X + 0.10, 7.72, SHAFT_MIN_Z, "bronze")
    box(groups, "Roof", "HatchCurbWest", SHAFT_MIN_X - 0.10, ROOF_BOTTOM, SHAFT_MIN_Z, SHAFT_MIN_X, 7.72, SHAFT_MAX_Z, "bronze")
    box(groups, "Roof", "HatchCurbEast", SHAFT_MAX_X, ROOF_BOTTOM, SHAFT_MIN_Z, SHAFT_MAX_X + 0.10, 7.72, SHAFT_MAX_Z, "bronze")
    box(groups, "Roof", "HatchLid", SHAFT_MIN_X - 0.04, 7.76, SHAFT_MAX_Z + 0.12, SHAFT_MAX_X + 0.04, 7.84, SHAFT_MAX_Z + 0.46, "wood")
    box(groups, "Roof", "EquipmentPad", -1.90, ROOF_TOP - 0.06, 1.60, 1.90, ROOF_TOP + 0.16, 4.60, "metal")
    box(groups, "Roof", "EquipmentRailL", -1.98, ROOF_TOP + 0.16, 1.54, -1.88, ROOF_TOP + 0.28, 4.66, "bronze")
    box(groups, "Roof", "EquipmentRailR", 1.88, ROOF_TOP + 0.16, 1.54, 1.98, ROOF_TOP + 0.28, 4.66, "bronze")
    for i in range(4):
        box(groups, "Roof", "PadBolt%d" % i, -1.60 + i * 1.05, ROOF_TOP + 0.16, 1.72, -1.48 + i * 1.05, ROOF_TOP + 0.22, 1.84, "bronze")
    box(groups, "Roof", "BeaconMast", -0.09, ROOF_TOP + 0.16, 2.95, 0.09, 8.86, 3.13, "silver")
    box(groups, "Roof", "BeaconCage", -0.17, 8.86, 2.87, 0.17, 8.98, 3.21, "bronze")
    box(groups, "Roof", "BeaconLamp", -0.12, 8.88, 2.92, 0.12, 9.00, 3.16, "fire")
    box(groups, "Roof", "RoofRimTrim", -4.44, ROOF_TOP - 0.06, -5.97, 4.44, ROOF_TOP + 0.02, 4.97, "bronze")

    # ---------------- Roof guard rail -------------------------------------
    for z in (-5.55, -3.20, -0.85, 1.50, 3.85, 4.55):
        for side in (-1, 1):
            x = side * 4.12
            box(groups, "RoofRails", "RailPost%d%d" % (side, int(z * 10)),
                min(x - 0.08, x + 0.08), ROOF_TOP, z - 0.08, max(x - 0.08, x + 0.08), RAIL_TOP, z + 0.08, "silver")
    for z in (-5.55, -3.20, -0.85, 1.50, 3.85):
        box(groups, "RoofRails", "RailPostFront%d" % int(z * 10), -4.18, ROOF_TOP, z - 0.08, -4.02, RAIL_TOP, z + 0.08, "silver")
    box(groups, "RoofRails", "RailFront", -4.18, RAIL_TOP - 0.10, -5.63, 4.18, RAIL_TOP, -5.47, "bronze")
    box(groups, "RoofRails", "RailFrontMid", -4.18, 7.96, -5.60, 4.18, 8.04, -5.50, "silver")
    box(groups, "RoofRails", "RailRear", -4.18, RAIL_TOP - 0.10, 4.47, -1.13, RAIL_TOP, 4.63, "bronze")
    box(groups, "RoofRails", "RailRearMid", -4.18, 7.96, 4.50, -1.13, 8.04, 4.60, "silver")
    for side in (-1, 1):
        x = side * 4.12
        box(groups, "RoofRails", "RailSide%d" % side,
            min(x - 0.08, x + 0.08), RAIL_TOP - 0.10, -5.63, max(x - 0.08, x + 0.08), RAIL_TOP, 4.63, "bronze")
        box(groups, "RoofRails", "RailSideMid%d" % side,
            min(x - 0.05, x + 0.05), 7.96, -5.60, max(x - 0.05, x + 0.05), 8.04, 4.60, "silver")

    # ---------------- Level 2 furniture -----------------------------------
    box(groups, "FurnitureUpper", "ConsoleBody", -1.90, DECK_TOP, -4.70, 1.90, DECK_TOP + 0.74, -3.88, "wood_dark")
    box(groups, "FurnitureUpper", "ConsoleTop", -2.00, DECK_TOP + 0.74, -4.78, 2.00, DECK_TOP + 0.84, -3.80, "wood")
    box(groups, "FurnitureUpper", "ConsoleLip", -2.00, DECK_TOP + 0.84, -4.80, 2.00, DECK_TOP + 0.96, -4.74, "bronze")
    for i, x in enumerate((-1.30, 0.0, 1.30)):
        box(groups, "FurnitureUpper", "ConsoleDial%d" % i, x - 0.20, DECK_TOP + 0.84, -4.42, x + 0.20, DECK_TOP + 0.90, -4.12, "metal")
    box(groups, "FurnitureUpper", "ConsoleLever", 1.08, DECK_TOP + 0.84, -4.40, 1.18, DECK_TOP + 1.18, -4.30, "bronze")
    for side in (-1, 1):
        x0, x1 = (3.58, 4.08) if side > 0 else (-4.08, -3.58)
        box(groups, "FurnitureUpper", "RestBench%d" % side, x0, DECK_TOP + 0.34, 0.60, x1, DECK_TOP + 0.46, 3.60, "wood")
        bx0, bx1 = (4.04, 4.12) if side > 0 else (-4.12, -4.04)
        box(groups, "FurnitureUpper", "RestBenchBack%d" % side, bx0, DECK_TOP + 0.46, 0.60, bx1, DECK_TOP + 1.22, 3.60, "wood_dark")
        cx0, cx1 = (3.62, 4.02) if side > 0 else (-4.02, -3.62)
        box(groups, "FurnitureUpper", "RestBenchCushion%d" % side, cx0, DECK_TOP + 0.46, 0.70, cx1, DECK_TOP + 0.58, 3.50, "cloth")
        for z in (0.72, 3.42):
            lx0, lx1 = (3.64, 3.72) if side > 0 else (-3.72, -3.64)
            box(groups, "FurnitureUpper", "RestBenchLeg%d%d" % (side, int(z * 100)), lx0, DECK_TOP, z, lx1, DECK_TOP + 0.34, z + 0.08, "bronze")
    box(groups, "FurnitureUpper", "MapTableTop", -1.40, DECK_TOP + 0.78, 0.70, 0.40, DECK_TOP + 0.88, 2.00, "wood")
    box(groups, "FurnitureUpper", "MapTableChart", -1.28, DECK_TOP + 0.88, 0.80, 0.28, DECK_TOP + 0.90, 1.90, "canvas")
    for sx in (-1.28, 0.28):
        for sz in (0.80, 1.88):
            box(groups, "FurnitureUpper", "MapTableLeg%d%d" % (int(sx * 100), int(sz * 100)),
                sx, DECK_TOP, sz - 0.04, sx + 0.08, DECK_TOP + 0.78, sz + 0.04, "bronze")

    # Roof-access shaft: rungs and guides, the aperture stays clear.
    for rung in range(9):
        y = DECK_TOP + 0.20 + rung * 0.36
        box(groups, "FurnitureUpper", "ShaftRung%d" % rung,
            SHAFT_MIN_X + 0.20, y, SHAFT_MAX_Z - 0.44, SHAFT_MAX_X - 0.20, y + 0.08, SHAFT_MAX_Z - 0.32, "silver")
    for side in (-1, 1):
        x = (SHAFT_MIN_X + SHAFT_MAX_X) / 2 + side * 0.74
        box(groups, "FurnitureUpper", "ShaftRail%d" % side, x - 0.05, DECK_TOP, SHAFT_MAX_Z - 0.40, x + 0.05, ROOF_BOTTOM, SHAFT_MAX_Z - 0.30, "bronze")

    # ---------------- Lamps ------------------------------------------------
    for side in (-1, 1):
        x = side * 4.96
        box(groups, "Details", "LampBracket%d" % side,
            min(x, side * 5.12), 2.14, 2.14, max(x, side * 5.12), 2.26, 2.26, "bronze")
        box(groups, "Details", "LampBowl%d" % side,
            x - 0.12, 2.26, 2.08, x + 0.12, 2.34, 2.32, "bronze")
        box(groups, "Details", "LampGlow%d" % side,
            x - 0.09, 2.34, 2.11, x + 0.09, 2.52, 2.29, "fire")
    box(groups, "Details", "LanternRod", -0.04, 6.92, 0.76, 0.04, 7.04, 0.84, "bronze")
    box(groups, "Details", "LanternCage", -0.20, 6.52, 0.60, 0.20, 6.92, 1.00, "bronze")
    box(groups, "Details", "LanternCore", -0.14, 6.58, 0.66, 0.14, 6.86, 0.94, "fire")

    emit_all(groups)
    return groups


def write_dae(groups, path):
    order = list(GROUPS)
    out = ['<?xml version="1.0" encoding="utf-8"?>',
           '<COLLADA xmlns="http://www.collada.org/2005/11/COLLADASchema" version="1.4.1">',
           '  <asset><contributor><author>SCIE-Port contributors</author>'
           '<authoring_tool>New-PortableFortressArt.py</authoring_tool></contributor>'
           '<unit name="meter" meter="1"/><up_axis>Y_UP</up_axis></asset>',
           '  <library_effects><effect id="FortressEffect"><profile_COMMON><technique sid="common">'
           '<lambert><diffuse><color>1 1 1 1</color></diffuse></lambert></technique>'
           '</profile_COMMON></effect></library_effects>',
           '  <library_materials><material id="FortressMaterial" name="FortressMaterial">'
           '<instance_effect url="#FortressEffect"/></material></library_materials>',
           '  <library_geometries>']

    total_tris = 0
    for name in order:
        group = groups[name]
        if not group.positions:
            raise ValueError("empty group " + name)
        count = group.vertices
        out.append('    <geometry id="{0}-mesh" name="{0}"><mesh>'.format(name))
        out.append('      <source id="{0}-positions"><float_array id="{0}-positions-array" count="{1}">{2}</float_array>'
                   '<technique_common><accessor source="#{0}-positions-array" count="{3}" stride="3">'
                   '<param name="X" type="float"/><param name="Y" type="float"/><param name="Z" type="float"/>'
                   '</accessor></technique_common></source>'.format(
                       name, len(group.positions), " ".join(fmt(v) for v in group.positions), count))
        out.append('      <source id="{0}-normals"><float_array id="{0}-normals-array" count="{1}">{2}</float_array>'
                   '<technique_common><accessor source="#{0}-normals-array" count="{3}" stride="3">'
                   '<param name="X" type="float"/><param name="Y" type="float"/><param name="Z" type="float"/>'
                   '</accessor></technique_common></source>'.format(
                       name, len(group.normals), " ".join(fmt(v) for v in group.normals), count))
        out.append('      <source id="{0}-uv"><float_array id="{0}-uv-array" count="{1}">{2}</float_array>'
                   '<technique_common><accessor source="#{0}-uv-array" count="{3}" stride="2">'
                   '<param name="S" type="float"/><param name="T" type="float"/>'
                   '</accessor></technique_common></source>'.format(
                       name, len(group.uvs), " ".join(fmt(v) for v in group.uvs), count))
        out.append('      <vertices id="{0}-vertices"><input semantic="POSITION" source="#{0}-positions"/></vertices>'.format(name))
        # count is the triangle count, the <p> stream holds three interleaved indices per
        # corner. Both used to be formatted with the wrong argument, which wrote the mesh
        # name into count and made the target importer reject the file.
        out.append('      <triangles material="FortressMaterial-symbol" count="{1}">'
                   '<input semantic="VERTEX" source="#{0}-vertices" offset="0"/>'
                   '<input semantic="NORMAL" source="#{0}-normals" offset="1"/>'
                   '<input semantic="TEXCOORD" source="#{0}-uv" offset="2" set="0"/>'
                   '<p>{2}</p></triangles>'.format(name, group.quads, " ".join(str(v) for v in group.indices)))
        out.append('    </mesh></geometry>')
        total_tris += group.quads

    out.append('  </library_geometries>')
    out.append('  <library_visual_scenes><visual_scene id="Scene" name="Scene">'
               '<node id="PortableFortress" name="PortableFortress" type="NODE">'
               '<matrix sid="transform">1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1</matrix>')
    for name in order:
        out.append('    <instance_geometry url="#{0}-mesh" name="{0}"><bind_material><technique_common>'
                   '<instance_material symbol="FortressMaterial-symbol" target="#FortressMaterial"/>'
                   '</technique_common></bind_material></instance_geometry>'.format(name))
    out.append('  </node></visual_scene></library_visual_scenes>'
               '<scene><instance_visual_scene url="#Scene"/></scene></COLLADA>')

    with open(path, "w", newline="\n") as handle:
        handle.write("\n".join(out))
    return total_tris, len(PARTS)


def fmt(value):
    text = "%.6f" % value
    text = text.rstrip("0").rstrip(".")
    return "0" if text in ("-0", "") else text


# ---------------------------------------------------------------------------
# Control core (deploy item + placed core)
# ---------------------------------------------------------------------------
CORE_PARTS = [
    ("CoreBase", (-0.34, 0.00, -0.34), (0.34, 0.10, 0.34), 0),
    ("CoreBody", (-0.24, 0.10, -0.24), (0.24, 0.40, 0.24), 1),
    ("CoreCollar", (-0.30, 0.40, -0.30), (0.30, 0.46, 0.30), 2),
    ("CorePrism", (-0.16, 0.46, -0.16), (0.16, 0.62, 0.16), 3),
    ("CoreLensN", (-0.10, 0.20, -0.27), (0.10, 0.34, -0.245), 5),
    ("CoreLensS", (-0.10, 0.20, 0.245), (0.10, 0.34, 0.27), 5),
    ("CoreLensE", (0.245, 0.20, -0.10), (0.27, 0.34, 0.10), 5),
    ("CoreLensW", (-0.27, 0.20, -0.10), (-0.245, 0.34, 0.10), 5),
    ("CoreFinNE", (0.20, 0.10, 0.20), (0.42, 0.44, 0.42), 0),
    ("CoreFinNW", (-0.42, 0.10, 0.20), (-0.20, 0.44, 0.42), 0),
    ("CoreFinSE", (0.20, 0.10, -0.42), (0.42, 0.44, -0.20), 0),
    ("CoreFinSW", (-0.42, 0.10, -0.42), (-0.20, 0.44, -0.20), 0),
    ("CoreFoot", (-0.40, 0.00, -0.40), (-0.30, 0.06, -0.30), 4),
    ("CoreFoot2", (0.30, 0.00, 0.30), (0.40, 0.06, 0.40), 4),
]

CORE_COLORS = [
    (44, 48, 54),     # 0 armour
    (92, 98, 106),    # 1 trim
    (196, 138, 54),   # 2 bronze
    (236, 176, 72),   # 3 core glow
    (24, 26, 30),     # 4 shadow
    (120, 168, 196),  # 5 lens
]


def write_core(path):
    geos = []
    inst = []
    for name, lo, hi, slot in CORE_PARTS:
        u = (slot + 0.5) / len(CORE_COLORS)
        x0, y0, z0 = lo
        x1, y1, z1 = hi
        geos.append(
            '    <geometry id="{n}-mesh" name="{n}"><mesh>\n'
            '      <source id="{n}-positions"><float_array id="{n}-positions-array" count="24">{x0} {y0} {z0} {x1} {y0} {z0} {x1} {y1} {z0} {x0} {y1} {z0} {x0} {y0} {z1} {x1} {y0} {z1} {x1} {y1} {z1} {x0} {y1} {z1}</float_array>'
            '<technique_common><accessor source="#{n}-positions-array" count="8" stride="3">'
            '<param name="X" type="float"/><param name="Y" type="float"/><param name="Z" type="float"/>'
            '</accessor></technique_common></source>\n'
            '      <source id="{n}-normals"><float_array id="{n}-normals-array" count="18">0 0 -1 0 0 1 -1 0 0 1 0 0 0 -1 0 0 1 0</float_array>'
            '<technique_common><accessor source="#{n}-normals-array" count="6" stride="3">'
            '<param name="X" type="float"/><param name="Y" type="float"/><param name="Z" type="float"/>'
            '</accessor></technique_common></source>\n'
            '      <source id="{n}-uv"><float_array id="{n}-uv-array" count="2">{u} 0.5</float_array>'
            '<technique_common><accessor source="#{n}-uv-array" count="1" stride="2">'
            '<param name="S" type="float"/><param name="T" type="float"/>'
            '</accessor></technique_common></source>\n'
            '      <vertices id="{n}-vertices"><input semantic="POSITION" source="#{n}-positions"/></vertices>\n'
            '      <triangles count="12"><input semantic="VERTEX" source="#{n}-vertices" offset="0"/>'
            '<input semantic="NORMAL" source="#{n}-normals" offset="1"/>'
            '<input semantic="TEXCOORD" source="#{n}-uv" offset="2" set="0"/>'
            '<p>0 0 0 1 0 0 2 0 0 0 0 0 2 0 0 3 0 0 '
            '4 1 0 6 1 0 5 1 0 4 1 0 7 1 0 6 1 0 '
            '0 2 0 3 2 0 7 2 0 0 2 0 7 2 0 4 2 0 '
            '1 3 0 5 3 0 6 3 0 1 3 0 6 3 0 2 3 0 '
            '0 4 0 4 4 0 5 4 0 0 4 0 5 4 0 1 4 0 '
            '3 5 0 2 5 0 6 5 0 3 5 0 6 5 0 7 5 0</p></triangles>\n'
            '    </mesh></geometry>'.format(n=name, u=fmt(u), x0=fmt(x0), y0=fmt(y0), z0=fmt(z0),
                                           x1=fmt(x1), y1=fmt(y1), z1=fmt(z1)))
        inst.append('    <instance_geometry url="#{0}-mesh" name="{0}"/>'.format(name))

    dae = "\n".join([
        '<?xml version="1.0" encoding="utf-8"?>',
        '<!-- SCIE-Port portable fortress control core; baked Y_UP, one render bone. -->',
        '<COLLADA xmlns="http://www.collada.org/2005/11/COLLADASchema" version="1.4.1">',
        '  <asset><contributor><author>SCIE-Port contributors</author>'
        '<authoring_tool>New-PortableFortressArt.py</authoring_tool></contributor>'
        '<unit name="meter" meter="1"/><up_axis>Y_UP</up_axis></asset>',
        '  <library_geometries>',
        "\n".join(geos),
        '  </library_geometries>',
        '  <library_visual_scenes><visual_scene id="Scene" name="Scene">'
        '<node id="FortressCore" name="FortressCore" type="NODE">'
        '<matrix sid="transform">1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1</matrix>',
        "\n".join(inst),
        '  </node></visual_scene></library_visual_scenes>',
        '  <scene><instance_visual_scene url="#Scene"/></scene>',
        '</COLLADA>',
    ])
    with open(path, "w", newline="\n") as handle:
        handle.write(dae)


def write_core_texture(path):
    img = Image.new("RGBA", (48, 8), (0, 0, 0, 255))
    for i, color in enumerate(CORE_COLORS):
        for x in range(i * 8, (i + 1) * 8):
            for y in range(8):
                img.putpixel((x, y), (color[0], color[1], color[2], 255))
    img.save(path, "PNG")


# ---------------------------------------------------------------------------
# Previews (software renderer; not a game screenshot)
# ---------------------------------------------------------------------------
LIGHT = (0.38, 0.82, 0.42)


def normalize(v):
    length = math.sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2])
    if length < 1e-9:
        return (0.0, 1.0, 0.0)
    return (v[0] / length, v[1] / length, v[2] / length)


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def render_view(path, title, eye, target, size=(1100, 780), fov_scale=1.15, subtitle=""):
    width, height = size
    img = Image.new("RGB", (width, height), (18, 22, 28))
    draw = ImageDraw.Draw(img)

    forward = normalize(sub(target, eye))
    right = normalize(cross(forward, (0.0, 1.0, 0.0)))
    up = cross(right, forward)
    light = normalize(LIGHT)
    focal = fov_scale * width

    ordered = []
    for region, points, normal in QUADS:
        view = []
        for point in points:
            delta = sub(point, eye)
            view.append((dot(delta, right), dot(delta, up), dot(delta, forward)))
        if view[0][2] < 0.25 or view[1][2] < 0.25 or view[2][2] < 0.25 or view[3][2] < 0.25:
            continue
        # Back face culling keeps closed volumes readable and removes the near wall
        # from interior shots.
        facing = dot(normal, sub(points[0], eye))
        if facing > 0:
            continue
        projected = [(width / 2 + focal * v[0] / v[2], height / 2 - focal * v[1] / v[2]) for v in view]
        # Nearest-vertex depth: with a coplanar stack (floor over its plinth) the
        # average depth of both quads is nearly identical and the painter order
        # flips, which showed the darker plinth through the floor.
        depth = min(v[2] for v in view)
        ordered.append((depth, region, projected, normal))

    ordered.sort(key=lambda item: -item[0])

    for depth, region, projected, normal in ordered:
        base = PALETTE[region]
        if normal[1] < -0.5:
            shade = 0.70          # floor bounce, otherwise undersides read as holes
        else:
            shade = 0.58 + 0.42 * max(0.0, dot(normal, light))
        color = (clamp8(base[0] * shade), clamp8(base[1] * shade), clamp8(base[2] * shade))
        if region == "fire":
            color = (clamp8(base[0] * 1.20), clamp8(base[1] * 1.05), clamp8(base[2] * 0.72))
        draw.polygon([(p[0], p[1]) for p in projected], fill=color)

    try:
        font = ImageFont.truetype("arial.ttf", 22)
        small = ImageFont.truetype("arial.ttf", 15)
    except Exception:
        font = ImageFont.load_default()
        small = font
    draw.text((26, 18), title, font=font, fill=(238, 236, 230))
    if subtitle:
        draw.text((26, 48), subtitle, font=small, fill=(168, 176, 182))
    draw.text((26, height - 30), "Generated preview from the model generator - not a game screenshot.",
              font=small, fill=(140, 148, 154))
    img.save(path, "PNG")


PREVIEWS = [
    ("front", "FRONT ELEVATION (panoramic nose)", (0.0, 4.9, -30.0), (0.0, 4.3, 0.0), ""),
    ("side", "EAST SIDE ELEVATION", (30.0, 4.9, 0.0), (0.0, 4.3, 0.0), ""),
    ("rear", "REAR ELEVATION (main entrance)", (0.0, 4.9, 30.0), (0.0, 4.3, 0.0), ""),
    ("entrance", "ENTRANCE CLOSE-UP", (0.6, 2.2, 11.0), (0.0, 1.5, 6.1), ""),
    ("interior-lower", "INTERIOR - LEVEL 1 (living space)", (3.60, 2.75, 3.70), (-3.40, 1.15, 0.30), ""),
    ("interior-upper", "INTERIOR - LEVEL 2 (observation deck)", (3.30, 5.30, 4.40), (-1.00, 5.10, -5.80), ""),
    ("roof", "ROOF DECK", (12.0, 12.5, 12.0), (0.0, 7.3, 0.0), ""),
]


# ---------------------------------------------------------------------------
def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    models = os.path.join(root, "src", "SCIEPort", "Assets", "Models")
    textures = os.path.join(root, "src", "SCIEPort", "Assets", "Textures")
    previews = os.path.join(root, "docs", "portable-fortress", "previews")
    for path in (models, textures, previews):
        if not os.path.isdir(path):
            os.makedirs(path)

    paint_atlas(os.path.join(textures, "PortableFortress.png"))
    paint_glass(os.path.join(textures, "FortressGlass.png"))
    paint_glow(os.path.join(textures, "FortressGlow.png"))
    write_core_texture(os.path.join(textures, "FortressCore.png"))

    groups = build_parts()
    tris, parts = write_dae(groups, os.path.join(models, "PortableFortress.dae"))
    write_core(os.path.join(models, "FortressCore.dae"))

    for name, title, eye, target, subtitle in PREVIEWS:
        render_view(os.path.join(previews, name + ".png"), title, eye, target, subtitle=subtitle)

    print("Generated Portable Fortress")
    print("  parts      : %d" % parts)
    print("  geometries : %d" % len(GROUPS))
    print("  triangles  : %d" % tris)
    print("  previews   : %s" % previews)
    print("  atlas      : 1024 x 1024, %d regions" % len(REGIONS))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
