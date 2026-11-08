"""Original modern-home additions. Run prepare/build/render/save via official Blender MCP.

Reuse the current V2 atlas and low-polygon DAE exporter; do not reset the existing
scene, rewrite the older models, or overwrite their editable .blend source.
"""
from pathlib import Path
import importlib.util, json, hashlib, math
import bpy
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[1]
OUTPUT=ROOT/'artifacts/home-extension'
RECORDS=ROOT/'docs/assets/home-extension'
spec=importlib.util.spec_from_file_location('home_v2_authoring',str(ROOT/'scripts/New-HomeFurnitureAssets.py'))
base=importlib.util.module_from_spec(spec); spec.loader.exec_module(base); base.OUTPUT=OUTPUT
NAMES=['HomeFireplace','HomeFireplaceOn','HomeDiningSet','HomeDoubleBed','HomeNightstand','HomeWallArt0','HomeWallArt1','HomeWallArt2']

def prepare():
    OUTPUT.mkdir(parents=True,exist_ok=True); RECORDS.mkdir(parents=True,exist_ok=True)
    image=bpy.data.images.get('HomeFurniture')
    if not image:
        image=bpy.data.images.load(str(base.ASSETS/'Textures/HomeFurniture.png')); image.name='HomeFurniture'
    image.colorspace_settings.name='sRGB'
    for role,_,kind in base.PALETTE:
        name='MAT-home-'+role
        if bpy.data.materials.get(name): continue
        material=bpy.data.materials.new(name); material.use_nodes=True; nodes=material.node_tree.nodes
        shader=nodes.get('Principled BSDF'); shader.inputs['Metallic'].default_value=0; shader.inputs['Roughness'].default_value=.78
        tex=nodes.new('ShaderNodeTexImage'); tex.image=image; tex.interpolation='Closest'
        material.node_tree.links.new(tex.outputs['Color'],shader.inputs['Base Color'])
    return {'atlas':'HomeFurniture','modelsToAuthor':NAMES,'existingObjectsRetained':True}

def fireplace(c,on):
    base.box('fireplace-shadow-plinth',(0,-.25,.035),(.84,.40,.07),'Base',c,.010)
    base.box('fireplace-rear',(0,-.45,.48),(.94,.06,.90),'Graphite',c,.012)
    for x in [-.42,.42]: base.box('fireplace-glacier-side',(x,-.25,.48),(.10,.46,.90),'Glacier',c,.012)
    base.box('fireplace-mantel',(0,-.25,.90),(.94,.46,.12),'Stone',c,.014)
    base.box('fireplace-hearth',(0,-.25,.14),(.94,.46,.22),'Glacier',c,.014)
    base.box('firebox-dark-liner',(0,-.408,.50),(.75,.025,.59),'Panel',c,.004)
    base.box('firebox-lower-tray',(0,-.24,.27),(.76,.32,.035),'Graphite',c,.004)
    for x in [-.378,.378]: base.box('firebox-silver-side',(x,-.013,.51),(.012,.014,.56),'Titanium',c,.002)
    for z in [.23,.79]: base.box('firebox-silver-lip',(0,-.013,z),(.76,.014,.012),'Titanium',c,.002)
    for i in range(7):
        x=-.27+i*.09
        base.cylinder('ceramic-fuel-element',(x,-.30,.29),(x,-.16,.29),.024,'Graphite',c,8)
    if on:
        base.box('linear-ice-ember',(0,-.17,.291),(.64,.025,.018),'Ice',c,.003)
        for i,h in enumerate([.13,.21,.16,.27,.18,.22,.12]):
            x=-.27+i*.09; y=-.225+(i%2)*.024
            outline=[(-.035,0),(.034,0),(.044,.19),(.029,.43),(.008,.66),(.016,1),(-.010,.86),(-.026,.63),(-.013,.43),(-.039,.22)]
            verts=[(x+dx,y,.30+dz*h) for dx,dz in outline]
            base.surface('curved-ice-flame',verts,[tuple(range(len(verts)))],'Ice',c,.008)
            core=[(x+dx*.45,y+.012,.30+dz*h*.56) for dx,dz in outline]
            base.surface('glacier-flame-core',core,[tuple(range(len(core)))],'Glacier',c,.003)
    base.box('fireplace-control',(0.29,-.012,.12),(.095,.012,.026),'Graphite',c,.004)
    base.box('fireplace-status',(0.312,-.003,.12),(.018,.004,.006),'Ice' if on else 'Titanium',c,.001)

