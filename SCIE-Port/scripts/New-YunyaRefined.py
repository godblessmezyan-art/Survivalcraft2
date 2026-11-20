"""Independent SCIE-Port whole-house blueprint, using only audited native/detail cells."""
import argparse
from collections import Counter, deque
import hashlib
import json
import math
from pathlib import Path

PORT = Path(__file__).resolve().parents[1]
ROOT = PORT.parent
NATIVE = json.loads((ROOT/'docs/blueprint-block-capabilities.json').read_text(encoding='utf-8-sig'))
DETAIL = json.loads((PORT/'docs/architecture-block-capabilities.json').read_text(encoding='utf-8-sig'))
CAPS = {b['type']:b for b in NATIVE['blocks']}
CAPS.update({b['key']:{'family':'detail','allowedData':b['allowedData']} for b in DETAIL['blocks']})
NAME = '云崖书屋·整屋精修 V1'


class House:
    def __init__(self):
        self.cells = {}
        self.groups = {}
        self.group = 'structure'

    def put(self, x, y, z, key, data=0, empty=False):
        assert all(type(v) is int for v in (x,y,z))
        assert key in CAPS and data in CAPS[key]['allowedData'], (key,data)
        point = (x,y,z)
        if empty and point in self.cells:
            return
        self.cells[point] = (key,CAPS[key]['family'],data)
        self.groups[point] = self.group

    def box(self, x0,x1,y0,y1,z0,z1,key,data=0,empty=False):
        for y in range(y0,y1+1):
            for z in range(z0,z1+1):
                for x in range(x0,x1+1):
                    self.put(x,y,z,key,data,empty)

    def clear(self, x0,x1,y0,y1,z0,z1):
        for point in list(self.cells):
            x,y,z=point
            if x0<=x<=x1 and y0<=y<=y1 and z0<=z<=z1:
                del self.cells[point]
                self.groups.pop(point,None)

    def detail(self,x,y,z,key,variant=0,rotation=0,mirror=False,empty=False):
        self.put(x,y,z,'Ancient'+key+'Block',(variant<<3)|rotation|(4 if mirror else 0),empty)

    def log(self,x0,x1,y0,y1,z0,z1):
        self.box(x0,x1,y0,y1,z0,z1,'OakWoodBlock',2 if x0!=x1 else 1 if z0!=z1 else 0)

    def slab(self,x,y,z,key='WoodenSlabBlock',top=False,brown=False):
        self.put(x,y,z,key,int(top)+(50 if brown else 0))

    def shelf(self,x0,x1,bottom,top,z):
        for x in range(x0,x1+1):
            for y in range(bottom,top+1):
                if x in (x0,x1) or (x-x0)%4==0:
                    self.put(x,y,z,'SpruceWoodBlock')
                elif (y-bottom)%2==0:
                    self.slab(x,y,z,top=True,brown=True)
                else:
                    self.put(x,y,z,'PlanksBlock',1+2*[14,13,11,4,9][(x+2*y+z)%5])


