"""Static CPU z-buffer previews of the candidate geometry, not game screenshots.

Pillow + numpy only. No particles, bloom, emissive shader, generated concept art,
or hidden high-poly substitute. All views read the delivered geometry.json.
"""
from pathlib import Path
import json, math, csv
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parent;OUT=ROOT/'previews';OUT.mkdir(exist_ok=True)
PARTS=json.loads((ROOT/'assets/geometry.json').read_text())
COLORS=np.array(json.loads((ROOT/'assets/palette.json').read_text()),dtype=float)/255
CSV_PATH=ROOT.parents[1]/'src/SCIEPort/Assets/Blocks/AbyssReaper.csv'
ROWS=(list(csv.DictReader(CSV_PATH.open(encoding='utf8'),delimiter=';'))[0] if CSV_PATH.exists() else
      {'FirstPersonOffset':'0.50,-0.38,-0.95','FirstPersonRotation':'0,-8,-8',
       'FirstPersonScale':'0.46','InHandScale':'0.8'})
FONT=Path('C:/Windows/Fonts/msyh.ttc');BOLD=Path('C:/Windows/Fonts/msyhbd.ttc')
def font(n,bold=False):return ImageFont.truetype(str(BOLD if bold else FONT),n)
def unit(v):v=np.array(v,dtype=float);return v/np.linalg.norm(v)
def ry(a):c,s=math.cos(a),math.sin(a);return np.array([[c,0,s],[0,1,0],[-s,0,c]])
def rz(a):c,s=math.cos(a),math.sin(a);return np.array([[c,-s,0],[s,c,0],[0,0,1]])
def rx(a):c,s=math.cos(a),math.sin(a);return np.array([[1,0,0],[0,c,-s],[0,s,c]])
def flatten(parts=PARTS):
    return [(np.array([a,b,c],float),np.array(n,float),co) for _,mesh in parts for a,b,c,n,co in mesh]
BASE=flatten()
def moved(tris,m=np.eye(3),offset=(0,0,0),scale=1):
    return [(p@m.T*scale+np.array(offset),unit(m@n),co) for p,n,co in tris]

def render(tris,w,h,camera=(0,0,6),target=(-.40,0,0),ortho=3.65,fov=55,background=True,clay=False):
    camera=np.array(camera,float);forward=unit(np.array(target)-camera)
    right=unit(np.cross(forward,[0,1,0]));up=unit(np.cross(right,forward))
    basis=np.array([right,up,forward]);view=unit(camera-np.array(target))
    ys,xs=np.mgrid[0:h,0:w]
    rad=((xs-w*.48)/(w*.8))**2+((ys-h*.39)/(h*.9))**2
    if background:
        bg=np.clip(1-rad,0,1)
        pixels=np.empty((h,w,3),dtype=np.float32)
        for j,(lo,hi) in enumerate([(35,89),(39,94),(51,108)]):pixels[:,:,j]=lo+(hi-lo)*bg
    else:pixels=np.ones((h,w,3),dtype=np.float32)*np.array([31,35,45])
    zbuf=np.full((h,w),np.inf);mask=np.zeros((h,w),bool)
    key=unit([-.45,.73,.66]);fill=unit([.75,.16,.50]);rim=unit([.18,.65,-.80])
    focal=h/(2*math.tan(math.radians(fov)/2))
    for world,n,col in tris:
        p=(world-camera)@basis.T
        if min(p[:,2])<=.02:continue
        if ortho is not None:
            uv=np.column_stack([w/2+p[:,0]*h/ortho,h/2-p[:,1]*h/ortho])
        else:uv=np.column_stack([w/2+p[:,0]/p[:,2]*focal,h/2-p[:,1]/p[:,2]*focal])
        x0=max(0,int(np.floor(uv[:,0].min())));x1=min(w-1,int(np.ceil(uv[:,0].max())))
        y0=max(0,int(np.floor(uv[:,1].min())));y1=min(h-1,int(np.ceil(uv[:,1].max())))
        if x0>x1 or y0>y1:continue
        a,b,c=uv
        den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
        if abs(den)<1e-8:continue
        y,x=np.mgrid[y0:y1+1,x0:x1+1];x=x+.5;y=y+.5
        u=((b[1]-c[1])*(x-c[0])+(c[0]-b[0])*(y-c[1]))/den
        v=((c[1]-a[1])*(x-c[0])+(a[0]-c[0])*(y-c[1]))/den
        q=1-u-v;inside=(u>=-1e-7)&(v>=-1e-7)&(q>=-1e-7)
        depth=u*p[0,2]+v*p[1,2]+q*p[2,2] if ortho is not None else 1/(u/p[0,2]+v/p[1,2]+q/p[2,2])
        old=zbuf[y0:y1+1,x0:x1+1];visible=inside&(depth<old)
        if not visible.any():continue
        color=np.array([.29,.32,.36]) if clay else (COLORS[col] if col<16 else np.array([.28,.32,.39]))
        # Fixed studio lighting. No time/energy brightness term; every purple
        # surface responds to lighting exactly like the rest of the geometry.
        diffuse=.48+.64*max(0,np.dot(n,key))+.22*max(0,np.dot(n,fill))+.24*max(0,np.dot(n,rim))
        halfvec=unit(key+view);spec=max(0,np.dot(n,halfvec))**32
        strength=.06 if col<6 else .14 if col<10 else .018
        if col==0:strength=.002
        rgb=np.clip(color*diffuse+np.array([.84,.91,1.])*spec*strength,0,1)*255
        old[visible]=depth[visible];pixels[y0:y1+1,x0:x1+1][visible]=rgb
        mask[y0:y1+1,x0:x1+1][visible]=True
    return Image.fromarray(np.uint8(pixels)),Image.fromarray(np.uint8(mask)*255)

