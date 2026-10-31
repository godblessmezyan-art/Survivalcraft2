"""Only crop, resize and circular-mask the image_gen atlas; do not redraw its artwork."""
from pathlib import Path
from PIL import Image, ImageDraw
import hashlib,json,shutil,sys
root=Path(__file__).resolve().parents[1];design=root/'docs/assets/excalibur'
source=design/'icons-original.png'
if len(sys.argv)>1:shutil.copy2(sys.argv[1],source)
im=Image.open(source).convert('RGBA');w,h=im.size;icons={}
for i,name in enumerate(('ExcaliburLake','ExcaliburBlessing','ExcaliburRelease')):
    icon=im.crop((round(i*w/3),0,round((i+1)*w/3),h)).resize((256,256),Image.Resampling.LANCZOS)
    mask=Image.new('L',(1024,1024));ImageDraw.Draw(mask).ellipse((8,8,1016,1016),fill=255)
    icon.putalpha(mask.resize((256,256),Image.Resampling.LANCZOS))
    path=root/f'src/SCIEPort/Assets/Textures/{name}.png';icon.save(path)
    icons[name]={'asset':path.relative_to(root).as_posix(),'sha256':hashlib.sha256(path.read_bytes()).hexdigest().upper()}
manifest={'tool':'built-in image_gen','source':'icons-original.png','sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest().upper(),'prompt':'icons-prompt.txt','processing':'3 equal crops, Lanczos resize, antialiased circular alpha only','icons':icons}
(design/'icons.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
