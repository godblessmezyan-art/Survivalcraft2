"""Original Eclipse design candidate A. Never writes to runtime Assets.

Deterministic, standard-library mesh/PNG authoring. All geometry baked, one Y_UP
identity root. Palette meanings are candidate-specific, NOT v0.1.1's shader map.
"""
from pathlib import Path
import math, json, struct, zlib, hashlib
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent
OUT = ROOT / 'assets'
OUT.mkdir(exist_ok=True)
# User approved silhouette; requested stronger violet identity. Purple metal is
# lit normally, energy highlights remain sparse. Not an emissive purple sheet.
PALETTE = [(8,4,18),(23,13,39),(48,27,75),(66,42,96),
           (87,61,121),(113,83,146),(126,103,163),(158,140,184),
           (196,184,222),(227,219,240),(26,17,38),(49,31,64),
           (73,32,117),(133,64,189),(184,121,231),(222,180,251)]
PARTS = []

def sub(a,b): return tuple(x-y for x,y in zip(a,b))
def add(a,b): return tuple(x+y for x,y in zip(a,b))
def mul(a,s): return tuple(x*s for x in a)
def dot(a,b): return sum(x*y for x,y in zip(a,b))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def length(a): return math.sqrt(dot(a,a))
def mix(a,b,t): return add(mul(a,1-t),mul(b,t))

def tri(mesh,a,b,c,col):
    n=cross(sub(b,a),sub(c,a)); size=length(n)
    if size < 1e-10: raise ValueError('Degenerate triangle')
    mesh.append([list(a),list(b),list(c),list(mul(n,1/size)),col])

def quad(mesh,a,b,c,d,col):
    tri(mesh,a,b,c,col);tri(mesh,a,c,d,col)

def closed(name,mesh):
    volume=sum(dot(a,cross(b,c))/6 for a,b,c,_,_ in mesh)
    if volume < 0:
        mesh=[[a,c,b,[-x for x in n],co] for a,b,c,n,co in mesh]
    PARTS.append([name,mesh])

def cat(vals,t):
    k=int(t); u=t-k
    if k>=len(vals)-1: return tuple(vals[-1])
    a,b,c,d=[vals[min(max(j,0),len(vals)-1)] for j in (k-1,k,k+1,k+2)]
    return tuple(.5*(2*b[i]+(-a[i]+c[i])*u+(2*a[i]-5*b[i]+4*c[i]-d[i])*u*u+
                     (-a[i]+3*b[i]-3*c[i]+d[i])*u*u*u) for i in range(len(a)))

# Individually authored outer/inner contours. No concentric radius or uniform width.
OUTER=[(.15,1.22),(-.10,1.48),(-.42,1.56),(-.72,1.48),(-.96,1.32),
       (-1.16,1.09),(-1.28,.80),(-1.30,.49),(-1.24,.20),(-1.10,-.08),
       (-.91,-.31),(-.70,-.49)]
INNER=[(-.035,.85),(-.28,1.01),(-.49,1.14),(-.64,1.16),(-.82,1.05),
       (-1.00,.89),(-1.12,.69),(-1.14,.46),(-1.10,.23),(-1.015,-.005),
       (-.88,-.245),(-.70,-.49)]
HALF=[(.119,),(.108,),(.092,),(.078,),(.069,),(.059,),(.051,),(.043,),(.034,),(.025,),(.014,),(.0008,)]
# A3 user direction: giant curved blade and an elegant long haft, taking only
# proportion/flow cues from Castorice. No borrowed mesh, silhouette tracing,
# butterfly, eye, filigree, or texture asset.
def enlarged(p):return (p[0]*1.38,.975+(p[1]-.975)*1.13,p[2]*1.10)
# Contour profile across each blade: forged spine, raised ridge, main face,
# hollow-ground shoulder, exceptionally thin cutting land, mirrored underside.
PROFILE=[(0,-.30),(0,.30),(.065,.80),(.24,1.0),(.65,.67),(.84,.39),(.986,.022),(1,0),
         (.986,-.022),(.84,-.39),(.65,-.67),(.24,-1.0),(.065,-.80)]