def label(im,number,title,subtitle,footer='紫曜巨镰 A3 · 静态几何预览 · 无粒子 / 无泛光 / 无动态发光'):
    d=ImageDraw.Draw(im);w,h=im.size
    d.rectangle((0,0,w,104),fill=(18,21,29));d.text((28,18),number+'  '+title,font=font(30,True),fill=(225,229,237))
    d.text((30,63),subtitle,font=font(16),fill=(152,163,183))
    d.rectangle((0,h-48,w,h),fill=(18,21,29));d.text((28,h-34),footer,font=font(14),fill=(145,155,174))
    return im

views=[('01-front','01','正面完整武器','厚刃根 → 残月腹 → 针状刃尖',0,0),
       ('02-side45','02','侧面 45°','检查复合斜面、碎刃厚度与弧形护柄',45,0),
       ('03-perspective','03','三维透视','偏心碎甲围绕黑色事件视界',-32,16)]
for name,no,title,subt,yaw,pitch in views:
    m=rx(math.radians(pitch))@ry(math.radians(yaw))
    t=moved(BASE,m)
    # Looking at center of bounds, never a separate concept-model asset.
    allp=np.concatenate([p for p,_,_ in t]);center=(allp.max(0)+allp.min(0))/2
    camera=center+np.array([0,0,6.9])
    im,_=render(t,1050,1300,camera,center,3.90 if name!='03-perspective' else None,fov=31)
    label(im,no,title,subt).save(OUT/(name+'.png'))
    print(name,flush=True)

# Inventory sizes. Downsample the same actual geometry, no hand-painted icon.
t=moved(BASE,ry(math.radians(-10))@rz(math.radians(-8)))
allp=np.concatenate([p for p,_,_ in t]);center=(allp.max(0)+allp.min(0))/2
icon,mask=render(t,600,600,center+[0,0,6],center,3.80,background=False)
icon.save(OUT/'icon-source.png')
im=Image.new('RGB',(1050,1300),(25,29,39));d=ImageDraw.Draw(im)
im.paste(icon.resize((610,610),Image.Resampling.LANCZOS),(220,130))
for x,size in zip([115,340,565,790],[32,48,64,96]):
    tile=icon.resize((size,size),Image.Resampling.LANCZOS)
    tile.save(OUT/f'icon-{size}.png')
    im.paste(tile,(x+int((140-size)/2),810+int((96-size)/2)))
    im.paste(tile.resize((140,140),Image.Resampling.NEAREST),(x,950))
    d.text((x,1130),f'{size} × {size}',font=font(20),fill=(195,203,217))
d.text((65,772),'实际像素尺寸',font=font(18),fill=(143,155,176))
d.text((65,918),'放大检查轮廓',font=font(18),fill=(143,155,176))
label(im,'04','物品栏缩小检查','同一模型直接缩小；下排为最近邻放大检查').save(OUT/'04-inventory.png')
print('04-inventory',flush=True)

