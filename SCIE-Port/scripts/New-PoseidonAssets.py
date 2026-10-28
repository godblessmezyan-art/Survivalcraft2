"""Original deterministic ocean regalia: baked Y_UP geometry, opaque palette, touch icons, PCM surf.
No source images, proprietary resources or third-party art. Pillow is used only for authored icons/QA.
"""
import importlib.util
import math
import struct
import wave
import random
import zlib
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / 'src/SCIEPort/Assets'
spec = importlib.util.spec_from_file_location('primitives', Path(__file__).with_name('New-OmniscientEyeAssets.py'))
primitives = importlib.util.module_from_spec(spec)
spec.loader.exec_module(primitives)
parts = primitives.parts
triangle = primitives.triangle
box = primitives.box
COLORS = [(8,25,53),(16,53,86),(24,91,134),(43,139,183),(57,191,214),(122,235,244),
          (78,49,27),(141,97,44),(202,158,71),(245,210,126),(231,249,241),(24,71,89)]

def tube(name, points, radii, color, sides=8):
    mesh=[]
    for i in range(len(points)-1):
        # Section rings lie in XZ; all bends and tapers are baked, never node transforms.
        for j in range(sides):
            def p(k,n):
                a=n*math.tau/sides
                return (points[k][0]+math.cos(a)*radii[k],points[k][1],points[k][2]+math.sin(a)*radii[k])
            a,b,c,d=p(i,j),p(i,j+1),p(i+1,j+1),p(i+1,j)
            triangle(mesh,a,c,b,color if j%3 else min(color+1,10))
            triangle(mesh,a,d,c,color if j%3 else min(color+1,10))
    for k,reverse in [(0,False),(len(points)-1,True)]:
        for j in range(sides):
            a=j*math.tau/sides;b=(j+1)*math.tau/sides
            p=(points[k][0]+math.cos(a)*radii[k],points[k][1],points[k][2]+math.sin(a)*radii[k])
            q=(points[k][0]+math.cos(b)*radii[k],points[k][1],points[k][2]+math.sin(b)*radii[k])
            triangle(mesh,points[k],q if reverse else p,p if reverse else q,color)
    parts.append((name,mesh))

def blade(name, outline, depth, color, offset=0):
    """Convex outline with faceted front/back ridges, gold edge and blue center on both faces."""
    mesh=[]
    area=sum(outline[i][0]*outline[(i+1)%len(outline)][1]-outline[(i+1)%len(outline)][0]*outline[i][1] for i in range(len(outline)))
    if area>0:outline=list(reversed(outline))
    cx=sum(p[0] for p in outline)/len(outline);cy=sum(p[1] for p in outline)/len(outline)
    for i,(x,y) in enumerate(outline):
        u,v=outline[(i+1)%len(outline)]
        triangle(mesh,(x,y,-depth*.22-offset),(u,v,-depth*.22-offset),(cx,cy,-depth-offset),color+i%2)
        triangle(mesh,(u,v,depth*.22+offset),(x,y,depth*.22+offset),(cx,cy,depth+offset),color+i%2)
        a,b,c,d=(x,y,-depth*.22-offset),(x,y,depth*.22+offset),(u,v,depth*.22+offset),(u,v,-depth*.22-offset)
        triangle(mesh,a,b,c,9);triangle(mesh,a,c,d,9)
    parts.append((name,mesh))

