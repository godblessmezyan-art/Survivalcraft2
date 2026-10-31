"""Original mesh-native Excalibur art; reuse only the project's COLLADA serializer.
No third-party models, textures, Fate screenshots or proprietary audio are used.
"""
import math, importlib.util, sys, json, hashlib, shutil
from pathlib import Path
from PIL import Image, ImageDraw
sys.dont_write_bytecode = True
ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('excalibur_geometry',Path(__file__).with_name('New-StormhammerAssets.py'))
g=importlib.util.module_from_spec(spec);sys.modules[spec.name]=g;spec.loader.exec_module(g)
ASSETS=ROOT/'src/SCIEPort/Assets';DESIGN=ROOT/'docs/assets/excalibur';DESIGN.mkdir(parents=True,exist_ok=True)
g.MODEL_PATH=ASSETS/'Models/Excalibur.dae';g.TEXTURE_PATH=ASSETS/'Textures/Excalibur.png'

def blade(name, profile, depth, material):
    m=g.Mesh(name,material)
    for face in (-1,1):
        for (y,x),(v,w) in zip(profile,profile[1:]):
            for side in (-1,1):
                # Distinct bevel faces make the silver blade read as metal from either side.
                a=(side*x,y,0);b=(side*w,v,0);c=(side*w*.74,v,face*depth*.45);d=(side*x*.74,y,face*depth*.45)
                m.polygon([a,b,c,d],(0,(y+v)*.5,0));m.polygon([d,c,(0,v,face*depth),(0,y,face*depth)],(0,(y+v)*.5,0))
    return m

def stroke(name,a,b,width,material):
    # Small bevelled rectangular inlays are geometry, so the gold text survives atlas sampling.
    dx,dy=b[0]-a[0],b[1]-a[1];n=math.hypot(dx,dy);ox=-dy/n*width*.5;oy=dx/n*width*.5
    m=g.Mesh(name,material)
    for z in (a[2],a[2]+(.002 if a[2]>0 else -.002)):
        m.polygon([(a[0]+ox,a[1]+oy,z),(b[0]+ox,b[1]+oy,z),(b[0]-ox,b[1]-oy,z),(a[0]-ox,a[1]-oy,z)],(0,(a[1]+b[1])*.5,0))
    return m

def grip_wire():
    m=g.Mesh('FineGoldenGripWire','gold')
    for i in range(144):
        t=i/144;u=(i+1)/144;a=t*math.tau*7;b=u*math.tau*7
        pts=[(.047*math.cos(a),-.36+.46*t-.002,.047*math.sin(a)),(.047*math.cos(b),-.36+.46*u-.002,.047*math.sin(b)),(.047*math.cos(b),-.36+.46*u+.002,.047*math.sin(b)),(.047*math.cos(a),-.36+.46*t+.002,.047*math.sin(a))]
        m.polygon(pts,(0,-.13,0))
    return m

def model():
    meshes=[blade('SilverWhiteDiamondBlade',[(.18,.105),(.33,.12),(1.40,.073),(1.64,.001)],.033,'silver'),
        g.cylinder_y('RoyalBlueLeatherGrip',(0,-.13,0),.046,.49,12,'grip'),
        grip_wire(),
        g.cylinder_y('GoldRicasso',(0,.17,0),.082,.09,12,'gold'),
        g.hex_crystal('SapphirePommel',(0,-.43,0),.083,.064,2,'grip'),
        g.cylinder_y('PommelCrown',(0,-.395,0),.068,.045,12,'gold')]
    for j,y in enumerate((-.36,.10)):
        meshes.append(g.cylinder_y('GripGoldCollar'+str(j),(0,y,0),.059,.032,16,'gold'))
    # Swept, fluted, three dimensional quillons and petal sockets, mirrored on the guard.
    for wing in (-1,1):
        m=g.Mesh('SweptGoldQuillon'+str(wing),'gold')
        outline=[(wing*.035,.12,0),(wing*.24,.16,0),(wing*.36,.25,0),(wing*.32,.11,0),(wing*.11,.035,0)]
        for face in (-1,1):
            ridge=(wing*.18,.12,face*.055)
            for i in range(len(outline)):m.polygon([outline[i],outline[(i+1)%len(outline)],ridge],(wing*.18,.13,0))
        meshes.append(m)
        for j in range(3):
            meshes.append(blade('GuardPetal'+str(wing)+str(j),[(.14+j*.022,.038),(.27+j*.028,.001)],.028,'gold'))
    for face in (-1,1):
        z=face*.058
        meshes.append(g.hex_crystal('BlueGuardJewel'+str(face),(0,.13,z),.063,.032,2,'grip'))
        # Fine geometrical inscriptions, distinct original strokes on both blade faces.
        for i in range(9):
            y=.42+i*.092;z=face*.035
            glyph=[((0,y-.028,z),(0,y+.028,z)),((0,y+.025,z),(.022,y+.006,z)),((0,y,z),(-.018,y-.018,z))]
            if i%2:glyph.append(((-.019,y+.019,z),(.019,y-.019,z)))
            for k,(a,b) in enumerate(glyph):meshes.append(stroke('Inscription'+str(face)+'_'+str(i)+'_'+str(k),a,b,.0045,'rune'))
        # Gold channels bordering the script, and ornamental chevrons at the blade base.
        for wing in (-1,1):
            meshes.append(stroke('GoldScriptRail'+str(face)+str(wing),(wing*.036,.39,face*.033),(wing*.027,1.28,face*.033),.003,'gold'))
            meshes.append(stroke('RicassoChevron'+str(face)+str(wing),(0,.27,face*.035),(wing*.065,.35,face*.03),.009,'gold'))
    return meshes

