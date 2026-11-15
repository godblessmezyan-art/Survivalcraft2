"""Compact house in viewer coordinates; export to the target game's right-handed coordinates."""
import argparse
from collections import Counter, deque
import hashlib
import json
from pathlib import Path

PORT=Path(__file__).resolve().parents[1];ROOT=PORT.parent
NATIVE=json.loads((ROOT/'docs/blueprint-block-capabilities.json').read_text(encoding='utf-8-sig'))
DETAIL=json.loads((PORT/'docs/architecture-block-capabilities.json').read_text(encoding='utf-8-sig'))
CAPS={b['type']:(b['family'],b['allowedData']) for b in NATIVE['blocks']}
CAPS.update({b['key']:('detail',b['allowedData']) for b in DETAIL['blocks']})
HOME={'BookshelfBlock':3,'BookPileBlock':4,'HomeSofaBlock':3,'HomeCoffeeTableBlock':1}
for key,n in HOME.items():CAPS[key]=('home',[(v<<3)|r for v in range(n) for r in range(4) if key!='HomeSofaBlock' or v in (0,2)])
NAME='云崖书屋·紧凑版 V2'


def mirror_data(f,data):
    if f=='detail':return ((data&~3)|((-data)&3))^4
    if f=='home':return (data&~3)|((-data)&3)
    if f=='door':return ((data&~3)|((-data)&3))^8
    if f=='stairs':return (data&~3)|(((1-data) if data&24 else -data)&3)
    if f in ('cube','log','slab','grass'):return data
    raise ValueError(('unreviewed reflection',f,data))


class House:
    def __init__(self):self.cells={};self.groups={};self.group='floor'
    def put(self,u,y,v,key,data=0,empty=False):
        f,allowed=CAPS[key];assert data in allowed,(key,data)
        p=(u,y,v)
        if empty and p in self.cells:return
        self.cells[p]=(key,f,data);self.groups[p]=self.group
    def box(self,u0,u1,y0,y1,v0,v1,key,data=0,empty=False):
        for y in range(y0,y1+1):
            for v in range(v0,v1+1):
                for u in range(u0,u1+1):self.put(u,y,v,key,data,empty)
    def clear(self,u0,u1,y0,y1,v0,v1):
        for p in list(self.cells):
            u,y,v=p
            if u0<=u<=u1 and y0<=y<=y1 and v0<=v<=v1:self.cells.pop(p);self.groups.pop(p)
    def detail(self,u,y,v,part,variant=0,rotation=0):self.put(u,y,v,'Ancient'+part+'Block',(variant<<3)|rotation)
    def log(self,u0,u1,y0,y1,v0,v1):self.box(u0,u1,y0,y1,v0,v1,'OakWoodBlock',2 if u0!=u1 else 1 if v0!=v1 else 0)


