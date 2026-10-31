"""Original articulated skeleton dragon. Seven bones, project-only geometry and UV.
No game/third-party meshes or textures. Preview images are software renders only.
"""
from pathlib import Path
import math,json,hashlib,random
import xml.etree.ElementTree as ET
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[1]
ASSETS=ROOT/'src/SCIEPort/Assets'
DESIGN=ROOT/'design/bone-dragon'
DESIGN.mkdir(parents=True,exist_ok=True)
PALETTE=[(174,179,172),(105,116,118),(222,223,199),(39,43,52),(20,25,33),(160,231,247),(72,57,93),(56,30,74)]
BONES={'Body':(None,(0,0,0)), 'WingL':('Body',(-.48,1.72,-.48)), 'WingR':('Body',(.48,1.72,-.48)),
 'Leg1':('Body',(-.65,1.16,-.82)), 'Leg2':('Body',(.65,1.16,-.82)),
 'Leg3':('Body',(-.61,1.14,1.05)), 'Leg4':('Body',(.61,1.14,1.05))}
MESHES={b:[] for b in BONES};PARTS=[]
def sub(a,b): return tuple(x-y for x,y in zip(a,b))
def add(a,b): return tuple(x+y for x,y in zip(a,b))
def mul(a,s): return tuple(x*s for x in a)
def dot(a,b): return sum(x*y for x,y in zip(a,b))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def length(a): return math.sqrt(dot(a,a))
def origin(b):
    p,o=BONES[b]; return add(origin(p),o) if p else o
def shape(name,bone,verts,faces,mat):
    center=tuple(sum(v[j] for v in verts)/len(verts) for j in range(3)); first=len(MESHES[bone])
    for face in faces:
        for i in range(1,len(face)-1):
            tri=[verts[face[0]],verts[face[i]],verts[face[i+1]]]
            n=cross(sub(tri[1],tri[0]),sub(tri[2],tri[0])); size=length(n)
            if size<1e-9: continue
            mid=tuple(sum(p[j] for p in tri)/3 for j in range(3))
            if dot(n,sub(mid,center))<0: tri[1],tri[2]=tri[2],tri[1];n=mul(n,-1)
            MESHES[bone].append((tri,mul(n,1/size),mat))
    PARTS.append({'name':name,'bone':bone,'triangles':len(MESHES[bone])-first})
def box(name,bone,c,size,mat,bevel=.16,yaw=0):
    x,y,z=(s/2 for s in size);d=min(x,z)*bevel
    outline=[(-x+d,-z),(x-d,-z),(x,-z+d),(x,z-d),(x-d,z),(-x+d,z),(-x,z-d),(-x,-z+d)]
    verts=[]
    for yy,scale in [(-y,.86),(-y+min(y*.32,.06),1),(y-min(y*.32,.06),1),(y,.80)]:
        for xx,zz in outline:
            xx*=scale;zz*=scale;verts.append((c[0]+xx*math.cos(yaw)+zz*math.sin(yaw),c[1]+yy,c[2]-xx*math.sin(yaw)+zz*math.cos(yaw)))
    faces=[list(range(7,-1,-1)),list(range(24,32))]
    for r in range(3):
        for k in range(8):faces.append([r*8+k,r*8+(k+1)%8,(r+1)*8+(k+1)%8,(r+1)*8+k])
    shape(name,bone,verts,faces,mat)
def horn(name,bone,base,tip,radius,mat,n=6):
    axis=sub(tip,base);unit=mul(axis,1/length(axis));ref=(0,0,1) if abs(unit[2])<.9 else (1,0,0)
    u=cross(unit,ref);u=mul(u,1/length(u));v=cross(unit,u)
    pts=[add(base,add(mul(u,math.cos(k*math.tau/n)*radius),mul(v,math.sin(k*math.tau/n)*radius))) for k in range(n)]+[tip]
    shape(name,bone,pts,[list(range(n))]+[[i,(i+1)%n,n] for i in range(n)],mat)
