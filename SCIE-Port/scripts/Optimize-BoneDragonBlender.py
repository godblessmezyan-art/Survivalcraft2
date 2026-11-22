"""Blender 5.x authoring pipeline for the original seven-bone dragon.

Run stages through Blender MCP (or exec in Blender's Python console):
  ns = {'__file__': SCRIPT}; exec(compile(open(SCRIPT).read(), SCRIPT, 'exec'), ns)
  ns['build'](); ns['render_view']('flight'); ns['export_game']()
No third-party artwork, game data or Blender Collada add-on is required.
Game Y-up -> Blender Z-up is (x, -z, y); export applies its inverse.
"""
from pathlib import Path
import ast
import hashlib
import json
import math
import xml.etree.ElementTree as ET
import bpy
import bmesh
from mathutils import Vector
from mathutils.geometry import tessellate_polygon

ROOT = Path(__file__).resolve().parents[1]
DESIGN = ROOT / 'design/bone-dragon'
WORK = ROOT / 'artifacts/bone-dragon-blender'
ASSETS = ROOT / 'src/SCIEPort/Assets'
SCENE = 'BoneDragon-Optimized'
BASELINE = 'BoneDragon-Baseline'
NAMES = ['Body', 'WingL', 'WingR', 'Leg1', 'Leg2', 'Leg3', 'Leg4']


def baseline():
    """Read only the original geometry definitions, never its export/preview calls."""
    path = ROOT / 'scripts/New-BoneDragonAssets.py'
    tree = ast.parse(path.read_text(encoding='utf-8'))
    cutoff = next(i for i, n in enumerate(tree.body)
                  if isinstance(n, ast.FunctionDef) and n.name == 'texture')
    nodes = [n for n in tree.body[:cutoff]
             if not (isinstance(n, ast.ImportFrom) and n.module == 'PIL')]
    ns = {'__file__': str(path)}
    exec(compile(ast.Module(body=nodes, type_ignores=[]), str(path), 'exec'), ns)
    return ns


