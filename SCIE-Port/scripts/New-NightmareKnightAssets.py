"""Original seven-bone cavalry: horse, armored rider and sword. No game art.
Primitive/export helpers reused from this project's original bone dragon generator.
Previews render the authored triangles in software; they are not game screenshots.
"""
from pathlib import Path
import math,json,hashlib,random
import xml.etree.ElementTree as ET
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[1]
ASSETS=ROOT/'src/SCIEPort/Assets'
DESIGN=ROOT/'design/nightmare-knight'
DESIGN.mkdir(parents=True,exist_ok=True)
PALETTE=[(30,27,35),(51,48,61),(80,85,100),(152,159,173),
 (17,16,24),(154,97,226),(64,41,84),(101,78,48),
 (40,29,37),(108,96,115),(188,193,210),(71,66,86),
 (214,181,248),(93,59,126),(23,22,31),(119,112,135)]
BONES={'Body':(None,(0,0,0)),
 'Leg1':('Body',(-.34,1.235,-.77)), 'Leg2':('Body',(.34,1.235,-.77)),
 'Leg3':('Body',(-.34,1.235,.83)), 'Leg4':('Body',(.34,1.235,.83)),
 'Rider':('Body',(0,1.99,.20)), 'SwordArm':('Rider',(.46,.71,-.08))}
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

# Sculpted equine barrel, lifted withers, sloping neck and long muzzle.
tube('HorseBarrel','Body',[(0,1.23,-.84,.27,.32),(0,1.32,-.57,.43,.46),(0,1.36,.2,.47,.45),(0,1.32,.80,.40,.38),(0,1.30,1.04,.25,.27)],0,10)
tube('HorseNeck','Body',[(0,1.48,-.55,.32,.40),(0,1.83,-.91,.25,.48),(0,2.20,-1.13,.17,.36),(0,2.36,-1.22,.15,.20)],1,8)
tube('HorseHead','Body',[(0,2.40,-1.08,.18,.26),(0,2.30,-1.36,.21,.23),(0,2.13,-1.68,.15,.13)],0,8)
box('Muzzle','Body',(0,2.10,-1.64),(.30,.24,.28),1,.25)
box('NoseArmor','Body',(0,2.30,-1.36),(.32,.10,.38),2,.26)
for side in (-1,1):
 horn('HorseEar','Body',(side*.12,2.54,-1.16),(side*.17,2.93,-1.10),.065,0,5)
 box('HorseSoulEye','Body',(side*.192,2.40,-1.26),(.025,.06,.10),5,.04)
 box('Nostril','Body',(side*.12,2.13,-1.78),(.065,.06,.015),4,.04)
 bonebar('Bridle','Body',(side*.22,2.44,-1.17),(side*.18,2.10,-1.61),.018,8)
 bonebar('Rein','Body',(side*.19,2.17,-1.55),(side*.27,2.44,-.18),.017,8)
 for z in (-.50,.47):box('HorseBarding','Body',(side*.43,1.39,z),(.08,.42,.52),1,.28)
 box('PurpleHarnessTrim','Body',(side*.478,1.45,-.48),(.018,.045,.47),13,.02)
for j in range(7):
 z=-1.10+j*.082
 horn('CroppedMane','Body',(0,2.23-j*.075,z),(0,2.45-j*.078,z+.08),.055,4,5)
bonebar('TailBase','Body',(0,1.45,1.0),(0,1.25,1.40),.085,0)
horn('TailHair','Body',(0,1.25,1.38),(0,.62,1.70),.12,4,6)
# Independent leg bones, shaped knees and dark iron shoes.
for i in range(1,5):
 b='Leg'+str(i);front=i<3;knee=(0,-.52,-.07 if front else .14)
 bonebar('UpperLeg',b,(0,0,0),knee,.105,0)
 box('Knee',b,knee,(.17,.19,.19),1,.22)
 bonebar('LowerLeg',b,knee,(0,-1.01,-.08),.057,0)
 box('Hoof',b,(0,-1.07,-.15),(.21,.16,.29),4,.14)
 box('IronShoe',b,(0,-1.148,-.15),(.22,.045,.30),2,.06)