def tube(name,bone,rings,mat,n=10):
    # Body profiles run along Z, ellipses in X/Y; sculpted shoulders, sternum and haunches.
    verts=[(cx+rx*math.cos(k*math.tau/n),cy+ry*math.sin(k*math.tau/n),z) for cx,cy,z,rx,ry in rings for k in range(n)]
    faces=[list(range(n-1,-1,-1)),list(range((len(rings)-1)*n,len(rings)*n))]
    for r in range(len(rings)-1):
        for k in range(n):faces.append([r*n+k,r*n+(k+1)%n,(r+1)*n+(k+1)%n,(r+1)*n+k])
    shape(name,bone,verts,faces,mat)
def muscle(name,bone,c,radii,mat):
    verts=[];n=10
    for j in range(1,8):
        phi=-math.pi/2+j*math.pi/8
        for k in range(n):
            a=k*math.tau/n;verts.append((c[0]+radii[0]*math.cos(phi)*math.cos(a),c[1]+radii[1]*math.sin(phi),c[2]+radii[2]*math.cos(phi)*math.sin(a)))
    faces=[list(range(n-1,-1,-1)),list(range(6*n,7*n))]
    for r in range(6):
        for k in range(n):faces.append([r*n+k,r*n+(k+1)%n,(r+1)*n+(k+1)%n,(r+1)*n+k])
    shape(name,bone,verts,faces,mat)


def bonebar(name,bone,a,b,r,mat=0):
    # Closed octagonal long bone with wider articulating ends.
    axis=sub(b,a);unit=mul(axis,1/length(axis));u=cross(unit,(0,0,1) if abs(unit[2])<.9 else (1,0,0));u=mul(u,1/length(u));v=cross(unit,u)
    verts=[]
    for t,w in ((0,1.20),(.12,1.03),(.80,.78),(1,1.15)):
        c=add(a,mul(axis,t))
        verts += [add(c,add(mul(u,math.cos(k*math.tau/6)*r*w),mul(v,math.sin(k*math.tau/6)*r*w))) for k in range(6)]
    faces=[list(range(5,-1,-1)),list(range(18,24))]
    for j in range(3):
        for k in range(6):faces.append([j*6+k,j*6+(k+1)%6,(j+1)*6+(k+1)%6,(j+1)*6+k])
    shape(name,bone,verts,faces,mat)

# Exposed vertebrae and an open rib cage; no solid wolf/rhino body under the bones.
for i in range(13):
    z=-1.30+i*.24;y=1.69+.10*math.cos(z)
    box('Vertebra','Body',(0,y,z),(.30,.22,.28),0,.28)
    if z<-.25 or z>.7:horn('DorsalSpine','Body',(0,y+.08,z),(0,y+.48,z+.16),.105,2)
for i in range(7):
    z=-.91+i*.28;wide=.58-.16*abs(z)
    for s in (-1,1):
        points=[(0,1.74,z),(s*wide*.78,1.57,z-.05),(s*wide,1.20,z+.02),(s*wide*.67,.99,z+.11),(0,1.04,z+.13)]
        for j in range(4):bonebar('OpenRib','Body',points[j],points[j+1],.065,0 if i%2 else 2)
bonebar('Sternum','Body',(0,1.03,-.85),(0,1.10,.90),.095,1)
for s in (-1,1):
    bonebar('ShoulderBlade','Body',(0,1.70,-1.0),(s*.67,1.25,-.85),.12,2)
    bonebar('PelvicArch','Body',(0,1.62,1.1),(s*.64,1.14,1.08),.14,0)
# Curved cervical spine leading to a long dragon skull and swept horns.
neck=[(0,1.70,-1.19),(0,1.88,-1.57),(0,2.04,-1.98),(0,2.12,-2.35)]
for i in range(3):
    bonebar('NeckVertebra','Body',neck[i],neck[i+1],.17-i*.025,0)
    horn('NeckRidge','Body',neck[i],add(neck[i],(0,.32,.14)),.08,2)
