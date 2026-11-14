"""Blender architectural review from final JSON and exported actual C# part meshes."""
import argparse
from collections import defaultdict
import hashlib
import json
import math
from pathlib import Path
import sys
import bpy
from mathutils import Vector

p=argparse.ArgumentParser();p.add_argument('--source',type=Path,required=True);p.add_argument('--parts',type=Path,required=True)
p.add_argument('--view',choices=['hero','front','east','interior'],default='hero');p.add_argument('--draft',action='store_true')
args=p.parse_args(sys.argv[sys.argv.index('--')+1:])
folder=args.source.resolve();source=folder/'yunya-refined-v1.json'
blueprint=json.loads(source.read_text(encoding='utf-8'));manifest=json.loads((folder/'manifest.json').read_text(encoding='utf-8'))
parts={(s['id'],s['data']>>3):s for s in json.loads(args.parts.read_text(encoding='utf-8-sig'))}
types=['Roof','Timber','Window','Lantern','Tea','Garden','Telescope']
part_ids={'Ancient'+name+'Block':978+i for i,name in enumerate(types)}
def visible(c):
    group=manifest['cellGroups'][f"{c['x']},{c['y']},{c['z']}"]
    return not (args.view=='interior' and (group in ('roof','veranda_roof','eave_brackets','snow_pines','attic') or (group=='west_annex' and c['y']>=17) or (group=='timber_plaster' and c['y']>=18)))
cells={(c['x'],c['y'],c['z']):c for c in blueprint['cells'] if visible(c)}
scene=bpy.data.scenes.new('YunyaRefined_'+args.view)
if bpy.context.window:bpy.context.window.scene=scene
world=bpy.data.worlds.new('YunyaRefined_Sky');world.use_nodes=True
world.node_tree.nodes['Background'].inputs['Color'].default_value=(.39,.48,.65,1)
world.node_tree.nodes['Background'].inputs['Strength'].default_value=.40
scene.world=world
collection=bpy.data.collections.new('YunyaRefined_FinalJSON');scene.collection.children.link(collection)


def linear(c):
    c=c/255
    return c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4


palette=[(255,255,255),(181,255,255),(255,181,255),(160,181,255),(255,240,160),(181,255,181),(255,181,160),(181,181,181),(112,112,112),(32,112,112),(112,32,112),(26,52,128),(87,54,31),(24,116,24),(136,32,32),(24,24,24)]


def color(key,d):
    c=(145,143,133)
    if key=='PlanksBlock' or key.startswith('Wooden'):c=(136,87,43)
    if key=='OakWoodBlock':c=(119,71,33)
    if key=='SpruceWoodBlock':c=(85,51,29)
    if key=='MimosaWoodBlock':c=(108,68,38)
    if key=='ClayBlock':c=(230,218,193)
    if key=='CobblestoneBlock':c=(126,132,128)
    if 'StoneBrick' in key:c=(159,158,149)
    if key=='GrassBlock':c=(93,134,49)
    if 'Leaves' in key:c=(44,89,43)
    if 'Marble' in key:c=(235,235,223)
    if key=='WhiteFlowerBlock':c=(248,237,191)
    if key=='PurpleFlowerBlock':c=(155,93,195)
    paint=None
    if key in ('PlanksBlock','ClayBlock','StoneBrickBlock','CobblestoneBlock','MarbleBlock','GraniteBlock') and d&1:paint=(d>>1)&15
    if 'Slab' in key and d&2:paint=(d>>2)&15
    if 'Stairs' in key and d&32:paint=(d>>6)&15
    if paint is not None:c=(79,44,27) if paint==12 and (key=='PlanksBlock' or key.startswith('Wooden')) else palette[paint]
    if key=='WickerLampBlock':c=(255,198,89)
    return c


