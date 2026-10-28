"""Original, deterministic SCIE-Port astrolabe. Python 3 standard library only.
All transforms are baked; one identity scene node, opaque palette PNG.
"""
from pathlib import Path
import math
import struct
import zlib
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1] / 'src/SCIEPort/Assets'
COLORS = [(12, 24, 64), (24, 49, 99), (42, 78, 126), (112, 72, 33),
          (185, 132, 60), (231, 186, 100), (43, 177, 198), (119, 227, 232), (255, 239, 202)]
parts = []

def triangle(mesh, a, b, c, color):
    u = [b[i]-a[i] for i in range(3)]; v = [c[i]-a[i] for i in range(3)]
    normal = [u[1]*v[2]-u[2]*v[1], u[2]*v[0]-u[0]*v[2], u[0]*v[1]-u[1]*v[0]]
    length = math.sqrt(sum(x*x for x in normal))
    if length < 1e-9: raise ValueError('Degenerate face')
    mesh.append((a, b, c, [x/length for x in normal], color))

def box(name, center, size, color, angle=0):
    mesh = []; p = []
    for x,y,z in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]:
        x,y,z=x*size[0]/2,y*size[1]/2,z*size[2]/2
        p.append((center[0]+x*math.cos(angle)-y*math.sin(angle), center[1]+x*math.sin(angle)+y*math.cos(angle),center[2]+z))
    for a,b,c,d in [(0,3,2,1),(4,5,6,7),(0,4,7,3),(1,2,6,5),(0,1,5,4),(3,7,6,2)]:
        triangle(mesh,p[a],p[b],p[c],color); triangle(mesh,p[a],p[c],p[d],color)
    parts.append((name,mesh))

def ring(name, radius, tilt, color):
    mesh=[]; n=32
    def point(i,j):
        a=2*math.pi*i/n; b=2*math.pi*j/4
        r=radius+.027*math.cos(b)
        x,y,z=r*math.cos(a),r*math.sin(a),.027*math.sin(b)
        return (x, .18+y*math.cos(tilt)-z*math.sin(tilt), y*math.sin(tilt)+z*math.cos(tilt))
    for i in range(n):
        for j in range(4):
            a,b,c,d=point(i,j),point(i+1,j),point(i+1,j+1),point(i,j+1)
            triangle(mesh,a,b,c,color if j%2 else min(color+1,5));triangle(mesh,a,c,d,color if j%2 else min(color+1,5))
    parts.append((name,mesh))

def generate():
    parts.clear()
    ring('OuterMeridian',.43,.42,4);ring('InnerEcliptic',.34,-.55,3)
    mesh=[]
    equator=[(.175*math.cos(i*math.pi/3),.18+.175*math.sin(i*math.pi/3),0) for i in range(6)]
    for i in range(6):
        triangle(mesh,equator[(i+1)%6],equator[i],(0,.18,-.16),i%3)
        triangle(mesh,equator[i],equator[(i+1)%6],(0,.18,.14),i%3)
    parts.append(('FloatingSapphire',mesh))
    # The eye is a faceted cyan iris on the front (-Z), never an eyeball.
    mesh=[]
    iris=[(.092*math.cos(i*math.pi/4),.18+.055*math.sin(i*math.pi/4),-.174) for i in range(8)]
    for i in range(8):triangle(mesh,iris[(i+1)%8],iris[i],(0,.18,-.194),6+i%2)
    parts.append(('StarIris',mesh))
    box('Pupil',(0,.18,-.199),(.018,.067,.01),0)
    box('EyeGlint',(-.031,.201,-.2),(.013,.012,.008),8)
    box('Grip',(0,-.44,0),(.067,.36,.075),0)
    box('Neck',(0,-.264,0),(.12,.08,.1),4)
    box('Pommel',(0,-.635,0),(.115,.065,.1),5)
    for i in range(4):box('GripBand'+str(i),(0,-.32-i*.075,0),(.081,.018,.087),4)
    for i in range(12):
        a=2*math.pi*i/12
        center=(.43*math.cos(a),.18+.43*math.sin(a)*math.cos(.42),.43*math.sin(a)*math.sin(.42)-.032)
        box('Rune'+str(i),center,(.014,.037,.009),8 if i%3==0 else 6,a)
    ns='http://www.collada.org/2005/11/COLLADASchema'; ET.register_namespace('',ns)
    def sub(parent,tag,attrs={},text=None):
        e=ET.SubElement(parent,'{'+ns+'}'+tag,attrs);e.text=text;return e
    doc=ET.Element('{'+ns+'}COLLADA',{'version':'1.4.1'});asset=sub(doc,'asset')
    sub(asset,'unit',{'name':'meter','meter':'1'});sub(asset,'up_axis',text='Y_UP')
    lib=sub(doc,'library_geometries')
    for name,mesh in parts:
        geo=sub(lib,'geometry',{'id':name+'-mesh','name':name});m=sub(geo,'mesh')
        positions=[];normals=[];uv=[]
        for a,b,c,n,col in mesh:
            for p in (a,b,c):positions+=list(p);normals+=n;uv += [(col+.5)/len(COLORS),.5]
        for suffix,data,stride,params in [('pos',positions,3,'XYZ'),('norm',normals,3,'XYZ'),('uv',uv,2,'ST')]:
            sid=name+'-'+suffix;src=sub(m,'source',{'id':sid})
            sub(src,'float_array',{'id':sid+'-array','count':str(len(data))},' '.join(format(v,'.7g') for v in data))
            acc=sub(sub(src,'technique_common'),'accessor',{'source':'#'+sid+'-array','count':str(len(data)//stride),'stride':str(stride)})
            for p in params:sub(acc,'param',{'name':p,'type':'float'})
        sub(sub(m,'vertices',{'id':name+'-vertices'}),'input',{'semantic':'POSITION','source':'#'+name+'-pos'})
        tris=sub(m,'triangles',{'count':str(len(mesh))})
        for sem,suffix,offset in [('VERTEX','vertices',0),('NORMAL','norm',1),('TEXCOORD','uv',2)]:
            sub(tris,'input',{'semantic':sem,'source':'#'+name+'-'+suffix,'offset':str(offset)})
        sub(tris,'p',text=' '.join(str(i) for i in range(len(mesh)*3) for _ in range(3)))
    scene=sub(sub(doc,'library_visual_scenes'),'visual_scene',{'id':'Scene','name':'Scene'})
    node=sub(scene,'node',{'id':'OmniscientEye','name':'OmniscientEye','type':'NODE'})
    sub(node,'matrix',{'sid':'transform'},'1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1')
    for name,_ in parts:sub(node,'instance_geometry',{'url':'#'+name+'-mesh'})
    sub(sub(doc,'scene'),'instance_visual_scene',{'url':'#Scene'})
    ET.indent(doc);ET.ElementTree(doc).write(ROOT/'Models/OmniscientEye.dae',encoding='utf-8',xml_declaration=True)
    def chunk(tag,b):return struct.pack('>I',len(b))+tag+b+struct.pack('>I',zlib.crc32(tag+b)&0xffffffff)
    w,h=128,16
    row=b''.join(bytes((*COLORS[x*len(COLORS)//w],255)) for x in range(w))
    png=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,8,6,0,0,0))+chunk(b'IDAT',zlib.compress((b'\0'+row)*h,9))+chunk(b'IEND',b'')
    (ROOT/'Textures/OmniscientEye.png').write_bytes(png)
    print('OmniscientEye:',len(parts),'meshes,',sum(len(m) for _,m in parts),'triangles, 1 root, opaque',w,'x',h)

if __name__=='__main__':generate()