tube('DragonCranium','Body',[(0,2.10,-2.22,.26,.25),(0,2.12,-2.50,.39,.28),(0,2.01,-2.78,.27,.18)],0,8)
tube('LongMuzzle','Body',[(0,1.98,-2.63,.26,.14),(0,1.93,-3.12,.18,.09),(0,1.92,-3.40,.13,.08)],2,6)
bonebar('LowerJaw','Body',(-.20,1.77,-2.62),(-.12,1.76,-3.30),.075,1)
bonebar('LowerJaw','Body',(.20,1.77,-2.62),(.12,1.76,-3.30),.075,1)
for s in (-1,1):
    box('DeepEyeSocket','Body',(s*.30,2.16,-2.62),(.15,.19,.23),4,.18)
    box('ColdSoulEye','Body',(s*.375,2.17,-2.66),(.028,.08,.12),5,.06)
    bonebar('Eyebrow','Body',(s*.16,2.30,-2.74),(s*.42,2.31,-2.44),.085,2)
    horn('SweptCrownHorn','Body',(s*.23,2.31,-2.27),(s*.53,2.66,-1.67),.14,2)
    horn('CheekHorn','Body',(s*.30,1.99,-2.36),(s*.59,1.83,-1.97),.10,1)
    for i in range(5):horn('DragonFang','Body',(s*(.14+i*.012),1.89,-3.23+i*.115),(s*(.14+i*.012),1.70,-3.24+i*.115),.036,2)
# A segmented long skeletal tail, ending in a spear-shaped fin.
tail=[(0,1.62,1.42),(.06,1.53,1.85),(.18,1.37,2.28),(.30,1.26,2.72),(.37,1.34,3.14),(.38,1.53,3.55)]
for i in range(5):
    bonebar('TailVertebra','Body',tail[i],tail[i+1],.145-i*.023,0)
    for s in (-1,1):horn('TailSpur','Body',tail[i],add(tail[i],(s*(.28-i*.035),.17,.10)),.065-i*.007,1)
horn('TailSpear','Body',tail[-1],(.38,1.65,3.84),.12,2)
# Four articulated bony legs; claws and joints remain on their own limb bones.
for i in range(1,5):
    b='Leg'+str(i);front=i<3
    knee=(0,-.46,-.12 if front else .23);ankle=(0,-.92,.02)
    bonebar('Humerus' if front else 'Femur',b,(0,0,0),knee,.12,0)
    box('KneeJoint',b,knee,(.22,.22,.24),1,.22)
    bonebar('ShinBone',b,knee,ankle,.075,2)
    bonebar('PairedShin',b,(.10,knee[1],knee[2]),(.10,-.91,.02),.04,0)
    for s in (-1,0,1):
        bonebar('Toe',b,ankle,(s*.13,-1.09,-.27),.055,0)
        horn('ObsidianClaw',b,(s*.13,-1.07,-.27),(s*.14,-1.12,-.51),.065,3)
# Wings have bone spars and recessed opaque torn web panels, with open gaps.
for side,label in ((-1,'WingL'),(1,'WingR')):
    def p(x,y,z):return (side*x,y,z)
    elbow=p(1.12,.22,.12);knuckle=p(2.20,.38,-.46)
    bonebar('WingHumerus',label,(0,0,0),elbow,.13,0)
    bonebar('WingRadius',label,elbow,knuckle,.10,2)
    ends=[p(3.55,.04,-.22),p(3.12,-.06,1.04),p(2.45,-.15,1.94),p(1.43,-.12,2.30),p(.24,-.20,1.34)]
    for j,end in enumerate(ends):
        middle=add(mul(knuckle,.45),mul(end,.55))
        bonebar('WingFinger',label,knuckle,middle,.065,0);bonebar('WingFingerTip',label,middle,end,.038,2)
        if j<4:
            # Thick closed panels avoid duplicated coplanar reverse triangles.
            q=[add(knuckle,(0,-.025,0)),end,add(mul(end,.32),mul(ends[j+1],.68)),ends[j+1]]
            vertices=q+[add(v,(0,-.026,0)) for v in q]
            shape('TornWingWeb',label,vertices,[[0,1,2,3],[7,6,5,4],[0,4,5,1],[1,5,6,2],[2,6,7,3],[3,7,4,0]],6 if j%2 else 7)
    horn('WingThumb',label,knuckle,add(knuckle,p(.10,.31,-.48)),.10,2)
