"""Arrange actual Blender proof renders; does not synthesize or retouch furniture."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[1]; source=ROOT/'artifacts/home-furniture-v2'
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',24)
small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
names=[('HomeFridge','冰箱'),('HomeCabinet0','组合橱柜'),('HomeSinkOpen','水槽'),('HomeSofa0','沙发'),('HomeCoffeeTable','茶几'),('HomeCurtain','窗帘')]
canvas=Image.new('RGB',(1200,934),'#E5EAEE'); draw=ImageDraw.Draw(canvas)
draw.text((24,12),'荒野探险家 · 现代科技家居 V2',fill='#303943',font=font)
for i,(name,label) in enumerate(names):
    with Image.open(source/(name+'-front.png')) as im:
        canvas.paste(im.convert('RGB').resize((390,390),Image.Resampling.LANCZOS),((i%3)*400+5,(i//3)*430+56))
    draw.text(((i%3)*400+18,(i//3)*430+450),label,fill='#303943',font=font)
draw.text((24,908),'Blender 颜色贴图预览 · 未构建，非游戏实机截图',fill='#546C80',font=small)
target=ROOT/'docs/assets/home-furniture/overview.png'; canvas.save(target)
allnames=sorted(source.glob('Home*-front.png'))
sheet=Image.new('RGB',(1280,1440),'#E5EAEE'); draw=ImageDraw.Draw(sheet)
for i,path in enumerate(allnames):
    with Image.open(path) as im: sheet.paste(im.convert('RGB').resize((316,316)),((i%4)*320,(i//4)*360))
    draw.text(((i%4)*320+5,(i//4)*360+322),path.stem.replace('-front',''),fill='#303943',font=small)
sheet.save(source/'all-variants.png')
rear=Image.new('RGB',(1200,880),'#E5EAEE'); draw=ImageDraw.Draw(rear)
for i,(name,label) in enumerate(names):
    path=source/(name.replace('Open','')+'-back.png')
    with Image.open(path) as im: rear.paste(im.convert('RGB').resize((390,390)),((i%3)*400+5,(i//3)*440))
    draw.text(((i%3)*400+12,(i//3)*440+398),label+' / 背面',fill='#303943',font=small)
rear.save(source/'rear-audit.png')
print(target)
