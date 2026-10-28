"""Optional software model preview for visual QA; Pillow required, no game assets.
This is not a screenshot or proof of Android rendering.
"""
import math
import importlib.util
from pathlib import Path
from PIL import Image, ImageDraw
spec = importlib.util.spec_from_file_location('eye', Path(__file__).with_name('New-OmniscientEyeAssets.py'))
eye = importlib.util.module_from_spec(spec); spec.loader.exec_module(eye)
eye.generate()

def dot(a,b): return sum(x*y for x,y in zip(a,b))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])
def unit(a):
    n=math.sqrt(dot(a,a));return tuple(x/n for x in a)

im = Image.new('RGB',(1400,850),(10,17,33)); draw=ImageDraw.Draw(im)
for camera, origin, title in [((0,.05,-3),(380,390),'FRONT'),((1.4,.65,-3),(1040,390),'THREE-QUARTER')]:
    f=unit(tuple(-x for x in camera));right=unit(cross(f,(0,1,0)));up=cross(right,f)
    projected=[]
    for name, mesh in eye.parts:
        for a,b,c,n,col in mesh:
            if dot(n,f)>=0:continue
            points=[(origin[0]+480*dot(p,right),origin[1]-480*dot(p,up)) for p in (a,b,c)]
            light=.48+.52*max(0,dot(n,unit((-.5,1,-2))))
            color=tuple(round(x*light) for x in eye.COLORS[col])
            projected.append((sum(dot(p,f) for p in (a,b,c))/3,points,color))
    for _,points,color in sorted(projected,reverse=True):draw.polygon(points,fill=color)
    draw.text((origin[0]-70,45),title,fill=(231,186,100))
draw.text((40,800),'THE ALL-SEEING EYE | original geometry | 784 triangles | software QA preview, not game capture',fill=(150,204,218))
out=Path(__file__).resolve().parents[1]/'artifacts/OmniscientEye-preview.png'
out.parent.mkdir(exist_ok=True);im.save(out);print(out)
