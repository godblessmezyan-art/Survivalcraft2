"""Build the SCNET rigid-bone arthropod assets. Use --preview with Blender Python for renders."""
import argparse
import hashlib
import math
import random
import struct
import xml.etree.ElementTree as ET
import zlib
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "src/SCIEPort/Assets"
OUT = ROOT / "artifacts/wild-arthropods"
PALETTE = (
    (118, 77, 45), (169, 112, 62), (209, 155, 89), (230, 184, 116),
    (71, 43, 34), (244, 216, 156), (155, 85, 49), (33, 27, 28),
    (37, 73, 69), (63, 118, 110), (102, 163, 138), (169, 205, 155),
    (164, 70, 45), (215, 110, 58), (236, 149, 76), (248, 222, 155),
)
NS = "{http://www.collada.org/2005/11/COLLADASchema}"


class Vector(tuple):
    def __add__(self, other):
        return Vector(a + b for a, b in zip(self, other))

    def __sub__(self, other):
        return Vector(a - b for a, b in zip(self, other))

    def __mul__(self, scalar):
        return Vector(x * scalar for x in self)

    __rmul__ = __mul__

    def cross(self, other):
        a, b, c = self
        x, y, z = other
        return Vector((b * z - c * y, c * x - a * z, a * y - b * x))

    def length(self):
        return math.sqrt(sum(x * x for x in self))

    def normalized(self):
        return self * (1 / self.length())

    def lerp(self, other, amount):
        return self + (other - self) * amount


def blender(point):
    return (point[0], -point[2], point[1])


def make_atlas(path, palette=PALETTE):
    """Print padded pixel clusters and edge bands into the native nearest-filtered texture."""
    rand = random.Random(7813)
    pattern = [[rand.choice((-10, -5, 0, 0, 5, 9)) for _ in range(64)] for _ in range(16)]
    width, height = 128, 32
    rows = bytearray()
    for y in range(height):
        rows.append(0)
        for x in range(width):
            band = -8 if y < 7 or y > 24 else 0
            speckle = pattern[y // 2][x // 2]
            rows.extend(max(0, min(255, channel + band + speckle)) for channel in palette[x // 8])
            rows.append(255)

    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))

    path.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
                     + chunk(b"IDAT", zlib.compress(rows, 9)) + chunk(b"IEND", b""))


class Creature:
    def __init__(self, name):
        self.name = name
        self.bones = {}
        self.bone("Body", None, (0, 0, 0))

    def bone(self, name, parent, center):
        self.bones[name] = (parent, Vector(center), [])

    def mesh(self, bone, vertices, faces, color):
        self.bones[bone][2].append((vertices, faces, color))

    def shell(self, bone, center, radii, color, sides=8):
        """Make a closed shell with a low bevel and a wide plain face."""
        cx, cy, cz = center
        rx, ry, rz = radii
        verts = []
        for factor, height in ((.69, -1), (1, -.35), (.92, .45), (.62, 1)):
            for side in range(sides):
                angle = 2 * math.pi * (side + .5) / sides
                verts.append((cx + rx * factor * math.cos(angle), cy + height * ry,
                              cz + rz * factor * math.sin(angle)))
        faces = [tuple(reversed(range(sides))), tuple(range(sides * 3, sides * 4))]
        for ring in range(3):
            for side in range(sides):
                next_side = (side + 1) % sides
                faces.append((ring * sides + side, ring * sides + next_side,
                              (ring + 1) * sides + next_side, (ring + 1) * sides + side))
        self.mesh(bone, verts, [tuple(reversed(face)) for face in faces], color)

    def limb(self, bone, points, radii, color, sides=5):
        """Sweep rings through the joint centers and finish thin tips with a vertex."""
        points = list(map(Vector, points))
        verts = []
        rings = []
        for i, point in enumerate(points):
            rings.append(len(verts))
            radius = radii[i]
            if radius == 0:
                verts.append(point)
                continue
            tangent = (points[min(i + 1, len(points) - 1)] - points[max(i - 1, 0)]).normalized()
            reference = Vector((0, 1, 0)) if abs(tangent[1]) < .95 else Vector((1, 0, 0))
            across = tangent.cross(reference).normalized()
            up = tangent.cross(across)
            for k in range(sides):
                a = 2 * math.pi * k / sides
                verts.append(point + radius * (across * math.cos(a) + up * math.sin(a)))
        faces = [tuple(reversed(range(sides)))]
        for i, (a, b) in enumerate(zip(rings, rings[1:])):
            for k in range(sides):
                j = (k + 1) % sides
                faces.append((a + k, a + j, b) if radii[i + 1] == 0 else (a + k, a + j, b + j, b + k))
        if radii[-1] > 0:
            faces.append(tuple(range(rings[-1], rings[-1] + sides)))
        self.mesh(bone, verts, faces, color)

    def triangles(self, bone):
        _, pivot, parts = self.bones[bone]
        for vertices, polygons, color in parts:
            for face in polygons:
                points = [Vector(vertices[index]) - pivot for index in face]
                for i in range(1, len(points) - 1):
                    corners = (0, i, i + 1)
                    a, b, c = (points[j] for j in corners)
                    perpendicular = (b - a).cross(c - a)
                    if perpendicular.length() < 1e-10:
                        raise ValueError(f"Degenerate triangle in {self.name}/{bone}")
                    normal = perpendicular.normalized()
                    for j in corners:
                        angle = 2 * math.pi * j / len(points)
                        uv = ((color + .5 + .29 * math.cos(angle)) / 16, .5 + .30 * math.sin(angle))
                        yield points[j], normal, uv


