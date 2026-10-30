"""Render exported furnished-home blueprint data into reviewable floor-plan PNGs.
Requires Pillow. Reads the actual JSON cells and recipe manifest, never edits blueprints.
"""
import argparse
import json
from collections import deque
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont


def render(source, output, font_path):
    layouts=json.loads((source/'layouts.json').read_text(encoding='utf-8'))
    def font(size): return ImageFont.truetype(str(font_path),size)
    colors=['#b78555','#c57d71','#839b61','#d8b768','#799bac','#ad8bba','#c27e9a']
    rendered=[]
    for layout in layouts:
        blueprint=json.loads((source/layout['file']).read_text(encoding='utf-8'))
        w,l=layout['width'],layout['length']; door=(w-1)//2; split=layout['partitionZ']
        materials={m['id']:m['key'] for m in blueprint.get('materials',[])}
        cells={(c['x'],c['y'],c['z']):c.get('key',materials.get(c.get('m'))) for c in blueprint['cells']}
        solid={(x,z) for (x,y,z) in cells if y in (1,2)}
        floor={(x,z) for x,y,z in cells if y==0}
        for item in layout['furniture']:
            solid.update((x,z) for x in range(item['x'],item['x']+item['width']) for z in range(item['z'],item['z']+item['length']))
        start=(door,1); parents={start:None}; q=deque([start])
        while q:
            x,z=q.popleft()
            for p in [(x-1,z),(x+1,z),(x,z-1),(x,z+1)]:
                if 0<p[0]<w-1 and 0<p[1]<l-1 and p in floor and p not in solid and p not in parents:
                    parents[p]=(x,z);q.append(p)
        paths=set()
        for item in layout['furniture']:
            p=(item['accessX'],item['accessZ'])
            if p not in parents: raise ValueError('Unreachable approach in exported file: '+layout['file'])
            while p is not None:
                paths.add(p);p=parents[p]
        image=Image.new('RGB',(1200,940),'#f4f2ed');d=ImageDraw.Draw(image)
        d.text((40,28),'一室一厅 · '+str(w)+' × '+str(l),font=font(34),fill='#233139')
        d.text((40,80),f"{layout['material']}外墙 / { {'Hip':'四坡','Gabled':'人字','Flat':'平顶'}[layout['roof']]}屋顶 / 种子 {layout['seed']} / {layout['cellCount']} 格",font=font(20),fill='#5c6870')
        unit=min(46,680//l);left=80;top=150
        for z in range(l):
            for x in range(w):
                rect=(left+x*unit,top+z*unit,left+(x+1)*unit,top+(z+1)*unit)
                fill='#e3eee9' if z<split else '#e4eaf3'
                if (x,z) in solid:fill='#516370'
                d.rectangle(rect,fill=fill,outline='#c6cdd0',width=1)
        for p in paths:
            x,z=p;cx=left+(x+.5)*unit;cy=top+(z+.5)*unit
            d.ellipse((cx-4,cy-4,cx+4,cy+4),fill='#d7a738')
        # Show authored windows on the shell even if their bottom sill remains solid.
        for (x,y,z),key in cells.items():
            if key in ('Glass','GlassBlock') and (x in (0,w-1) or z in (0,l-1)):
                rect=(left+x*unit+5,top+z*unit+5,left+(x+1)*unit-5,top+(z+1)*unit-5)
                d.rectangle(rect,fill='#9cd4df')
        for n,item in enumerate(layout['furniture']):
            x,z=item['x'],item['z'];sx,sz=item['width'],item['length']
            rect=(left+x*unit+2,top+z*unit+2,left+(x+sx)*unit-2,top+(z+sz)*unit-2)
            d.rectangle(rect,fill=colors[n],outline='#f5f3ee',width=2)
            center=(left+(x+sx*.5)*unit,top+(z+sz*.5)*unit)
            d.text(center,str(n+1),font=font(20),fill='white',anchor='mm')
            ax,az=item['accessX'],item['accessZ'];cx=left+(ax+.5)*unit;cy=top+(az+.5)*unit
            d.ellipse((cx-6,cy-6,cx+6,cy+6),fill='#f4f2ed',outline='#b27d20',width=2)
        dx=left+(door+.5)*unit
        d.rectangle((left+door*unit+2,top+2,left+(door+1)*unit-2,top+unit-2),fill='#cfa550')
        d.line([(dx,top-30),(dx,top-5)],fill='#c28b2f',width=4)
        d.polygon([(dx,top),(dx-7,top-10),(dx+7,top-10)],fill='#c28b2f')
        d.text((left+w*unit+18,top+unit),'客厅',font=font(22),fill='#355b4d')
        d.text((left+w*unit+18,top+(split+1)*unit),'卧室',font=font(22),fill='#45607d')
        d.text((left,top+l*unit+12),'入口在上方（作者 -Z 方向）   ·   黄色点线为可达路线',font=font(18),fill='#4c5c64')
        tx=760
        d.text((tx,155),'家具与通行',font=font(27),fill='#233139')
        for n,item in enumerate(layout['furniture']):
            y=210+n*55
            d.rounded_rectangle((tx,y,tx+30,y+30),radius=5,fill=colors[n])
            d.text((tx+15,y+15),str(n+1),font=font(18),fill='white',anchor='mm')
            d.text((tx+44,y+1),item['name'],font=font(23),fill='#34444c')
        d.text((tx,630),'○ 家具旁的两格高通行点',font=font(19),fill='#86621e')
        d.text((tx,665),'墙体 / 家具包围盒均避让通道',font=font(18),fill='#5c6870')
        d.text((tx,702),'示意图隐藏屋顶，非游戏截图',font=font(18),fill='#5c6870')
        d.text((tx,736),'家具为结构块装饰模板',font=font(18),fill='#5c6870')
        d.text((40,910),'基于导出的实际 JSON 坐标绘制；游戏内碰撞、触屏与 Android 效果仍待验证。',font=font(19),fill='#6a7478')
        path=output/(Path(layout['file']).stem+'.png');image.save(path);rendered.append(image)
    overview=Image.new('RGB',(1800,470),'#f4f2ed')
    for n,image in enumerate(rendered): overview.paste(image.resize((600,470),Image.Resampling.LANCZOS),(n*600,0))
    overview.save(output/'furnished-homes-overview.png')


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--source',type=Path,default=Path('docs/examples/furnished-homes'))
    parser.add_argument('--output',type=Path,default=Path('artifacts/furnished-homes'))
    parser.add_argument('--font',type=Path,default=Path('C:/Windows/Fonts/msyh.ttc'))
    args=parser.parse_args();args.output.mkdir(parents=True,exist_ok=True)
    render(args.source,args.output,args.font)
