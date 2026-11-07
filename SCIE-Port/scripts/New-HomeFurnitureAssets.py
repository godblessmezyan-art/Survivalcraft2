"""Original cozy furniture. Execute build(name) inside Blender via official MCP.

Blender owns geometry, bevels, UVs and rendering. The small COLLADA serializer
targets the verified SCNET importer (one identity node, independent offsets).
The runtime atlas contains color detail only; Blender PBR is preview-only.
"""
from pathlib import Path
import math, hashlib, json, xml.etree.ElementTree as ET
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT/'src/SCIEPort/Assets'
OUTPUT = ROOT/'artifacts/home-furniture'
PALETTE = [
    ('Cream',(.88,.84,.73),'paint'),('Oak',(.63,.39,.19),'wood'),
    ('Linen',(.77,.73,.62),'cloth'),('Sage',(.40,.55,.40),'cloth'),
    ('Steel',(.58,.64,.64),'metal'),('Brass',(.68,.48,.20),'metal'),
    ('Ceramic',(.92,.93,.86),'paint'),('Shadow',(.13,.17,.15),'paint'),
    ('Water',(.37,.69,.73),'water'),('OakDark',(.28,.17,.09),'wood'),
    ('Ivory',(.96,.92,.81),'paint'),('Seam',(.49,.47,.39),'cloth'),
    ('Leaf',(.25,.40,.25),'paint'),('Clay',(.63,.33,.23),'paint'),
    ('Paper',(.78,.72,.55),'paint'),('Stone',(.65,.66,.60),'stone')]
NAMES = ['HomeFridge','HomeFridgeOpen'] + ['HomeCabinet'+str(i) for i in range(4)] + ['HomeSink','HomeSinkOpen'] + ['HomeSofa'+str(i) for i in range(5)] + ['HomeCoffeeTable','HomeCurtain','HomeCurtainOpen']

def setup():
    OUTPUT.mkdir(parents=True,exist_ok=True)
    # A dedicated factory-startup scene was inspected before authorizing this reset.
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    image=bpy.data.images.get('HomeFurniture') or bpy.data.images.new('HomeFurniture',width=512,height=512,alpha=False)
    pixels=[]
    for y in range(512):
        for x in range(512):
            k=(y//128)*4+x//128; color=PALETTE[k][1]; kind=PALETTE[k][2]
            u=(x%128)/128; v=(y%128)/128
            noise=math.sin(x*127.1+y*311.7)*43758.5453; noise-=math.floor(noise)
            delta=(noise-.5)*.018
            if kind=='wood': delta+=.032*math.sin(90*v+5*math.sin(7*u))+.012*math.sin(235*v+11*math.sin(8*u))
            elif kind=='cloth': delta+=.018*((x%4<2)-(y%4<2))+.008*math.sin(u*40)
            elif kind=='metal': delta+=.012*math.sin(v*190)
            elif kind=='stone': delta+=.018*math.sin(x*.13+y*.09)*math.sin(x*.07-y*.11)
            pixels.extend([max(0,min(1,c+delta)) for c in color]+[1])
    image.pixels=pixels; image.filepath_raw=str(ASSETS/'Textures/HomeFurniture.png'); image.file_format='PNG'; image.save()
    for role,color,kind in PALETTE:
        material=bpy.data.materials.get('MAT-home-'+role) or bpy.data.materials.new('MAT-home-'+role)
        material.use_nodes=True; shader=material.node_tree.nodes.get('Principled BSDF')
        shader.inputs['Metallic'].default_value=1 if kind=='metal' else 0
        shader.inputs['Roughness'].default_value=.38 if kind=='metal' else .76
        tex=material.node_tree.nodes.new('ShaderNodeTexImage'); tex.image=image; tex.interpolation='Closest'
        material.node_tree.links.new(tex.outputs['Color'],shader.inputs['Base Color'])
    world=bpy.data.worlds.new('Home-neutral-world'); world.use_nodes=True
    world.node_tree.nodes['Background'].inputs[0].default_value=(.34,.39,.38,1)
    world.node_tree.nodes['Background'].inputs[1].default_value=.65; bpy.context.scene.world=world
    print('Home atlas and materials initialized')

def finish(obj,name,role,collection):
    obj.name='GEO-'+name
    for c in list(obj.users_collection): c.objects.unlink(obj)
    collection.objects.link(obj)
    obj.data.materials.clear(); obj.data.materials.append(bpy.data.materials['MAT-home-'+role])
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    mesh=obj.data; uv=mesh.uv_layers.get('HomeUV') or mesh.uv_layers.new(name='HomeUV')
    mesh.uv_layers.active=uv; uv.active_render=True
    index=next(i for i,p in enumerate(PALETTE) if p[0]==role); col=index%4; row=index//4
    minimum=Vector(tuple(min(v.co[i] for v in mesh.vertices) for i in range(3)))
    extent=Vector(tuple(max(v.co[i] for v in mesh.vertices)-minimum[i] for i in range(3)))
    for poly in mesh.polygons:
        axis=max(range(3),key=lambda a:abs(poly.normal[a])); axes=[a for a in range(3) if a!=axis]
        for li in poly.loop_indices:
            co=mesh.vertices[mesh.loops[li].vertex_index].co
            a,b=[(co[i]-minimum[i])/max(extent[i],.00001) for i in axes]
            uv.data[li].uv=((col+.04+.92*a)/4,(row+.04+.92*b)/4)
    return obj

def box(name,loc,size,role,c,bevel=.015,rotation=0):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc)
    obj=bpy.context.object; obj.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=obj.modifiers.new('Crafted edge','BEVEL'); mod.width=min(bevel,min(size)*.24); mod.segments=2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.rotation_euler.z=rotation
    return finish(obj,name,role,c)