materials={}
def material(key,d=0,vertex=False,glass=False):
    identity=(key,d,vertex,glass)
    if identity in materials:return materials[identity]
    mat=bpy.data.materials.new('Yunya_'+str(identity));mat.use_nodes=True
    nodes=mat.node_tree.nodes;links=mat.node_tree.links;shader=nodes.get('Principled BSDF')
    shader.inputs['Roughness'].default_value=.83
    if glass:
        nodes.clear();out=nodes.new('ShaderNodeOutputMaterial');transparent=nodes.new('ShaderNodeBsdfTransparent')
        glossy=nodes.new('ShaderNodeBsdfGlossy');glossy.inputs['Color'].default_value=(.58,.76,.84,1);glossy.inputs['Roughness'].default_value=.13
        mix=nodes.new('ShaderNodeMixShader');mix.inputs[0].default_value=.08
        links.new(transparent.outputs[0],mix.inputs[1]);links.new(glossy.outputs[0],mix.inputs[2]);links.new(mix.outputs[0],out.inputs['Surface'])
    elif vertex:
        attribute=nodes.new('ShaderNodeVertexColor');attribute.layer_name='GameVertexColor';links.new(attribute.outputs['Color'],shader.inputs['Base Color'])
        if key=='AncientLanternBlock':
            glow=nodes.new('ShaderNodeVertexColor');glow.layer_name='GameGlow';links.new(glow.outputs['Color'],shader.inputs['Emission Color']);shader.inputs['Emission Strength'].default_value=1.2
    else:
        rgb=tuple(map(linear,color(key,d)));shader.inputs['Base Color'].default_value=(*rgb,1)
        if key=='WickerLampBlock':shader.inputs['Emission Color'].default_value=(1,.45,.09,1);shader.inputs['Emission Strength'].default_value=1.8
        # These original review shaders approximate native stone/wood, not exported game textures.
        if key not in ('WhiteFlowerBlock','PurpleFlowerBlock','SpruceLeavesBlock'):
            tex=nodes.new('ShaderNodeTexNoise');tex.inputs['Scale'].default_value=11;tex.inputs['Detail'].default_value=1.2
            ramp=nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].color=(*(a*.78 for a in rgb),1);ramp.color_ramp.elements[1].color=(*(min(a*1.08,1) for a in rgb),1)
            links.new(tex.outputs['Fac'],ramp.inputs[0]);links.new(ramp.outputs['Color'],shader.inputs['Base Color'])
            if 'StoneBrick' in key:
                brick=nodes.new('ShaderNodeTexBrick');coord=nodes.new('ShaderNodeTexCoord');position=nodes.new('ShaderNodeSeparateXYZ');links.new(coord.outputs['Object'],position.inputs[0])
                geom=nodes.new('ShaderNodeNewGeometry');normal=nodes.new('ShaderNodeSeparateXYZ');links.new(geom.outputs['Normal'],normal.inputs[0])
                absolute=nodes.new('ShaderNodeMath');absolute.operation='ABSOLUTE';links.new(normal.outputs['X'],absolute.inputs[0])
                choice=nodes.new('ShaderNodeMix');choice.data_type='FLOAT';links.new(absolute.outputs[0],choice.inputs[0]);links.new(position.outputs['X'],choice.inputs[2]);links.new(position.outputs['Y'],choice.inputs[3])
                vector=nodes.new('ShaderNodeCombineXYZ');links.new(choice.outputs[0],vector.inputs['X']);links.new(position.outputs['Z'],vector.inputs['Y']);links.new(vector.outputs[0],brick.inputs['Vector'])
                brick.inputs['Scale'].default_value=1;brick.inputs['Brick Width'].default_value=1;brick.inputs['Row Height'].default_value=.5;brick.inputs['Mortar Size'].default_value=.018;brick.inputs['Color1'].default_value=(*rgb,1);brick.inputs['Color2'].default_value=(*(a*.86 for a in rgb),1);brick.inputs['Mortar'].default_value=(*(a*.42 for a in rgb),1)
                links.new(brick.outputs['Color'],shader.inputs['Base Color'])
    materials[identity]=mat;return mat


