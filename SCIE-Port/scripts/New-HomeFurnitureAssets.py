"""Original modern smart-home furniture V2. Author inside Blender via official MCP.

Blender owns geometry, bevels, UVs and rendering. The small COLLADA serializer
targets the verified SCNET importer (one identity node, independent offsets).
The runtime atlas contains color detail only; Blender PBR is preview-only.
"""
from pathlib import Path
import math, hashlib, json, struct, zlib, xml.etree.ElementTree as ET
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT/'src/SCIEPort/Assets'
OUTPUT = ROOT/'artifacts/home-furniture-v2'
# sRGB palette. The runtime gets these color pixels, not Blender PBR materials.
PALETTE = [
    ('Glacier','#E5EAEE','paint'),('Titanium','#A4B1BD','metal'),
    ('MistCloth','#E5EAEE','cloth'),('SpaceBlue','#546C80','cloth'),
    ('Steel','#A4B1BD','metal'),('EdgeSilver','#C8D1D8','metal'),
    ('Porcelain','#E5EAEE','paint'),('Graphite','#303943','paint'),
    ('Ice','#65D5EC','indicator'),('Base','#303943','paint'),
    ('Glass','#303943','glass'),('Seam','#B5C0C9','cloth'),
    ('Panel','#252E38','panel'),('Curtain','#C8D0D6','cloth'),
    ('Paper','#E5EAEE','paint'),('Stone','#BCC4CA','stone')]
NAMES = ['HomeFridge','HomeFridgeOpen'] + ['HomeCabinet'+str(i) for i in range(4)] + ['HomeSink','HomeSinkOpen'] + ['HomeSofa'+str(i) for i in range(5)] + ['HomeCoffeeTable','HomeCurtain','HomeCurtainOpen']