def build():
    h=House()
    h.box(2,17,-1,-1,6,14,'StoneBrickBlock')
    h.box(2,7,0,0,7,14,'PlanksBlock');h.box(8,17,0,0,6,14,'PlanksBlock')
    # A short exterior threshold, no stairs, mountains, trees or tall stone basement.
    h.box(1,1,0,0,5,11,'StoneBrickBlock');h.box(2,12,0,0,5,5,'StoneBrickBlock')
    h.group='tea_room'
    h.box(2,2,1,5,7,14,'ClayBlock',1);h.box(3,7,1,5,7,7,'ClayBlock',1)
    h.box(2,7,1,5,14,14,'ClayBlock',1)
    h.group='main_room'
    h.box(8,8,1,5,6,14,'ClayBlock',1);h.box(8,17,1,5,14,14,'ClayBlock',1)
    h.box(8,17,5,5,6,6,'PlanksBlock',25)
    h.box(17,17,5,6,6,14,'PlanksBlock',25)
    for u,v in ((2,7),(2,14),(8,6),(8,14),(17,6),(17,14)):
        h.detail(u,1,v,'Timber',0);h.log(u,u,2,5,v,v)
    h.group='glass'
    # The main front is glass throughout, with no front door.
    for u in range(9,17):
        for y in range(1,5):
            if u in (9,16) or y in (1,4):h.detail(u,y,6,'Window',0)
            else:h.put(u,y,6,'GlassBlock')
    for v in range(7,14):
        for y in range(1,5):
            if v in (7,13) or y in (1,4):h.detail(17,y,v,'Window',0,1)
            else:h.put(17,y,v,'GlassBlock')
    h.group='moon_window'
    for u,y,variant in ((4,3,2),(5,3,3),(4,2,4),(5,2,5)):h.detail(u,y,7,'Window',variant)
    h.group='doors'
    # One internal wooden door. The tea-room side entry is the user's future stair connection.
    for u in (2,8):
        h.clear(u,u,1,2,10,10);h.put(u,1,10,'WoodenDoorBlock',1)
    h.group='furniture'
    # Actual single-cell custom shelves; no synthetic book-spine cubes or hidden occupancy IDs.
    for u in range(10,16):
        for y in range(1,4):h.put(u,y,13,'BookshelfBlock',8 if u%2 else 16)
    for u in (3,4):
        for y in (1,2):h.put(u,y,13,'BookshelfBlock',8)
    for u in (11,13):h.put(u,1,10,'HomeSofaBlock')
    h.put(12,1,8,'HomeCoffeeTableBlock')
    h.put(15,1,11,'BookPileBlock',24);h.put(10,1,12,'BookPileBlock',8)
    h.detail(5,1,11,'Tea',0)
    for u,v in ((4,11),(6,11),(5,9)):h.detail(u,1,v,'Tea',1)
    h.group='platform'
    h.box(13,20,0,0,3,5,'PlanksBlock');h.box(18,20,0,0,3,14,'PlanksBlock')
    h.log(13,20,-1,-1,3,3);h.log(20,20,-1,-1,4,14)
    for u in range(13,21):h.detail(u,1,3,'Timber',4)
    for v in range(4,15):h.detail(20,1,v,'Timber',4,1)
    for u in range(18,21):h.detail(u,1,14,'Timber',4)
    for v in (4,13):
        h.log(17,17,-3,-1,v,v)
        for i in range(3):h.detail(18+i,-3+i,v,'Timber',3,1)
    h.detail(19,1,9,'Telescope',0,1)
    h.group='lights'
    for u,v in ((3,9),(5,12),(10,8),(15,12)):h.detail(u,4,v,'Lantern',0)
    for u,v in ((13,3),(20,3),(20,14)):h.detail(u,2,v,'Lantern',2)
    h.group='roof'
    for u in range(7,19):
        yy=12-abs(u-12)
        for v in range(4,17):
            h.put(u,yy-1,v,'PlanksBlock',25)
            h.detail(u,yy,v,'Roof',1 if u==12 else 5 if v in (4,16) else 0,0 if u==12 else 1 if u<12 else 3)
        for v in (6,14):
            for y in range(6,yy-1):h.put(u,y,v,'ClayBlock',1)
    for v in range(4,17):
        h.detail(6,6,v,'Roof',3,3);h.detail(19,6,v,'Roof',3,1)
    for v,r in ((3,2),(17,0)):h.detail(12,12,v,'Roof',3,r)
    h.group='tea_roof'
    for u in range(1,8):
        yy=5+u//3
        for v in range(6,16):
            h.put(u,yy-1,v,'PlanksBlock',25,empty=True)
            h.put(u,yy,v,'PlanksBlock' if u%3==2 else 'WoodenSlabBlock',25 if u%3==2 else 50)
    h.group='brackets'
    for v in (6,9,12,15):h.detail(18,5,v,'Timber',2)
    return h


