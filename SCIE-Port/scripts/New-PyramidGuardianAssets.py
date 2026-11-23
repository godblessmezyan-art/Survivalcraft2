"""Build the original rigid humanoid meshes and pixel atlas for the pyramid."""

from math import sqrt
from pathlib import Path
from random import Random
import xml.etree.ElementTree as ET

from PIL import Image, ImageDraw


ASSETS = Path(__file__).resolve().parents[1] / "src/SCIEPort/Assets"
TILE = 64
ATLAS = 256
NS = "http://www.collada.org/2005/11/COLLADASchema"
ET.register_namespace("", NS)

LINEN, WRAPS, FRAY, SKIN, GOLD, NEMES, LAPIS, JADE, WHITE, SHADOW, EYES, LEATHER = range(12)
COLORS = (
    (173, 155, 119), (201, 185, 149), (134, 112, 82), (83, 65, 47),
    (208, 155, 59), (35, 78, 118), (22, 46, 77), (38, 135, 129),
    (207, 194, 156), (44, 34, 32), (125, 225, 204), (107, 74, 39),
)


def build_atlas():
    image = Image.new("RGB", (ATLAS, ATLAS), COLORS[SHADOW])
    rng = Random(235)
    for index, color in enumerate(COLORS):
        tile = Image.new("RGB", (TILE, TILE), color)
        pixels = tile.load()
        for y in range(TILE):
            for x in range(TILE):
                base = color
                shade = rng.choice((-7, -4, -2, 0, 0, 3, 5))
                if index in (LINEN, WRAPS, FRAY, WHITE):
                    phase = (y + x // 9 + (index % 3) * 2) % 12
                    shade += (-39, -16, 9, 12, 7, 5, 3, 1, 0, -3, -8, -13)[phase]
                    shade += (x % 3 == 0) * 5
                elif index == NEMES:
                    base = COLORS[GOLD] if (y + x // 16) % 14 < 4 else color
                    shade += 9 if x % 6 == 0 else 0
                elif index == GOLD:
                    shade += 14 - (x + y) % 23
                pixels[x, y] = tuple(max(0, min(255, c + shade)) for c in base)
        if index in (LINEN, WRAPS, FRAY):
            draw = ImageDraw.Draw(tile)
            for _ in range(12):
                x, y = rng.randrange(61), rng.randrange(64)
                draw.line((x, y, x + rng.randrange(1, 4), y), fill=tuple(c - 27 for c in color))
        image.paste(tile, (index % 4 * TILE, (3 - index // 4) * TILE))
    image.save(ASSETS / "Textures/PyramidGuardians.png")


class Guardian:
    def __init__(self, name, hips, shoulders, neck, leg_gap, shoulder_gap):
        self.name = name
        self.pivots = {
            "Body": (0, 0, 0),
            "Head": (0, neck, 0),
            "Hand1": (-shoulder_gap, shoulders, 0),
            "Hand2": (shoulder_gap, shoulders, 0),
            "Leg1": (-leg_gap, hips, 0),
            "Leg2": (leg_gap, hips, 0),
        }
        self.meshes = {bone: [] for bone in self.pivots}

    def face(self, bone, points, material):
        a, b, c = points[:3]
        u = [b[i] - a[i] for i in range(3)]
        v = [c[i] - a[i] for i in range(3)]
        normal = [u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0]]
        length = sqrt(sum(n * n for n in normal))
        normal = [n / length for n in normal]
        axis = max(range(3), key=lambda i: abs(normal[i]))
        s_axis, t_axis = ((2, 1), (0, 2), (0, 1))[axis]
        s_min, t_min = min(p[s_axis] for p in points), min(p[t_axis] for p in points)
        s_span = max(p[s_axis] for p in points) - s_min
        t_span = max(p[t_axis] for p in points) - t_min
        vertices = []
        for point in points:
            s = (point[s_axis] - s_min) / s_span
            t = (point[t_axis] - t_min) / t_span
            # Map away from neighboring tiles so texture filtering stays within the chosen surface.
            uv = ((material % 4 + .06 + .88 * s) / 4, (material // 4 + .06 + .88 * t) / 4)
            vertices.append((point, normal, uv))
        for i in range(1, len(vertices) - 1):
            self.meshes[bone].extend((vertices[0], vertices[i], vertices[i + 1]))

    def piece(self, bone, bottom, top, foot, shoulder, material, center=(0, 0), bevel=.018):
        """Tapered octagonal prism in the rigid bone's local coordinates."""
        x, z = center

        def ring(y, profile):
            w, d = (side / 2 for side in profile)
            cut = min(bevel, .35 * min(w, d))
            return [(x + a, y, z + b) for a, b in (
                (-w + cut, -d), (w - cut, -d), (w, -d + cut), (w, d - cut),
                (w - cut, d), (-w + cut, d), (-w, d - cut), (-w, -d + cut))]

        low, high = ring(bottom, foot), ring(top, shoulder)
        self.face(bone, low, material)
        self.face(bone, high[::-1], material)
        for i in range(8):
            j = (i + 1) % 8
            self.face(bone, (low[i], high[i], high[j], low[j]), material)

    def band(self, bone, bottom, top, width, depth, material, x=0, z=0):
        self.piece(bone, bottom, top, (width, depth), (width, depth), material, (x, z))


def mummy():
    model = Guardian("PyramidMummy", hips=.86, shoulders=1.38, neck=1.45, leg_gap=.12, shoulder_gap=.261)
    piece, band = model.piece, model.band
    piece("Body", .80, 1.46, (.34, .25), (.43, .26), LINEN)
    piece("Body", 1.02, 1.14, (.40, .28), (.414, .28), WRAPS)
    band("Body", 1.27, 1.34, .424, .28, FRAY)
    band("Body", .77, .99, .079, .031, FRAY, -.135, -.148)
    band("Body", .70, .87, .049, .024, WRAPS, -.108, -.146)
    piece("Head", -.025, .36, (.259, .243), (.34, .30), LINEN)
    band("Head", .234, .318, .334, .289, WRAPS)
    band("Head", .135, .211, .269, .025, SHADOW, z=-.137)
    for side in (-1, 1):
        band("Head", .162, .187, .041, .014, EYES, .075 * side, -.151)
    band("Head", .028, .078, .212, .026, FRAY, .021, -.134)
    for i in (1, 2):
        hand, leg = f"Hand{i}", f"Leg{i}"
        piece(hand, -.54, .054, (.145, .178), (.174, .203), LINEN if i == 1 else WRAPS)
        band(hand, -.44, -.35, .155, .189, FRAY)
        band(hand, -.51, -.46, .151, .185, WRAPS)
        piece(hand, -.627, -.52, (.132, .147), (.145, .164), SKIN)
        piece(leg, -.825, .041, (.16, .169), (.205, .229), LINEN)
        band(leg, -.55, -.46, .184, .215, WRAPS)
        band(leg, -.86, -.75, .175, .256, FRAY, z=-.038)
    return model


def pharaoh():
    model = Guardian("PyramidPharaoh", hips=.88, shoulders=1.52, neck=1.58, leg_gap=.132, shoulder_gap=.342)
    piece, band = model.piece, model.band
    piece("Body", .81, 1.588, (.43, .26), (.545, .31), LAPIS)
    piece("Body", 1.30, 1.541, (.277, .335), (.60, .35), GOLD)
    piece("Body", 1.36, 1.502, (.28, .351), (.589, .364), JADE)
    piece("Body", 1.407, 1.451, (.425, .365), (.497, .365), GOLD)
    band("Body", 1.298, 1.406, .112, .047, GOLD, z=-.185)
    band("Body", 1.319, 1.380, .068, .034, JADE, z=-.206)
    band("Body", .867, .938, .459, .291, GOLD)
    band("Body", .591, .886, .137, .062, NEMES, z=-.114)
    band("Body", .571, .62, .142, .064, GOLD, z=-.115)
    piece("Head", -.028, .356, (.247, .25), (.336, .31), SKIN)
    piece("Head", .224, .404, (.438, .366), (.394, .35), NEMES)
    band("Head", .234, .271, .449, .384, GOLD)
    for side in (-1, 1):
        piece("Head", -.083, .271, (.153, .34), (.116, .329), NEMES, (.216 * side, .008))
        band("Head", -.094, -.054, .157, .342, GOLD, .218 * side, .01)
        band("Head", .145, .196, .095, .03, SHADOW, .071 * side, -.147)
        band("Head", .16, .18, .059, .019, EYES, .071 * side, -.165)
    piece("Head", -.137, -.004, (.059, .052), (.10, .072), LAPIS, (0, -.141))
    band("Head", -.136, -.107, .074, .066, GOLD, z=-.146)
    piece("Head", .276, .433, (.098, .079), (.089, .052), GOLD, (0, -.188))
    piece("Head", .39, .50, (.096, .062), (.054, .063), GOLD, (0, -.189))
    band("Head", .438, .467, .046, .021, JADE, z=-.227)
    for i in (1, 2):
        hand, leg = f"Hand{i}", f"Leg{i}"
        piece(hand, -.562, .054, (.166, .202), (.209, .249), LAPIS)
        band(hand, -.17, -.092, .219, .255, GOLD)
        band(hand, -.555, -.409, .195, .233, GOLD)
        band(hand, -.516, -.47, .199, .237, JADE)
        piece(hand, -.658, -.545, (.158, .186), (.163, .192), SKIN)
        piece(leg, -.848, .034, (.167, .204), (.225, .255), LEATHER)
        piece(leg, -.384, .055, (.212, .256), (.237, .269), WHITE)
        band(leg, -.42, -.352, .218, .262, GOLD)
        band(leg, -.65, -.587, .184, .253, NEMES)
        band(leg, -.88, -.763, .191, .309, GOLD, z=-.048)
    return model


def node(parent, tag, attributes=None, text=None):
    result = ET.SubElement(parent, f"{{{NS}}}{tag}", attributes or {})
    result.text = text
    return result


def export(model):
    doc = ET.Element(f"{{{NS}}}COLLADA", {"version": "1.4.1"})
    asset = node(doc, "asset")
    node(asset, "unit", {"name": "meter", "meter": "1"})
    node(asset, "up_axis", text="Y_UP")
    geometries = node(doc, "library_geometries")
    for bone, vertices in model.meshes.items():
        name = model.name + "_" + bone
        mesh = node(node(geometries, "geometry", {"id": name, "name": name}), "mesh")
        for index, (suffix, fields) in enumerate((("pos", "XYZ"), ("normal", "XYZ"), ("uv", "ST"))):
            sid = f"{name}_{suffix}"
            data = [component for vertex in vertices for component in vertex[index]]
            source = node(mesh, "source", {"id": sid})
            node(source, "float_array", {"id": sid + "_array", "count": str(len(data))},
                 " ".join(f"{value:.6g}" for value in data))
            accessor = node(node(source, "technique_common"), "accessor", {
                "source": f"#{sid}_array", "count": str(len(vertices)), "stride": str(len(fields))})
            for field in fields:
                node(accessor, "param", {"name": field, "type": "float"})
        point = node(mesh, "vertices", {"id": name + "_vertices"})
        node(point, "input", {"semantic": "POSITION", "source": f"#{name}_pos"})
        triangles = node(mesh, "triangles", {"count": str(len(vertices) // 3)})
        for offset, (semantic, suffix) in enumerate((("VERTEX", "vertices"), ("NORMAL", "normal"), ("TEXCOORD", "uv"))):
            node(triangles, "input", {"semantic": semantic, "source": f"#{name}_{suffix}", "offset": str(offset)})
        node(triangles, "p", text=" ".join(str(i) for i in range(len(vertices)) for _ in range(3)))
    scene = node(node(doc, "library_visual_scenes"), "visual_scene", {"id": "Scene"})
    root = scene
    for bone, (x, y, z) in model.pivots.items():
        part = node(root, "node", {"id": bone, "name": bone, "type": "NODE"})
        if bone == "Body":
            root = part
        node(part, "matrix", {"sid": "transform"}, f"1 0 0 {x:g} 0 1 0 {y:g} 0 0 1 {z:g} 0 0 0 1")
        node(part, "instance_geometry", {"url": f"#{model.name}_{bone}"})
    node(node(doc, "scene"), "instance_visual_scene", {"url": "#Scene"})
    ET.indent(doc)
    path = ASSETS / f"Models/{model.name}.dae"
    path.write_bytes(ET.tostring(doc, encoding="utf-8", xml_declaration=True) + b"\n")
    print(f"{path.name}: {sum(len(mesh) for mesh in model.meshes.values()) // 3} triangles")


if __name__ == "__main__":
    build_atlas()
    export(mummy())
    export(pharaoh())