def write_atlas():
    """Write sRGB bytes directly: no preview color transform, no alpha/transparency."""
    raw=bytearray()
    for y in range(512):
        raw.append(0)
        for x in range(512):
            # PNG rows are top-down, matching export()'s V flip.
            k=((511-y)//128)*4+x//128; _,hexcolor,kind=PALETTE[k]
            color=[int(hexcolor[i:i+2],16) for i in (1,3,5)]
            u=(x%128)/127; v=((511-y)%128)/127
            noise=math.sin(x*127.1+y*311.7)*43758.5453; noise-=math.floor(noise)
            delta=(noise-.5)*1.4
            if kind=='cloth': delta+=1.5*((x%4<2)-(y%4<2))+.7*math.sin(u*50)
            elif kind=='metal': delta+=1.8*math.sin(v*370)+5.0*math.sin(u*math.pi)-2.5
            elif kind=='stone': delta+=2.3*math.sin(x*.13+y*.09)*math.sin(x*.07-y*.11)+1.4*math.cos(v*30+u*7)
            elif kind=='glass': delta+=7*math.exp(-((u-.35*v-.25)/.055)**2)+2*math.exp(-((u-.35*v-.6)/.11)**2)
            elif kind=='panel': delta+=.7*math.cos(v*95)
            elif kind=='indicator': delta=0
            raw.extend(max(0,min(255,round(c+delta))) for c in color)
            raw.append(255)
    def chunk(tag,data): return struct.pack('>I',len(data))+tag+data+struct.pack('>I',zlib.crc32(tag+data)&0xffffffff)
    png=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',512,512,8,6,0,0,0))+chunk(b'sRGB',b'\x00')+chunk(b'IDAT',zlib.compress(bytes(raw),9))+chunk(b'IEND',b'')
    path=ASSETS/'Textures/HomeFurniture.png'; pending=path.with_suffix('.png.pending')
    pending.write_bytes(png); pending.replace(path)
    return path

def setup():
    OUTPUT.mkdir(parents=True,exist_ok=True)
    # A dedicated factory-startup scene was inspected before authorizing this reset.
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    path=write_atlas()
    image=bpy.data.images.load(str(path),check_existing=False); image.name='HomeFurniture'; image.colorspace_settings.name='sRGB'
    for role,color,kind in PALETTE:
        material=bpy.data.materials.get('MAT-home-'+role) or bpy.data.materials.new('MAT-home-'+role)
        material.use_nodes=True; material.node_tree.nodes.clear()
        shader=material.node_tree.nodes.new('ShaderNodeBsdfPrincipled'); out=material.node_tree.nodes.new('ShaderNodeOutputMaterial')
        material.node_tree.links.new(shader.outputs['BSDF'],out.inputs['Surface'])
        # Keep the proof image faithful to the color-only game pipeline. No preview-only
        # metal, transmission or emission that could hide a weak runtime texture.
        shader.inputs['Metallic'].default_value=0; shader.inputs['Roughness'].default_value=.78
        tex=material.node_tree.nodes.new('ShaderNodeTexImage'); tex.image=image; tex.interpolation='Closest'
        material.node_tree.links.new(tex.outputs['Color'],shader.inputs['Base Color'])
    world=bpy.data.worlds.new('Home-neutral-world'); world.use_nodes=True
    world.node_tree.nodes['Background'].inputs[0].default_value=(.28,.32,.37,1)
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
        mod=obj.modifiers.new('Crafted edge','BEVEL'); mod.width=min(bevel,min(size)*.24); mod.segments=1 if min(size)<.035 else 2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.rotation_euler.z=rotation
    return finish(obj,name,role,c)

def cylinder(name,a,b,radius,role,c,sides=12):
    direction=Vector(b)-Vector(a)
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides,radius=radius,depth=direction.length,location=(Vector(a)+Vector(b))/2)
    obj=bpy.context.object; obj.rotation_euler=direction.to_track_quat('Z','Y').to_euler()
    for poly in obj.data.polygons: poly.use_smooth=abs(poly.normal.z)<.99
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
    verts=[]; sides=6 if radius<.006 else 10
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
    obj=surface(name,verts,faces,role,c)
    for poly in obj.data.polygons: poly.use_smooth=closed or len(poly.vertices)==4
    return obj

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
    if variant!=2:
        box('recessed-graphite-toe',(0,back+depth/2-.025,bottom/2),(width-.09,depth-.11,bottom),'Base',c,.008)
        box('toe-silver-edge',(0,front-.065,.018),(.83,.008,.012),'Titanium',c,.002)
    box('graphite-left-stile',(-.455,back+depth/2,(top+bottom)/2),(.05,depth,top-bottom),'Graphite',c,.009)
    box('graphite-right-stile',(.455,back+depth/2,(top+bottom)/2),(.05,depth,top-bottom),'Graphite',c,.009)
    box('cabinet-back',(0,back+.025,(top+bottom)/2),(.90,.05,top-bottom),'Graphite',c,.008)
    box('cabinet-floor',(0,back+depth/2,bottom+.025),(.90,depth,.05),'Graphite',c,.008)
    box('continuous-reveal',(0,front-.008,(top+bottom)/2),(.90,.025,top-bottom-.02),'Panel',c,.003)
    if not sink: box('stone-slab' if variant!=2 else 'wall-cabinet-cap',(0,back+depth/2,top+.025),(.99,depth+.04,.05),'Stone' if variant!=2 else 'Graphite',c,.009)
    if variant==3:
        for i in range(3):
            z=bottom+.12+i*.22
            box('flush-drawer-'+str(i),(0,front+.008,z),(.86,.045,.205),'Glacier',c,.008)
            box('drawer-finger-recess-'+str(i),(0,front+.032,z+.076),(.56,.009,.018),'Graphite',c,.003)
            box('drawer-silver-lip-'+str(i),(0,front+.039,z+.065),(.56,.012,.009),'Titanium',c,.002)
    else:
        for side in [-1,1]:
            x=side*.22
            box('flush-glacier-door-'+str(side),(x,front+.015,(top+bottom)/2),(.426,.045,top-bottom-.025),'Glacier',c,.008)
            box('inset-finger-channel-'+str(side),(x,front+.040,top-.054),(.32,.009,.020),'Graphite',c,.003)
            box('machined-handle-lip-'+str(side),(x,front+.047,top-.067),(.32,.016,.010),'Titanium',c,.002)
    box('status-recess',(.35,front+.040,bottom+.046),(.055,.009,.015),'Panel',c,.002)
    box('ice-status',(.35,front+.046,bottom+.046),(.021,.006,.006),'Ice',c,.001)
    if variant==1:
        box('corner-flush-return',(.486,0,.45),(.035,.73,.64),'Glacier',c,.008)
        box('corner-return-channel',(.507,0,.71),(.009,.57,.020),'Graphite',c,.003)
        box('corner-return-lip',(.515,0,.697),(.012,.57,.010),'Titanium',c,.002)

