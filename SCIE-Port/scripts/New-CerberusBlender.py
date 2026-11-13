"""Author the original classical Cerberus model, texture, rig and previews.

Run headless:  blender.exe -b -P scripts/New-CerberusBlender.py

Everything here is procedurally authored from measurements in this file. No game
meshes, APK assets, DLLs or third-party artwork are read. Game space is Y-up
(X right, Y up, Z back) and maps to Blender space as (x, -z, y).

24 rigid bones, one closed manifold mesh per bone, every exported triangle has
three independent vertices. Palette atlas is 1024x512 with 4x4 tiles of 256x128.
"""
from pathlib import Path
import hashlib
import json
import math
import struct
import zlib

import bpy
import bmesh
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
DESIGN = ROOT / 'design/cerberus'
VALID = DESIGN / 'validation'
ASSETS = ROOT / 'src/SCIEPort/Assets'
MODEL = ASSETS / 'Models/AbyssCerberus.dae'
TEXTURE = ASSETS / 'Textures/AbyssCerberus.png'
BLEND = DESIGN / 'AbyssCerberus.blend'
SCENE = 'AbyssCerberus'
TRI_BUDGET = 19000
VERT_BUDGET = 65535

TAU = math.tau


# ---------------------------------------------------------------- vector maths
def vadd(a, b):
    return (a[0] + b[0], a[1] + b[1], a[2] + b[2])


def vsub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def vmul(a, s):
    return (a[0] * s, a[1] * s, a[2] * s)


def vdot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def vcross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def vlen(a):
    return math.sqrt(vdot(a, a))


def vnorm(a):
    size = vlen(a)
    return (a[0] / size, a[1] / size, a[2] / size) if size > 1e-12 else (0.0, 1.0, 0.0)


def vlerp(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t)


def rotate_axis(p, axis, angle):
    axis = vnorm(axis)
    c, s = math.cos(angle), math.sin(angle)
    return vadd(vadd(vmul(p, c), vmul(vcross(axis, p), s)), vmul(axis, vdot(axis, p) * (1 - c)))


def rotate_y(p, angle):
    """Rotation about game up-axis, used for the spread side heads."""
    c, s = math.cos(angle), math.sin(angle)
    return (p[0] * c + p[2] * s, p[1], -p[0] * s + p[2] * c)


def xyz(p):
    """Game (x, y, z) -> Blender (x, -z, y)."""
    return (p[0], -p[2], p[1])


def game(p):
    """Blender (x, y, z) -> game (x, z, -y)."""
    return (p[0], p[2], -p[1])


# ----------------------------------------------------------------- primitives
def frames(points):
    """Parallel-transported frames so curved limbs never twist or pinch."""
    tangents = []
    for i, point in enumerate(points):
        if i == 0:
            tangent = vsub(points[1], points[0])
        elif i == len(points) - 1:
            tangent = vsub(points[-1], points[-2])
        else:
            tangent = vsub(points[i + 1], points[i - 1])
        tangents.append(vnorm(tangent))
    ref = (0.0, 1.0, 0.0) if abs(tangents[0][1]) < 0.85 else (1.0, 0.0, 0.0)
    u = vnorm(vcross(tangents[0], ref))
    out = []
    for i, tangent in enumerate(tangents):
        if i:
            axis = vcross(tangents[i - 1], tangent)
            size = vlen(axis)
            if size > 1e-9:
                u = rotate_axis(u, axis, math.atan2(size, vdot(tangents[i - 1], tangent)))
        u = vnorm(vsub(u, vmul(tangent, vdot(u, tangent))))
        out.append((tangent, u, vcross(tangent, u)))
    return out


def tube(points, radii, sides, caps=True, cyclic=False):
    """Closed swept tube. radii is a list of (ru, rv) per point."""
    frames_ = frames(points)
    verts = []
    for point, radius, (_, u, v) in zip(points, radii, frames_):
        ru, rv = radius if isinstance(radius, (tuple, list)) else (radius, radius)
        for k in range(sides):
            angle = TAU * k / sides
            verts.append(vadd(point, vadd(vmul(u, ru * math.cos(angle)), vmul(v, rv * math.sin(angle)))))
    faces = []
    if caps and not cyclic:
        faces.append(list(range(sides - 1, -1, -1)))
        faces.append(list(range((len(points) - 1) * sides, len(points) * sides)))
    rings = len(points) if cyclic else len(points) - 1
    for ring in range(rings):
        nxt = (ring + 1) % len(points)
        for k in range(sides):
            a = ring * sides + k
            b = ring * sides + (k + 1) % sides
            faces.append([a, b, nxt * sides + (k + 1) % sides, nxt * sides + k])
    return verts, faces


def ellipsoid(center, radii, sides=10, bands=6, squash=None):
    """Closed lat/long ellipsoid; squash scales the low latitudes for muscle bellies."""
    verts = []
    for j in range(1, bands):
        phi = -math.pi / 2 + j * math.pi / bands
        scale = 1.0 if squash is None else squash(phi)
        for k in range(sides):
            a = TAU * k / sides
            verts.append((center[0] + radii[0] * scale * math.cos(phi) * math.cos(a),
                          center[1] + radii[1] * math.sin(phi),
                          center[2] + radii[2] * scale * math.cos(phi) * math.sin(a)))
    faces = [list(range(sides - 1, -1, -1)), list(range((bands - 2) * sides, (bands - 1) * sides))]
    for ring in range(bands - 2):
        for k in range(sides):
            a = ring * sides + k
            b = ring * sides + (k + 1) % sides
            faces.append([a, b, b + sides, a + sides])
    return verts, faces


def box(center, size, bevel=0.18, yaw=0.0, pitch=0.0):
    """Beveled slab, 60 triangles, same construction as the existing pipeline."""
    x, y, z = (s / 2 for s in size)
    d = min(x, z) * bevel
    outline = [(-x + d, -z), (x - d, -z), (x, -z + d), (x, z - d),
               (x - d, z), (-x + d, z), (-x, z - d), (-x, -z + d)]
    verts = []
    for yy, scale in [(-y, .86), (-y + min(y * .32, .06), 1), (y - min(y * .32, .06), 1), (y, .80)]:
        for xx, zz in outline:
            xx *= scale
            zz *= scale
            px, pz = xx, zz
            if pitch:
                py = -pz * math.sin(pitch)
                pz = pz * math.cos(pitch)
            else:
                py = 0.0
            verts.append((center[0] + px * math.cos(yaw) + pz * math.sin(yaw),
                          center[1] + yy + py,
                          center[2] - px * math.sin(yaw) + pz * math.cos(yaw)))
    faces = [list(range(7, -1, -1)), list(range(24, 32))]
    for ring in range(3):
        for k in range(8):
            faces.append([ring * 8 + k, ring * 8 + (k + 1) % 8,
                          (ring + 1) * 8 + (k + 1) % 8, (ring + 1) * 8 + k])
    return verts, faces


def cone(base, tip, radius, sides=6, tip_radius=0.0):
    axis = vsub(tip, base)
    unit = vnorm(axis)
    ref = (0.0, 0.0, 1.0) if abs(unit[2]) < 0.9 else (1.0, 0.0, 0.0)
    u = vnorm(vcross(unit, ref))
    v = vcross(unit, u)
    verts = []
    for point, rad in ((base, radius), (tip, tip_radius)):
        if rad <= 1e-6:
            verts.append(point)
        else:
            for k in range(sides):
                a = TAU * k / sides
                verts.append(vadd(point, vadd(vmul(u, rad * math.cos(a)), vmul(v, rad * math.sin(a)))))
    if tip_radius <= 1e-6:
        faces = [list(range(sides - 1, -1, -1))] + [[k, (k + 1) % sides, sides] for k in range(sides)]
    else:
        faces = [list(range(sides - 1, -1, -1)), list(range(sides, 2 * sides))]
        for k in range(sides):
            faces.append([k, (k + 1) % sides, sides + (k + 1) % sides, sides + k])
    return verts, faces


def octa(center, axes, halves):
    """Eight triangle scale/spike plate; cheap and always closed."""
    u, v, w = (vnorm(a) for a in axes)
    a, b, c = halves
    verts = [vadd(center, vmul(u, a)), vadd(center, vmul(u, -a)),
             vadd(center, vmul(v, b)), vadd(center, vmul(v, -b)),
             vadd(center, vmul(w, c)), vadd(center, vmul(w, -c))]
    faces = [[4, 0, 2], [4, 2, 1], [4, 1, 3], [4, 3, 0],
             [5, 2, 0], [5, 1, 2], [5, 3, 1], [5, 0, 3]]
    return verts, faces


def fin(a, b, tip, normal, thick=0.03):
    """Solid triangular crest/belly plate, 8 triangles."""
    n = vmul(vnorm(normal), thick / 2)
    verts = [vadd(a, n), vadd(b, n), vadd(tip, n), vadd(a, vmul(n, -1)),
             vadd(b, vmul(n, -1)), vadd(tip, vmul(n, -1))]
    faces = [[0, 1, 2], [5, 4, 3], [0, 3, 4, 1], [1, 4, 5, 2], [2, 5, 3, 0]]
    return verts, faces


# ------------------------------------------------------------------ anatomy map
HEAD_C = (0.0, 2.66, -1.80)
SIDE_SCALE = 0.90
HEAD_YAW = 0.26
SNAKE_C = (0.0, 2.72, -0.94)
TAIL_HEAD_C = (0.0, 1.82, 2.34)


