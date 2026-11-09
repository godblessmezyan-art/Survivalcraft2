"""Blender review renderer. Geometry comes exclusively from blueprint JSON.

Run with Blender --background --python this.py -- --source DIR [--draft].
Procedural review textures are original approximations; no game resources used.
"""
import argparse
import hashlib
import json
import math
import sys
from collections import defaultdict
from pathlib import Path

import bpy
from mathutils import Vector

parser=argparse.ArgumentParser()
parser.add_argument('--source',type=Path,required=True)
parser.add_argument('--draft',action='store_true')
parser.add_argument('--view',default='hero',choices=['hero','front','east','back','interior'])
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
folder=args.source.resolve()
bp=json.loads((folder/'云崖书屋.json').read_text(encoding='utf-8'))
manifest=json.loads((folder/'manifest.json').read_text(encoding='utf-8'))
cells={(c['x'],c['y'],c['z']):c for c in bp['cells']}
palette=manifest['palette']

# Operate in a dedicated new scene; preserve any existing live user's scene.
scene=bpy.data.scenes.new('YunyaBlueprintReview')
if bpy.context.window:
    bpy.context.window.scene=scene
collection=bpy.data.collections.new('Yunya_ActualBlueprint')
scene.collection.children.link(collection)
world=bpy.data.worlds.new('Yunya_NeutralWorld');world.use_nodes=True
world.node_tree.nodes['Background'].inputs['Color'].default_value=(.30,.39,.55,1)
world.node_tree.nodes['Background'].inputs['Strength'].default_value=.55
scene.world=world


def srgb(c):
    c=c/255
    return c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4


