"""Validate the committed original DEX, source pin, closed class set and Android entry points."""
import hashlib
import json
from pathlib import Path
import struct
import zlib
root=Path(__file__).resolve().parents[1]
m=json.loads((root/'tools/codex-picker/manifest.json').read_text(encoding='utf-8'))
data=(root/'src/SCIEPort/Features/CelestialCodex/Android/DocumentPicker.dex').read_bytes()
assert hashlib.sha256(data).hexdigest()==m['dexSha256']
assert hashlib.sha256((root/m['source']).read_bytes()).hexdigest()==m['sourceSha256']
assert data[:8]==b'dex\n035\0' and len(data)==m['dexBytes'] and len(data)<128*1024
assert struct.unpack_from('<I',data,8)[0]==zlib.adler32(data[12:])&0xffffffff
assert data[12:32]==hashlib.sha1(data[32:]).digest()
def pair(at):return struct.unpack_from('<2I',data,at)
def u32(at):return struct.unpack_from('<I',data,at)[0]
def string(at):
    while data[at]&128:at+=1
    at+=1
    end=data.index(0,at)
    return data[at:end].decode('utf-8',errors='replace')
n,off=pair(56);strings=[string(u32(off+i*4)) for i in range(n)]
n,off=pair(64);types=[strings[u32(off+i*4)] for i in range(n)]
n,off=pair(96);classes=[types[u32(off+i*32)] for i in range(n)]
assert len(classes)==5 and all(c.startswith('Lorg/scieport/codex/CodexDocumentPicker') for c in classes),classes
n,off=pair(88);methods=[]
for i in range(n):
    cls,proto,name=struct.unpack_from('<HHI',data,off+i*8)
    methods.append((types[cls],strings[name]))
for name in ['start','cancel','getState','getPath','getError','onActivityResult','copyLimited','validateName']:
    assert ('Lorg/scieport/codex/CodexDocumentPicker;',name) in methods,name
for s in ['android.intent.action.OPEN_DOCUMENT','android.intent.category.OPENABLE','_display_name']:
    assert s in strings,s
assert not any(c.startswith(('Landroid/','Ljava/','Landroidx/')) for c in classes)
print('Codex picker original DEX/source/SHA1/Adler32/SAF checks passed:',len(data),'bytes,',len(classes),'original classes')