STRIP_COL=[3,5,3,2,3,6,8,8,6,3,2,3,5]

def bladepoint(t,f,z):
    # Widen blade belly independently of its arc span; root and tip still taper.
    p=mix(cat(OUTER,t),cat(INNER,t),.5+(f-.5)*1.18); h=cat(HALF,t)[0]
    # Subtle torsion, baked into positions rather than an animated bone.
    twist=.012*math.sin(t*.45)*(f-.4)
    return enlarged((p[0],p[1],h*z+twist))

def fracture_delta(f,seed):
    return .055*math.sin(f*13+seed)+.018*math.sin(f*31+seed)

def blade(name,start,end,seed,tip=False):
    mesh=[];n=max(12,int((end-start)*9));loops=[]
    for i in range(n+1):
        t=start+(end-start)*i/n
        ring=[]
        for f,z in PROFILE:
            delta=(fracture_delta(f,seed) if i==0 and start else
                   fracture_delta(f,seed+2) if i==n and not tip else 0)
            ring.append(bladepoint(t+delta,f,z))
        loops.append(ring)
    for i in range(n):
        for j,col in enumerate(STRIP_COL):
            quad(mesh,loops[i][j],loops[i+1][j],loops[i+1][(j+1)%len(PROFILE)],loops[i][(j+1)%len(PROFILE)],col)
    for i in (0,n):
        center=tuple(sum(p[j] for p in loops[i])/len(PROFILE) for j in range(3))
        for j in range(len(PROFILE)):
            if i==0: tri(mesh,center,loops[i][j],loops[i][(j+1)%len(PROFILE)],12)
            else: tri(mesh,center,loops[i][(j+1)%len(PROFILE)],loops[i][j],12)
    if tip:
        # End at an actual shared apex, not a flat-ended equal-width arc.
        # Last sample is short of t=11; add needle termination.
        apex=enlarged((-.70,-.49,0))
        # Remove the last cap, connect every profile face to the same tip.
        del mesh[-len(PROFILE):]
        for j in range(len(PROFILE)):
            tri(mesh,loops[-1][j],apex,loops[-1][(j+1)%len(PROFILE)],STRIP_COL[j])
    closed(name,mesh)

blade('Crescent_01_Root',0,3.20,1)
blade('Crescent_02_Fall',3.36,7.24,3)
blade('Crescent_03_Fang',7.43,10.90,5,True)

def blade_ribbon(name,start,end,f0,f1,height,colors):
    """Solid raised inlay following the blade's compound contour."""
    mesh=[];n=max(3,int((end-start)*9));rings=[]
    for i in range(n+1):
        t=start+(end-start)*i/n
        # pointed taper on ornamental overlays, not rectangular strips
        taper=.09+.91*math.sin(math.pi*i/n)**.5
        mid=(f0+f1)/2;w=(f1-f0)*taper/2
        for_side=[]
        for f,z in [(mid-w,height),(mid+w,height),(mid+w,height-.055),(mid-w,height-.055)]:
            for_side.append(bladepoint(t,f,z))
        rings.append(for_side)
    for i in range(n):
        for j in range(4):quad(mesh,rings[i][j],rings[i+1][j],rings[i+1][(j+1)%4],rings[i][(j+1)%4],colors[j%len(colors)])
    quad(mesh,*rings[0],colors[0]);quad(mesh,*reversed(rings[-1]),colors[0])
    closed(name,mesh)