def head_center(side):
    return HEAD_C if side == 0 else (side * 1.30, 2.44, -1.62)


def head_scale(side):
    return 1.0 if side == 0 else SIDE_SCALE


def head_yaw(side):
    return 0.0 if side == 0 else -side * HEAD_YAW


def hinge(center, scale, yaw, lift=-0.12, back=0.02):
    return vadd(center, rotate_y((0.0, lift * scale, back * scale), yaw))


BONE_DEFS = [
    ('Body', None, (0.0, 1.42, 0.15)),
    ('Chest', 'Body', (0.0, 1.52, -1.02)),
    ('NeckC', 'Body', (0.0, 1.84, -1.20)),
    ('NeckL', 'Body', (-0.66, 1.74, -1.06)),
    ('NeckR', 'Body', (0.66, 1.74, -1.06)),
    ('Head', 'NeckC', HEAD_C),
    ('HeadL', 'NeckL', head_center(-1)),
    ('HeadR', 'NeckR', head_center(1)),
    ('Jaw', 'Head', hinge(HEAD_C, 1.0, 0.0)),
    ('JawL', 'HeadL', hinge(head_center(-1), SIDE_SCALE, head_yaw(-1))),
    ('JawR', 'HeadR', hinge(head_center(1), SIDE_SCALE, head_yaw(1))),
    ('Leg1', 'Body', (-0.60, 1.24, -0.78)),
    ('Leg2', 'Body', (0.60, 1.24, -0.78)),
    ('Leg3', 'Body', (-0.56, 1.22, 0.98)),
    ('Leg4', 'Body', (0.56, 1.22, 0.98)),
    ('TailA', 'Body', (0.0, 1.36, 1.34)),
    ('TailB', 'TailA', (0.0, 1.25, 1.82)),
    ('TailC', 'TailB', (0.0, 1.50, 2.16)),
    ('TailHead', 'TailC', (0.0, 1.76, 2.30)),
    ('TailJaw', 'TailHead', hinge(TAIL_HEAD_C, 0.58, math.pi)),
    ('SnakeC', 'Body', (0.0, 2.02, -0.06)),
    ('SnakeL', 'Body', (-0.54, 2.02, 0.10)),
    ('SnakeR', 'Body', (0.54, 2.02, 0.10)),
    ('SnakeJaw', 'SnakeC', hinge(SNAKE_C, 0.62, 0.0)),
]
BONE_NAMES = [b[0] for b in BONE_DEFS]
BONES = {name: (parent, pivot) for name, parent, pivot in BONE_DEFS}
PARTS = {name: [] for name in BONE_NAMES}


def part(bone, name, material, verts, faces):
    PARTS[bone].append({'name': name, 'material': material, 'verts': verts, 'faces': faces})


def add_tube(bone, name, material, points, radii, sides, cyclic=False):
    verts, faces = tube(points, radii, sides, cyclic=cyclic)
    part(bone, name, material, verts, faces)


def add_box(bone, name, material, center, size, bevel=0.2, yaw=0.0):
    verts, faces = box(center, size, bevel, yaw)
    part(bone, name, material, verts, faces)


# -------------------------------------------------------------------- torso
TORSO = [
    (-1.66, 1.50, .24, .22), (-1.60, 1.48, .40, .36), (-1.46, 1.50, .60, .54),
    (-1.18, 1.52, .82, .68), (-0.86, 1.54, .91, .73), (-0.48, 1.52, .86, .70),
    (-0.06, 1.47, .75, .62), (0.40, 1.50, .65, .56), (0.86, 1.56, .70, .60),
    (1.20, 1.59, .73, .62), (1.50, 1.56, .56, .50), (1.60, 1.60, .40, .36),
    (1.66, 1.62, .24, .22),
]


def torso_surface(z, angle, inset=0.05):
    """Point on the torso wall at height z and ellipse angle, slightly sunk in."""
    for (z0, y0, u0, v0), (z1, y1, u1, v1) in zip(TORSO, TORSO[1:]):
        if z0 <= z <= z1:
            t = (z - z0) / (z1 - z0)
            cy = y0 + (y1 - y0) * t
            ru = (u0 + (u1 - u0) * t) * (1 - inset)
            rv = (v0 + (v1 - v0) * t) * (1 - inset)
            return (ru * math.cos(angle), cy + rv * math.sin(angle), z)
    z0, y0, u0, v0 = TORSO[0] if z < TORSO[0][0] else TORSO[-1]
    return (u0 * (1 - inset) * math.cos(angle), y0 + v0 * (1 - inset) * math.sin(angle), z)


add_tube('Body', 'Torso', 0, [(0.0, y, z) for z, y, _, _ in TORSO],
         [(ru, rv) for _, _, ru, rv in TORSO], 16)
RIB_ANGLES = (1.22, 1.00, 0.66, 0.22, -0.38, -0.98)
RIB_SHIFT = (0.00, -0.02, 0.00, 0.03, 0.07, 0.10)


def add_rib(bone, z0):
    """Rib band traced on the torso wall so it never floats off the flank."""
    for side in (-1, 1):
        points = []
        for angle, shift in zip(RIB_ANGLES, RIB_SHIFT):
            x, y, z = torso_surface(z0 + shift, angle)
            points.append((side * x, y, z))
        add_tube(bone, 'Rib', 0, points, [.052, .058, .055, .050, .045, .040], 6)


for side in (-1, 1):
    part('Body', 'ShoulderHump', 1, *ellipsoid((side * .50, 1.87, -.74), (.40, .29, .47), 10, 5))
    part('Body', 'HaunchMuscle', 0, *ellipsoid((side * .45, 1.68, 1.04), (.40, .34, .46), 10, 5))
for i in range(4):
    add_rib('Body', -0.30 + i * .30)
add_tube('Body', 'SpinalRidge', 1, [(0.0, 2.02 - abs(z) * .07, z) for z in
                                    (-0.34, -0.10, 0.16, 0.44, 0.74, 1.04, 1.34, 1.58)],
         [.055] * 8, 8)
for i in range(7):
    z = -0.44 + i * .31
    part('Body', 'BackSpur', 15, *octa((0.0, 2.10 - abs(z) * .05, z), ((0, 1, 0), (0, 0, 1), (1, 0, 0)), (.04, .075, .028)))
for side in (-1, 1):
    add_tube('Body', 'Scapula', 1, [(0.0, 1.84, -1.02), (side * .34, 1.74, -1.10), (side * .66, 1.32, -.84)],
             [.11, .15, .10], 6)
    add_tube('Body', 'PelvisArch', 1, [(0.0, 1.76, 1.10), (side * .46, 1.66, 1.24), (side * .62, 1.30, 1.04)],
             [.13, .16, .11], 6)
    for i in range(5):
        add_box('Body', 'BellyScute', 6, (0.0, 1.00 + abs(i - 2) * .02, -.42 + i * .34),
                (.52 - abs(i - 2) * .07, .09, .26), .30)
for i in range(6):
    part('Body', 'DorsalScale', 4, *octa((0.0, 2.06 - i * .012, -0.28 + i * .30),
                                         ((0, 1, 0), (0, 0, 1), (1, 0, 0)), (.05, .09, .04)))

# -------------------------------------------------------------------- chest
add_tube('Chest', 'Sternum', 2, [(0.0, 1.30, -1.44), (0.0, 1.22, -1.00), (0.0, 1.20, -0.52)],
         [(.10, .09), (.11, .10), (.08, .07)], 8)
for i in range(4):
    add_rib('Chest', -1.30 + i * .28)
add_box('Chest', 'SternumPlate', 15, (0.0, 1.40, -1.40), (.44, .11, .22), .30)
add_box('Chest', 'ThroatPlate', 0, (0.0, 1.27, -1.42), (.30, .14, .14), .30)
for x in (-.66, 0.0, .66):
    y = 1.80 if x == 0 else 1.70
    part('Chest', 'NeckRootBoss', 0, *ellipsoid((x, y, -1.16), (.28, .32, .34), 8, 4))


