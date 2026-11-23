"""Static export gates for Egyptian tomb decor, complemented by target Engine import."""
from pathlib import Path
import hashlib, json, math, xml.etree.ElementTree as ET
from PIL import Image
ROOT = Path(__file__).resolve().parents[1]
manifest = json.loads((ROOT/'docs/assets/pyramid-decor/manifest.json').read_text(encoding='utf-8'))
assert hashlib.sha256((ROOT/'scripts/New-PyramidDecorAssets.py').read_bytes()).hexdigest() == manifest['generatorSha256'], 'Authoring script fingerprint changed; regenerate/save the manifest.'
ns = {'c': 'http://www.collada.org/2005/11/COLLADASchema'}
entries = manifest['models']
assert len(entries) == 20
triangles = 0
for entry in entries:
    path = ROOT/'src/SCIEPort/Assets/Models'/(entry['name']+'.dae')
    assert hashlib.sha256(path.read_bytes()).hexdigest() == entry['sha256'], path
    root = ET.parse(path).getroot()
    assert root.find('c:asset/c:up_axis', ns).text == 'Y_UP'
    nodes = root.findall('c:library_visual_scenes/c:visual_scene/c:node', ns)
    assert len(nodes) == 1
    parts = nodes[0].findall('c:node', ns)
    assert len(parts) + 1 == entry['bones']
    footprint = entry.get('footprint', [1,1,1])
    width, height, depth = footprint
    owners = {}
    for part in parts:
        name = part.attrib['name']
        assert name.startswith('Part') and 0 <= int(name[4:]) < math.prod(footprint)
        for instance in part.findall('c:instance_geometry', ns):
            owners[instance.attrib['url'][1:]] = int(name[4:])
    assert [float(x) for x in nodes[0].find('c:matrix', ns).text.split()] == [1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]
    geoms = root.findall('c:library_geometries/c:geometry', ns)
    assert len(geoms) == entry['meshes']
    count = 0
    model_positions = []
    for geom in geoms:
        mesh = geom.find('c:mesh', ns); sources = {}
        for source in mesh.findall('c:source', ns):
            values = [float(x) for x in source.find('c:float_array', ns).text.split()]
            accessor = source.find('c:technique_common/c:accessor', ns)
            stride = int(accessor.attrib['stride']); size = int(accessor.attrib['count'])
            assert len(values) == size*stride and all(math.isfinite(x) for x in values)
            sources[source.attrib['id']] = (values, stride, size)
        tris = mesh.find('c:triangles', ns); n = int(tris.attrib['count']); count += n
        assert [int(i.attrib['offset']) for i in tris.findall('c:input', ns)] == [0,1,2]
        indices = [int(x) for x in tris.find('c:p', ns).text.split()]
        assert len(indices) == n*9
        for suffix, stride, offset in [('p',3,0),('n',3,1),('uv',2,2)]:
            values, actualstride, size = next(v for k,v in sources.items() if k == geom.attrib['id']+suffix)
            assert actualstride == stride and all(0 <= i < size for i in indices[offset::3])
            if suffix == 'uv':
                assert all(0 <= x <= 1 for x in values)
                for axis in range(2):
                    coords = values[axis::2]
                    assert max(coords)-min(coords) <= .23001, 'Default UV leaked across atlas tiles: '+geom.attrib['name']
        positions = sources[geom.attrib['id']+'p'][0]
        normals = sources[geom.attrib['id']+'n'][0]
        model_positions.extend(zip(positions[::3], positions[1::3], positions[2::3]))
        if parts:
            cell = owners[geom.attrib['id']]
            origin = (cell%width-.5,cell//(width*depth),cell//width%depth-.5)
            assert all(-1e-6 <= value - origin[i%3] <= 1.000001 for i,value in enumerate(positions)), entry['name']
        for i in range(n):
            a,b,c = [positions[(i*3+j)*3:(i*3+j+1)*3] for j in range(3)]
            u = [b[j]-a[j] for j in range(3)]; v = [c[j]-a[j] for j in range(3)]
            cross = (u[1]*v[2]-u[2]*v[1], u[2]*v[0]-u[0]*v[2], u[0]*v[1]-u[1]*v[0])
            assert sum(x*x for x in cross) > 1e-20, 'Degenerate triangle: '+geom.attrib['name']
            for corner in range(3):
                normal = normals[(i*3+corner)*3:(i*3+corner+1)*3]
                assert sum(x*y for x,y in zip(cross, normal)) > 0, 'Normal disagrees with winding: '+geom.attrib['name']
    assert abs(min(y for _,y,_ in model_positions)) < 1e-5, 'Expected a floor origin: '+entry['name']
    assert all(-.5 < x < width-.5 and -.5 < z < depth-.5 and 0 <= y <= height
               for x,y,z in model_positions), entry['name']
    assert count == entry['triangles'] and 100 <= count <= 1600
    triangles += count
texture = ROOT/'src/SCIEPort/Assets/Textures/PyramidDecor.png'
assert hashlib.sha256(texture.read_bytes()).hexdigest() == manifest['textureSha256']
with Image.open(texture) as image:
    assert image.size == (512,512) and image.convert('RGBA').getextrema()[3] == (255,255)
print(f'Pyramid decor COLLADA, independent indices, finite/nondegenerate geometry, per-part atlas UV and opaque PNG: {len(entries)} models / {triangles} triangles passed.')