def blade_inset(name,path,side,width=.006,color=12):
    """Broken engraved channel on the actual swept blade surface, solid inlay.
    Path is authored in longitudinal / transverse blade coordinates.
    """
    mesh=[];loops=[]
    for t,f in path:
        # Linear interpolation of the same cross section used by the main blade.
        front=PROFILE[1:8]
        za=0
        for (fa,zz),(fb,zb) in zip(front,front[1:]):
            if fa<=f<=fb:za=zz+(zb-zz)*(f-fa)/(fb-fa);break
        lift=.008 if color>=12 else 0
        loops.append([bladepoint(t,ff,side*(za+dz+lift)) for ff,dz in
                     [(f-width,.025),(f+width,.025),(f+width,.009),(f-width,.009)]])
    for i in range(len(loops)-1):
        for j in range(4):quad(mesh,loops[i][j],loops[i+1][j],loops[i+1][(j+1)%4],loops[i][(j+1)%4],color)
    quad(mesh,*loops[0],color);quad(mesh,*reversed(loops[-1]),color)
    closed(name,mesh)

# Three defined structural layers; silhouette not dependent on painted lines.
for seg,(lo,hi) in enumerate([(0.10,2.95),(3.59,6.90),(7.70,9.90)]):
    for side in (-1,1):
        blade_ribbon(f'ArmorStratum_{seg}_{side}',lo,hi,.10,.33,side*1.075,[3,5,2,3])
        # Interrupted recessed void seams, NOT a continuous luminous outline.
        for k,(a,b) in enumerate([(lo+.13,lo+.49),(hi-.48,hi-.14)]):
            blade_ribbon(f'VoidSeam_{seg}_{side}_{k}',a,b,.455,.473,side*.885,[13,12,12,12])
for side in (-1,1):
    blade_ribbon('EdgeSpark_Root_'+str(side),.55,1.05,.918,.932,side*.18,[14,12,12,12])
    blade_ribbon('EdgeSpark_Fang_'+str(side),8.33,8.83,.918,.932,side*.18,[13,12,12,12])
    # Authored branching fractures, shorter and thinner than the structural ridges.
    for k,path in enumerate([[(.62,.37),(.86,.49),(1.03,.43),(1.26,.59),(1.47,.64)],
                            [(4.00,.39),(4.25,.52),(4.44,.47),(4.75,.67)],
                            [(8.03,.43),(8.19,.62),(8.43,.69)]]):
        blade_inset('FractureBed_'+str(side)+'_'+str(k),path,side,.016,0)
        blade_inset('FractureThread_'+str(side)+'_'+str(k),path,side,.0045,13)

def polygon_area(poly): return sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(poly,poly[1:]+poly[:1]))/2
def ears(poly):
    """Ear clipping for genuinely concave armor outlines."""
    ids=list(range(len(poly)));result=[]
    def cr(a,b,c):return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
    while len(ids)>3:
        for k in range(len(ids)):
            a,b,c=ids[k-1],ids[k],ids[(k+1)%len(ids)]
            if cr(poly[a],poly[b],poly[c])<1e-10:continue
            if any(all(cr(poly[u],poly[v],poly[j])>=-1e-10 for u,v in ((a,b),(b,c),(c,a))) for j in ids if j not in (a,b,c)):continue
            result.append((a,b,c));ids.pop(k);break
        else: raise ValueError('Polygon cannot triangulate')
    result.append(tuple(ids));return result

def armor(name,poly,depth,face=2,edge=5,zc=0,bevel=.045):
    if polygon_area(poly)<0:poly=list(reversed(poly))
    center=(sum(p[0] for p in poly)/len(poly),sum(p[1] for p in poly)/len(poly))
    inset=[mix(p,center,bevel) for p in poly];n=len(poly);mesh=[]
    for side in (-1,1):
        pp=[(x,y,zc+side*depth) for x,y in inset]
        edgep=[(x,y,zc+side*depth*.68) for x,y in poly]
        for a,b,c in ears(inset):tri(mesh,pp[a],pp[b] if side>0 else pp[c],pp[c] if side>0 else pp[b],face)
        for j in range(n):
            a,b=j,(j+1)%n
            vs=[edgep[a],edgep[b],pp[b],pp[a]]
            quad(mesh,*(vs if side>0 else list(reversed(vs))),edge)
    for j in range(n):
        a,b=poly[j],poly[(j+1)%n]
        quad(mesh,(a[0],a[1],zc-depth*.68),(b[0],b[1],zc-depth*.68),
             (b[0],b[1],zc+depth*.68),(a[0],a[1],zc+depth*.68),3)
    closed(name,mesh)