def cylinder(name,a,b,radius,role,c,sides=12):
    direction=Vector(b)-Vector(a)
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides,radius=radius,depth=direction.length,location=(Vector(a)+Vector(b))/2)
    obj=bpy.context.object; obj.rotation_euler=direction.to_track_quat('Z','Y').to_euler()
    return finish(obj,name,role,c)

def surface(name,vertices,faces,role,c,thickness=0):
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(vertices,[],faces); mesh.update()
    obj=bpy.data.objects.new(name,mesh); c.objects.link(obj); bpy.context.view_layer.objects.active=obj
    if thickness:
        mod=obj.modifiers.new('Real cloth thickness','SOLIDIFY'); mod.thickness=thickness
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(obj,name,role,c)

def tube(name,points,radius,role,c):
    closed=points[0]==points[-1]
    if closed: points=points[:-1]
    verts=[]; sides=10
    for i,p in enumerate(points):
        tangent=(Vector(points[(i+1)%len(points)])-Vector(points[(i-1)%len(points)])) if closed else Vector(points[min(i+1,len(points)-1)])-Vector(points[max(0,i-1)])
        tangent.normalize(); cross=tangent.cross(Vector((0,1,0)))
        if cross.length<.01: cross=tangent.cross(Vector((1,0,0)))
        cross.normalize(); second=tangent.cross(cross)
        for j in range(sides): verts.append(tuple(Vector(p)+radius*(math.cos(j*math.tau/sides)*cross+math.sin(j*math.tau/sides)*second)))
    faces=[] if closed else [tuple(reversed(range(sides))),tuple((len(points)-1)*sides+j for j in range(sides))]
    for i in range(len(points) if closed else len(points)-1):
        nxt=(i+1)%len(points)
        for j in range(sides): faces.append((i*sides+j,i*sides+(j+1)%sides,nxt*sides+(j+1)%sides,nxt*sides+j))
    return surface(name,verts,faces,role,c)

def cushion(name,loc,size,role,c):
    # Multiple gently inflated cross-sections, with an explicit stitched welt.
    obj=box(name,loc,size,role,c,bevel=min(size)*.22)
    x,y,z=loc; w,d,h=size
    points=[]
    for cx,cy,base in [(w/2-.035,d/2-.035,0),(-w/2+.035,d/2-.035,90),(-w/2+.035,-d/2+.035,180),(w/2-.035,-d/2+.035,270)]:
        for j in range(4):
            angle=math.radians(base+j*30)
            points.append((x+cx+.035*math.cos(angle),y+cy+.035*math.sin(angle),z+h*.27))
    points.append(points[0]); tube(name+'-welt',points,.0035,'Seam',c)
    return obj