# Low, purposeful saddle between wing roots. No new item or native mesh.
box('SaddlePad','Body',(0,1.88,.16),(.70,.15,.82),3,.25)
for z in (-.23,.56):box('SaddlePommel','Body',(0,2.03,z),(.75,.20,.10),1,.22)
for s in (-1,1):
    bonebar('HarnessRail','Body',(s*.39,1.86,-.28),(s*.39,1.85,.63),.035,6)
    bonebar('StirrupStrap','Body',(s*.43,1.83,.10),(s*.51,1.20,.12),.035,3)
box('SoulCore','Body',(0,1.47,-.53),(.21,.31,.23),5,.28)
def texture():
    image=Image.new('RGB',(512,256));pix=image.load();rng=random.Random(311067)
    for y in range(256):
        for x in range(512):
            tile=x//128+(y//128)*4;base=PALETTE[tile];edge=min(x%128,y%128,127-x%128,127-y%128)
            variation=rng.randint(-5,5)+(5 if edge<3 else 0)
            pix[x,y]=tuple(max(0,min(255,v+variation)) for v in base)
    draw=ImageDraw.Draw(image)
    for mat in (0,1,2,3,6):
        ox=(mat%4)*128;oy=(mat//4)*128
        for k in range(16):
            x=ox+rng.randint(10,115);y=oy+rng.randint(9,113)
            draw.line([(x,y),(x+6,y-3),(x+13,y-2)],fill=tuple(max(0,v-13) for v in PALETTE[mat]),width=1)
        draw.line([(ox+9,oy+116),(ox+9,oy+9),(ox+117,oy+9)],fill=tuple(min(255,v+17) for v in PALETTE[mat]),width=2)
    image.save(ASSETS/'Textures/AbyssBoneDragon.png')

def export():
    ns='http://www.collada.org/2005/11/COLLADASchema';ET.register_namespace('',ns)
    def e(parent,tag,attrs=None,text=None):
        obj=ET.SubElement(parent,'{'+ns+'}'+tag,attrs or {});obj.text=text;return obj
    doc=ET.Element('{'+ns+'}COLLADA',{'version':'1.4.1'});asset=e(doc,'asset');e(asset,'unit',{'name':'meter','meter':'1'});e(asset,'up_axis',text='Y_UP');lib=e(doc,'library_geometries')
    for b,triangles in MESHES.items():
        name='BoneDragon_'+b;m=e(e(lib,'geometry',{'id':name,'name':name}),'mesh');pos=[];norm=[];uv=[]
        for ps,n,mat in triangles:
            axis=max(range(3),key=lambda j:abs(n[j]));a,c=[j for j in range(3) if j!=axis]
            for p in ps:
                pos.extend(p);norm.extend(n)
                u=.1+.8*((p[a]*.91+.5)%1);v=.1+.8*((p[c]*.83+.5)%1);uv.extend(((mat%4+u)/4,(mat//4+v)/2))
        for suf,values,stride,fields in [('pos',pos,3,'XYZ'),('normal',norm,3,'XYZ'),('uv',uv,2,'ST')]:
            sid=name+'_'+suf;s=e(m,'source',{'id':sid});e(s,'float_array',{'id':sid+'_array','count':str(len(values))},' '.join(f'{v:.7g}' for v in values));ac=e(e(s,'technique_common'),'accessor',{'source':'#'+sid+'_array','count':str(len(values)//stride),'stride':str(stride)})
            for f in fields:e(ac,'param',{'name':f,'type':'float'})
        e(e(m,'vertices',{'id':name+'_vertices'}),'input',{'semantic':'POSITION','source':'#'+name+'_pos'});t=e(m,'triangles',{'count':str(len(triangles))})
        for k,(semantic,suf) in enumerate([('VERTEX','vertices'),('NORMAL','normal'),('TEXCOORD','uv')]):
            attrs={'semantic':semantic,'source':'#'+name+'_'+suf,'offset':str(k)}
            if k==2:attrs['set']='0'
            e(t,'input',attrs)
        e(t,'p',text=' '.join(str(i) for i in range(len(triangles)*3) for _ in range(3)))
    scene=e(e(doc,'library_visual_scenes'),'visual_scene',{'id':'Scene'});nodes={}
    for b,(parent,p) in BONES.items():
        node=e(nodes[parent] if parent else scene,'node',{'id':b,'name':b,'type':'NODE'});nodes[b]=node
        e(node,'matrix',{'sid':'transform'},f'1 0 0 {p[0]:.7g} 0 1 0 {p[1]:.7g} 0 0 1 {p[2]:.7g} 0 0 0 1')
        e(node,'instance_geometry',{'url':'#BoneDragon_'+b})
    e(e(doc,'scene'),'instance_visual_scene',{'url':'#Scene'});ET.indent(doc)
    ET.ElementTree(doc).write(ASSETS/'Models/AbyssBoneDragon.dae',encoding='utf-8',xml_declaration=True)
    path=ASSETS/'Models/AbyssBoneDragon.dae';path.write_bytes(path.read_bytes().replace(b'\r\n',b'\n'))


def preview(yaw,pitch,path,phase=0,flying=True):
    W,H=1200,900;im=Image.new('RGB',(W,H),(19,23,30));d=ImageDraw.Draw(im);items=[]
    cy,sy=math.cos(yaw),math.sin(yaw);cp,sp=math.cos(pitch),math.sin(pitch)
    def proj(p):
        x,z=p[0]*cy-p[2]*sy,p[0]*sy+p[2]*cy;y=p[1]-1.4
        return (W/2+x*100,H*.56-(y*cp-z*sp)*100,z*cp+y*sp)
    for b,ts in MESHES.items():
        angle=0;wing=b.startswith('Wing')
        if wing:angle=(-1 if b=='WingL' else 1)*(.20+.35*math.sin(phase) if flying else 1.12)
        elif b.startswith('Leg'):angle=.25 if flying else .32*math.sin(phase+(0 if b in ('Leg1','Leg4') else math.pi))
        for tri,n,mat in ts:
            ps=[]
            for q in tri:
                if wing:q=(q[0]*math.cos(angle)-q[1]*math.sin(angle),q[0]*math.sin(angle)+q[1]*math.cos(angle),q[2])
                elif b.startswith('Leg'):q=(q[0],q[1]*math.cos(angle)-q[2]*math.sin(angle),q[1]*math.sin(angle)+q[2]*math.cos(angle))
                ps.append(add(q,origin(b)))
            n=cross(sub(ps[1],ps[0]),sub(ps[2],ps[0]));n=mul(n,1/length(n))
            light=.43+.57*max(0,dot(n,(-.36,.78,-.51)))
            if mat==5:light=1.1
            points=[proj(q) for q in ps]
            items.append((sum(q[2] for q in points)/3,[(q[0],q[1]) for q in points],tuple(min(255,int(c*light)) for c in PALETTE[mat])))
    for _,ps,c in sorted(items,reverse=True):d.polygon(ps,fill=c)
    d.text((24,24),'BONE DRAGON | ORIGINAL MESH | SOFTWARE PREVIEW - NOT GAMEPLAY',fill=(180,207,219))
    im.save(path);return im
texture();export()
preview(.65,.32,DESIGN/'preview-flight.png');preview(1.35,.15,DESIGN/'preview-ground.png',flying=False)
frames=[preview(.65,.32,DESIGN/'frame.png',i*math.tau/12).resize((720,540)) for i in range(12)]
frames[0].save(DESIGN/'flight-preview.gif',save_all=True,append_images=frames[1:],duration=85,loop=0);(DESIGN/'frame.png').unlink()
points=[add(p,origin(b)) for b,ts in MESHES.items() for tri,_,_ in ts for p in tri]
report={'original':True,'source':'scripts/New-BoneDragonAssets.py','game_meshes_used':False,'bones':{b:{'parent':p,'pivot':v} for b,(p,v) in BONES.items()},'meshes':len(MESHES),'triangles':sum(map(len,MESHES.values())),'bounds':{'min':[min(p[j] for p in points) for j in range(3)],'max':[max(p[j] for p in points) for j in range(3)]},'parts':PARTS}
report['assets']={p.relative_to(ROOT).as_posix():hashlib.sha256(p.read_bytes()).hexdigest().upper() for p in (ASSETS/'Models/AbyssBoneDragon.dae',ASSETS/'Textures/AbyssBoneDragon.png')}
(DESIGN/'manifest.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('Original bone dragon:',len(BONES),'bones,',report['triangles'],'triangles. Software preview only.')
