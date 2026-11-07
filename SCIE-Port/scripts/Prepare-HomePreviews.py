"""Arrange actual Blender proof renders; does not synthesize or retouch furniture."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[1]; source=ROOT/'artifacts/home-furniture'
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',24)
small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
names=[('HomeFridgeOpen','冰箱'),('HomeCabinet0','组合橱柜'),('HomeSinkOpen','水槽'),('HomeSofa0','沙发'),('HomeCoffeeTable','茶几'),('HomeCurtain','窗帘')]
canvas=Image.new('RGB',(1200,934),'#eee8da'); draw=ImageDraw.Draw(canvas)
draw.text((24,12),'荒野探险家 · 家居系列',fill='#36453e',font=font)
for i,(name,label) in enumerate(names):
    with Image.open(source/(name+'-front.png')) as im:
        canvas.paste(im.convert('RGB').resize((390,390),Image.Resampling.LANCZOS),((i%3)*400+5,(i//3)*430+56))
    draw.text(((i%3)*400+18,(i//3)*430+450),label,fill='#36453e',font=font)
draw.text((24,908),'Blender 实际模型预览 · 非游戏实机截图',fill='#59665b',font=small)
target=ROOT/'docs/assets/home-furniture/overview.png'; canvas.save(target)
allnames=sorted(source.glob('Home*-front.png'))
sheet=Image.new('RGB',(1280,1440),'#eee8da'); draw=ImageDraw.Draw(sheet)
for i,path in enumerate(allnames):
    with Image.open(path) as im: sheet.paste(im.convert('RGB').resize((316,316)),((i%4)*320,(i//4)*360))
    draw.text(((i%4)*320+5,(i//4)*360+322),path.stem.replace('-front',''),fill='#36453e',font=small)
sheet.save(source/'all-variants.png')
print(target)
