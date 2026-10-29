"""Candidate asset integrity; reads DAE/PNG and all mesh topology, not runtime code."""
from pathlib import Path
import json, math, hashlib, xml.etree.ElementTree as ET
from collections import defaultdict,Counter
from PIL import Image

ROOT=Path(__file__).resolve().parent;ASSETS=ROOT/'assets'
parts=json.loads((ASSETS/'geometry.json').read_text());manifest=json.loads((ASSETS/'manifest.json').read_text())
def sub(a,b):return [x-y for x,y in zip(a,b)]
def cross(a,b):return [a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]]
def dot(a,b):return sum(x*y for x,y in zip(a,b))
def key(p):return tuple(round(x,8) for x in p)
report=[];total=0
for name,mesh in parts:
    edges=defaultdict(list);volume=0
    for i,(a,b,c,n,col) in enumerate(mesh):
        assert all(math.isfinite(v) for p in (a,b,c,n) for v in p),(name,'nonfinite')
        normal=cross(sub(b,a),sub(c,a));norm=math.sqrt(dot(normal,normal))
        assert norm>1e-10,(name,'degenerate')
        assert abs(dot(n,n)-1)<1e-8 and dot(normal,n)/norm>0.999999,(name,'normal')
        assert isinstance(col,int) and 0<=col<16
        volume+=dot(a,cross(b,c))/6
        for p,q in ((a,b),(b,c),(c,a)):
            p,q=key(p),key(q);edges[tuple(sorted([p,q]))].append((p,q,i))
    bad=[edge for edge,uses in edges.items() if len(uses)!=2 or uses[0][:2] != tuple(reversed(uses[1][:2]))]
    assert not bad,(name,'nonmanifold or inconsistent edge winding',len(bad),bad[:2])
    assert volume>0,(name,'inward')
    report.append({'name':name,'triangles':len(mesh),'signedVolume':volume,'closedConsistent':True})
    total+=len(mesh)
assert total==manifest['triangles'] and total<12000
ns={'c':'http://www.collada.org/2005/11/COLLADASchema'}
doc=ET.parse(ASSETS/'AbyssEclipseCandidate.dae')
assert doc.find('.//c:up_axis',ns).text=='Y_UP'
nodes=doc.findall('.//c:node',ns);assert len(nodes)==1
assert list(map(float,nodes[0].find('c:matrix',ns).text.split()))==[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]
geos=doc.findall('.//c:geometry',ns);assert len(geos)==len(parts)
assert len(nodes[0].findall('c:instance_geometry',ns))==len(geos)
for geo,(name,mesh) in zip(geos,parts):
    assert geo.attrib['id']==name
    for source in geo.findall('c:mesh/c:source',ns):
        array=source.find('c:float_array',ns);values=list(map(float,array.text.split()))
        assert len(values)==int(array.attrib['count']) and all(math.isfinite(v) for v in values)
        if source.attrib['id'].endswith('-uv'):assert all(0<v<1 for v in values)
    triangles=geo.find('c:mesh/c:triangles',ns);assert int(triangles.attrib['count'])==len(mesh)
    ix=list(map(int,triangles.find('c:p',ns).text.split()))
    assert len(ix)==len(mesh)*9 and min(ix)==0 and max(ix)==len(mesh)*3-1
png=Image.open(ASSETS/'AbyssEclipseCandidate.png').convert('RGBA')
assert png.size==(256,16) and png.getchannel('A').getextrema()==(255,255)
for filename,expected in manifest['sha256'].items():assert hashlib.sha256((ASSETS/filename).read_bytes()).hexdigest().upper()==expected
framing=json.loads((ROOT/'previews/framing.json').read_text())
assert all(v['bladeWithinIdleFrustum'] for v in framing.values())
result={'status':'passed','triangles':total,'meshes':len(parts),'topology':report,
        'axis':'Y_UP','root':'identity','texture':'opaque RGBA 256x16','idleFramingFov':[65,75,85],
        'scope':'Offline candidate integrity, NOT Android/game acceptance.'}
(ROOT/'validation.json').write_text(json.dumps(result,indent=2),encoding='utf8',newline='\n')
print(f'PASS: {len(parts)} closed consistently oriented meshes / {total} triangles; DAE, PNG and idle framing valid.')