native=defaultdict(lambda:[[],[]]);detail_cache={};lamps=[];rendered=0
def game_point(v):return (v[0],v[2],v[1])


def cube(c,bounds=(0,0,0,1,1,1),full=False):
    x,y,z=c['x'],c['y'],c['z'];x0,y0,z0,x1,y1,z1=bounds
    points=[Vector(game_point((x+xx,y+yy,z+zz))) for xx,yy,zz in ((x0,y0,z0),(x1,y0,z0),(x1,y1,z0),(x0,y1,z0),(x0,y0,z1),(x1,y0,z1),(x1,y1,z1),(x0,y1,z1))]
    center=sum(points,Vector())/8;verts,polys=native[(c['key'],c.get('d',0))]
    faces=((4,5,6,7),(1,2,6,5),(0,1,2,3),(0,3,7,4),(3,2,6,7),(0,1,5,4));offsets=((0,0,1),(1,0,0),(0,0,-1),(-1,0,0),(0,1,0),(0,-1,0))
    for face,delta in zip(faces,offsets):
        other=cells.get((x+delta[0],y+delta[1],z+delta[2]))
        if full and other and other.get('f','cube') in ('cube','log','grass') and other['key'] not in ('GlassBlock','WickerLampBlock','SpruceLeavesBlock'):continue
        points4=[points[i] for i in face];normal=(points4[1]-points4[0]).cross(points4[2]-points4[0])
        if normal.dot(sum(points4,Vector())/4-center)<0:points4.reverse()
        base=len(verts);verts.extend(tuple(v) for v in points4);polys.append(tuple(range(base,base+4)))


def part_mesh(key,d,index):
    cache=(key,d,index)
    if cache in detail_cache:return detail_cache[cache]
    sample=parts[(part_ids[key],d>>3)]['parts'][index];vertices=[]
    for v in sample['vertices']:
        x,y,z=v[:3]
        if d&4:x=1-x
        for _ in range(d&3):x,z=z,1-x
        vertices.append((x,z,y))
    polys=[]
    for i in range(0,len(sample['indices']),3):
        a,b,c=sample['indices'][i:i+3];polys.append((a,c,b) if d&4 else (a,b,c))
    mesh=bpy.data.meshes.new('CSharp_'+key+'_'+str(d)+'_'+str(index));mesh.from_pydata(vertices,[],polys);mesh.update()
    attr=mesh.color_attributes.new(name='GameVertexColor',type='FLOAT_COLOR',domain='CORNER');glow=mesh.color_attributes.new(name='GameGlow',type='FLOAT_COLOR',domain='CORNER')
    for i,loop in enumerate(mesh.loops):
        v=sample['vertices'][loop.vertex_index];rgb=tuple(map(linear,v[3:6]));attr.data[i].color=(*rgb,v[6]/255)
        glow.data[i].color=(*rgb,1) if key=='AncientLanternBlock' and v[3]>240 and v[4]>160 else (0,0,0,1)
    mesh.materials.append(material(key,vertex=True,glass=index==1));detail_cache[cache]=mesh;return mesh


