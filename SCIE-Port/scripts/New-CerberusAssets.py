"""Original Cerberus anatomy, armor, UV texture and COLLADA skeleton. No game meshes.

Nine rigid bones: one shared three-head assembly, three synchronized jaws and four
walking legs. Software previews use the same triangles; they are NOT game footage.
"""
from pathlib import Path
import math, json, hashlib, random
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'src/SCIEPort/Assets'
DESIGN = ROOT / 'design/cerberus'
DESIGN.mkdir(parents=True, exist_ok=True)
PALETTE = [(28,30,34),(58,63,70),(96,106,116),(157,170,180),
           (9,11,15),(222,243,250),(117,133,148),(50,29,67)]
BONES = {'Body':(None,(0,0,0)), 'Head':('Body',(0,1.65,-.85)),
         'Leg1':('Body',(-.60,1.18,-.73)), 'Leg2':('Body',(.60,1.18,-.73)),
         'Leg3':('Body',(-.56,1.13,.95)), 'Leg4':('Body',(.56,1.13,.95))}
for i,x in enumerate((-.56,0,.56),1): BONES['Jaw'+str(i)]=('Head',(x,-.10,-.28))
MESHES={b:[] for b in BONES}; PARTS=[]
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

# Single powerful, barrel-chested canine torso. Armor is separately sculpted original geometry.
tube('Thorax','Body',[(0,1.42,-1.05,.50,.47),(0,1.39,-.65,.78,.63),(0,1.35,.12,.67,.49),(0,1.32,.70,.48,.39),(0,1.35,1.14,.64,.51),(0,1.30,1.40,.40,.34)],0,12)
shape('Breastbone','Body',[(-.55,1.77,-1.0),(.55,1.77,-1.0),(.37,1.24,-1.04),(0,.95,-.95),(-.37,1.24,-1.04),(-.42,1.7,-.77),(.42,1.7,-.77),(0,1.08,-.76)],[[0,1,2,3,4],[5,7,6],[0,5,6,1],[1,6,7,3,2],[3,7,5,0,4]],1)
for side in (-1,1):
    box('ShoulderCuirass','Body',(side*.64,1.60,-.52),(.48,.76,.90),1,.25,side*.12)
    for i in range(3):
        box('FlankRib','Body',(side*(.65-i*.045),1.35,.04+i*.25),(.17,.62,.19),1,.20,side*.1)
    box('HaunchPlate','Body',(side*.47,1.53,1.01),(.50,.67,.71),1)
for i in range(6):
    z=-.68+i*.33;box('SpinalPlate','Body',(0,1.94-abs(z)*.09,z),(.34,.17,.35),2)
    if i<4:horn('BackSpur','Body',(0,1.99,z),(0,2.16-i*.025,z+.12),.095,1)
for side in (-1,1):
    for i in range(3):box('PurpleFissure','Body',(side*.77,1.66-i*.12,-.58+i*.08),(.018,.14,.045),7,.05)
tube('ArmoredTail','Body',[(0,1.29,1.25,.20,.18),(0,1.31,1.48,.15,.14),(.10,1.46,1.66,.11,.10),(.15,1.65,1.79,.025,.025)],0,8)

