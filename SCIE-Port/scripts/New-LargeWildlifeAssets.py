"""Original low-poly models for the mossback turtle and cloudwing ray."""
import importlib.util
import math
import random
from pathlib import Path

_source = Path(__file__).with_name("New-WildArthropodAssets.py")
_spec = importlib.util.spec_from_file_location("wildlife_geometry", _source)
geometry = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(geometry)

Creature = geometry.Creature
PALETTE = (
    (70, 73, 50), (105, 108, 69), (126, 125, 75), (170, 155, 98),
    (63, 92, 52), (99, 135, 61), (143, 164, 75), (34, 43, 31),
    (39, 76, 109), (63, 122, 150), (85, 166, 184), (143, 213, 212),
    (189, 211, 207), (50, 87, 117), (219, 181, 100), (27, 40, 52),
)


def turtle():
    animal = Creature("MossbackTurtle")
    center_z = .19
    animal.shell("Body", (0, .74, center_z), (1.23, .32, 1.32), 0, 12)
    airfoil = ((.86, .65), (1, .91), (.97, 1.02), (.82, 1.33), (.53, 1.62), (.24, 1.76))
    rings = []
    for radius, y in airfoil:
        rings.append([(1.26 * radius * math.cos(a), y + .016 * math.cos(3 * a),
                       center_z + 1.36 * radius * math.sin(a))
                      for a in ((2 * math.pi * (i + .5) / 16) for i in range(16))])
    for row, (low, high) in enumerate(zip(rings, rings[1:])):
        for i in range(16):
            j = (i + 1) % 16
            color = 0 if row == 0 else (1 + (i + row) % 2 if row >= 2 else 3)
            animal.mesh("Body", (low[i], high[i], high[j], low[j]), [(0, 1, 2, 3)], color)
    animal.mesh("Body", rings[0], [tuple(range(16))], 0)
    animal.mesh("Body", rings[-1], [tuple(reversed(range(16)))], 2)

    # Scutes overlap the dome. Their bevels preserve a shell silhouette at game distance.
    for i, z in enumerate((-.62, -.06, .53, 1.02)):
        y = (1.52, 1.68, 1.65, 1.46)[i]
        animal.shell("Body", (0, y, z), (.33, .085, .29), 2 if i % 2 else 1, 6)
    for s in (-1, 1):
        for i, z in enumerate((-.48, .07, .64)):
            animal.shell("Body", (s * (.60 + (.08 if i == 1 else 0)), 1.45 - .034 * abs(i - 1), z),
                         (.35, .085, .28), 1 if i % 2 else 2, 6)
    rng = random.Random(311)
    for i in range(22):
        x = rng.uniform(-.94, .97)
        z = rng.uniform(-.66, 1.16)
        falloff = (x / 1.27) ** 2 + ((z - center_z) / 1.36) ** 2
        if falloff > .69:
            continue
        y = 1.73 - .57 * falloff
        animal.shell("Body", (x, y, z),
                     (rng.uniform(.10, .26), rng.uniform(.032, .060), rng.uniform(.11, .25)),
                     4 + i % 3, 6)

    animal.bone("Head", "Body", (0, .79, -.84))
    animal.limb("Head", [(0, .77, -.86), (0, .79, -1.24), (0, .73, -1.57)],
                [.33, .28, .23], 1, 8)
    animal.shell("Head", (0, .75, -1.60), (.39, .295, .39), 2)
    animal.shell("Head", (0, .61, -1.74), (.34, .125, .27), 3)
    for side in (-1, 1):
        animal.shell("Head", (side * .308, .837, -1.73), (.080, .077, .096), 7, 6)
        animal.shell("Head", (side * .358, .850, -1.767), (.033, .033, .038), 14, 6)
        animal.shell("Head", (side * .134, .767, -1.930), (.036, .028, .041), 7, 6)

    for side, label in ((-1, "L"), (1, "R")):
        for j, z in enumerate((-.72, .93)):
            bone = f"Leg{label}{j + 1}"
            root = (side * .84, .67, z)
            paw = (side * 1.012, .168, z - .18)
            animal.bone(bone, "Body", root)
            animal.limb(bone, [root, (side * 1.01, .46, z + .038), paw], [.215, .217, .177], 1, 7)
            animal.shell(bone, (paw[0], .136, paw[2] - .032), (.285, .132, .305), 2, 7)
            for offset in (-.153, 0, .153):
                animal.limb(bone, [(paw[0] + offset, .075, paw[2] - .24),
                                   (paw[0] + offset, .042, paw[2] - .33),
                                   (paw[0] + offset, .028, paw[2] - .40)], [.054, .039, 0], 3, 4)
    animal.bone("Tail", "Body", (0, .67, 1.24))
    animal.limb("Tail", [(0, .66, 1.22), (0, .515, 1.63), (.06, .44, 1.92), (.105, .41, 2.00)],
                [.223, .130, .068, 0], 1, 7)
    return animal