# Saddle, girth and visible stirrups; rider thighs straddle the horse.
box('SaddlePad','Body',(0,1.78,.17),(.84,.11,.90),6,.20)
box('SaddleSeat','Body',(0,1.89,.19),(.62,.13,.67),8,.22)
for z in (-.19,.60):box('SaddleRim','Body',(0,1.98,z),(.65,.14,.08),7,.14)
for side in (-1,1):
 bonebar('Girth','Body',(side*.44,1.75,.24),(side*.45,.97,.24),.023,8)
 bonebar('StirrupStrap','Body',(side*.40,1.87,.16),(side*.55,1.22,.10),.024,8)
 box('Stirrup','Body',(side*.55,1.20,.08),(.20,.07,.26),2,.10)
# Fully enclosed black-iron knight, layered breastplate and folded cloak.
box('Pelvis','Rider',(0,.05,0),(.54,.26,.39),1,.18)
box('Breastplate','Rider',(0,.45,-.035),(.64,.61,.39),1,.22)
box('ChestRidge','Rider',(0,.49,-.245),(.13,.45,.065),2,.20)
box('ChestSoulSeal','Rider',(0,.52,-.285),(.07,.13,.014),5,.05)
box('Belt','Rider',(0,.18,0),(.59,.09,.43),8,.10)
box('Buckle','Rider',(0,.18,-.23),(.13,.08,.03),7,.10)
box('Gorget','Rider',(0,.80,-.035),(.37,.12,.34),2,.22)
box('ClosedHelm','Rider',(0,1.00,-.055),(.40,.40,.41),1,.28)
box('Brow','Rider',(0,1.08,-.27),(.40,.07,.055),2,.10)
box('VisorSlit','Rider',(0,1.015,-.274),(.30,.037,.02),4,.02)
box('SoulVisor','Rider',(0,1.015,-.289),(.23,.017,.008),5,.01)
box('FaceRidge','Rider',(0,.92,-.28),(.055,.15,.04),2,.10)
horn('HelmetCrest','Rider',(0,1.19,-.03),(0,1.43,.08),.095,2,6)
for side in (-1,1):
 box('Pauldron','Rider',(side*.39,.72,-.01),(.28,.22,.39),2,.25)
 bonebar('SeatedThigh','Rider',(side*.21,.04,-.04),(side*.51,-.21,-.28),.14,1)
 box('KneeGuard','Rider',(side*.52,-.25,-.28),(.22,.25,.23),2,.28)
 bonebar('Greave','Rider',(side*.52,-.32,-.26),(side*.54,-.67,-.05),.09,1)
 box('RidingBoot','Rider',(side*.54,-.72,-.14),(.20,.15,.33),4,.15)
 for j in range(3):box('CloakPlate','Rider',(side*.17,.60-j*.18,.24+j*.025),(.35,.24,.06),6,.08)
bonebar('ReinArmUpper','Rider',(-.40,.67,0),(-.45,.38,-.24),.10,1)
bonebar('ReinArmLower','Rider',(-.45,.38,-.24),(-.23,.36,-.50),.085,1)
box('ReinGauntlet','Rider',(-.23,.36,-.50),(.15,.14,.16),2,.18)
# Right arm and long sword share the seventh bone; the blade swings downward.
bonebar('SwordUpperArm','SwordArm',(0,0,0),(.04,-.25,-.13),.10,1)
bonebar('SwordForearm','SwordArm',(.04,-.25,-.13),(.015,-.30,-.40),.082,1)
box('SwordGauntlet','SwordArm',(.015,-.30,-.40),(.16,.15,.17),2,.18)
bonebar('SwordGrip','SwordArm',(.015,-.30,-.32),(.015,-.30,-.57),.038,8)
box('Crossguard','SwordArm',(.015,-.30,-.58),(.37,.065,.055),7,.12)
# Closed diamond-section blade, with a pale steel edge and narrow purple fuller.
shape('LongswordBlade','SwordArm',[(.015,-.30,-.61),(-.055,-.30,-.68),(.015,-.26,-.68),(.085,-.30,-.68),(.015,-.34,-.68),(.015,-.30,-1.46)],[[0,1,2],[0,2,3],[0,3,4],[0,4,1],[1,5,2],[2,5,3],[3,5,4],[4,5,1]],10)
box('BladeFuller','SwordArm',(.015,-.255,-1.00),(.012,.008,.55),13,.02)