# First person: use UNMODIFIED runtime CSV idle scale/rotation/offset, fixed
# aspect matching user's screenshot. This is a framing reference, not native UI.
offset=np.array(list(map(float,ROWS['FirstPersonOffset'].split(','))))
angles=list(map(float,ROWS['FirstPersonRotation'].split(',')))
m=ry(math.radians(angles[1]))@rz(math.radians(angles[2]))
fp=moved(BASE,m,offset,float(ROWS['FirstPersonScale']))
im,mask=render(fp,1664,768,(0,0,0),(0,0,-1),None,75,False)
# Replace only background pixels with a restrained sample landscape.
bg=Image.new('RGB',im.size);d=ImageDraw.Draw(bg)
for y in range(768):
    if y<390:c=(94-int(y*.035),112-int(y*.025),132-int(y*.01))
    else:c=(39-int((y-390)*.026),53-int((y-390)*.025),60-int((y-390)*.025))
    d.line((0,y,1664,y),fill=c)
for x in range(-1000,2800,230):d.line((832,390,x,768),fill=(56,69,74),width=1)
for y in [405,430,468,525,610,730]:d.line((0,y,1664,y),fill=(56,69,74),width=1)
bg.paste(im,(0,0),mask);d=ImageDraw.Draw(bg)
d.line((825,384,839,384),fill=(225,227,230),width=2);d.line((832,377,832,391),fill=(225,227,230),width=2)
# Control silhouettes convey mobile occlusion without claiming actual UI bounds.
for x in [36,1468]:
    d.rounded_rectangle((x,550,x+160,710),radius=28,outline=(129,142,154),width=2)
    d.text((x+42,610),'触控区',font=font(20),fill=(159,170,181))
label(bg,'05','第一人称手持比例','沿用当前 CSV：scale 0.46 / offset 0.50, −0.38, −0.95 / FOV 75°',
      '离线视锥参考；原生 FOV、手臂、攻击动画与 UI 遮挡仍待接入后实机确认').save(OUT/'05-first-person.png')
print('05-first-person',flush=True)

# Original block-proportioned mannequin, no game meshes/textures distributed.
MAN=[]
def box(center,size,col=16):
    x,y,z=center;a,b,c=np.array(size)/2
    pts=np.array([[x-a,y-b,z-c],[x+a,y-b,z-c],[x+a,y+b,z-c],[x-a,y+b,z-c],
                  [x-a,y-b,z+c],[x+a,y-b,z+c],[x+a,y+b,z+c],[x-a,y+b,z+c]])
    for inds in [(0,3,2,1),(4,5,6,7),(0,4,7,3),(1,2,6,5),(3,7,6,2),(0,1,5,4)]:
        a,b,c,d=pts[list(inds)];n=unit(np.cross(b-a,c-a));MAN.append((np.array([a,b,c]),n,col));MAN.append((np.array([a,c,d]),n,col))
def limb(a,b,width,depth):
    a=np.array(a);b=np.array(b);direction=unit(b-a);side=unit(np.cross(direction,[0,0,1]));z=unit(np.cross(side,direction))
    frame=np.array([side,direction,z]).T
    begin=len(MAN);box((0,0,0),(width,np.linalg.norm(b-a),depth))
    MAN[begin:]=moved(MAN[begin:],frame,(a+b)/2)
box((0,1.62,0),(.31,.30,.28));box((0,1.19,0),(.46,.54,.25));box((0,.88,0),(.42,.14,.24))
for side in (-1,1):
    box((side*.115,.44,0),(.19,.76,.23));box((side*.115,.05,.045),(.21,.10,.33))
