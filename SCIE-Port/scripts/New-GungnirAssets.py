"""Original Gungnir: faceted wing blade, inlaid runes, chased silver, antique gold.

Reuses only this project's geometry serializer; never imports third-party art.
Run with Python 3 + Pillow + numpy. Outputs final UV-mapped mesh and inspectable previews.
"""
import math
import random
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import importlib.util
import sys
sys.dont_write_bytecode = True

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("gungnir_geometry", Path(__file__).with_name("New-StormhammerAssets.py"))
g = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = g
spec.loader.exec_module(g)
g.MODEL_PATH = ROOT / "src/SCIEPort/Assets/Models/Gungnir.dae"
g.TEXTURE_PATH = ROOT / "src/SCIEPort/Assets/Textures/Gungnir.png"


def blade(name, profile, depth, material):
    """Diamond-section blade with a raised ridge and bevelled cutting perimeter."""
    mesh = g.Mesh(name, material)
    taper_width = max(x for y,x in profile)
    for side in (-1, 1):
        for i in range(len(profile) - 1):
            y0, x0 = profile[i]
            y1, x1 = profile[i + 1]
            for wing in (-1, 1):
                a, b = (wing * x0, y0, 0), (wing * x1, y1, 0)
                z0, z1 = depth * min(1, x0/(taper_width*.4)), depth * min(1, x1/(taper_width*.4))
                c, d = (0, y1, side * z1), (0, y0, side * z0)
                e, f = (wing*x0*.82,y0,side*z0*.28),(wing*x1*.82,y1,side*z1*.28)
                mesh.polygon([a,b,f,e], (0,(y0+y1)/2,0))
                mesh.polygon([e,f,c,d], (0,(y0+y1)/2,0))
    return mesh


def model():
    meshes = [g.cylinder_y("DivineIronShaft", (0, -0.2, 0), 0.044, 2.5, 12, "iron"),
              g.cylinder_y("LeatherGrip", (0, -0.35, 0), 0.051, 0.62, 12, "grip"),
              g.helix_wrap("GoldGripWire", -0.65, -0.05, 0.055, 9, "gold"),
              blade("WingedSilverBlade", [(0.9, .06), (1.12, .21), (1.38, .16), (1.82, .09), (2.2, .001)], .07, "silver"),
              blade("GoldBladeSpine", [(1.0, .025), (1.4, .038), (2.15, .001)], .079, "gold"),
              blade("LightWhiteGoldRidge", [(1.08, .009), (1.5, .013), (2.16, .001)], .082, "crystal"),
              blade("TailCounterweight", [(-1.78, .001), (-1.53, .075), (-1.4, .04)], .055, "silver")]
    for i, y in enumerate((-1.43, -.72, -.66, -.05, .02, .72, .85, .95)):
        meshes.append(g.cylinder_y("ChasedGoldCollar" + str(i), (0, y, 0), .065 if y < .8 else .09, .035, 16, "gold"))
    # Two independent swept quillons: tapered raised facets, not flat triangles.
    for side in (-1, 1):
        mesh = g.Mesh("WingGuard" + str(side), "gold")
        pts = [(side * .035, .83, 0), (side * .23, .95, 0), (side * .31, 1.2, 0), (side * .14, 1.07, 0)]
        for z in (-.045, .045):
            ridge = (side * .15, 1.0, z)
            for i in range(4): mesh.polygon([pts[i], pts[(i + 1) % 4], ridge], (side * .14, 1, 0))
        meshes.append(mesh)
    # Readable raised runic inlays on both faces of the shaft, bounded geometry cost.
    for face in (-1, 1):
        for i in range(12):
            y = .12 + i * .047
            mesh = g.Mesh("LightRune_%s_%s" % (face, i), "rune")
            z = face * .046
            def line(x0, y0, x1, y1):
                dx, dy = x1-x0, y1-y0
                length = math.hypot(dx, dy)
                dx, dy = -dy/length*.002, dx/length*.002
                mesh.polygon([(x0-dx,y0-dy,z),(x0+dx,y0+dy,z),(x1+dx,y1+dy,z),(x1-dx,y1-dy,z)], (0,y,0))
            line(0,y-.014,0,y+.014)
            line(0,y+.014,.016,y)
            line(.016,y,0,y-.003)
            if i % 2: line(0,y,-.012,y+.009)
            meshes.append(mesh)
    return meshes


