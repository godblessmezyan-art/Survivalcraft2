"""Original Abyss V0.1.1: faceted dark steel, inward crescent and recessed void iris.
One identity root; rotation baked; standard library only. No game assets.
"""
from pathlib import Path
import importlib.util, math, struct, zlib, xml.etree.ElementTree as ET
spec=importlib.util.spec_from_file_location('primitives',Path(__file__).with_name('New-OmniscientEyeAssets.py'))
e=importlib.util.module_from_spec(spec);spec.loader.exec_module(e)
e.parts.clear()
def tri(mesh,a,b,c,col):e.triangle(mesh,a,b,c,col)
def quad(mesh,a,b,c,d,col):tri(mesh,a,b,c,col);tri(mesh,a,c,d,col)
def prism(name,y0,y1,r0,r1,cols,n=8):
 mesh=[]
 for i in range(n):
  a=2*math.pi*i/n;b=2*math.pi*(i+1)/n
  p=(r0*math.cos(a),y0,r0*math.sin(a));q=(r0*math.cos(b),y0,r0*math.sin(b))
  u=(r1*math.cos(a),y1,r1*math.sin(a));v=(r1*math.cos(b),y1,r1*math.sin(b))
  quad(mesh,p,u,v,q,cols[i%len(cols)])
  tri(mesh,(0,y0,0),p,q,cols[0]);tri(mesh,(0,y1,0),v,u,cols[0])
 e.parts.append((name,mesh))
def ring(name,cx,cy,z,ri,ro,col,segments=16):
 mesh=[]
 for i in range(segments):
  a=2*math.pi*i/segments;b=2*math.pi*(i+1)/segments
  p=lambda angle,r,zz:(cx+r*math.cos(angle),cy+r*math.sin(angle),zz)
  quad(mesh,p(a,ri,z),p(a,ro,z),p(b,ro,z),p(b,ri,z),col)
  quad(mesh,p(a,ri,z-.006),p(b,ri,z-.006),p(b,ro,z-.006),p(a,ro,z-.006),col)
  quad(mesh,p(a,ro,z-.006),p(b,ro,z-.006),p(b,ro,z),p(a,ro,z),col)
  quad(mesh,p(a,ri,z),p(b,ri,z),p(b,ri,z-.006),p(a,ri,z-.006),col)
 e.parts.append((name,mesh))
prism('FacetedDarkstarShaft',-1.03,.88,.037,.047,[2,1,3,1,2,1,4,1])
prism('LeatherGrip',-.73,-.18,.046,.046,[6,6,7,6])
for i in range(7):prism('SteelGripBand'+str(i),-.70+i*.078,-.688+i*.078,.052,.052,[3,2,5,2])
prism('UpperFerrule',.64,.76,.064,.054,[3,2,8,2])
e.box('SplitHeadBackplate',(.012,.84,0),(.24,.21,.15),2,.27)
e.box('ForgedNeck',(.025,1.035,0),(.09,.38,.09),3,-.05)
# Both faces expose a black depression and thin luminous concentric iris.
for side in (-1,1):
 z=side*.115
 ring('CoreSteelRim'+str(side),.018,.845,z,.091,.137,8)
 ring('CoreShadowLip'+str(side),.018,.845,z+side*.008,.072,.092,11)
 ring('VoidOuterIris'+str(side),.018,.845,z+side*.014,.059,.071,12)
 ring('VoidInnerIris'+str(side),.018,.845,z+side*.018,.028,.036,14)
 e.box('AbyssBlackCenter'+str(side),(.018,.845,z-side*.010),(.108,.108,.005),0,.785)
# Six-point bevel profile: dark broad faces, bright narrow honed edges.
for seg,(lo,hi) in enumerate([(8,44),(47,83),(86,127)]):
 mesh=[];steps=16
 def cross(a,j):
  t=(a-8)/119;outer=.92;w=.22*(1-.82*t);th=.064*(1-.75*t)
  rr=[outer,outer-w*.08,outer-w*.86,outer-w,outer-w*.86,outer-w*.08][j%6]
  zz=[0,th,th,0,-th,-th][j%6]
  return (-.10+rr*math.sin(math.radians(a)),.34+rr*math.cos(math.radians(a)),zz)
 for i in range(steps):
  a=lo+(hi-lo)*i/steps;b=lo+(hi-lo)*(i+1)/steps
  for j,col in enumerate([8,2+seg%2,9,9,2+seg%2,8]):quad(mesh,cross(a,j),cross(b,j),cross(b,j+1),cross(a,j+1),col)
 for a,rev in [(lo,False),(hi,True)]:
  v=[cross(a,j) for j in range(6)]
  for j in range(1,5):tri(mesh,v[0],v[j+1] if rev else v[j],v[j] if rev else v[j+1],4)
 mesh=[(a,c,b,[-v for v in n],col) for a,b,c,n,col in mesh]
 e.parts.append(('BrokenBlade'+str(seg),mesh))
 # Separate solid hairline of energy at the sharpened inner edge, no emissive blade faces.
 energy=[]
 def edge(a,j):
  t=(a-8)/119;rr=.92-.22*(1-.82*t)-.002
  radius=rr+[.003,0,-.003,0][j%4];z=[0,.003,0,-.003][j%4]
  return (-.10+radius*math.sin(math.radians(a)),.34+radius*math.cos(math.radians(a)),z)
 for i in range(steps):
  a=lo+(hi-lo)*i/steps;b=lo+(hi-lo)*(i+1)/steps
  for j in range(4):quad(energy,edge(a,j+1),edge(b,j+1),edge(b,j),edge(a,j),13)
 e.parts.append(('VoidEdge'+str(seg),energy))