def scorpion():
    animal = Creature("SandScorpion")
    animal.shell("Body", (0, .157, .066), (.222, .101, .284), 1)
    animal.shell("Body", (0, .154, -.169), (.208, .110, .158), 2)
    for i, z in enumerate((-.040, .052, .144, .225)):
        animal.shell("Body", (0, .230 - i * .009, z),
                     (.202 - i * .017, .042, .061), 2 if i % 2 == 0 else 1)
    for side, letter in ((-1, "L"), (1, "R")):
        animal.shell("Body", (side * .045, .256, -.217), (.026, .013, .026), 7, 6)
        animal.shell("Body", (side * .123, .225, -.266), (.019, .014, .021), 4, 6)
        for i in range(4):
            z = -.172 + i * .100
            root = (side * (.162 - max(0, i - 1) * .015), .144, z)
            tip = (side * (.386 + (.028 if i in (1, 2) else 0)), .015, z + (i - 1) * .067)
            name = f"Leg{letter}{i + 1}"
            animal.bone(name, "Body", root)
            animal.limb(name, [root, (side * .283, .160, z + (i - 1) * .033), tip],
                        [.037, .023, .009], 1)
            animal.limb(name, [(tip[0] * .963, .049, tip[2]), tip,
                               (tip[0] + side * .012, .002, tip[2] - .017)], [.012, .010, 0], 4)
        joint = (side * .143, .148, -.234)
        name = "Claw" + letter
        animal.bone(name, "Body", joint)
        animal.limb(name, [joint, (side * .250, .119, -.289), (side * .277, .117, -.391)],
                    [.048, .044, .035], 2)
        animal.shell(name, (side * .277, .117, -.403), (.080, .055, .091), 2)
        animal.limb(name, [(side * .310, .114, -.437), (side * .337, .120, -.515),
                           (side * .277, .127, -.560)], [.033, .022, 0], 3)
        animal.limb(name, [(side * .240, .114, -.437), (side * .211, .116, -.502),
                           (side * .247, .125, -.548)], [.029, .018, 0], 6)
    tail = [(0, .164, .273), (0, .245, .344), (0, .360, .369), (0, .448, .303)]
    for i in range(3):
        name = f"Tail{i + 1}"
        animal.bone(name, "Body" if i == 0 else f"Tail{i}", tail[i])
        animal.limb(name, [Vector(tail[i]).lerp(Vector(tail[i + 1]), -.14),
                           Vector(tail[i]).lerp(Vector(tail[i + 1]), .50), tail[i + 1]],
                    [.057 - .008 * i, .052 - .007 * i, .037 - .004 * i], 2 - i % 2, 6)
    animal.shell("Tail3", (0, .453, .278), (.050, .040, .062), 6)
    animal.limb("Tail3", [(0, .459, .276), (0, .454, .205), (0, .397, .171)],
                [.032, .024, 0], 4, 6)
    return animal