def textures():
    image = Image.new("RGBA", (1024, 1024), (20, 21, 26, 255))
    draw = ImageDraw.Draw(image)
    palette = {"iron": ((44,49,58),(112,123,135)), "silver": ((123,135,150),(221,227,231)),
               "gold": ((116,83,37),(224,189,106)), "rune": ((205,158,62),(255,240,178)),
               "grip": ((30,27,29),(75,63,52)), "connector": ((67,74,83),(155,165,174)),
               "crystal": ((244,221,153),(255,255,237)), "engraving": ((23,27,33),(98,102,110))}
    for seed, (name, box) in enumerate(g.ATLAS_REGIONS.items()):
        g.paint_material(draw, box, *palette[name], seed + 914)
    # Etched repeating knots and rune strokes belong to defined UV regions.
    for name in ("gold", "silver", "rune"):
        x0,y0,x1,y1 = g.ATLAS_REGIONS[name]
        for x in range(x0+16,x1-12,32):
            for y in range(y0+20,y1-20,48):
                color = palette[name][1]
                draw.line([(x,y+20),(x,y-15),(x+10,y-4),(x,y+3)], fill=color, width=2)
                draw.line([(x,y),(x-8,y-7)], fill=color,width=2)
    image.save(g.TEXTURE_PATH)
    icons = Image.new("RGBA", (512,256), (19,23,29,255))
    d = ImageDraw.Draw(icons)
    for j in range(2):
        cx = j*256+128
        for radius in (112,104,88): d.ellipse((cx-radius,128-radius,cx+radius,128+radius),outline=(192,155,77),width=3)
        for i in range(16):
            a = i*math.tau/16
            x,y = cx+97*math.cos(a),128+97*math.sin(a)
            d.line([(x,y-5),(x,y+5),(x+5,y)],fill=(236,213,156),width=2)
        if j == 0:
            d.polygon([(cx,55),(cx+48,80),(cx+35,150),(cx,184),(cx-35,150),(cx-48,80)], fill=(55,62,71),outline=(246,219,146),width=4)
            d.line([(cx,154),(cx,80),(cx+27,103),(cx,122),(cx-24,99)],fill=(255,240,188),width=6)
        else:
            for y in (70,105,140): d.ellipse((cx-55,y-10,cx+55,y+10),outline=(214,177,86),width=3)
            d.line([(cx,45),(cx,174)],fill=(255,245,213),width=8)
            d.polygon([(cx,199),(cx-23,150),(cx,163),(cx+23,150)],fill=(255,237,184))
    icons.save(ROOT / "src/SCIEPort/Assets/Textures/GungnirSkills.png")


def main():
    textures()
    meshes = model()
    g.write_model(meshes)
    text = g.MODEL_PATH.read_text(encoding="utf8").replace("Stormhammer", "Gungnir")
    # Serializer stores hammer attachment metadata; replace with spear-specific anchors.
    import re
    text = re.sub(r"<extra>.*?</extra>", '<extra><technique profile="SCIEPORT"><attachment name="Grip" x="0" y="-0.35" z="0"/><attachment name="Tip" x="0" y="2.2" z="0"/><attachment name="Tail" x="0" y="-1.78" z="0"/></technique></extra>', text, flags=re.S)
    g.MODEL_PATH.write_text(text,encoding="utf8")
    preview = Image.new("RGB",(1000,1100),(22,26,33))
    for i, yaw in enumerate((0,.65,1.57)):
        # Render at full aspect, then crop horizontal whitespace (no geometry distortion).
        panel=g.render_preview(meshes,yaw,900,1050).crop((285,0,615,1050))
        preview.paste(panel,(i*333,50))
    ImageDraw.Draw(preview).text((30,20),"GUNGNIR | FRONT / THREE-QUARTER / EDGE | REAL MESH PREVIEW",fill=(230,206,157))
    preview.save(ROOT / "docs/images/gungnir-model-preview.png")
    audio = ROOT / "src/SCIEPort/Assets/Audio/Gungnir"
    audio.mkdir(exist_ok=True,parents=True)
    g.write_wav(audio/"Invoke.wav",1.5,lambda t,d,r: .22*math.sin(math.pi*t/d)**2*(math.sin(math.tau*110*t)+.5*math.sin(math.tau*164.81*t)+.3*math.sin(math.tau*220*t)))
    g.write_wav(audio/"Impact.wav",1.8,lambda t,d,r: .65*math.exp(-t*4)*(math.sin(math.tau*(55*t+20*t*t))+.25*r.uniform(-1,1))+.12*math.exp(-t*2)*math.sin(math.tau*440*t))
    print("Gungnir:",len(meshes),"meshes",sum(m.triangle_count for m in meshes),"triangles; original UV texture/icons/audio generated")


if __name__ == "__main__": main()
