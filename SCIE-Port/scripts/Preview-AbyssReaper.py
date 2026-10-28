"""Software proportion QA, not in-game acceptance."""
import json, math
from pathlib import Path
from PIL import Image, ImageDraw
root=Path(__file__).resolve().parents[1]
parts=json.loads((root/'docs/assets/abyss-reaper/geometry.json').read_text())
colors=[(10,8,17),(30,25,40),(52,40,65),(109,69,35),(176,123,60),(225,174,97),(95,38,158),(186,102,240),(242,221,255)]
im=Image.new('RGB',(1500,880),(22,25,33));d=ImageDraw.Draw(im)
# Views use CSV scales; figure reference = 1.77 native world units.
for ox,oy,scale,angle,title in [(240,420,225,0,'INVENTORY'),(760,470,200*.64,-.31,'FIRST PERSON | .64 scale'),(1210,430,200*.8,.55,'THIRD PERSON | .80 scale')]:
 if ox>1000:
  d.rectangle((ox-105,oy-177,ox-35,oy+177),fill=(48,56,65),outline=(100,108,118));d.ellipse((ox-95,oy-230,ox-45,oy-177),fill=(65,73,84))
 if 500<ox<1000:
  d.line((ox-200,oy-170,ox+210,oy-170),fill=(85,95,108));d.text((ox-180,oy-190),'eye / unobstructed center',fill=(150,155,165))
 polys=[]
 for name,mesh in parts:
  for a,b,c,n,col in mesh:
   ps=[]
   for x,y,z in (a,b,c):ps.append((ox+scale*(x*math.cos(angle)+z*math.sin(angle)),oy-scale*y))
   shade=.55+.45*max(0,-n[2]*.8+n[1]*.3)
   polys.append((sum(p[2] for p in (a,b,c)),ps,tuple(int(v*shade) for v in colors[col])))
 for _,ps,c in sorted(polys,reverse=True):d.polygon(ps,fill=c)
 d.text((ox-160,40),title,fill=(193,178,212))
d.text((30,830),'ABYSS | original geometry | CSV scales | software QA only; game camera, pose and Android acceptance pending',fill=(170,175,185))
im.save(root/'docs/assets/abyss-reaper/proportions.png')
