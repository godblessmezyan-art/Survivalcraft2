"""Original mobile wildlife. Execute build/export/render in the official Blender MCP.

Game Y-up -> Blender Z-up: (x,-z,y), a rotation with determinant +1.
Rigid per-bone meshes use native procedural animation, no skinning addon.
"""
from pathlib import Path
import math, json, hashlib, xml.etree.ElementTree as ET
import bpy, bmesh
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'artifacts/wildlife-blender'
ASSETS = ROOT / 'src/SCIEPort/Assets'
SCENE = 'Wildlife-0.91'
PALETTE = [(196,83,32),(243,220,184),(32,28,28),(83,48,32),
           (166,145,119),(222,201,171),(228,150,150),(252,242,221),
           (194,47,38),(233,166,48),(109,126,65),(210,143,66),
           (144,91,45),(79,126,100),(255,255,255),(94,83,73)]
BONES = {
 'Fox': {'Body':(None,(0,0,0)),'Head':('Body',(0,.62,-.52)),
         'Leg1':('Body',(-.21,.43,-.34)),'Leg2':('Body',(.21,.43,-.34)),
         'Leg3':('Body',(-.21,.43,.34)),'Leg4':('Body',(.21,.43,.34))},
 'Rabbit': {'Body':(None,(0,0,0)),'Head':('Body',(0,.54,-.4)),
            'Leg1':('Body',(-.16,.34,-.3)),'Leg2':('Body',(.16,.34,-.3)),
            'Leg3':('Body',(-.16,.34,.28)),'Leg4':('Body',(.16,.34,.28))},
 'Chicken': {'Body':(None,(0,0,0)),'Neck':('Body',(0,.43,-.16)),
             'Head':('Neck',(0,.18,-.08)), 'Leg1':('Body',(-.09,.23,0)),
             'Leg2':('Body',(.09,.23,0)), 'Wing1':('Body',(-.17,.43,.02)),
             'Wing2':('Body',(.17,.43,.02))},
 'Cat': {'Body':(None,(0,0,0)),'Head':('Body',(0,.45,-.33)),
         'Leg1':('Body',(-.12,.30,-.22)),'Leg2':('Body',(.12,.30,-.22)),
         'Leg3':('Body',(-.12,.30,.22)),'Leg4':('Body',(.12,.30,.22))}}

def coord(p): return (p[0],-p[2],p[1])
def game(p): return (p[0],p[2],-p[1])
def pivot(species,bone):
    parent,p = BONES[species][bone]
    return Vector(p)+(pivot(species,parent) if parent else Vector((0,0,0)))

def shape(species,bone,name,verts,faces,color):
    scene=bpy.data.scenes[SCENE]
    p=pivot(species,bone)
    mesh=bpy.data.meshes.new('MESH-'+species+'-'+name)
    mesh.from_pydata([coord(Vector(v)-p) for v in verts],[],faces)
    bm=bmesh.new(); bm.from_mesh(mesh); bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(mesh); bm.free()
    mesh.materials.append(bpy.data.materials['MAT-wildlife'])
    uv=mesh.uv_layers.new(name='Atlas')
    for poly in mesh.polygons:
        # Every closed surface gets an explicit UV island inside its padded swatch.
        for j,li in enumerate(poly.loop_indices):
            uv.data[li].uv=((color+(0.30 if j%2 else .70))/16, .30 if j<2 else .70)
    obj=bpy.data.objects.new('GEO-'+species+'-'+bone+'-'+name,mesh)
    scene.collection.objects.link(obj)
    obj.parent=bpy.data.objects['ARM-'+species+'-'+bone]
    return obj

def ell(s,b,n,c,r,m,segments=8,rings=4):
    vs=[(c[0],c[1]-r[1],c[2])]
    for j in range(1,rings):
        a=-math.pi/2+math.pi*j/rings
        for k in range(segments):
            t=math.tau*k/segments
            vs.append((c[0]+r[0]*math.cos(a)*math.cos(t),c[1]+r[1]*math.sin(a),c[2]+r[2]*math.cos(a)*math.sin(t)))
    vs.append((c[0],c[1]+r[1],c[2])); top=len(vs)-1
    fs=[]
    for k in range(segments):
        fs.append((0,1+k,1+(k+1)%segments))
        fs.append((top,1+(rings-2)*segments+(k+1)%segments,1+(rings-2)*segments+k))
    for j in range(rings-2):
        for k in range(segments):
            a=1+j*segments+k; d=1+j*segments+(k+1)%segments
            fs.append((a,d,d+segments,a+segments))
    return shape(s,b,n,vs,fs,m)

