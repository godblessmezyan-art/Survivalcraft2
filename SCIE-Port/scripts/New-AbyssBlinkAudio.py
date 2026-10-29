"""Original synthesized, deterministic void-shift cues, mono PCM, no sampled audio."""
import math
import random
import struct
import wave
from pathlib import Path
RATE = 22050
ROOT = Path(__file__).resolve().parents[1] / 'src/SCIEPort/Assets/Audio/Abyss'
for name, seed in [('Shift', 713), ('Hunt', 714)]:
    rng = random.Random(seed)
    samples = bytearray()
    phase = 0.0
    smooth = 0.0
    for i in range(RATE):
        t = i / RATE
        noise = rng.uniform(-1, 1)
        smooth = .88*smooth + .12*noise
        frequency = (245 if name == 'Shift' else 155) * (1-.75*t) + 30
        phase += 2*math.pi*frequency/RATE
        envelope = math.sin(math.pi*t)**1.4 * math.exp(-2.5*t)
        sample = (.42*math.sin(phase) + .8*smooth + .16*noise) * envelope * .75
        samples.extend(struct.pack('<h', int(max(-1,min(1,sample))*32767)))
    ROOT.mkdir(parents=True,exist_ok=True)
    with wave.open(str(ROOT / ('Blink'+name+'.wav')), 'wb') as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(RATE)
        wav.writeframes(samples)