# Three visibly separate neck roots feeding the same shoulder assembly; no separate head rotations.
for i,x in enumerate((-.56,0,.56),1):
    yaw=(i-2)*-.10;lift=.04 if i==2 else 0
    tube('Neck'+str(i),'Head',[(x,-.08,.23,.27,.33),(x,.02,-.04,.25,.31),(x,.12+lift,-.22,.24,.27)],0,8)
    tube('Skull'+str(i),'Head',[(x,.22+lift,-.02,.18,.22),(x,.24+lift,-.25,.25,.27),(x,.18+lift,-.50,.19,.19)],1,10)
    box('ForeheadShield'+str(i),'Head',(x,.40+lift,-.34),(.43,.13,.43),2,.18,yaw)
    tube('Muzzle'+str(i),'Head',[(x,.10+lift,-.43,.19,.14),(x,.08+lift,-.74,.16,.11),(x,.10+lift,-.89,.14,.09)],0,8)
    box('Nose'+str(i),'Head',(x,.11+lift,-.90),(.29,.16,.10),4,.10,yaw)
    for side in (-1,1):
        box('CheekArmor','Head',(x+side*.21,.08+lift,-.31),(.11,.29,.37),1,.30,side*.19)
        box('Brow','Head',(x+side*.14,.28+lift,-.51),(.21,.09,.19),2,.15,side*.25)
        box('SoulEye','Head',(x+side*.15,.235+lift,-.555),(.11,.065,.035),5,.10)
        horn('EarHorn','Head',(x+side*.16,.39+lift,-.16),(x+side*.24,.74+lift+(i==2)*.06,.03),.105,1)
        horn('CheekFang','Head',(x+side*.18,.05+lift,-.58),(x+side*.18,-.17+lift,-.62),.047,3)
    box('CrownGroove','Head',(x,.47+lift,-.28),(.035,.025,.28),7,.05)
    jaw='Jaw'+str(i)
    box('LowerJaw','Jaw'+str(i),(0,-.045,-.28),(.34,.14,.63),1,.22)
    box('SoulMouth',jaw,(0,.03,-.35),(.22,.025,.39),5,.04)
    for side in (-1,1):
        for k in range(3):horn('LowerFang',jaw,(side*.12,.015,-.15-k*.15),(side*.12,.12,-.18-k*.15),.033,3)

for index in range(1,5):
    bone='Leg'+str(index);front=index<3
    muscle('MuscularUpper',bone,(0,-.24,.04),(.255,.40,.29),0)
    box('KneeArmor',bone,(0,-.52,-.05 if front else .12),(.45,.28,.48),1,.2)
    box('Shin',bone,(0,-.76,.02 if front else -.06),(.27,.52,.29),0,.2)
    box('Greave',bone,(0,-.77,-.11),(.28,.37,.10),2,.12)
    box('HeavyPaw',bone,(0,-1.02,-.13),(.48,.25,.62),0,.18)
    for toe in (-1,0,1):
        box('ToeArmor',bone,(toe*.145,-.98,-.29),(.135,.14,.24),1,.15)
        horn('Claw',bone,(toe*.145,-1.02,-.40),(toe*.145,-1.09,-.58),.065,3)

# Match the requested world-space envelope exactly, retaining every rig pivot.
global_points=[add(p,origin(b)) for b,ts in MESHES.items() for tri,_,_ in ts for p in tri]
lo=[min(p[j] for p in global_points) for j in range(3)];hi=[max(p[j] for p in global_points) for j in range(3)]
factor=(1.8/(hi[0]-lo[0]),2.5/(hi[1]-lo[1]),3.6/(hi[2]-lo[2]))
offset=(-(hi[0]+lo[0])/2,-lo[1],-(hi[2]+lo[2])/2)
original_origins={b:origin(b) for b in BONES}
def scaled(p):return tuple((p[j]+offset[j])*factor[j] for j in range(3))
new_origins={b:scaled(o) for b,o in original_origins.items()}
for b,(parent,_) in list(BONES.items()):BONES[b]=(parent,sub(new_origins[b],new_origins[parent]) if parent else new_origins[b])
for b,triangles in list(MESHES.items()):
    out=[]
    for tri,n,mat in triangles:
        ps=[sub(scaled(add(p,original_origins[b])),new_origins[b]) for p in tri]
        nn=cross(sub(ps[1],ps[0]),sub(ps[2],ps[0]));nn=mul(nn,1/length(nn));out.append((ps,nn,mat))
    MESHES[b]=out

def texture():
    image=Image.new('RGB',(512,256));pix=image.load();rng=random.Random(311065)
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
    image.save(ASSETS/'Textures/AbyssCerberus.png')

def export():
    ns='http://www.collada.org/2005/11/COLLADASchema';ET.register_namespace('',ns)
    def e(parent,tag,attrs=None,text=None):
        obj=ET.SubElement(parent,'{'+ns+'}'+tag,attrs or {});obj.text=text;return obj
    doc=ET.Element('{'+ns+'}COLLADA',{'version':'1.4.1'});asset=e(doc,'asset');e(asset,'unit',{'name':'meter','meter':'1'});e(asset,'up_axis',text='Y_UP');lib=e(doc,'library_geometries')
    for b,triangles in MESHES.items():
        name='Cerberus_'+b;m=e(e(lib,'geometry',{'id':name,'name':name}),'mesh');pos=[];norm=[];uv=[]
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
        e(node,'instance_geometry',{'url':'#Cerberus_'+b})
    e(e(doc,'scene'),'instance_visual_scene',{'url':'#Scene'});ET.indent(doc)
    ET.ElementTree(doc).write(ASSETS/'Models/AbyssCerberus.dae',encoding='utf-8',xml_declaration=True)
    path=ASSETS/'Models/AbyssCerberus.dae';path.write_bytes(path.read_bytes().replace(b'\r\n',b'\n'))

