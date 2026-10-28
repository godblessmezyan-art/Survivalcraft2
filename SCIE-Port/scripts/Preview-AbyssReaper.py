"""Reproducible software QA: native idle CSV transforms, candidate FOV75; no device claim."""
import json, math, csv
from pathlib import Path
from PIL import Image,ImageDraw
root=Path(__file__).resolve().parents[1];out=root/'docs/assets/abyss-reaper'
parts=json.loads((out/'geometry.json').read_text());colors=json.loads((out/'palette.json').read_text())
row=list(csv.DictReader((root/'src/SCIEPort/Assets/Blocks/AbyssReaper.csv').open(encoding='utf8'),delimiter=';'))[0]
def rot(p,yaw,roll):
 x,y,z=p;c=math.cos(roll);s=math.sin(roll);x,y=x*c-y*s,x*s+y*c
 c=math.cos(yaw);s=math.sin(yaw);return (x*c+z*s,y,-x*s+z*c)
def transform(p,mode):
 if mode=='FP':
  scale=float(row['FirstPersonScale']);yaw,roll=[math.radians(float(row['FirstPersonRotation'].split(',')[i])) for i in (1,2)]
  q=rot(tuple(v*scale for v in p),yaw,roll);offset=list(map(float,row['FirstPersonOffset'].split(',')))
  return tuple(q[i]+offset[i] for i in range(3))
 return rot(p,.45 if mode=='THIRD' else -.25,0)
for mode in ('ICON','FP','THIRD','BACK'):
 im=Image.new('RGB',(1100,680),(23,27,36));d=ImageDraw.Draw(im);polys=[]
 if mode=='FP':
  im=Image.new('RGB',(1100,508),(51,70,87));d=ImageDraw.Draw(im);d.rectangle((0,260,1100,508),fill=(24,45,49))
  d.line((543,254,557,254),fill=(170,180,185));d.line((550,247,550,261),fill=(170,180,185))
  focal=254/math.tan(math.radians(75)/2)
  def proj(p):return (550+focal*p[0]/(-p[2]),254-focal*p[1]/(-p[2]))
 else:
  def proj(p):return (600+p[0]*210,330-p[1]*210)
 if mode=='THIRD':
  d.rectangle((260,190,330,555),fill=(58,68,81));d.ellipse((266,135,322,190),fill=(85,94,106));d.text((235,580),'1.77m reference',fill=(170,180,185))
 for name,mesh in parts:
  for a,b,c,n,col in mesh:
   vs=[transform(p,'ICON' if mode=='BACK' else mode) for p in (a,b,c)]
   if mode=='BACK':vs=[(-x,y,-z) for x,y,z in vs]
   if mode=='FP' and any(p[2]>=-.01 for p in vs):continue
   normal=rot(n,.45 if mode=='THIRD' else -.25,0)
   shade=1 if col>=12 else .42+.58*max(0,normal[0]*-.3+normal[1]*.5-normal[2]*.8)
   poly=[proj(p) for p in vs];color=tuple(int(v*shade) for v in colors[col]);polys.append((sum(p[2] for p in vs),poly,color))
 for _,poly,color in sorted(polys,reverse=True):d.polygon(poly,fill=color)
 d.text((24,22),'ABYSS V0.1.1 / '+mode+' / software QA only',fill=(215,207,230))
 if mode=='FP':d.text((24,im.height-25),'CSV idle transform | FOV75 candidate | native camera, controls and animation pending device acceptance',fill=(190,199,210))
 im.save(out/('preview-'+mode.lower()+'.png'))
# Blade's inward direction and idle frustum bounds across two plausible vertical FOVs.
blade=[transform(p,'FP') for name,mesh in parts if name.startswith('BrokenBlade') for a,b,c,_,_ in mesh for p in (a,b,c)]
for fov in (65,85):
 ty=math.tan(math.radians(fov)/2);tx=ty*2772/1280
 for x,y,z in blade:assert z<0 and abs(x/(-z*tx))<1 and abs(y/(-z*ty))<1
print('CSV idle blade framing passed at vertical FOV65/85; device camera remains pending.')