def sweep(s,b,n,points,radii,m,sides=6):
    vs=[]
    for i,p in enumerate(points):
        tangent=(Vector(points[min(i+1,len(points)-1)])-Vector(points[max(0,i-1)])).normalized()
        u=tangent.cross(Vector((0,1,0)) if abs(tangent.y)<.9 else Vector((1,0,0))).normalized(); v=tangent.cross(u)
        for k in range(sides):
            t=math.tau*k/sides; vs.append(Vector(p)+radii[i]*(u*math.cos(t)+v*math.sin(t)))
    fs=[tuple(range(sides-1,-1,-1)),tuple(range(len(vs)-sides,len(vs)))]
    for i in range(len(points)-1):
        for k in range(sides):
            a=i*sides+k; d=i*sides+(k+1)%sides; fs.append((a,d,d+sides,a+sides))
    return shape(s,b,n,vs,fs,m)

def ear(s,n,x,y,z,w,h,m):
    shape(s,'Head',n,[(x-w,y,z-.045),(x+w,y,z-.045),(x,y+h,z+.01),
                     (x-w,y,z+.06),(x+w,y,z+.06),(x,y+h,z+.06)],
          [(0,1,2),(5,4,3),(0,3,4,1),(1,4,5,2),(2,5,3,0)],m)
    shape(s,'Head',n+'Inner',[(x-w*.55,y+.035,z-.047),(x+w*.55,y+.035,z-.047),
                           (x,y+h*.77,z-.005)],[(0,1,2)],6)

def eyes(s,y,z,width,size):
    for side in [-1,1]:
        ell(s,'Head','Eye'+str(side),(side*width,y,z),(size,size,size*.65),2,8,3)
        ell(s,'Head','EyeLight'+str(side),(side*width-size*.2,y+size*.3,z-size*.55),(size*.28,)*3,14,4,3)

def fox():
    s='Fox'
    ell(s,'Body','Torso',(0,.52,.03),(.26,.25,.57),0)
    ell(s,'Body','Chest',(0,.57,-.31),(.24,.27,.25),1)
    sweep(s,'Body','Brush',[(0,.53,.43),(0,.50,.74),(0,.36,1.03),(0,.27,1.28)],[.13,.21,.18,.075],0,8)
    sweep(s,'Body','WhiteTailTip',[(0,.29,1.20),(0,.26,1.40),(0,.29,1.49)],[.12,.085,.012],1,8)
    ell(s,'Head','Skull',(0,.73,-.59),(.215,.195,.24),0)
    sweep(s,'Head','TaperedMuzzle',[(0,.67,-.66),(0,.64,-.85),(0,.65,-1.01)],[.16,.11,.043],1)
    ell(s,'Head','Nose',(0,.65,-1.015),(.055,.043,.035),2)
    eyes(s,.77,-.77,.16,.031)
    for side in [-1,1]:
        ear(s,'Ear'+str(side),side*.14,.84,-.56,.10,.25,3)
        ell(s,'Head','Cheek'+str(side),(side*.16,.66,-.65),(.105,.08,.16),1,6,3)
    for i in range(1,5):
        p=pivot(s,'Leg'+str(i)); x,y,z=p
        ell(s,'Leg'+str(i),'Upper', (x,.32,z),(.075,.20,.10),0,6,3)
        sweep(s,'Leg'+str(i),'Stocking',[(x,.23,z),(x,.07,z-.025)],[.056,.04],3)
        ell(s,'Leg'+str(i),'Paw',(x,.055,z-.055),(.068,.05,.11),2,6,3)