# --------------------------------------------------------------------- heads
def build_head(bone, jaw_bone, center, yaw, scale, eye_material, ears=True, snake=False):
    sc = scale

    def P(x, y, z):
        return vadd(center, rotate_y((x * sc, y * sc, z * sc), yaw))

    def t(name, material, points, radii, sides, cyclic=False):
        add_tube(bone, name, material, [P(*p) for p in points],
                 [(a * sc, b * sc) for a, b in radii], sides, cyclic)

    def b(name, material, at, size, bevel=0.22):
        add_box(bone, name, material, P(*at), (size[0] * sc, size[1] * sc, size[2] * sc), bevel, yaw)

    cs, ms = (8, 6) if snake else (12, 10)
    t('Cranium', 0, [(0, .02, .20), (0, .04, .02), (0, .03, -.16), (0, .01, -.34), (0, -.01, -.50)],
      [(.19, .17), (.27, .23), (.29, .22), (.26, .19), (.20, .15)], cs)
    b('ForeheadPlate', 1, (0, .16, -.30), (.44, .12, .34))
    for side in (-1, 1):
        t('BrowRidge', 1, [(side * .08, .20, -.44), (side * .20, .23, -.36), (side * .30, .15, -.24)],
          [(.05, .05), (.06, .055), (.045, .045)], 6)
        t('CheekArch', 1, [(side * .24, .02, -.06), (side * .34, -.01, -.28), (side * .30, -.06, -.48)],
          [(.075, .07), (.065, .06), (.05, .05)], 6)
        ring = [(side * .245, .10 + .085 * math.cos(k * TAU / 6), -.36 + .105 * math.sin(k * TAU / 6))
                for k in range(6)]
        t('EyeSocket', 4, ring, [(.028, .028)] * 6, 4, cyclic=True)
        part(bone, 'Eye', eye_material,
             *ellipsoid(P(side * .25, .10, -.36), (.045 * sc, .075 * sc, .085 * sc), 6, 3))
        if not snake:
            b('SnoutPlate', 1, (side * .19, -.02, -.60), (.09, .15, .32), .25)
            b('Jowl', 1, (side * .26, -.09, -.22), (.16, .24, .34), .30)
            part(bone, 'TempleMuscle', 0,
                 *ellipsoid(P(side * .22, .12, -.10), (.09 * sc, .14 * sc, .17 * sc), 8, 3))
        if ears:
            t('Ear', 1, [(side * .15, .27, -.04), (side * .21, .40, .04), (side * .25, .51, .11)],
              [(.085, .07), (.05, .045), (.012, .012)], 6)
            part(bone, 'EarInner', 4, *octa(P(side * .19, .38, .02),
                                            ((1, 0, 0), (0, 1, 0), (0, 0, 1)), (.03 * sc, .05 * sc, .04 * sc)))
    t('Muzzle', 1, [(0, .02, -.40), (0, 0, -.58), (0, -.01, -.74), (0, -.02, -.86)],
      [(.235, .18), (.21, .155), (.175, .13), (.145, .105)], ms)
    b('NosePad', 4, (0, .06, -.91), (.24, .10, .12), .28)
    for side in (-1, 1):
        part(bone, 'Nostril', 4, *octa(P(side * .09, .10, -.90),
                                       ((1, 0, 0), (0, 1, 0), (0, 0, 1)), (.025 * sc, .03 * sc, .04 * sc)))
    for side in (-1, 1):
        for i in range(3):
            base = P(side * (.17 - .015 * i), .00, -.52 - .13 * i)
            tip = P(side * (.16 - .015 * i), -.14, -.53 - .13 * i)
            verts, faces = cone(base, tip, .035 * sc, 5)
            part(bone, 'UpperFang', 11, verts, faces)
    for i, x in enumerate((-.10, 0.0, .10)):
        verts, faces = fin(P(x, .26, .14), P(x, .26, -.04), P(x, .44, -.02), (1, 0, 0), .035 * sc)
        part(bone, 'OccipitalCrest', 1, verts, faces)
    if not snake:
        for side in (-1, 1):
            t('Lip', 1, [(side * .12, -.10, -.78), (side * .19, -.10, -.52), (side * .22, -.08, -.30)],
              [(.035, .03)] * 3, 5)
        t('Palate', 10, [(0, -.09, -.28), (0, -.10, -.54), (0, -.11, -.78)], [(.13, .045)] * 3, 8)
        for i, x in enumerate((-.10, 0.0, .10)):
            part(bone, 'CrownScale', 3, *octa(P(x, .35, -.22),
                                              ((0, 1, 0), (1, 0, 0), (0, 0, 1)), (.045 * sc, .07 * sc, .08 * sc)))
    if snake:
        for side in (-1, 1):
            verts, faces = fin(P(side * .20, .12, .06), P(side * .20, .12, -.14),
                               P(side * .36, .30, -.06), (1, 0, 0), .03 * sc)
            part(bone, 'HoodFin', 5, verts, faces)
        for i, z in enumerate((-.10, -.30, -.50)):
            part(bone, 'SerpentCrest', 4, *octa(P(0, .30 - i * .02, z),
                                                ((0, 1, 0), (0, 0, 1), (1, 0, 0)),
                                                (.04 * sc, .07 * sc, .035 * sc)))


def build_jaw(bone, origin, yaw, scale, teeth=4, venom=True):
    sc = scale

    def P(x, y, z):
        return vadd(origin, rotate_y((x * sc, y * sc, z * sc), yaw))

    add_tube(bone, 'Mandible', 1, [P(0, .01, .04), P(0, -.02, -.16), P(0, -.03, -.34),
                                   P(0, -.02, -.50), P(0, .00, -.62)],
             [(.19 * sc, .075 * sc), (.17 * sc, .07 * sc), (.15 * sc, .065 * sc),
              (.13 * sc, .055 * sc), (.10 * sc, .045 * sc)], 10)
    for side in (-1, 1):
        add_box(bone, 'RamusPlate', 1, P(side * .16, .03, .05), (.10 * sc, .12 * sc, .16 * sc), .25, yaw)
    add_box(bone, 'Chin', 1, P(0, .02, -.62), (.18 * sc, .08 * sc, .10 * sc), .30, yaw)
    add_tube(bone, 'GumRidge', 10, [P(0, -.01, -.12), P(0, -.02, -.38), P(0, -.02, -.58)],
             [(.13 * sc, .035 * sc)] * 3, 6)
    for side in (-1, 1):
        for i in range(teeth):
            base = P(side * (.115 - .012 * i), .00, -.16 - .13 * i)
            tip = P(side * (.115 - .012 * i), .10, -.17 - .13 * i)
            verts, faces = cone(base, tip, .026 * sc, 5)
            part(bone, 'LowerFang', 11, verts, faces)
    add_tube(bone, 'Tongue', 12, [P(0, .01, -.06), P(0, .02, -.24), P(0, .015, -.42)],
             [(.06 * sc, .026 * sc)] * 3, 6)
    if venom:
        for side in (-1, 1):
            for i in range(2):
                base = P(side * (.10 - .02 * i), .00, -.22 - .18 * i)
                tip = P(side * (.11 - .02 * i), -.09 - .02 * i, -.23 - .18 * i)
                verts, faces = cone(base, tip, .010 * sc, 5, .003 * sc)
                part(bone, 'VenomDrip', 13, verts, faces)
    add_box(bone, 'JawUnderplate', 1, P(0, -.06, -.30), (.26 * sc, .06 * sc, .44 * sc), .25, yaw)


# --------------------------------------------------------------------- necks
def build_neck(bone, side):
    if side == 0:
        points = [(0, 1.84, -1.14), (0, 2.04, -1.26), (0, 2.24, -1.38), (0, 2.46, -1.52), (0, 2.62, -1.70)]
    else:
        points = [(side * .66, 1.74, -1.02), (side * .86, 1.94, -1.14), (side * 1.06, 2.16, -1.30),
                  (side * 1.24, 2.34, -1.46), (side * 1.29, 2.42, -1.54)]
    add_tube(bone, 'Neck', 0, points,
             [(.24, .22), (.23, .21), (.21, .20), (.20, .19), (.22, .20)], 10)
    add_tube(bone, 'Throat', 1, [vadd(p, (0, -.14, -.02)) for p in points[1:4]],
             [(.075, .05)] * 3, 8)
    add_tube(bone, 'NeckCrest', 2, [vadd(p, (0, .15, .01)) for p in points[:4]],
             [(.045, .045)] * 4, 6)
    for i in range(4):
        p = vadd(points[i], (0, .17, .02))
        part(bone, 'CrestSpike', 2, *octa(p, ((0, 1, 0), (0, 0, 1), (1, 0, 0)), (.035, .075, .03)))
    for i in (1, 3):
        part(bone, 'NeckMuscle', 0,
             *ellipsoid(vadd(points[i], (side * .12, .02, 0)), (.10, .14, .16), 8, 3))