# Two purposeful reverse thorns, broken dorsal armor; no decorative chain.
armor('DorsalHook',[(.035,1.18),(.20,1.27),(.36,1.49),(.32,1.20),(.23,1.10),(.15,.99),(.085,1.07)],.090,2,5)
armor('ReverseThorn',[(.11,1.01),(.27,1.01),(.43,1.14),(.37,.94),(.26,.86),(.15,.88)],.068,1,4)
armor('RootCrest',[(-.31,1.46),(-.37,1.64),(-.21,1.55),(-.08,1.53),(.06,1.40),(-.10,1.42)],.068,1,4)
armor('NeckArmor',[(-.10,.94),(.11,.96),(.14,.78),(.095,.59),(.025,.45),(-.07,.59)],.097,2,5)
for side in (-1,1):
    # Irregular root-scale overlaps the blade's thickest third and gives the
    # mounting area its own forged silhouette, distinct from middle/fang pieces.
    armor('RootScale_'+str(side),[(.06,1.215),(-.10,1.387),(-.29,1.449),(-.26,1.350),
          (-.32,1.303),(-.19,1.321),(-.075,1.240),(-.044,1.127)],.012,2,4,side*.111,.06)

# Open, asymmetrical, non-concentric fragments surrounding a recessed black core.
# Each is a bespoke broken jaw; no torus or circular target geometry.
fragments=[('North',[(-.215,1.035),(-.12,1.19),(.03,1.205),(.085,1.155),(-.055,1.145),(-.125,1.075)],.135),
           ('East',[(.17,1.12),(.244,1.035),(.23,.90),(.175,.845),(.182,.975),(.138,1.05)],.163),
           ('South',[(.12,.785),(-.015,.735),(-.195,.835),(-.225,.915),(-.126,.84),(-.015,.814),(.052,.852)],.115)]
for side in (-1,1):
    for name,poly,z in fragments:armor('ShatteredOrbit_'+name+'_'+str(side),poly,.018,2,6,side*(z+.065),.10)

def void_well():
    # Double-sided concave well; its dark interior sits in front of the blade's
    # mounting plate to prevent that plate clipping through the event horizon.
    mesh=[];n=18;cx,cy=-.015,.975
    radius=[1,.97,1.01,.96,.98,1.02,.96,1,.98,1.01,.97,.99,1.02,.96,.99,1,.98,1.01]
    def p(i,r,z):
        a=2*math.pi*(i%n)/n;rr=radius[i%n]
        return (cx+.143*r*rr*math.cos(a),cy+.154*r*rr*math.sin(a),z)
    for side in (-1,1):
        for i in range(n):
            # Sloping facets are slightly lifted charcoal so their depth survives
            # a static light; the bottom remains almost-black and off-center.
            a=p(i,1,side*.205);b=p(i+1,1,side*.205)
            c=p(i+1,.50,side*.142);d=p(i,.50,side*.142)
            verts=[a,b,c,d] if side>0 else [d,c,b,a]
            quad(mesh,*verts,1 if i in (2,3,4,10,11) else 0)
            center=(cx+.014,cy-.015,side*.132)
            tri(mesh,center,d if side>0 else c,c if side>0 else d,0)
    for i in range(n):quad(mesh,p(i,1,-.205),p(i+1,1,-.205),p(i+1,1,.205),p(i,1,.205),0)
    closed('ConcaveEventHorizon',mesh)
