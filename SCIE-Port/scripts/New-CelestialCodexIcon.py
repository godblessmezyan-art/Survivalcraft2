"""Generate the original 32px book icon using only the Python standard library."""
from pathlib import Path
import struct
import zlib

size = 32
pixels = [[(0, 0, 0, 0) for _ in range(size)] for _ in range(size)]

def rect(x1, y1, x2, y2, color):
    for y in range(y1, y2 + 1):
        for x in range(x1, x2 + 1):
            pixels[y][x] = (*color, 255)

rect(5, 3, 26, 28, (48, 31, 40))
rect(7, 4, 25, 27, (210, 171, 91))
rect(8, 6, 24, 24, (35, 65, 90))
rect(5, 4, 8, 26, (77, 52, 59))
rect(6, 6, 7, 23, (160, 116, 64))
rect(9, 25, 24, 26, (239, 222, 180))
rect(10, 26, 23, 26, (177, 148, 108))
rect(10, 8, 22, 8, (161, 133, 80))
rect(10, 22, 22, 22, (161, 133, 80))
rect(16, 10, 16, 20, (247, 217, 133))
rect(11, 15, 21, 15, (247, 217, 133))
rect(14, 13, 18, 17, (247, 217, 133))
rect(15, 14, 17, 16, (255, 245, 202))
rect(21, 24, 22, 29, (151, 57, 60))

def chunk(kind, data):
    return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data) & 0xffffffff)

raw = b''.join(b'\0' + bytes(channel for pixel in row for channel in pixel) for row in pixels)
png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', size, size, 8, 6, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b'')
target = Path(__file__).resolve().parent.parent / 'src/SCIEPort/Assets/Textures/CelestialCodex/Book.png'
target.parent.mkdir(parents=True, exist_ok=True)
target.write_bytes(png)
print(target)
