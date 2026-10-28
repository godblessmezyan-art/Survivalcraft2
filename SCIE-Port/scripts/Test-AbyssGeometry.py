import json, math, xml.etree.ElementTree as ET
from pathlib import Path
root=Path(__file__).resolve().parents[1]
parts=json.loads((root/'docs/assets/abyss-reaper/geometry.json').read_text())
for name,mesh in parts:
 for a,b,c,n,col in mesh:
  u=[b[i]-a[i] for i in range(3)];v=[c[i]-a[i] for i in range(3)]
  cross=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]]
  assert sum(cross[i]*n[i] for i in range(3))>1e-10,name
 if name.startswith('BrokenBlade'):
  volume=sum(a[0]*(b[1]*c[2]-b[2]*c[1])+a[1]*(b[2]*c[0]-b[0]*c[2])+a[2]*(b[0]*c[1]-b[1]*c[0]) for a,b,c,_,_ in mesh)/6
  assert volume>0,name
xml=ET.parse(root/'src/SCIEPort/Assets/Models/AbyssReaper.dae')
ns={'c':'http://www.collada.org/2005/11/COLLADASchema'}
for arr in xml.findall('.//c:float_array',ns):
 if 'uv-array' in arr.attrib['id']:assert all(0<=float(x)<=1 for x in arr.text.split())
print('Abyss geometry: nondegenerate faces, normals, outward closed blades and UV passed.')

names={name for name,_ in parts}
assert not any(name.startswith(('FloatingShard','ShardRift','CoreCage')) for name in names)
blade=[p for name,mesh in parts if name.startswith('BrokenBlade') for a,b,c,_,_ in mesh for p in (a,b,c)]
assert min(p[0] for p in blade)<-.7 and max(p[0] for p in blade)<.01,'Blade must point inward, not right out of frame'
assert {'VoidOuterIris-1','VoidOuterIris1','VoidInnerIris-1','VoidInnerIris1'}<=names
areas=[0.,0.]
for name,mesh in parts:
 for a,b,c,n,col in mesh:
  u=[b[i]-a[i] for i in range(3)];v=[c[i]-a[i] for i in range(3)]
  cross=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]]
  area=sum(x*x for x in cross)**.5/2;areas[0]+=area
  if col>=12:areas[1]+=area
assert areas[1]/areas[0]<.08,'Emission must stay on narrow edge/iris, not broad purple blade faces'
print('Inward blade, no shard chain, exposed two-sided iris; emissive surface ratio %.2f%% passed.'%(100*areas[1]/areas[0]))