def write(h,folder,*,name=NAME,filename='yunya-compact-v2.json',additional_landmarks=None,floor_levels=(0,),scope=None,glass_side='-X'):
    folder.mkdir(parents=True,exist_ok=True)
    lo=tuple(min(p[a] for p in h.cells) for a in range(3));hi=tuple(max(p[a] for p in h.cells) for a in range(3))
    size=tuple(hi[a]-lo[a]+1 for a in range(3))
    def point(p):return [hi[0]-p[0],p[1]-lo[1],p[2]-lo[2]]
    cells=[];groups={}
    for p,(key,f,data) in h.cells.items():
        x,y,z=point(p);d=mirror_data(f,data);c={'x':x,'y':y,'z':z,'key':key}
        if f!='cube':c['f']=f
        if d:c['d']=d
        cells.append(c);groups[f'{x},{y},{z}']=h.groups[p]
    cells.sort(key=lambda c:(c['y'],c['z'],c['x']))
    bp={'schemaVersion':3,'kind':'builderassistant.blueprint','name':name,'cells':cells}
    raw=(json.dumps(bp,ensure_ascii=False,separators=(',',':'))+'\n').encode('utf-8')
    assert len(cells)<10000 and max(size)<64 and len(raw)<2*1024**2
    (folder/filename).write_bytes(raw)
    landmarks={k:point(p) for k,p in {'tea':(5,0,10),'main':(12,0,10),'frontPlatform':(16,0,4),'rightPlatform':(19,0,11),'internalDoor':(8,1,10),'sideEntry':(2,1,10)}.items()}
    landmarks.update({k:point(p) for k,p in (additional_landmarks or {}).items()})
    assert landmarks['tea'][0]>landmarks['main'][0]>landmarks['rightPlatform'][0]
    deck={(c['x'],c['z']) for c in cells if c['y']==-lo[1] and groups[f"{c['x']},{c['y']},{c['z']}"]=='platform'}
    found={next(iter(deck))};q=list(found)
    while q:
        x,z=q.pop()
        for p in ((x-1,z),(x+1,z),(x,z-1),(x,z+1)):
            if p in deck and p not in found:found.add(p);q.append(p)
    assert found==deck
    floor_y=-lo[1];by_position={(c['x'],c['y'],c['z']):c for c in cells}
    def clear_for_walk(p):return p not in by_position or by_position[p].get('f')=='door'
    walk=set()
    for c in cells:
        x,y,z=c['x'],c['y'],c['z']
        floor=(c['key'] in ('PlanksBlock','StoneBrickBlock') and y+lo[1] in floor_levels) or c.get('f')=='stairs'
        if floor and all(clear_for_walk((x,y+dy,z)) for dy in (1,2)):walk.add((x,y,z))
    start=point((1,0,10));visited={tuple(start)};todo=list(visited)
    assert next(iter(visited)) in walk
    while todo:
        x,y,z=todo.pop()
        for dx,dz in ((-1,0),(1,0),(0,-1),(0,1)):
            for dy in (-1,0,1):
                p=(x+dx,y+dy,z+dz)
                if p in walk and p not in visited:visited.add(p);todo.append(p)
    targets={'tea','main','frontPlatform','rightPlatform'}|set(additional_landmarks or {})
    reachable={key:tuple(p) in visited for key,p in landmarks.items() if key in targets}
    assert all(reachable.values()),reachable
    metadata={'name':name,'minModVersion':'0.90.0','size':dict(zip('xyz',size)),'cells':len(cells),'sha256':hashlib.sha256(raw).hexdigest(),
      'sourceFile':filename,'mainFloorY':-lo[1],'facade':'-Z','glassSide':glass_side,'designOrigin':lo,'cellGroups':groups,
      'landmarks':landmarks,'platformConnected':True,'teaFrontRecess':1,'rearWallsAligned':True,
      'circulation':{'method':'two clear cells over cube/stair floor; cardinal steps with at most one-cell rise; wooden doors considered open','reachable':reachable},
      'materials':dict(Counter(c['key'] for c in cells)),'groups':dict(Counter(groups.values())),
      'coordinateConvention':'Viewer-right U maps to game -X. For a camera outside -Z, native view-right is -X. Blender maps game(x,y,z) to (x,-z,y), determinant +1.',
      'scope':scope or 'Building and connected platforms only; no stairs, trees, mountain, cellar or attic; tea-side entry reserved for user stairs.',
      'furniturePolicy':'Actual single-cell custom Bookshelf, BookPile, Sofa and CoffeeTable; tall/multicell/storage furniture remains excluded.'}
    (folder/'manifest.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({k:v for k,v in metadata.items() if k not in ('cellGroups','groups','materials')},ensure_ascii=True,indent=2))


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--output',type=Path,default=ROOT/'artifacts/yunya-compact-v2')
    a=p.parse_args();write(build(),a.output)
