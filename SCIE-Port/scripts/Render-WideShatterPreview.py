"""Label native GPU wide-shatter captures. Synthetic scenery, never Android/gameplay proof."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('directory', type=Path)
    p.add_argument('--assembly', type=Path, required=True)
    p.add_argument('--baseline', type=Path)
    args = p.parse_args()
    f = Path('C:/Windows/Fonts/msyh.ttc')
    font = ImageFont.truetype(str(f), 18) if f.exists() else ImageFont.load_default()
    first = ('0.61.0 原范围 · 0.60s', args.baseline) if args.baseline else ('0.15s · 前方开裂', args.directory/'q1-skill3-0.15.png')
    stages = [first, ('0.30s · 裂界覆盖前方', args.directory/'coverage-q1-a2.22-f85-filled.png'),
              ('0.60s · 厚片分离', args.directory/'q1-skill3-0.60.png'), ('1.30s · 向中心坍缩', args.directory/'q1-skill3-1.30.png'),
              ('1.60s · 扩张爆炸', args.directory/'q1-skill3-1.60.png'), ('1.80s · 恢复', args.directory/'q1-skill3-after.png')]
    sheet = Image.new('RGB', (1440, 670), '#10101b')
    draw = ImageDraw.Draw(sheet)
    draw.text((14, 10), '原生 Engine/OpenGL 合成场景 · 非游戏世界 · 非Android实拍 · 前方裂界扩大', font=font, fill='white')
    for i, (caption, path) in enumerate(stages):
        x, y = i % 3 * 480, 46+i//3*310
        draw.text((x+12, y+5), caption, font=font, fill='#e4caff')
        sheet.paste(Image.open(path).resize((480, 270)), (x, y+32))
    sheet.save(args.directory/'wide-shatter-stages.png')
    rows = list(csv.DictReader((args.directory/'coverage.csv').open()))
    result = dict(evidence='Target Engine/OpenGL synthetic scenery; NOT game world or Android',
                  assembly_sha256=hashlib.sha256(args.assembly.read_bytes()).hexdigest().upper(),
                  minimum_tested_frontal_coverage_percent=min(float(r['covered_percent']) for r in rows),
                  coverage_cases=len(rows), first_person='Owner FppCamera foreground geometry', observer='World-space geometry with DepthRead')
    (args.directory/'preview.json').write_text(json.dumps(result, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
