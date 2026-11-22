"""Generate the original sandstone material and legible pyramid glyphs."""
from pathlib import Path
import random

from PIL import Image, ImageDraw


def sandstone(seed):
    rng = random.Random(seed)
    tile = Image.new("RGBA", (32, 32))
    for y in range(32):
        for x in range(32):
            grain = rng.randrange(-8, 9) + (3 if (y + x // 9) % 7 < 3 else 0)
            edge = 12 if min(x, y) < 2 else -18 if max(x, y) > 29 else 0
            tile.putpixel((x, y), (155 + grain + edge, 111 + grain + edge, 68 + grain + edge, 255))
    return tile


def glyph(draw, symbol, color):
    if symbol == 0:
        draw.ellipse((10, 10, 22, 22), outline=color, width=2)
        draw.ellipse((14, 14, 18, 18), fill=color)
        for start, end in [((16, 5), (16, 8)), ((16, 24), (16, 27)), ((5, 16), (8, 16)), ((24, 16), (27, 16))]:
            draw.line((start, end), fill=color, width=2)
    elif symbol == 1:
        draw.line((6, 10, 12, 11, 16, 18, 20, 11, 26, 10), fill=color, width=2)
        draw.line((6, 15, 10, 16, 16, 23, 22, 16, 26, 15), fill=color, width=2)
    else:
        draw.line((6, 24, 16, 7, 26, 24, 6, 24), fill=color, width=2)
        draw.line((12, 20, 20, 20), fill=color, width=2)


atlas = Image.new("RGBA", (512, 512))
edge_color = (234, 182, 97, 255)
shadow = (61, 43, 37, 255)
bright = (251, 218, 135, 255)
for data in range(13):
    tile = sandstone(data)
    draw = ImageDraw.Draw(tile)
    draw.rectangle((2, 2, 29, 29), outline=edge_color, width=1)
    if data < 9:
        if data < 6:
            draw.rounded_rectangle((4, 4, 27, 27), radius=4, fill=shadow, outline=edge_color, width=2)
        else:
            draw.rectangle((4, 4, 27, 27), fill=shadow)
        glyph(draw, data % 3, (136, 243, 209, 255) if 3 <= data < 6 else bright)
    elif data == 9:
        draw.rectangle((4, 3, 27, 28), outline=shadow, width=2)
        for x in (7, 12, 19, 24):
            draw.line((x, 5, x, 26), fill=shadow, width=2)
        draw.line((4, 16, 27, 16), fill=edge_color, width=3)
    elif data == 10:
        draw.ellipse((5, 5, 26, 26), outline=shadow, width=2)
        draw.polygon(((16, 8), (24, 16), (16, 24), (8, 16)), outline=bright)
    elif data == 11:
        draw.rectangle((6, 5, 25, 26), fill=shadow, outline=edge_color, width=2)
        draw.polygon(((16, 7), (22, 17), (19, 23), (12, 23), (9, 17)), fill=edge_color)
        draw.polygon(((16, 12), (19, 17), (16, 21), (13, 17)), fill=(255, 246, 193, 255))
    else:
        draw.rectangle((6, 6, 25, 25), outline=shadow, width=2)
        draw.line((7, 22, 13, 22, 13, 17, 18, 17, 18, 12, 24, 12), fill=bright, width=2)
    atlas.paste(tile, (data * 32, 0))

target = Path(__file__).resolve().parents[1] / "src/SCIEPort/Assets/Textures/PyramidMechanism.png"
atlas.save(target)