def dining(c):
    base.box('dining-stone-top',(.5,-.5,.75),(.90,1.32,.06),'Stone',c,.018)
    base.box('dining-shadow-frame',(.5,-.5,.705),(.83,1.24,.035),'Graphite',c,.008)
    for x in [.15,.85]:
        for y in [.05,-1.05]: base.cylinder('dining-titanium-leg',(x,y,.015),(x,y,.72),.025,'Titanium',c,10)
    for side in [-1,1]:
        x=.5+side*.71
        base.box('chair-shadow-base',(x,-.5,.365),(.48,.50,.035),'Graphite',c,.008)
        base.cushion('chair-seat',(x,-.5,.42),(.50,.52,.075),'MistCloth',c)
        base.box('chair-upholstered-back',(.5+side*.93,-.5,.715),(.07,.52,.48),'MistCloth',c,.024)
        base.box('chair-back-silver-spine',(.5+side*.965,-.5,.67),(.010,.025,.40),'Titanium',c,.002)
        for lx in [x-.18,x+.18]:
            for ly in [-.31,-.69]: base.cylinder('chair-slim-leg',(lx,ly,.012),(lx,ly,.38),.018,'Titanium',c,8)
        px=.5+side*.245
        base.box('woven-placemat',(px,-.5,.784),(.32,.43,.006),'SpaceBlue',c,.001)
        base.cylinder('porcelain-plate',(px,-.5,.788),(px,-.5,.801),.115,'Porcelain',c,20)
        base.cylinder('plate-well',(px,-.5,.802),(px,-.5,.804),.080,'Glacier',c,20)
        base.box('titanium-cutlery',(px,-.685,.795),(.015,.16,.008),'Titanium',c,.002)
        base.cylinder('dining-cup',(px,-.5+side*.34,.783),(px,-.5+side*.34,.865),.035,'Porcelain',c,12)
        base.cylinder('cup-dark-interior',(px,-.5+side*.34,.866),(px,-.5+side*.34,.868),.027,'Graphite',c,12)

def bed(c):
    base.box('bed-inset-floating-plinth',(.5,-.5,.12),(1.64,1.64,.16),'Base',c,.020)
    base.box('bed-graphite-frame',(.5,-.5,.23),(1.88,1.88,.18),'Graphite',c,.024)
    base.box('bed-silver-under-edge',(.5,-.5,.325),(1.86,1.86,.012),'Titanium',c,.003)
    for x in [.045,.955]:
        base.cushion('split-mattress',(x,-.5,.46),(.90,1.78,.24),'MistCloth',c)
        base.cushion('bed-pillow',(x,-1.08,.645),(.70,.38,.13),'MistCloth',c)
    base.box('upholstered-headboard',(.5,-1.40,.58),(1.92,.12,.80),'Graphite',c,.022)
    for i in range(3): base.box('headboard-soft-panel',(-.12+i*.62,-1.328,.69),(.60,.025,.48),'MistCloth',c,.009)
    base.box('headboard-silver-reveal',(.5,-1.31,.43),(1.82,.008,.010),'Titanium',c,.002)
    base.box('duvet-layer',(.5,-.23,.595),(1.82,1.22,.032),'MistCloth',c,.010)
    base.box('space-blue-bed-runner',(.5,.20,.619),(1.84,.34,.022),'SpaceBlue',c,.006)
    for x in [-.40,1.40]: base.box('runner-tail',(x,.20,.49),(.025,.34,.24),'SpaceBlue',c,.006)
    base.box('bed-status-recess',(.5,.445,.24),(.07,.012,.022),'Panel',c,.003)
    base.box('bed-ice-status',(.5,.453,.24),(.023,.006,.006),'Ice',c,.001)