def build():
    h=House()
    h.group='stone_archive'
    # A hollow, usable stone lower storey; the underside and buttresses remain visible.
    for y in range(0,12):
        for z in range(9,25):
            for x in range(8,25):
                if x in (8,24) or z in (9,24):
                    h.put(x,y,z,'CobblestoneBlock' if (x*17+z*13+y*7)%13==0 else 'StoneBrickBlock')
    h.box(9,23,2,2,10,23,'StoneBrickBlock')
    for x in (8,16,24):
        h.box(x,x,1,11,8,8,'StoneBrickBlock')
        for y in (4,8,11):
            h.box(x-1,x+1,y,y,8,8,'StoneBrickBlock')
    for z in (11,18,23):
        h.box(25,25,1,11,z,z,'StoneBrickBlock')
        for y in (4,8,11):
            h.box(25,25,y,y,z-1,z+1,'StoneBrickBlock')
    for x in (11,20):
        h.clear(x,x+1,5,7,8,9)
        for xx in range(x,x+2):
            for y in range(5,8):
                h.detail(xx,y,9,'Window',1)
            h.put(xx,6,10,'WickerLampBlock')
            h.slab(xx,4,8,'StoneBrickSlabBlock',top=True)
    for z in (14,21):
        h.clear(24,25,5,7,z,z+1)
        for zz in range(z,z+2):
            for y in range(5,8):
                h.detail(24,y,zz,'Window',1,1)
            h.put(23,6,zz,'WickerLampBlock')
    h.shelf(17,22,3,7,22)

    h.group='main_floor'
    h.box(8,24,11,11,4,24,'StoneBrickBlock')
    h.clear(9,23,11,11,10,23)
    h.box(9,23,12,12,5,24,'PlanksBlock')
    h.box(8,8,12,12,4,24,'StoneBrickBlock')
    h.box(9,24,12,12,4,4,'StoneBrickBlock')
    # Two-wide lower staircase, with real headroom through the main floor.
    h.group='archive_stair'
    h.clear(10,11,11,12,18,21)
    for i in range(10):
        for x in (10,11):
            h.put(x,3+i,11+i,'WoodenStairsBlock',2)
            h.put(x,2+i,11+i,'PlanksBlock')
    h.box(9,9,13,13,18,21,'WoodenFenceBlock')

    h.group='timber_plaster'
    h.box(9,9,13,19,9,24,'ClayBlock',1)
    h.box(9,24,13,19,24,24,'ClayBlock',1)
    h.box(18,24,13,19,9,9,'ClayBlock',1)
    for x,z in ((9,9),(18,9),(24,9),(9,24),(24,24)):
        h.detail(x,13,z,'Timber',0)
        h.log(x,x,14,18,z,z)
    for y in (19,):
        h.log(9,24,y,y,9,9);h.log(9,24,y,y,24,24)
        h.log(9,9,y,y,9,24);h.log(24,24,y,y,9,24)
    # The main gable is visible above a lower, independent veranda roof.
    for z in (9,24):
        for x in range(9,25):
            for y in range(20,28-abs(x-16)):
                h.put(x,y,z,'ClayBlock',1)
        h.log(16,16,20,27,z,z)
    for x in range(14,19):
        for y in (23,24):
            h.detail(x,y,9,'Window',1)
        h.put(x,23,10,'WickerLampBlock')
    h.log(13,19,25,25,9,9)
    # White moon-window bay: a real two-by-two circular aperture.
    h.group='moon_window'
    for x,y,v in ((20,15,2),(21,15,3),(20,14,4),(21,14,5)):
        h.detail(x,y,9,'Window',v)
    h.shelf(19,22,13,16,11)

    h.group='glass_library'
    # Thin real mesh window panels, not opaque textures painted onto a wall.
    for z in range(10,24):
        for y in range(13,19):
            if z in (10,14,18,23) or y in (13,18):h.detail(24,y,z,'Window',0,1)
            else:h.put(24,y,z,'GlassBlock')
    # Two-wide walk-through exit to the terrace.
    h.clear(24,24,13,15,21,22)
    # A west annex doorway and inferred rear windows remain usable.
    h.clear(9,9,13,15,12,14)
    for z in (17,21):
        for y in (14,15):h.detail(9,y,z,'Window',1,1)
    for x in (11,21):
        for y in (14,15):h.detail(x,y,24,'Window',1)

    h.group='library_furniture'
    h.shelf(12,22,13,17,23)
    h.shelf(12,16,13,16,16)
    for z in (12,17):
        h.detail(22,13,z,'Tea',0)
        h.detail(21,13,z,'Tea',1)
        h.detail(22,18,z,'Lantern',0)
    h.detail(22,14,20,'Lantern',2)
    h.group='attic'
    h.box(13,20,20,20,18,23,'PlanksBlock')
    for i in range(8):
        for x in (17,18):
            h.put(x,13+i,10+i,'WoodenStairsBlock',2)
            h.put(x,12+i,10+i,'PlanksBlock')
    for x in range(13,17):h.detail(x,21,18,'Timber',4)
    h.shelf(14,19,21,23,22)
    h.detail(19,21,20,'Tea',0)
    h.detail(18,21,20,'Tea',1)
    h.detail(16,25,20,'Lantern',0)

    h.group='west_annex'
    h.box(1,8,12,12,9,20,'PlanksBlock')
    h.box(1,8,11,11,9,9,'StoneBrickBlock')
    h.box(1,1,11,11,9,20,'StoneBrickBlock')
    h.box(1,8,13,17,9,9,'ClayBlock',1)
    h.box(1,8,13,17,20,20,'ClayBlock',1)
    h.box(1,1,13,17,10,19,'ClayBlock',1)
    h.clear(3,5,13,15,9,9)
    for x,z in ((1,9),(8,9),(1,20),(8,20)):
        h.log(x,x,13,17,z,z)
    for y in (14,15):h.detail(2,y,9,'Window',1)
    for z in (13,17):
        for y in (14,15):h.detail(1,y,z,'Window',1,1)
    h.shelf(2,6,13,16,19)
    h.detail(4,13,16,'Tea',0);h.detail(3,13,16,'Tea',1)
    h.detail(2,16,10,'Lantern',1)
    # Annex is an enclosed wing with a distinct half-roof, not an extended eave.
    for x in range(0,9):
        y=17+x//2
        for z in range(7,23):
            h.put(x,y-1,z,'PlanksBlock',25,empty=True)
            h.slab(x,y,z,top=bool(x%2),brown=True)
    for x in range(0,9):h.detail(x,17+x//2+1,7,'Roof',2)

    h.group='tea_veranda'
    for x in range(10,15):h.detail(x,16,6,'Tea',3)
    h.detail(12,13,7,'Tea',0)
    for x,z in ((11,7),(13,7),(12,6),(12,8)):h.detail(x,13,z,'Tea',1)
    for x,z in ((9,6),(17,6)):h.detail(x,13,z,'Garden',2)
    for x,z in ((9,4),(18,4),(23,4)):h.detail(x,13,z,'Lantern',2)
    for x in range(19,24):h.detail(x,13,4,'Timber',4)
    h.group='veranda_roof'
    for z in range(4,9):
        for x in range(8,25):
            h.put(x,16+(z-4)//2,z,'PlanksBlock',25,empty=True)
            h.slab(x,17+(z-4)//2,z,top=bool(z%2),brown=True)
    for x in range(8,25):h.detail(x,18,3,'Roof',2)
    for x in range(9,25,3):h.detail(x,16,4,'Timber',2)

    h.group='terrace'
    h.box(24,31,12,12,10,24,'PlanksBlock')
    for z in (10,17,24):
        h.log(24,31,11,11,z,z)
        h.log(24,24,4,11,z,z)
        h.put(24,3,z,'StoneBrickBlock')
        for i in range(6):h.detail(25+i,5+i,z,'Timber',3,1)
    h.log(31,31,11,11,10,24)
    for z in range(10,25):h.detail(31,13,z,'Timber',4,1)
    for x in range(24,32):
        for z in (10,24):h.detail(x,13,z,'Timber',4)
    for z in (10,17,24):
        h.detail(31,13,z,'Timber',0);h.detail(31,14,z,'Lantern',2)
    h.detail(28,13,17,'Telescope',0,1)
    h.detail(26,13,22,'Garden',3)
    h.detail(26,13,12,'Tea',0)
    h.detail(27,13,12,'Tea',1)

    h.group='roof'
    # Native timber underlay closes the silhouette beneath thin overlapping tile modules.
    for x in range(6,27):
        y=28-abs(x-16)
        for z in range(6,27):
            h.put(x,y-1,z,'PlanksBlock',25)
            h.detail(x,y,z,'Roof',1 if x==16 else 0,0 if x==16 else 1 if x<16 else 3)
    for x,r in ((5,3),(27,1)):
        for z in range(6,27):h.detail(x,18,z,'Roof',3,r)
    for x,z,r in ((5,5,2),(27,5,1),(5,27,3),(27,27,0)):h.detail(x,18,z,'Roof',4,r)
    h.detail(16,28,5,'Roof',3,2);h.detail(16,28,27,'Roof',3,0)
    h.group='eave_brackets'
    for z in range(8,26,3):
        h.detail(25,18,z,'Timber',2)
        h.detail(26,17,z,'Lantern',0)

    h.group='approach'
    for i in range(12):
        z=-4+i;y=1+i
        for x in (3,4,5):
            h.put(x,y,z,'StoneBrickStairsBlock',2)
            h.box(x,x,max(0,y-2),y-1,z,z,'StoneBrickBlock')
        if i in (0,4,8):
            h.box(2,2,max(0,y-2),y+1,z,z,'StoneBrickBlock')
            h.detail(2,y+2,z,'Lantern',2)
    h.box(2,9,12,12,8,8,'StoneBrickBlock')
    # Cut back through the annex roof-free front gate after decorative passes.
    h.clear(3,5,13,15,8,9)

    h.group='garden_cliff'
    for z in range(-3,28):
        top=2+max(0,z+3)//3
        for x in range(-3,1):
            h.box(x,x,max(0,top-2),top-1,z,z,'CobblestoneBlock')
            h.put(x,top,z,'GrassBlock')
            if (x+2*z)%5==0:h.put(x,top+1,z,'PurpleFlowerBlock' if z%3==0 else 'WhiteFlowerBlock')
    for x,z,y in ((10,8,11),(18,8,10),(23,8,11),(25,15,10),(25,22,8)):
        for i in range(5):h.detail(x,y-i,z,'Garden',1,0 if z==8 else 1,empty=True)
    h.group='cherry'
    h.box(17,19,12,12,3,4,'StoneBrickBlock')
    h.box(18,18,13,16,3,3,'MimosaWoodBlock')
    h.box(17,19,16,16,3,3,'MimosaWoodBlock',2)
    for x in range(17,20):
        for y in range(17,20):
            for z in range(2,5):
                if abs(x-18)+abs(y-18)+abs(z-3)<=2:h.detail(x,y,z,'Garden',0)
    h.group='snow_pines'
    # The rear pine grows from a solid outcrop rather than floating past the terrace.
    for z in range(25,31):
        for x in range(24,30):
            if abs(x-26)+abs(z-27)>4:continue
            top=13-max(0,abs(x-26)+abs(z-27)-1)//2
            h.box(x,x,0,top-1,z,z,'CobblestoneBlock',empty=True)
            h.put(x,top,z,'GrassBlock',empty=True)
    for tx,tz,base,height in ((-2,24,11,22),(26,27,14,17)):
        h.box(tx,tx,base-2,base-1,tz,tz,'StoneBrickBlock',empty=True)
        h.box(tx,tx,base,base+height,tz,tz,'SpruceWoodBlock',empty=True)
        for offset,radius in ((height-2,2),(height-6,3),(height-10,4),(height-14,3)):
            yy=base+offset
            for dx in range(-radius,radius+1):
                for dz in range(-radius,radius+1):
                    if abs(dx)+abs(dz)<=radius+1 and (dx or dz):
                        h.put(tx+dx,yy,tz+dz,'SpruceLeavesBlock',empty=True)
                        if (dx*3+dz*5+yy)%4==0:h.slab(tx+dx,yy+1,tz+dz,'MarbleSlabBlock')
    return h


def circulation(h):
    # Conservative: only full blocks or stairs support a two-cell-clear walking position.
    nodes=set()
    for (x,y,z),(key,f,d) in h.cells.items():
        if f in ('cube','stairs','grass') and key not in ('GlassBlock','WickerLampBlock','SpruceLeavesBlock'):
            if (x,y+1,z) not in h.cells and (x,y+2,z) not in h.cells:nodes.add((x,y,z))
    start=(4,1,-4)
    assert start in nodes,'Approach entry blocked'
    found={start};todo=deque([start])
    while todo:
        x,y,z=todo.popleft()
        for dx,dz in ((1,0),(-1,0),(0,1),(0,-1)):
            for dy in (-1,0,1):
                p=(x+dx,y+dy,z+dz)
                if p in nodes and p not in found:found.add(p);todo.append(p)
    destinations={'westAnnex':(6,12,12),'teaVeranda':(15,12,7),'library':(20,12,18),'terrace':(28,12,20),'lowerArchive':(14,2,16),'attic':(16,20,19)}
    return {'method':'two clear whole cells above native cube/stair floor, cardinal steps with at most one-cell rise',
            'entry':start,'reachable':{k:p in found for k,p in destinations.items()},'visited':len(found),'destinations':destinations}


def write(h, output):
    output.mkdir(parents=True,exist_ok=True)
    lo=tuple(min(p[a] for p in h.cells) for a in range(3));hi=tuple(max(p[a] for p in h.cells) for a in range(3))
    size=tuple(hi[a]-lo[a]+1 for a in range(3))
    cells=[];groups={}
    for point,(key,f,data) in sorted(h.cells.items(),key=lambda item:(item[0][1],item[0][2],item[0][0])):
        x,y,z=(point[a]-lo[a] for a in range(3));cell={'x':x,'y':y,'z':z,'key':key}
        if f!='cube':cell['f']=f
        if data:cell['d']=data
        cells.append(cell);groups[f'{x},{y},{z}']=h.groups[point]
    assert len(cells)<=10000 and max(size)<=64,(len(cells),size)
    blueprint={'schemaVersion':3,'kind':'builderassistant.blueprint','name':NAME,'cells':cells}
    raw=(json.dumps(blueprint,ensure_ascii=False,separators=(',',':'))+'\n').encode('utf-8')
    assert len(raw)<=2*1024*1024
    target=output/'yunya-refined-v1.json';target.write_bytes(raw)
    circulation_report=circulation(h)
    assert all(circulation_report['reachable'].values()),circulation_report
    manifest={'name':NAME,'minModVersion':'0.89.0','size':dict(zip('xyz',size)),'cells':len(cells),'sha256':hashlib.sha256(raw).hexdigest(),
              'originUnnormalized':lo,'mainFloorY':12-lo[1],'facade':'-Z','glassSide':'+X','cellGroups':groups,
              'groups':dict(Counter(h.groups.values())),'materials':dict(Counter(c['key'] for c in cells)),
              'circulation':circulation_report,'sourcePolicy':'original single photo; unseen rear/interiors inferred; game geometry authoritative',
              'previewPolicy':'actual C# detail meshes; native block shape/material approximation; not a game screenshot'}
    (output/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (output/'circulation.json').write_text(json.dumps(circulation_report,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({k:v for k,v in manifest.items() if k not in ('cellGroups','groups','materials')},ensure_ascii=True,indent=2))
    return target,manifest


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--output',type=Path,default=ROOT/'artifacts/yunya-refined-v1')
    args=parser.parse_args();write(build(),args.output)
