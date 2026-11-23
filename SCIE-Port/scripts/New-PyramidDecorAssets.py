"""Original Egyptian tomb decor for the pyramid. Pure-Python COLLADA authoring.

No Blender dependency: geometry is built from oriented boxes, cylinders, lathes
and cones, then serialized in the verified SCNET import order (one identity node,
independent p/n/uv offsets, Y_UP, per-part atlas UV). The 512x512 RGBA atlas is
written byte-for-byte. All art is original and procedural.
"""
from pathlib import Path
import math, hashlib, json, struct, zlib, xml.etree.ElementTree as ET
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'src/SCIEPort/Assets'
OUTPUT = ROOT / 'artifacts/pyramid-decor'
OUTPUT.mkdir(parents=True, exist_ok=True)

# One palette entry per 128px atlas tile (4x4 grid). Order defines tile index.
PALETTE = [
    ('Sandstone', '#C9A46B'),   # 0 warm limestone
    ('Gold', '#D9B23C'),        # 1 gilded trim
    ('Lapis', '#2A4E8C'),       # 2 lapis / blue stripe
    ('Turquoise', '#2E8B8B'),   # 3 faience
    ('Clay', '#9C5B3C'),        # 4 terracotta jar
    ('LinenWad', '#D8C9A6'),    # 5 wrapped linen / shabti
    ('Obsidian', '#2B2B31'),    # 6 dark stone / anubis
    ('Shade', '#6E5A3A'),       # 7 carved shadow
    ('Bone', '#E6DCC0'),        # 8 bone / mask face
    ('Papyrus', '#CBB887'),     # 9 scroll / offering bread
    ('Ember', '#E6612A'),       # 10 flame
    ('RedOchre', '#8E3B2A'),    # 11 red ochre painting
    ('PaleGold', '#E8CD72'),    # 12 unused tile filler
    ('DeepLapis', '#1B3663'),   # 13 unused tile filler
    ('WarmShade', '#5A462C'),   # 14 unused tile filler
    ('Dust', '#B7A276'),        # 15 unused tile filler
]

# model name -> list of parts; each part = (role, primitive, params, uv_tile)
# Primitives use local block coordinates in [0,1] x [0,1] footprint, y up.
def box(cx, cy, cz, sx, sy, sz, rot=0.0):
    return ('box', (cx, cy, cz, sx, sy, sz, rot))
def cyl(cx, cy, cz, r, h, sides=12, r2=None):
    return ('cyl', (cx, cy, cz, r, h, sides, r2))
def cone(cx, cy, cz, r, h, sides=12):
    return ('cone', (cx, cy, cz, r, h, sides, None))

# (role, primitive, uv_tile)
def part(role, prim, tile):
    return (role, prim, tile)

MODELS = {}
# Sarcophagus: closed human-shaped coffin lid; open variant lifts the lid aside.
MODELS['PyramidSarcophagus'] = [
    part('Sandstone', box(.5,.14,.5,.82,.28,.44), 0),
    part('Gold', box(.5,.30,.5,.72,.06,.36), 1),
    part('Lapis', box(.5,.34,.5,.30,.05,.22), 2),
    part('LinenWad', cone(.5,.58,.5,.26,.46,12), 5),
    part('Gold', box(.5,.40,.5,.44,.05,.12), 1),
    part('Turquoise', box(.5,.72,.5,.20,.06,.14), 3),
]
MODELS['PyramidSarcophagusOpen'] = [
    part('Sandstone', box(.5,.14,.5,.82,.28,.44), 0),
    part('Shade', box(.5,.29,.5,.66,.05,.30), 7),
    part('LinenWad', box(.5,.34,.5,.50,.12,.24), 5),
    part('Gold', box(.5,.20,.78,.72,.06,.36,0.35), 1),
    part('Lapis', box(.5,.26,.78,.30,.05,.22,0.35), 2),
    part('LinenWad', cone(.5,.50,.78,.26,.40,12), 5),
]
# Canopic jars: four variants with distinct head stoppers (human/baboon/jackal/falcon).
def canopic(head_tile, beak=False, ears=False):
    p = [
        part('Clay', cyl(.5,.20,.5,.22,.40,12,.18), 4),
        part('Gold', cyl(.5,.41,.5,.20,.05,12), 1),
        part('LinenWad', cyl(.5,.46,.5,.15,.14,12), 5),
        part(head_tile, box(.5,.60,.5,.20,.18,.18), head_tile),
    ]
    if ears:
        p += [part(head_tile, box(.40,.74,.5,.05,.16,.06), head_tile),
              part(head_tile, box(.60,.74,.5,.05,.16,.06), head_tile)]
    if beak:
        p += [part('Gold', cone(.5,.62,.60,.06,.14,8), 1)]
    return p