def cabinet(c,variant=0,sink=False):
    bottom=.32 if variant==2 else .08; top=.80
    width=.96; depth=.52 if variant==2 else .90; back=-.44; front=back+depth
    if variant!=2: box('oak-plinth',(0,back+depth/2,bottom/2),(width-.08,depth-.06,bottom),'OakDark',c,.008)
    box('left-stile',(-.455,back+depth/2,(top+bottom)/2),(.05,depth,top-bottom),'Oak',c)
    box('right-stile',(.455,back+depth/2,(top+bottom)/2),(.05,depth,top-bottom),'Oak',c)
    box('cabinet-back',(0,back+.025,(top+bottom)/2),(.90,.05,top-bottom),'Cream',c,.008)
    box('cabinet-floor',(0,back+depth/2,bottom+.025),(.90,depth,.05),'Oak',c,.008)
    if not sink: box('rounded-stone-counter',(0,back+depth/2,top+.025),(.99,depth+.04,.05),'Stone' if variant!=2 else 'Oak',c,.016)
    if variant==3:
        for i in range(3):
            z=bottom+.12+i*.22
            box('drawer-'+str(i),(0,front+.008,z),(.86,.045,.205),'Cream',c,.012)
            cylinder('drawer-handle-'+str(i),(-.12,front+.05,z+.025),(.12,front+.05,z+.025),.011,'Brass',c)
    else:
        for side in [-1,1]:
            x=side*.22
            box('shaker-door-'+str(side),(x,front+.015,(top+bottom)/2),(.42,.045,top-bottom-.025),'Cream',c,.012)
            box('inset-panel-'+str(side),(x,front+.040,(top+bottom)/2),(.32,.012,top-bottom-.15),'Ivory',c,.006)
            cylinder('door-pull-'+str(side),(side*.055,front+.075,top-.19),(side*.055,front+.075,top-.08),.009,'Brass',c)
    if variant==1:
        # A return door gives the corner module two usable visual fronts.
        box('corner-return-door',(.486,0,.45),(.035,.73,.64),'Cream',c,.012)
        box('corner-return-inset',(.508,0,.45),(.012,.60,.51),'Ivory',c,.006)
        cylinder('corner-return-pull',(.535,.25,.60),(.535,.25,.71),.009,'Brass',c)

def fridge(c,opened):
    for x in [-.37,.37]:
        for y in [-.32,.32]: cylinder('levelling-foot',(x,y,0),(x,y,.075),.035,'Shadow',c)
    box('insulated-back',(0,-.39,.98),(.86,.08,1.80),'Cream',c,.025)
    for x in [-.40,.40]: box('rounded-side',(x,0,.98),(.08,.78,1.80),'Cream',c,.025)
    for z in [.10,1.85]: box('insulated-cap',(0,0,z),(.82,.78,.08),'Cream',c,.018)
    for z in [.27,.66,1.05,1.38]:
        box('shelf',(0,0,z),(.72,.67,.025),'Ivory',c,.008)
        cylinder('shelf-lip',(-.34,.32,z),(.34,.32,z),.008,'Steel',c)
    box('freezer-divider',(0,0,1.39),(.75,.73,.055),'Cream',c,.015)
    for z,h in [(.73,1.22),(1.65,.40)]:
        door=box('recessed-door',(0,.415,z),(.84,.095,h),'Cream',c,.035)
        seal=box('door-gasket',(0,.355,z),(.79,.025,h-.04),'Shadow',c,.018)
        handle=cylinder('brushed-handle',(-.28,.495,z-h*.25),(-.28,.495,z+h*.25),.014,'Brass',c)
        if opened:
            hinge=Vector((.40,.38,z)); angle=-math.radians(72)
            for obj in [door,seal,handle]:
                v=obj.location-hinge; obj.location=hinge+Vector((math.cos(angle)*v.x-math.sin(angle)*v.y,math.sin(angle)*v.x+math.cos(angle)*v.y,v.z)); obj.rotation_euler.z+=angle
    box('lower-vent',(0,.41,.09),(.62,.012,.04),'Shadow',c,.006)
    for i in range(8): box('vent-fin',(-.27+i*.077,.421,.09),(.012,.009,.035),'Steel',c,.002)

