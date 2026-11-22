"""Render the exported, actual C# meshes; no substitute geometry or game screenshot claim."""
import argparse
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
TYPES = ['AncientRoofBlock','AncientTimberBlock','AncientWindowBlock','AncientLanternBlock','AncientTeaBlock','AncientGardenBlock','AncientTelescopeBlock']


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--meshes', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    args = p.parse_args()
    samples = json.loads(args.meshes.read_text(encoding='utf-8-sig'))
    args.output.mkdir(parents=True, exist_ok=True)
    canvas = Image.new('RGB', (1440, 1740), '#e7e2d8')
    draw = ImageDraw.Draw(canvas)
    font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 21)
    small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 15)
    title = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 32)
    draw.text((28, 16), '古建细节套件 · 28 种外观', font=title, fill='#3e2d22')
    draw.text((30, 65), '实际 C# BlockMesh 几何预览；非游戏截图。透明、照明与实机性能待验证。', font=small, fill='#6b6258')
    gallery = []
    for sample in samples:
        row = sample['id'] - 1014
        col = sample['data'] >> 3
        left, top = 15 + col * 238, 105 + row * 231
        draw.rounded_rectangle((left, top, left + 228, top + 219), 12, fill='#f5f1e9')
        triangles = []
        for part in sample['parts']:
            vertices = part['vertices']
            for i in range(0, len(part['indices']), 3):
                points = [vertices[part['indices'][i + j]] for j in range(3)]
                # Painter order from the viewer at positive X, Y and negative Z.
                depth = sum(v[0] + .65 * v[1] - v[2] for v in points) / 3
                screen = [(left + 114 + (v[0] + v[2] - 1) * 80, top + 143 - v[1] * 100 + (v[0] - v[2]) * 30) for v in points]
                color = points[0][3:7]
                alpha = color[3] / 255
                rgb = tuple(round(c * alpha + bg * (1-alpha)) for c, bg in zip(color[:3], (245,241,233)))
                triangles.append((depth, screen, rgb))
        for _, points, color in sorted(triangles, key=lambda x: x[0]):
            draw.polygon(points, fill=color)
        draw.text((left + 12, top + 177), sample['name'], font=font, fill='#3e2d22')
        draw.text((left + 12, top + 201), f"{sample['id']} / DATA {sample['data']}", font=small, fill='#81715e')
        gallery.append({'x': col * 3 + 1, 'y': 1, 'z': row * 3 + 1, 'key': TYPES[row], 'f': 'detail', 'd': sample['data']})
    canvas.save(args.output / 'architecture-kit.png')
    # One gallery file and one working assembly example, both directly importable.
    blueprints = ROOT / 'blueprints'
    blueprints.mkdir(exist_ok=True)
    floor = [{'x': x, 'y': 0, 'z': z, 'key': 'PlanksBlock'} for x in range(18) for z in range(21)]
    (blueprints / 'ancient-detail-kit.json').write_text(json.dumps({'schemaVersion': 3, 'kind': 'builderassistant.blueprint', 'name': '古建细节展台', 'cells': floor + gallery}, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    floor = [{'x':x,'y':0,'z':z,'key':'PlanksBlock'} for x in range(12) for z in range(8) if not(8 <= x <= 10 and 4 <= z <= 6)]
    placements = [dict(templateId='AncientMoonWindow_A',x=1,y=1,z=5,rotation=0),dict(templateId='AncientTeaCorner_A',x=4,y=1,z=2,rotation=0),dict(templateId='AncientObservatory_A',x=8,y=0,z=4,rotation=0)]
    (blueprints / 'ancient-bookhouse-corners.json').write_text(json.dumps({'schemaVersion':3,'kind':'builderassistant.blueprint','name':'书屋细节组合','cells':floor,'furniturePlacements':placements},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    capabilities = {'mod':'SCIEPort','minVersion':'0.89.0','schemaVersion':3,'family':'detail','data':'rotation | (mirrorX ? 4 : 0) | (variant << 3)','singleCell':True,'blocks':[]}
    for row, key in enumerate(TYPES):
        forms = [s for s in samples if s['id']==1014+row]
        capabilities['blocks'].append({'key':key,'index':1014+row,'forms':[{'variant':s['data']>>3,'name':s['name']} for s in forms],'allowedData':list(range(len(forms)*8))})
    (ROOT/'docs/architecture-block-capabilities.json').write_text(json.dumps(capabilities,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'forms':len(samples),'preview':str(args.output/'architecture-kit.png'),'galleryCells':len(gallery)+378},ensure_ascii=False))


if __name__ == '__main__':
    main()