def fridge(c,opened):
    for x in [-.37,.37]:
        for y in [-.32,.32]: cylinder('levelling-foot',(x,y,0),(x,y,.075),.035,'Graphite',c)
    box('titanium-back',(0,-.39,.98),(.86,.08,1.80),'Titanium',c,.018)
    for x in [-.40,.40]: box('titanium-side',(x,0,.98),(.08,.78,1.80),'Titanium',c,.018)
    for z in [.10,1.85]: box('insulated-cap',(0,0,z),(.82,.78,.08),'Titanium',c,.012)
    box('interior-liner',(0,-.343,.98),(.70,.014,1.62),'Glacier',c,.005)
    for z in [.27,.66,1.05,1.38]:
        box('shelf',(0,0,z),(.72,.67,.025),'Glacier',c,.006)
        cylinder('shelf-lip',(-.34,.32,z),(.34,.32,z),.008,'Steel',c)
    box('freezer-divider',(0,0,1.39),(.75,.73,.055),'Glacier',c,.012)
    for z,h in [(.745,1.23),(1.635,.43)]:
        parts=[]
        parts.append(box('titanium-door',(0,.415,z),(.84,.095,h),'Titanium',c,.018))
        parts.append(box('graphite-door-seal',(0,.355,z),(.80,.025,h-.02),'Graphite',c,.008))
        parts.append(box('handle-recess',(-.285,.466,z),(.052,.012,h*.46),'Graphite',c,.006))
        parts.append(box('handle-silver-edge',(-.305,.482,z),(.012,.029,h*.44),'EdgeSilver',c,.004))
        if z>1:
            parts.append(box('display-bezel',(.13,.466,z+.015),(.285,.012,.19),'Graphite',c,.010))
            parts.append(box('display-glass',(.13,.474,z+.015),(.255,.008,.16),'Panel',c,.006))
            for i,w in enumerate([.065,.10,.040]):
                parts.append(box('display-status-line',(.09,.480,z+.056-i*.032),(w,.004,.006),'Titanium',c,.001))
            parts.append(box('ice-indicator',(.218,.481,z-.038),(.016,.004,.007),'Ice',c,.001))
        if opened:
            hinge=Vector((.40,.38,z)); angle=-math.radians(72)
            for obj in parts:
                v=obj.location-hinge; obj.location=hinge+Vector((math.cos(angle)*v.x-math.sin(angle)*v.y,math.sin(angle)*v.x+math.cos(angle)*v.y,v.z)); obj.rotation_euler.z+=angle
    box('lower-vent',(0,.41,.09),(.62,.012,.04),'Graphite',c,.006)
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
    basin=surface('brushed-stainless-basin',verts,faces,'Steel',c,.008)
    for poly in basin.data.polygons: poly.use_smooth=len(poly.vertices)==4
    cylinder('drain-rim',(0,.035,.62),(0,.035,.626),.035,'EdgeSilver',c,16)
    cylinder('drain-recess',(0,.035,.626),(0,.035,.628),.023,'Graphite',c,12)
    points=[(0,-.29,.84),(0,-.29,1.075),(0,-.277,1.125),(0,-.245,1.156),(0,-.20,1.17),(0,-.15,1.164),(0,-.104,1.139),(0,-.073,1.095),(0,-.065,1.025)]
    tube('proportioned-mixer',points,.015,'Steel',c)
    cylinder('mixer-foot',(0,-.29,.843),(0,-.29,.864),.030,'EdgeSilver',c)
    cylinder('mixer-lever',(.065,-.29,.85),(.065,-.29,.925),.012,'Titanium',c)
    box('lever-top',(.065,-.265,.925),(.023,.075,.012),'Steel',c,.003)
    if opened: cylinder('water-stream',(0,-.065,1.024),(0,-.065,.64),.010,'Ice',c,8)

def sofa(c,variant):
    box('inset-floating-plinth',(0,-.02,.095),(.68,.59,.19),'Base',c,.013)
    box('graphite-shadow-frame',(0,0,.225),(.90,.82,.095),'Graphite',c,.016)
    box('silver-under-edge',(0,0,.265),(.88,.80,.010),'Titanium',c,.002)
    left=variant in [0,1,4]; right=variant in [0,3]
    seatwidth=.94-(.14 if left else 0)-(.14 if right else 0)
    seatx=(.07 if left else 0)-(.07 if right else 0)
    cushion('seat-under-layer',(seatx,.065,.292),(seatwidth-.012,.68,.045),'Seam',c)
    cushion('seat',(seatx,.075,.40),(seatwidth,.70,.24),'MistCloth',c)
    cushion('layered-back',(0,-.345,.72),(.94,.20,.46),'MistCloth',c)
    if left: cushion('left-arm',(-.405,.025,.55),(.16,.87,.31),'MistCloth',c)
    if right: cushion('right-arm',(.405,.025,.55),(.16,.87,.31),'MistCloth',c)
    if variant==4: cushion('corner-back',(-.365,.02,.72),(.20,.84,.44),'MistCloth',c)
    if variant in [0,1,3,4]:
        pillow=cushion('space-blue-pillow',(seatx,-.17,.66),(.30,.15,.30),'SpaceBlue',c)
        pillow.rotation_euler.y=math.radians(12)