# Two tiny isolated chips near the actual blade gaps; no chain/counterweight.
for i,(a,y) in enumerate([(.68,.99),(.82,.54)]):
 e.box('GapChip'+str(i),(a,y,.015),(.030,.063,.028),4,.45)
 e.box('GapChipRift'+str(i),(a,y,-.003),(.008,.025,.008),13,.45)
prism('TailSteelCollar',-1.08,-.98,.063,.046,[3,2,8,2])
prism('TailStarSpike',-1.29,-1.08,.003,.06,[1,3,5,2],6)
# Positive-determinant 180-degree Y rotation: blade now projects inward for a right-hand grip.
e.parts[:]=[(name,[((-a[0],a[1],-a[2]),(-b[0],b[1],-b[2]),(-c[0],c[1],-c[2]),[-n[0],n[1],-n[2]],col) for a,b,c,n,col in mesh]) for name,mesh in e.parts]
ns='http://www.collada.org/2005/11/COLLADASchema';ET.register_namespace('',ns)
def sub(p,t,attrs=None,text=None):
    x=ET.SubElement(p,'{'+ns+'}'+t,attrs or {});x.text=text;return x
doc=ET.Element('{'+ns+'}COLLADA',{'version':'1.4.1'});asset=sub(doc,'asset');sub(asset,'unit',{'name':'meter','meter':'1'});sub(asset,'up_axis',text='Y_UP')
lib=sub(doc,'library_geometries')
for name,mesh in e.parts:
    m=sub(sub(lib,'geometry',{'id':name+'-mesh','name':name}),'mesh')
    positions=[];normals=[];uv=[]
    for a,b,c,n,col in mesh:
        for p in (a,b,c):positions+=list(p);normals+=n;uv +=[(col+.5)/16,.5]
    for suffix,data,stride,params in [('pos',positions,3,'XYZ'),('norm',normals,3,'XYZ'),('uv',uv,2,'ST')]:
        sid=name+'-'+suffix;src=sub(m,'source',{'id':sid})
        sub(src,'float_array',{'id':sid+'-array','count':str(len(data))},' '.join(format(v,'.7g') for v in data))
        ac=sub(sub(src,'technique_common'),'accessor',{'source':'#'+sid+'-array','count':str(len(data)//stride),'stride':str(stride)})
        for p in params:sub(ac,'param',{'name':p,'type':'float'})
    sub(sub(m,'vertices',{'id':name+'-vertices'}),'input',{'semantic':'POSITION','source':'#'+name+'-pos'})
    tri=sub(m,'triangles',{'count':str(len(mesh))})
    for sem,suffix,o in [('VERTEX','vertices',0),('NORMAL','norm',1),('TEXCOORD','uv',2)]:sub(tri,'input',{'semantic':sem,'source':'#'+name+'-'+suffix,'offset':str(o)})
    sub(tri,'p',text=' '.join(str(i) for i in range(len(mesh)*3) for _ in range(3)))
node=sub(sub(sub(doc,'library_visual_scenes'),'visual_scene',{'id':'Scene'}),'node',{'id':'AbyssReaper','name':'AbyssReaper','type':'NODE'})
sub(node,'matrix',{'sid':'transform'},'1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1')
for name,_ in e.parts:sub(node,'instance_geometry',{'url':'#'+name+'-mesh'})
sub(sub(doc,'scene'),'instance_visual_scene',{'url':'#Scene'})
root=Path(__file__).resolve().parents[1]/'src/SCIEPort/Assets'
ET.indent(doc);ET.ElementTree(doc).write(root/'Models/AbyssReaper.dae',encoding='utf-8',xml_declaration=True)
colors=[(8,7,13),(39,43,55),(64,71,86),(92,103,122),(126,139,157),(168,180,196),(24,25,33),(55,52,68),(191,205,220),(221,230,241),(244,246,255),(26,12,42),(96,47,151),(159,84,223),(210,169,251),(239,221,255)]
row=b''.join(bytes((*colors[x*16//256],255)) for x in range(256))
def chunk(t,b):return struct.pack('>I',len(b))+t+b+struct.pack('>I',zlib.crc32(t+b)&0xffffffff)
(root/'Textures/AbyssReaper.png').write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',256,16,8,6,0,0,0))+chunk(b'IDAT',zlib.compress((b'\0'+row)*16,9))+chunk(b'IEND',b''))
print('Abyss Reaper:',len(e.parts),'meshes,',sum(len(m) for _,m in e.parts),'triangles, 1 root, opaque 256x16')

import json
out=Path(__file__).resolve().parents[1]/'docs/assets/abyss-reaper'
out.mkdir(parents=True,exist_ok=True)
(out/'geometry.json').write_text(json.dumps(e.parts),encoding='utf8')

(out/'palette.json').write_text(json.dumps(colors),encoding='utf8')