# ---------------------------------------------------------------------- legs
def build_leg(bone, rear):
    px, py, pz = BONES[bone][1]

    def P(x, y, z):
        return (px + x, py + y, pz + z)

    if rear:
        part(bone, 'UpperMuscle', 0, *ellipsoid(P(0, -.14, .10), (.31, .42, .36), 12, 6))
        part(bone, 'ShoulderCollar', 0, *ellipsoid(P(0, .00, .06), (.36, .30, .36), 8, 3))
        part(bone, 'ThighMass', 1, *ellipsoid(P(0, -.32, .16), (.28, .28, .32), 8, 3))
        part(bone, 'KneeJoint', 4, *ellipsoid(P(0, -.52, -.05), (.19, .17, .21), 8, 3))
        add_tube(bone, 'Shank', 1, [P(0, -.50, -.04), P(0, -.62, .01), P(0, -.76, .06), P(0, -.88, .11)],
                 [(.17, .18), (.155, .165), (.135, .145), (.12, .13)], 10)
        part(bone, 'HockJoint', 4, *ellipsoid(P(0, -.90, .12), (.13, .12, .16), 8, 3))
        add_tube(bone, 'Metatarsal', 0, [P(0, -.90, .10), P(0, -.99, .01), P(0, -1.04, -.08)],
                 [(.125, .12), (.115, .11), (.105, .10)], 8)
        add_box(bone, 'HockPlate', 1, P(0, -.92, .16), (.20, .18, .16), .25)
        part(bone, 'HeelSpur', 2, *octa(P(0, -.93, .23), ((0, 1, 0), (0, 0, 1), (1, 0, 0)), (.04, .08, .04)))
    else:
        part(bone, 'UpperMuscle', 0, *ellipsoid(P(0, -.17, .02), (.27, .37, .31), 12, 6))
        part(bone, 'ShoulderCollar', 0, *ellipsoid(P(0, -.02, .01), (.34, .28, .32), 8, 3))
        part(bone, 'ThighMass', 1, *ellipsoid(P(0, -.34, .09), (.23, .24, .26), 8, 3))
        part(bone, 'ElbowJoint', 4, *ellipsoid(P(0, -.52, .03), (.17, .15, .18), 8, 3))
        add_tube(bone, 'Forearm', 1, [P(0, -.50, .03), P(0, -.66, .07), P(0, -.80, .06), P(0, -.90, .02)],
                 [(.15, .16), (.14, .15), (.12, .13), (.11, .12)], 10)
        part(bone, 'WristJoint', 4, *ellipsoid(P(0, -.93, .03), (.12, .11, .14), 8, 3))
        add_tube(bone, 'Metacarpal', 0, [P(0, -.94, .02), P(0, -1.00, -.04), P(0, -1.03, -.09)],
                 [(.13, .12), (.12, .11), (.11, .10)], 8)
    add_box(bone, 'ShoulderPlate', 1, P(0, -.03 if not rear else .00, -.03 if not rear else .06), (.46, .24, .48), .22)
    add_box(bone, 'Paw', 0, P(0, -1.06, -.15), (.42, .20, .50), .20)
    for index, x in enumerate((-.15, -.05, .05, .15)):
        add_tube(bone, 'Toe', 1, [P(x, -1.10, -.20), P(x, -1.13, -.36), P(x * 1.05, -1.14, -.48)],
                 [(.062, .060), (.057, .055), (.047, .045)], 8)
        verts, faces = cone(P(x, -1.14, -.47), P(x, -1.20, -.62), .045, 5, .006)
        part(bone, 'Claw', 14, verts, faces)
    add_box(bone, 'AnkleBand', 1, P(0, -.98, -.01), (.34, .10, .30), .24)
    part(bone, 'ElbowSpur', 2, *octa(P(0, -.55, -.09), ((0, 1, 0), (0, 0, 1), (1, 0, 0)), (.07, .10, .07)))


# ---------------------------------------------------------------------- tail
def build_tail():
    segments = [
        ('TailA', [(0, 1.42, 1.20), (0, 1.37, 1.36), (0, 1.31, 1.54), (0, 1.27, 1.72), (0, 1.25, 1.86)],
         [(.31, .29), (.28, .26), (.25, .23), (.22, .21), (.20, .19)]),
        ('TailB', [(0, 1.25, 1.86), (0, 1.30, 2.00), (0, 1.40, 2.12), (0, 1.52, 2.21)],
         [(.20, .19), (.18, .17), (.165, .155), (.15, .14)]),
        ('TailC', [(0, 1.52, 2.21), (0, 1.62, 2.28), (0, 1.72, 2.33)],
         [(.15, .14), (.135, .125), (.12, .11)]),
    ]
    for index, (bone, points, radii) in enumerate(segments):
        add_tube(bone, 'TailVertebra', 4, points, radii, 12 if index < 2 else 10)
        if index == 0:
            continue
        for i in range(len(points) - 1):
            z = (points[i][2] + points[i + 1][2]) / 2
            y = (points[i][1] + points[i + 1][1]) / 2
            r = radii[i][1]
            verts, faces = fin((0, y + r * .85, z - .03), (0, y + r * .85, z + .03),
                               (0, y + r + .11, z), (1, 0, 0), .035)
            part(bone, 'DorsalFin', 5, verts, faces)
            verts, faces = fin((0, y - r * .70, z - .045), (0, y - r * .70, z + .045),
                               (0, y - r - .04, z), (1, 0, 0), .04)
            part(bone, 'VentralScute', 6, verts, faces)
            for side in (-1, 1):
                part(bone, 'LateralScale', 4, *octa((side * (radii[i][0] + .012), y, z),
                                                    ((1, 0, 0), (0, 1, 0), (0, 0, 1)),
                                                    (.03, .05, .055)))
        for i in range(2):
            p = points[i]
            part(bone, 'TailSpur', 1, *octa((0, p[1] + radii[i][1] + .06, p[2] + .04),
                                            ((0, 1, 0), (0, 0, 1), (1, 0, 0)), (.03, .07, .03)))


# --------------------------------------------------------------------- snakes
def build_snakes():
    central = [(0, 2.02, -.06), (0, 2.14, -.20), (0, 2.30, -.38), (0, 2.44, -.56),
               (0, 2.58, -.74), (0, 2.66, -.86)]
    add_tube('SnakeC', 'SerpentBody', 5, central,
             [(.12, .105), (.11, .10), (.10, .09), (.09, .08), (.08, .07), (.075, .065)], 8)
    build_head('SnakeC', 'SnakeJaw', SNAKE_C, 0.0, .62, 7, ears=False, snake=True)
    build_jaw('SnakeJaw', BONES['SnakeJaw'][1], 0.0, .62, teeth=3, venom=False)
    for i in range(5):
        p = central[i]
        part('SnakeC', 'SerpentScale', 4, *octa(vadd(p, (0, .055, .01)),
                                                ((0, 1, 0), (0, 0, 1), (1, 0, 0)), (.04, .055, .04)))
    for side in (-1, 1):
        bone = 'SnakeL' if side < 0 else 'SnakeR'
        center = (side * .95, 2.46, -.62)
        yaw = -side * .85
        points = [(side * .54, 2.02, .10), (side * .62, 2.10, -.02), (side * .72, 2.20, -.18),
                  (side * .82, 2.30, -.34), (side * .90, 2.40, -.50), (side * .93, 2.46, -.58)]
        add_tube(bone, 'SerpentBody', 5, points,
                 [(.10, .09), (.095, .085), (.085, .075), (.075, .07), (.068, .062), (.062, .056)], 8)
        build_head(bone, None, center, yaw, .52, 8, ears=False, snake=True)
        build_jaw(bone, hinge(center, .52, yaw), yaw, .52, teeth=3, venom=False)
        for i in range(4):
            part(bone, 'SerpentScale', 4, *octa(vadd(points[i], (0, .045, .01)),
                                                ((0, 1, 0), (0, 0, 1), (1, 0, 0)), (.035, .05, .035)))
    minors = [('Chest', (side * .34, 2.14, -.42), -side * .45) for side in (-1, 1)]
    minors.append(('Body', (0.0, 2.07, .50), math.pi))
    for bone, center, yaw in minors:
        build_minor_snake(bone, center, yaw, .34)


def build_minor_snake(bone, center, yaw, scale):
    def P(x, y, z):
        return vadd(center, rotate_y((x * scale, y * scale, z * scale), yaw))

    add_tube(bone, 'MinorSkull', 4, [P(0, .02, .10), P(0, .05, -.02), P(0, .02, -.16)],
             [(.10, .09), (.13, .11), (.11, .09)], 6)
    add_tube(bone, 'MinorSnout', 5, [P(0, .02, -.14), P(0, 0, -.28), P(0, -.01, -.38)],
             [(.11, .08), (.09, .07), (.07, .055)], 6)
    add_tube(bone, 'MinorJaw', 1, [P(0, -.03, -.02), P(0, -.02, -.20), P(0, 0, -.36)],
             [(.09, .05), (.08, .04), (.06, .035)], 5)
    add_tube(bone, 'MinorNeck', 4, [P(0, .02, .14), P(0, -.18, .20), P(0, -.42, .24)],
             [(.10, .09), (.09, .08), (.08, .07)], 6)
    for side in (-1, 1):
        part(bone, 'MinorEye', 8, *octa(P(side * .12, .09, -.10),
                                        ((1, 0, 0), (0, 1, 0), (0, 0, 1)), (.025, .035, .035)))
        verts, faces = cone(P(side * .07, -.02, -.30), P(side * .07, -.11, -.31), .022, 4)
        part(bone, 'MinorFang', 11, verts, faces)
        verts, faces = fin(P(side * .14, .06, .10), P(side * .14, .06, -.10),
                           P(side * .26, .22, -.06), (1, 0, 0), .025)
        part(bone, 'MinorHood', 5, verts, faces)


# ---------------------------------------------------------------- build all
build_head('Head', 'Jaw', HEAD_C, 0.0, 1.0, 7)
build_head('HeadL', 'JawL', head_center(-1), head_yaw(-1), SIDE_SCALE, 8)
build_head('HeadR', 'JawR', head_center(1), head_yaw(1), SIDE_SCALE, 9)
build_jaw('Jaw', BONES['Jaw'][1], 0.0, 1.0)
build_jaw('JawL', BONES['JawL'][1], head_yaw(-1), SIDE_SCALE)
build_jaw('JawR', BONES['JawR'][1], head_yaw(1), SIDE_SCALE)
for name, side in (('NeckC', 0), ('NeckL', -1), ('NeckR', 1)):
    build_neck(name, side)
for name, rear in (('Leg1', False), ('Leg2', False), ('Leg3', True), ('Leg4', True)):
    build_leg(name, rear)
build_tail()
build_head('TailHead', 'TailJaw', TAIL_HEAD_C, math.pi, .58, 8, ears=False, snake=True)
build_jaw('TailJaw', BONES['TailJaw'][1], math.pi, .58, teeth=3, venom=False)
build_snakes()