def preview(yaw,pitch,path,phase=0,bite=None):
    W,H=1050,800;image=Image.new('RGB',(W,H),(18,21,28));draw=ImageDraw.Draw(image);cy,sy=math.cos(yaw),math.sin(yaw);cp,sp=math.cos(pitch),math.sin(pitch)
    def project(p):
        x,z=p[0]*cy-p[2]*sy,p[0]*sy+p[2]*cy;y=p[1]-1.2
        return (W*.5+x*185,H*.53-(y*cp-z*sp)*185,z*cp+y*sp)
    draw.ellipse((210,594,845,735),fill=(11,13,18));items=[]
    for bone,triangles in MESHES.items():
        angle=0
        if bone.startswith('Leg'):angle=.28*math.sin(phase+(0 if bone in ('Leg1','Leg4') else math.pi))
        if bone.startswith('Jaw') and bite is not None:angle=-(.08+.48*bite/.24 if bite<.24 else .025+.1*min(1,(bite-.24)/.34))
        for points,n,mat in triangles:
            world=[]
            for p in points:
                if angle:p=(p[0],p[1]*math.cos(angle)-p[2]*math.sin(angle),p[1]*math.sin(angle)+p[2]*math.cos(angle))
                world.append(add(p,origin(bone)))
            normal=cross(sub(world[1],world[0]),sub(world[2],world[0]));normal=mul(normal,1/length(normal));normal_cam=normal[0]*sy+normal[2]*cy
            if normal_cam*cp+normal[1]*sp>0:continue
            ps=[project(p) for p in world];light=.42+.58*max(0,dot(normal,(-.36,.78,-.51)))
            if mat==5:light=1.08
            color=tuple(min(255,int(v*light)) for v in PALETTE[mat]);items.append((sum(p[2] for p in ps)/3,[(p[0],p[1]) for p in ps],color))
    for _,points,color in sorted(items,reverse=True):draw.polygon(points,fill=color)
    draw.text((25,22),'CERBERUS | ORIGINAL 3D MESH | SOFTWARE PREVIEW - NOT GAMEPLAY',fill=(174,192,207))
    image.save(path);return image

texture();export()
preview(.58,.18,DESIGN/'preview-front.png');preview(math.pi/2,.16,DESIGN/'preview-side.png');preview(math.pi+.5,.22,DESIGN/'preview-rear.png')
frames=[preview(.58,.18,DESIGN/'walk-frame.png',i*math.tau/12).resize((630,480)) for i in range(12)]
frames[0].save(DESIGN/'walk-preview.gif',save_all=True,append_images=frames[1:],duration=90,loop=0);(DESIGN/'walk-frame.png').unlink()
frames=[preview(.40,.12,DESIGN/'bite-frame.png',bite=i*.06).resize((630,480)) for i in range(10)]
frames[0].save(DESIGN/'bite-preview.gif',save_all=True,append_images=frames[1:],duration=90,loop=0);(DESIGN/'bite-frame.png').unlink()
manifest={'original':True,'source':'scripts/New-CerberusAssets.py','game_meshes_used':False,'bounds':[1.8,2.5,3.6],'bones':{b:{'parent':p,'pivot':v} for b,(p,v) in BONES.items()},'triangles':sum(map(len,MESHES.values())),'meshes':len(MESHES),'parts':PARTS}
manifest['assets']={p.relative_to(ROOT).as_posix():hashlib.sha256(p.read_bytes()).hexdigest().upper() for p in (ASSETS/'Models/AbyssCerberus.dae',ASSETS/'Textures/AbyssCerberus.png')}
(DESIGN/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
print(f'Original Cerberus: {manifest["triangles"]} triangles / {len(BONES)} bones / 1.8 x 2.5 x 3.6; software previews authored.')