def main():
    atlas=Image.new('RGB',(1024,1024));d=ImageDraw.Draw(atlas)
    palette={'iron':(98,109,129),'silver':(222,235,249),'gold':(223,166,45),'rune':(255,218,111),'grip':(13,35,99),'connector':(117,87,29),'crystal':(244,251,255),'engraving':(52,76,131)}
    for mat,box in g.ATLAS_REGIONS.items():
        d.rectangle(box,fill=palette[mat]);x0,y0,x1,y1=box
        for y in range(y0,y1):
            v=1+.10*math.sin(y*.031);color=tuple(min(255,int(c*v)) for c in palette[mat]);d.line((x0,y,x1-1,y),fill=color)
    atlas.save(g.TEXTURE_PATH)
    meshes=model();g.write_model(meshes)
    # Serializer identity is Excalibur, no inherited hammer attachment metadata.
    text=g.MODEL_PATH.read_text(encoding='utf-8').replace('Original SCIE-Port Stormhammer','Original SCIE-Port Excalibur').replace('name="Stormhammer"','name="Excalibur"').replace('id="Stormhammer"','id="Excalibur"')
    start=text.index('      <extra>');end=text.index('</extra>',start)+len('</extra>');text=text[:start]+text[end:];g.MODEL_PATH.write_text(text,encoding='utf-8')
    # Preview the very same vertices at an enlarged presentation scale (not game evidence).
    for m in meshes:m.vertices=[(x*1.9,y*1.9-.25,z*1.9) for x,y,z in m.vertices]
    front=g.render_preview(meshes,0);angle=g.render_preview(meshes,.55);sheet=Image.new('RGB',(front.width*2,front.height),(12,18,31));sheet.paste(front,(0,0));sheet.paste(angle,(front.width,0));sheet.save(DESIGN/'model-preview.png')
    audio=ASSETS/'Audio/Excalibur';audio.mkdir(parents=True,exist_ok=True)
    g.write_wav(audio/'Charge.wav',2.8,lambda t,duration,rng: .16*min(1,t/.25)*(1-t/duration)*sum(math.sin(math.tau*f*t) for f in (220,330,440,660))/4)
    g.write_wav(audio/'Release.wav',1.2,lambda t,duration,rng: .38*math.exp(-t*3)*math.sin(math.tau*(92*t+180*t*t))+.1*math.exp(-t*5)*math.sin(math.tau*880*t))
    manifest={'origin':'Original code-native model and synthesized sound; no third-party assets','bones':1,'meshes':len(meshes),'triangles':sum(m.triangle_count for m in meshes),'files':{}}
    for path in [g.MODEL_PATH,g.TEXTURE_PATH,*audio.glob('*.wav')]:manifest['files'][str(path.relative_to(ROOT)).replace('\\','/')]=hashlib.sha256(path.read_bytes()).hexdigest().upper()
    (DESIGN/'model.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({k:v for k,v in manifest.items() if k!='files'}))
if __name__=='__main__':main()