def nightstand(c):
    base.box('nightstand-inset-toe',(0,-.05,.035),(.50,.49,.07),'Base',c,.008)
    base.box('nightstand-graphite-shell',(0,-.05,.31),(.64,.62,.50),'Graphite',c,.015)
    base.box('nightstand-stone-top',(0,-.05,.575),(.66,.64,.05),'Stone',c,.010)
    for z in [.205,.425]:
        base.box('nightstand-glacier-drawer',(0,.271,z),(.58,.035,.195),'Glacier',c,.008)
        base.box('nightstand-finger-recess',(0,.294,z+.062),(.36,.009,.015),'Graphite',c,.002)
        base.box('nightstand-silver-lip',(0,.302,z+.051),(.36,.012,.008),'Titanium',c,.002)
    base.box('nightstand-ice-status',(.22,.293,.102),(.020,.005,.006),'Ice',c,.001)

def art_shape(name,coords,role,c,depth=-.410):
    return base.surface(name,[(x,depth,z) for x,z in coords],[tuple(range(len(coords)))],role,c,.002)

def artwork(c,variant):
    base.box('art-graphite-backing',(0,-.462,.50),(.80,.045,.76),'Graphite',c,.006)
    for x in [-.39,.39]: base.box('art-silver-frame-side',(x,-.43,.50),(.020,.025,.76),'Titanium',c,.003)
    for z in [.13,.87]: base.box('art-silver-frame-rail',(0,-.43,z),(.78,.025,.020),'Titanium',c,.003)
    base.box('art-canvas',(0,-.433,.50),(.752,.014,.714),'SpaceBlue' if variant!=1 else 'Glacier',c,.002)
    if variant==0:
        circle=[(.12+.18*math.cos(i*math.tau/32),.60+.18*math.sin(i*math.tau/32)) for i in range(32)]
        art_shape('art-glacier-disc',circle,'Glacier',c)
        art_shape('art-graphite-mountain',[(-.35,.18),(.35,.18),(.35,.38),(.08,.57),(-.13,.37),(-.35,.45)],'Graphite',c,-.403)
        art_shape('art-ice-horizon',[(-.31,.30),(.30,.30),(.30,.306),(-.31,.306)],'Ice',c,-.397)
    elif variant==1:
        for i,(z,role) in enumerate([(.20,'Graphite'),(.36,'SpaceBlue'),(.52,'Titanium')]):
            # Keep the valley off the cap's triangulation diagonal. The old .09
            # produced a collinear ear after the DAE's seven-digit quantization.
            art_shape('art-layered-landscape',[(-.35,z),(.35,z),(.35,z+.08),(.12,z+.17),(-.10,z+.105),(-.35,z+.14)],role,c,-.409-i*.005)
        art_shape('art-small-ice-sun',[(.23+.040*math.cos(i*math.tau/20),.72+.040*math.sin(i*math.tau/20)) for i in range(20)],'Ice',c,-.390)
    else:
        for i,role in enumerate(['Glacier','Titanium','Ice']):
            points=[]
            for j in range(41):
                a=j*math.tau/40; x=.27*math.cos(a); z=.14*math.sin(a); angle=math.radians(-38+i*38)
                points.append((x*math.cos(angle)-z*math.sin(angle),-.407+i*.004,.50+x*math.sin(angle)+z*math.cos(angle)))
            points[-1]=points[0]; base.tube('art-orbit',points,.003 if i!=2 else .002,'Ice' if i==2 else role,c)
        art_shape('art-orbit-core',[(.040*math.cos(i*math.tau/20),.50+.040*math.sin(i*math.tau/20)) for i in range(20)],'Glacier',c,-.386)

def build(name):
    if name not in NAMES: raise ValueError(name)
    c=bpy.data.collections.get('COL-'+name)
    if c:
        for obj in list(c.objects): bpy.data.objects.remove(obj,do_unlink=True)
    else:
        c=bpy.data.collections.new('COL-'+name); bpy.context.scene.collection.children.link(c)
    if name.startswith('HomeFireplace'): fireplace(c,name.endswith('On'))
    elif name=='HomeDiningSet': dining(c)
    elif name=='HomeDoubleBed': bed(c)
    elif name=='HomeNightstand': nightstand(c)
    else: artwork(c,int(name[-1]))
    bpy.context.view_layer.update(); return base.export(c,name)