def refined():
    g = baseline()
    replace = {'OpenRib', 'Sternum', 'ShoulderBlade', 'PelvicArch', 'NeckVertebra',
               'NeckRidge', 'DragonCranium', 'LongMuzzle', 'LowerJaw', 'DeepEyeSocket',
               'ColdSoulEye', 'Eyebrow', 'SweptCrownHorn', 'CheekHorn', 'DragonFang',
               'TailVertebra', 'TailSpur', 'TailSpear', 'WingHumerus', 'WingRadius',
               'WingFinger', 'WingFingerTip', 'TornWingWeb', 'WingThumb', 'KneeJoint'}
    kept = {b: [] for b in NAMES}
    offsets = dict.fromkeys(NAMES, 0)
    parts = []
    for part in g['PARTS']:
        b, count = part['bone'], part['triangles']
        start = offsets[b]
        if part['name'] not in replace:
            kept[b].extend(g['MESHES'][b][start:start + count])
            parts.append(part)
        offsets[b] += count
    g['MESHES'], g['PARTS'] = kept, parts
    shape, bar, horn, tube = (g[k] for k in ('shape', 'bonebar', 'horn', 'tube'))

    def sweep(name, bone, points, radii, material, sides=6):
        """Closed curved bones: shared rings, continuous contour, no elbow boxes."""
        cyclic = (Vector(points[0]) - Vector(points[-1])).length < 1e-8
        if cyclic:
            points, radii = points[:-1], radii[:-1]
        verts = []
        for i, p in enumerate(points):
            tangent = Vector(points[(i + 1) % len(points)] if cyclic else points[min(i + 1, len(points) - 1)]) - Vector(points[(i - 1) % len(points)] if cyclic else points[max(0, i - 1)])
            tangent.normalize()
            u = tangent.cross(Vector((0, 0, 1)) if abs(tangent.z) < .9 else Vector((1, 0, 0))).normalized()
            v = tangent.cross(u)
            for k in range(sides):
                a = math.tau * k / sides
                verts.append(tuple(Vector(p) + radii[i] * (u * math.cos(a) + v * math.sin(a))))
        faces = [] if cyclic else [list(range(sides - 1, -1, -1)),
                                   list(range((len(points) - 1) * sides, len(points) * sides))]
        for i in range(len(points) if cyclic else len(points) - 1):
            for k in range(sides):
                a = i * sides + k
                b = i * sides + (k + 1) % sides
                faces.append([a, b, (b + sides) % len(verts), (a + sides) % len(verts)])
        shape(name, bone, verts, faces, material)

    # A barrel-shaped rib cage reads as bone from every side, with fewer joints.
    for i in range(7):
        z = -.91 + i * .28
        width = .64 - .17 * abs(z)
        for s in (-1, 1):
            points = [(0, 1.76, z), (s * width * .70, 1.65, z - .04),
                      (s * width, 1.40, z - .02), (s * width * .96, 1.16, z + .04),
                      (s * width * .62, .99, z + .12), (0, 1.04, z + .14)]
            sweep('CurvedRib', 'Body', points, [.060, .066, .061, .055, .050, .046], 2 if i % 3 == 0 else 0)
    sweep('Sternum', 'Body', [(0, 1.04, -.90), (0, .99, -.15), (0, 1.09, .93)], [.075, .09, .065], 1)
    for s in (-1, 1):
        sweep('Scapula', 'Body', [(0, 1.73, -1), (s * .37, 1.63, -1.10), (s * .67, 1.20, -.82)], [.12, .15, .11], 2)
        sweep('PelvicArch', 'Body', [(0, 1.65, 1.12), (s * .48, 1.54, 1.25), (s * .65, 1.16, 1.05)], [.14, .17, .12], 0)

    neck = [(0, 1.72, -1.17), (0, 1.90, -1.54), (0, 2.13, -1.91), (0, 2.23, -2.30)]
    sweep('CervicalSpine', 'Body', neck, [.18, .16, .14, .16], 0, 8)
    for i in range(3):
        horn('CervicalCrest', 'Body', neck[i], g['add'](neck[i], (0, .29, .12)), .085, 2)

    # Open skull architecture: cranial roof + cheek arches around true eye gaps.
    tube('CranialRoof', 'Body', [(0, 2.29, -2.20, .23, .16), (0, 2.36, -2.45, .36, .15),
                               (0, 2.24, -2.74, .25, .11)], 0, 8)
    tube('NasalBridge', 'Body', [(0, 2.10, -2.65, .26, .14), (0, 2.01, -3.05, .20, .11),
                               (0, 1.98, -3.36, .15, .10)], 2, 8)
    for s in (-1, 1):
        sweep('ZygomaticArch', 'Body', [(s * .20, 2.20, -2.22), (s * .37, 2.02, -2.43),
                                      (s * .28, 1.98, -2.76)], [.10, .10, .07], 0)
        sweep('Mandible', 'Body', [(s * .24, 2.04, -2.30), (s * .27, 1.79, -2.53),
                                 (s * .17, 1.75, -3.01), (s * .11, 1.78, -3.33)], [.085, .095, .065, .045], 0)
        # Oval socket rim and recessed, bounded dark interior; no floating cubes.
        eye = [(s * .347, 2.20 + .145 * math.sin(k * math.tau / 10),
                -2.52 + .21 * math.cos(k * math.tau / 10)) for k in range(11)]
        sweep('OrbitalRim', 'Body', eye, [.045] * 11, 2, 5)
        tube('SoulSocket', 'Body', [(s * .32, 2.19, -2.39, .048, .10),
                                  (s * .33, 2.19, -2.63, .048, .10)], 4, 6)
        sweep('SoulEye', 'Body', [(s * .385, 2.19, -2.45), (s * .385, 2.18, -2.58)], [.035, .027], 5, 6)
        sweep('BrowPlate', 'Body', [(s * .16, 2.34, -2.75), (s * .40, 2.38, -2.48),
                                  (s * .28, 2.35, -2.25)], [.07, .095, .06], 2)
        sweep('SweptCrownHorn', 'Body', [(s * .24, 2.38, -2.26), (s * .38, 2.58, -2.06),
                                       (s * .48, 2.73, -1.80), (s * .56, 2.79, -1.51)], [.13, .10, .058, .002], 2, 7)
        sweep('CheekHorn', 'Body', [(s * .28, 2.02, -2.33), (s * .48, 1.94, -2.18),
                                  (s * .62, 1.96, -1.95)], [.09, .065, .002], 1)
        for i in range(5):
            z = -3.23 + .115 * i
            x = s * (.16 + .024 * i)
            depth = .125 + (.065 if i == 3 else 0)
            horn('UpperFang', 'Body', (x, 1.93 + i * .021, z), (x * .94, 1.93 + i * .021 - depth, z - .025), .034, 2, 5)
        for i in range(3):
            horn('LowerFang', 'Body', (s * (.12 + i * .03), 1.79, -3.16 + .19 * i),
                 (s * (.12 + i * .03), 1.88, -3.17 + .19 * i), .027, 2, 5)
        # Nostril opening, modeled rather than painted onto a stretched end cap.
        sweep('Nostril', 'Body', [(s * .18, 2.04, -3.13), (s * .158, 2.02, -3.25)], [.035, .026], 4, 5)
    bar('Chin', 'Body', (-.11, 1.78, -3.30), (.11, 1.78, -3.30), .05, 2)

    tail = [(0, 1.64, 1.40), (.03, 1.59, 1.68), (.10, 1.48, 1.97), (.19, 1.35, 2.25),
            (.28, 1.26, 2.55), (.34, 1.27, 2.85), (.38, 1.35, 3.12), (.39, 1.51, 3.39), (.38, 1.65, 3.75)]
    for i in range(len(tail) - 1):
        r = .14 - i * .0135
        midpoint = tuple((a + b) / 2 for a, b in zip(tail[i], tail[i + 1]))
        sweep('CaudalVertebra', 'Body', [tail[i], midpoint, tail[i + 1]], [r * 1.15, r * .76, r], 0)
        if i < 7:
            for s in (-1, 1):
                horn('CaudalProcess', 'Body', tail[i], g['add'](tail[i], (s * (.24 - i * .023), .12, .08)), max(.025, r * .42), 1, 5)
    horn('TailLance', 'Body', tail[-1], (.38, 1.74, 3.96), .085, 2)

    for i in range(1, 5):
        knee = (0, -.46, -.12 if i < 3 else .23)
        tube('Patella', 'Leg' + str(i), [(0, -.46, knee[2] - .10, .08, .085),
                                       (0, -.46, knee[2], .14, .13),
                                       (0, -.46, knee[2] + .10, .08, .085)], 1, 6)

    # Continuous tapering fingers and scalloped/torn solid membranes, no alpha sorting.
    for side, bone in ((-1, 'WingL'), (1, 'WingR')):
        def p(x, y, z):
            return (side * x, y, z)
        elbow, palm = p(1.12, .22, .12), p(2.20, .38, -.46)
        sweep('WingUpperArm', bone, [(0, 0, 0), p(.55, .15, .02), elbow], [.14, .11, .14], 0)
        sweep('WingForearm', bone, [elbow, p(1.68, .34, -.14), palm], [.12, .085, .115], 2)
        ends = [p(3.55, .04, -.22), p(3.12, -.06, 1.04), p(2.45, -.15, 1.94), p(1.43, -.12, 2.30), p(.24, -.20, 1.34)]
        for j, end in enumerate(ends):
            mid = tuple(a * .45 + b * .55 for a, b in zip(palm, end))
            sweep('WingDigit', bone, [palm, mid, end], [.070, .048, .018], 0 if j % 2 else 2)
        for j in range(4):
            a, b = Vector(ends[j]), Vector(ends[j + 1])
            anchor = Vector(palm) + Vector((0, -.035, .015))
            def scallop(t, inset):
                edge = a.lerp(b, t)
                return edge.lerp(anchor, inset)
            boundary = [anchor, a, scallop(.25, .12), scallop(.43, .25),
                        scallop(.49, .45 if j in (0, 2) else .28),
                        scallop(.56, .20), scallop(.77, .12), b]
            count = len(boundary)
            verts = [tuple(v) for v in boundary] + [tuple(v + Vector((0, -.026, 0))) for v in boundary]
            tess = tessellate_polygon([boundary])
            faces = []
            for tri in tess:
                ix = [v if isinstance(v, int) else min(range(count), key=lambda k: (boundary[k] - v).length_squared) for v in tri]
                faces.extend([ix, [k + count for k in reversed(ix)]])
            faces.extend([[k, (k + 1) % count, (k + 1) % count + count, k + count] for k in range(count)])
            shape('ScallopedWingWeb', bone, verts, faces, 6 if j % 2 else 7)
        sweep('AlarClaw', bone, [palm, g['add'](palm, p(.10, .21, -.28)), g['add'](palm, p(.10, .30, -.51))], [.10, .065, .002], 2)
    return g