# ------------------------------------------------------------- normalise model
def normalise():
    points = [v for bone in BONE_NAMES for item in PARTS[bone] for v in item['verts']]
    lo = [min(p[j] for p in points) for j in range(3)]
    hi = [max(p[j] for p in points) for j in range(3)]
    offset = (-(lo[0] + hi[0]) / 2, -lo[1], -(lo[2] + hi[2]) / 2)
    for bone in BONE_NAMES:
        for item in PARTS[bone]:
            item['verts'] = [vadd(v, offset) for v in item['verts']]
    for name in BONE_NAMES:
        parent, pivot = BONES[name]
        BONES[name] = (parent, vadd(pivot, offset))
    points = [v for bone in BONE_NAMES for item in PARTS[bone] for v in item['verts']]
    lo = [min(p[j] for p in points) for j in range(3)]
    hi = [max(p[j] for p in points) for j in range(3)]
    return {'min': lo, 'max': hi, 'size': [hi[j] - lo[j] for j in range(3)]}


BOUNDS = normalise()


def local_pivot(bone):
    parent = BONES[bone][0]
    pivot = BONES[bone][1]
    return pivot if parent is None else vsub(pivot, BONES[parent][1])


# ------------------------------------------------------------------- texture
def write_png(path, array):
    height, width = array.shape[:2]
    raw = bytearray()
    for row in array:
        raw.append(0)
        raw.extend(row.astype('uint8').tobytes())

    def chunk(tag, data):
        return (struct.pack('>I', len(data)) + tag + data
                + struct.pack('>I', zlib.crc32(tag + data) & 0xffffffff))

    png = b'\x89PNG\r\n\x1a\n'
    png += chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 2, 0, 0, 0))
    png += chunk(b'IDAT', zlib.compress(bytes(raw), 9))
    png += chunk(b'IEND', b'')
    path.write_bytes(png)


REGIONS = [
    (0, 'obsidian-fur', (22, 24, 29)), (1, 'graphite-fur', (52, 57, 66)),
    (2, 'silver-fur', (104, 115, 127)), (3, 'highlight-fur', (132, 143, 154)),
    (4, 'deep-scale', (26, 31, 38)), (5, 'teal-scale', (44, 72, 78)),
    (6, 'bone-belly', (138, 136, 126)), (7, 'dark-gold-eye', (168, 130, 46)),
    (8, 'cyan-eye', (190, 224, 236)), (9, 'amber-eye', (188, 112, 36)),
    (10, 'mouth', (56, 16, 20)), (11, 'fang-ivory', (208, 203, 188)),
    (12, 'tongue', (84, 42, 76)), (13, 'venom', (206, 228, 230)),
    (14, 'claw', (56, 60, 68)), (15, 'bone-armor', (146, 142, 128)),
]