def sink(c,opened):
    cabinet(c,sink=True)
    deck=[]; deckfaces=[]; sides=24
    for ring in [0,1]:
        for i in range(sides):
            a=i*math.tau/sides; x,y=math.cos(a),math.sin(a)
            radius=min(.48/max(abs(x),1e-6),.475/max(abs(y),1e-6))
            deck.append((radius*x,radius*y,.849) if ring==0 else (.295*x,.035+.265*y,.849))
    for i in range(sides): deckfaces.append((i,(i+1)%sides,sides+(i+1)%sides,sides+i))
    surface('continuous-inset-counter',deck,deckfaces,'Stone',c,.045)
    verts=[]; faces=[]; sides=24
    for rx,ry,z in [(.295,.265,.849),(.272,.242,.815),(.212,.177,.63),(.19,.155,.62)]:
        for i in range(sides):
            a=i*math.tau/sides; verts.append((rx*math.cos(a),.035+ry*math.sin(a),z))
    for ring in range(3):
        for i in range(sides): faces.append((ring*sides+i,ring*sides+(i+1)%sides,(ring+1)*sides+(i+1)%sides,(ring+1)*sides+i))
    faces.append(tuple(range(3*sides,4*sides)))
    surface('deep-ceramic-bowl',verts,faces,'Ceramic',c,.008)
    cylinder('drain',(0,.035,.62),(0,.035,.626),.035,'Steel',c,16)
    points=[(0,-.29,.84),(0,-.29,.935),(0,-.26,.982),(0,-.19,.988),(0,-.12,.957),(0,-.10,.92)]
    tube('curved-mixer',points,.016,'Steel',c)
    cylinder('mixer-lever',(.065,-.29,.85),(.065,-.29,.94),.018,'Brass',c)
    if opened: cylinder('water-stream',(0,-.10,.919),(0,-.10,.64),.012,'Water',c,8)

def sofa(c,variant):
    for x in [-.36,.36]:
        for y in [-.32,.32]: cylinder('tapered-leg',(x,y,0),(x*.97,y*.97,.16),.033,'OakDark',c)
    box('oak-frame',(0,0,.19),(.94,.88,.14),'Oak',c,.025)
    left=variant in [0,1,4]; right=variant in [0,3]
    seatwidth=.94-(.14 if left else 0)-(.14 if right else 0)
    seatx=(.07 if left else 0)-(.07 if right else 0)
    cushion('seat',(seatx,.075,.40),(seatwidth,.70,.24),'Linen',c)
    cushion('angled-back',(0,-.345,.72),(.94,.20,.46),'Linen',c)
    if left: cushion('left-arm',(-.405,.025,.55),(.16,.87,.31),'Linen',c)
    if right: cushion('right-arm',(.405,.025,.55),(.16,.87,.31),'Linen',c)
    if variant==4: cushion('corner-back',(-.365,.02,.72),(.20,.84,.44),'Linen',c)
    if variant in [0,1,3,4]:
        pillow=cushion('sage-pillow',(seatx,-.17,.66),(.30,.15,.30),'Sage',c)
        pillow.rotation_euler.y=math.radians(12)

def table(c):
    box('soft-oak-tabletop',(0,0,.41),(.88,.79,.075),'Oak',c,.055)
    for x in [-.31,.31]:
        for y in [-.26,.26]: cylinder('splayed-leg',(x*1.06,y*1.06,.015),(x*.91,y*.91,.38),.039,'OakDark',c)
    box('lower-shelf',(0,0,.17),(.64,.52,.025),'Oak',c,.014)
    box('linen-book',(0,-.02,.463),(.22,.17,.025),'Sage',c,.005)
    box('book-pages',(0,-.02,.475),(.20,.155,.014),'Paper',c,.002)
    cylinder('ceramic-cup',(.23,.12,.455),(.23,.12,.53),.041,'Ceramic',c,16)
    cylinder('tea-surface',(.23,.12,.531),(.23,.12,.533),.033,'OakDark',c,16)

def curtain(c,opened):
    cylinder('curtain-rod',(-.48,-.38,1.92),(.48,-.38,1.92),.015,'Brass',c,16)
    for side in [-1,1]:
        cylinder('rod-support',(side*.42,-.46,1.92),(side*.42,-.37,1.92),.018,'OakDark',c)
        center=side*(.37 if opened else .23); width=.20 if opened else .46
        for panel in [0,1]:
            z0=.06 if panel==0 else 1.75; height=1.74 if panel==0 else .055
            verts=[]; faces=[]; steps=32
            for row in range(3):
                z=z0+height*row/2
                for j in range(steps+1):
                    t=j/steps; x=center+(t-.5)*width
                    y=-.35+.038*math.cos(t*math.tau*5)+.004*math.sin(row*2+t*8)
                    verts.append((x,y,z))
            for row in range(2):
                for j in range(steps):
                    a=row*(steps+1)+j; faces.append((a,a+1,a+steps+2,a+steps+1))
            surface('pleated-fabric' if panel==0 else 'woven-header',verts,faces,'Sage' if panel==0 else 'Linen',c,.005)
        for j in range(5):
            x=center+(j/4-.5)*width*.88
            tube('curtain-loop',[(x,-.36,1.79),(x,-.39,1.92),(x,-.35,1.945),(x,-.32,1.81)],.009,'Linen',c)