void_well()
# Tapered irregular purple lens fragments, tucked around the black silhouette.
for side in (-1,1):
    armor('AccretionSliver_A_'+str(side),[(-.143,1.043),(-.095,1.108),(-.022,1.123),(-.081,1.090)],.006,13,14,side*.211,.12)
    armor('AccretionSliver_B_'+str(side),[(.103,.990),(.103,.940),(.062,.885),(.090,.949)],.005,12,13,side*.213,.10)
    armor('AccretionSliver_C_'+str(side),[(-.12,.870),(-.063,.835),(.006,.841),(-.062,.850)],.004,13,14,side*.201,.10)

# Compound shaft profile: robust core, two grip zones, angular armored sleeves.
OCT=[(-.55,-1),(.55,-1),(1,-.5),(1,.5),(.55,1),(-.55,1),(-1,.5),(-1,-.5)]
def shaft(name,stations,cols):
    mesh=[];loops=[]
    for y,r,rz,cx in stations:loops.append([(cx+x*r,y,z*rz) for x,z in OCT])
    for i in range(len(loops)-1):
        for j in range(8):quad(mesh,loops[i][j],loops[i+1][j],loops[i+1][(j+1)%8],loops[i][(j+1)%8],cols[j%len(cols)])
    for i in (0,len(loops)-1):
        y,r,rz,cx=stations[i]
        for j in range(8):tri(mesh,(cx,y,0),loops[i][(j+1)%8] if i else loops[i][j],loops[i][j] if i else loops[i][(j+1)%8],2)
    closed(name,mesh)

shaft('DarkstarSpine',[(-1.30,.049,.046,.025),(-1.0,.058,.051,.015),(-.48,.053,.053,.005),(.15,.058,.052,0),(.67,.066,.057,0),(.91,.060,.065,0)],[3,2,4,2,1,3,2,4])
for name,lo,hi in [('Lower',-1.05,-.59),('Upper',-.12,.31)]:
    shaft('Grip_'+name,[(lo,.063,.058,0),(lo+.025,.068,.061,0),(hi-.025,.068,.061,0),(hi,.063,.058,0)],[10,11,10,10,10,11,10,10])
    # Continuous diagonal leather binding, actual raised strap with square section.
    mesh=[];n=112;turns=7
    for i in range(n):
        def p(t,j):
            ang=2*math.pi*turns*t;frac=(ang/(2*math.pi)*8)%8;k=int(frac);u=frac-k
            x,z=mix(OCT[k],OCT[(k+1)%8],u)
            y=lo+.035+(hi-lo-.07)*t+[-.007,.007,.007,-.007][j]
            r=.069+([0,0,.004,.004][j]);return (x*r,y,z*(r-.006))
        for j in range(4):quad(mesh,p(i/n,j),p((i+1)/n,j),p((i+1)/n,(j+1)%4),p(i/n,(j+1)%4),11 if j==2 else 10)
    quad(mesh,*[p(0,j) for j in range(4)],10);quad(mesh,*reversed([p(1,j) for j in range(4)]),10)
    closed('HelicalBinding_'+name,mesh)
    for k,y in enumerate([lo-.015,hi]):shaft(f'GripFerrule_{name}_{k}',[(y,.064,.061,0),(y+.009,.078,.070,0),(y+.025,.078,.070,0),(y+.034,.064,.061,0)],[4,3,6,3,4,3,6,3])

