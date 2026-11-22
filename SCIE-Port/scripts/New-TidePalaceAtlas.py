"""Draw the palace's engraved stone, three readable glyphs, and working indicators."""
from pathlib import Path
import random

from PIL import Image, ImageDraw


def stone(seed):
    rng = random.Random(seed)
    tile = Image.new("RGBA", (32, 32))
    for y in range(32):
        for x in range(32):
            speckle = rng.randrange(-7, 8)
            relief = 8 if min(x, y) < 2 else -9 if max(x, y) > 29 else 0
            band = 3 if (y + x // 6) % 9 < 3 else 0
            tone = speckle + relief + band
            tile.putpixel((x, y), (43 + tone, 67 + tone, 65 + tone, 255))
    return tile


def glyph(draw, form, color):
    if form == 0:  # fork
        draw.line((10, 10, 10, 15, 16, 19, 22, 15, 22, 10), fill=color, width=2)
        draw.line((16, 8, 16, 25), fill=color, width=2)
    elif form == 1:  # wave
        draw.line((7, 19, 12, 15, 16, 13, 20, 17, 25, 16), fill=color, width=3)
        draw.line((9, 23, 13, 20, 18, 19, 22, 21), fill=color, width=1)
    else:  # ring
        draw.ellipse((9, 9, 23, 23), outline=color, width=2)
        draw.rectangle((15, 15, 17, 17), fill=color)


atlas = Image.new("RGBA", (512, 512))
for index in range(14):
    tile = stone(index)
    art = ImageDraw.Draw(tile)
    gold = (177, 151, 98, 255)
    dark = (20, 39, 41, 255)
    glow = (131, 228, 208, 255)
    if 1 <= index <= 3:
        art.ellipse((4, 4, 27, 27), fill=dark, outline=gold, width=2)
        glyph(art, index - 1, glow)
    elif index in (4, 5):
        art.rectangle((5, 4, 26, 27), fill=dark, outline=gold, width=2)
        y = 19 if index == 4 else 10
        art.line((16, 6, 16, 25), fill=gold, width=2)
        art.rounded_rectangle((8, y - 3, 24, y + 3), radius=2, fill=gold if index == 4 else glow)
    elif index in (6, 7):
        art.rectangle((7, 4, 24, 27), outline=gold)
        art.polygon(((16, 6), (23, 16), (16, 25), (9, 16)), fill=glow if index == 7 else dark, outline=gold)
    elif index == 8:
        art.rectangle((3, 3, 28, 28), outline=gold, width=2)
        art.line((5, 24, 14, 15, 14, 6), fill=gold, width=2)
        art.line((26, 24, 17, 15, 17, 6), fill=gold, width=2)
    elif 9 <= index <= 11:
        art.rectangle((4, 4, 27, 27), fill=dark, outline=gold, width=2)
        glyph(art, index - 9, gold)
    elif index == 12:
        art.ellipse((4, 4, 27, 27), outline=gold)
        glyph(art, 0, gold)
    elif index == 13:
        art.rectangle((4, 4, 27, 27), fill=dark, outline=gold, width=2)
        art.polygon(((16, 5), (25, 16), (16, 26), (6, 16)), fill=glow, outline=gold)
        art.line((16, 9, 16, 22), fill=(222, 255, 237, 255), width=2)
    atlas.paste(tile, (index * 32, 0))

target = Path(__file__).resolve().parents[1] / "src/SCIEPort/Assets/Textures/TidePalace.png"
atlas.save(target)
