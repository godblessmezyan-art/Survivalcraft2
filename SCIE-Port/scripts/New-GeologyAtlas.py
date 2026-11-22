"""Rebuild the original pixel tiles for the geology cubes."""

from math import sin
from pathlib import Path

from PIL import Image, ImageDraw


def hash_byte(x, y, seed):
    value = (x * 374761393 + y * 668265263 + seed * 2246822519) & 0xFFFFFFFF
    value = (value ^ (value >> 13)) * 1274126177 & 0xFFFFFFFF
    return (value ^ (value >> 16)) & 255


def line_noise(x, y, seed):
    # The coarse cells wrap at the tile edge, so broad tones repeat without a hard seam.
    u, v = (x % 8) / 8, (y % 8) / 8
    cx, cy = x // 8, y // 8
    a = hash_byte(cx, cy, seed) * (1 - u) + hash_byte((cx + 1) % 4, cy, seed) * u
    b = hash_byte(cx, (cy + 1) % 4, seed) * (1 - u) + hash_byte((cx + 1) % 4, (cy + 1) % 4, seed) * u
    return (a * (1 - v) + b * v - 128) / 9


PALETTE = [
    (51, 47, 50), (59, 64, 69), (66, 71, 75), (48, 45, 49), (217, 206, 169),
    (156, 78, 59), (181, 114, 67), (100, 118, 134), (211, 168, 107),
    (177, 170, 152), (68, 142, 179), (220, 219, 207),
]


def pixel(slot, x, y):
    high = hash_byte(x, y, slot + 9)
    relief = line_noise(x, y, slot + 200)
    shade = relief + (high - 128) / 19
    if slot == 0:
        cell = hash_byte(x // 3, y // 3, 90)
        if cell > 206 and (x % 3, y % 3) in ((1, 1), (1, 2), (2, 1)):
            shade -= 15
        if high > 244:
            shade += 13
    elif slot == 1:
        stagger = 1 if relief > 4 else 0
        groove = (x + stagger) % 11
        shade += -20 if groove == 0 else 14 if groove == 1 else 0
        if (y + 4 * (x // 11)) % 16 == 0:
            shade -= 9
    elif slot == 2:
        if (x + y // 2 + int(relief / 5)) % 17 == 4 or (y - x // 3) % 21 == 10:
            shade -= 18
    elif slot == 3:
        shade = relief * .5 + (high - 128) / 8
    elif slot in (4, 5, 6, 7, 8, 9):
        layer = int(y + 2.6 * sin(x * .195 + slot) + relief / 5)
        band = layer % (6 if slot == 4 else 9)
        shade += (-13, 10, 3)[band] if band <= 2 else 0
        if slot == 9 and (x + y // 3) % 14 == 0:
            shade -= 13
    elif slot == 10:
        fracture = (x - y // 2 + int(relief / 5)) % 14
        shade += 43 if fracture == 1 else 19 if fracture == 2 else -16 if fracture == 0 else 0
        shade += 10 * sin((x + y) * .24)
    elif slot == 11:
        cx, cy = x // 8, y // 8
        distances = []
        for j in range(cy - 1, cy + 2):
            for i in range(cx - 1, cx + 2):
                dx = x - (8 * i + 2 + hash_byte(i % 4, j % 4, 71) % 5)
                dy = y - (8 * j + 2 + hash_byte(i % 4, j % 4, 78) % 5)
                distances.append(dx * dx + dy * dy)
        nearest, next_nearest = sorted(distances)[:2]
        shade = -21 + shade * .4 if next_nearest - nearest < 7 else 9 + shade * .7
    return tuple(max(0, min(255, round(component + shade))) for component in PALETTE[slot]) + (255,)


def field_tile(slot, x, y):
    grain = line_noise(x, y, 341 + slot) + (hash_byte(x, y, 77) - 128) / 22
    if slot == 16:
        base = (195, 180, 149)
        radius = ((x % 16 - 8) ** 2 + (y % 16 - 9) ** 2) ** .5
        shell = 2.5 <= radius <= 6.5 and y % 16 <= 10
        pattern = (27 if int(radius) % 2 else -18) if shell else 0
    elif slot in (17, 18):
        base = (47, 115, 159) if slot == 17 else (62, 144, 189)
        ridge = (y + round(2 * sin(x * .196)) + (x // 4 if slot == 18 else 0)) % 8
        pattern = (79, 41, -22, -9, 0, 13, 8, -6)[ridge]
    else:
        base = (147, 132, 111) if slot == 19 else (162, 151, 128)
        layer = (y + round(2 * sin(x * .196 + slot))) % 8
        pattern = (-36, -22, 1, 20, 28, 12, 4, -9)[layer]
    return tuple(max(0, min(255, round(c + grain + pattern))) for c in base) + (255,)


def main():
    atlas = Image.new("RGBA", (512, 512))
    for slot in range(len(PALETTE)):
        for y in range(32):
            for x in range(32):
                atlas.putpixel((slot * 32 + x, y), pixel(slot, x, y))
    draw = ImageDraw.Draw(atlas)
    crystal = [(4, 24), (6, 13), (13, 5), (21, 7), (28, 16), (27, 24), (20, 28), (10, 28)]
    draw.polygon([(x + 384, y) for x, y in crystal], fill=(190, 112, 82, 255))
    draw.polygon([(390, 13), (397, 5), (405, 7), (402, 16), (393, 19)], fill=(255, 231, 193, 255))
    draw.polygon([(402, 16), (405, 7), (412, 16), (411, 24), (404, 28)], fill=(225, 150, 110, 255))
    draw.polygon([(393, 19), (402, 16), (404, 28), (394, 28)], fill=(250, 200, 153, 255))

    for slot in (13, 14):
        for y in range(32):
            for x in range(32):
                rim = min(x, 31 - x, y, 31 - y)
                if rim < 3 or slot == 13 and y >= 26:
                    color = pixel(2, x, y)
                else:
                    grain = line_noise(x, y, 121) + (hash_byte(x, y, 36) - 128) / 24
                    facet = ((x + y // 2) % 9 - (y - x // 2) % 7) * 2
                    glow = max(0, 17 - abs(x - 15) - abs(y - 14))
                    color = tuple(round(max(0, min(255, c + grain + facet + glow))) for c in (230, 162, 105)) + (255,)
                atlas.putpixel((slot * 32 + x, y), color)
    for slot in range(16, 21):
        for y in range(32):
            for x in range(32):
                atlas.putpixel(((slot % 16) * 32 + x, (slot // 16) * 32 + y), field_tile(slot, x, y))
    output = Path(__file__).resolve().parents[1] / "src/SCIEPort/Assets/Textures/Geology.png"
    atlas.save(output, optimize=True)


if __name__ == "__main__":
    main()
