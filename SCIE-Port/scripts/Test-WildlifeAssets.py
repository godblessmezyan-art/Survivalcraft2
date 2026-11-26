"""Validate exported rest geometry independently of Blender and the C# runtime."""
from pathlib import Path
import math, xml.etree.ElementTree as E
ROOT=Path(__file__).resolve().parents[1]/'src/SCIEPort/Assets/Models'
ns={'c':'http://www.collada.org/2005/11/COLLADASchema'}
checks=0
for name in ['Fox','Rabbit','Chicken','Cat','SandScorpion','FiddlerCrab']:
    r=E.parse(ROOT/(name+'.dae')).getroot()
    assert r.find('c:asset/c:up_axis',ns).text=='Y_UP'
    for mesh in r.findall('.//c:geometry/c:mesh',ns):
        sources={s.get('id'):[float(v) for v in s.find('c:float_array',ns).text.split()] for s in mesh.findall('c:source',ns)}
        tri=mesh.find('c:triangles',ns)
        vertices=mesh.find('c:vertices/c:input',ns).get('source')[1:]
        normal=next(x for x in tri.findall('c:input',ns) if x.get('semantic')=='NORMAL').get('source')[1:]
        uv=next(x for x in tri.findall('c:input',ns) if x.get('semantic')=='TEXCOORD').get('source')[1:]
        pos=sources[vertices];norm=sources[normal];tex=sources[uv]
        assert all(math.isfinite(v) for values in sources.values() for v in values)
        assert all(0<=v<=1 for v in tex)
        indices=[int(x) for x in tri.find('c:p',ns).text.split()]
        assert len(indices)==int(tri.get('count'))*9
        for offset in range(0,len(indices),9):
            p=[pos[indices[offset+j]*3:indices[offset+j]*3+3] for j in [0,3,6]]
            u=[p[1][k]-p[0][k] for k in range(3)];v=[p[2][k]-p[0][k] for k in range(3)]
            cross=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]]
            assert sum(c*c for c in cross)>1e-18
            n=norm[indices[offset+1]*3:indices[offset+1]*3+3]
            assert abs(sum(x*x for x in n)-1)<1e-5
            assert sum(a*b for a,b in zip(cross,n))>0
            checks+=1
    print(name,'finite UV/normal/winding/nondegenerate geometry passed')
def dimensions(name):
    r = E.parse(ROOT / (name + '.dae')).getroot()
    meshes = {g.get('id'): g.find('c:mesh', ns) for g in r.findall('.//c:geometry', ns)}
    points = []

    def visit(node, origin):
        transform = list(map(float, node.find('c:matrix', ns).text.split()))
        position = [origin[i] + transform[4 * i + 3] for i in range(3)]
        mesh_ref = node.find('c:instance_geometry', ns)
        if mesh_ref is not None:
            mesh = meshes[mesh_ref.get('url')[1:]]
            source_id = mesh.find('c:vertices/c:input', ns).get('source')[1:]
            source = next(s for s in mesh.findall('c:source', ns) if s.get('id') == source_id)
            xyz = list(map(float, source.find('c:float_array', ns).text.split()))
            points.extend([xyz[j + i] + position[i] for i in range(3)] for j in range(0, len(xyz), 3))
        for child in node.findall('c:node', ns):
            visit(child, position)

    for node in r.findall('.//c:visual_scene/c:node', ns):
        visit(node, (0, 0, 0))
    assert 0 <= min(p[1] for p in points) < .015, name + ' feet nearly touch the ground'
    return tuple(max(p[i] for p in points) - min(p[i] for p in points) for i in range(3))

sx, sy, sz = dimensions('SandScorpion')
cx, cy, cz = dimensions('FiddlerCrab')
assert .78 <= sx <= .91 and .43 <= sy <= .53 and .90 <= sz <= 1.04, 'scorpion body envelope'
assert .44 <= cx <= .55 and .20 <= cy <= .28 and .35 <= cz <= .47, 'crab body envelope'
assert cy < .55 * sy and cx < .65 * sx, 'crab reads as the smaller creature'
print('Wildlife triangle checks:', checks)
print(f'Scorpion rest bounds {sx:.3f} x {sy:.3f} x {sz:.3f}; crab {cx:.3f} x {cy:.3f} x {cz:.3f}')
