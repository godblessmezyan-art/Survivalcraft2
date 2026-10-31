"""CPU visual QA of exported real Engine triangles (not an in-game screenshot).

Run Export-VoidHorizonMesh.ps1 first, then this script with its output directory.
Three synthetic depth panels stand at 8/48/140 units. Complete-domain coverage
must hide every panel. This is diagnostic CPU projection, not actual scenery.
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
    rgb[:, :width//3] = (220, 80, 45)
    rgb[:, width//3:2*width//3] = (40, 175, 95)
    rgb[:, 2*width//3:] = (220, 180, 40)
    scene_depth = np.empty((height, width))
    scene_depth[:, :width//3] = 140
    scene_depth[:, width//3:2*width//3] = 48
    scene_depth[:, 2*width//3:] = 8
    coverage = np.zeros((height, width), bool)
    yaw, pitch = math.radians(yaw), math.radians(pitch)
    forward = np.array([math.sin(yaw)*math.cos(pitch), math.sin(pitch), math.cos(yaw)*math.cos(pitch)])
    right = np.array([-math.cos(yaw), 0, math.sin(yaw)])
    up = np.cross(right, forward)
    basis = np.stack([right, up, forward])
    focal = height/(2*math.tan(math.radians(65)/2))
    for row in mesh:
        opaque = bool(row[1])
        depth_read = bool(row[2])
        vertices = row[3:].reshape(3, 7).copy()
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
            if depth_read:
                inverse_depth=w0*invz[0]+w1*invz[1]+w2*invz[2]
                inside &= inverse_depth >= 1/scene_depth[ylo:yhi+1, xlo:xhi+1]
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
        ('world-hole', .35, 0, 0), ('horizon-start', .7, 0, 0), ('far-erosion', 1.3, 0, 0),
        ('middle-erosion', 2, 0, 0), ('near-erosion', 2.6, 0, 0),
        ('full-front', 3.1, 0, 0), ('full-side', 3.1, 90, 0), ('full-back', 3.1, 180, 0),
        ('full-up', 3.1, 0, 80), ('full-down', 3.1, 0, -80),
        ('full-zenith', 3.1, 0, 90), ('full-nadir', 3.1, 0, -90),
        ('collapse', 3.82, 0, 0), ('burst-start', 3.94, 0, 0), ('shockwave', 4.04, 0, 0),
        ('shockwave-late', 4.15, 0, 0), ('instant-exit', 4.2, 0, 0),
    ]
    cards, report = [], []
    for name, time, yaw, pitch in views:
        mesh_path = args.directory/f'mesh-{time:.2f}.tsv'
        with mesh_path.open(encoding='utf-8-sig') as stream:
            has_data = any(line.strip() and not line.startswith('#') for line in stream)
        mesh = np.atleast_2d(np.loadtxt(mesh_path, comments='#')) if has_data else np.empty((0, 24))
        picture, uncovered = render(mesh, yaw, pitch)
        picture.save(args.directory/f'{name}.png')
        report.append(dict(view=name, time=time, yaw=yaw, pitch=pitch, triangles=len(mesh), uncovered_pixels=uncovered))
        if name.startswith('full-') and uncovered:
            raise AssertionError(f'{name}: original world visible through {uncovered} pixels')
        card = Image.new('RGB', (480, 300), '#14141d')
        card.paste(picture.resize((480, 270), Image.Resampling.LANCZOS), (0, 30))
        ImageDraw.Draw(card).text((10, 8), f'{name} | t={time:g} | yaw={yaw} pitch={pitch}', fill='#e5ddec')
        cards.append(card)
        print(report[-1])
    sheet = Image.new('RGB', (1440, 50+300*math.ceil(len(cards)/3)), '#08080f')
    ImageDraw.Draw(sheet).text((12, 14), 'CPU diagnostic / compiled mesh + synthetic depth panels. NOT GPU / GAMEPLAY / ANDROID.', fill='#e5ddec')
    for i, card in enumerate(cards):
        sheet.paste(card, ((i%3)*480, 50+(i//3)*300))
    sheet.save(args.directory/'contact-sheet.png')
    (args.directory/'coverage.json').write_text(json.dumps(report, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