def crab():
    animal = Creature("FiddlerCrab")
    animal.shell("Body", (0, .104, .016), (.158, .071, .121), 9)
    animal.shell("Body", (0, .151, -.003), (.149, .040, .107), 10)
    animal.shell("Body", (0, .078, -.060), (.122, .046, .088), 8)
    for side, letter in ((-1, "L"), (1, "R")):
        x = side * .075
        animal.limb("Body", [(x, .142, -.065), (x * 1.20, .218, -.095)], [.014, .011], 10)
        animal.shell("Body", (x * 1.20, .219, -.098), (.020, .022, .019), 7, 6)
        animal.shell("Body", (x * 1.21, .225, -.113), (.007, .008, .006), 11, 6)
        for i in range(4):
            z = -.069 + i * .044
            root = (side * .107, .092, z)
            knee = (side * (.176 + (.014 if i in (1, 2) else 0)), .101, z + (i - 1) * .025)
            toe = (side * (.225 + (.014 if i in (1, 2) else 0)), .002, z + (i - 1) * .045)
            name = f"Leg{letter}{i + 1}"
            animal.bone(name, "Body", root)
            animal.limb(name, [root, knee, toe], [.023, .015, 0], 9)
            animal.shell(name, knee, (.018, .016, .017), 11, 6)
    animal.bone("ClawR", "Body", (.092, .097, -.080))
    animal.limb("ClawR", [(.093, .092, -.075), (.139, .098, -.111), (.168, .123, -.149)],
                [.032, .026, .030], 12)
    animal.shell("ClawR", (.179, .139, -.167), (.072, .055, .069), 13)
    animal.limb("ClawR", [(.201, .147, -.192), (.233, .168, -.240), (.189, .186, -.278)],
                [.043, .032, 0], 14)
    animal.limb("ClawR", [(.161, .125, -.193), (.129, .138, -.242), (.173, .165, -.266)],
                [.030, .018, 0], 15)
    animal.bone("ClawL", "Body", (-.087, .089, -.075))
    animal.limb("ClawL", [(-.075, .088, -.067), (-.136, .099, -.113), (-.158, .121, -.149)],
                [.024, .020, .023], 9)
    animal.shell("ClawL", (-.164, .119, -.142), (.039, .029, .046), 12)
    for x, tip in ((-.185, -.168), (-.143, -.152)):
        animal.limb("ClawL", [(x, .119, -.153), (x, .130, -.191), (tip, .133, -.204)], [.016, .010, 0], 13)
    return animal