def material(key,d=0):
    name=f'MAT-Yunya-{key}-{d}'
    if name in bpy.data.materials:return bpy.data.materials[name]
    mat=bpy.data.materials.new(name);mat.use_nodes=True
    nodes=mat.node_tree.nodes;links=mat.node_tree.links
    p=nodes.get('Principled BSDF');p.inputs['Roughness'].default_value=.82
    color=(140,135,128)
    if key in ('PlanksBlock','WoodenSlabBlock','WoodenStairsBlock','WoodenFenceBlock'):
        color=(160,101,49)
    elif key in ('OakWoodBlock','MimosaWoodBlock'):color=(135,80,35)
    elif key=='SpruceWoodBlock':color=(78,46,28)
    elif key=='BirchWoodBlock':color=(208,191,151)
    elif 'StoneBrick' in key:color=(148,146,141)
    elif 'Cobblestone' in key:color=(126,133,127)
    elif 'Granite' in key:color=(122,123,122)
    elif 'Marble' in key:color=(235,230,219)
    elif key=='ClayBlock':color=(226,211,182)
    elif key=='GrassBlock':color=(85,124,39)
    elif 'Leaves' in key:color=(37,77,34)
    elif key=='CopperBlock':color=(176,111,49)
    elif key=='CoalBlock':color=(26,27,24)
    elif key=='IronFenceBlock':color=(39,37,32)
    elif key=='WickerLampBlock':color=(255,205,98)
    elif key=='PurpleFlowerBlock':color=(145,96,196)
    elif key=='WhiteFlowerBlock':color=(238,237,201)
    elif key=='TallGrassBlock':color=(115,146,53)
    # Use verified target WorldPalette.DefaultColors, including paint-presence flag.
    paint=None
    if key in ('PlanksBlock','ClayBlock','StoneBrickBlock','CobblestoneBlock','MarbleBlock','GraniteBlock') and d&1:
        paint=(d>>1)&15
    elif 'Slab' in key and d&2:paint=(d>>2)&15
    elif 'Stairs' in key and d&32:paint=(d>>6)&15
    elif 'Fence' in key and d&16:paint=(d>>5)&15
    if paint is not None:
        pc=palette[paint]
        # Painted material keeps subtle surface variation, while its diffuse tone
        # follows the game's palette. Brown roof is deliberately dark cedar.
        if paint==12 and ('Wooden' in key or key=='PlanksBlock'):color=(78,44,25)
        else:color=tuple(int(t*.88) for t in pc[:3])
    p.inputs['Base Color'].default_value=(*map(srgb,color),1)
    if key=='GlassBlock':
        # Both outer faces of real glass cubes are present. Transparent + weak
        # glossy reflection previews their open view without bending the room.
        nodes.clear();out=nodes.new('ShaderNodeOutputMaterial')
        transparent=nodes.new('ShaderNodeBsdfTransparent')
        glossy=nodes.new('ShaderNodeBsdfGlossy');glossy.inputs['Color'].default_value=(.65,.82,.90,1)
        glossy.inputs['Roughness'].default_value=.09
        mix=nodes.new('ShaderNodeMixShader');mix.inputs[0].default_value=.12
        links.new(transparent.outputs[0],mix.inputs[1]);links.new(glossy.outputs[0],mix.inputs[2]);links.new(mix.outputs[0],out.inputs[0])
        return mat
    if key=='WickerLampBlock':
        p.inputs['Emission Color'].default_value=(1,.37,.045,1)
        p.inputs['Emission Strength'].default_value=2
    if key=='CopperBlock':p.inputs['Metallic'].default_value=1;p.inputs['Roughness'].default_value=.4
    tex=nodes.new('ShaderNodeTexNoise');tex.inputs['Scale'].default_value=8
    tex.inputs['Detail'].default_value=2;tex.inputs['Roughness'].default_value=.7
    coord=nodes.new('ShaderNodeTexCoord')
    links.new(coord.outputs['UV'],tex.inputs['Vector'])
    ramp=nodes.new('ShaderNodeValToRGB')
    rgb=list(map(srgb,color))
    ramp.color_ramp.elements[0].color=(*(v*.72 for v in rgb),1)
    ramp.color_ramp.elements[1].color=(*(min(v*1.1,1) for v in rgb),1)
    links.new(tex.outputs['Fac'],ramp.inputs[0]);links.new(ramp.outputs['Color'],p.inputs['Base Color'])
    if 'StoneBrick' in key or 'Planks' in key or 'WoodenSlab' in key or key=='WindowBlock' or key=='WickerLampBlock':
        brick=nodes.new('ShaderNodeTexBrick');links.new(coord.outputs['UV'],brick.inputs['Vector'])
        brick.inputs['Scale'].default_value=2
        brick.inputs['Brick Width'].default_value=1 if 'Stone' in key else 2
        brick.inputs['Row Height'].default_value=.5 if 'Stone' in key else .25
        brick.inputs['Mortar Size'].default_value=.014 if 'Stone' in key else .008
        brick.inputs['Mortar Smooth'].default_value=0
        brick.inputs['Color1'].default_value=(*(v*.93 for v in rgb),1)
        brick.inputs['Color2'].default_value=(*(v*.78 for v in rgb),1)
        brick.inputs['Mortar'].default_value=(*(v*.38 for v in rgb),1)
        if key in ('WindowBlock','WickerLampBlock'):
            brick.inputs['Scale'].default_value=3
            brick.inputs['Brick Width'].default_value=.8
            brick.inputs['Row Height'].default_value=.8
            brick.inputs['Mortar Size'].default_value=.06
            brick.offset=0
            brick.inputs['Color1'].default_value=(1,.56,.16,1)
            brick.inputs['Color2'].default_value=(1,.56,.16,1)
            brick.inputs['Mortar'].default_value=(.04,.023,.008,1)
            links.new(brick.outputs['Color'],p.inputs['Emission Color'])
            p.inputs['Emission Strength'].default_value=1.5
        links.new(brick.outputs['Color'],p.inputs['Base Color'])
        bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.2;bump.inputs['Distance'].default_value=.02
        links.new(brick.outputs['Fac'],bump.inputs['Height']);links.new(bump.outputs[0],p.inputs['Normal'])
    return mat