def rabbit():
    s='Rabbit'
    ell(s,'Body','Rump',(0,.36,.17),(.235,.28,.33),4)
    ell(s,'Body','Chest',(0,.38,-.18),(.18,.23,.27),5)
    ell(s,'Body','CottonTail',(0,.42,.48),(.115,.12,.105),7,8,3)
    ell(s,'Head','Skull',(0,.57,-.39),(.18,.18,.205),5)
    ell(s,'Head','Muzzle',(0,.49,-.54),(.115,.085,.12),7,6,3)
    ell(s,'Head','Nose',(0,.51,-.65),(.027,.023,.02),6,4,3)
    eyes(s,.61,-.52,.15,.035)
    for side in [-1,1]:
        ell(s,'Head','LongEar'+str(side),(side*.095,.89,-.36),(.065,.32,.055),4,6,4)
        ell(s,'Head','PinkEar'+str(side),(side*.095,.89,-.402),(.037,.25,.015),6,6,3)
    for i in range(1,5):
        b='Leg'+str(i); x,y,z=pivot(s,b)
        ell(s,b,'Haunch' if i>2 else 'Foreleg',(x,.22,z),(.115 if i>2 else .045,.18,.15 if i>2 else .055),4,6,3)
        ell(s,b,'LongFoot',(x,.045,z-.09),(.09 if i>2 else .05,.045,.20 if i>2 else .09),5,6,3)

def chicken():
    s='Chicken'
    ell(s,'Body','PearBody',(0,.38,.04),(.215,.25,.29),7)
    ell(s,'Neck','RaisedNeck',(0,.55,-.18),(.105,.18,.105),7)
    ell(s,'Head','Head',(0,.69,-.26),(.115,.125,.12),7)
    sweep(s,'Head','Beak',[(0,.665,-.34),(0,.65,-.46)],[.064,.005],9,4)
    eyes(s,.71,-.325,.098,.023)
    for i in range(3): ell(s,'Head','Comb'+str(i),(0,.80,-.32+i*.065),(.028,.065,.045),8,6,3)
    ell(s,'Head','Wattle',(0,.58,-.32),(.037,.062,.025),8,6,3)
    for side in [-1,1]:
        b='Wing1' if side<0 else 'Wing2'
        ell(s,b,'FoldedWing',(side*.185,.40,.055),(.055,.14,.205),5,6,4)
        for k in range(3): sweep(s,'Body','TailFeather'+str(side)+str(k),[(side*.035,.47,.23),(side*(.055+k*.02),.65+k*.025,.45)],[.05,.025],3,4)
    for i in [1,2]:
        x,y,z=pivot(s,'Leg'+str(i))
        sweep(s,'Leg'+str(i),'Shank',[(x,.24,.005),(x,.055,-.015)],[.025,.018],9)
        for d in [-1,0,1]: sweep(s,'Leg'+str(i),'Toe'+str(d),[(x,.035,-.005),(x+d*.045,.025,-.13)],[.014,.006],9,4)

def cat():
    s='Cat'
    ell(s,'Body','SlenderTorso',(0,.34,.015),(.16,.17,.37),11)
    ell(s,'Body','Bib',(0,.33,-.24),(.14,.16,.13),7)
    sweep(s,'Body','UprightTail',[(0,.36,.32),(.03,.43,.53),(.09,.66,.64),(.12,.85,.62),(.12,.94,.53)],[.046,.045,.04,.035,.025],11)
    for j in range(3): ell(s,'Body','TailBand'+str(j),(.09+j*.014,.65+j*.09,.64),(.042,.035,.042),12,6,3)
    ell(s,'Head','Skull',(0,.51,-.35),(.16,.14,.145),11)
    for side in [-1,1]:
        ear(s,'PointedEar'+str(side),side*.10,.59,-.33,.065,.17,11)
        ell(s,'Head','Muzzle'+str(side),(side*.045,.46,-.47),(.06,.045,.05),7,6,3)
        ell(s,'Head','GreenEye'+str(side),(side*.107,.535,-.46),(.034,.033,.016),13,8,3)
        ell(s,'Head','Pupil'+str(side),(side*.107,.535,-.476),(.009,.027,.005),2,6,3)
        for j in range(2): sweep(s,'Head','Whisker'+str(side)+str(j),[(side*.065,.46-j*.015,-.49),(side*.21,.47-j*.028,-.50)],[.003,.001],7,3)
    ell(s,'Head','PinkNose',(0,.48,-.508),(.025,.018,.015),6,4,3)
    for j in range(3): ell(s,'Head','ForeheadStripe'+str(j),((j-1)*.045,.615,-.405),(.012,.026,.027),12,4,3)
    for i in range(1,5):
        b='Leg'+str(i); x,y,z=pivot(s,b)
        sweep(s,b,'Leg',[(x,.30,z),(x,.075,z)],[.045,.028],11)
        ell(s,b,'WhitePaw',(x,.045,z-.035),(.05,.043,.075),7,6,3)