def wing(animal, bone, sections, side, tip=False):
    """Closed foil with separate back and belly patterns, measured in world units."""
    chord = (0, .16, .43, .69, .86, 1)
    profile = (.10, .66, 1, .67, .33, .10)
    surface = []
    for x, leading, trailing, y, thick in sections:
        top, lower = [], []
        for q, p in zip(chord, profile):
            z = leading + (trailing - leading) * q
            top.append((side * x, y + thick * p, z))
            lower.append((side * x, y - thick * p * .49, z))
        surface.append((top, lower))

    def face(vertices, color, reverse=False):
        indices = (3, 2, 1, 0) if (side < 0) != reverse else (0, 1, 2, 3)
        animal.mesh(bone, vertices, [indices], color)

    for row, ((near, near_bottom), (far, far_bottom)) in enumerate(zip(surface, surface[1:])):
        for i in range(len(chord) - 1):
            color = (13, 10, 8, 9, 13)[i]
            if not tip and row in (1, 2) and i in (1, 3):
                color = 11 if row == 2 else 10
            if tip and i == 2:
                color = 9
            face((near[i], near[i + 1], far[i + 1], far[i]), color)
            face((near_bottom[i], near_bottom[i + 1], far_bottom[i + 1], far_bottom[i]), 12 if i < 4 else 9, True)
        face((near[0], far[0], far_bottom[0], near_bottom[0]), 13)
        face((near[-1], far[-1], far_bottom[-1], near_bottom[-1]), 9, True)

    for end, (top, lower) in enumerate((surface[0], surface[-1])):
        for i in range(len(chord) - 1):
            face((top[i], lower[i], lower[i + 1], top[i + 1]), 9, bool(end))


def ray():
    animal = Creature("CloudwingRay")
    animal.shell("Body", (0, .48, -.15), (.94, .314, 1.37), 8, 12)
    animal.shell("Body", (0, .365, -.29), (.79, .182, 1.15), 12, 10)
    animal.shell("Body", (0, .59, -.58), (.63, .232, .75), 9, 10)
    for side, label in ((-1, "L"), (1, "R")):
        animal.shell("Body", (side * .56, .638, -.932), (.143, .114, .123), 15, 7)
        animal.shell("Body", (side * .6, .684, -1.01), (.047, .049, .038), 14, 6)
        animal.limb("Body", [(side * .55, .425, -1.12), (side * .66, .457, -1.45),
                             (side * .46, .675, -1.74), (side * .318, .73, -1.79)],
                    [.144, .124, .083, 0], 10, 6)
        animal.shell("Body", (side * .49, .402, .883), (.39, .083, .49), 13, 6)

        root = (side * .66, .49, -.23)
        tip_root = (side * 2.11, .505, -.08)
        animal.bone("Wing" + label, "Body", root)
        animal.bone("WingTip" + label, "Wing" + label, tip_root)
        wing(animal, "Wing" + label,
             ((.46, -1.30, 1.23, .46, .190), (.98, -1.32, 1.24, .48, .172),
              (1.52, -1.21, 1.10, .50, .145), (2.17, -1.00, .89, .51, .116)), side)
        wing(animal, "WingTip" + label,
             ((2.07, -.98, .86, .505, .103), (2.59, -.78, .70, .54, .074),
              (3.10, -.55, .49, .62, .043), (3.49, -.12, .046, .758, .010)), side, True)

    animal.bone("Tail1", "Body", (0, .48, .96))
    animal.bone("Tail2", "Tail1", (0, .51, 1.88))
    animal.limb("Tail1", [(0, .48, .91), (0, .49, 1.37), (0, .51, 1.92)], [.186, .116, .072], 8, 8)
    animal.limb("Tail2", [(0, .51, 1.80), (0, .545, 2.33), (.068, .67, 2.91),
                         (.16, .79, 3.07)], [.083, .048, .035, 0], 9, 7)
    animal.shell("Tail2", (0, .63, 2.34), (.21, .045, .26), 10, 6)
    return animal


if __name__ == "__main__":
    geometry.make_atlas(geometry.ASSETS / "Textures/LargeWildlife.png", PALETTE)
    geometry.export(turtle())
    geometry.export(ray())