def geometry():
    parts.clear()
    tube('OceanShaft',[(0,-1.22,0),(0,-1.12,0),(0,.50,0),(0,.63,0)],[.038,.045,.045,.067],1)
    tube('RoyalGrip',[(0,-.56,0),(0,-.50,0),(0,-.04,0),(0,.02,0)],[.051,.058,.058,.051],0)
    for i,y in enumerate([-.99,-.71,-.53,-.35,-.17,.02,.23,.43,.56]):
        tube('GildedCollar'+str(i),[(0,y-.012,0),(0,y+.012,0)],[.062 if i<6 else .071]*2,8)
    blade('TidalPommel',[(-.075,-1.17),(0,-1.31),(.075,-1.17),(0,-1.10)],.065,8)
    # Double helix wave inlay, authored as thin gold geometry, visible in opaque daylight rendering.
    for strand in range(2):
        for i in range(16):
            y=-1.08+i*.098
            angle=i*.62+strand*math.pi
            box('ShaftWave_%d_%d'%(strand,i),(.047*math.cos(angle),y,.047*math.sin(angle)),(.013,.052,.013),8,math.sin(angle)*.38)
    # The head is a royal lyre/crescent, NOT three parallel spear sticks.
    tube('CrownNeck',[(0,.49,0),(0,.58,0),(0,.71,0)],[.08,.12,.10],8)
    for sign,side in [(-1,'Port'),(1,'Starboard')]:
        pts=[(0,.60,0),(sign*.19,.63,0),(sign*.34,.73,0),(sign*.44,.91,0),(sign*.48,1.16,0)]
        tube(side+'Crescent',pts,[.09,.085,.073,.061,.055],8)
        # Outward-curved prongs terminate lower than the central blade.
        outline=[(sign*.40,1.05),(sign*.54,1.08),(sign*.68,1.64),(sign*.48,1.45),(sign*.39,1.23)]
        if sign<0:outline.reverse()
        blade(side+'Prong',outline,.073,8)
        inner=[(sign*.435,1.15),(sign*.50,1.17),(sign*.60,1.49),(sign*.46,1.36)]
        if sign<0:inner.reverse()
        blade(side+'AquamarineInlay',inner,.018,3,.078)
        blade(side+'SeaFin',[(sign*.19,.72),(sign*.29,.87),(sign*.29,.67)] if sign>0 else [(sign*.29,.67),(sign*.29,.87),(sign*.19,.72)],.035,8)
    blade('SovereignMainBlade',[(-.12,.74),(.12,.74),(.17,1.22),(0,1.84),(-.17,1.22)],.085,8)
    blade('MainDeepBlueInlay',[(-.052,.89),(.052,.89),(.088,1.23),(0,1.68),(-.088,1.23)],.018,2,.09)
    # Aquamarine gem in a substantial gilt bezel. White facets are opaque highlights, no emissive dependency.
    blade('GemBezel',[(-.16,.62),(0,.51),(.16,.62),(0,.87)],.13,8)
    blade('HeartOfTheOcean',[(-.115,.65),(0,.56),(.115,.65),(0,.82)],.025,3,.14)
    for sign in [-1,1]:box('GemGlint'+str(sign),(sign*.035,.70,-.172),(.014,.048,.008),10,sign*.3)
    # Split fork runes down the shaft, on BOTH faces.
    for face in [-1,1]:
        for i,y in enumerate([-.91,-.70,.16,.34]):
            z=face*.052
            box('RuneStem%d_%d'%(face,i),(0,y,z),(.012,.055,.008),5)
            for sign in [-1,1]:box('RuneFork%d_%d_%d'%(face,i,sign),(sign*.013,y+.014,z),(.012,.034,.008),9,sign*-.65)