def build_texture():
    import numpy as np
    rng = np.random.default_rng(20261113)
    image = np.zeros((512, 1024, 3), np.float32)
    yy, xx = np.mgrid[0:128, 0:256]

    def tile(index):
        return image[(index // 4) * 128:(index // 4 + 1) * 128, (index % 4) * 256:(index % 4 + 1) * 256]

    def fur(index, base, amp, stride):
        area = tile(index)
        area[:] = np.array(base, np.float32) + rng.normal(0, amp, (128, 256, 1))
        if stride:
            strands = (np.sin(xx * 1.35 + yy * .55) > .90) | (np.sin(xx * .7 - yy * .4) > .97)
            area[strands] = np.clip(area[strands] + stride, 0, 255)

    def scales(index, base, edge, accent):
        area = tile(index)
        row = yy // 13
        cx = (xx + (row % 2) * 13) % 26
        cy = yy % 13
        d = np.minimum(np.minimum(cx, 26 - cx) / 13.0, np.minimum(cy, 13 - cy) / 6.5)
        area[:] = np.array(base, np.float32) + (np.array(accent, np.float32) - np.array(base, np.float32)) * d[..., None] * .65
        area[d < .16] = edge
        area[:] = np.clip(area + rng.normal(0, 3.2, (128, 256, 1)), 0, 255)

    def eye(index, outer, inner, slit=True):
        area = tile(index)
        r = np.sqrt(((xx - 128) / 108.0) ** 2 + ((yy - 64) / 60.0) ** 2)
        grad = np.clip(1 - r, 0, 1)[..., None]
        area[:] = np.array(outer, np.float32) + (np.array(inner, np.float32) - np.array(outer, np.float32)) * grad
        if slit:
            area[(np.abs(xx - 128) < 8) & (np.abs(yy - 64) < 48)] = (10, 10, 12)
        area[:] = np.clip(area + rng.normal(0, 2.0, (128, 256, 1)), 0, 255)

    def flat(index, base, amp, ripple=0.0):
        area = tile(index)
        area[:] = np.array(base, np.float32) + rng.normal(0, amp, (128, 256, 1))
        if ripple:
            area[:] = np.clip(area + np.sin(xx * .22 + yy * .31)[..., None] * ripple, 0, 255)

    fur(0, REGIONS[0][2], 5.0, 7)
    fur(1, REGIONS[1][2], 6.0, 9)
    fur(2, REGIONS[2][2], 7.0, 12)
    fur(3, REGIONS[3][2], 6.0, 14)
    scales(4, REGIONS[4][2], (13, 16, 21), (44, 52, 62))
    scales(5, REGIONS[5][2], (22, 38, 42), (72, 112, 118))
    scales(6, REGIONS[6][2], (116, 113, 104), (176, 172, 160))
    eye(7, (78, 56, 18), (196, 156, 66))
    eye(8, (92, 138, 156), (222, 244, 250))
    eye(9, (92, 48, 12), (208, 132, 48))
    flat(10, REGIONS[10][2], 4.0, 5.0)
    flat(11, REGIONS[11][2], 3.0, 4.0)
    flat(12, REGIONS[12][2], 5.0, 6.0)
    flat(13, REGIONS[13][2], 2.0, 3.0)
    dir = tile(14)
    dir[:] = np.array(REGIONS[14][2], np.float32) + (yy[..., None] / 127.0) * 46
    dir[:] = np.clip(dir + rng.normal(0, 3.0, (128, 256, 1)), 0, 255)
    bone = tile(15)
    bone[:] = np.array(REGIONS[15][2], np.float32) + rng.normal(0, 4.0, (128, 256, 1))
    cracks = (np.abs(np.sin(xx * .11 + yy * .07)) > .995) | (np.abs(np.sin(xx * .05 - yy * .13)) > .997)
    bone[cracks] = (96, 92, 82)
    write_png(TEXTURE, np.clip(image, 0, 255))
    return {'size': [1024, 512], 'regions': [
        {'index': i, 'name': name, 'color': list(color),
         'pixel_rect': [i % 4 * 256, i // 4 * 128, (i % 4 + 1) * 256, (i // 4 + 1) * 128],
         'game_uv_rect': [i % 4 / 4, i // 4 / 4, (i % 4 + 1) / 4, (i // 4 + 1) / 4]}
        for i, name, color in REGIONS]}


# --------------------------------------------------------------------- scene
def build_materials():
    image = bpy.data.images.load(str(TEXTURE), check_existing=True)
    image.colorspace_settings.name = 'sRGB'
    out = []
    for index in range(16):
        material = bpy.data.materials.new('MAT-Cerberus-%02d' % index)
        material.use_nodes = True
        nodes = material.node_tree.nodes
        nodes.clear()
        tex = nodes.new('ShaderNodeTexImage')
        tex.image = image
        tex.interpolation = 'Linear'
        shader = nodes.new('ShaderNodeBsdfPrincipled')
        shader.inputs['Roughness'].default_value = .78
        shader.inputs['Specular IOR Level'].default_value = .26
        material.node_tree.links.new(tex.outputs['Color'], shader.inputs['Base Color'])
        output = nodes.new('ShaderNodeOutputMaterial')
        material.node_tree.links.new(shader.outputs['BSDF'], output.inputs['Surface'])
        out.append(material)
    return out


def build_scene():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    scene = bpy.context.scene
    scene.name = SCENE
    world = bpy.data.worlds.new('WORLD-Cerberus')
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs[0].default_value = (.035, .045, .060, 1)
    world.node_tree.nodes['Background'].inputs[1].default_value = .55
    scene.world = world
    scene.render.engine = 'BLENDER_EEVEE'
    try:
        scene.eevee.taa_render_samples = 32
    except Exception:
        pass
    scene.render.resolution_x = 1500
    scene.render.resolution_y = 1100
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGB'
    scene.view_settings.view_transform = 'Standard'
    try:
        scene.view_settings.look = 'None'
    except Exception:
        pass

    ground = bpy.data.meshes.new('GROUND')
    ground.from_pydata([(-20, -20, 0), (20, -20, 0), (20, 20, 0), (-20, 20, 0)], [], [(0, 1, 2, 3)])
    ground.update()
    ground_material = bpy.data.materials.new('MAT-Ground')
    ground_material.use_nodes = True
    ground_material.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (.020, .024, .030, 1)
    ground_material.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = 1.0
    ground.materials.append(ground_material)
    ground_object = bpy.data.objects.new('GROUND', ground)
    scene.collection.objects.link(ground_object)

    for suffix, location, power, size in [('Key', (5, -7, 8), 950, 7), ('Fill', (-7, -4, 4), 460, 8),
                                          ('Rim', (1, 8, 6), 600, 6), ('Under', (0, -2, .6), 130, 5)]:
        light = bpy.data.lights.new('LGT-Cerberus-' + suffix, 'AREA')
        light.energy = power
        light.shape = 'DISK'
        light.size = size
        obj = bpy.data.objects.new(light.name, light)
        scene.collection.objects.link(obj)
        obj.location = location
        obj.rotation_euler = (Vector((0, 0, 1.6)) - obj.location).to_track_quat('-Z', 'Y').to_euler()

    camera_data = bpy.data.cameras.new('CAM-Cerberus')
    camera = bpy.data.objects.new('CAM-Cerberus', camera_data)
    scene.collection.objects.link(camera)
    camera_data.type = 'ORTHO'
    camera_data.ortho_scale = 8.0
    scene.camera = camera

    materials_ = build_materials()
    empties = {}
    for bone, (parent, pivot) in BONES.items():
        mesh_verts, mesh_faces, face_materials, part_ranges = [], [], [], []
        pivot = BONES[bone][1]
        for item in PARTS[bone]:
            base = len(mesh_verts)
            mesh_verts.extend(xyz(vsub(v, pivot)) for v in item['verts'])
            for face in item['faces']:
                mesh_faces.append(tuple(base + i for i in face))
                face_materials.append(item['material'])
            part_ranges.append((item['name'], base, base + len(item['verts']), len(item['faces'])))
        mesh = bpy.data.meshes.new('MESH-' + bone)
        mesh.from_pydata(mesh_verts, [], mesh_faces)
        mesh.update()
        for material in materials_:
            mesh.materials.append(material)
        for polygon, material_index in zip(mesh.polygons, face_materials):
            polygon.material_index = material_index
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(mesh)
        bm.free()
        mesh.update()

        uv = mesh.uv_layers.new(name='UV-GameAtlas')
        for polygon in mesh.polygons:
            normal = game(polygon.normal)
            axis = max(range(3), key=lambda j: abs(normal[j]))
            a, c = [j for j in range(3) if j != axis]
            for loop_index in polygon.loop_indices:
                point = game(mesh.vertices[mesh.loops[loop_index].vertex_index].co)
                u = .1 + .8 * ((point[a] * .91 + .5) % 1)
                v = .1 + .8 * ((point[c] * .83 + .5) % 1)
                uv.data[loop_index].uv = ((polygon.material_index % 4 + u) / 4,
                                          1 - (polygon.material_index // 4 + v) / 4)

        empty = bpy.data.objects.new('ARM-' + bone, None)
        empty.empty_display_size = .18
        scene.collection.objects.link(empty)
        empty.location = xyz(local_pivot(bone))
        if parent:
            empty.parent = empties[parent]
        empties[bone] = empty

        geo = bpy.data.objects.new('GEO-' + bone, mesh)
        geo['game_bone'] = bone
        geo.parent = empty
        scene.collection.objects.link(geo)
        geo['parts'] = [name for name, _, _, _ in part_ranges]
    return scene


# ----------------------------------------------------------------- validation
def validate():
    scene = bpy.context.scene
    bpy.context.view_layer.update()
    rows = []
    tiles = {}
    points = []
    for bone in BONE_NAMES:
        geo = bpy.data.objects['GEO-' + bone]
        mesh = geo.data
        mesh.calc_loop_triangles()
        bm = bmesh.new()
        bm.from_mesh(mesh)
        row = {
            'bone': bone,
            'mesh': geo.name,
            'parts': len(PARTS[bone]),
            'vertices': len(mesh.vertices),
            'triangles': len(mesh.loop_triangles),
            'boundary_edges': sum(edge.is_boundary for edge in bm.edges),
            'nonmanifold_edges': sum(not edge.is_manifold for edge in bm.edges),
            'zero_area_faces': sum(face.calc_area() < 1e-10 for face in bm.faces),
            'signed_volume': bm.calc_volume(signed=True),
        }
        bm.free()
        if row['boundary_edges'] or row['nonmanifold_edges'] or row['zero_area_faces'] or row['signed_volume'] <= 0:
            raise RuntimeError('Invalid closed geometry: ' + json.dumps(row))
        parent = BONES[bone][0]
        expected = 'ARM-' + parent if parent else None
        actual = geo.parent.parent.name if geo.parent and geo.parent.parent else None
        if geo.parent.name != 'ARM-' + bone or actual != expected:
            raise RuntimeError('Broken bone hierarchy: ' + bone)
        if (geo.parent.location - Vector(xyz(local_pivot(bone)))).length > 1e-6:
            raise RuntimeError('Bone pivot drift: ' + bone)
        for polygon in mesh.polygons:
            material = polygon.material_index
            tiles[material] = tiles.get(material, 0) + 1
            for loop_index in polygon.loop_indices:
                u, v = mesh.uv_layers.active.data[loop_index].uv
                if not (0.005 < u < .995 and 0.005 < v < .995):
                    raise RuntimeError('UV outside atlas: ' + bone)
                if int(u * 4) != material % 4 or int((1 - v) * 4) != material // 4:
                    raise RuntimeError('UV crosses palette region: ' + bone)
        world = [game((geo.matrix_world @ vertex.co)) for vertex in mesh.vertices]
        row['bounds'] = {'min': [round(min(p[j] for p in world), 4) for j in range(3)],
                         'max': [round(max(p[j] for p in world), 4) for j in range(3)]}
        points.extend(world)
        rows.append(row)

    total = sum(row['triangles'] for row in rows)
    vertices = total * 3
    if len(rows) != 24 or len(PARTS) != 24:
        raise RuntimeError('Expected 24 bones and 24 meshes')
    prefix = sum(r['triangles'] for r in rows if r['bone'] in ('Body', 'Chest'))
    print('TRIANGLES body=%d head3=%d neck3=%d jaw3=%d legs4=%d tail=%d snakes=%d total=%d' % (
        prefix,
        sum(r['triangles'] for r in rows if r['bone'] in ('Head', 'HeadL', 'HeadR')),
        sum(r['triangles'] for r in rows if r['bone'] in ('NeckC', 'NeckL', 'NeckR')),
        sum(r['triangles'] for r in rows if r['bone'] in ('Jaw', 'JawL', 'JawR')),
        sum(r['triangles'] for r in rows if r['bone'] in ('Leg1', 'Leg2', 'Leg3', 'Leg4')),
        sum(r['triangles'] for r in rows if r['bone'] in ('TailA', 'TailB', 'TailC', 'TailHead', 'TailJaw')),
        sum(r['triangles'] for r in rows if r['bone'] in ('SnakeC', 'SnakeL', 'SnakeR', 'SnakeJaw')),
        total))
    if total > TRI_BUDGET or vertices > VERT_BUDGET:
        raise RuntimeError('Budget exceeded: %d triangles / %d vertices' % (total, vertices))
    report = {
        'kind': 'Blender geometry validation, not gameplay',
        'blender': bpy.app.version_string,
        'bone_count': len(rows),
        'mesh_count': len(rows),
        'triangles': total,
        'export_vertices': vertices,
        'triangle_budget': TRI_BUDGET,
        'vertex_budget': VERT_BUDGET,
        'bounds': {'min': [round(min(p[j] for p in points), 4) for j in range(3)],
                   'max': [round(max(p[j] for p in points), 4) for j in range(3)]},
        'size': [round(max(p[j] for p in points) - min(p[j] for p in points), 4) for j in range(3)],
        'palette_faces': {str(k): v for k, v in sorted(tiles.items())},
        'palette_regions_used': sorted(tiles),
        'uv_inset': 'all face corners inside 0.1-0.9 of their 256x128 tile',
        'meshes': rows,
    }
    if len(tiles) != 16:
        raise RuntimeError('Not all 16 atlas regions are used: %s' % sorted(tiles))
    VALID.mkdir(parents=True, exist_ok=True)
    (VALID / 'geometry_report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    return report


# --------------------------------------------------------------------- export
def export_game():
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    meshes = {}
    total = 0
    for bone in BONE_NAMES:
        mesh = bpy.data.objects['GEO-' + bone].data
        mesh.calc_loop_triangles()
        rows = []
        for triangle in mesh.loop_triangles:
            points = [game(mesh.vertices[i].co) for i in triangle.vertices]
            normal = vnorm(vcross(vsub(points[1], points[0]), vsub(points[2], points[0])))
            uvs = [(mesh.uv_layers.active.data[li].uv.x, 1 - mesh.uv_layers.active.data[li].uv.y)
                   for li in triangle.loops]
            rows.append((points, normal, uvs))
        meshes[bone] = rows
        total += len(rows)
    if total > TRI_BUDGET or total * 3 > VERT_BUDGET:
        raise RuntimeError('Budget exceeded during export: %d triangles' % total)

    ns = 'http://www.collada.org/2005/11/COLLADASchema'
    ET = __import__('xml.etree.ElementTree', fromlist=['ElementTree'])
    ET.register_namespace('', ns)

    def element(parent, tag, attrs=None, text=None):
        node = ET.SubElement(parent, '{' + ns + '}' + tag, attrs or {})
        node.text = text
        return node

    doc = ET.Element('{' + ns + '}COLLADA', {'version': '1.4.1'})
    asset = element(doc, 'asset')
    element(asset, 'unit', {'name': 'meter', 'meter': '1'})
    element(asset, 'up_axis', text='Y_UP')
    library = element(doc, 'library_geometries')
    for bone in BONE_NAMES:
        rows = meshes[bone]
        name = 'Cerberus_' + bone
        mesh_node = element(element(library, 'geometry', {'id': name, 'name': name}), 'mesh')
        positions = [value for points, _, _ in rows for point in points for value in point]
        normals = [value for _, normal, _ in rows for _ in range(3) for value in normal]
        uvs = [value for _, _, uv in rows for pair in uv for value in pair]
        for suffix, values, stride, fields in [('pos', positions, 3, 'XYZ'), ('normal', normals, 3, 'XYZ'),
                                               ('uv', uvs, 2, 'ST')]:
            source_id = name + '_' + suffix
            source = element(mesh_node, 'source', {'id': source_id})
            element(source, 'float_array', {'id': source_id + '_array', 'count': str(len(values))},
                    ' '.join('%.7g' % value for value in values))
            accessor = element(element(source, 'technique_common'), 'accessor',
                               {'source': '#' + source_id + '_array', 'count': str(len(values) // stride),
                                'stride': str(stride)})
            for field in fields:
                element(accessor, 'param', {'name': field, 'type': 'float'})
        element(element(mesh_node, 'vertices', {'id': name + '_vertices'}),
                'input', {'semantic': 'POSITION', 'source': '#' + name + '_pos'})
        triangles = element(mesh_node, 'triangles', {'count': str(len(rows))})
        for offset, (semantic, suffix) in enumerate([('VERTEX', 'vertices'), ('NORMAL', 'normal'),
                                                     ('TEXCOORD', 'uv')]):
            attrs = {'semantic': semantic, 'source': '#' + name + '_' + suffix, 'offset': str(offset)}
            if offset == 2:
                attrs['set'] = '0'
            element(triangles, 'input', attrs)
        element(triangles, 'p', text=' '.join(str(i) for i in range(len(rows) * 3) for _ in range(3)))

    visual = element(element(doc, 'library_visual_scenes'), 'visual_scene', {'id': 'Scene'})
    nodes = {}
    for bone in BONE_NAMES:
        parent = BONES[bone][0]
        node = element(nodes[parent] if parent else visual,
                       'node', {'id': bone, 'name': bone, 'type': 'NODE'})
        nodes[bone] = node
        px, py, pz = local_pivot(bone)
        element(node, 'matrix', {'sid': 'transform'},
                '1 0 0 %.7g 0 1 0 %.7g 0 0 1 %.7g 0 0 0 1' % (px, py, pz))
        element(node, 'instance_geometry', {'url': '#Cerberus_' + bone})
    element(element(doc, 'scene'), 'instance_visual_scene', {'url': '#Scene'})
    ET.indent(doc)
    ET.ElementTree(doc).write(MODEL, encoding='utf-8', xml_declaration=True)
    MODEL.write_bytes(MODEL.read_bytes().replace(b'\r\n', b'\n'))
    return {'path': str(MODEL), 'triangles': total, 'vertices': total * 3,
            'sha256': hashlib.sha256(MODEL.read_bytes()).hexdigest().upper()}


def inspect_dae(path=MODEL):
    ET = __import__('xml.etree.ElementTree', fromlist=['ElementTree'])
    raw = path.read_bytes()
    tree = ET.parse(path)
    root = tree.getroot()
    ns = {'c': 'http://www.collada.org/2005/11/COLLADASchema'}
    up_axis = root.find('.//c:up_axis', ns).text
    geometries = {g.get('id'): g for g in root.findall('.//c:library_geometries/c:geometry', ns)}
    geometry_rows = []
    total_vertices = 0
    total_triangles = 0
    for name, geometry in geometries.items():
        mesh = geometry.find('c:mesh', ns)
        positions = mesh.find('c:source[@id="%s_pos"]/c:float_array' % name, ns)
        uv_values = mesh.find('c:source[@id="%s_uv"]/c:float_array' % name, ns)
        triangles = mesh.find('c:triangles', ns)
        count = int(triangles.get('count'))
        indices = [int(v) for v in triangles.find('c:p', ns).text.split()]
        vertex_count = int(positions.get('count')) // 3
        if len(indices) != count * 9:
            raise RuntimeError('Bad index stride in ' + name)
        if min(indices) < 0 or max(indices) >= vertex_count:
            raise RuntimeError('Index out of range in ' + name)
        if int(uv_values.get('count')) != vertex_count * 2:
            raise RuntimeError('UV count mismatch in ' + name)
        if len(triangles.findall('c:input', ns)) != 3:
            raise RuntimeError('Missing VERTEX/NORMAL/TEXCOORD input in ' + name)
        geometry_rows.append({'geometry': name, 'triangles': count, 'vertices': vertex_count})
        total_vertices += vertex_count
        total_triangles += count

    parents = {}
    nodes = {}
    visual = root.find('.//c:library_visual_scenes/c:visual_scene', ns)

    def walk(parent, element):
        for node in element.findall('c:node', ns):
            name = node.get('name')
            parents[name] = parent
            nodes[name] = node
            walk(name, node)

    walk(None, visual)
    expected = {name: parent for name, parent, _ in BONE_DEFS}
    if parents != expected:
        raise RuntimeError('DAE hierarchy mismatch: %s' % sorted(parents))
    for name, node in nodes.items():
        if node.find('c:instance_geometry', ns) is None or node.find('c:matrix', ns) is None:
            raise RuntimeError('Node missing matrix/geometry: ' + name)
    if b'\r' in raw:
        raise RuntimeError('DAE contains CR; LF required')
    report = {
        'kind': 'Collada structural inspection, not gameplay',
        'path': str(path.relative_to(ROOT)).replace('\\', '/'),
        'bytes': len(raw),
        'collada_version': root.get('version'),
        'up_axis': up_axis,
        'line_endings': 'LF',
        'nodes': len(nodes),
        'geometries': len(geometries),
        'hierarchy': parents,
        'bone_count': len(nodes),
        'mesh_count': len(geometries),
        'triangles': total_triangles,
        'export_vertices': total_vertices,
        'index_bounds_ok': True,
        'three_inputs_per_triangles': True,
        'per_bone': geometry_rows,
        'sha256': hashlib.sha256(raw).hexdigest().upper(),
    }
    if len(nodes) != 24 or len(geometries) != 24:
        raise RuntimeError('DAE must contain 24 nodes and geometries')
    if up_axis != 'Y_UP' or root.get('version') != '1.4.1':
        raise RuntimeError('DAE must be Y_UP Collada 1.4.1')
    (VALID / 'dae_report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    return report


# -------------------------------------------------------------------- renders
VIEWS = {
    'front': ((0.0, 12.0, 2.1), (0.0, 0.0, 1.55), 1.18),
    'side': ((12.0, 0.0, 2.1), (0.0, 0.0, 1.55), 1.18),
    'back': ((0.0, -12.0, 2.1), (0.0, 0.0, 1.55), 1.18),
    'top': ((0.0, 0.02, 14.0), (0.0, 0.0, 1.2), 1.12),
    'three-quarter': ((8.0, 9.0, 5.4), (0.0, 0.0, 1.45), 1.18),
    'head': ((3.4, 7.4, 4.1), (0.0, 1.95, 2.55), 1.22),
}
HEAD_BONES = {'Head', 'HeadL', 'HeadR', 'Jaw', 'JawL', 'JawR', 'NeckC', 'NeckL', 'NeckR'}


def camera_objects(bones=None):
    return [bpy.data.objects['GEO-' + bone] for bone in (bones or BONE_NAMES)]


def fit_camera(view, bones=None):
    from bpy_extras.object_utils import world_to_camera_view
    scene = bpy.context.scene
    camera = scene.camera
    position, target, margin_factor = VIEWS[view]
    camera.location = position
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat('-Z', 'Y').to_euler()
    bpy.context.view_layer.update()
    objects = camera_objects(bones)
    inverse = camera.matrix_world.inverted()
    points = [inverse @ (obj.matrix_world @ vertex.co) for obj in objects for vertex in obj.data.vertices]
    xmin, xmax = min(p.x for p in points), max(p.x for p in points)
    ymin, ymax = min(p.y for p in points), max(p.y for p in points)
    rotation = camera.rotation_euler.to_matrix()
    camera.location += rotation @ Vector(((xmin + xmax) / 2, (ymin + ymax) / 2, 0.0))
    bpy.context.view_layer.update()
    aspect = scene.render.resolution_x / scene.render.resolution_y
    camera.data.ortho_scale = max((xmax - xmin) * margin_factor, (ymax - ymin) * aspect * margin_factor)
    bpy.context.view_layer.update()
    normalized = [world_to_camera_view(scene, camera, obj.matrix_world @ vertex.co)
                  for obj in objects for vertex in obj.data.vertices]
    frame = [min(p.x for p in normalized), min(p.y for p in normalized),
             max(p.x for p in normalized), max(p.y for p in normalized)]
    margin = min(frame[0], frame[1], 1 - frame[2], 1 - frame[3])
    if margin <= 0.01 or not all(math.isfinite(v) for v in frame):
        raise RuntimeError('Clipped render for view %s: %s' % (view, frame))
    return {'normalized_bounds': [round(v, 4) for v in frame], 'minimum_margin': round(margin, 4),
            'whole_model_in_frame': True, 'ortho_scale': round(camera.data.ortho_scale, 4)}


def render_views():
    scene = bpy.context.scene
    scene.render.resolution_x = 1500
    scene.render.resolution_y = 1100
    report = {'kind': 'Blender preview frames, not gameplay', 'views': {}}
    for view in ('front', 'side', 'back', 'top', 'three-quarter', 'head'):
        bones = HEAD_BONES if view == 'head' else None
        fit = fit_camera(view, bones)
        path = DESIGN / ('blender-' + view + '.png')
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        fit['render'] = path.name
        fit['scope'] = 'head close-up' if view == 'head' else 'whole model'
        report['views'][view] = fit
    (VALID / 'multiview_fit_report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    return report


# ------------------------------------------------------------------ animation
def reset_pose():
    for bone in BONE_NAMES:
        empty = bpy.data.objects['ARM-' + bone]
        empty.rotation_euler = (0.0, 0.0, 0.0)
    body = bpy.data.objects['ARM-Body']
    body.location = Vector(xyz(local_pivot('Body')))


def apply_pose(rotations=None, body_lift=0.0):
    reset_pose()
    for bone, angles in (rotations or {}).items():
        bpy.data.objects['ARM-' + bone].rotation_euler = angles
    if body_lift:
        body = bpy.data.objects['ARM-Body']
        body.location = Vector(xyz(local_pivot('Body'))) + Vector((0.0, 0.0, body_lift))
    bpy.context.view_layer.update()


def idle_pose(phase):
    sway = math.sin(phase)
    return {'head': {'Head': (.035 * sway, 0.0, .05 * sway), 'HeadL': (.03 * sway, 0.0, .04 * sway),
                     'HeadR': (.03 * sway, 0.0, -.04 * sway), 'NeckC': (.02 * sway, 0.0, 0.0),
                     'TailA': (0.0, 0.0, .10 * sway), 'TailB': (0.0, 0.0, .12 * sway),
                     'TailC': (0.0, 0.0, .10 * sway), 'TailHead': (0.0, 0.0, .08 * sway),
                     'SnakeC': (.02 * sway, 0.0, .08 * sway), 'SnakeL': (.02 * sway, 0.0, -.06 * sway),
                     'SnakeR': (.02 * sway, 0.0, .06 * sway), 'Jaw': (-.02, 0.0, 0.0)},
            'lift': .018 * math.sin(phase * 2)}


def walk_pose(phase):
    swing = .40
    return {'head': {'Leg1': (swing * math.sin(phase), 0.0, 0.0),
                     'Leg2': (swing * math.sin(phase + math.pi), 0.0, 0.0),
                     'Leg3': (.44 * math.sin(phase + math.pi), 0.0, 0.0),
                     'Leg4': (.44 * math.sin(phase), 0.0, 0.0),
                     'Head': (.04 * math.sin(phase * 2), 0.0, .03 * math.sin(phase)),
                     'NeckC': (.03 * math.sin(phase * 2), 0.0, 0.0),
                     'TailA': (0.0, 0.0, .12 * math.sin(phase)), 'TailB': (0.0, 0.0, .14 * math.sin(phase - .4)),
                     'SnakeC': (.03 * math.sin(phase * 2), 0.0, .06 * math.sin(phase))},
            'lift': .035 * abs(math.sin(phase * 2))}


def roar_pose(amount):
    return {'head': {'Jaw': (-.58 * amount, 0.0, 0.0), 'JawL': (-.52 * amount, 0.0, 0.0),
                     'JawR': (-.52 * amount, 0.0, 0.0), 'SnakeJaw': (-.50 * amount, 0.0, 0.0),
                     'TailJaw': (.46 * amount, 0.0, 0.0), 'NeckC': (.16 * amount, 0.0, 0.0),
                     'Head': (.10 * amount, 0.0, 0.0), 'NeckL': (.10 * amount, 0.0, -.05 * amount),
                     'NeckR': (.10 * amount, 0.0, .05 * amount), 'HeadL': (.08 * amount, 0.0, .06 * amount),
                     'HeadR': (.08 * amount, 0.0, -.06 * amount), 'SnakeC': (.14 * amount, 0.0, 0.0),
                     'SnakeL': (.12 * amount, 0.0, 0.0), 'SnakeR': (.12 * amount, 0.0, 0.0),
                     'TailHead': (-.10 * amount, 0.0, 0.0), 'TailA': (0.0, 0.0, .05 * amount)},
            'lift': .02 * amount}


def render_motion():
    scene = bpy.context.scene
    scene.render.resolution_x = 900
    scene.render.resolution_y = 680
    fit_camera('three-quarter')
    frames = []
    plan = [('idle', [idle_pose(i * math.tau / 6) for i in range(3)]),
            ('walk', [walk_pose(i * math.tau / 6) for i in range(6)]),
            ('roar', [roar_pose(v) for v in (0.0, .45, 1.0, .6)])]
    for action, states in plan:
        for index, state in enumerate(states):
            apply_pose(state['head'] if 'head' in state else state, state.get('lift', 0.0))
            fit = fit_camera('three-quarter')
            path = VALID / ('%s-%02d.png' % (action, index + 1))
            scene.render.filepath = str(path)
            bpy.ops.render.render(write_still=True)
            frames.append({'action': action, 'frame': index + 1, 'render': path.name,
                           'whole_model_in_frame': True, 'minimum_margin': fit['minimum_margin']})
    reset_pose()
    fit_camera('three-quarter')
    scene.render.resolution_x = 1500
    scene.render.resolution_y = 1100
    report = {'kind': 'Blender key pose previews for the existing 24-bone rig, not gameplay',
              'actions': {'idle': 'breathing, head sway, tail sway',
                          'walk': 'alternating fore/hind leg swing, body bob, head bob',
                          'roar': 'all jaws open, necks lift, snake jaws open'},
              'frames': frames}
    (VALID / 'motion_preview_report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    return report


# ------------------------------------------------------------------- manifest
def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def save_blend():
    for image in bpy.data.images:
        if image.filepath and image.name.endswith('.png'):
            try:
                image.pack()
            except Exception:
                pass
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))


def write_manifest(geometry, texture, exported, dae, multiview, motion):
    manifest = {
        'original': True,
        'asset': 'AbyssCerberus',
        'source': 'scripts/New-CerberusBlender.py',
        'game_meshes_used': False,
        'blender': bpy.app.version_string,
        'bone_count': 24,
        'mesh_count': 24,
        'bones': {row['bone']: {'parent': BONES[row['bone']][0],
                                'pivot': [round(v, 5) for v in BONES[row['bone']][1]],
                                'local_pivot': [round(v, 5) for v in local_pivot(row['bone'])],
                                'mesh': row['mesh'], 'geometry': 'Cerberus_' + row['bone'],
                                'vertices': row['vertices'], 'triangles': row['triangles'],
                                'signed_volume': round(row['signed_volume'], 6)}
                  for row in geometry['meshes']},
        'bone_order': BONE_NAMES,
        'triangles': geometry['triangles'],
        'export_vertices': geometry['export_vertices'],
        'bounds': geometry['bounds'],
        'size': geometry['size'],
        'uv': texture,
        'texture': {'path': 'src/SCIEPort/Assets/Textures/AbyssCerberus.png',
                    'size': texture['size'], 'opaque': True, 'format': 'PNG RGB8'},
        'validation': {
            'geometry': 'validation/geometry_report.json',
            'dae': 'validation/dae_report.json',
            'multiview': 'validation/multiview_fit_report.json',
            'motion': 'validation/motion_preview_report.json',
            'conclusion': '24 closed manifold positive-volume meshes, UVs inside 16 palette tiles, '
                          'DAE Y_UP LF Collada 1.4.1 with %d nodes, all previews framed.'
                          % dae['nodes'],
        },
        'assets': {MODEL.relative_to(ROOT).as_posix(): exported['sha256'],
                   'src/SCIEPort/Assets/Textures/AbyssCerberus.png': sha256(TEXTURE)},
        'previews': {view: 'blender-%s.png' % view for view in VIEWS},
        'motion_previews': 'validation/idle-*.png, validation/walk-*.png, validation/roar-*.png',
    }
    (DESIGN / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    return manifest


def main():
    texture = build_texture()
    build_scene()
    geometry = validate()
    exported = export_game()
    dae = inspect_dae()
    multiview = render_views()
    motion = render_motion()
    reset_pose()
    fit_camera('three-quarter')
    save_blend()
    manifest = write_manifest(geometry, texture, exported, dae, multiview, motion)
    print(json.dumps({
        'bones': manifest['bone_count'], 'meshes': manifest['mesh_count'],
        'triangles': manifest['triangles'], 'vertices': manifest['export_vertices'],
        'size': [round(v, 3) for v in manifest['size']],
        'triangle_counts': {row['bone']: row['triangles'] for row in geometry['meshes']},
        'texture': texture['size'], 'dae_sha256': exported['sha256'],
        'png_sha256': sha256(TEXTURE), 'blender': bpy.app.version_string,
    }, indent=2))
    print('CERBERUS BUILD OK')


main()