def xyz(p):
    return (p[0], -p[2], p[1])


def game(p):
    return (p[0], p[2], -p[1])


def materials():
    img = bpy.data.images.get('AbyssBoneDragon.png') or bpy.data.images.load(str(ASSETS / 'Textures/AbyssBoneDragon.png'))
    img.colorspace_settings.name = 'sRGB'
    out = []
    for i in range(8):
        name = 'MAT-BoneDragon-' + str(i)
        mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        mat.use_nodes = True
        nodes = mat.node_tree.nodes
        nodes.clear()
        tex = nodes.new('ShaderNodeTexImage'); tex.image = img; tex.interpolation = 'Closest'
        shader = nodes.new('ShaderNodeBsdfPrincipled')
        shader.inputs['Roughness'].default_value = .82
        shader.inputs['Specular IOR Level'].default_value = .2
        mat.node_tree.links.new(tex.outputs['Color'], shader.inputs['Base Color'])
        # Preview has no glow or reflective tricks absent from the game material.
        output = nodes.new('ShaderNodeOutputMaterial')
        mat.node_tree.links.new(shader.outputs['BSDF'], output.inputs['Surface'])
        out.append(mat)
    return out


def author_scene(name, data):
    if name in bpy.data.scenes:
        raise RuntimeError('Scene already exists; inspect it before rebuilding: ' + name)
    scene = bpy.data.scenes.new(name)
    bpy.context.window.scene = scene
    col = bpy.data.collections.new('COL-' + name); scene.collection.children.link(col)
    mats = materials()
    parents = {}
    for bone, (parent, pivot) in data['BONES'].items():
        obj = bpy.data.objects.new('ARM-' + name + '-' + bone, None)
        col.objects.link(obj); obj.location = xyz(pivot)
        if parent:
            obj.parent = parents[parent]
        parents[bone] = obj
        triangles = data['MESHES'][bone]
        verts = [xyz(p) for tri, _, _ in triangles for p in tri]
        mesh = bpy.data.meshes.new('MESH-' + name + '-' + bone)
        mesh.from_pydata(verts, [], [tuple(range(i, i + 3)) for i in range(0, len(verts), 3)])
        for m in mats:
            mesh.materials.append(m)
        for poly, (tri, normal, material) in zip(mesh.polygons, triangles):
            poly.material_index = material
        bm = bmesh.new(); bm.from_mesh(mesh)
        original_verts = list(bm.verts)
        offset = 0
        # Weld within each closed anatomical part. Shared palm endpoints belong to
        # overlapping solids, and must not turn five capped fingers into one fan.
        for part in (p for p in data['PARTS'] if p['bone'] == bone):
            end = offset + part['triangles'] * 3
            bmesh.ops.remove_doubles(bm, verts=original_verts[offset:end], dist=.000001)
            offset = end
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(mesh); bm.free(); mesh.update()
        geo = bpy.data.objects.new('GEO-' + name + '-' + bone, mesh)
        geo['game_bone'] = bone; geo.parent = obj; col.objects.link(geo)
        uv = mesh.uv_layers.new(name='UV-GameAtlas')
        for face in mesh.polygons:
            n = game(face.normal)
            axis = max(range(3), key=lambda j: abs(n[j])); a, c = [j for j in range(3) if j != axis]
            # Positive padded atlas coordinates use existing game's vertical convention.
            for li in face.loop_indices:
                p = game(mesh.vertices[mesh.loops[li].vertex_index].co)
                u = .1 + .8 * ((p[a] * .91 + .5) % 1)
                v = .1 + .8 * ((p[c] * .83 + .5) % 1)
                # Blender images have bottom-origin UV; PNG/game atlas uses top rows first.
                uv.data[li].uv = ((face.material_index % 4 + u) / 4, 1 - (face.material_index // 4 + v) / 2)
    setup_stage(scene)
    return scene


def setup_stage(scene):
    scene.world = bpy.data.worlds.new('WORLD-' + scene.name)
    scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.055, .075, .095, 1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value = .45
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = 1500; scene.render.resolution_y = 1100
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.view_settings.view_transform = 'Standard'
    for suffix, pos, power, size in [('Key', (4, 6, 9), 600, 6), ('Fill', (-5, 3, 4), 300, 7), ('Rim', (1, -6, 7), 700, 5)]:
        light = bpy.data.lights.new('LGT-' + scene.name + '-' + suffix, 'AREA')
        light.energy = power; light.shape = 'DISK'; light.size = size
        obj = bpy.data.objects.new(light.name, light); scene.collection.objects.link(obj)
        obj.location = pos; obj.rotation_euler = (Vector((0, 0, 1.4)) - obj.location).to_track_quat('-Z', 'Y').to_euler()
    camera = bpy.data.cameras.new('CAM-' + scene.name)
    cam = bpy.data.objects.new(camera.name, camera); scene.collection.objects.link(cam)
    camera.type = 'ORTHO'; camera.ortho_scale = 10
    scene.camera = cam


def build():
    WORK.mkdir(parents=True, exist_ok=True)
    author_scene(BASELINE, baseline())
    scene = author_scene(SCENE, refined())
    bpy.context.window.scene = scene
    for area in bpy.context.screen.areas:
        if area.type == 'VIEW_3D':
            area.spaces.active.shading.type = 'MATERIAL'
    return {'scenes': [BASELINE, SCENE], 'triangles': sum(len(o.data.polygons) for o in scene.objects if o.type == 'MESH')}


def pose(scene, flying=True, phase=0):
    for bone in NAMES:
        node = bpy.data.objects['ARM-' + scene.name + '-' + bone]
        node.rotation_euler = (0, 0, 0)
        if bone.startswith('Wing'):
            # Conjugate game Rz into Blender space (negative Blender Y).
            node.rotation_euler.y = -(-1 if bone == 'WingL' else 1) * (.20 + .35 * math.sin(phase) if flying else 1.12)
        elif bone.startswith('Leg'):
            node.rotation_euler.x = .32 if flying else 0
    bpy.context.view_layer.update()


def render_view(view='flight', before=False):
    scene = bpy.data.scenes[BASELINE if before else SCENE]
    bpy.context.window.scene = scene
    pose(scene, flying=view not in ('ground', 'side'))
    cam = scene.camera
    target = Vector((0, -.1, 1.4))
    pos = {'flight': (8, 11, 7), 'ground': (8, 11, 4.7), 'side': (12, 0, 3.5),
           'front': (0, 14, 4), 'back': (0, -14, 4), 'top': (0, 0, 15), 'head': (5, 9, 4.5)}[view]
    if view == 'head':
        target = Vector((0, 2.65, 2.20))
    cam.location = pos
    cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam.data.ortho_scale = 3.1 if view == 'head' else 10.4 if view in ('flight', 'top') else 8.8
    if view != 'head':
        bpy.context.view_layer.update()
        rotation = cam.rotation_euler.to_matrix()
        points = [rotation.transposed() @ (o.matrix_world @ v.co - cam.location)
                  for o in scene.objects if o.type == 'MESH' for v in o.data.vertices]
        xmin, xmax = min(p.x for p in points), max(p.x for p in points)
        ymin, ymax = min(p.y for p in points), max(p.y for p in points)
        cam.location += rotation @ Vector(((xmin + xmax) / 2, (ymin + ymax) / 2, 0))
        cam.data.ortho_scale = max(xmax - xmin, (ymax - ymin) * scene.render.resolution_x / scene.render.resolution_y) * 1.16
    path = DESIGN / ('blender-' + ('before-' if before else '') + view + '.png')
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    if not before:
        directory = DESIGN / 'validation'
        directory.mkdir(exist_ok=True)
        report_path = directory / 'multiview_fit_report.json'
        report = json.loads(report_path.read_text()) if report_path.exists() else {'kind': 'Blender framing audit, not gameplay', 'views': {}}
        report['views'][view] = {'render': path.name, 'kind': 'head detail crop' if view == 'head' else 'whole model',
                                 'camera_scale': cam.data.ortho_scale}
        if view != 'head':
            report['views'][view].update(camera_fit(scene))
        report_path.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    return {'path': str(path), 'kind': 'Blender preview, not gameplay'}


def camera_fit(scene):
    from bpy_extras.object_utils import world_to_camera_view
    bpy.context.view_layer.update()
    points = [world_to_camera_view(scene, scene.camera, o.matrix_world @ v.co)
              for o in scene.objects if o.type == 'MESH' for v in o.data.vertices]
    bounds = [min(p.x for p in points), min(p.y for p in points),
              max(p.x for p in points), max(p.y for p in points)]
    margin = min(bounds[0], bounds[1], 1 - bounds[2], 1 - bounds[3])
    if margin < 0 or not all(math.isfinite(v) for v in bounds):
        raise RuntimeError('Clipped or nonfinite pose: ' + str(bounds))
    return {'normalized_bounds': bounds, 'minimum_margin': margin, 'whole_model_in_frame': True}


def render_pose_frames():
    """Sample the existing C# wing formula; this does not add game animation."""
    scene = bpy.data.scenes[SCENE]
    bpy.context.window.scene = scene
    scene.render.resolution_x = 900; scene.render.resolution_y = 660
    cam = scene.camera
    cam.location = (8, 11, 7)
    cam.rotation_euler = (Vector((0, -.1, 1.4)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam.data.ortho_scale = 10.4
    paths, checks = [], []
    for i in range(12):
        phase = i * math.tau / 12
        pose(scene, phase=phase)
        path = WORK / f'flight-{i:02}.png'
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        paths.append(str(path))
        checks.append({'phase': phase, 'wing_angle': .20 + .35 * math.sin(phase), **camera_fit(scene)})
    scene.render.resolution_x = 1500; scene.render.resolution_y = 1100
    pose(scene)
    (DESIGN / 'validation/pose_report.json').write_text(json.dumps({'kind': 'Existing animation formula sampled in Blender, not gameplay',
        'flight_frames': checks, 'landing_angle': 1.12, 'landing_view': 'blender-ground.png',
        'rider_gameplay': '待验证', 'bone_pivots_and_animation_code': 'unchanged'}, indent=2) + '\n', encoding='utf-8')
    return {'frames': paths, 'formula': '.20+.35*sin(phase)', 'kind': 'Blender pose test, not gameplay'}


def export_game():
    scene = bpy.data.scenes[SCENE]
    bpy.context.window.scene = scene
    validation = validate()
    data = baseline()
    triangles = {}
    total = 0
    # Export bone-local rest geometry, independent of the currently displayed pose.
    for bone in NAMES:
        obj = bpy.data.objects['GEO-' + SCENE + '-' + bone]
        mesh = obj.data; mesh.calc_loop_triangles()
        rows = []
        for tri in mesh.loop_triangles:
            face = mesh.polygons[tri.polygon_index]
            ps = [game(mesh.vertices[i].co) for i in tri.vertices]
            normal = tuple((Vector(ps[1]) - Vector(ps[0])).cross(Vector(ps[2]) - Vector(ps[0])).normalized())
            uvs = [(mesh.uv_layers.active.data[li].uv.x, 1 - mesh.uv_layers.active.data[li].uv.y) for li in tri.loops]
            rows.append((ps, normal, uvs))
        triangles[bone] = rows; total += len(rows)
    if total > 7674:
        raise RuntimeError('Triangle budget exceeds original Android model')
    ns = 'http://www.collada.org/2005/11/COLLADASchema'
    ET.register_namespace('', ns)
    def e(parent, tag, attrs=None, text=None):
        obj = ET.SubElement(parent, '{' + ns + '}' + tag, attrs or {}); obj.text = text; return obj
    doc = ET.Element('{' + ns + '}COLLADA', {'version': '1.4.1'})
    asset = e(doc, 'asset'); e(asset, 'unit', {'name': 'meter', 'meter': '1'}); e(asset, 'up_axis', text='Y_UP')
    lib = e(doc, 'library_geometries')
    for bone, rows in triangles.items():
        name = 'BoneDragon_' + bone
        mesh = e(e(lib, 'geometry', {'id': name, 'name': name}), 'mesh')
        pos = [v for ps, n, uv in rows for p in ps for v in p]
        normals = [v for ps, n, uv in rows for p in ps for v in n]
        uvs = [v for ps, n, uv in rows for p in uv for v in p]
        for suffix, values, stride, fields in [('pos', pos, 3, 'XYZ'), ('normal', normals, 3, 'XYZ'), ('uv', uvs, 2, 'ST')]:
            sid = name + '_' + suffix
            source = e(mesh, 'source', {'id': sid})
            e(source, 'float_array', {'id': sid + '_array', 'count': str(len(values))}, ' '.join(f'{v:.7g}' for v in values))
            accessor = e(e(source, 'technique_common'), 'accessor', {'source': '#' + sid + '_array', 'count': str(len(values) // stride), 'stride': str(stride)})
            for f in fields:
                e(accessor, 'param', {'name': f, 'type': 'float'})
        e(e(mesh, 'vertices', {'id': name + '_vertices'}), 'input', {'semantic': 'POSITION', 'source': '#' + name + '_pos'})
        tri = e(mesh, 'triangles', {'count': str(len(rows))})
        for offset, (semantic, suffix) in enumerate([('VERTEX', 'vertices'), ('NORMAL', 'normal'), ('TEXCOORD', 'uv')]):
            attrs = {'semantic': semantic, 'source': '#' + name + '_' + suffix, 'offset': str(offset)}
            if offset == 2:
                attrs['set'] = '0'
            e(tri, 'input', attrs)
        e(tri, 'p', text=' '.join(str(i) for i in range(len(rows) * 3) for _ in range(3)))
    visual = e(e(doc, 'library_visual_scenes'), 'visual_scene', {'id': 'Scene'})
    nodes = {}
    for bone, (parent, p) in data['BONES'].items():
        nodes[bone] = e(nodes[parent] if parent else visual, 'node', {'id': bone, 'name': bone, 'type': 'NODE'})
        e(nodes[bone], 'matrix', {'sid': 'transform'}, f'1 0 0 {p[0]} 0 1 0 {p[1]} 0 0 1 {p[2]} 0 0 0 1')
        e(nodes[bone], 'instance_geometry', {'url': '#BoneDragon_' + bone})
    e(e(doc, 'scene'), 'instance_visual_scene', {'url': '#Scene'})
    ET.indent(doc)
    path = ASSETS / 'Models/AbyssBoneDragon.dae'
    ET.ElementTree(doc).write(path, encoding='utf-8', xml_declaration=True)
    pose(scene)
    for image in bpy.data.images:
        if image.name == 'AbyssBoneDragon.png':
            image.pack()
    for area in bpy.context.screen.areas:
        if area.type == 'VIEW_3D':
            area.spaces.active.shading.type = 'MATERIAL'
            area.spaces.active.region_3d.view_perspective = 'CAMERA'
    render_camera = scene.camera
    render_camera.location = (8, 11, 7)
    render_camera.rotation_euler = (Vector((0, -.1, 1.4)) - render_camera.location).to_track_quat('-Z', 'Y').to_euler()
    render_camera.data.ortho_scale = 10.4
    bpy.ops.wm.save_as_mainfile(filepath=str(WORK / 'AbyssBoneDragon.blend'))
    report = {'original': True, 'source': 'scripts/Optimize-BoneDragonBlender.py',
              'baseline_source': 'scripts/New-BoneDragonAssets.py', 'game_meshes_used': False,
              'bones': {b: {'parent': parent, 'pivot': p} for b, (parent, p) in data['BONES'].items()},
              'meshes': 7, 'triangles': total, 'baseline_triangles': 7674,
              'bounds': validation['rest_bounds'], 'parts': refined()['PARTS'],
              'assets': {p.relative_to(ROOT).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest().upper()
                         for p in (path, ASSETS / 'Textures/AbyssBoneDragon.png')}}
    (DESIGN / 'manifest.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    return {'triangles': total, 'dae': str(path), 'blend': str(WORK / 'AbyssBoneDragon.blend'),
            'sha256': hashlib.sha256(path.read_bytes()).hexdigest().upper()}


def validate():
    """Fail export for open/degenerate meshes, broken palette UVs or changed pivots."""
    data = baseline()
    scene = bpy.data.scenes[SCENE]
    rows, points, pivots = [], [], []
    for bone in NAMES:
        obj = bpy.data.objects['GEO-' + SCENE + '-' + bone]
        mesh = obj.data
        node = bpy.data.objects['ARM-' + SCENE + '-' + bone]
        pivot = data['BONES'][bone][1]
        error = (node.location - Vector(xyz(pivot))).length
        pivots.append({'bone': bone, 'local_pivot': pivot, 'error': error})
        if error > 1e-6 or obj.location.length > 1e-6 or (obj.scale - Vector((1, 1, 1))).length > 1e-6:
            raise RuntimeError('Changed bone pivot or unapplied mesh transform: ' + bone)
        bm = bmesh.new(); bm.from_mesh(mesh)
        row = {'bone': bone, 'vertices': len(mesh.vertices), 'triangles': len(mesh.polygons),
               'boundary_edges': sum(e.is_boundary for e in bm.edges),
               'nonmanifold_edges': sum(not e.is_manifold for e in bm.edges),
               'zero_area_faces': sum(f.calc_area() < 1e-10 for f in bm.faces),
               'signed_volume': bm.calc_volume(signed=True)}
        bm.free()
        if row['boundary_edges'] or row['nonmanifold_edges'] or row['zero_area_faces'] or row['signed_volume'] <= 0:
            raise RuntimeError('Invalid closed geometry: ' + str(row))
        for face in mesh.polygons:
            for li in face.loop_indices:
                u, v = mesh.uv_layers.active.data[li].uv
                if not (0 < u < 1 and 0 < v < 1):
                    raise RuntimeError('Out-of-range UV')
                if int(u * 4) + int((1 - v) * 2) * 4 != face.material_index:
                    raise RuntimeError('UV crosses palette regions')
        rows.append(row)
        origin = Vector(data['origin'](bone))
        points.extend(Vector(game(v.co)) + origin for v in mesh.vertices)
    total = sum(row['triangles'] for row in rows)
    assert total <= 7674 and len(rows) == 7
    report = {'kind': 'Blender geometry validation, not gameplay', 'blender': bpy.app.version_string,
              'meshes': rows, 'triangles': total, 'reduction_percent': round((1 - total / 7674) * 100, 2),
              'rest_bounds': {'min': [min(p[j] for p in points) for j in range(3)],
                              'max': [max(p[j] for p in points) for j in range(3)]},
              'uv_faces_checked': total, 'palette_region_coverage': 'all face corners, including caps/sides',
              'rider_mount_offset': [0, 2.1, .16], 'gameplay_changes': False}
    (DESIGN / 'registration_report.json').write_text(json.dumps({'coordinate_mapping': '(x,y,z) game -> (x,-z,y) Blender',
        'scale': 1, 'pivots': pivots, 'bone_order': NAMES, 'seat_geometry': 'unchanged baseline saddle and harness',
        'head_neck_tail_animation': 'rigid Body; no extra shader matrices'}, indent=2) + '\n', encoding='utf-8')
    (DESIGN / 'atlas_regions.json').write_text(json.dumps({'image': 'AbyssBoneDragon.png', 'size': [512, 256],
        'origin': 'PNG/game top-left; Blender V inverted explicitly on import/export',
        'regions': [{'index': i, 'name': n, 'game_uv_rect': [i % 4 / 4, i // 4 / 2, (i % 4 + 1) / 4, (i // 4 + 1) / 2]}
                    for i, n in enumerate(['bone', 'joint', 'ivory', 'claw-saddle', 'socket', 'soul', 'membrane-a', 'membrane-b'])]}, indent=2) + '\n', encoding='utf-8')
    (DESIGN / 'validation').mkdir(exist_ok=True)
    (DESIGN / 'validation/geometry_report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    return report


if globals().get('__name__') == '__main__':
    build()
    for view in ('flight', 'ground', 'side', 'front', 'back', 'top', 'head'):
        render_view(view)
    render_view('flight', before=True)
    render_pose_frames()
    print(json.dumps(export_game(), indent=2))
