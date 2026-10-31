"""Compose labelled evidence from native GPU captures and a separately labelled CPU HUD schematic.
Does not paint/recreate skill art. Icons come from the original imagegen runtime PNGs.
"""
import argparse
import csv
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('directory', type=Path)
    p.add_argument('--assembly', required=True, type=Path)
    args = p.parse_args()
    root = Path(__file__).resolve().parents[1]
    font_path = Path('C:/Windows/Fonts/msyh.ttc')
    font = ImageFont.truetype(str(font_path), 18) if font_path.exists() else ImageFont.load_default()
    stages = [('断界斩 · 四秒裂隙', 'q1-skill1-0.30'), ('无界折跃 · 双生裂隙', 'q1-skill2-0.30'),
              ('万象破碎 · 开裂 0.15s', 'q1-skill3-0.15'), ('破碎 0.60s', 'q1-skill3-0.60'),
              ('坍缩 1.30s', 'q1-skill3-1.30'), ('扩张冲击波 1.60s', 'q1-skill3-1.60')]
    sheet = Image.new('RGB', (1440, 670), '#11111d')
    draw = ImageDraw.Draw(sheet)
    draw.text((16, 9), '原生 Engine/OpenGL 合成场景 · 非游戏世界 · 非 Android 实拍', font=font, fill='white')
    for i, (caption, filename) in enumerate(stages):
        x, y = (i % 3)*480, 46+(i//3)*310
        draw.text((x+12, y+5), caption, font=font, fill='#e4caff')
        sheet.paste(Image.open(args.directory/(filename+'.png')).resize((480, 270)), (x, y+32))
    sheet.save(args.directory/'key-stages.png')
    assets = root/'src/SCIEPort/Assets/Textures'
    icons = [Image.open(assets/('Abyss'+n+'V2.png')).convert('RGBA') for n in ['WorldCut', 'Blink', 'Shatter']]
    atlas = Image.open(assets/'AbyssSkillsV1.png').convert('RGBA')
    w, h = atlas.size
    icons += [atlas.crop((w//2, h//2, w, h)), atlas.crop((0, 0, w//2, h//2))]
    rows = list(csv.DictReader((args.directory/'hud-layout.csv').open()))
    hud = Image.new('RGB', (1280, 1480), '#16212c')
    names = ['1 断界斩', '2 无界折跃', '3 万象破碎', '4 虚夜湮灭', '5 裂界星门']
    for left in [0, 1]:
        top = left*740
        d = ImageDraw.Draw(hud)
        d.text((20, top+14), 'CPU布局示意 · 非游戏截图 · '+('左手镜像' if left else '右手布局'), font=font, fill='white')
        for x, name in [(16 if left else 1136, '查看'), (1136 if left else 16, '移动')]:
            d.ellipse((x, top+564, x+128, top+692), outline='#66788a', width=3)
            d.text((x+40, top+614), name, font=font, fill='#c5d1dd')
        d.rectangle((320, top+662, 960, top+712), outline='#66788a', width=2)
        d.text((570, top+678), '快捷栏', font=font, fill='#c5d1dd')
        for row in rows:
            if int(row['left']) != left:
                continue
            i = int(row['index'])
            x, y, s = (round(float(row[n])) for n in ['x', 'y', 'size'])
            icon = icons[i].resize((s, s), Image.Resampling.LANCZOS)
            hud.paste(icon, (x, y+top), icon)
            d.text((x+s//2-ImageDraw.Draw(hud).textlength(names[i], font=font)//2, y+top+s+2), names[i], font=font, fill='#ebd4ff')
    hud.save(args.directory/'hud-schematic.png')
    metadata = dict(gpu='Native Engine/OpenGL synthetic geometry; NOT gameplay/Android',
                    hud='CPU layout from real compiled Place method; schematic controls, not native UI',
                    assembly_sha256=hashlib.sha256(args.assembly.read_bytes()).hexdigest().upper(),
                    art='Existing imagegen PNGs, resized only for schematic')
    (args.directory/'preview.json').write_text(json.dumps(metadata, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