for c in blueprint['cells']:
    key=c['key'];d=c.get('d',0);f=c.get('f','cube');group=manifest['cellGroups'][f"{c['x']},{c['y']},{c['z']}"]
    if not visible(c):continue
    rendered+=1
    if f=='detail':
        for index in (0,1):
            if not parts[(part_ids[key],d>>3)]['parts'][index]['vertices']:continue
            obj=bpy.data.objects.new('GEO_'+group+'_'+str(rendered)+'_'+str(index),part_mesh(key,d,index));collection.objects.link(obj);obj.location=(c['x'],c['z'],c['y'])
        # Keep the review light outside the opaque luminous mesh so it can illuminate the room.
        if key=='AncientLanternBlock':lamps.append((c['x']+.5,c['z']+.5,c['y']+(.10 if d>>3 in (0,1) else .9)))
    elif f=='slab':cube(c,(0,.5 if d&1 else 0,0,1,1 if d&1 else .5,1))
    elif f=='stairs':
        cube(c,(0,0,0,1,.5,1));bounds=((0,.5,0,1,1,.5),(0,.5,0,.5,1,1),(0,.5,.5,1,1,1),(.5,.5,0,1,1,1))[d&3];cube(c,bounds)
    elif f=='fence':
        cube(c,(.38,0,.38,.62,1,.62));cube(c,(0,.65,.46,1,.79,.54))
    elif f=='plant':
        cube(c,(.46,0,.46,.54,.7,.54));cube(c,(.20,.6,.35,.80,.75,.65));cube(c,(.35,.6,.20,.65,.75,.80))
    else:
        cube(c,full=True)
        if key=='WickerLampBlock':lamps.append((c['x']+.5,c['z']+.5,c['y']+1.15))

for (key,d),(verts,polys) in native.items():
    if not polys:continue
    mesh=bpy.data.meshes.new('NativeReview_'+key+'_'+str(d));mesh.from_pydata(verts,[],polys);mesh.update()
    obj=bpy.data.objects.new('GEO_native_'+key+'_'+str(d),mesh);collection.objects.link(obj);mesh.materials.append(material(key,d,glass=key=='GlassBlock'))
for i,location in enumerate(lamps):
    data=bpy.data.lights.new('LGT_practical_'+str(i),'POINT');data.energy=280;data.color=(1,.53,.19);data.shadow_soft_size=.25
    obj=bpy.data.objects.new(data.name,data);collection.objects.link(obj);obj.location=location

target=Vector((20,17,16));sun=bpy.data.lights.new('LGT_sunset','SUN');sun.energy=2.4;sun.angle=math.radians(8);sun.color=(1,.83,.65)
obj=bpy.data.objects.new(sun.name,sun);collection.objects.link(obj);obj.rotation_euler=(.55,-.35,-.65)
for name,pos,energy,tint in [('key',(60,-45,55),24000,(1,.83,.63)),('fill',(-30,-8,38),7000,(.78,.86,1))]:
    data=bpy.data.lights.new('LGT_'+name,'AREA');data.energy=energy;data.size=30;data.color=tint
    obj=bpy.data.objects.new(data.name,data);collection.objects.link(obj);obj.location=pos;obj.rotation_euler=(target-obj.location).to_track_quat('-Z','Y').to_euler()
camdata=bpy.data.cameras.new('CAM_'+args.view);camdata.type='ORTHO';camdata.ortho_scale=54 if args.view=='hero' else 49
cam=bpy.data.objects.new(camdata.name,camdata);collection.objects.link(cam);scene.camera=cam
cam.location={'hero':(100,-85,53),'front':(20,-115,19),'east':(135,17,20),'interior':(90,-80,115)}[args.view]
cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.engine='CYCLES';scene.cycles.samples=16 if args.draft else 48;scene.cycles.use_denoising=True;scene.cycles.max_bounces=6;scene.cycles.transparent_max_bounces=20
scene.render.resolution_x=1000 if args.draft else 1800;scene.render.resolution_y=950 if args.draft else 1600;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG';scene.render.filepath=str(folder/(args.view+'.png'))
assert scene.camera is not None
bpy.ops.wm.save_as_mainfile(filepath=str(folder/('yunya-'+args.view+'.blend')))
report={'blueprintSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'blenderVersion':bpy.app.version_string,'renderedCells':rendered,'view':args.view,
        'source':'final JSON; C# detail meshes, approximate native block shapes/materials; no render-only geometry',
        'partsSha256':hashlib.sha256(args.parts.read_bytes()).hexdigest(),'camera':{'location':list(cam.location),'target':list(target),'orthoScale':camdata.ortho_scale}}
(folder/(args.view+'-render.json')).write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
bpy.ops.render.render(write_still=True,scene=scene.name)