def build():
    if bpy.data.scenes.get(SCENE): raise RuntimeError('Wildlife scene already exists; inspect before rebuilding')
    OUT.mkdir(parents=True,exist_ok=True)
    scene=bpy.data.scenes.new(SCENE); bpy.context.window.scene=scene
    scene.world=bpy.data.worlds.new('WORLD-wildlife'); scene.world.use_nodes=True
    scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.16,.18,.21,1)
    scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.65
    image=bpy.data.images.new('WildlifePalette',width=128,height=8)
    pixels=[]
    for y in range(8):
        for x in range(128):
            rgb=PALETTE[x//8]; noise=((x*17+y*31)%7-3)*.004
            pixels.extend([max(0,min(1,c/255+noise)) for c in rgb]+[1])
    image.pixels=pixels; image.filepath_raw=str(ASSETS/'Textures/WildlifePalette.png'); image.file_format='PNG'; image.save()
    mat=bpy.data.materials.new('MAT-wildlife'); mat.use_nodes=True
    tex=mat.node_tree.nodes.new('ShaderNodeTexImage'); tex.image=image; tex.interpolation='Closest'
    bsdf=mat.node_tree.nodes.get('Principled BSDF'); bsdf.inputs['Roughness'].default_value=.87
    mat.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
    for index,(s,bones) in enumerate(BONES.items()):
        for b,(parent,p) in bones.items():
            obj=bpy.data.objects.new('ARM-'+s+'-'+b,None); scene.collection.objects.link(obj)
            if parent: obj.parent=bpy.data.objects['ARM-'+s+'-'+parent]; obj.location=coord(p)
            else: obj.location=(index*1.75,0,0)
    fox(); rabbit(); chicken(); cat()
    for s in BONES:
        # Export one native mesh per bone, including all disconnected decorative shells.
        for b in BONES[s]:
            objs=[o for o in scene.objects if o.type=='MESH' and o.parent==bpy.data.objects['ARM-'+s+'-'+b]]
            bpy.ops.object.select_all(action='DESELECT')
            for o in objs: o.select_set(True)
            bpy.context.view_layer.objects.active=objs[0]; bpy.ops.object.join()
            objs[0].name='GEO-'+s+'-'+b
    scene.render.engine='CYCLES'; scene.cycles.samples=24; scene.cycles.use_denoising=True
    scene.render.resolution_x=1600; scene.render.resolution_y=800; scene.render.resolution_percentage=100
    scene.view_settings.view_transform='Standard'
    cam=bpy.data.objects.new('CAM-wildlife',bpy.data.cameras.new('CAM-wildlife')); scene.collection.objects.link(cam)
    cam.data.type='ORTHO'; cam.data.ortho_scale=7.6; cam.location=(4.6,8,4)
    cam.rotation_euler=(Vector((2.6,0,.5))-cam.location).to_track_quat('-Z','Y').to_euler(); scene.camera=cam
    for name,pos,power,size in [('Key',(1,4,6),500,5),('Fill',(6,-2,4),350,4),('Rim',(-2,-3,4),400,3)]:
        light=bpy.data.lights.new('LGT-'+name,'AREA'); light.energy=power; light.shape='DISK'; light.size=size
        obj=bpy.data.objects.new('LGT-'+name,light); scene.collection.objects.link(obj); obj.location=pos
        obj.rotation_euler=(Vector((2.5,0,.3))-obj.location).to_track_quat('-Z','Y').to_euler()
    scene.render.image_settings.file_format='PNG'; scene.render.film_transparent=True
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'wildlife.blend'))
    return {'scene':SCENE,'version':bpy.app.version_string,'species':list(BONES),'objects':len(scene.objects)}

