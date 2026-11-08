"""Static export gates complement the actual target Engine import in Test-HomeFurniture.ps1."""
from pathlib import Path
import hashlib,json,math,xml.etree.ElementTree as ET
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]
manifest=json.loads((ROOT/'docs/assets/home-furniture/manifest.json').read_text(encoding='utf-8'))
assert hashlib.sha256((ROOT/'scripts/New-HomeFurnitureAssets.py').read_bytes()).hexdigest()==manifest['generatorSha256'], 'Authoring script fingerprint changed; regenerate/save the manifest.'
extension=json.loads((ROOT/'docs/assets/home-extension/manifest.json').read_text(encoding='utf-8'))
assert hashlib.sha256((ROOT/'scripts/New-HomeExtensionAssets.py').read_bytes()).hexdigest()==extension['generatorSha256'], 'Extension authoring fingerprint changed; regenerate/save.'
assert extension['baseGeneratorSha256']==manifest['generatorSha256']
assert extension['textureSha256']==manifest['textureSha256'], 'Both collections must use the same atlas bytes.'
ns={'c':'http://www.collada.org/2005/11/COLLADASchema'}
assert len(manifest['models'])==16
assert len(extension['models'])==8
entries=manifest['models']+extension['models']
triangles=0
for entry in entries:
    path=ROOT/'src/SCIEPort/Assets/Models'/ (entry['name']+'.dae')
    assert hashlib.sha256(path.read_bytes()).hexdigest()==entry['sha256'],path
    root=ET.parse(path).getroot()
    assert root.find('c:asset/c:up_axis',ns).text=='Y_UP'
    nodes=root.findall('c:library_visual_scenes/c:visual_scene/c:node',ns)
    assert len(nodes)==1 and not nodes[0].findall('c:node',ns)
    assert [float(x) for x in nodes[0].find('c:matrix',ns).text.split()]==[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]
    geoms=root.findall('c:library_geometries/c:geometry',ns)
    assert len(geoms)==entry['meshes']
    count=0
    for geom in geoms:
        mesh=geom.find('c:mesh',ns); sources={}
        for source in mesh.findall('c:source',ns):
            values=[float(x) for x in source.find('c:float_array',ns).text.split()]
            accessor=source.find('c:technique_common/c:accessor',ns)
            stride=int(accessor.attrib['stride']); size=int(accessor.attrib['count'])
            assert len(values)==size*stride and all(math.isfinite(x) for x in values)
            sources[source.attrib['id']]=(values,stride,size)
        tris=mesh.find('c:triangles',ns); n=int(tris.attrib['count']); count+=n
        assert [int(i.attrib['offset']) for i in tris.findall('c:input',ns)]==[0,1,2]
        indices=[int(x) for x in tris.find('c:p',ns).text.split()]
        assert len(indices)==n*9
        for suffix,stride,offset in [('p',3,0),('n',3,1),('uv',2,2)]:
            values,actualstride,size=next(v for k,v in sources.items() if k==geom.attrib['id']+suffix)
            assert actualstride==stride and all(0<=i<size for i in indices[offset::3])
            if suffix=='uv':
                assert all(0<=x<=1 for x in values)
                for axis in range(2):
                    coords=values[axis::2]
                    assert max(coords)-min(coords)<=.23001, 'Default UV leaked across atlas tiles: '+geom.attrib['name']
        positions=sources[geom.attrib['id']+'p'][0]
        for i in range(n):
            a,b,c=[positions[(i*3+j)*3:(i*3+j+1)*3] for j in range(3)]
            u=[b[j]-a[j] for j in range(3)]; v=[c[j]-a[j] for j in range(3)]
            area=sum(x*x for x in (u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]))
            assert area>1e-20, 'Degenerate triangle: '+geom.attrib['name']
    assert count==entry['triangles'] and 100<=count<=6000
    triangles+=count
texture=ROOT/'src/SCIEPort/Assets/Textures/HomeFurniture.png'
assert hashlib.sha256(texture.read_bytes()).hexdigest()==manifest['textureSha256']
with Image.open(texture) as image:
    assert image.size==(512,512) and image.convert('RGBA').getextrema()[3]==(255,255)
print(f'Home COLLADA, independent indices, finite/nondegenerate geometry, per-part atlas UV and opaque PNG: {len(entries)} models / {triangles} triangles passed.')