limb((-.31,1.40,0),(-.38,.92,.025),.16,.19)
limb((.31,1.40,0),(.42,1.12,.07),.16,.19);limb((.42,1.12,.07),(.47,1.22,.21),.15,.18)
hand=np.array([.47,1.22,.21])
# Current item scale/roll on a proposed chest-height reference pose. The manikin
# is NOT the game's rig; this must not be presented as native idle animation QA.
held=moved(BASE,rz(math.radians(-12)),hand+np.array([0,.12,0]),float(ROWS['InHandScale']))
scene=MAN+held
t=moved(scene,ry(math.radians(-18)))
im,_=render(t,1050,1300,camera=(0,1.35,7),target=(-.20,1.32,0),ortho=3.60)
d=ImageDraw.Draw(im)
y_top=650-(1.77-1.32)*1300/3.6;y_ground=650-(0-1.32)*1300/3.6
d.line((875,y_top,875,y_ground),fill=(142,155,174),width=2)
for y in [y_top,y_ground]:d.line((864,y,886,y),fill=(142,155,174),width=2)
d.text((900,790),'1.77 m',font=font(18),fill=(191,202,216))
label(im,'06','第三人称玩家持握比例','1.77 m 设计人台 · 当前 InHandScale 0.8 · 约 2.67 m 级长柄',
      '胸前持握设计参考，未套用游戏骨骼；双手长柄造型不代表新增双手动作').save(OUT/'06-third-person.png')
print('06-third-person',flush=True)

# Inspection-only closeups and neutral clay: expose real thickness and design.
head=[tr for tr in BASE if tr[0][:,1].max()>.72 and tr[0][:,0].max()>-.48]
t=moved(head,ry(math.radians(-24)))
im,_=render(t,1300,1000,camera=(0,.98,5),target=(0,.98,0),ortho=1.28)
label(im,'07','镰首 / 黑洞凹腔','不对称碎甲与间断能量碎屑；没有同心靶环').save(OUT/'07-core-detail.png')
im,_=render(moved(BASE,ry(math.radians(55))),1050,1300,camera=(0,0,6),target=(-.24,0,0),ortho=3.9,clay=True)
label(im,'08','无色几何检查','统一灰模，检查刃口斜面、厚度与护柄实体结构').save(OUT/'08-clay.png')

# Six requested views in a readable review board.
sheet=Image.new('RGB',(2100,1810),(12,15,22));d=ImageDraw.Draw(sheet)
d.text((44,22),'蚀月 · 破碎神镰',font=font(43,True),fill=(232,234,241))
d.text((47,80),'ABYSS / ECLIPSE     紫曜巨镰 A3 · 加大残月 / 修长弧形护柄 / 紫晶裂隙 · 未替换运行资源',font=font(22),fill=(181,165,209))
paths=['01-front','02-side45','03-perspective','04-inventory','05-first-person','06-third-person']
for i,name in enumerate(paths):
    x=24+(i%3)*692;y=135+(i//3)*800
    tile=Image.new('RGB',(668,775),(22,26,35));im=Image.open(OUT/(name+'.png'))
    if name=='05-first-person':
        im.thumbnail((668,520),Image.Resampling.LANCZOS);tile.paste(im,((668-im.width)//2,110))
        td=ImageDraw.Draw(tile);td.text((25,25),'05  第一人称比例',font=font(26,True),fill=(220,226,235))
        td.text((25,590),'沿用旧版持握参数，未修改运行资源',font=font(19),fill=(153,166,186))
        td.text((25,632),'横屏 2772:1280 · 视锥参考',font=font(19),fill=(153,166,186))
    else:
        im.thumbnail((668,775),Image.Resampling.LANCZOS);tile.paste(im,((668-im.width)//2,(775-im.height)//2))
    sheet.paste(tile,(x,y))
d.text((40,1760),'全部视图来自同一个候选网格；无粒子 / 无泛光 / 无动态发光。工作室打光与人台比例均非游戏内截图。',font=font(20),fill=(150,162,182))
sheet.save(OUT/'review-board.png')
print('review-board',flush=True)

# Quantitative framing references; do not imply native-camera acceptance.
blade=[p for name,mesh in PARTS if name.startswith('Crescent_') for a,b,c,_,_ in mesh for p in (a,b,c)]
blade=np.array(blade)
m=ry(math.radians(angles[1]))@rz(math.radians(angles[2]))
points=blade@m.T*float(ROWS['FirstPersonScale'])+offset
report={}
for fov in (65,75,85):
    sy=math.tan(math.radians(fov)/2);sx=sy*2772/1280
    projected=np.column_stack([points[:,0]/(-points[:,2]*sx),points[:,1]/(-points[:,2]*sy)])
    report[str(fov)]={'bladeWithinIdleFrustum':bool(np.all(np.abs(projected)<1)),
                      'ndcMin':projected.min(0).tolist(),'ndcMax':projected.max(0).tolist()}
(OUT/'framing.json').write_text(json.dumps(report,indent=2),encoding='utf8')
