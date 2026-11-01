"""Preserve the image_gen original; resize and circular-mask only. Never changes skill 1 art."""
from pathlib import Path
from PIL import Image, ImageDraw
import shutil,hashlib,json,sys
root=Path(__file__).resolve().parents[1];folder=root/'docs/assets/excalibur-v2';folder.mkdir(parents=True,exist_ok=True)
source=folder/'avalon-original.png'
if len(sys.argv)>1:shutil.copy2(sys.argv[1],source)
im=Image.open(source).convert('RGBA').resize((256,256),Image.Resampling.LANCZOS)
mask=Image.new('L',(1024,1024));ImageDraw.Draw(mask).ellipse((8,8,1016,1016),fill=255);im.putalpha(mask.resize((256,256),Image.Resampling.LANCZOS))
asset=root/'src/SCIEPort/Assets/Textures/ExcaliburAvalon.png';im.save(asset)
manifest={'tool':'built-in image_gen','source':'avalon-original.png','sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest().upper(),'prompt':'avalon-prompt.txt','asset':asset.relative_to(root).as_posix(),'sha256':hashlib.sha256(asset.read_bytes()).hexdigest().upper(),'processing':'RGBA Lanczos resize + circular alpha only','unchangedSkill1Sha256':hashlib.sha256((root/'src/SCIEPort/Assets/Textures/ExcaliburLake.png').read_bytes()).hexdigest().upper()}
(folder/'avalon.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
