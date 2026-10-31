"""Label real native Engine GPU fixture captures, preserving their pixels below the label.

These captures show synthetic geometry, NOT an actual game world or Android device.
Optional ffmpeg packages the 127 native captures (0..4.2s) as an annotated video.
"""
import argparse
import hashlib
import json
import math
import subprocess
from pathlib import Path
from PIL import Image, ImageDraw


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    parser.add_argument('--ffmpeg', type=Path)
    parser.add_argument('--assembly', type=Path)
    args = parser.parse_args()
    times = ['0.00', '0.35', '0.70', '1.30', '1.80', '2.40', '2.80', '3.10', '3.70', '3.82', '3.94', '4.01', '4.12', '4.20']
    caption = 'Native Engine / OpenGL | SYNTHETIC SCENE | NOT GAMEPLAY / NOT ANDROID'
    for filename, stages in [('contact-sheet.png', times), ('key-stages.png', ['0.35', '1.80', '2.80', '3.82', '4.01', '4.20'])]:
        sheet = Image.new('RGB', (1440, 52+300*math.ceil(len(stages)/3)), '#10101b')
        draw = ImageDraw.Draw(sheet)
        draw.text((14, 12), caption, fill='white')
        for i, t in enumerate(stages):
            frame = Image.open(args.directory/f'landscape-{t}.png').resize((480, 270))
            x, y = (i%3)*480, 52+(i//3)*300
            draw.text((x+12, y+7), f'{t} seconds', fill='white')
            sheet.paste(frame, (x, y+30))
        sheet.save(args.directory/filename)
    metadata = dict(evidence='Native Engine/OpenGL synthetic scenery; not actual game/Android', stages=times)
    if args.assembly:
        metadata['assembly_sha256'] = hashlib.sha256(args.assembly.read_bytes()).hexdigest().upper()
    if args.ffmpeg:
        annotated = args.directory/'annotated-frames'
        annotated.mkdir(exist_ok=True)
        for path in sorted((args.directory/'frames').glob('frame-*.png')):
            index = int(path.stem.split('-')[-1])
            picture = Image.open(path)
            frame = Image.new('RGB', (960, 576), '#10101b')
            frame.paste(picture, (0, 36))
            ImageDraw.Draw(frame).text((12, 12), f'{caption} | {index/30:.2f}s', fill='white')
            frame.save(annotated/path.name)
        subprocess.run([str(args.ffmpeg), '-loglevel', 'error', '-y', '-framerate', '30', '-i', str(annotated/'frame-%03d.png'), '-c:v', 'libx264', '-preset', 'fast', '-crf', '20', '-pix_fmt', 'yuv420p', str(args.directory/'void-distance-native-gpu.mp4')], check=True)
        metadata['frames'] = 127
    (args.directory/'preview.json').write_text(json.dumps(metadata, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