buckets=defaultdict(lambda: [[],[],[]])
faces=[(0,3,2,1),(4,5,6,7),(0,1,5,4),(3,7,6,2),(0,4,7,3),(1,2,6,5)]
neighbor=[(0,-1,0),(0,1,0),(0,0,-1),(0,0,1),(-1,0,0),(1,0,0)]


def box(c,bounds=(0,0,0,1,1,1),full=False):
    key,d=c['key'],c.get('d',0)
    category=manifest['cellGroups'].get(f"{c['x']},{c['y']},{c['z']}",'structure')
    if args.view=='interior' and category in ('main_roof','veranda_roof','eave_brackets','spruce_backdrop'):
        return
    # Roof can be hidden in the saved .blend independently from the building.
    bucket=buckets[(key,d,category)]
    x,y,z=c['x'],c['y'],c['z'];x0,y0,z0,x1,y1,z1=bounds
    vv=[(x+x0,z+z0,y+y0),(x+x1,z+z0,y+y0),(x+x1,z+z1,y+y0),(x+x0,z+z1,y+y0),
        (x+x0,z+z0,y+y1),(x+x1,z+z0,y+y1),(x+x1,z+z1,y+y1),(x+x0,z+z1,y+y1)]
    # Game Y is Blender Z; adjust face mapping accordingly.
    face_neighbors=[(0,-1,0),(0,1,0),(0,0,-1),(0,0,1),(-1,0,0),(1,0,0)]
    for n,idx in enumerate(faces):
        dx,dy,dz=face_neighbors[n];other=cells.get((x+dx,y+dy,z+dz))
        if full and other and other.get('f','cube')=='cube':
            if key=='GlassBlock' and other['key']=='GlassBlock':continue
            if other['key']!='GlassBlock' and key!='GlassBlock':continue
        start=len(bucket[0]);bucket[0].extend(vv[i] for i in idx)
        bucket[1].append(tuple(range(start,start+4)))
        # UVs tile once per actual cell face; no camera-dependent decoration.
        bucket[2].extend(((0,0),(1,0),(1,1),(0,1)))


for c in bp['cells']:
    f=c.get('f','cube');d=c.get('d',0);key=c['key']
    if f=='slab':
        box(c,(0,.5 if d&1 else 0,0,1,1 if d&1 else .5,1))
    elif f=='stairs':
        r=d&3;upper=bool(d&4)
        box(c,(0,.5 if upper else 0,0,1,1 if upper else .5,1))
        bounds=[(0,0,0,1,1,.5),(0,0,0,.5,1,1),(0,0,.5,1,1,1),(.5,0,0,1,1,1)][r]
        b=list(bounds);b[1]=0 if upper else .5;b[4]=.5 if upper else 1
        box(c,b)
    elif f=='fence':
        box(c,(.375,0,.375,.625,1,.625))
        mask=d&15
        for bit,(dx,dz) in enumerate(((1,0),(-1,0),(0,1),(0,-1))):
            o=cells.get((c['x']+dx,c['y'],c['z']+dz))
            if o and (o.get('f','cube')=='fence' or (o.get('f','cube') in ('cube','log') and o['key']!='GlassBlock')):
                mask|=1<<bit
        for yy in (.28,.73):
            for bit,b in enumerate(((.5,yy,.44,1,yy+.12,.56),(0,yy,.44,.5,yy+.12,.56),(.44,yy,.5,.56,yy+.12,1),(.44,yy,0,.56,yy+.12,.5))):
                if mask&(1<<bit):box(c,b)
    elif f=='plant':
        # Approximate the native crossed plant mesh with compact stems/petals.
        box(c,(.45,0,.45,.55,.75,.55))
        if key.endswith('FlowerBlock'):
            box(c,(.16,.55,.33,.84,.71,.67));box(c,(.33,.55,.16,.67,.71,.84))
        else:
            box(c,(.20,.10,.47,.80,.60,.53));box(c,(.47,.10,.20,.53,.60,.80))
    else:
        box(c,full=True)

