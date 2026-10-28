"""Original Eye of Oblivion, using only project-owned primitive geometry helpers.
Independent geometry, opaque palette and content paths. Standard library only.
"""
from pathlib import Path
import importlib.util
import math
import struct
import zlib
import xml.etree.ElementTree as ET

spec=importlib.util.spec_from_file_location('primitives',Path(__file__).with_name('New-OmniscientEyeAssets.py'))
e=importlib.util.module_from_spec(spec);spec.loader.exec_module(e)
e.parts.clear()
e.ring('OblivionBronze',.43,.15,3)
e.ring('CoreCage',.28,-.65,4)
mesh=[]
vertices=[(.16*math.cos(i*math.pi/4),.18+.16*math.sin(i*math.pi/4),0) for i in range(8)]
for i in range(8):
    e.triangle(mesh,vertices[(i+1)%8],vertices[i],(0,.18,-.14),6+i%2)
    e.triangle(mesh,vertices[i],vertices[(i+1)%8],(0,.18,.14),i%3)
e.parts.append(('VioletVoidCore',mesh))
e.box('OblivionPupil',(0,.18,-.15),(.026,.12,.018),0)
e.box('Handle',(0,-.43,0),(.074,.38,.08),0)
e.box('Socket',(0,-.25,0),(.13,.09,.12),4)
e.box('Pommel',(0,-.64,0),(.12,.07,.11),3)
for i in range(4):e.box('BronzeGrip'+str(i),(0,-.31-.08*i,0),(.088,.019,.094),4)
for i in range(8):
    a=i*math.pi/4
    e.box('ObsidianSeal'+str(i),(.43*math.cos(a),.18+.43*math.sin(a)*math.cos(.15),.43*math.sin(a)*math.sin(.15)),(.054,.105,.075),1,a-math.pi/2)
    e.box('VioletRune'+str(i),(.43*math.cos(a),.18+.43*math.sin(a)*math.cos(.15),.43*math.sin(a)*math.sin(.15)-.043),(.018,.052,.01),7,a-math.pi/2)

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
node=sub(sub(sub(doc,'library_visual_scenes'),'visual_scene',{'id':'Scene'}),'node',{'id':'DevouringEye','name':'DevouringEye','type':'NODE'})
sub(node,'matrix',{'sid':'transform'},'1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1')
for name,_ in e.parts:sub(node,'instance_geometry',{'url':'#'+name+'-mesh'})
sub(sub(doc,'scene'),'instance_visual_scene',{'url':'#Scene'})
root=Path(__file__).resolve().parents[1]/'src/SCIEPort/Assets'
ET.indent(doc);ET.ElementTree(doc).write(root/'Models/DevouringEye.dae',encoding='utf-8',xml_declaration=True)
colors=[(10,8,17),(30,25,40),(52,40,65),(109,69,35),(176,123,60),(225,174,97),(95,38,158),(186,102,240),(242,221,255)]
row=b''.join(bytes((*colors[x*9//128],255)) for x in range(128))
def chunk(t,b):return struct.pack('>I',len(b))+t+b+struct.pack('>I',zlib.crc32(t+b)&0xffffffff)
(root/'Textures/DevouringEye.png').write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',128,16,8,6,0,0,0))+chunk(b'IDAT',zlib.compress((b'\0'+row)*16,9))+chunk(b'IEND',b''))
print('Devouring Eye:',len(e.parts),'meshes,',sum(len(m) for _,m in e.parts),'triangles, 1 root, opaque 128x16')