def render(name,back=False):
    scene=bpy.context.scene
    for c in bpy.data.collections:
        if c.name.startswith('COL-Home'): c.hide_render=c.name!='COL-'+name
    camera=bpy.data.objects.get('CAM-home-extension')
    if not camera:
        camera=bpy.data.objects.new('CAM-home-extension',bpy.data.cameras.new('CAM-home-extension')); scene.collection.objects.link(camera)
    scene.camera=camera
    large=name in ['HomeDoubleBed','HomeDiningSet']; target=Vector((.5,-.5,.48)) if large else Vector((0,-.20,.48))
    camera.location=target+Vector((-3,-4,2.3) if back else (2.8,4,2.5))
    camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.type='ORTHO'; camera.data.ortho_scale=3.15 if large else 1.65 if 'Fireplace' in name else 1.35
    for obj in list(bpy.data.objects):
        if obj.type=='LIGHT' and obj.name.startswith('LGT-home'): obj.hide_render=True
    for label,loc,power,size in [('key',(3,4,6),420,5),('fill',(-4,1,3),220,4),('rim',(0,-4,4),300,3)]:
        obj=bpy.data.objects.get('LGT-extension-'+label)
        if not obj:
            data=bpy.data.lights.new('LGT-extension-'+label,'AREA'); obj=bpy.data.objects.new(data.name,data); scene.collection.objects.link(obj)
        obj.data.energy=power; obj.data.size=size; obj.location=loc; obj.rotation_euler=(target-obj.location).to_track_quat('-Z','Y').to_euler()
    scene.render.engine='CYCLES'; scene.cycles.samples=24; scene.cycles.use_denoising=True
    scene.render.resolution_x=640; scene.render.resolution_y=640; scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'; scene.render.film_transparent=False; scene.view_settings.view_transform='AgX'
    scene.render.filepath=str(OUTPUT/(name+('-back.png' if back else '-front.png')))
    bpy.ops.render.render(write_still=True); return scene.render.filepath

def save():
    # Save a separate gallery; restore live object positions afterwards. Existing source
    # .blend files and all older asset bytes remain untouched.
    original={o.name:o.location.copy() for o in bpy.data.objects if o.type=='MESH'}
    visibility={c.name:c.hide_render for c in bpy.data.collections}
    camera=bpy.data.objects.get('CAM-home-extension'); previous_camera=bpy.context.scene.camera
    camera_matrix=camera.matrix_world.copy() if camera else None; camera_scale=camera.data.ortho_scale if camera else None
    try:
        for c in bpy.data.collections:
            if c.name.startswith('COL-Home'): c.hide_render=c.name not in ['COL-'+n for n in NAMES]
        for i,name in enumerate(NAMES):
            for obj in bpy.data.collections['COL-'+name].objects: obj.location+=Vector(((i%4-1.5)*3.8,(i//4-.5)*3.8,0))
        if camera:
            camera.location=(12,18,16); camera.rotation_euler=(Vector((0,0,.6))-camera.location).to_track_quat('-Z','Y').to_euler(); camera.data.ortho_scale=19; bpy.context.scene.camera=camera
        bpy.data.images['HomeFurniture'].pack()
        bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT/'ModernHomeExtension.blend'))
    finally:
        for name,loc in original.items(): bpy.data.objects[name].location=loc
        for name,hidden in visibility.items(): bpy.data.collections[name].hide_render=hidden
        if camera: camera.matrix_world=camera_matrix; camera.data.ortho_scale=camera_scale
        bpy.context.scene.camera=previous_camera
        bpy.context.view_layer.update()
    manifest={'status':'待验证','author':'Original project geometry and geometric artwork','artDirection':'Modern smart-home V2','blender':bpy.app.version_string,'generatorSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'baseGeneratorSha256':hashlib.sha256((ROOT/'scripts/New-HomeFurnitureAssets.py').read_bytes()).hexdigest(),'models':[json.loads((OUTPUT/(n+'.json')).read_text()) for n in NAMES],'texture':'Assets/Textures/HomeFurniture.png','textureSha256':hashlib.sha256((base.ASSETS/'Textures/HomeFurniture.png').read_bytes()).hexdigest(),'nativeBuildAndGameValidation':'Not run at user request'}
    (RECORDS/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    return {'scene':str(OUTPUT/'ModernHomeExtension.blend'),'models':len(NAMES),'exportTriangles':sum(m['triangles'] for m in manifest['models'])}
