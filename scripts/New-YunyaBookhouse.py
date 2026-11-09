"""Reconstruct the supplied 云崖书屋 reference as native schema-3 blueprint cells.

No game assets are copied. Coordinates are game X/Y/Z (Y up), facade faces -Z.
Run from any directory; the capability matrix is resolved beside this repository.
"""
import argparse
import hashlib
import json
import math
from collections import Counter, deque
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CAPS = json.loads((ROOT / 'docs/blueprint-block-capabilities.json').read_text(encoding='utf-8-sig'))
BLOCKS = {b['type']: b for b in CAPS['blocks']}
PALETTE = [(255,255,255),(181,255,255),(255,181,255),(160,181,255),
           (255,240,160),(181,255,181),(255,181,160),(181,181,181),
           (112,112,112),(32,112,112),(112,32,112),(26,52,128),
           (87,54,31),(24,116,24),(136,32,32),(24,24,24)]


class House:
    def __init__(self):
        self.cells = {}
        self.groups = {}
        self.group = 'structure'

    def set(self, x, y, z, key, d=0, empty=False):
        p = (x,y,z)
        assert all(isinstance(t, int) for t in p)
        cap = BLOCKS[key]
        assert d in cap['allowedData'], (key,d)
        if empty and p in self.cells:
            return
        self.cells[p] = (key,cap['family'],d)
        self.groups[p] = self.group

    def box(self, x0,x1,y0,y1,z0,z1,key,d=0,empty=False):
        for x in range(x0,x1+1):
            for y in range(y0,y1+1):
                for z in range(z0,z1+1):
                    self.set(x,y,z,key,d,empty)

    def remove(self,x0,x1,y0,y1,z0,z1):
        for p in list(self.cells):
            x,y,z=p
            if x0<=x<=x1 and y0<=y<=y1 and z0<=z<=z1:
                del self.cells[p]
                self.groups.pop(p)

    def log(self,x0,x1,y0,y1,z0,z1,key='OakWoodBlock'):
        axis = 2 if x1>x0 else (1 if z1>z0 else 0)
        self.box(x0,x1,y0,y1,z0,z1,key,axis)

    def slab(self,x,y,z,key='WoodenSlabBlock',top=False,color=None,empty=False):
        d=int(top)+(2+4*color if color is not None else 0)
        self.set(x,y,z,key,d,empty)

    def lantern(self,x,y,z,hanging=0):
        self.set(x,y,z,'WickerLampBlock')
        self.slab(x,y-1,z,color=12)
        self.slab(x,y+1,z,top=False,color=12)
        for i in range(2,hanging+2):
            self.set(x,y+i,z,'IronFenceBlock')

    def garden(self,x,z,y,flower=None):
        self.set(x,y,z,'GrassBlock')
        if flower:
            self.set(x,y+1,z,flower)

    def shelf(self,x0,x1,y0,y1,z):
        for x in range(x0,x1+1):
            for y in range(y0,y1+1):
                if x in (x0,x1) or (x-x0)%5==0:
                    self.set(x,y,z,'SpruceWoodBlock')
                elif (y-y0)%3==0:
                    self.slab(x,y,z,top=True,color=12)
                else:
                    color=[14,13,12,11,4,9,6][(x*5+((y-y0)//3)*3+z)%7]
                    self.set(x,y,z,'PlanksBlock',1+2*color)


def build():
    h=House()
    # Irregular, hollow cliff masonry. The underside remains visible when built
    # over an actual drop; this is an architectural pedestal, not solid terrain.
    h.group='cliff_masonry'
    for y in range(0,18):
        for x in range(12,39):
            for z in range(10,33):
                cut = (x>34 and z>29 and y<5) or (x<15 and z<13 and y<7)
                skin = x in (12,38) or z in (10,32)
                ledge = y in (7,16) and (x<=13 or z<=11 or x>=37 or z>=31)
                if not cut and (skin or ledge):
                    key='CobblestoneBlock' if (x*13+z*5+y*11)%17<3 else 'StoneBrickBlock'
                    h.set(x,y,z,key)
    # Strong front / east buttresses and uneven downward ends.
    for x in (13,25,37):
        h.box(x,x+1,2,17,9,10,'StoneBrickBlock')
        for y in (5,11,16):
            h.box(x-1,x+2,y,y,8,10,'StoneBrickBlock')
            for xx in range(x-1,x+3):
                h.slab(xx,y-1,8,'StoneBrickSlabBlock',top=True)
    for z in (12,23,31):
        h.box(38,39,1,17,z,z+1,'StoneBrickBlock')
        for y in (6,12,16):
            h.box(38,40,y,y,z-1,z+2,'StoneBrickBlock')
    # Lit lower archive windows, recessed behind a gridded front.
    h.group='lower_archive'
    for x in (19,31):
        h.remove(x,x+2,8,12,9,10)
        h.box(x,x+2,8,12,11,11,'WickerLampBlock')
        h.box(x,x+2,8,12,10,10,'WindowBlock')
        for xx in range(x-1,x+4):
            h.slab(xx,7,9,'StoneBrickSlabBlock',top=True)
        h.box(x-1,x+3,13,13,10,10,'StoneBrickBlock')
    for z in (17,27):
        h.remove(38,39,8,12,z,z+2)
        h.box(37,37,8,12,z,z+2,'WickerLampBlock')
        h.box(38,38,8,12,z,z+2,'WindowBlock')
    h.box(13,37,1,1,11,31,'StoneBrickBlock')
    h.remove(18,34,1,1,14,28)
    # Main deck and stone cornice.
    h.group='main_floor'
    h.box(11,38,17,17,5,33,'StoneBrickBlock')
    # Plank floor above spans the interior; retain only the visible stone fascia,
    # not an invisible second 525-cell solid floor.
    h.remove(13,37,17,17,11,31)
    h.box(12,38,18,18,6,32,'PlanksBlock')
    h.box(11,38,18,18,5,5,'StoneBrickBlock')
    h.box(11,11,18,18,5,33,'StoneBrickBlock')
    # A two-wide internal stair provides access to the lower archive.
    h.group='interior_stair'
    for i in range(16):
        z=14+i
        h.box(15,16,2+i,2+i,z,z,'WoodenStairsBlock',2)
        h.box(15,16,1+i,1+i,z,z,'PlanksBlock')
    h.remove(15,16,17,18,28,30)
    h.box(14,14,18,20,27,30,'WoodenFenceBlock')
    # Pale plaster and cedar timber frame, with an open tea veranda.
    h.group='plaster_and_frame'
    h.box(12,38,19,30,32,32,'ClayBlock',1)
    h.box(12,12,19,30,11,32,'ClayBlock',1)
    h.box(25,38,19,29,10,10,'ClayBlock',1)
    h.box(12,24,19,27,14,14,'PlanksBlock')
    for x,z in ((12,10),(25,10),(38,10),(12,32),(38,32),(38,21)):
        h.log(x,x,19,30,z,z)
    for y in (19,27,30):
        h.log(12,38,y,y,10,10)
        h.log(12,38,y,y,32,32)
        h.log(12,12,y,y,10,32)
        h.log(38,38,y,y,10,32)
    # Main gable plaster infill (roof is a 1:2 pitch).
    for z in (10,32):
        for x in range(12,39):
            top=39-abs(x-25)//2
            h.box(x,x,31,top,z,z,'ClayBlock',1)
        h.log(25,25,30,39,z,z)
        h.log(13,37,32,32,z,z)
    # Upper lantern window in the front gable.
    for x in range(22,29):
        for y in range(33,37):
            if x in (22,28) or y in (33,36):
                h.set(x,y,9,'PlanksBlock',25)
            else:
                h.set(x,y,10,'WickerLampBlock')
                h.set(x,y,9,'WindowBlock')
    # Moon-window: a real stepped circular opening, with books one cell behind it.
    h.group='moon_window'
    cx,cy=31,24
    for x in range(26,37):
        for y in range(19,30):
            rr=((x-cx)/4.5)**2+((y-cy)/4.5)**2
            if rr<0.76:
                h.remove(x,x,y,y,10,10)
            elif rr<1.2:
                h.set(x,y,10,'PlanksBlock',25)
                h.set(x,y,9,'PlanksBlock')
    h.shelf(27,35,19,27,12)
    # Glass library east facade, slim mullions, heavy wood corners/sill/header.
    h.group='glass_library'
    for z in range(12,32):
        for y in range(20,30):
            if z in (12,31):
                h.set(38,y,z,'OakWoodBlock')
            elif z in (18,25) or y==24:
                h.set(38,y,z,'IronFenceBlock',12 if y==24 else 0)
            else:
                h.set(38,y,z,'GlassBlock')
    h.log(38,38,19,19,11,32)
    h.log(38,38,30,30,11,32)
    # Two-cell walk-through opening from the library onto its balcony.
    h.remove(38,38,19,22,27,28)
    h.slab(38,18,27,top=True)
    h.slab(38,18,28,top=True)
    # Hidden west windows; all back-side details here are inferred.
    for z in (19,27):
        h.box(12,12,22,25,z,z+2,'WindowBlock')
    # Sweeping principal gable roof: alternating upper/lower half-slabs make
    # contiguous half-cell roof courses, and two projecting verge bands.
    h.group='main_roof'
    for x in range(7,45):
        distance=abs(x-25)
        surface=40.5-distance*.5
        if distance>=16:
            surface += .5*(distance-15)  # raised outer eave tips
        for z in range(6,37):
            yy=math.floor(surface-.5)
            top=surface-yy>0.75
            h.slab(x,yy,z,top=top,color=12)
            if z in (6,36):
                h.set(x,yy,z,'PlanksBlock',25)
                h.slab(x,yy+1,z,color=12)
    h.log(25,25,41,41,6,36,'SpruceWoodBlock')
    h.box(25,25,42,43,5,6,'PlanksBlock',25)
    h.box(25,25,42,43,36,37,'PlanksBlock',25)
    h.slab(25,44,5,color=12)
    h.slab(25,44,37,color=12)
    # Front lower veranda roof: broad second eave, connected to the gable.
    h.group='veranda_roof'
    for z in range(4,12):
        for x in range(8,39):
            surface=29+(z-4)*.28
            if x<10 or x>36:
                surface+=.5
            yy=math.floor(surface)
            h.slab(x,yy,z,top=(surface-yy>=.45),color=12)
            if z==4:
                h.set(x,yy,z,'PlanksBlock',25)
                h.slab(x,yy+1,z,color=12)
    # Exposed honey-coloured rafters, bracket sets and running eave fascia.
    h.group='eave_brackets'
    h.log(8,39,28,28,4,4)
    for x in range(10,39,3):
        h.log(x,x,27,27,4,7)
        h.set(x,26,7,'OakWoodBlock')
        h.slab(x,26,6,top=True)
        h.slab(x,27,4,top=True)
    for z in range(8,36,3):
        h.log(38,43,30,30,z,z)
        h.set(39,29,z,'OakWoodBlock')
        h.slab(40,29,z,top=True)
        h.slab(42,30,z,top=True)
    h.log(44,44,31,31,7,35,'SpruceWoodBlock')
    for z,dz in ((6,-1),(36,1)):
        for i in range(4):
            h.set(44,32+i//2,z+dz*i,'PlanksBlock',25)
            h.slab(44,33+i//2,z+dz*i,color=12)
    # Cantilevered east terrace with actual doubled knee braces.
    h.group='cantilever_terrace'
    h.box(39,49,18,18,12,34,'PlanksBlock')
    for z in (12,22,33):
        h.log(37,49,17,17,z,z)
        h.log(38,38,7,17,z,z)
        for i in range(9):
            h.set(39+i,9+i,z,'OakWoodBlock',2)
            if i<7:
                h.set(40+i,9+i,z,'OakWoodBlock',2)
        h.box(38,39,7,8,z,z,'StoneBrickBlock')
    h.log(49,49,17,17,12,34)
    # Fences self-connect in game. Posts receive cap slabs and warm lanterns.
    h.box(49,49,19,20,12,34,'WoodenFenceBlock',12)
    h.box(39,49,19,20,34,34,'WoodenFenceBlock',3)
    h.box(39,49,19,20,12,12,'WoodenFenceBlock',3)
    for z in (12,19,26,34):
        h.log(49,49,19,21,z,z)
        h.slab(49,22,z)
    for x in (39,44):
        h.log(x,x,19,21,34,34)
        h.slab(x,22,34)
    h.lantern(49,23,34)
    h.lantern(39,23,12,hanging=5)
    h.lantern(43,27,32,hanging=2)
    # A small block-built brass telescope: decorative, no custom entity.
    h.group='telescope'
    h.set(45,19,26,'WoodenFenceBlock')
    h.set(45,20,26,'WoodenFenceBlock')
    for dx,dz in ((-1,-1),(1,0),(0,1)):
        h.set(45+dx,19,26+dz,'WoodenFenceBlock')
    h.box(45,45,21,21,25,28,'CopperBlock')
    h.set(45,22,29,'CopperBlock')
    h.set(45,22,30,'CoalBlock')
    # Tea alcove with a suspended short noren and floor cushions.
    h.group='tea_veranda'
    for x in range(14,24):
        h.slab(x,25,8,'MarbleSlabBlock',top=True,color=0)
        if x%3:
            h.set(x,24,8,'ClayBlock',1)
    for x in (17,21):
        h.set(x,19,11,'WoodenFenceBlock')
    for x in range(16,23):
        h.slab(x,20,11,top=True)
    h.set(19,21,11,'CoalBlock')
    h.slab(18,21,11,'MarbleSlabBlock',color=0)
    h.slab(21,21,11,'MarbleSlabBlock',color=0)
    for x in (16,22):
        for z in (9,13):
            h.slab(x,19,z,'MarbleSlabBlock',color=0)
    h.lantern(23,21,12)
    h.lantern(13,20,5)
    h.lantern(37,20,5)
    h.box(25,37,19,19,5,5,'WoodenFenceBlock',3)
    # Custom shelves: books are coloured planks, not invented bookshelf blocks.
    h.group='library_furnishings'
    h.shelf(18,36,19,28,30)
    for z0 in (15,23):
        h.shelf(28,34,19,25,z0)
    for z in range(14,30):
        for y in range(19,27):
            if z in (14,19,24,29):
                h.set(14,y,z,'SpruceWoodBlock')
            elif (y-19)%3==0:
                h.slab(14,y,z,top=True,color=12)
            else:
                h.set(14,y,z,'PlanksBlock',1+2*[14,13,11,4][(y+z)%4])
    for z in range(17,27):
        h.slab(35,21,z,top=True)
        h.slab(36,21,z,top=True)
        if z in (17,22,26):
            h.set(35,20,z,'WoodenFenceBlock')
            h.set(36,20,z,'WoodenFenceBlock')
        if z%3==0:
            h.slab(34,19,z)
            h.set(33,20,z,'WoodenFenceBlock')
    h.slab(35,22,20,'MarbleSlabBlock',color=0)
    h.slab(35,22,24,'MarbleSlabBlock',color=0)
    for z in (16,23,29):
        h.lantern(34,27,z,hanging=3)
    h.lantern(36,22,27)
    # Approach climbs from a small garden landing to the tea veranda.
    h.group='approach_stairs'
    for i in range(18):
        z=i-8
        y=i+1
        # Long, straight ascent along the west/front edge, two cells of headroom.
        for xx in range(5,10):
            h.set(xx,y,z,'StoneBrickStairsBlock',2)
            h.set(xx,y-1,z,'StoneBrickBlock')
        if i%4==0:
            for xx in (4,10):
                h.box(xx,xx,max(0,y-3),y+1,z,z,'StoneBrickBlock')
    h.box(5,12,18,18,10,13,'StoneBrickBlock')
    # Connection from the top of the stair to the main porch, outside west wall.
    h.box(9,11,18,18,6,20,'StoneBrickBlock')
    h.lantern(4,4,-8)
    h.lantern(10,12,0)
    h.lantern(10,20,8)
    # A narrow planted rock flank, kept below budget instead of copying distant mountains.
    h.group='planted_cliff'
    for z in range(4,36):
        height=8+(z//5)*3
        for x in range(0,5):
            top=height+(4-x)//2
            for y in range(max(0,top-1),top+1):
                h.set(x,y,z,'GraniteBlock' if (x+y+z)%7==0 else 'CobblestoneBlock',empty=True)
            h.set(x,top+1,z,'GrassBlock',empty=True)
            if (x*5+z)%6==0:
                h.set(x,top+2,z,['WhiteFlowerBlock','PurpleFlowerBlock','TallGrassBlock'][(z+x)%3],empty=True)
    # Ivy represented by spruce-leaf cascades anchored to the stone, not an unsupported IvyBlock.
    h.group='cascading_greenery'
    for x,z,top,length in ((16,8,17,10),(27,8,8,6),(35,9,17,9),(40,23,15,9),(40,30,10,7)):
        for k in range(length):
            h.set(x+(k//3)%2,top-k,z,'SpruceLeavesBlock',empty=True)
            if k%3==0:
                h.set(x+1,top-k,z,'SpruceLeavesBlock',empty=True)
    # Planters with native mature flowers and decorative segmented bamboo.
    h.group='planters'
    for x,z in ((12,7),(26,7),(11,2),(3,3),(4,22)):
        h.set(x,18 if z<10 and x>10 else 7,z,'StoneBrickBlock',empty=True)
    for x in (11,12):
        h.set(x,19,12,'GrassBlock')
        h.box(x,x,20,24,12,12,'WoodenFenceBlock',16+32*13)
    h.box(2,4,0,0,-7,-4,'CobblestoneBlock')
    for x in range(2,5):
        for z in range(-7,-3):
            h.garden(x,z,1,'WhiteFlowerBlock' if (x+z)%3 else 'PurpleFlowerBlock')
    # Cherry branch next to the moon window: rounded pink slab clusters rather
    # than importing an unsupported cherry-leaf block or using a texture plane.
    h.group='cherry_tree'
    h.box(25,26,18,19,3,4,'StoneBrickBlock')
    h.set(25,20,3,'GrassBlock')
    for y in range(21,27):
        h.set(25+(y>23),y,3,'MimosaWoodBlock')
    for x,y,z in ((24,24,3),(26,25,3),(27,24,4),(24,26,4)):
        h.set(x,y,z,'MimosaWoodBlock',2)
        for dx in range(-2,3):
            for dz in range(-1,2):
                if dx*dx+dz*dz<=4:
                    h.slab(x+dx,y+1,z+dz,'MarbleSlabBlock',color=2,empty=True)
                    if (dx+dz)%2==0:
                        h.slab(x+dx,y+2,z+dz,'MarbleSlabBlock',color=2,empty=True)
    # Distinct west annex requested by the user: a complete room with a lower
    # single-slope / half-roof, not merely the main veranda's extended eave.
    h.group='west_annex'
    h.remove(0,11,19,30,9,25)
    h.box(1,11,18,18,10,24,'PlanksBlock')
    for x in range(1,12):
        h.set(x,17,10,'StoneBrickBlock')
        h.set(x,17,24,'StoneBrickBlock')
    h.box(1,1,17,17,10,24,'StoneBrickBlock')
    h.box(1,11,19,25,10,10,'ClayBlock',1)
    h.box(1,11,19,25,24,24,'ClayBlock',1)
    h.box(1,1,19,25,11,23,'ClayBlock',1)
    for x,z in ((1,10),(11,10),(1,24),(11,24)):
        h.log(x,x,19,26,z,z)
    h.remove(5,9,19,23,10,10)
    h.box(2,3,21,23,10,10,'WindowBlock')
    h.log(1,11,25,25,10,10)
    h.log(1,11,25,25,24,24)
    for x in range(1,12):
        for z in (10,24):
            h.box(x,x,26,math.floor(25.5+x*.25),z,z,'ClayBlock',1)
    for x in range(0,15):
        surface=26.5+x*.25
        yy=math.floor(surface-.5)
        for z in range(7,27):
            h.slab(x,yy,z,top=surface-yy>.75,color=12)
            if z in (7,26):
                h.set(x,yy,z,'PlanksBlock',25)
    h.log(0,0,25,25,8,25)
    for z in range(9,25,4):
        h.log(0,3,24,24,z,z)
    # Passage into the main tea/library building, beneath its retained beams.
    h.remove(12,12,19,23,16,18)
    h.shelf(2,10,19,24,23)
    h.lantern(3,23,14)
    h.slab(4,20,18,top=True)
    h.slab(5,20,18,top=True)
    h.set(4,19,18,'WoodenFenceBlock')
    # Snow-tipped spruce backdrop behind the left roof line.
    h.group='spruce_backdrop'
    for tx,tz,base,height in ((3,29,24,22),):
        h.log(tx,tx,base,base+height,tz,tz,'SpruceWoodBlock')
        for yy,r in ((base+height-3,3),(base+height-7,4),(base+height-11,5),(base+height-15,4)):
            for dx in range(-r,r+1):
                for dz in range(-r,r+1):
                    if abs(dx)+abs(dz)<=r+1 and not (dx==dz==0):
                        h.set(tx+dx,yy,tz+dz,'SpruceLeavesBlock',empty=True)
                        if (dx*7+dz*11+yy)%4==0:
                            # Snow needs an opaque support; leaf tops do not qualify.
                            # A white lower half slab keeps the snow cap decorative/stable.
                            h.slab(tx+dx,yy+1,tz+dz,'MarbleSlabBlock',color=0,empty=True)
    return h


def write_house(h, output):
    output.mkdir(parents=True,exist_ok=True)
    mins=tuple(min(p[i] for p in h.cells) for i in range(3))
    maxima=tuple(max(p[i] for p in h.cells) for i in range(3))
    size=tuple(maxima[i]-mins[i]+1 for i in range(3))
    cells=[]
    groups={}
    for (x,y,z),(key,f,d) in sorted(h.cells.items(),key=lambda t:(t[0][1],t[0][2],t[0][0])):
        p=(x-mins[0],y-mins[1],z-mins[2])
        cell=dict(zip(('x','y','z'),p));cell['key']=key
        if f!='cube':cell['f']=f
        if d:cell['d']=d
        cells.append(cell)
        groups[','.join(map(str,p))]=h.groups[(x,y,z)]
    assert len(cells)<=CAPS['maxBlocksPerOperation'], len(cells)
    assert max(size)<=CAPS['maxAxisLength'],size
    data={'schemaVersion':3,'kind':'builderassistant.blueprint','name':'云崖书屋','cells':cells}
    encoded=json.dumps(data,ensure_ascii=False,separators=(',',':'))+'\n'
    assert len(encoded.encode('utf-16-le'))//2<=CAPS['maxImportChars']
    file=output/'云崖书屋.json';file.write_text(encoded,encoding='utf-8',newline='\n')
    # Conservative circulation: two fully empty cells above a complete floor or
    # an upper slab. Do not count walking through furniture/fences as a route.
    walk=set()
    for (x,y,z),(key,f,d) in h.cells.items():
        if y==18 and (key in ('PlanksBlock','StoneBrickBlock') or (f=='slab' and d&1)):
            if (x,19,z) not in h.cells and (x,20,z) not in h.cells:
                walk.add((x,z))
    points={'westAnnex':(7,11),'library':(26,18),'balcony':(45,20),'teaVeranda':(20,9)}
    reached={points['westAnnex']};pending=deque(reached)
    while pending:
        x,z=pending.popleft()
        for p in ((x-1,z),(x+1,z),(x,z-1),(x,z+1)):
            if p in walk and p not in reached:
                reached.add(p);pending.append(p)
    circulation={'reachable':{name:p in reached for name,p in points.items()},
                 'reachableFloorCells':len(reached),'method':'Two empty cells over full floor / upper slab at floor Y18'}
    assert all(circulation['reachable'].values()),circulation
    (output/'circulation.json').write_text(json.dumps(circulation,indent=2),encoding='utf-8')
    manifest={'name':'云崖书屋','schemaVersion':3,'size':dict(zip(('x','y','z'),size)),
              'cells':len(cells),'bytes':file.stat().st_size,'sha256':hashlib.sha256(file.read_bytes()).hexdigest(),
              'coordinateShift':mins,'floorY':18-mins[1],'facade':'-Z','glassSide':'+X',
              'materials':dict(Counter(c['key'] for c in cells)),
              'groups':dict(Counter(h.groups.values())),
              'circulation':circulation,
              'palette':PALETTE,'cellGroups':groups,
              'inferred':['背立面及西立面','内部动线与藏书陈列','崖台下层空间及内部楼梯'],
              'adaptations':['樱花使用粉色大理石半砖','竹子使用绿色木栅栏','书籍使用染色木板','望远镜为铜块装饰'],
              'preview':'Reads the final JSON. Materials are procedural approximations, not game screenshots.'}
    (output/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in manifest.items() if k not in ('cellGroups','palette','materials')},ensure_ascii=False,indent=2))


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--output',type=Path,default=ROOT/'artifacts/yunya-bookhouse')
    args=parser.parse_args()
    write_house(build(),args.output)