for (key,d,category),(verts,polys,uvs) in buckets.items():
    if not polys:continue
    mesh=bpy.data.meshes.new(f'MESH-{category}-{key}-{d}')
    mesh.from_pydata(verts,[],polys);mesh.update()
    uv=mesh.uv_layers.new(name='PerCellFaces')
    for i,v in enumerate(uvs):uv.data[i].uv=v
    obj=bpy.data.objects.new(f'GEO-{category}-{key}-{d}',mesh);collection.objects.link(obj)
    obj.data.materials.append(material(key,d))

# Actual lamp locations drive warm local fill; lights add no export geometry.
for i,c in enumerate(bp['cells']):
    if c['key']=='WickerLampBlock' and manifest['cellGroups'].get(f"{c['x']},{c['y']},{c['z']}") not in ('lower_archive',):
        data=bpy.data.lights.new(f'LIGHT-lamp-{i}','POINT');data.energy=450;data.color=(1,.48,.16);data.shadow_soft_size=.65
        o=bpy.data.objects.new(data.name,data);scene.collection.objects.link(o)
        o.location=(c['x']+.5,c['z']+.5,c['y']+.5)

sun=bpy.data.lights.new('LIGHT-late_afternoon','SUN');sun.energy=2.1;sun.angle=math.radians(12);sun.color=(1,.83,.65)
obj=bpy.data.objects.new(sun.name,sun);scene.collection.objects.link(obj);obj.rotation_euler=(math.radians(28),math.radians(-20),math.radians(-35))
area=bpy.data.lights.new('LIGHT-sky_fill','AREA');area.energy=20000;area.shape='DISK';area.size=50;area.color=(.65,.77,1)
obj=bpy.data.objects.new(area.name,area);scene.collection.objects.link(obj);obj.location=(65,-35,65)
obj.rotation_euler=(Vector((25,18,20))-obj.location).to_track_quat('-Z','Y').to_euler()
warm=bpy.data.lights.new('LIGHT-warm_facade','AREA');warm.energy=45000;warm.size=35;warm.color=(1,.76,.48)
obj=bpy.data.objects.new(warm.name,warm);scene.collection.objects.link(obj);obj.location=(75,-45,42)
obj.rotation_euler=(Vector((30,15,23))-obj.location).to_track_quat('-Z','Y').to_euler()

camera=bpy.data.cameras.new('CAM-Yunya');camera.type='ORTHO';camera.ortho_scale=74
cam=bpy.data.objects.new(camera.name,camera);scene.collection.objects.link(cam);scene.camera=cam
target=Vector((26,29,23))
locations={'hero':(130,-91,64),'front':(26,-131,30),'east':(170,29,33),'back':(-95,149,75),'interior':(105,-96,112)}
cam.location=locations[args.view];cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.engine='CYCLES';scene.cycles.samples=24 if args.draft else 64
scene.cycles.use_denoising=True;scene.cycles.max_bounces=6;scene.cycles.transparent_max_bounces=16
scene.render.resolution_x=1200 if args.draft else 1800
scene.render.resolution_y=1100 if args.draft else 1600
scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.film_transparent=False
scene.view_settings.view_transform='AgX'
scene.render.filepath=str(folder/f'{args.view}.png')
# Packed Blender file contains only this blueprint scene and original shaders.
blend_name='云崖书屋-preview.blend' if args.view=='hero' else f'云崖书屋-{args.view}.blend'
bpy.ops.wm.save_as_mainfile(filepath=str(folder/blend_name))
info={'blueprintSha256':hashlib.sha256((folder/'云崖书屋.json').read_bytes()).hexdigest(),
      'cells':len(cells),'blenderVersion':bpy.app.version_string,'view':args.view,
      'render':scene.render.filepath,'geometrySource':'final blueprint JSON only',
      'materials':'original procedural approximation; not game textures',
      'cameraLocation':list(cam.location),'cameraTarget':list(target),'orthographicScale':camera.ortho_scale}
(folder/f'{args.view}-render.json').write_text(json.dumps(info,ensure_ascii=False,indent=2),encoding='utf-8')
bpy.ops.render.render(write_still=True)
print('YUNYA_RENDER_COMPLETE',scene.render.filepath)