def texture():
 image=Image.new('RGB',(256,256));pix=image.load();rng=random.Random(710070)
 for y in range(256):
  for x in range(256):
   mat=x//64+(y//64)*4;delta=rng.randint(-3,3)
   pix[x,y]=tuple(max(0,min(255,v+delta)) for v in PALETTE[mat])
 draw=ImageDraw.Draw(image)
 for mat in (1,2,3,7,10):
  x=(mat%4)*64;y=(mat//4)*64
  draw.line([(x+5,y+57),(x+5,y+5),(x+58,y+5)],fill=tuple(min(255,c+14) for c in PALETTE[mat]),width=1)
  for k in range(4):
   u=x+12+k*11;draw.line([(u,y+28),(u+5,y+22)],fill=tuple(max(0,c-9) for c in PALETTE[mat]),width=1)
 image.save(ASSETS/'Textures/AbyssNightmareKnight.png')

def export():
    ns='http://www.collada.org/2005/11/COLLADASchema';ET.register_namespace('',ns)
    def e(parent,tag,attrs=None,text=None):
        obj=ET.SubElement(parent,'{'+ns+'}'+tag,attrs or {});obj.text=text;return obj
    doc=ET.Element('{'+ns+'}COLLADA',{'version':'1.4.1'});asset=e(doc,'asset');e(asset,'unit',{'name':'meter','meter':'1'});e(asset,'up_axis',text='Y_UP');lib=e(doc,'library_geometries')
    for b,triangles in MESHES.items():
        name='NightmareKnight_'+b;m=e(e(lib,'geometry',{'id':name,'name':name}),'mesh');pos=[];norm=[];uv=[]
        for ps,n,mat in triangles:
            axis=max(range(3),key=lambda j:abs(n[j]));a,c=[j for j in range(3) if j!=axis]
            for p in ps:
                pos.extend(p);norm.extend(n)
                u=.1+.8*((p[a]*.91+.5)%1);v=.1+.8*((p[c]*.83+.5)%1);uv.extend(((mat%4+u)/4,(mat//4+v)/4))
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
        e(node,'instance_geometry',{'url':'#NightmareKnight_'+b})
    e(e(doc,'scene'),'instance_visual_scene',{'url':'#Scene'});ET.indent(doc)
    ET.ElementTree(doc).write(ASSETS/'Models/AbyssNightmareKnight.dae',encoding='utf-8',xml_declaration=True)
    path=ASSETS/'Models/AbyssNightmareKnight.dae';path.write_bytes(path.read_bytes().replace(b'\r\n',b'\n'))


def rx(p,a):return (p[0],p[1]*math.cos(a)-p[2]*math.sin(a),p[1]*math.sin(a)+p[2]*math.cos(a))
def rz(p,a):return (p[0]*math.cos(a)-p[1]*math.sin(a),p[0]*math.sin(a)+p[1]*math.cos(a),p[2])
def sword_angle(age):
 if age<0 or age>=.64:return -.12
 if age<.12:return -.12+.62*age/.12
 if age<.26:return .5-1.3*(age-.12)/.14
 return -.8+.68*(age-.26)/.38

def worldpoint(b,p,phase,charge,age):
 if b.startswith('Leg'):p=rx(p,(.30+.10*charge)*math.sin(phase+(0 if b in ('Leg1','Leg4') else math.pi)))
 if b=='SwordArm':p=rz(rx(p,sword_angle(age)),.12 if 0<=age<.64 else 0)
 parent,pivot=BONES[b];p=add(p,pivot)
 if parent:
  if parent=='Rider':p=rx(p,-.16*charge)
  p=add(p,origin(parent))
 if b=='Rider':p=add(rx(sub(p,origin(b)),-.16*charge),origin(b))
 return p

def preview(yaw,pitch,name,phase=0,charge=0,age=-1):
 W,H=1100,1000;image=Image.new('RGB',(W,H),(30,32,42));draw=ImageDraw.Draw(image);items=[]
 cy,sy=math.cos(yaw),math.sin(yaw);cp,sp=math.cos(pitch),math.sin(pitch)
 def project(p):
  x,z=p[0]*cy-p[2]*sy,p[0]*sy+p[2]*cy;y=p[1]-1.72
  return (W/2+x*180,H*.53-(y*cp-z*sp)*180,z*cp+y*sp)
 for b,ts in MESHES.items():
  for tri,n,mat in ts:
   ps=[worldpoint(b,p,phase,charge,age) for p in tri];n=cross(sub(ps[1],ps[0]),sub(ps[2],ps[0]));n=mul(n,1/length(n))
   light=.56+.44*max(0,dot(n,(-.35,.83,-.43)))
   if mat==5:light=1.25
   pts=[project(q) for q in ps];items.append((sum(q[2] for q in pts)/3,[(q[0],q[1]) for q in pts],tuple(min(255,int(v*light)) for v in PALETTE[mat])))
 for _,pts,color in sorted(items,reverse=True):draw.polygon(pts,fill=color)
 draw.text((22,22),'NIGHTMARE KNIGHT | ORIGINAL MESH | SOFTWARE PREVIEW - NOT GAMEPLAY',fill=(211,201,225))
 image.save(DESIGN/name);return image
texture();export()
preview(-.74,.22,'preview-three-quarter.png');preview(-1.52,.10,'preview-side.png');preview(.03,.08,'preview-front.png')
frames=[preview(-.74,.22,'frame.png',i*math.tau/14,1 if i<7 else 0,(i-7)*.64/7 if i>=7 else -1).resize((660,600)) for i in range(14)]
frames[0].save(DESIGN/'charge-and-sword.gif',save_all=True,append_images=frames[1:],duration=90,loop=0);(DESIGN/'frame.png').unlink()
points=[add(p,origin(b)) for b,ts in MESHES.items() for tri,_,_ in ts for p in tri]
animated=[worldpoint(b,p,phase,charge,age) for phase in (0,math.pi/2,math.pi,math.pi*1.5) for charge in (0,1) for age in (-1,.12,.22,.26,.5) for b,ts in MESHES.items() for tri,_,_ in ts for p in tri]
def bounds(ps):return {'min':[min(p[j] for p in ps) for j in range(3)],'max':[max(p[j] for p in ps) for j in range(3)]}
report={'original':True,'source':'scripts/New-NightmareKnightAssets.py','game_meshes_used':False,'bones':{b:{'parent':parent,'pivot':pivot} for b,(parent,pivot) in BONES.items()},'meshes':len(MESHES),'triangles':sum(map(len,MESHES.values())),'bounds':bounds(points),'animated_sample_bounds':bounds(animated),'parts':PARTS}
report['assets']={p.relative_to(ROOT).as_posix():hashlib.sha256(p.read_bytes()).hexdigest().upper() for p in (ASSETS/'Models/AbyssNightmareKnight.dae',ASSETS/'Textures/AbyssNightmareKnight.png')}
(DESIGN/'manifest.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('Original nightmare knight:',len(BONES),'bones,',report['triangles'],'triangles; software preview only.')
print(report['bounds']);print(report['animated_sample_bounds'])