MODELS['PyramidCanopicHuman'] = canopic(8)      # Imsety - liver - human
MODELS['PyramidCanopicBaboon'] = canopic(7)     # Hapy - lungs - baboon
MODELS['PyramidCanopicJackal'] = canopic(6, ears=True)  # Duamutef - stomach - jackal
MODELS['PyramidCanopicFalcon'] = canopic(1, beak=True)  # Qebehsenuef - intestines - falcon
# Storage jar: clay amphora with narrow neck and two handles.
MODELS['PyramidStorageJar'] = [
    part('Clay', cyl(.5,.26,.5,.30,.52,12,.16), 4),
    part('Clay', cyl(.5,.55,.5,.16,.18,12), 4),
    part('RedOchre', cyl(.5,.66,.5,.18,.05,12), 11),
    part('Clay', box(.28,.48,.5,.10,.22,.06,0.5), 4),
    part('Clay', box(.72,.48,.5,.10,.22,.06,-0.5), 4),
]
# Shabti: mummy-shaped servant figure with crossed arms and a small basket.
MODELS['PyramidShabti'] = [
    part('LinenWad', box(.5,.16,.5,.30,.32,.22), 5),
    part('LinenWad', cone(.5,.56,.5,.18,.44,10), 5),
    part('Turquoise', box(.5,.78,.5,.16,.10,.16), 3),
    part('Gold', box(.5,.66,.5,.28,.05,.10), 1),
    part('Papyrus', box(.5,.40,.62,.20,.12,.08), 9),
]
# Anubis statue: jackal-headed guardian on a plinth.
MODELS['PyramidAnubisStatue'] = [
    part('Obsidian', box(.5,.08,.5,.42,.16,.40), 6),
    part('Obsidian', box(.5,.46,.5,.26,.64,.24), 6),
    part('Obsidian', box(.5,.84,.5,.24,.22,.22), 6),
    part('Gold', box(.5,.74,.5,.26,.04,.24), 1),
    part('Obsidian', box(.41,1.00,.5,.06,.20,.08), 6),
    part('Obsidian', box(.59,1.00,.5,.06,.20,.08), 6),
    part('Lapis', box(.43,.86,.62,.05,.04,.02), 2),
    part('Lapis', box(.57,.86,.62,.05,.04,.02), 2),
]
# Offering table: low slab with bread and a libation channel.
MODELS['PyramidOfferingTable'] = [
    part('Sandstone', box(.5,.28,.5,.74,.10,.50), 0),
    part('Sandstone', box(.5,.14,.5,.20,.28,.20), 0),
    part('Papyrus', cyl(.30,.36,.42,.09,.05,10), 9),
    part('Papyrus', cyl(.54,.36,.42,.09,.05,10), 9),
    part('Gold', box(.5,.34,.66,.60,.02,.04), 1),
]
# Brazier: stone basin on legs, with a lit flame variant.
MODELS['PyramidBrazier'] = [
    part('Obsidian', box(.5,.12,.5,.30,.24,.30), 6),
    part('Sandstone', cyl(.5,.32,.5,.28,.16,12,.34), 0),
    part('Gold', cyl(.5,.41,.5,.30,.04,12), 1),
]
MODELS['PyramidBrazierLit'] = MODELS['PyramidBrazier'] + [
    part('Ember', cone(.5,.58,.5,.18,.34,10), 10),
    part('Ember', cone(.5,.72,.5,.10,.22,8), 10),
]
# Book of the Dead scroll: rolled papyrus tied with a linen band.
MODELS['PyramidBookOfDead'] = [
    part('Papyrus', cyl(.5,.10,.5,.16,.16,12), 9),
    part('Papyrus', box(.5,.12,.5,.56,.02,.30), 9),
    part('LinenWad', box(.5,.13,.5,.06,.04,.32), 5),
    part('RedOchre', box(.36,.135,.5,.08,.01,.10), 11),
    part('Lapis', box(.64,.135,.5,.08,.01,.10), 2),
]
# Obelisk: tall tapering monument with a gilded pyramidion.
MODELS['PyramidObelisk'] = [
    part('Sandstone', box(.5,.06,.5,.44,.12,.44), 0),
    part('Sandstone', cyl(.5,.78,.5,.24,1.32,4,.16), 0),
    part('Gold', cone(.5,1.58,.5,.18,.28,4), 1),
    part('RedOchre', box(.5,.60,.63,.16,.30,.02), 11),
    part('Lapis', box(.5,1.00,.62,.10,.20,.02), 2),
]

