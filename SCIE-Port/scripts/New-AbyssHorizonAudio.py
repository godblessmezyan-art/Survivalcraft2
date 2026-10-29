"""Generate original, deterministic, one-second mono horizon cues; no sampled audio."""
import math
import random
import struct
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / 'src/SCIEPort/Assets/Audio/Abyss'
RATE = 22050

def synth(name, seed):
    rng = random.Random(seed)
    previous = 0.0
    samples = bytearray()
    phase = 0.0
    for n in range(RATE):
        t = n / RATE
        noise = rng.uniform(-1.0, 1.0)
        previous = .93 * previous + .07 * noise
        if name == 'Wake':
            frequency = 42 + 28*t
            envelope = math.sin(math.pi*t)**1.6
            value = .72*math.sin(phase) + .6*previous
        elif name == 'Rift':
            frequency = 190 - 120*t
            envelope = math.sin(math.pi*t)**1.25
            value = .38*math.sin(phase) + 1.5*previous + .13*noise
        elif name == 'Collapse':
            frequency = 110 - 75*t
            envelope = math.sin(math.pi*t)**1.5
            value = .65*math.sin(phase) + .8*previous
        else:
            frequency = 85 - 48*t
            envelope = math.exp(-5*t) * min(1,t*80) * min(1,(1-t)*25)
            value = .85*math.sin(phase) + 1.1*previous + .18*noise
        phase += 2*math.pi*frequency/RATE
        sample = max(-1.0,min(1.0,value*envelope*.65))
        samples.extend(struct.pack('<h', round(sample*32767)))
    ROOT.mkdir(parents=True, exist_ok=True)
    with wave.open(str(ROOT / ('Horizon'+name+'.wav')), 'wb') as target:
        target.setnchannels(1)
        target.setsampwidth(2)
        target.setframerate(RATE)
        target.writeframes(samples)

if __name__ == '__main__':
    for index, cue in enumerate(('Wake','Rift','Collapse','Burst')):
        synth(cue, 501+index)
