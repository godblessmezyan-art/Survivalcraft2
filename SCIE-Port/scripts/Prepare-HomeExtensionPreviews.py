"""Contact sheets of actual Blender renders; no synthesized/retouched product views."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
ROOT=Path(__file__).resolve().parents[1]
source=ROOT/'artifacts/home-extension'; target=ROOT/'docs/assets/home-extension'
target.mkdir(parents=True,exist_ok=True)
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',24)
small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
families=[('HomeFireplaceOn','壁炉'),('HomeDiningSet','双人餐桌'),('HomeDoubleBed','双人床'),('HomeNightstand','床头柜'),('HomeWallArt0','挂画 · 山月'),('HomeWallArt2','挂画 · 轨迹')]
sheet=Image.new('RGB',(1200,950),'#E5EAEE'); draw=ImageDraw.Draw(sheet)
draw.text((24,12),'荒野探险家 · 现代科技家居 · 新增五类',fill='#303943',font=font)
for i,(name,label) in enumerate(families):
    with Image.open(source/(name+'-front.png')) as im: sheet.paste(im.convert('RGB').resize((390,390),Image.Resampling.LANCZOS),((i%3)*400+5,(i//3)*430+54))
    draw.text(((i%3)*400+15,(i//3)*430+449),label,fill='#303943',font=font)
draw.text((24,919),'Blender 实际模型预览 · 未构建，非游戏实机截图',fill='#546C80',font=small)
sheet.save(target/'overview.png')
names=['HomeFireplace','HomeFireplaceOn','HomeDiningSet','HomeDoubleBed','HomeNightstand','HomeWallArt0','HomeWallArt1','HomeWallArt2']
sheet=Image.new('RGB',(1280,720),'#E5EAEE'); draw=ImageDraw.Draw(sheet)
for i,name in enumerate(names):
    with Image.open(source/(name+'-front.png')) as im: sheet.paste(im.convert('RGB').resize((316,316)),((i%4)*320,(i//4)*360))
    draw.text(((i%4)*320+5,(i//4)*360+322),name,fill='#303943',font=small)
sheet.save(source/'all-variants.png')
sheet=Image.new('RGB',(1200,880),'#E5EAEE'); draw=ImageDraw.Draw(sheet)
for i,(name,label) in enumerate(families[:5]):
    with Image.open(source/(name+'-back.png')) as im: sheet.paste(im.convert('RGB').resize((390,390)),((i%3)*400+5,(i//3)*440))
    draw.text(((i%3)*400+15,(i//3)*440+398),label+' / 背面',fill='#303943',font=small)
sheet.save(source/'rear-audit.png')
print(target/'overview.png')