ATLAS_SIZE = 512
TILE = 128

def write_atlas():
    raw = bytearray()
    colors = []
    for name, hx in PALETTE:
        colors.append([int(hx[i:i+2], 16) for i in (1, 3, 5)])
    for y in range(ATLAS_SIZE):
        raw.append(0)
        for x in range(ATLAS_SIZE):
            k = ((511 - y) // TILE) * 4 + x // TILE
            base = colors[k]
            u = (x % TILE) / TILE
            v = (y % TILE) / TILE
            n = math.sin(x * 12.9898 + y * 78.233) * 43758.5453
            n -= math.floor(n)
            grain = (n - .5) * 10
            vein = 6 * math.sin(u * 22 + v * 6) * math.cos(v * 17 - u * 4)
            seam = -24 if (x % TILE < 2 or y % TILE < 2) else 0
            delta = grain + vein + seam
            raw.extend(max(0, min(255, round(c + delta))) for c in base)
            raw.append(255)
    def chunk(tag, data):
        return struct.pack('>I', len(data)) + tag + data + struct.pack('>I', zlib.crc32(tag + data) & 0xffffffff)
    png = (b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', ATLAS_SIZE, ATLAS_SIZE, 8, 6, 0, 0, 0))
           + chunk(b'sRGB', b'\x00') + chunk(b'IDAT', zlib.compress(bytes(raw), 9)) + chunk(b'IEND', b''))
    path = ASSETS / 'Textures/PyramidDecor.png'
    pending = path.with_suffix('.png.pending')
    pending.write_bytes(png)
    pending.replace(path)
    return path

# ---- geometry builders: return (positions, normals, uvs) triangle soup in game space (x,z,-y) ----
def emit_box(p, tile, params, seg=2):
    cx, cy, cz, sx, sy, sz, rot = params
    hx, hy, hz = sx/2, sy/2, sz/2
    corners = [(-hx,-hy,-hz),(hx,-hy,-hz),(hx,hy,-hz),(-hx,hy,-hz),
               (-hx,-hy,hz),(hx,-hy,hz),(hx,hy,hz),(-hx,hy,hz)]
    faces = [(0,3,2,1),(4,5,6,7),(0,1,5,4),(2,3,7,6),(0,4,7,3),(1,2,6,5)]
    normals = [(0,-1,0),(0,1,0),(0,0,-1),(0,0,1),(-1,0,0),(1,0,0)]
    ca, sa = math.cos(rot), math.sin(rot)
    def xf(v):
        x, y, z = v
        xr, zr = x*ca - z*sa, x*sa + z*ca
        return (cx + xr, cy + y, cz + zr)
    def lerp(a, b, t):
        return tuple(a[i] + (b[i]-a[i])*t for i in range(len(a)))
    for fi, f in enumerate(faces):
        quad = [xf(corners[i]) for i in f]
        nrm = normals[fi]
        nr = (nrm[0]*ca - nrm[2]*sa, nrm[1], nrm[0]*sa + nrm[2]*ca)
        a, b, c, d = quad
        uvq = [(0.04,0.04),(0.96,0.04),(0.96,0.96),(0.04,0.96)]
        for i in range(seg):
            for j in range(seg):
                u0, u1 = i/seg, (i+1)/seg
                v0, v1 = j/seg, (j+1)/seg
                def surf(u, v):
                    return lerp(lerp(a,b,u), lerp(d,c,u), v), lerp(lerp(uvq[0],uvq[1],u), lerp(uvq[3],uvq[2],u), v)
                p00,uv00 = surf(u0,v0); p10,uv10 = surf(u1,v0)
                p11,uv11 = surf(u1,v1); p01,uv01 = surf(u0,v1)
                for vtx, uv in ((p00,uv00),(p10,uv10),(p11,uv11),(p00,uv00),(p11,uv11),(p01,uv01)):
                    p['pos'].append((vtx[0], vtx[2], -vtx[1]))
                    p['nrm'].append((nr[0], nr[2], -nr[1]))
                    p['uv'].append(((tile%4 + uv[0])/4, (tile//4 + uv[1])/4))

def emit_lathe(p, tile, params, cone_top=False):
    cx, cy, cz, r, h, sides, r2 = params
    rbot, rtop = r, (r2 if r2 is not None else r)
    if cone_top:
        rtop = 0.0
    # side quads
    for i in range(sides):
        a0 = 2*math.pi*i/sides
        a1 = 2*math.pi*(i+1)/sides
        p0 = (cx+rbot*math.cos(a0), cy, cz+rbot*math.sin(a0))
        p1 = (cx+rbot*math.cos(a1), cy, cz+rbot*math.sin(a1))
        p2 = (cx+rtop*math.cos(a1), cy+h, cz+rtop*math.sin(a1))
        p3 = (cx+rtop*math.cos(a0), cy+h, cz+rtop*math.sin(a0))
        mx = math.cos((a0+a1)/2); mz = math.sin((a0+a1)/2)
        nrm = (mx, (rbot-rtop)/max(h,1e-6), mz)
        ln = math.sqrt(sum(t*t for t in nrm)) or 1
        nrm = tuple(t/ln for t in nrm)
        u0, u1 = i/sides, (i+1)/sides
        if cone_top:
            quad = [(p0,0.04+u0*.92,0.04),(p1,0.04+u1*.92,0.04),
                    (p2,0.5,0.96)]
            order = (0,1,2)
        else:
            quad = [(p0,0.04+u0*.92,0.04),(p1,0.04+u1*.92,0.04),
                    (p2,0.04+u1*.92,0.96),(p3,0.04+u0*.92,0.96)]
            order = (0,1,2,0,2,3)
        for idx in order:
            vtx, u, v = quad[idx]
            p['pos'].append((vtx[0], vtx[2], -vtx[1]))
            p['nrm'].append((nrm[0], nrm[2], -nrm[1]))
            p['uv'].append(((tile%4 + u)/4, (tile//4 + v)/4))
    # bottom cap
    for i in range(sides):
        a0 = 2*math.pi*i/sides; a1 = 2*math.pi*(i+1)/sides
        c = (cx, cy, cz)
        v0 = (cx+rbot*math.cos(a0), cy, cz+rbot*math.sin(a0))
        v1 = (cx+rbot*math.cos(a1), cy, cz+rbot*math.sin(a1))
        for vtx in (c, v1, v0):
            p['pos'].append((vtx[0], vtx[2], -vtx[1]))
            p['nrm'].append((0,0,1))
            p['uv'].append(((tile%4 + .5)/4, (tile//4 + .5)/4))
    # top cap (skip for cone)
    if not cone_top and rtop > 1e-6:
        for i in range(sides):
            a0 = 2*math.pi*i/sides; a1 = 2*math.pi*(i+1)/sides
            c = (cx, cy+h, cz)
            v0 = (cx+rtop*math.cos(a0), cy+h, cz+rtop*math.sin(a0))
            v1 = (cx+rtop*math.cos(a1), cy+h, cz+rtop*math.sin(a1))
            for vtx in (c, v0, v1):
                p['pos'].append((vtx[0], vtx[2], -vtx[1]))
                p['nrm'].append((0,0,-1))
                p['uv'].append(((tile%4 + .5)/4, (tile//4 + .5)/4))

def build_parts(parts):
    meshes = []
    for role, prim, tile in parts:
        p = {'pos':[], 'nrm':[], 'uv':[]}
        kind, params = prim
        if kind == 'box':
            emit_box(p, tile, params)
        elif kind == 'cyl':
            emit_lathe(p, tile, params, cone_top=False)
        elif kind == 'cone':
            emit_lathe(p, tile, params, cone_top=True)
        meshes.append((role, p))
    return meshes

NS = 'http://www.collada.org/2005/11/COLLADASchema'
ET.register_namespace('', NS)
def e(parent, tag, attrib=None, text=None):
    attrs = {k: str(v) for k, v in (attrib or {}).items()}
    child = ET.SubElement(parent, '{'+NS+'}'+tag, attrs)
    if text is not None:
        child.text = str(text)
    return child

def export_model(name, meshes):
    root = ET.Element('{'+NS+'}COLLADA', {'version':'1.4.1'})
    asset = e(root, 'asset'); e(asset, 'unit', {'name':'meter','meter':'1'}); e(asset, 'up_axis', text='Y_UP')
    lib = e(root, 'library_geometries')
    visual = e(root, 'library_visual_scenes')
    scene = e(visual, 'visual_scene', {'id':'Scene','name':'Scene'})
    node = e(scene, 'node', {'id':name,'name':name,'type':'NODE'})
    e(node, 'matrix', {'sid':'transform'}, '1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1')
    tris_total = 0
    bounds = []
    def flat(vectors):
        out = []
        for v in vectors:
            out.extend(v)
        return out
    for idx, (role, p) in enumerate(meshes):
        pos, nrm, uv = p['pos'], p['nrm'], p['uv']
        n = len(pos)
        assert n == len(nrm) and n == len(uv) and n % 3 == 0
        tris_total += n // 3
        bounds.extend(pos)
        ident = f'{name}_g{idx}'
        geom = e(lib, 'geometry', {'id':ident,'name':role})
        m = e(geom, 'mesh')
        for suffix, vectors, stride, params in [('p',pos,3,'XYZ'),('n',nrm,3,'XYZ'),('uv',uv,2,'ST')]:
            values = flat(vectors)
            source = e(m, 'source', {'id':ident+suffix})
            e(source, 'float_array', {'id':ident+suffix+'a','count':str(len(values))}, ' '.join(format(v,'.7g') for v in values))
            acc = e(e(source, 'technique_common'), 'accessor', {'source':'#'+ident+suffix+'a','count':str(n),'stride':str(stride)})
            for param in params:
                e(acc, 'param', {'name':param,'type':'float'})
        vert = e(m, 'vertices', {'id':ident+'v'})
        e(vert, 'input', {'semantic':'POSITION','source':'#'+ident+'p'})
        tr = e(m, 'triangles', {'count':str(n//3)})
        for semantic, src, off in [('VERTEX','v',0),('NORMAL','n',1),('TEXCOORD','uv',2)]:
            e(tr, 'input', {'semantic':semantic,'source':'#'+ident+src,'offset':str(off)})
        e(tr, 'p', text=' '.join(str(j) for j in range(n) for _ in range(3)))
        e(node, 'instance_geometry', {'url':'#'+ident,'name':role})
    e(e(root, 'scene'), 'instance_visual_scene', {'url':'#Scene'})
    path = ASSETS / 'Models' / (name+'.dae')
    pending = path.with_suffix('.dae.pending')
    pending.write_bytes(ET.tostring(root, encoding='utf-8', xml_declaration=True))
    pending.replace(path)
    return {
        'name': name, 'triangles': tris_total, 'meshes': len(meshes),
        'bounds': [[min(v[i] for v in bounds) for i in range(3)], [max(v[i] for v in bounds) for i in range(3)]],
        'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
    }

def main():
    atlas = write_atlas()
    atlas_hash = hashlib.sha256(atlas.read_bytes()).hexdigest()
    reports = []
    for name, parts in MODELS.items():
        meshes = build_parts(parts)
        report = export_model(name, meshes)
        assert 100 <= report['triangles'] <= 6000, (name, report['triangles'])
        reports.append(report)
    manifest = {
        'artRevision': 'Egyptian tomb decor V1',
        'status': '待验证',
        'author': 'Original project procedural geometry and atlas',
        'generatorSha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        'textureSha256': atlas_hash,
        'textureSize': [ATLAS_SIZE, ATLAS_SIZE],
        'palette': [{'name':n,'color':h} for n,h in PALETTE],
        'models': reports,
    }
    mp = ROOT / 'docs/assets/pyramid-decor/manifest.json'
    mp.parent.mkdir(parents=True, exist_ok=True)
    mp.write_text(json.dumps(manifest, indent=2, ensure_ascii=False), encoding='utf-8')
    for r in reports:
        (OUTPUT/(r['name']+'.json')).write_text(json.dumps(r, indent=2), encoding='utf-8')
    print(f"Generated {len(reports)} Egyptian decor models, {sum(r['triangles'] for r in reports)} triangles, atlas {atlas_hash[:16]}")

if __name__ == '__main__':
    main()
