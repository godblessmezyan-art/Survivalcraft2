"""Validate exported rest geometry independently of Blender and the C# runtime."""
from pathlib import Path
import math, xml.etree.ElementTree as E
ROOT=Path(__file__).resolve().parents[1]/'src/SCIEPort/Assets/Models'
ns={'c':'http://www.collada.org/2005/11/COLLADASchema'}
checks=0
for name in ['Fox','Rabbit','Chicken','Cat']:
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
print('Wildlife triangle checks:',checks)