def export(animal):
    def add(node, tag, attr=None, value=None):
        result = ET.SubElement(node, tag, attr or {})
        result.text = value
        return result

    root = ET.Element("COLLADA", {"xmlns": NS[1:-1], "version": "1.4.1"})
    asset = add(root, "asset")
    add(asset, "unit", {"name": "meter", "meter": "1"})
    add(asset, "up_axis", value="Y_UP")
    library = add(root, "library_geometries")
    visual = add(add(root, "library_visual_scenes"), "visual_scene", {"id": "Scene"})
    nodes, count = {}, 0
    for bone, (parent, pivot, _) in animal.bones.items():
        ident = animal.name + "_" + bone
        geometry = add(add(library, "geometry", {"id": ident, "name": bone}), "mesh")
        pos, nor, uv = [], [], []
        for vertex, normal, texcoord in animal.triangles(bone):
            pos.extend(vertex)
            nor.extend(normal)
            u, v = texcoord
            uv.extend((u, 1 - v))
        for suffix, values, fields in (("pos", pos, "XYZ"), ("normal", nor, "XYZ"), ("uv", uv, "ST")):
            name = f"{ident}_{suffix}"
            source = add(geometry, "source", {"id": name})
            add(source, "float_array", {"id": name + "_a", "count": str(len(values))},
                " ".join(f"{v:.7g}" for v in values))
            acc = add(add(source, "technique_common"), "accessor",
                      {"source": "#" + name + "_a", "count": str(len(values) // len(fields)), "stride": str(len(fields))})
            for field in fields:
                add(acc, "param", {"name": field, "type": "float"})
        add(add(geometry, "vertices", {"id": ident + "_vertices"}), "input",
            {"semantic": "POSITION", "source": "#" + ident + "_pos"})
        tri = add(geometry, "triangles", {"count": str(len(pos) // 9)})
        for i, (semantic, suffix) in enumerate((("VERTEX", "vertices"), ("NORMAL", "normal"), ("TEXCOORD", "uv"))):
            add(tri, "input", {"semantic": semantic, "source": f"#{ident}_{suffix}", "offset": str(i)})
        add(tri, "p", value=" ".join(str(i) for i in range(len(pos) // 3) for _ in range(3)))
        count += len(pos) // 9
        node = add(nodes[parent] if parent else visual, "node", {"id": bone, "name": bone, "type": "NODE"})
        nodes[bone] = node
        p = pivot - animal.bones[parent][1] if parent else pivot
        add(node, "matrix", {"sid": "transform"}, f"1 0 0 {p[0]:.7g} 0 1 0 {p[1]:.7g} 0 0 1 {p[2]:.7g} 0 0 0 1")
        add(node, "instance_geometry", {"url": "#" + ident})
    add(add(root, "scene"), "instance_visual_scene", {"url": "#Scene"})
    path = ASSETS / "Models" / (animal.name + ".dae")
    ET.indent(root)
    ET.ElementTree(root).write(path, encoding="utf-8", xml_declaration=True)
    vertices = [v for _, _, parts in animal.bones.values() for verts, _, _ in parts for v in verts]
    print(animal.name, len(animal.bones), count,
          [round(max(v[i] for v in vertices) - min(v[i] for v in vertices), 3) for i in range(3)],
          hashlib.sha256(path.read_bytes()).hexdigest())


def material(name, color):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    mat.node_tree.nodes.get("Principled BSDF").inputs["Base Color"].default_value = mat.diffuse_color
    return mat


def load_preview_model(name, x, texture):
    """Read the delivered DAE geometry without altering any source scale or joint offset."""
    root = ET.parse(ASSETS / "Models" / (name + ".dae")).getroot()
    geometries = {g.get("id"): g.find(NS + "mesh") for g in root.find(NS + "library_geometries")}

    def node_mesh(node, origin):
        t = list(map(float, node.findtext(NS + "matrix").split()))
        pos = origin + Vector((t[3], t[7], t[11]))
        ref = node.find(NS + "instance_geometry")
        if ref is not None:
            geom = geometries[ref.get("url")[1:]]
            sources = [list(map(float, source.findtext(NS + "float_array").split()))
                       for source in geom.findall(NS + "source")]
            verts, _, uv = sources
            points = [blender(verts[i:i + 3]) for i in range(0, len(verts), 3)]
            faces = [tuple(range(i, i + 3)) for i in range(0, len(points), 3)]
            mesh = bpy.data.meshes.new("GEO-" + name + "-" + node.get("name"))
            mesh.from_pydata(points, [], faces)
            layer = mesh.uv_layers.new(name="Atlas")
            for loop in mesh.loops:
                u, v = uv[loop.vertex_index * 2:loop.vertex_index * 2 + 2]
                layer.data[loop.index].uv = (u, 1 - v)
            mesh.materials.append(texture)
            obj = bpy.data.objects.new(mesh.name, mesh)
            bpy.context.scene.collection.objects.link(obj)
            obj.location = blender(pos)
        for child in node.findall(NS + "node"):
            node_mesh(child, pos)

    for node in root.find(f"{NS}library_visual_scenes/{NS}visual_scene").findall(NS + "node"):
        node_mesh(node, Vector((x, 0, 0)))


def preview():
    global bpy
    import bpy
    from mathutils import Vector as BVector

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 14
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1500
    scene.render.resolution_y = 850
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = "Standard"
    scene.world = bpy.data.worlds.new("Studio")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (.35, .42, .49, 1)
    scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = .38

    def box(name, center, dimensions, surface):
        bpy.ops.mesh.primitive_cube_add(size=1, location=center)
        obj = bpy.context.object
        obj.name = "GEO-" + name
        obj.dimensions = dimensions
        obj.data.materials.append(surface)

    ground = material("Sandstone platform", (.33, .29, .23))
    alternate = material("Pale sandstone platform", (.46, .41, .32))
    ink = material("Lettering", (.05, .07, .08))
    block = material("One-block reference", (.23, .30, .34))

    def text(message, at, size):
        curve = bpy.data.curves.new("Label " + message, "FONT")
        curve.body = message
        curve.align_x = "CENTER"
        curve.size = size
        obj = bpy.data.objects.new(message, curve)
        obj.data.materials.append(ink)
        scene.collection.objects.link(obj)
        obj.location = at

    def texture(name):
        mat = material(name, (.5, .5, .5))
        node = mat.node_tree.nodes.new("ShaderNodeTexImage")
        node.image = bpy.data.images.load(str(ASSETS / "Textures" / (name + ".png")))
        node.interpolation = "Closest"
        mat.node_tree.links.new(node.outputs["Color"], mat.node_tree.nodes.get("Principled BSDF").inputs["Base Color"])
        return mat

    wildlife, arthropod = texture("WildlifePalette"), texture("WildArthropods")
    models = (("Rabbit", -.9, "Rabbit", wildlife), ("SandScorpion", .3, "Sand scorpion", arthropod),
              ("Chicken", 1.5, "Chicken", wildlife), ("FiddlerCrab", 2.7, "Fiddler crab", arthropod))
    for name, x, title, tex in models:
        load_preview_model(name, x, tex)
        text(title, (x, -.72, .008), .108)

    box("Tile base", (.3, 0, -.085), (6, 2.5, .14), ground)
    for i in range(5):
        x = -2.1 + i * 1.2
        box(f"Grid tile {i + 1}", (x, 0, -.022), (1.188, 2.39, .044), alternate)
    box("One block", (-2.1, 0, .5), (1, 1, 1), block)
    text("1 game block", (-2.1, -.72, .008), .108)
    text("WILDLIFE   /   SAME GAME SCALE", (.25, -1.025, .008), .145)

    for name, pos, power, size in (("Key", (-3, -4, 6), 620, 5), ("Fill", (4, 2, 4), 330, 4)):
        lamp = bpy.data.lights.new(name, "AREA")
        lamp.energy, lamp.shape, lamp.size = power, "DISK", size
        obj = bpy.data.objects.new(name, lamp)
        scene.collection.objects.link(obj)
        obj.location = pos
        obj.rotation_euler = (BVector((0, 0, 0)) - obj.location).to_track_quat("-Z", "Y").to_euler()

    cam = bpy.data.objects.new("Scale camera", bpy.data.cameras.new("Scale camera"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.data.type, cam.data.ortho_scale = "ORTHO", 7.1
    cam.location = (.65, -5.9, 5.8)
    cam.rotation_euler = (BVector((.27, -.04, .24)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = str(OUT / "wildlife-scale.png")
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / "wildlife-scale.blend"))
    bpy.ops.render.render(write_still=True)

    # Use one common camera scale for both small creatures in a readable detail view.
    for obj in list(scene.objects):
        if obj.name.startswith(("GEO-Rabbit", "GEO-Chicken", "GEO-One", "GEO-Grid", "GEO-Tile")) or obj.type == "FONT":
            bpy.data.objects.remove(obj, do_unlink=True)
        elif obj.name.startswith("GEO-SandScorpion"):
            obj.location.x -= .96
        elif obj.name.startswith("GEO-FiddlerCrab"):
            obj.location.x -= 2.02
    box("Detail base", (0, 0, -.04), (3.2, 2.2, .08), alternate)
    text("Sand scorpion", (-.66, -.70, .008), .095)
    text("Fiddler crab", (.68, -.70, .008), .095)
    cam.data.ortho_scale = 3.0
    cam.location = (.25, -4.7, 4.7)
    cam.rotation_euler = (BVector((0, -.09, .13)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = str(OUT / "wildlife-detail.png")
    bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    args = argparse.ArgumentParser()
    args.add_argument("--preview", action="store_true")
    options = args.parse_args()
    OUT.mkdir(exist_ok=True, parents=True)
    make_atlas(ASSETS / "Textures" / "WildArthropods.png")
    for creature in (scorpion(), crab()):
        export(creature)
    if options.preview:
        preview()
