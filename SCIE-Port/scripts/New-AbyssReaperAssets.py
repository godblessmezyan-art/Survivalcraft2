"""Original Abyss scythe; deterministic geometry and opaque palette."""
from pathlib import Path
import importlib.util, math, struct, zlib, xml.etree.ElementTree as ET
spec=importlib.util.spec_from_file_location('primitives',Path(__file__).with_name('New-OmniscientEyeAssets.py'))
e=importlib.util.module_from_spec(spec);spec.loader.exec_module(e)
e.parts.clear()
e.box('DarkstarShaft',(0,-.1,0),(.065,1.9,.075),1)
for i in range(12):
 e.box('GripBand'+str(i),(0,-.62+i*.075,0),(.084,.022,.09),2 if i%3 else 3)
e.box('BladeSocket',(.02,.84,0),(.26,.24,.19),2,.18)
e.box('HeadSpine',(.045,1.055,0),(.10,.42,.11),2,-.07)
e.box('CoreCage',(.02,.85,0),(.18,.18,.22),3,.785)
e.box('VoidCore',(.02,.85,-.124),(.10,.10,.05),7,.785)
# Closed beveled crescent cross sections: spine, flat, cutting edge. Gaps separate three blades.
for seg,(lo,hi) in enumerate([(8,44),(47,83),(86,127)]):
 mesh=[]
 def pt(a,j):
  t=(a-8)/119; radius=.92
  cx=-.10; cy=.34
  # four-section bevel tapers towards the final tip
  w=.22*(1-.82*t); thick=.066*(1-.75*t)
  rr=[radius,radius-w*.5,radius-w,radius-w*.5][j%4]
  zz=[0,thick,0,-thick][j%4]
  return (cx+rr*math.sin(math.radians(a)),cy+rr*math.cos(math.radians(a)),zz)
 steps=24
 for i in range(steps):
  a=lo+(hi-lo)*i/steps;b=lo+(hi-lo)*(i+1)/steps
  for j in range(4):
   v=[pt(a,j),pt(b,j),pt(b,j+1),pt(a,j+1)]
   for x,y,z in [(0,1,2),(0,2,3)]:e.triangle(mesh,v[x],v[y],v[z],6 if j in (1,2) else 1+seg%2)
 for a,rev in [(lo,False),(hi,True)]:
  v=[pt(a,j) for j in range(4)]
  for x,y,z in ([(0,2,1),(0,3,2)] if rev else [(0,1,2),(0,2,3)]):e.triangle(mesh,v[x],v[y],v[z],2)
 mesh=[(a,c,b,[-v for v in n],col) for a,b,c,n,col in mesh]
 e.parts.append(('BrokenBlade'+str(seg),mesh))
for i in range(5):
 e.box('FloatingShard'+str(i),(-.2-i*.10,.9-i*.11,.02),(.10,.18-i*.018,.055),2,-.5+i*.13)
 e.box('ShardRift'+str(i),(-.2-i*.10,.9-i*.11,-.012),(.018,.10-i*.01,.01),6,-.5+i*.13)
e.box('TailSocket',(0,-1.04,0),(.115,.13,.12),3)
mesh=[]
for i in range(6):
 a=i*math.pi/3;b=(i+1)*math.pi/3
 e.triangle(mesh,(.08*math.cos(a),-.99,.08*math.sin(a)),(.08*math.cos(b),-.99,.08*math.sin(b)),(0,-1.30,0),i%3)
e.parts.append(('TerminalStarSpike',mesh))
ns='http://www.collada.org/2005/11/COLLADASchema';ET.register_namespace('',ns)
def sub(p,t,attrs=None,text=None):
    x=ET.SubElement(p,'{'+ns+'}'+t,attrs or {});x.text=text;return x
doc=ET.Element('{'+ns+'}COLLADA',{'version':'1.4.1'});asset=sub(doc,'asset');sub(asset,'unit',{'name':'meter','meter':'1'});sub(asset,'up_axis',text='Y_UP')
lib=sub(doc,'library_geometries')
for name,mesh in e.parts:
    m=sub(sub(lib,'geometry',{'id':name+'-mesh','name':name}),'mesh')
    positions=[];normals=[];uv=[]
    for a,b,c,n,col in mesh:
        for p in (a,b,c):positions+=list(p);normals+=n;uv +=[(col+.5)/9,.5]
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
colors=[(10,8,17),(30,25,40),(52,40,65),(109,69,35),(176,123,60),(225,174,97),(95,38,158),(186,102,240),(242,221,255)]
row=b''.join(bytes((*colors[x*9//128],255)) for x in range(128))
def chunk(t,b):return struct.pack('>I',len(b))+t+b+struct.pack('>I',zlib.crc32(t+b)&0xffffffff)
(root/'Textures/AbyssReaper.png').write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',128,16,8,6,0,0,0))+chunk(b'IDAT',zlib.compress((b'\0'+row)*16,9))+chunk(b'IEND',b''))
print('Abyss Reaper:',len(e.parts),'meshes,',sum(len(m) for _,m in e.parts),'triangles, 1 root, opaque 128x16')

import json
out=Path(__file__).resolve().parents[1]/'docs/assets/abyss-reaper'
out.mkdir(parents=True,exist_ok=True)
(out/'geometry.json').write_text(json.dumps(e.parts),encoding='utf8')
