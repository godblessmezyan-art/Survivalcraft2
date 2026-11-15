"""Approved V2 footprint, opaque right wall/door/bookshelves, and an internal loft."""
import argparse
import importlib.util
from pathlib import Path

base_path=Path(__file__).with_name('New-YunyaCompact.py')
spec=importlib.util.spec_from_file_location('yunya_compact',base_path)
base=importlib.util.module_from_spec(spec);spec.loader.exec_module(base)


def build():
    h=base.build()
    original_bounds=tuple((min(p[a] for p in h.cells),max(p[a] for p in h.cells)) for a in range(3))
    h.group='right_wall'
    h.box(17,17,1,5,7,13,'ClayBlock',1)
    h.group='right_door'
    h.clear(17,17,1,2,11,11);h.put(17,1,11,'WoodenDoorBlock',1)
    h.group='right_bookshelves'
    for v in (7,8,9):
        for y in (1,2,3):h.put(16,y,v,'BookshelfBlock',9)
    h.clear(15,15,1,1,11,11)
    h.put(15,1,12,'BookPileBlock',24)

    h.group='loft_floor'
    h.box(10,14,5,5,10,13,'PlanksBlock')
    h.clear(9,10,5,5,10,11)
    h.group='loft_beams'
    for v in (12,13):h.log(10,14,4,4,v,v)
    h.group='loft_stairs'
    # Five rises, two blocks wide. Leave a two-cell-high opening through the loft edge.
    for i in range(5):
        for u in (9,10):
            h.put(u,1+i,7+i,'WoodenStairsBlock',2)
            h.put(u,i,7+i,'PlanksBlock')
    h.group='loft_rail'
    for u in range(11,15):h.detail(u,6,10,'Timber',4)
    for v in (12,13):h.detail(10,6,v,'Timber',4,1)
    for v in (11,12,13):h.detail(14,6,v,'Timber',4,1)
    h.group='loft_furniture'
    for u in (11,12,13):
        for y in (6,7):h.put(u,y,13,'BookshelfBlock',8)
    h.put(13,6,11,'HomeCoffeeTableBlock')
    h.put(13,6,12,'HomeSofaBlock')
    h.group='loft_window'
    for u in (11,12,13):
        for y in (7,8):h.detail(u,y,6,'Window',1)
    h.group='loft_lights'
    h.log(11,13,10,10,11,11)
    h.detail(12,9,11,'Lantern',0)
    assert original_bounds==tuple((min(p[a] for p in h.cells),max(p[a] for p in h.cells)) for a in range(3))
    assert all(not (p[0]==17 and 7<=p[2]<=13 and (value[0]=='GlassBlock' or (value[0]=='AncientWindowBlock' and value[2]>>3==0))) for p,value in h.cells.items())
    return h


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--output',type=Path,default=base.ROOT/'artifacts/yunya-loft-v3')
    args=parser.parse_args()
    base.write(build(),args.output,name='云崖书屋·阁楼版 V3',filename='yunya-loft-v3.json',
        additional_landmarks={'loft':(11,5,11),'rightDoorAccess':(17,0,11)},floor_levels=(0,5),glass_side='none',
        scope='Same V2 building envelope; opaque right wall with platform door and custom shelves; rear loft and two-wide interior stairs; no exterior stairs or scenery.')