shaft('WaistArmature',[(-.57,.065,.059,0),(-.51,.085,.074,0),(-.32,.073,.067,0),(-.17,.064,.058,0)],[3,2,5,2,3,2,5,2])
for side in (-1,1):
    armor('WaistFace_'+str(side),[(-.045,-.17),(.025,-.22),(.090,-.36),(.118,-.50),
          (.061,-.43),(.025,-.59),(-.023,-.52),(-.073,-.34)],.012,2,5,side*.071)
    armor('UpperArmoredFace_'+str(side),[(-.065,.68),(-.054,.45),(-.013,.32),(.029,.40),
          (.063,.57),(.127,.65),(.076,.625),(.040,.73)],.012,2,5,side*.066)
    # Flowing split-crescent ferrule instead of identical mechanical sleeves.
    armor('SweptHaftGuard_'+str(side),[(.043,.72),(.147,.65),(.205,.53),(.174,.36),
          (.128,.47),(.126,.55),(.073,.59)],.016,3,6,side*.078)
    armor('HaftGuardInner_'+str(side),[(-.075,.71),(-.135,.61),(-.120,.49),(-.045,.33),
          (-.066,.51),(-.087,.59),(-.051,.65)],.013,2,4,side*.072)
    # Runes are inset-looking dark groove beds plus narrower violet segments.
    for k,y in enumerate([.43,.54,-.32,-.43]):
        x=0;z=side*(.086 if y<0 else .081)
        poly=[(-.021,y+.032),(.007,y+.010),(-.006,y-.006),(.014,y-.030),(.025,y-.026),(.006,y-.003),(.021,y+.012),(-.013,y+.040)]
        armor('RuneBed_'+str(side)+'_'+str(k),poly,.002,0,1,z,.02)
        small=[(x*.54,y+(yy-y)*.77) for x,yy in poly]
        armor('RuneInlay_'+str(side)+'_'+str(k),small,.0012,13 if k%2 else 12,12,z+side*.0025,.01)

shaft('ButtArmor',[(-1.40,.049,.049,.025),(-1.25,.089,.072,.021),(-1.15,.074,.063,.019),(-1.09,.055,.051,.017)],[3,2,6,2,3,2,5,2])
# A sculpted four-ridge spear tip with an offset apex and stepped shoulders.
armor('TerminalStarFang',[(-.064,-1.27),(.024,-1.35),(.096,-1.28),(.063,-1.43),(.008,-1.61),(-.034,-1.43)],.055,2,6,.0,.09)

# Broader crescent's root plate / dorsal crest follow the new blade transform.
for k,(name,mesh) in enumerate(PARTS):
    if name.startswith(('RootScale','RootCrest')):
        transformed=[]
        for a,b,c,_,col in mesh:tri(transformed,enlarged(a),enlarged(b),enlarged(c),col)
        PARTS[k]=[name,transformed]

# Merge repeated related details, retaining three independent primary blade meshes.
groups={}
for name,mesh in PARTS:
    if name.startswith(('VoidSeam','EdgeSpark','Fracture')): key='InterruptedVoidInlays'
    elif name.startswith('ArmorStratum'): key='RaisedBladeArmor'
    elif name.startswith('ShatteredOrbit'): key='AsymmetricOrbitFragments'
    elif name.startswith('AccretionSliver'): key='AccretionSlivers'
    elif name.startswith('Rune'):key='ShaftRunes'
    elif name.startswith('GripFerrule'):key='GripFerrules'
    else:key=name
    groups.setdefault(key,[]).extend(mesh)
PARTS=list(groups.items())