def export():
    ns='http://www.collada.org/2005/11/COLLADASchema'; ET.register_namespace('',ns)
    reports={}
    for s,bones in BONES.items():
        def e(p,t,a=None,text=None):
            child=ET.SubElement(p,'{'+ns+'}'+t,a or {}); child.text=text; return child
        root=ET.Element('{'+ns+'}COLLADA',version='1.4.1')
        asset=e(root,'asset'); e(asset,'unit',{'name':'meter','meter':'1'}); e(asset,'up_axis',text='Y_UP')
        lib=e(root,'library_geometries'); count=0
        for b in bones:
            mesh=bpy.data.objects['GEO-'+s+'-'+b].data; mesh.calc_loop_triangles()
            pos=[]; normals=[]; uv=[]
            for tri in mesh.loop_triangles:
                ps=[Vector(game(mesh.vertices[i].co)) for i in tri.vertices]
                n=(ps[1]-ps[0]).cross(ps[2]-ps[0])
                if n.length<1e-9: raise RuntimeError('Degenerate triangle '+s+b)
                n.normalize()
                for p,li in zip(ps,tri.loops): pos.extend(p); normals.extend(n); uv.extend((mesh.uv_layers.active.data[li].uv.x,1-mesh.uv_layers.active.data[li].uv.y))
            mid=s+'_'+b; gm=e(e(lib,'geometry',{'id':mid,'name':b}),'mesh')
            for suffix,values,stride,fields in [('pos',pos,3,'XYZ'),('normal',normals,3,'XYZ'),('uv',uv,2,'ST')]:
                sid=mid+'_'+suffix; src=e(gm,'source',{'id':sid})
                e(src,'float_array',{'id':sid+'_array','count':str(len(values))},' '.join(f'{v:.7g}' for v in values))
                acc=e(e(src,'technique_common'),'accessor',{'source':'#'+sid+'_array','count':str(len(values)//stride),'stride':str(stride)})
                for f in fields: e(acc,'param',{'name':f,'type':'float'})
            e(e(gm,'vertices',{'id':mid+'_vertices'}),'input',{'semantic':'POSITION','source':'#'+mid+'_pos'})
            tri=e(gm,'triangles',{'count':str(len(pos)//9)})
            for i,(semantic,suffix) in enumerate([('VERTEX','vertices'),('NORMAL','normal'),('TEXCOORD','uv')]):
                a={'semantic':semantic,'source':'#'+mid+'_'+suffix,'offset':str(i)}
                if i==2:a['set']='0'
                e(tri,'input',a)
            e(tri,'p',text=' '.join(str(i) for i in range(len(pos)//3) for j in range(3))); count+=len(pos)//9
        visual=e(e(root,'library_visual_scenes'),'visual_scene',{'id':'Scene'}); nodes={}
        for b,(parent,p) in bones.items():
            node=e(nodes[parent] if parent else visual,'node',{'id':b,'name':b,'type':'NODE'}); nodes[b]=node
            e(node,'matrix',{'sid':'transform'},f'1 0 0 {p[0]} 0 1 0 {p[1]} 0 0 1 {p[2]} 0 0 0 1')
            e(node,'instance_geometry',{'url':'#'+s+'_'+b})
        e(e(root,'scene'),'instance_visual_scene',{'url':'#Scene'})
        path=ASSETS/'Models'/f'{s}.dae'; ET.indent(root); ET.ElementTree(root).write(path,encoding='utf-8',xml_declaration=True)
        reports[s]={'triangles':count,'bones':len(bones),'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
    (OUT/'manifest.json').write_text(json.dumps(reports,indent=2)+'\n')
    return reports

def render(view='overview'):
    scene=bpy.data.scenes[SCENE]; bpy.context.window.scene=scene
    cam=scene.camera
    for i,s in enumerate(BONES): bpy.data.objects['ARM-'+s+'-Body'].location=(0,i*1.75,0) if view=='side' else (i*1.75,0,0)
    pos={'overview':(4.6,8,4),'side':(9,2.6,2.6),'back':(2.6,-9,2.4)}[view]
    target=(0,2.15,.5) if view=='side' else (2.6,0,.5)
    cam.data.ortho_scale=8.6 if view=='side' else 7.6
    cam.location=pos; cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(OUT/(view+'.png')); bpy.ops.render.render(write_still=True)
    return {'path':scene.render.filepath}