def export(c,name):
    ns='http://www.collada.org/2005/11/COLLADASchema'; ET.register_namespace('',ns)
    def e(parent,tag,attrib=None,text=None):
        child=ET.SubElement(parent,'{'+ns+'}'+tag,attrib or {})
        if text is not None: child.text=str(text)
        return child
    root=ET.Element('{'+ns+'}COLLADA',{'version':'1.4.1'}); asset=e(root,'asset'); e(asset,'unit',{'name':'meter','meter':'1'}); e(asset,'up_axis',text='Y_UP')
    lib=e(root,'library_geometries'); visual=e(root,'library_visual_scenes'); scene=e(visual,'visual_scene',{'id':'Scene','name':'Scene'}); node=e(scene,'node',{'id':name,'name':name,'type':'NODE'}); e(node,'matrix',{'sid':'transform'},'1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1')
    triangles=0; bounds=[]
    for index,obj in enumerate(c.objects):
        if obj.type!='MESH': continue
        mesh=obj.data; mesh.calc_loop_triangles(); positions=[]; normals=[]; uvs=[]
        normalmat=obj.matrix_world.to_3x3().inverted().transposed()
        for tri in mesh.loop_triangles:
            for li in tri.loops:
                co=obj.matrix_world@mesh.vertices[mesh.loops[li].vertex_index].co
                normal=(normalmat@mesh.corner_normals[li].vector).normalized()
                positions.extend((co.x,co.z,-co.y)); normals.extend((normal.x,normal.z,-normal.y))
                uv=mesh.uv_layers.active.data[li].uv; uvs.extend((uv.x,1-uv.y)); bounds.append((co.x,co.z,-co.y))
        count=len(positions)//3; triangles+=count//3
        ident='g'+str(index); geom=e(lib,'geometry',{'id':ident,'name':obj.name}); m=e(geom,'mesh')
        for suffix,values,stride,params in [('p',positions,3,'XYZ'),('n',normals,3,'XYZ'),('uv',uvs,2,'ST')]:
            source=e(m,'source',{'id':ident+suffix}); e(source,'float_array',{'id':ident+suffix+'a','count':str(len(values))},' '.join(format(v,'.7g') for v in values))
            accessor=e(e(source,'technique_common'),'accessor',{'source':'#'+ident+suffix+'a','count':str(count),'stride':str(stride)})
            for param in params: e(accessor,'param',{'name':param,'type':'float'})
        vert=e(m,'vertices',{'id':ident+'v'}); e(vert,'input',{'semantic':'POSITION','source':'#'+ident+'p'})
        tris=e(m,'triangles',{'count':str(count//3)})
        for semantic,source,offset in [('VERTEX','v',0),('NORMAL','n',1),('TEXCOORD','uv',2)]: e(tris,'input',{'semantic':semantic,'source':'#'+ident+source,'offset':str(offset)})
        e(tris,'p',text=' '.join(str(i) for j in range(count) for i in (j,j,j))); e(node,'instance_geometry',{'url':'#'+ident,'name':obj.name})
    e(e(root,'scene'),'instance_visual_scene',{'url':'#Scene'})
    path=ASSETS/'Models'/f'{name}.dae'; ET.ElementTree(root).write(path,encoding='utf-8',xml_declaration=True)
    report={'name':name,'triangles':triangles,'meshes':len(c.objects),'bounds':[[min(v[i] for v in bounds) for i in range(3)],[max(v[i] for v in bounds) for i in range(3)]],'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
    (OUTPUT/(name+'.json')).write_text(json.dumps(report,indent=2),encoding='utf-8')
    return report

def build(name):
    assert name in NAMES
    c=bpy.data.collections.get('COL-'+name)
    if c:
        for obj in list(c.objects): bpy.data.objects.remove(obj,do_unlink=True)
    else: c=bpy.data.collections.new('COL-'+name); bpy.context.scene.collection.children.link(c)
    if name.startswith('HomeFridge'): fridge(c,name.endswith('Open'))
    elif name.startswith('HomeCabinet'): cabinet(c,int(name[-1]))
    elif name.startswith('HomeSink'): sink(c,name.endswith('Open'))
    elif name.startswith('HomeSofa'): sofa(c,int(name[-1]))
    elif name=='HomeCoffeeTable': table(c)
    elif name.startswith('HomeCurtain'): curtain(c,name.endswith('Open'))
    bpy.context.view_layer.update()
    return export(c,name)

def render(name,view='front'):
    from mathutils import Vector
    c=bpy.data.collections['COL-'+name]
    for other in bpy.data.collections:
        if other.name.startswith('COL-Home'): other.hide_render=other!=c
    scene=bpy.context.scene
    if 'CAM-home' not in bpy.data.objects:
        camera=bpy.data.objects.new('CAM-home',bpy.data.cameras.new('CAM-home')); scene.collection.objects.link(camera)
    camera=bpy.data.objects['CAM-home']; scene.camera=camera
    tall='Fridge' in name or 'Curtain' in name; target=Vector((0,0,.92 if tall else .48))
    camera.location=target+Vector(((-2.8 if name=='HomeFridgeOpen' else 2.8),4,2.5) if view=='front' else (-3,-4,2.1))
    camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler(); camera.data.type='ORTHO'; camera.data.ortho_scale=2.55 if tall else 1.55
    for obj in list(bpy.data.objects):
        if obj.name.startswith('LGT-home'): bpy.data.objects.remove(obj,do_unlink=True)
    for tag,loc,power,size in [('key',(3,4,6),420,5),('fill',(-4,1,3),220,4),('rim',(0,-4,4),300,3)]:
        light=bpy.data.lights.new('LGT-home-'+tag,'AREA'); light.energy=power; light.shape='DISK'; light.size=size
        obj=bpy.data.objects.new(light.name,light); scene.collection.objects.link(obj); obj.location=loc; obj.rotation_euler=(target-obj.location).to_track_quat('-Z','Y').to_euler()
    scene.render.engine='CYCLES'; scene.cycles.samples=24; scene.cycles.use_denoising=True
    scene.render.resolution_x=640; scene.render.resolution_y=640; scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'; scene.render.film_transparent=False
    scene.view_settings.view_transform='AgX'; scene.render.filepath=str(OUTPUT/(name+'-'+view+'.png'))
    bpy.ops.render.render(write_still=True)
    return scene.render.filepath

def save():
    original={obj.name:obj.location.copy() for obj in bpy.data.objects if obj.type=='MESH'}
    camera=bpy.data.objects.get('CAM-home')
    old_camera=camera.matrix_world.copy() if camera else None
    old_scale=camera.data.ortho_scale if camera else None
    for c in bpy.data.collections:
        if c.name.startswith('COL-Home'): c.hide_render=False
    for index,name in enumerate(NAMES):
        offset=Vector(((index%4-1.5)*2.1,(index//4-1.5)*2.8,0))
        for obj in bpy.data.collections['COL-'+name].objects: obj.location+=offset
    if camera:
        camera.location=(10,16,15); camera.rotation_euler=(Vector((0,0,.8))-camera.location).to_track_quat('-Z','Y').to_euler(); camera.data.ortho_scale=14
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                area.spaces.active.shading.type='MATERIAL'; area.spaces.active.shading.use_scene_world=False
                area.spaces.active.region_3d.view_location=(0,0,.7); area.spaces.active.region_3d.view_distance=14
    bpy.data.images['HomeFurniture'].pack()
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT/'CozyFurniture.blend'))
    for name,location in original.items(): bpy.data.objects[name].location=location
    if camera: camera.matrix_world=old_camera; camera.data.ortho_scale=old_scale
    bpy.context.view_layer.update()
    reports=[json.loads((OUTPUT/(n+'.json')).read_text()) for n in NAMES]
    manifest={'author':'Original project geometry and procedural textures','blender':bpy.app.version_string,'generatorSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'models':reports,'textureSha256':hashlib.sha256((ASSETS/'Textures/HomeFurniture.png').read_bytes()).hexdigest()}
    target=ROOT/'docs/assets/home-furniture'; target.mkdir(parents=True,exist_ok=True)
    (target/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    return manifest
