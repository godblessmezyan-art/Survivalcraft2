"""CPU visual QA of exported real Engine triangles (not an in-game screenshot).

Run Export-VoidHorizonMesh.ps1 first, then this script with its output directory.
Opaque coverage uses a magenta backdrop; a complete domain must cover every pixel.
No GPU shaders, game camera, UI, sound or device performance are simulated here.
"""
import argparse
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


def clip_near(vertices, near=0.1):
    result = []
    for a, b in zip(vertices, vertices[1:] + vertices[:1]):
        ina, inb = a[2] >= near, b[2] >= near
        if ina:
            result.append(a)
        if ina != inb:
            f = (near-a[2])/(b[2]-a[2])
            result.append(a+(b-a)*f)
    return result


def render(mesh, yaw=0, pitch=0, width=960, height=540):
    rgb = np.empty((height, width, 3), np.float64)
    rgb[:] = (220, 0, 150)
    coverage = np.zeros((height, width), bool)
    yaw, pitch = math.radians(yaw), math.radians(pitch)
    forward = np.array([math.sin(yaw)*math.cos(pitch), math.sin(pitch), math.cos(yaw)*math.cos(pitch)])
    right = np.array([-math.cos(yaw), 0, math.sin(yaw)])
    up = np.cross(right, forward)
    basis = np.stack([right, up, forward])
    focal = height/(2*math.tan(math.radians(65)/2))
    for row in mesh:
        opaque = bool(row[1])
        vertices = row[2:].reshape(3, 7).copy()
        vertices[:, :3] = vertices[:, :3]@basis.T
        vertices = clip_near(list(vertices))
        for i in range(1, len(vertices)-1):
            tri = np.array([vertices[0], vertices[i], vertices[i+1]])
            invz = 1/tri[:, 2]
            x = width/2+focal*tri[:, 0]*invz
            y = height/2-focal*tri[:, 1]*invz
            xlo, xhi = max(0, math.floor(x.min())), min(width-1, math.ceil(x.max()))
            ylo, yhi = max(0, math.floor(y.min())), min(height-1, math.ceil(y.max()))
            if xhi < xlo or yhi < ylo:
                continue
            denominator = (y[1]-y[2])*(x[0]-x[2])+(x[2]-x[1])*(y[0]-y[2])
            if abs(denominator) < 1e-9:
                continue
            px, py = np.meshgrid(np.arange(xlo, xhi+1)+0.5, np.arange(ylo, yhi+1)+0.5)
            w0 = ((y[1]-y[2])*(px-x[2])+(x[2]-x[1])*(py-y[2]))/denominator
            w1 = ((y[2]-y[0])*(px-x[2])+(x[0]-x[2])*(py-y[2]))/denominator
            w2 = 1-w0-w1
            inside = (w0>=-1e-8)&(w1>=-1e-8)&(w2>=-1e-8)
            if not np.any(inside):
                continue
            weights = np.stack([w0[inside], w1[inside], w2[inside]], axis=-1)*invz
            weights /= weights.sum(axis=-1, keepdims=True)
            colors = weights@tri[:, 3:]
            target = rgb[ylo:yhi+1, xlo:xhi+1]
            if opaque:
                target[inside] = colors[:, :3]
                coverage[ylo:yhi+1, xlo:xhi+1][inside] = True
            else:
                alpha = colors[:, 3:4]/255
                target[inside] = colors[:, :3]*alpha+target[inside]*(1-alpha)
    return Image.fromarray(np.uint8(np.clip(rgb, 0, 255))), int((~coverage).sum())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    args = parser.parse_args()
    views = [
        ('stable-front', 10, 0, 0), ('stable-side', 10, 90, 0),
        ('stable-back', 10, 180, 0), ('stable-up', 10, 0, 80),
        ('stable-down', 10, 0, -80), ('stable-zenith', 10, 0, 90), ('stable-nadir', 10, 0, -90), ('expansion-front', 2.5, 0, 0),
        ('expansion-side', 3, 90, 0), ('emergence-start', 4, 0, 0),
        ('collapse-front', 15, 0, 0), ('exit-front', 16.5, 0, 0),
        ('rift-front', 1.5, 0, 0),
    ]
    cards, report = [], []
    for name, time, yaw, pitch in views:
        mesh = np.loadtxt(args.directory/f'mesh-{time:.1f}.tsv', comments='#')
        picture, uncovered = render(mesh, yaw, pitch)
        picture.save(args.directory/f'{name}.png')
        report.append(dict(view=name, time=time, yaw=yaw, pitch=pitch, triangles=len(mesh), uncovered_pixels=uncovered))
        if name.startswith('stable-') and uncovered:
            raise AssertionError(f'{name}: original world visible through {uncovered} pixels')
        card = Image.new('RGB', (480, 300), '#14141d')
        card.paste(picture.resize((480, 270), Image.Resampling.LANCZOS), (0, 30))
        ImageDraw.Draw(card).text((10, 8), f'{name} | t={time:g} | yaw={yaw} pitch={pitch}', fill='#e5ddec')
        cards.append(card)
        print(report[-1])
    sheet = Image.new('RGB', (1440, 50+300*math.ceil(len(cards)/3)), '#08080f')
    ImageDraw.Draw(sheet).text((12, 14), 'VOID DOMAIN | Actual compiled mesh, CPU projection only. NOT GAMEPLAY. Magenta = original-world region.', fill='#e5ddec')
    for i, card in enumerate(cards):
        sheet.paste(card, ((i%3)*480, 50+(i//3)*300))
    sheet.save(args.directory/'contact-sheet.png')
    (args.directory/'coverage.json').write_text(json.dumps(report, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
