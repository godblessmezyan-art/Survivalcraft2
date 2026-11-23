"""Build the original Sunstep tomb props and the SCNET COLLADA/atlas assets."""
from pathlib import Path
from collections import defaultdict
import hashlib
import json
import math
import random
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'src/SCIEPort/Assets'
ATLAS_SIZE = 512
TILE = 128
PALETTE = [
    ('Limestone', '#93816A'), ('Gold', '#856945'), ('Lapis', '#3D5054'), ('Faience', '#567973'),
    ('Terracotta', '#825748'), ('Linen', '#9C8E71'), ('Basalt', '#4B4841'), ('Recess', '#342D26'),
    ('Alabaster', '#B6A791'), ('Papyrus', '#B6A078'), ('Flame', '#C76C2B'), ('RedGranite', '#66514A'),
    ('CoffinInlay', '#615347'), ('Cartouche', '#796A60'), ('Coals', '#402F2A'), ('PaintedBand', '#796450'),
]
STONE, GOLD, BLUE, JADE, CLAY, LINEN, BLACK, DARK, IVORY, PAPER, FIRE, GRANITE, INLAY, CARVED, COALS, BAND = range(16)


def write_atlas():
    atlas = Image.new('RGB', (ATLAS_SIZE, ATLAS_SIZE))
    rng = random.Random(371102)
    for index, (_, color) in enumerate(PALETTE):
        base = tuple(bytes.fromhex(color[1:]))
        surface = Image.new('RGB', (TILE, TILE))
        fleck = 14 if index in (STONE, BLACK, GRANITE, CARVED) else 7
        pixels = []
        for v in range(TILE):
            for u in range(TILE):
                grain = rng.triangular(-fleck, fleck)
                patina = 5 * math.sin(u * .069 + math.sin(v * .054)) + 4 * math.cos(v * .079 - u * .023)
                if index in (STONE, BLACK, GRANITE):
                    strata = math.sin(v*.19 + 1.8*math.sin(u*.056) + 1.3*math.sin(u*.23))
                    patina += 2.5 * strata - (4 if strata < -.88 else 0)
                if index == FIRE:
                    patina += 32 * (v / 127 - .4)
                pixels.append(tuple(max(0, min(255, round(c + grain + patina))) for c in base))
        surface.putdata(pixels)
        draw = ImageDraw.Draw(surface)
        if index in (PAPER, INLAY, CARVED, BAND):
            ink = {PAPER: '#6C4737', INLAY: '#3F372F', CARVED: '#4F433A', BAND: '#514A41'}[index]
            edge = '#8C7550' if index != PAPER else '#805A42'
            draw.rectangle((13, 13, 114, 114), outline=edge, width=3)
            draw.rectangle((18, 18, 109, 109), outline=ink, width=2)
            # Original pictographs with broad strokes that remain legible under mipmaps.
            rows = (34, 58, 82) if index != BAND else (48, 70)
            for row in rows:
                for col in (36, 62, 88):
                    kind = rng.randrange(4)
                    if kind == 0:
                        draw.line([(col-8,row+4), (col,row-5), (col+8,row+4), (col-8,row+4)], fill=ink, width=3)
                    elif kind == 1:
                        draw.ellipse((col-7, row-5, col+7, row+5), outline=ink, width=3)
                        draw.line((col, row-3, col, row+3), fill=ink, width=3)
                    elif kind == 2:
                        draw.line([(col-7,row+4),(col-4,row-3),(col,row+2),(col+6,row-5)], fill=ink, width=3)
                    else:
                        draw.arc((col-6,row-5,col+6,row+7), 0, 180, fill=ink, width=3)
                        draw.line((col,row, col,row+8), fill=ink, width=3)
        if index in (STONE, BLACK, GRANITE):
            for _ in range(54):
                u, v = rng.randrange(4, 120), rng.randrange(4, 120)
                stain = tuple(max(0, c-rng.randrange(5, 23)) for c in base)
                draw.point((u, v), fill=stain)
                if rng.random() < .35:
                    draw.line(((u, v), (u+2, v-1), (u+5, v)), fill=stain)
        atlas.paste(surface, ((index % 4) * TILE, (3 - index // 4) * TILE))
    path = ASSETS / 'Textures/PyramidDecor.png'
    atlas.convert('RGBA').save(path, compress_level=9)
    return path


def circle(sides):
    return [(math.cos(math.tau * i / sides), math.sin(math.tau * i / sides)) for i in range(sides)]


CHAMFER = [(1,-.79),(1,.79),(.79,1),(-.79,1),(-1,.79),(-1,-.79),(-.79,-1),(.79,-1)]
COFFIN = [(1,-.62),(.86,.42),(.66,.81),(.36,1),(-.36,1),(-.66,.81),(-.86,.42),(-1,-.62),(-.72,-1),(.72,-1)]


class Prop:
    def __init__(self):
        self.meshes = defaultdict(lambda: {'pos': [], 'nrm': [], 'uv': []})

    def face(self, tile, positions, uv, transform=None):
        if transform:
            positions = [transform(*point) for point in positions]
        for b in range(1, len(positions) - 1):
            points = [positions[i] for i in (0, b, b+1)]
            a, bpoint, c = points
            left = [bpoint[i] - a[i] for i in range(3)]
            right = [c[i] - a[i] for i in range(3)]
            normal = (left[1]*right[2]-left[2]*right[1], left[2]*right[0]-left[0]*right[2],
                      left[0]*right[1]-left[1]*right[0])
            length = math.sqrt(sum(v*v for v in normal))
            if length < 1e-10:
                continue
            mesh = self.meshes[tile]
            mesh['pos'].extend(points)
            mesh['nrm'].extend([tuple(v / length for v in normal)] * 3)
            for i in (0, b, b+1):
                u, v = uv[i]
                mesh['uv'].append(((tile % 4 + .055 + u*.89)/4, (tile//4 + .055 + v*.89)/4))

    def profile(self, rings, tiles, shape=None, sides=12, center=(0,0), transform=None, cap=None, strip=False):
        """Join (y, x-radius, z-radius[, x, z]) rings and cap both ends."""
        shape = circle(sides) if shape is None else shape
        points = []
        for y, rx, rz, *offset in rings:
            x, z = offset or center
            points.append([(x+u*rx,y,z+v*rz) for u,v in shape])
        for row, (a,b) in enumerate(zip(points, points[1:])):
            tile = tiles if isinstance(tiles, int) else tiles[row]
            for i in range(len(shape)):
                j = (i+1) % len(shape)
                lo, hi = (i/len(shape),(i+1)/len(shape)) if strip else (0,1)
                self.face(tile, [a[i],b[i],b[j],a[j]], [(lo,0),(lo,1),(hi,1),(hi,0)], transform)
        bottom, top = (tiles, tiles) if isinstance(tiles, int) else (tiles[0], tiles[-1])
        cap_uv = [(.5+u*.5,.5+v*.5) for u,v in shape]
        self.face(bottom, points[0], cap_uv, transform)
        self.face(top if cap is None else cap, points[-1][::-1], cap_uv[::-1], transform)

    def banded_box(self, x, y, z, rx, rz, h, tile, cap=None):
        bevel = min(.022, h*.25, rx*.18, rz*.18)
        self.profile([(y,rx-bevel,rz-bevel),(y+bevel,rx,rz),(y+h-bevel,rx,rz),(y+h,rx-bevel,rz-bevel)],
                     tile, CHAMFER, center=(x,z), cap=cap)

    def oval(self, x, y, z, rx, ry, rz, tile, sides=10):
        rings = [(y+ry*h, rx*w, rz*w) for h,w in [(-1,.10),(-.75,.66),(-.36,.93),(.15,1),(.65,.76),(1,.10)]]
        self.profile(rings,tile,sides=sides,center=(x,z))

    def rod(self, start, end, radius, tile, end_radius=None, sides=8):
        dx, dy, dz = (end[i]-start[i] for i in range(3))
        length = math.sqrt(dx*dx+dy*dy+dz*dz)
        axis = (dx/length,dy/length,dz/length)
        ref = (0,1,0) if abs(axis[1]) < .9 else (1,0,0)
        normal = (axis[1]*ref[2]-axis[2]*ref[1],axis[2]*ref[0]-axis[0]*ref[2],axis[0]*ref[1]-axis[1]*ref[0])
        norm = math.sqrt(sum(c*c for c in normal))
        u = tuple(c/norm for c in normal)
        v = (u[1]*axis[2]-u[2]*axis[1],u[2]*axis[0]-u[0]*axis[2],u[0]*axis[1]-u[1]*axis[0])
        def transform(x,y,z):
            return tuple(start[i]+u[i]*x+axis[i]*y+v[i]*z for i in range(3))
        tip = radius if end_radius is None else end_radius
        self.profile([(0,radius,radius),(length,tip,tip)], tile, sides=sides, transform=transform)

    def parts(self):
        return [(PALETTE[tile][0], mesh) for tile,mesh in sorted(self.meshes.items())]


def mask(prop,x,y,z,tile=IVORY):
    prop.oval(x,y,z,.067,.072,.051,tile)
    prop.oval(x,y-.008,z+.045,.016,.027,.026,tile,8)
    for side in (-1,1):
        prop.rod((x+side*.021,y+.011,z+.046),(x+side*.047,y+.017,z+.042),.006,BLACK)
    prop.rod((x-.021,y-.031,z+.045),(x+.021,y-.031,z+.045),.004,GRANITE)


def coffin(opened):
    p = Prop()
    x = -.16 if opened else 0
    p.profile([(0,.185,.407),(.031,.216,.431),(.17,.219,.429),(.195,.230,.438),(.222,.216,.422),
               (.222,.179,.383),(.082,.158,.344)], [STONE,STONE,STONE,GOLD,DARK,DARK], COFFIN,center=(x,0))
    def lid(x0,y,z):
        if not opened:
            return x0,y+.205,z
        angle = -1.09
        return .15+x0*math.cos(angle)-y*math.sin(angle), .182+x0*math.sin(angle)+y*math.cos(angle),z*.96
    p.profile([(0,.205,.419),(.027,.219,.429),(.066,.205,.414),(.092,.184,.391)],
              [BLUE,GOLD,BLUE],COFFIN,transform=lid,cap=INLAY)
    p.profile([(.089,.091,.114,0,.249),(.122,.084,.109,0,.249),(.136,.064,.086,0,.269)],
              [BLUE,GOLD],CHAMFER,transform=lid,cap=IVORY)
    p.profile([(.136,.018,.030,0,.267),(.156,.011,.021,0,.263)],IVORY,CHAMFER,transform=lid)
    for x0 in (-.034,.034):
        p.profile([(.136,.021,.007,x0,.293),(.140,.019,.005,x0,.294)],BLACK,CHAMFER,transform=lid)
    p.profile([(.136,.025,.006,0,.227),(.140,.022,.004,0,.227)],GRANITE,CHAMFER,transform=lid)
    for side in (-1,1):
        p.profile([(.09,.018,.20,side*.107,-.029),(.103,.016,.179,side*.107,-.027)],
                  GOLD, CHAMFER,transform=lid)
    if opened:
        p.oval(x,.107,.017,.109,.047,.271,LINEN,12)
        p.oval(x,.126,.273,.065,.052,.068,LINEN,10)
        for z in (-.16,-.07,.02,.11):
            p.banded_box(x,.124,z,.093,.015,.012,IVORY)
    return p


def canopic(kind):
    p = Prop()
    p.profile([(0,.116,.116),(.032,.143,.143),(.092,.153,.153),(.23,.211,.199),(.337,.204,.193),
               (.407,.157,.153),(.442,.146,.143),(.476,.151,.148)],
              [STONE,GOLD,IVORY,IVORY,BAND,IVORY,GOLD],sides=14,strip=True)
    p.profile([(.458,.117,.114),(.489,.139,.127),(.526,.121,.111)], [STONE,BLUE],sides=12)
    if kind == 'Human':
        p.oval(0,.614,-.013,.103,.112,.092,BLUE)
        mask(p,0,.607,.063)
        p.rod((0,.557,.098),(0,.527,.089),.015,GOLD,sides=6)
    elif kind == 'Baboon':
        p.oval(0,.615,-.011,.113,.105,.091,STONE)
        for side in (-1,1):
            p.oval(side*.094,.604,.034,.026,.037,.027,IVORY,8)
            p.oval(side*.04,.622,.069,.024,.022,.021,IVORY,8)
            p.oval(side*.04,.622,.088,.011,.011,.01,BLACK,8)
        p.oval(0,.567,.089,.068,.041,.062,GRANITE)
        p.rod((-.044,.558,.131),(.044,.558,.131),.005,DARK)
    elif kind == 'Jackal':
        jackal(p, .601, 0, .80, BLACK)
    else:
        p.oval(0,.617,-.004,.099,.112,.09,IVORY)
        p.oval(0,.666,-.026,.102,.069,.091,BLUE)
        for side in (-1,1):
            p.oval(side*.052,.62,.065,.022,.024,.025,GOLD,8)
            p.oval(side*.052,.622,.088,.012,.012,.010,BLACK,8)
        p.rod((0,.616,.074),(0,.595,.142),.035,GOLD,.020)
        p.rod((0,.595,.142),(0,.55,.122),.020,GOLD,.006)
    return p


def jackal(p,y,z,scale,tile):
    p.oval(0,y,z,.103*scale,.096*scale,.108*scale,tile)
    p.rod((0,y-.018*scale,z+.051*scale),(0,y-.032*scale,z+.218*scale),.073*scale,tile,.034*scale)
    p.oval(0,y-.029*scale,z+.214*scale,.037*scale,.025*scale,.021*scale,BLACK,8)
    for side in (-1,1):
        x = side*.066*scale
        p.profile([(y+.047*scale,.043*scale,.035*scale,x,z-.025*scale),
                   (y+.136*scale,.025*scale,.020*scale,x*1.17,z-.031*scale),
                   (y+.196*scale,.006*scale,.007*scale,x*1.25,z-.025*scale)],tile,CHAMFER)
        p.rod((x,y+.066*scale,z+.004*scale),(x*1.22,y+.151*scale,z-.012*scale),.011*scale,GOLD,.004*scale)
        p.oval(x,y+.016*scale,z+.087*scale,.016*scale,.012*scale,.013*scale,GOLD,8)


def storage_jar():
    p = Prop()
    p.profile([(0,.125,.124),(.036,.157,.143),(.101,.19,.171),(.214,.248,.216),(.338,.238,.207),
               (.417,.185,.163),(.474,.119,.115),(.566,.104,.101),(.582,.133,.120),(.614,.133,.12),
               (.614,.094,.088),(.455,.082,.078)], [CLAY,CLAY,CLAY,CLAY,BAND,CLAY,CLAY,CLAY,GRANITE,CLAY,DARK],
              sides=16,strip=True)
    for side in (-1,1):
        nodes = [(side*.105,.536,0),(side*.206,.515,0),(side*.261,.46,0),(side*.258,.398,0),(side*.213,.352,0)]
        for a,b in zip(nodes,nodes[1:]):
            p.rod(a,b,.023,CLAY)
    return p


def shabti():
    p = Prop()
    p.banded_box(0,0,0,.143,.112,.052,BLACK)
    p.profile([(.052,.093,.075),(.087,.102,.086),(.255,.086,.072),(.459,.119,.076),
               (.562,.145,.086),(.625,.101,.073),(.656,.074,.064)], [JADE,JADE,JADE,JADE,GOLD,JADE],CHAMFER,cap=JADE)
    p.oval(0,.722,-.017,.094,.108,.076,BLUE)
    mask(p,0,.715,.041,JADE)
    p.rod((-.087,.555,.072),(.078,.45,.083),.024,JADE)
    p.rod((.086,.55,.080),(-.073,.469,.102),.021,IVORY)
    p.rod((-.099,.445,.092),(-.059,.604,.10),.009,GOLD)
    p.rod((.1,.445,.09),(.068,.602,.10),.009,GOLD)
    return p


def anubis():
    p = Prop()
    p.banded_box(0,0,0,.258,.389,.065,BLACK)
    p.banded_box(0,.065,0,.231,.355,.024,GOLD)
    p.banded_box(0,.088,0,.219,.338,.042,BLUE)
    p.oval(0,.275,-.048,.14,.137,.259,BLACK,12)
    p.oval(0,.277,.126,.125,.139,.099,BLACK)
    for side in (-1,1):
        p.rod((side*.092,.187,.097),(side*.099,.167,.316),.044,BLACK,.031)
        p.oval(side*.099,.167,.294,.045,.029,.055,BLACK)
    p.profile([(.288,.102,.084,0,.143),(.378,.110,.092,0,.138),(.428,.086,.081,0,.135),
               (.492,.077,.073,0,.134)], [BLACK,GOLD,BLACK], sides=12)
    jackal(p,.519,.135,1,BLACK)
    p.rod((.023,.291,-.255),(.089,.17,-.315),.036,BLACK,.022)
    p.rod((.089,.17,-.315),(.178,.155,-.271),.022,BLACK,.013)
    return p


def offering():
    p = Prop()
    p.banded_box(0,0,0,.254,.185,.053,STONE)
    p.profile([(.053,.162,.111),(.091,.138,.091),(.246,.097,.073),(.285,.144,.111)],
              [STONE,STONE,BAND], CHAMFER)
    p.profile([(.277,.318,.218),(.317,.376,.251),(.369,.365,.247),(.369,.32,.205),(.332,.312,.198)],
              [STONE,STONE,GOLD,STONE],CHAMFER)
    for x,z,rx in [(-.173,.039,.081),(.013,.09,.078),(.087,-.073,.065)]:
        p.oval(x,.356,z,rx,.053,.049,PAPER,8)
    p.profile([(.332,.066,.064,.216,.048),(.407,.083,.081,.216,.048),(.421,.080,.078,.216,.048),
               (.421,.063,.062,.216,.048),(.387,.056,.055,.216,.048)], [CLAY,BLUE,CLAY,DARK])
    return p


def brazier(lit):
    p = Prop()
    p.banded_box(0,0,0,.222,.222,.055,BLACK)
    p.profile([(.052,.156,.156),(.103,.142,.142),(.263,.106,.106),(.297,.124,.124)], [GOLD,BLACK,GOLD],CHAMFER)
    p.profile([(.271,.139,.139),(.302,.209,.209),(.375,.284,.284),(.422,.293,.293),(.439,.286,.286),
               (.439,.253,.253),(.349,.181,.181)], [BLACK,BLACK,BLACK,GOLD,BLUE,DARK],sides=14)
    for x,z in [(-.09,-.06),(.07,-.083),(-.007,.095),(.129,.039)]:
        p.oval(x,.368,z,.082,.034,.054,COALS,8)
    if lit:
        for x,z,h,lean in [(-.069,-.011,.729,-.057),(.029,.045,.824,.044),(.086,-.054,.699,.077)]:
            p.profile([(.389,.076,.061,x,z),(.475,.085,.073,x,z),(.579,.050,.044,x+lean*.25,z),
                       (h-.042,.027,.026,x+lean*.66,z+.009),(h,.004,.004,x+lean,z+.028)],
                      [COALS,FIRE,FIRE,GOLD],sides=9)
    return p


def scroll():
    p = Prop()
    p.profile([(0,.315,.216),(.015,.342,.229),(.029,.321,.217)], [LINEN,PAPER], CHAMFER,cap=PAPER)
    for x in (-.301,.301):
        p.rod((x,.068,-.222),(x,.068,.222),.058,LINEN,sides=12)
        p.rod((x,.068,-.231),(x,.068,.231),.018,GRANITE,sides=10)
        for z in (-.164,.164):
            p.rod((x,.069,z-.012),(x,.069,z+.012),.059,GOLD,sides=12)
    return p


def obelisk():
    p = Prop()
    p.banded_box(0,0,0,.241,.241,.050,BLACK)
    p.banded_box(0,.05,0,.206,.206,.066,GRANITE)
    p.banded_box(0,.116,0,.175,.175,.045,STONE)
    p.profile([(.16,.140,.140),(.217,.139,.139),(.748,.089,.089),(.769,.093,.093)],
              [GRANITE,GRANITE,GOLD],CHAMFER)
    p.profile([(.767,.094,.094),(.908,.002,.002)],GOLD,CHAMFER)
    for side in (-1,1):
        def face(x,y,z):
            return (x,y,z*side)
        # Cap the carved plaque on both sides, with text mapped to a continuous field.
        p.banded_box(0,.177,side*.133,.079,.013,.028,GRANITE)
        vertices = [(-.063,.238,.140),(.063,.238,.140),(.055,.693,.098),(-.055,.693,.098)]
        uv = [(0,0),(1,0),(1,1),(0,1)]
        p.face(CARVED,vertices if side==1 else vertices[::-1],uv if side==1 else uv[::-1],face)
    return p


def front_panel(p, x, y, z, half_width, height, tile):
    p.face(tile, [(x-half_width,y,z),(x-half_width,y+height,z),
                  (x+half_width,y+height,z),(x+half_width,y,z)],
           [(0,0),(0,1),(1,1),(1,0)])


def royal_coffin(opened):
    p = Prop()
    center = (.50, 1.00)
    p.banded_box(.50, 0, 1.00, .86, 1.40, .22, GRANITE)
    p.profile([(.21,.72,1.26),(.33,.75,1.33),(.78,.70,1.30),(.92,.67,1.28),
               (.92,.54,1.17),(.47,.50,1.11)], [BLACK,BLACK,GRANITE,STONE,DARK],
              CHAMFER, center=center)
    # A short load-bearing ledge supports the displaced lid at the head of the coffin.
    p.banded_box(.50, .22, 2.20, .63, .25, .70, BLACK)
    lid_center = (.50, 1.28 if opened else .97)
    p.profile([(.91,.68,1.20),(1.03,.68,1.18),(1.21,.56,1.09),(1.27,.50,1.02)],
              [BLACK,GRANITE,BLACK], CHAMFER, center=lid_center)
    p.profile([(1.266,.30,.76),(1.283,.26,.71)], GRANITE, COFFIN, center=lid_center, cap=INLAY)
    p.profile([(1.277,.145,.166,.5,lid_center[1]+.57),(1.32,.123,.13,.5,lid_center[1]+.58)],
              GRANITE, CHAMFER, cap=CARVED)
    front_panel(p,.50,.43,-.298,.38,.29,INLAY)
    for tip in (-1,1):
        for z in (.13,.67,1.22,1.78):
            x = .50+tip*.709
            corner = [(x,.43,z-.16),(x,.76,z-.16),(x,.76,z+.16),(x,.43,z+.16)]
            p.face(CARVED, corner if tip>0 else corner[::-1], [(0,0),(0,1),(1,1),(1,0)])
    if opened:
        # The shroud just clears the exposed end beneath the displaced lid.
        p.oval(.50,.57,.73,.26,.12,.82,LINEN,12)
        p.oval(.50,.63,-.025,.17,.123,.175,LINEN)
    return p


def standing_guardian():
    p = Prop()
    p.banded_box(0,0,0,.42,.42,.19,GRANITE)
    p.banded_box(0,.19,0,.34,.35,.15,BLACK)
    for side in (-1,1):
        x = side*.12
        p.banded_box(x,.34,-.035,.105,.20,.15,BLACK)
        p.profile([(.40,.079,.081,x,0),(.79,.108,.103,x,.027),(1.17,.107,.112,x,.015)],
                  BLACK, CHAMFER)
        p.rod((side*.232,1.93,-.004),(side*.218,1.49,-.020),.073,BLACK,.057,6)
        p.rod((side*.218,1.51,-.020),(side*.186,1.19,-.091),.056,BLACK,.044,6)
        p.banded_box(side*.176,1.12,-.087,.049,.050,.14,GRANITE)
    p.profile([(1.03,.234,.17),(1.38,.199,.147),(1.58,.151,.121)],
              [BLACK,GRANITE],CHAMFER)
    p.profile([(1.48,.161,.130),(1.75,.218,.144),(1.96,.268,.137),(2.05,.153,.111),(2.17,.092,.088)],
              [BLACK,BLACK,BLACK,GOLD], CHAMFER)
    front_panel(p,0,1.13,-.166,.107,.24,BAND)
    # Mirror the canonical jackal head in the horizontal plane to face the entry.
    head = Prop()
    jackal(head,2.245,-.095,2.30,BLACK)
    for tile, mesh in head.meshes.items():
        for i in range(0,len(mesh['pos']),3):
            positions = [(x,y,-z) for x,y,z in mesh['pos'][i:i+3]][::-1]
            uv = [(4*u-tile%4-.055,4*v-tile//4-.055) for u,v in mesh['uv'][i:i+3]][::-1]
            p.face(tile, positions, [(u/.89,v/.89) for u,v in uv])
    return p


def ritual_brazier(lit):
    p = Prop()
    p.banded_box(0,0,0,.40,.40,.16,GRANITE)
    p.profile([(.15,.29,.29),(.23,.25,.25),(.64,.17,.17),(.73,.22,.22)],
              [BLACK,BLACK,GRANITE], CHAMFER)
    p.profile([(.69,.23,.23),(.78,.30,.30),(.96,.418,.418),(1.04,.427,.427),
               (1.07,.397,.397),(1.07,.347,.347),(.93,.258,.258)],
              [BLACK,BLACK,GRANITE,GOLD,BLACK,DARK], sides=14)
    for x,z in ((-.12,-.08),(.095,-.12),(-.04,.11),(.13,.08)):
        p.oval(x,.963,z,.10,.047,.072,COALS,8)
    if lit:
        for x,z,height in ((-.095,.034,1.17),(.09,-.064,1.235),(-.05,-.065,1.14)):
            p.profile([(.966,.071,.069,x,z),(1.074,.064,.061,x+.015,z),
                       (height,.006,.006,x+.058,z-.017)], [COALS,FIRE], sides=8)
    return p


def tomb_obelisk():
    p = Prop()
    p.banded_box(0,0,0,.43,.41,.21,BLACK)
    p.banded_box(0,.21,0,.32,.32,.20,GRANITE)
    p.profile([(.405,.235,.235),(.47,.231,.232),(2.18,.148,.149),(2.24,.15,.15)],
              [GRANITE,BLACK,GRANITE], CHAMFER)
    p.profile([(2.24,.152,.152),(2.58,.007,.007)], STONE, CHAMFER)
    p.face(CARVED,[(-.135,.60,-.228),(-.086,2.04,-.160),(.086,2.04,-.160),(.135,.60,-.228)],
           [(0,0),(0,1),(1,1),(1,0)])
    return p


MODELS = {
    'Sarcophagus': lambda: coffin(False), 'SarcophagusOpen': lambda: coffin(True),
    **{'Canopic'+name: (lambda kind=name: canopic(kind)) for name in ('Human','Baboon','Jackal','Falcon')},
    'StorageJar': storage_jar, 'Shabti': shabti, 'AnubisStatue': anubis,
    'OfferingTable': offering, 'Brazier': lambda: brazier(False), 'BrazierLit': lambda: brazier(True),
    'BookOfDead': scroll, 'Obelisk': obelisk,
}

MONUMENTS = {
    'RoyalCoffin': (lambda: royal_coffin(False), [2, 2, 3]),
    'RoyalCoffinOpen': (lambda: royal_coffin(True), [2, 2, 3]),
    'StoneGuardian': (standing_guardian, [1, 3, 1]),
    'RitualBrazier': (lambda: ritual_brazier(False), [1, 2, 1]),
    'RitualBrazierLit': (lambda: ritual_brazier(True), [1, 2, 1]),
    'TombObelisk': (tomb_obelisk, [1, 3, 1]),
}

NS = 'http://www.collada.org/2005/11/COLLADASchema'
ET.register_namespace('', NS)
def e(parent, tag, attrib=None, text=None):
    attrs = {k: str(v) for k, v in (attrib or {}).items()}
    child = ET.SubElement(parent, '{'+NS+'}'+tag, attrs)
    if text is not None:
        child.text = str(text)
    return child

def clip_triangle(points, axis, boundary, sign):
    clipped = []
    for start, end in zip(points[-1:] + points[:-1], points):
        a = sign * (start[axis] - boundary)
        b = sign * (end[axis] - boundary)
        if (a < 0) != (b < 0):
            t = a / (a - b)
            clipped.append(tuple(u + (v-u)*t for u,v in zip(start,end)))
        if b >= 0:
            clipped.append(end)
    return clipped


def split_monument(meshes, footprint):
    """Keep vertices within the cell used by SCNET for light sampling."""
    width, height, depth = footprint
    assert all(-.5 < x < width-.5 and 0 <= y <= height and -.5 < z < depth-.5
               for _,mesh in meshes for x,y,z in mesh['pos']), 'Monument exceeds authored footprint.'
    for y in range(height):
        for z in range(depth):
            for x in range(width):
                part = (y * depth + z) * width + x
                lower = (x-.5, y, z-.5)
                for material, mesh in meshes:
                    chunk = {'pos': [], 'nrm': [], 'uv': []}
                    for i in range(0,len(mesh['pos']),3):
                        polygon = [pos + uv for pos,uv in zip(mesh['pos'][i:i+3],mesh['uv'][i:i+3])]
                        for axis, boundary in enumerate(lower):
                            if polygon: polygon = clip_triangle(polygon,axis,boundary,1)
                            if polygon: polygon = clip_triangle(polygon,axis,boundary+1,-1)
                        for vertex in range(1,len(polygon)-1):
                            a,b,c = polygon[0],polygon[vertex],polygon[vertex+1]
                            u = [b[j]-a[j] for j in range(3)]
                            v = [c[j]-a[j] for j in range(3)]
                            cross = (u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0])
                            if sum(n*n for n in cross) < 1e-14:
                                continue
                            chunk['pos'].extend(p[:3] for p in (a,b,c))
                            chunk['uv'].extend(p[3:] for p in (a,b,c))
                            chunk['nrm'].extend([mesh['nrm'][i]] * 3)
                    if chunk['pos']:
                        yield material, chunk, part


def export_model(name, meshes, footprint=None):
    root = ET.Element('{'+NS+'}COLLADA', {'version':'1.4.1'})
    asset = e(root, 'asset'); e(asset, 'unit', {'name':'meter','meter':'1'}); e(asset, 'up_axis', text='Y_UP')
    lib = e(root, 'library_geometries')
    visual = e(root, 'library_visual_scenes')
    scene = e(visual, 'visual_scene', {'id':'Scene','name':'Scene'})
    node = e(scene, 'node', {'id':name,'name':name,'type':'NODE'})
    e(node, 'matrix', {'sid':'transform'}, '1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1')
    tris_total = 0
    bounds = []
    def flat(vectors):
        out = []
        for v in vectors:
            out.extend(v)
        return out
    sections = list(split_monument(meshes, footprint)) if footprint else [(role, mesh, None) for role, mesh in meshes]
    part_nodes = {}
    for idx, (role, p, part) in enumerate(sections):
        pos, nrm, uv = p['pos'], p['nrm'], p['uv']
        n = len(pos)
        assert n == len(nrm) and n == len(uv) and n % 3 == 0
        tris_total += n // 3
        bounds.extend(pos)
        ident = f'{name}_g{idx}'
        geom = e(lib, 'geometry', {'id':ident,'name':role})
        m = e(geom, 'mesh')
        for suffix, vectors, stride, params in [('p',pos,3,'XYZ'),('n',nrm,3,'XYZ'),('uv',uv,2,'ST')]:
            values = flat(vectors)
            source = e(m, 'source', {'id':ident+suffix})
            e(source, 'float_array', {'id':ident+suffix+'a','count':str(len(values))}, ' '.join(format(v,'.7g') for v in values))
            acc = e(e(source, 'technique_common'), 'accessor', {'source':'#'+ident+suffix+'a','count':str(n),'stride':str(stride)})
            for param in params:
                e(acc, 'param', {'name':param,'type':'float'})
        vert = e(m, 'vertices', {'id':ident+'v'})
        e(vert, 'input', {'semantic':'POSITION','source':'#'+ident+'p'})
        tr = e(m, 'triangles', {'count':str(n//3)})
        for semantic, src, off in [('VERTEX','v',0),('NORMAL','n',1),('TEXCOORD','uv',2)]:
            e(tr, 'input', {'semantic':semantic,'source':'#'+ident+src,'offset':str(off)})
        e(tr, 'p', text=' '.join(str(j) for j in range(n) for _ in range(3)))
        owner = node
        if part is not None:
            if part not in part_nodes:
                label = f'Part{part}'
                part_nodes[part] = e(node, 'node', {'id':name+label,'name':label,'type':'NODE'})
            owner = part_nodes[part]
        e(owner, 'instance_geometry', {'url':'#'+ident,'name':role})
    e(e(root, 'scene'), 'instance_visual_scene', {'url':'#Scene'})
    path = ASSETS / 'Models' / (name+'.dae')
    pending = path.with_suffix('.dae.pending')
    pending.write_bytes(ET.tostring(root, encoding='utf-8', xml_declaration=True))
    pending.replace(path)
    return {
        'name': name, 'triangles': tris_total, 'meshes': len(sections), 'bones': 1 + len(part_nodes),
        'bounds': [[min(v[i] for v in bounds) for i in range(3)], [max(v[i] for v in bounds) for i in range(3)]],
        'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
    }

def main():
    atlas = write_atlas()
    reports = [export_model('Pyramid'+name, model().parts()) for name, model in MODELS.items()]
    for name, (model, footprint) in MONUMENTS.items():
        report = export_model('Pyramid'+name, model().parts(), footprint)
        report['footprint'] = footprint
        reports.append(report)
    manifest = {
        'artRevision': 'Sunstep tomb decor V3',
        'author': 'Original project geometry and illustrated atlas',
        'generatorSha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        'textureSha256': hashlib.sha256(atlas.read_bytes()).hexdigest(),
        'textureSize': [ATLAS_SIZE, ATLAS_SIZE],
        'palette': [{'name': name, 'color': color} for name,color in PALETTE],
        'models': reports,
    }
    path = ROOT / 'docs/assets/pyramid-decor/manifest.json'
    path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
    print(f"Generated {len(reports)} decor models, {sum(r['triangles'] for r in reports)} triangles.")


if __name__ == '__main__':
    main()