def export_dae():
    ns='http://www.collada.org/2005/11/COLLADASchema';ET.register_namespace('',ns)
    def el(p,t,a=None,txt=None):
        e=ET.SubElement(p,'{'+ns+'}'+t,a or {});e.text=txt;return e
    doc=ET.Element('{'+ns+'}COLLADA',{'version':'1.4.1'})
    asset=el(doc,'asset');el(asset,'unit',{'name':'meter','meter':'1'});el(asset,'up_axis',txt='Y_UP')
    lib=el(doc,'library_geometries')
    for name,mesh in PARTS:
        m=el(el(lib,'geometry',{'id':name,'name':name}),'mesh');pos=[];norm=[];uv=[]
        for a,b,c,n,col in mesh:
            for p in (a,b,c):pos.extend(p);norm.extend(n);uv.extend([(col+.5)/16,.5])
        for suf,data,stride,fields in [('pos',pos,3,'XYZ'),('norm',norm,3,'XYZ'),('uv',uv,2,'ST')]:
            sid=name+'-'+suf;s=el(m,'source',{'id':sid})
            el(s,'float_array',{'id':sid+'-array','count':str(len(data))},' '.join(format(x,'.8g') for x in data))
            ac=el(el(s,'technique_common'),'accessor',{'source':'#'+sid+'-array','count':str(len(data)//stride),'stride':str(stride)})
            for f in fields:el(ac,'param',{'name':f,'type':'float'})
        el(el(m,'vertices',{'id':name+'-vertices'}),'input',{'semantic':'POSITION','source':'#'+name+'-pos'})
        t=el(m,'triangles',{'count':str(len(mesh))})
        for sem,suf,i in [('VERTEX','vertices',0),('NORMAL','norm',1),('TEXCOORD','uv',2)]:
            attrs={'semantic':sem,'source':'#'+name+'-'+suf,'offset':str(i)}
            if sem=='TEXCOORD':attrs['set']='0'
            el(t,'input',attrs)
        el(t,'p',txt=' '.join(str(i) for i in range(len(mesh)*3) for _ in range(3)))
    node=el(el(el(doc,'library_visual_scenes'),'visual_scene',{'id':'Scene'}),'node',{'id':'AbyssEclipseCandidate','name':'AbyssEclipseCandidate','type':'NODE'})
    el(node,'matrix',{'sid':'transform'},'1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1')
    for name,_ in PARTS:el(node,'instance_geometry',{'url':'#'+name})
    el(el(doc,'scene'),'instance_visual_scene',{'url':'#Scene'})
    ET.indent(doc);ET.ElementTree(doc).write(OUT/'AbyssEclipseCandidate.dae',encoding='utf-8',xml_declaration=True)

export_dae()
def chunk(t,b):return struct.pack('>I',len(b))+t+b+struct.pack('>I',zlib.crc32(t+b)&0xffffffff)
row=b''.join(bytes((*PALETTE[x//16],255)) for x in range(256))
(OUT/'AbyssEclipseCandidate.png').write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',256,16,8,6,0,0,0))+chunk(b'IDAT',zlib.compress((b'\0'+row)*16,9))+chunk(b'IEND',b''))
(OUT/'geometry.json').write_text(json.dumps(PARTS,separators=(',',':')),encoding='utf8')
(OUT/'palette.json').write_text(json.dumps(PALETTE),encoding='utf8')
vertices=[p for _,mesh in PARTS for a,b,c,_,_ in mesh for p in (a,b,c)]
area=[0.0]*16
for _,mesh in PARTS:
    for a,b,c,n,col in mesh:area[col]+=length(cross(sub(b,a),sub(c,a)))/2
manifest={'design':'Eclipse / fractured god-scythe / violet great-scythe candidate A3','runtimeIntegrated':False,
    'bladeRevision':{'spanMultiplier':1.38,'verticalMultiplier':1.13,'bellyWidthMultiplier':1.18,'thicknessMultiplier':1.10},
    'axis':'Y_UP','root':'identity','bonesExpected':1,'meshes':len(PARTS),
    'triangles':sum(len(m) for _,m in PARTS),'bounds':[[min(p[j] for p in vertices) for j in range(3)],[max(p[j] for p in vertices) for j in range(3)]],
    'surfaceAreaFractions':{'violetMetalAndGrip':sum(area[:6]+area[10:12])/sum(area),'coldLavenderBevels':sum(area[6:10])/sum(area),'brightVoidInlays':sum(area[12:])/sum(area)},
    'sha256':{p.name:hashlib.sha256(p.read_bytes()).hexdigest().upper() for p in OUT.glob('*') if p.suffix in ('.dae','.png')},
    'source':'Original parameterized geometry authored for this project; no third-party model or game content.'}
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
print(json.dumps(manifest,indent=2))