def model():
    ns='http://www.collada.org/2005/11/COLLADASchema';ET.register_namespace('',ns)
    def sub(p,t,a=None,text=None):
        e=ET.SubElement(p,'{'+ns+'}'+t,a or {});e.text=text;return e
    doc=ET.Element('{'+ns+'}COLLADA',{'version':'1.4.1'});a=sub(doc,'asset')
    sub(a,'unit',{'name':'meter','meter':'1'});sub(a,'up_axis',text='Y_UP');lib=sub(doc,'library_geometries')
    for name,mesh in parts:
        m=sub(sub(lib,'geometry',{'id':name+'-mesh','name':name}),'mesh')
        pos=[];norm=[];uv=[]
        for a,b,c,n,col in mesh:
            for p in (a,b,c):pos.extend(p);norm.extend(n);uv.extend(((col+.5)/len(COLORS),.5))
        for suffix,data,stride,params in [('pos',pos,3,'XYZ'),('norm',norm,3,'XYZ'),('uv',uv,2,'ST')]:
            sid=name+'-'+suffix;src=sub(m,'source',{'id':sid})
            sub(src,'float_array',{'id':sid+'-array','count':str(len(data))},' '.join(format(v,'.7g') for v in data))
            acc=sub(sub(src,'technique_common'),'accessor',{'source':'#'+sid+'-array','count':str(len(data)//stride),'stride':str(stride)})
            for p in params:sub(acc,'param',{'name':p,'type':'float'})
        sub(sub(m,'vertices',{'id':name+'-vertices'}),'input',{'semantic':'POSITION','source':'#'+name+'-pos'})
        tris=sub(m,'triangles',{'count':str(len(mesh))})
        for sem,suffix,offset in [('VERTEX','vertices',0),('NORMAL','norm',1),('TEXCOORD','uv',2)]:sub(tris,'input',{'semantic':sem,'source':'#'+name+'-'+suffix,'offset':str(offset)})
        sub(tris,'p',text=' '.join(str(i) for i in range(len(mesh)*3) for _ in range(3)))
    scene=sub(sub(doc,'library_visual_scenes'),'visual_scene',{'id':'Scene','name':'Scene'})
    root=sub(scene,'node',{'id':'PoseidonTrident','name':'PoseidonTrident','type':'NODE'})
    sub(root,'matrix',{'sid':'transform'},'1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1')
    for name,_ in parts:sub(root,'instance_geometry',{'url':'#'+name+'-mesh'})
    sub(sub(doc,'scene'),'instance_visual_scene',{'url':'#Scene'})
    ET.indent(doc);ET.ElementTree(doc).write(ROOT/'Models/PoseidonTrident.dae',encoding='utf-8',xml_declaration=True)
    def chunk(tag,b):return struct.pack('>I',len(b))+tag+b+struct.pack('>I',zlib.crc32(tag+b)&0xffffffff)
    w,h=256,16;row=b''.join(bytes((*COLORS[x*len(COLORS)//w],255)) for x in range(w))
    (ROOT/'Textures/PoseidonTrident.png').write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,8,6,0,0,0))+chunk(b'IDAT',zlib.compress((b'\0'+row)*h,9))+chunk(b'IEND',b''))

def icons():
    from PIL import Image,ImageDraw
    im=Image.new('RGBA',(512,256));d=ImageDraw.Draw(im)
    for k in range(2):
        x=k*256
        d.ellipse((x+7,7,x+249,249),fill=(7,24,47,246),outline=(208,168,83,255),width=7)
        d.ellipse((x+22,22,x+234,234),outline=(36,149,184,255),width=3)
        if k==0:
            for j in range(3):
                points=[(x+42+i*4,133+j*17+round(math.sin(i*.17+j*.5)*13)) for i in range(41)]
                d.line(points,fill=(61,188+j*15,229,255),width=7-j)
            d.line([(x+111,181),(x+148,69)],fill=(249,216,137),width=8)
            d.line([(x+119,91),(x+116,63),(x+149,83),(x+172,69),(x+169,95),(x+149,104),(x+119,91)],fill=(160,240,249),width=6)
        else:
            points=[(x+42,188),(x+43,127),(x+65,88),(x+102,62),(x+144,58),(x+177,73),(x+197,103),(x+181,125),(x+157,117),(x+155,95),(x+134,99),(x+123,129),(x+144,163),(x+203,189)]
            d.polygon(points,fill=(23,108,170),outline=(146,237,248))
            d.line(points[2:9],fill=(221,255,255),width=9)
            for j in range(3):d.arc((x+52-j*7,151+j*7,x+212,208+j*9),0,170,fill=(66,205,231),width=4)
        for i in range(8):
            a=i*math.tau/8
            px=x+128+round(112*math.cos(a));py=128+round(112*math.sin(a))
            d.ellipse((px-3,py-3,px+3,py+3),fill=(252,223,149))
    im.save(ROOT/'Textures/PoseidonSkills.png')

def sounds():
    folder=ROOT/'Audio/Poseidon';folder.mkdir(parents=True,exist_ok=True)
    for name,duration in [('Surge',.65),('Tsunami',1.8)]:
        rng=random.Random(951 if name=='Surge' else 1951);rate=22050;low=0;data=bytearray()
        for i in range(round(rate*duration)):
            t=i/rate;p=t/duration
            noise=rng.uniform(-1,1);low=low*.94+noise*.06
            envelope=min(1,p*14)*(1-p)**1.3
            swell=math.sin(2*math.pi*(92*t-19*t*t))*.18+low*2.7+noise*.06
            value=int(max(-1,min(1,swell*envelope*.75))*32767);data.extend(struct.pack('<h',value))
        with wave.open(str(folder/(name+'.wav')),'wb') as wav:
            wav.setnchannels(1);wav.setsampwidth(2);wav.setframerate(rate);wav.writeframes(data)

def preview():
    from PIL import Image,ImageDraw
    def dot(a,b):return sum(x*y for x,y in zip(a,b))
    def cross(a,b):return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
    def unit(a):return tuple(x/math.sqrt(dot(a,a)) for x in a)
    im=Image.new('RGB',(1400,1100),(7,19,34));d=ImageDraw.Draw(im)
    for camera,origin,title in [((0,.05,-3),(370,600),'FRONT'),((1.4,.4,-3),(1020,600),'THREE-QUARTER')]:
        f=unit(tuple(-x for x in camera));right=unit(cross(f,(0,1,0)));up=cross(right,f);faces=[]
        for name,mesh in parts:
            for a,b,c,n,col in mesh:
                if dot(n,f)>=0:continue
                points=[(origin[0]+285*dot(p,right),origin[1]-285*dot(p,up)) for p in (a,b,c)]
                light=.55+.45*max(0,dot(n,unit((-.5,1,-2))))
                faces.append((sum(dot(p,f) for p in (a,b,c))/3,points,tuple(round(x*light) for x in COLORS[col])))
        for _,points,color in sorted(faces,reverse=True):d.polygon(points,fill=color)
        d.text((origin[0]-70,40),title,fill=(245,210,126))
    d.text((40,1060),'POSEIDON | original low-poly ocean regalia | software QA only, not game/Android capture',fill=(144,211,228))
    out=ROOT.parents[2]/'artifacts/Poseidon-preview.png';out.parent.mkdir(exist_ok=True);im.save(out);print(out)

if __name__=='__main__':
    geometry()
    for name,mesh in parts:
        if name in ('SovereignMainBlade','PortProng','StarboardProng','HeartOfTheOcean') and mesh[0][3][2]>=0:
            raise ValueError('Front face winding must face -Z: '+name)
    model();icons();sounds();preview()
    print('Poseidon:',len(parts),'meshes,',sum(len(m) for _,m in parts),'triangles; 1 identity root; opaque palette')