def table(c):
    box('thin-smoked-glass-top',(0,0,.42),(.88,.79,.054),'Glass',c,.018)
    box('polished-glass-edge',(0,0,.395),(.868,.778,.009),'SpaceBlue',c,.002)
    box('titanium-underframe',(0,0,.378),(.79,.70,.022),'Titanium',c,.005)
    for x in [-.31,.31]:
        for y in [-.26,.26]:
            cylinder('slim-titanium-leg',(x*1.06,y*1.06,.015),(x*.97,y*.97,.378),.026,'Titanium',c)
            cylinder('graphite-foot',(x*1.06,y*1.06,.006),(x*1.06,y*1.06,.022),.027,'Graphite',c)
    for x in [-.31,.31]: box('lower-stretcher',(x,0,.10),(.018,.54,.020),'Titanium',c,.004)
    box('slim-tablet',(0,-.02,.458),(.22,.17,.014),'Graphite',c,.004)
    box('tablet-screen',(0,-.02,.466),(.197,.147,.004),'Panel',c,.001)
    box('tablet-status',(.065,-.06,.469),(.023,.004,.002),'Ice',c,0)
    cylinder('porcelain-cup',(.23,.12,.45),(.23,.12,.525),.041,'Porcelain',c,16)
    cylinder('cup-interior',(.23,.12,.526),(.23,.12,.529),.033,'Graphite',c,16)

def curtain(c,opened):
    box('minimal-track',(0,-.38,1.92),(.96,.055,.038),'Titanium',c,.008)
    box('track-shadow-slot',(0,-.348,1.91),(.91,.009,.012),'Graphite',c,.002)
    for side in [-1,1]:
        box('track-wall-mount',(side*.42,-.43,1.92),(.045,.09,.035),'Graphite',c,.006)
        center=side*(.37 if opened else .23); width=.20 if opened else .46
        for panel in [0,1]:
            z0=.06 if panel==0 else 1.75; height=1.74 if panel==0 else .055
            verts=[]; faces=[]; steps=30
            for row in range(4):
                z=z0+height*row/3
                for j in range(steps+1):
                    t=j/steps; x=center+(t-.5)*width
                    y=-.35+(.031 if opened else .038)*math.cos(t*math.tau*5)+.003*math.sin(t*math.pi)*math.sin(row*1.1)
                    verts.append((x,y,z))
            for row in range(3):
                for j in range(steps):
                    a=row*(steps+1)+j; faces.append((a,a+1,a+steps+2,a+steps+1))
            fabric=surface('silver-pleated-fabric' if panel==0 else 'woven-header',verts,faces,'Curtain' if panel==0 else 'MistCloth',c,.005)
            for poly in fabric.data.polygons: poly.use_smooth=True
        for j in range(5):
            x=center+(j/4-.5)*width*.88
            box('hidden-track-glider',(x,-.36,1.855),(.018,.020,.086),'Titanium',c,.004)

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
    path=ASSETS/'Models'/f'{name}.dae'; pending=path.with_suffix('.dae.pending')
    pending.write_bytes(ET.tostring(root,encoding='utf-8',xml_declaration=True)); pending.replace(path)
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
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT/'ModernFurnitureV2.blend'))
    for name,location in original.items(): bpy.data.objects[name].location=location
    if camera: camera.matrix_world=old_camera; camera.data.ortho_scale=old_scale
    bpy.context.view_layer.update()
    reports=[json.loads((OUTPUT/(n+'.json')).read_text()) for n in NAMES]
    manifest={'artRevision':'Modern smart home V2','status':'待验证','author':'Original project geometry and procedural textures','blender':bpy.app.version_string,'generatorSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'models':reports,'textureSha256':hashlib.sha256((ASSETS/'Textures/HomeFurniture.png').read_bytes()).hexdigest(),'note':'Export bookkeeping only. V2 target Engine import, build, automated tests and game/device execution were not run.'}
    target=ROOT/'docs/assets/home-furniture'; target.mkdir(parents=True,exist_ok=True)
    (target/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    regions=[]
    for i,(role,color,kind) in enumerate(PALETTE):
        col=i%4; row=i//4; u0=(col+.04)/4; u1=(col+.96)/4; v0=(row+.04)/4; v1=(row+.96)/4
        regions.append({'role':role,'srgb':color,'kind':kind,'blenderUV':[u0,v0,u1,v1],'engineUV':[u0,1-v1,u1,1-v0]})
    (target/'atlas_regions.json').write_text(json.dumps({'artRevision':'V2','asset':'Assets/Textures/HomeFurniture.png','size':[512,512],'opaque':True,'sha256':manifest['textureSha256'],'regions':regions},indent=2),encoding='utf-8')
    return manifest
